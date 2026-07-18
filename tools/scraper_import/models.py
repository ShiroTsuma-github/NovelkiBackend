from __future__ import annotations

from dataclasses import dataclass, field
from decimal import Decimal
from pathlib import Path


@dataclass(frozen=True)
class MetadataRecord:
    name: str
    description: str | None


@dataclass(frozen=True)
class AuthorRecord:
    primary_name: str
    other_names: tuple[str, ...]

    @property
    def all_names(self) -> tuple[str, ...]:
        return (self.primary_name, *self.other_names)


@dataclass(frozen=True)
class ReviewIssue:
    source: str
    row_number: int | None
    title: str | None
    field: str
    value: str | None
    reason: str


@dataclass(frozen=True)
class BookRecord:
    source: str
    row_number: int | None
    primary_title: str
    content_type: str
    status: str
    author_key: str | None
    alternative_titles: tuple[str, ...]
    genres: tuple[str, ...]
    tags: tuple[str, ...]
    total_chapters: Decimal | None
    current_chapter_number: Decimal | None
    current_chapter_label: str | None
    rating: int | None
    priority: int | None
    description: str | None
    notes: str | None
    raw_imported_line: str | None
    requested_url: str | None
    cover_path: Path | None
    cover_mime_type: str | None


@dataclass
class Catalog:
    tags: dict[str, MetadataRecord] = field(default_factory=dict)
    genres: dict[str, MetadataRecord] = field(default_factory=dict)
    authors: list[AuthorRecord] = field(default_factory=list)
    books: list[BookRecord] = field(default_factory=list)
    issues: list[ReviewIssue] = field(default_factory=list)
    scanned: int = 0
    skipped: int = 0


def collapse_whitespace(value: str) -> str:
    return " ".join(value.split())


def normalized(value: str) -> str:
    return collapse_whitespace(value).casefold()
