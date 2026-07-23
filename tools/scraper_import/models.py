from __future__ import annotations

from dataclasses import dataclass, field
from decimal import Decimal
from pathlib import Path
import unicodedata


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


def metadata_match_key(value: str) -> str:
    decomposed = unicodedata.normalize("NFD", collapse_whitespace(value))
    return "".join(
        character.upper()
        for character in decomposed
        if not unicodedata.combining(character) and character.isalnum()
    )


def metadata_match_distance(left: str, right: str, cutoff: int | None = None) -> int:
    left_key = metadata_match_key(left)
    right_key = metadata_match_key(right)
    if len(left_key) > len(right_key):
        left_key, right_key = right_key, left_key
    maximum = cutoff if cutoff is not None else max(len(left_key), len(right_key))
    if len(right_key) - len(left_key) > maximum:
        return maximum + 1
    previous = list(range(len(left_key) + 1))
    for right_index, right_character in enumerate(right_key, start=1):
        current = [right_index]
        row_minimum = right_index
        for left_index, left_character in enumerate(left_key, start=1):
            current.append(
                min(
                    current[-1] + 1,
                    previous[left_index] + 1,
                    previous[left_index - 1] + (left_character != right_character),
                )
            )
            row_minimum = min(row_minimum, current[-1])
        if cutoff is not None and row_minimum > cutoff:
            return cutoff + 1
        previous = current
    return previous[-1]


def metadata_names_match(left: str, right: str) -> bool:
    left_key = metadata_match_key(left)
    right_key = metadata_match_key(right)
    if not left_key or not right_key:
        return False
    if left_key == right_key:
        return True
    minimum_length = min(len(left_key), len(right_key))
    maximum_length = max(len(left_key), len(right_key))
    if minimum_length < 8:
        return False
    maximum_distance = 2 if maximum_length >= 16 else 1
    if maximum_length - minimum_length > maximum_distance:
        return False
    distance = metadata_match_distance(left, right, maximum_distance)
    return distance <= maximum_distance and 1 - distance / maximum_length >= 0.9
