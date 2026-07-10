from __future__ import annotations

import csv
import os
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any
from urllib.parse import quote_plus
from uuid import UUID

try:
    import psycopg
except ImportError:
    psycopg = None


ROOT = Path(__file__).resolve().parents[1]
ENV_PATH = ROOT / ".env"
USER_ID = "019f31b5-b49e-78ed-828d-c6dc62a0808c"
OUTPUT_PATH = ROOT / "books_import.csv"
FIELDNAMES = [
    "primaryTitle",
    "authorName",
    "contentType",
    "status",
    "tags",
    "totalChapters",
    "currentChapterNumber",
    "currentChapterLabel",
    "rating",
    "priority",
    "description",
    "notes",
    "rawImportedLine",
]


def main() -> int:
    if psycopg is None:
        print(
            "Missing dependency: install `psycopg[binary]` or `psycopg` before running this script.",
            file=sys.stderr,
        )
        return 1

    try:
        user_id = str(UUID(USER_ID))
    except ValueError:
        print(f"Invalid USER_ID constant: {USER_ID}", file=sys.stderr)
        return 1

    connection_string = resolve_connection_string()
    if not connection_string:
        print(
            "DB connection string not found. Set DB_CONNECTION_STRING in .env or environment.",
            file=sys.stderr,
        )
        return 1

    with psycopg.connect(normalize_connection_string(connection_string)) as connection:
        books = fetch_books(connection, user_id)
        if not books:
            print(f"No books found for user {user_id}.", file=sys.stderr)
            return 1

        tags = fetch_tags(connection, [book["id"] for book in books])

    with OUTPUT_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDNAMES)
        writer.writeheader()
        for book in books:
            writer.writerow(build_row(book, tags))

    print(f"Exported {len(books)} books to {OUTPUT_PATH}")
    return 0


def resolve_connection_string() -> str | None:
    env_value = os.getenv("DB_CONNECTION_STRING")
    if env_value:
        return env_value

    if not ENV_PATH.exists():
        return None

    for line in ENV_PATH.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in stripped:
            continue
        key, value = stripped.split("=", 1)
        if key.strip() == "DB_CONNECTION_STRING":
            return value.strip().strip('"').strip("'")

    return None


def normalize_connection_string(value: str) -> str:
    stripped = value.strip()
    if "://" in stripped:
        return stripped

    parts: dict[str, str] = {}
    for segment in stripped.split(";"):
        item = segment.strip()
        if not item or "=" not in item:
            continue
        key, raw_value = item.split("=", 1)
        parts[key.strip().lower()] = raw_value.strip()

    if not parts:
        return stripped

    host = parts.get("host", "localhost")
    port = parts.get("port", "5432")
    database = parts.get("database") or parts.get("dbname") or parts.get("initial catalog", "")
    username = parts.get("username") or parts.get("user id") or parts.get("userid") or parts.get("user", "")
    password = parts.get("password", "")

    query_parts: list[str] = []
    ssl_mode = parts.get("ssl mode") or parts.get("sslmode")
    if ssl_mode:
        query_parts.append(f"sslmode={quote_plus(ssl_mode)}")

    query = f"?{'&'.join(query_parts)}" if query_parts else ""
    return (
        f"postgresql://{quote_plus(username)}:{quote_plus(password)}"
        f"@{host}:{port}/{quote_plus(database)}{query}"
    )


def fetch_books(connection: Any, user_id: str) -> list[dict[str, Any]]:
    query = """
        SELECT
            b."Id" AS id,
            b."PrimaryTitle" AS primary_title,
            a."PrimaryName" AS author_name,
            ct."Name" AS content_type,
            s."Name" AS status,
            b."TotalChapters" AS total_chapters,
            b."CurrentChapterNumber" AS current_chapter_number,
            b."CurrentChapterLabel" AS current_chapter_label,
            b."Rating" AS rating,
            b."Priority" AS priority,
            b."Description" AS description,
            b."Notes" AS notes,
            b."RawImportedLine" AS raw_imported_line
        FROM "Books" b
        JOIN "ContentTypes" ct ON ct."Id" = b."ContentTypeId"
        JOIN "Statuses" s ON s."Id" = b."StatusId"
        LEFT JOIN "Authors" a ON a."Id" = b."AuthorId"
        WHERE b."OwnerId" = %s
        ORDER BY lower(b."PrimaryTitle"), b."Id"
    """
    with connection.cursor(row_factory=psycopg.rows.dict_row) as cursor:
        cursor.execute(query, (user_id,))
        return list(cursor.fetchall())


def fetch_tags(connection: Any, book_ids: list[Any]) -> dict[str, list[str]]:
    query = """
        SELECT
            bt."BookId" AS book_id,
            t."Name" AS tag_name
        FROM "BookTag" bt
        JOIN "Tags" t ON t."Id" = bt."TagId"
        WHERE bt."BookId" = ANY(%s)
        ORDER BY bt."BookId", lower(t."Name"), t."Id"
    """
    grouped: dict[str, list[str]] = defaultdict(list)
    with connection.cursor(row_factory=psycopg.rows.dict_row) as cursor:
        cursor.execute(query, (book_ids,))
        for row in cursor.fetchall():
            tag_name = row["tag_name"]
            if tag_name is None:
                continue
            grouped[str(row["book_id"])].append(str(tag_name).strip())
    return dict(grouped)


def build_row(book: dict[str, Any], tags: dict[str, list[str]]) -> dict[str, str]:
    book_id = str(book["id"])
    return {
        "primaryTitle": string_or_empty(book["primary_title"]),
        "authorName": string_or_empty(book["author_name"]),
        "contentType": string_or_empty(book["content_type"]),
        "status": string_or_empty(book["status"]),
        "tags": "; ".join(tags.get(book_id, [])),
        "totalChapters": format_number(book["total_chapters"]),
        "currentChapterNumber": format_number(book["current_chapter_number"]),
        "currentChapterLabel": string_or_empty(book["current_chapter_label"]),
        "rating": string_or_empty(book["rating"]),
        "priority": string_or_empty(book["priority"]),
        "description": string_or_empty(book["description"]),
        "notes": normalize_newlines(book["notes"]),
        "rawImportedLine": string_or_empty(book["raw_imported_line"]),
    }


def normalize_newlines(value: Any) -> str:
    if value is None:
        return ""
    return str(value).replace("\r\n", "\n").replace("\r", "\n").strip()


def string_or_empty(value: Any) -> str:
    if value is None:
        return ""
    return str(value)


def format_number(value: Any) -> str:
    if value is None:
        return ""
    normalized = format(value, "f") if hasattr(value, "as_tuple") else str(value)
    if "." not in normalized:
        return normalized
    normalized = normalized.rstrip("0").rstrip(".")
    return normalized or "0"


if __name__ == "__main__":
    raise SystemExit(main())
