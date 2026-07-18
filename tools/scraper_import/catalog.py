from __future__ import annotations

import csv
import json
import re
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any, Iterable

from .models import (
    AuthorRecord,
    BookRecord,
    Catalog,
    MetadataRecord,
    ReviewIssue,
    collapse_whitespace,
    normalized,
)


ROW_DIRECTORY = re.compile(r"^(?P<row>\d{5})-")


def load_catalog(storage_dir: Path, corrected_csv: Path) -> Catalog:
    rows = _read_corrected_rows(corrected_csv)
    catalog = Catalog()
    author_rows: list[tuple[str, ...]] = []

    for details_path in sorted(storage_dir.rglob("details.json")):
        catalog.scanned += 1
        source = str(details_path.parent.relative_to(storage_dir))
        try:
            details = json.loads(details_path.read_text(encoding="utf-8"))
        except (OSError, UnicodeError, json.JSONDecodeError) as error:
            catalog.skipped += 1
            _issue(catalog, source, None, None, "details", None, f"invalid details.json: {error}")
            continue

        if details.get("status") != "ok":
            catalog.skipped += 1
            continue

        _collect_metadata(catalog, source, details)
        names = _clean_strings(details.get("authors") or [])
        if names:
            author_rows.append(tuple(names))

        row_number, row = _corrected_row(details_path, storage_dir, rows)
        if row is None:
            catalog.skipped += 1
            _issue(
                catalog,
                source,
                row_number,
                _clean(details.get("title")),
                "correctedCsv",
                None,
                "no unambiguous row in KSIAZKI_POPRAWIONE.csv; book not imported",
            )
            continue

        book = _make_book(catalog, source, row_number, row, details, details_path.parent)
        if book is None:
            catalog.skipped += 1
        else:
            catalog.books.append(book)

    catalog.authors = _merge_authors(catalog, author_rows)
    return catalog


def write_review_report(path: Path, issues: Iterable[ReviewIssue]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=("source", "rowNumber", "title", "field", "value", "reason"),
            delimiter=";",
        )
        writer.writeheader()
        for issue in issues:
            writer.writerow(
                {
                    "source": issue.source,
                    "rowNumber": issue.row_number,
                    "title": issue.title,
                    "field": issue.field,
                    "value": issue.value,
                    "reason": issue.reason,
                }
            )
    temporary.replace(path)


def _read_corrected_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle, delimiter=";")
        if not reader.fieldnames or "primaryTitle" not in reader.fieldnames:
            raise ValueError(f"Unsupported corrected CSV header: {path}")
        return [dict(row) for row in reader]


def _corrected_row(
    details_path: Path, storage_dir: Path, rows: list[dict[str, str]]
) -> tuple[int | None, dict[str, str] | None]:
    if details_path.parent.parent != storage_dir:
        return None, None
    match = ROW_DIRECTORY.match(details_path.parent.name)
    if match is None:
        return None, None
    row_number = int(match.group("row"))
    if row_number < 1 or row_number > len(rows):
        return row_number, None
    return row_number, rows[row_number - 1]


def _collect_metadata(catalog: Catalog, source: str, details: dict[str, Any]) -> None:
    for field, target in (
        ("tags", catalog.tags),
        ("languages", catalog.tags),
        ("genres", catalog.genres),
    ):
        for raw in details.get(field) or []:
            if not isinstance(raw, dict):
                continue
            name = _clean(raw.get("name"))
            if not name:
                continue
            key = normalized(name)
            existing = target.get(key)
            description = _clean(raw.get("description"))
            if existing is None or (not existing.description and description):
                target[key] = MetadataRecord(name, description)
            elif description and existing.description and description != existing.description:
                chosen = max((existing.description, description), key=len)
                target[key] = MetadataRecord(existing.name, chosen)
                _issue(catalog, source, None, name, f"{field}.description", description, "conflicting descriptions; kept the longer value")


def _make_book(
    catalog: Catalog,
    source: str,
    row_number: int | None,
    row: dict[str, str],
    details: dict[str, Any],
    artifact_dir: Path,
) -> BookRecord | None:
    primary_title = _clean(row.get("primaryTitle"))
    content_type = _clean(row.get("contentType")) or _clean(details.get("contentType"))
    status = _clean(row.get("status"))
    if not primary_title or not content_type or not status:
        _issue(catalog, source, row_number, primary_title, "required", None, "title, content type, or status is missing; book not imported")
        return None

    total = _decimal(catalog, source, row_number, primary_title, "totalChapters", row.get("totalChapters"), positive=True)
    current = _decimal(catalog, source, row_number, primary_title, "currentChapterNumber", row.get("currentChapterNumber"), positive=False)
    if total is not None and current is not None and current > total:
        _issue(catalog, source, row_number, primary_title, "totalChapters", str(total), "current chapter exceeds total; total left empty")
        total = None

    author_names = _clean_strings(details.get("authors") or [])
    alternative_titles = _dedupe(
        [details.get("title"), *(details.get("associatedTitles") or [])],
        excluded=(primary_title,),
    )
    genres = _dedupe(raw.get("name") for raw in details.get("genres") or [] if isinstance(raw, dict))
    scraper_tags = [raw.get("name") for raw in details.get("tags") or [] if isinstance(raw, dict)]
    csv_tags = re.split(r"\s*;\s*", row.get("tags") or "")
    tags = _dedupe([*scraper_tags, *csv_tags])
    description = _bounded_text(catalog, source, row_number, primary_title, "description", details.get("description"), 4000)
    notes = _join_notes(row.get("notes"), row.get("comment"))
    cover_path, cover_mime = _cover(catalog, source, row_number, primary_title, details, artifact_dir)

    return BookRecord(
        source=source,
        row_number=row_number,
        primary_title=primary_title,
        content_type=content_type,
        status=status,
        author_key=normalized(author_names[0]) if author_names else None,
        alternative_titles=alternative_titles,
        genres=genres,
        tags=tags,
        total_chapters=total,
        current_chapter_number=current,
        current_chapter_label=_bounded_text(catalog, source, row_number, primary_title, "currentChapterLabel", row.get("currentChapterLabel"), 100),
        rating=_integer(catalog, source, row_number, primary_title, "rating", row.get("rating"), 1, 10),
        priority=_integer(catalog, source, row_number, primary_title, "priority", row.get("priority"), 1, 5),
        description=description,
        notes=_bounded_text(catalog, source, row_number, primary_title, "notes", notes, 4000),
        raw_imported_line=_bounded_text(catalog, source, row_number, primary_title, "rawImportedLine", row.get("rawImportedLine"), 4000),
        requested_url=_clean(details.get("requestedUrl")),
        cover_path=cover_path,
        cover_mime_type=cover_mime,
    )


def _cover(catalog: Catalog, source: str, row_number: int | None, title: str, details: dict[str, Any], artifact_dir: Path) -> tuple[Path | None, str | None]:
    cover = details.get("cover")
    relative = _clean(cover.get("path")) if isinstance(cover, dict) else None
    if not relative:
        _issue(catalog, source, row_number, title, "cover", None, "scraper has no cover")
        return None, None
    base = artifact_dir.resolve()
    candidate = (artifact_dir / relative).resolve()
    if base not in candidate.parents or not candidate.is_file():
        _issue(catalog, source, row_number, title, "cover", relative, "cover path is invalid or missing")
        return None, None
    return candidate, _clean(cover.get("mimeType"))


def _merge_authors(catalog: Catalog, rows: list[tuple[str, ...]]) -> list[AuthorRecord]:
    groups: list[list[str]] = []
    for row in rows:
        names = list(_dedupe(row))
        keys = {normalized(name) for name in names}
        overlaps = [index for index, group in enumerate(groups) if keys & {normalized(name) for name in group}]
        if not overlaps:
            groups.append(names)
            continue
        first = overlaps[0]
        merged = list(_dedupe([*groups[first], *names]))
        for index in reversed(overlaps[1:]):
            merged = list(_dedupe([*merged, *groups[index]]))
            del groups[index]
        groups[first] = merged

    authors: list[AuthorRecord] = []
    for names in groups:
        if len(names) > 26:
            _issue(catalog, "authors", None, names[0], "otherNames", str(len(names) - 1), "more than 25 aliases; extra aliases left out")
            names = names[:26]
        authors.append(AuthorRecord(names[0], tuple(names[1:])))
    return sorted(authors, key=lambda author: normalized(author.primary_name))


def _decimal(catalog: Catalog, source: str, row: int | None, title: str, field: str, raw: Any, positive: bool) -> Decimal | None:
    value = _clean(raw)
    if not value:
        return None
    try:
        number = Decimal(value.replace(",", "."))
    except InvalidOperation:
        _issue(catalog, source, row, title, field, value, "not a decimal; left empty")
        return None
    if number < 0 or (positive and number == 0):
        _issue(catalog, source, row, title, field, value, "outside API range; left empty")
        return None
    return number


def _integer(catalog: Catalog, source: str, row: int | None, title: str, field: str, raw: Any, minimum: int, maximum: int) -> int | None:
    value = _clean(raw)
    if not value:
        return None
    try:
        number = int(value)
    except ValueError:
        _issue(catalog, source, row, title, field, value, "not an integer; left empty")
        return None
    if not minimum <= number <= maximum:
        _issue(catalog, source, row, title, field, value, "outside API range; left empty")
        return None
    return number


def _bounded_text(catalog: Catalog, source: str, row: int | None, title: str | None, field: str, raw: Any, limit: int) -> str | None:
    value = _clean(raw)
    if not value:
        return None
    if len(value) <= limit:
        return value
    _issue(catalog, source, row, title, field, value, f"exceeds API limit {limit}; truncated")
    return value[:limit]


def _join_notes(notes: Any, comment: Any) -> str | None:
    parts = [_clean(notes)]
    clean_comment = _clean(comment)
    if clean_comment:
        parts.append(f"Comment: {clean_comment}")
    return "\n".join(part for part in parts if part) or None


def _dedupe(values: Iterable[Any], excluded: Iterable[str] = ()) -> tuple[str, ...]:
    excluded_keys = {normalized(value) for value in excluded}
    result: list[str] = []
    seen = set(excluded_keys)
    for raw in values:
        value = _clean(raw)
        if not value or normalized(value) in seen:
            continue
        seen.add(normalized(value))
        result.append(value)
    return tuple(result)


def _clean_strings(values: Iterable[Any]) -> list[str]:
    return list(_dedupe(values))


def _clean(value: Any) -> str | None:
    if value is None:
        return None
    cleaned = collapse_whitespace(str(value))
    return cleaned or None


def _issue(catalog: Catalog, source: str, row: int | None, title: str | None, field: str, value: str | None, reason: str) -> None:
    catalog.issues.append(ReviewIssue(source, row, title, field, value, reason))
