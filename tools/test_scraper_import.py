from __future__ import annotations

import csv
import json
import tempfile
import unittest
from decimal import Decimal
from pathlib import Path
from typing import Any

from tools.scraper_import.catalog import load_catalog
from tools.scraper_import.models import BookRecord, Catalog, MetadataRecord
from tools.scraper_import.workflow import (
    ImportStatistics,
    _import_book,
    _resolve_genre_candidates,
    _tag_candidates,
)


class ScraperImportCatalogTests(unittest.TestCase):
    def test_manga_mode_excludes_novels_and_their_metadata(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            storage = root / "storage"
            storage.mkdir()
            corrected_csv = root / "corrected.csv"
            rows = [
                ("Novel Book", "Novel"),
                ("Manga Book", "Manga"),
                ("Manhwa Book", "Manhwa"),
                ("Manhua Book", "Manhua"),
            ]
            with corrected_csv.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(
                    handle,
                    fieldnames=("primaryTitle", "contentType", "status"),
                    delimiter=";",
                )
                writer.writeheader()
                for title, content_type in rows:
                    writer.writerow(
                        {
                            "primaryTitle": title,
                            "contentType": content_type,
                            "status": "Reading",
                        }
                    )

            for row_number, (title, content_type) in enumerate(rows, start=1):
                artifact = storage / f"{row_number:05d}-book-hash"
                artifact.mkdir()
                (artifact / "details.json").write_text(
                    json.dumps(
                        {
                            "status": "ok",
                            "contentType": content_type,
                            "title": title,
                            "authors": [f"{content_type} Author"],
                            "genres": [
                                {
                                    "name": f"{content_type} Genre",
                                    "description": "Genre description",
                                }
                            ],
                            "tags": [
                                {
                                    "name": f"{content_type} Tag",
                                    "description": "Tag description",
                                }
                            ],
                            "description": "Description",
                        }
                    ),
                    encoding="utf-8",
                )

            catalog = load_catalog(storage, corrected_csv, manga_only=True)

            self.assertEqual(
                ["Manga", "Manhwa", "Manhua"],
                [book.content_type for book in catalog.books],
            )
            self.assertNotIn("novel tag", catalog.tags)
            self.assertNotIn("novel genre", catalog.genres)
            self.assertNotIn(
                "Novel Author",
                [author.primary_name for author in catalog.authors],
            )

    def test_manhwa_payload_uses_anime_planet_as_metadata_source(self) -> None:
        api = RecordingApi()
        book = BookRecord(
            source="artifact",
            row_number=2,
            primary_title="Comic",
            content_type="Manhwa",
            status="Reading",
            author_key=None,
            alternative_titles=("Alternative",),
            genres=(),
            tags=(),
            total_chapters=Decimal("12"),
            current_chapter_number=Decimal("3"),
            current_chapter_label=None,
            rating=None,
            priority=None,
            description="Description",
            notes=None,
            raw_imported_line=None,
            requested_url="https://www.anime-planet.com/manga/comic",
            cover_path=None,
            cover_mime_type=None,
        )

        _import_book(
            api,
            Catalog(),
            ImportStatistics(),
            book,
            author_ids={},
            genre_ids={},
            dictionary_ids={
                ("type", "manhwa"): "type-id",
                ("status", "reading"): "status-id",
            },
            skip_covers=True,
            overwrite_covers=False,
        )

        self.assertEqual("Anime-Planet", api.payload["alternativeTitles"][0]["source"])
        self.assertEqual("Anime-Planet", api.payload["links"][0]["sourceType"])

    def test_details_title_becomes_primary_and_csv_title_becomes_alias(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            storage = root / "storage"
            artifact = storage / "00001-comic-hash"
            artifact.mkdir(parents=True)
            corrected_csv = root / "corrected.csv"
            corrected_csv.write_text(
                "primaryTitle;contentType;status\nOld Imported Title;Manga;Reading\n",
                encoding="utf-8",
            )
            (artifact / "details.json").write_text(
                json.dumps(
                    {
                        "status": "ok",
                        "contentType": "Manga",
                        "title": "Canonical Details Title",
                        "associatedTitles": ["Another Alias"],
                        "authors": [],
                        "genres": [],
                        "tags": [],
                        "description": "Description",
                    }
                ),
                encoding="utf-8",
            )

            catalog = load_catalog(storage, corrected_csv)

            book = catalog.books[0]
            self.assertEqual("Canonical Details Title", book.primary_title)
            self.assertEqual(
                ("Old Imported Title", "Another Alias"),
                book.alternative_titles,
            )

    def test_metadata_is_classified_against_api_genres_with_csharp_distance_rules(self) -> None:
        api = RecordingApi()
        catalog = Catalog(
            genres={
                "action": MetadataRecord("Action", None),
                "dungeon": MetadataRecord("Dungeon", "Dungeon description"),
            },
            tags={
                "sliceoflifee": MetadataRecord("SLICEOFLIFEE", None),
                "magic": MetadataRecord("Magic", "Magic description"),
            },
        )
        statistics = ImportStatistics()

        matches = _resolve_genre_candidates(api, catalog, statistics)
        tags = _tag_candidates(catalog, matches)

        self.assertEqual("genre-action", matches["action"])
        self.assertEqual("genre-slice", matches["sliceoflifee"])
        self.assertEqual({"dungeon", "magic"}, set(tags))


class RecordingApi:
    def __init__(self) -> None:
        self.payload: dict[str, Any] = {}

    def create_book(self, payload: dict[str, Any]) -> str:
        self.payload = payload
        return "book-id"

    def genres(self) -> list[dict[str, Any]]:
        return [
            {"id": "genre-action", "name": "Action"},
            {"id": "genre-slice", "name": "Slice Of Life"},
        ]


if __name__ == "__main__":
    unittest.main()
