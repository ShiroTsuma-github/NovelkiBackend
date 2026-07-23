from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal
from typing import Any, Callable

from .api import ApiError, NovelkiApi
from .models import (
    AuthorRecord,
    BookRecord,
    Catalog,
    MetadataRecord,
    ReviewIssue,
    metadata_match_distance,
    metadata_names_match,
    normalized,
)


Progress = Callable[[str], None]
MANGA_CONTENT_TYPES = {"manga", "manhwa", "manhua"}


@dataclass
class ImportStatistics:
    tags_created: int = 0
    tags_existing: int = 0
    genres_created: int = 0
    genres_existing: int = 0
    authors_created: int = 0
    authors_existing: int = 0
    books_created: int = 0
    books_existing: int = 0
    covers_uploaded: int = 0
    failed: int = 0


def run_import(
    api: NovelkiApi,
    catalog: Catalog,
    *,
    limit_books: int | None,
    skip_covers: bool,
    overwrite_covers: bool,
    progress: Progress = print,
) -> ImportStatistics:
    statistics = ImportStatistics()
    api.ensure_admin()

    progress("[1/4] Fetching genres and classifying metadata")
    genre_ids = _resolve_genre_candidates(api, catalog, statistics)
    tag_candidates = _tag_candidates(catalog, genre_ids)
    progress(f"[2/4] Global tags: {len(tag_candidates)}")
    _create_metadata(
        api.create_global_tag, tag_candidates, catalog, statistics, "tag", progress
    )

    progress(f"[3/4] Authors: {len(catalog.authors)}")
    author_ids = _create_authors(api, catalog, statistics, progress)
    dictionary_ids = _resolve_dictionaries(api, catalog, statistics)

    books = catalog.books if limit_books is None else catalog.books[:limit_books]
    progress(f"[4/4] Books: {len(books)}")
    for index, book in enumerate(books, start=1):
        progress(f"  [{index}/{len(books)}] {book.primary_title}")
        _import_book(
            api,
            catalog,
            statistics,
            book,
            author_ids,
            genre_ids,
            dictionary_ids,
            skip_covers,
            overwrite_covers,
        )
    return statistics


def _create_metadata(
    create: Callable[[str, str | None], dict[str, Any]],
    records: dict[str, MetadataRecord],
    catalog: Catalog,
    statistics: ImportStatistics,
    kind: str,
    progress: Progress,
) -> set[str]:
    available: set[str] = set()
    for index, (key, record) in enumerate(sorted(records.items()), start=1):
        if index == 1 or index % 100 == 0:
            progress(f"  metadata {index}/{len(records)}")
        if not record.description:
            _problem(catalog, statistics, kind, None, record.name, "description", None, "missing description; global entity not created")
            continue
        try:
            create(record.name, record.description)
            setattr(statistics, f"{kind}s_created", getattr(statistics, f"{kind}s_created") + 1)
            available.add(key)
        except ApiError as error:
            if error.status == 409:
                setattr(statistics, f"{kind}s_existing", getattr(statistics, f"{kind}s_existing") + 1)
                available.add(key)
            else:
                _problem(catalog, statistics, kind, None, record.name, kind, record.name, error.detail)
    return available


def _metadata_candidates(catalog: Catalog) -> dict[str, MetadataRecord]:
    result: dict[str, MetadataRecord] = {}
    for records in (catalog.genres, catalog.tags):
        for key, record in records.items():
            existing = result.get(key)
            if existing is None:
                result[key] = record
            elif not existing.description and record.description:
                result[key] = MetadataRecord(existing.name, record.description)
            elif existing.description and record.description and len(record.description) > len(existing.description):
                result[key] = MetadataRecord(existing.name, record.description)
    for book in catalog.books:
        for name in (*book.genres, *book.tags):
            key = normalized(name)
            result.setdefault(key, MetadataRecord(name, None))
    return result


def _resolve_genre_candidates(
    api: NovelkiApi,
    catalog: Catalog,
    statistics: ImportStatistics,
) -> dict[str, str]:
    available = [
        (str(item.get("id") or ""), str(item.get("name") or "").strip())
        for item in api.genres()
        if item.get("id") and str(item.get("name") or "").strip()
    ]
    result: dict[str, str] = {}
    for key, candidate in _metadata_candidates(catalog).items():
        match = next(
            (
                item
                for item in sorted(
                    available,
                    key=lambda item: (
                        metadata_match_distance(candidate.name, item[1]),
                        item[1].casefold(),
                    ),
                )
                if metadata_names_match(candidate.name, item[1])
            ),
            None,
        )
        if match is not None:
            result[key] = match[0]
    statistics.genres_existing = len(set(result.values()))
    return result


def _tag_candidates(
    catalog: Catalog,
    genre_ids: dict[str, str],
) -> dict[str, MetadataRecord]:
    return {
        key: record
        for key, record in _metadata_candidates(catalog).items()
        if key not in genre_ids
    }


def _create_authors(
    api: NovelkiApi,
    catalog: Catalog,
    statistics: ImportStatistics,
    progress: Progress,
) -> dict[str, str]:
    result: dict[str, str] = {}
    for index, author in enumerate(catalog.authors, start=1):
        if index == 1 or index % 50 == 0:
            progress(f"  authors {index}/{len(catalog.authors)}")
        response: dict[str, Any] | None = None
        try:
            response = api.create_author(author.primary_name, author.other_names)
            statistics.authors_created += 1
        except ApiError as error:
            if error.status != 409:
                _problem(catalog, statistics, "author", None, author.primary_name, "author", author.primary_name, error.detail)
                continue
            response = _find_author(api, author)
            if response is None:
                _problem(catalog, statistics, "author", None, author.primary_name, "author", author.primary_name, "conflict could not be resolved unambiguously")
                continue
            statistics.authors_existing += 1
            response = _merge_author_aliases(api, catalog, statistics, author, response)

        author_id = str(response.get("id") or "")
        if not author_id:
            _problem(catalog, statistics, "author", None, author.primary_name, "authorId", None, "API response did not contain id")
            continue
        for name in author.all_names:
            result[normalized(name)] = author_id
        for name in response.get("otherNames") or []:
            result[normalized(str(name))] = author_id
        result[normalized(str(response.get("primaryName") or author.primary_name))] = author_id
    return result


def _find_author(api: NovelkiApi, author: AuthorRecord) -> dict[str, Any] | None:
    matches: dict[str, dict[str, Any]] = {}
    requested = {normalized(name) for name in author.all_names}
    for name in author.all_names:
        for candidate in api.search_authors(name):
            candidate_names = {
                normalized(str(candidate.get("primaryName") or "")),
                *(normalized(str(alias)) for alias in candidate.get("otherNames") or []),
            }
            if requested & candidate_names and candidate.get("id"):
                matches[str(candidate["id"])] = candidate
    return next(iter(matches.values())) if len(matches) == 1 else None


def _merge_author_aliases(
    api: NovelkiApi,
    catalog: Catalog,
    statistics: ImportStatistics,
    requested: AuthorRecord,
    existing: dict[str, Any],
) -> dict[str, Any]:
    primary = str(existing.get("primaryName") or requested.primary_name)
    aliases = _unique(
        [*(str(value) for value in existing.get("otherNames") or []), *requested.all_names],
        excluded=(primary,),
    )
    if len(aliases) > 25:
        _problem(catalog, statistics, "author", None, primary, "otherNames", str(len(aliases)), "alias merge exceeds API limit; existing author kept unchanged")
        return existing
    existing_keys = {normalized(str(value)) for value in existing.get("otherNames") or []}
    if {normalized(value) for value in aliases} == existing_keys:
        return existing
    try:
        return api.update_author(str(existing["id"]), tuple(aliases))
    except (ApiError, KeyError) as error:
        _problem(catalog, statistics, "author", None, primary, "otherNames", None, f"could not merge aliases: {error}")
        return existing


def _resolve_dictionaries(api: NovelkiApi, catalog: Catalog, statistics: ImportStatistics) -> dict[tuple[str, str], str]:
    result: dict[tuple[str, str], str] = {}
    values = {
        *(('type', book.content_type) for book in catalog.books),
        *(('status', book.status) for book in catalog.books),
    }
    for dictionary, name in sorted(values):
        try:
            response = api.dictionary_by_name(dictionary, name)
            result[(dictionary, normalized(name))] = str(response["id"])
        except (ApiError, KeyError) as error:
            _problem(catalog, statistics, "dictionary", None, name, dictionary, name, str(error))
    return result


def _import_book(
    api: NovelkiApi,
    catalog: Catalog,
    statistics: ImportStatistics,
    book: BookRecord,
    author_ids: dict[str, str],
    genre_ids: dict[str, str],
    dictionary_ids: dict[tuple[str, str], str],
    skip_covers: bool,
    overwrite_covers: bool,
) -> None:
    metadata_source = (
        "Anime-Planet"
        if normalized(book.content_type) in MANGA_CONTENT_TYPES
        else "NovelUpdates"
    )
    author_id = author_ids.get(book.author_key) if book.author_key else None
    metadata_candidates = _unique([*book.genres, *book.tags], excluded=())
    matched_genre_ids = list(
        dict.fromkeys(
            genre_ids[normalized(name)]
            for name in metadata_candidates
            if normalized(name) in genre_ids
        )
    )
    tags = [name for name in metadata_candidates if normalized(name) not in genre_ids]
    content_type_id = dictionary_ids.get(("type", normalized(book.content_type)))
    status_id = dictionary_ids.get(("status", normalized(book.status)))
    if (book.author_key and not author_id) or not content_type_id or not status_id:
        reason = f"unresolved dependencies: author={bool(book.author_key and not author_id)}, type={not content_type_id}, status={not status_id}"
        _problem(catalog, statistics, book.source, book.row_number, book.primary_title, "dependencies", None, reason)
        return

    payload = {
        "primaryTitle": book.primary_title,
        "contentTypeId": content_type_id,
        "statusId": status_id,
        "authorId": author_id,
        "authorName": None,
        "alternativeTitles": [
            {"title": title, "language": None, "source": metadata_source}
            for title in book.alternative_titles
        ],
        "genreIds": matched_genre_ids,
        "tags": tags,
        "totalChapters": _number(book.total_chapters),
        "currentChapterNumber": _number(book.current_chapter_number),
        "currentChapterLabel": book.current_chapter_label,
        "rating": book.rating,
        "priority": book.priority,
        "description": book.description,
        "notes": book.notes,
        "rawImportedLine": book.raw_imported_line,
        "links": ([{"url": book.requested_url, "label": metadata_source, "sourceType": metadata_source, "isPrimary": True, "lastReadHere": False}] if book.requested_url else []),
    }
    created = True
    existing: dict[str, Any] | None = None
    try:
        book_id = api.create_book(payload)
        statistics.books_created += 1
    except ApiError as error:
        if error.status != 409:
            _problem(catalog, statistics, book.source, book.row_number, book.primary_title, "book", None, error.detail)
            return
        created = False
        existing = _find_book(api, book)
        if existing is None:
            _problem(catalog, statistics, book.source, book.row_number, book.primary_title, "book", None, "duplicate could not be resolved unambiguously")
            return
        book_id = str(existing["id"])
        statistics.books_existing += 1

    if skip_covers or book.cover_path is None:
        return
    cover_ready = (
        isinstance((existing or {}).get("cover"), dict)
        and (existing or {})["cover"].get("status") in {"Found", "Uploaded"}
    )
    if not created and cover_ready and not overwrite_covers:
        return
    if book.cover_path.stat().st_size > 10 * 1024 * 1024:
        _problem(catalog, statistics, book.source, book.row_number, book.primary_title, "cover", str(book.cover_path), "cover exceeds API 10 MiB limit")
        return
    try:
        api.upload_cover(book_id, book.cover_path, book.cover_mime_type)
        statistics.covers_uploaded += 1
    except (ApiError, OSError) as error:
        _problem(catalog, statistics, book.source, book.row_number, book.primary_title, "cover", str(book.cover_path), str(error))


def _find_book(api: NovelkiApi, book: BookRecord) -> dict[str, Any] | None:
    title_key = normalized(book.primary_title)
    matches = []
    for candidate in api.search_books(book.primary_title):
        titles = {normalized(str(candidate.get("primaryTitle") or ""))}
        titles.update(normalized(str(value)) for value in candidate.get("alternativeTitles") or [])
        if title_key in titles and normalized(str(candidate.get("contentType") or "")) == normalized(book.content_type):
            matches.append(candidate)
    return matches[0] if len(matches) == 1 else None


def _number(value: Decimal | None) -> int | float | None:
    if value is None:
        return None
    return int(value) if value == value.to_integral_value() else float(value)


def _unique(values: list[str], excluded: tuple[str, ...]) -> list[str]:
    seen = {normalized(value) for value in excluded}
    result = []
    for value in values:
        if not value.strip() or normalized(value) in seen:
            continue
        seen.add(normalized(value))
        result.append(" ".join(value.split()))
    return result


def _problem(
    catalog: Catalog,
    statistics: ImportStatistics,
    source: str,
    row_number: int | None,
    title: str | None,
    field: str,
    value: str | None,
    reason: str,
) -> None:
    statistics.failed += 1
    catalog.issues.append(ReviewIssue(source, row_number, title, field, value, reason))
