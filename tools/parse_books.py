from __future__ import annotations

import csv
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BOOKS_PATH = ROOT / "notes/books.txt"
CSV_PATH = ROOT / "books_import.csv"
UNCERTAIN_PATH = ROOT / "books_uncertain.txt"

TYPE_NOVEL = "Novel"
TYPE_MANGA = "Manga"
TYPE_MANHWA = "Manhwa"

STATUS_READING = "Reading"
STATUS_COMPLETED = "Completed"
STATUS_UNKNOWN = "Unknown"

LABEL_PATTERNS = [
    re.compile(r"\bvol\.\d+\s+Epilogue\b", re.IGNORECASE),
    re.compile(r"\bb\d+e\d+\b", re.IGNORECASE),
    re.compile(r"\bv\d+c\d+(?:p\d+)?\b", re.IGNORECASE),
    re.compile(r"\bch\d+(?:p\d+)?\b", re.IGNORECASE),
    re.compile(r"\b(?:v|V)\d+\b"),
    re.compile(r"\b(?:p|P)\d+\b"),
    re.compile(r"\b(?:ex|Ex|EX)\d+\b"),
    re.compile(r"\b(?:B|b)\d+\b"),
]

PURE_NUMERIC_RE = re.compile(r"^\d+(?:\.\d+)?$")
FREQUENCY_RE = re.compile(r"(?<!\S)~(?=[0-9?])([^\s,)]+)")
PAREN_RE = re.compile(r"\(([^()]*)\)")
PREFIX_RE = re.compile(r"^\s*(\d+(?:\.\d+){0,2})\.?\s*(.*)$")
TRAILING_NUMBER_RE = re.compile(r"^(.*?)(?:\s+(\d+(?:\.\d+)?))?\s*$")


def detect_content_type(index_text: str) -> tuple[str, list[str]]:
    if index_text.count(".") == 2:
        return TYPE_MANHWA, ["h-manhwa"]
    if index_text.count(".") == 1 and index_text.startswith("1."):
        return TYPE_NOVEL, []
    return TYPE_MANGA, []


def normalize_spaces(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def extract_labels(text: str) -> tuple[str, list[str]]:
    labels: list[str] = []
    working = text
    for pattern in LABEL_PATTERNS:
        for match in pattern.finditer(working):
            labels.append(match.group(0))
        working = pattern.sub(" ", working)
    return normalize_spaces(working), labels


def rating_from_count(count: int) -> int | None:
    if count <= 0:
        return None
    if count == 1:
        return 8
    if count == 2:
        return 9
    return 10


def determine_status(raw: str) -> tuple[str, bool, str | None]:
    has_check = "✓" in raw
    has_cross = "❌" in raw
    has_question = "?" in raw
    ambiguous = False
    note = None
    if has_check and has_cross:
        note = "chyba autor porzucil"
        if has_question:
            note = "chyba skonczone | chyba autor porzucil"
        return STATUS_UNKNOWN, False, note
    if has_check and has_question:
        note = "chyba skonczone"
        return STATUS_COMPLETED, ambiguous, note
    if has_question and has_cross:
        ambiguous = True
        return STATUS_UNKNOWN, ambiguous, note
    if has_check:
        return STATUS_COMPLETED, ambiguous, note
    if has_cross:
        return STATUS_UNKNOWN, False, "chyba autor porzucil"
    return STATUS_READING, ambiguous, note


def parse_line(raw_line: str) -> tuple[dict[str, str], list[str]] | None:
    stripped = raw_line.replace("\ufeff", "").strip()
    if not stripped or stripped.startswith("[[["):
        return None
    if stripped.lower() == "webtoony 🌟 🌟 🌟".lower():
        return None

    prefix_match = PREFIX_RE.match(stripped)
    if not prefix_match:
        return None

    index_text = prefix_match.group(1)
    body = normalize_spaces(prefix_match.group(2))
    content_type, base_tags = detect_content_type(index_text)

    uncertain: list[str] = []
    status, ambiguous_status, status_note = determine_status(raw_line)
    if ambiguous_status:
        uncertain.append("ambiguous status markers")

    priority = sum(raw_line.count(ch) for ch in ("🌟", "⭐"))
    rating_count = raw_line.count("💯")
    rating = rating_from_count(rating_count)
    notes_parts: list[str] = []
    if status_note:
        notes_parts.append(status_note)
    freq_matches = FREQUENCY_RE.findall(body)
    if freq_matches:
        filtered = ["~" + item.rstrip(",") for item in freq_matches if item.rstrip(",") != "?"]
        if filtered:
            notes_parts.append("release_frequency=" + ", ".join(filtered))
    body = FREQUENCY_RE.sub(" ", body)

    # Remove known emoji/status markers from the parsing surface.
    body = re.sub(r"[✓❌🌟⭐💯💩]", " ", body)
    body = re.sub(r"(?<!\S)\?(?!\S)", " ", body)
    body = normalize_spaces(body)

    parenthetical_items = PAREN_RE.findall(body)
    body_without_parens = PAREN_RE.sub(" ", body)

    total_chapters = ""
    labels: list[str] = []

    for item in parenthetical_items:
        normalized = normalize_spaces(item)
        if not normalized:
            continue
        if PURE_NUMERIC_RE.fullmatch(normalized):
            if total_chapters:
                uncertain.append(f"multiple numeric totals: {total_chapters}, {normalized}")
            total_chapters = normalized
            continue
        remaining, extracted = extract_labels(normalized)
        if extracted and not remaining:
            labels.extend(extracted)
            continue
        notes_parts.append(normalized)
    body_without_parens, inline_labels = extract_labels(body_without_parens)
    labels.extend(inline_labels)

    trailing_match = TRAILING_NUMBER_RE.match(body_without_parens)
    if not trailing_match:
        uncertain.append("could not split title and chapter number")
        title = body_without_parens
        current_chapter = ""
    else:
        title = normalize_spaces(trailing_match.group(1))
        current_chapter = trailing_match.group(2) or ""

    if not title:
        uncertain.append("empty parsed title")

    if title.endswith(" 32 Star") and not current_chapter:
        title = title[:-8].strip()
        current_chapter = "32"
        uncertain = [reason for reason in uncertain if reason != "missing standalone chapter count"]

    if not current_chapter and not total_chapters:
        uncertain.append("missing standalone chapter count")

    if stripped == "491. World's Apocalypse Online 80?":
        title = "World's Apocalypse Online"
        current_chapter = "80"
        if "not sure" not in notes_parts:
            notes_parts.append("not sure")
        uncertain = [reason for reason in uncertain if reason != "missing standalone chapter count"]

    if stripped == "1.1.10 Man Up,Girl! 17 (????????)":
        current_chapter = "17"
        notes_parts.append("????????")

    if "💩" in raw_line:
        poop_count = raw_line.count("💩")
        notes_parts.append(f"reaction_poop={poop_count}")
        poop_rating = max(1, 5 - poop_count)
        rating = poop_rating if rating is None else min(rating, poop_rating)

    if labels:
        deduped_labels: list[str] = []
        seen: set[str] = set()
        for label in labels:
            normalized_label = label.strip()
            lowered = normalized_label.lower()
            if lowered not in seen:
                seen.add(lowered)
                deduped_labels.append(normalized_label)
        current_label = "; ".join(deduped_labels)
        if len(deduped_labels) > 1:
            uncertain.append(f"multiple chapter labels: {current_label}")
    else:
        current_label = ""

    if status == STATUS_COMPLETED and total_chapters and current_chapter != total_chapters:
        current_chapter = total_chapters

    tags = list(base_tags)
    deduped_notes: list[str] = []
    seen_notes: set[str] = set()
    for note in notes_parts:
        normalized_note = note.strip()
        if not normalized_note:
            continue
        lowered = normalized_note.lower()
        if lowered not in seen_notes:
            seen_notes.add(lowered)
            deduped_notes.append(normalized_note)

    record = {
        "primaryTitle": title,
        "contentType": content_type,
        "status": status,
        "contentTypeId": "",
        "statusId": "",
        "authorId": "",
        "authorName": "",
        "alternativeTitles": "",
        "genreIds": "",
        "tags": "; ".join(tags),
        "totalChapters": total_chapters,
        "currentChapterNumber": current_chapter,
        "currentChapterLabel": current_label,
        "rating": "" if rating is None else str(rating),
        "priority": "" if priority == 0 else str(min(priority, 5)),
        "description": "",
        "comment": "",
        "notes": "\n".join(deduped_notes),
        "rawImportedLine": stripped,
        "links": "",
    }

    return record, uncertain


def main() -> None:
    records: list[dict[str, str]] = []
    uncertain_entries: list[str] = []

    for line_number, raw_line in enumerate(BOOKS_PATH.read_text(encoding="utf-8").splitlines(), start=1):
        parsed = parse_line(raw_line)
        if parsed is None:
            continue
        record, reasons = parsed
        records.append(record)
        if reasons:
            uncertain_entries.append(
                f"Line {line_number}: {record['rawImportedLine']}\n"
                f"  Parsed title: {record['primaryTitle']}\n"
                f"  Reasons: {', '.join(dict.fromkeys(reasons))}\n"
            )

    fieldnames = [
        "primaryTitle",
        "contentType",
        "status",
        "contentTypeId",
        "statusId",
        "authorId",
        "authorName",
        "alternativeTitles",
        "genreIds",
        "tags",
        "totalChapters",
        "currentChapterNumber",
        "currentChapterLabel",
        "rating",
        "priority",
        "description",
        "comment",
        "notes",
        "rawImportedLine",
        "links",
    ]

    with CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(records)

    header = [
        "Assumptions:",
        "- numbering: `X` -> Manga, `1.X` -> Novel, `1.1.X` -> Manhwa with tag `h-manhwa`",
        "- status: `✓` -> Completed, `✓?` -> Completed with note `chyba skonczone`, `✓❌` -> Unknown with note `chyba autor porzucil`",
        "- rating: `💯` -> 8, `💯💯` -> 9, `💯💯💯` or more -> 10",
        "- `💩` lowers rating further: one `💩` -> 4, two `💩` -> 3, etc.",
        "- priority: count of `⭐` and `🌟`, capped at 5",
        "- `~x` moved into notes as release frequency, `~?` ignored",
        "",
        "Entries requiring review:",
        "",
    ]
    UNCERTAIN_PATH.write_text("\n".join(header) + "\n".join(uncertain_entries), encoding="utf-8-sig")


if __name__ == "__main__":
    main()
