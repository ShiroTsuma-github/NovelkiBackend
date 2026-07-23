import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

from tools.novelupdates_scraper import (
    add_languages_to_details_tags,
    apply_details_to_row,
    backfill_content_types,
    backfill_language_tags,
    create_single_page_job,
    details_match_row_title,
    extract_anime_planet_details_from_html,
    extract_languages_from_html,
    find_skipped_artifacts,
    is_skipped_status,
    normalize_language_tags,
    ordered_scrape_jobs,
    prepare_links,
    read_rows,
    resolve_artifact_directory,
    wait_for_manual_page_correction,
    write_rows_atomic,
)
from t import make_details_link


class NovelUpdatesScraperTests(unittest.TestCase):
    def test_details_link_removes_apostrophes_without_splitting_the_word(self) -> None:
        self.assertEqual(
            "https://www.novelupdates.com/series/babys-toy/",
            make_details_link("Baby's Toy", "Novel"),
        )

    def test_manual_page_correction_supports_retry_and_temporary_skip(self) -> None:
        page = Mock(url="https://www.novelupdates.com/series/correct/")
        with patch("builtins.input", return_value="c"):
            self.assertFalse(wait_for_manual_page_correction(page, 10))

        with (
            patch("builtins.input", return_value="r"),
            patch("tools.novelupdates_scraper.wait_for_series_page", return_value="ready"),
        ):
            self.assertTrue(wait_for_manual_page_correction(page, 10))
        self.assertEqual(
            "https://www.novelupdates.com/series/babys-toy/",
            make_details_link("Baby’s Toy", "Novel"),
        )

    def test_skipped_report_only_includes_directories_with_only_skipped_details(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            artifacts = Path(directory)
            skipped = artifacts / "00002-skipped-book-hash"
            skipped.mkdir()
            (skipped / "details.json").write_text(
                json.dumps(
                    {
                        "status": "manually-skipped",
                        "title": "Skipped Book",
                        "requestedUrl": "https://example/skipped",
                    }
                ),
                encoding="utf-8",
            )
            completed = artifacts / "00001-completed-book-hash"
            completed.mkdir()
            (completed / "details.json").write_text(
                json.dumps({"status": "ok", "title": "Completed"}), encoding="utf-8"
            )
            skipped_with_html = artifacts / "00003-not-details-only-hash"
            skipped_with_html.mkdir()
            (skipped_with_html / "details.json").write_text(
                json.dumps({"status": "manually-skipped", "title": "Has HTML"}), encoding="utf-8"
            )
            (skipped_with_html / "page.html").write_text("html", encoding="utf-8")

            result = find_skipped_artifacts(artifacts)

            self.assertEqual(1, len(result))
            self.assertEqual(2, result[0]["rowNumber"])
            self.assertEqual("Skipped Book", result[0]["title"])
            self.assertTrue(is_skipped_status(result[0]["status"]))

    def test_language_backfill_adds_described_language_to_details_tags(self) -> None:
        html = """
        <div id="editlanguage"></div><span class="langlmsg"></span>
        <div id="showlang">
          <a class="genre lang" lid="495"
             href="https://www.novelupdates.com/language/chinese/"
             title="View All Series in Chinese">Chinese</a><br>
        </div>
        """
        languages = extract_languages_from_html(html)
        self.assertEqual("Chinese", languages[0]["name"])
        self.assertEqual("View All Series in Chinese", languages[0]["description"])

        with tempfile.TemporaryDirectory() as directory:
            artifact = Path(directory) / "00001-book-hash"
            artifact.mkdir()
            (artifact / "page.html").write_text(html, encoding="utf-8")
            (artifact / "details.json").write_text(
                json.dumps({"status": "ok", "title": "Book", "tags": []}),
                encoding="utf-8",
            )

            statistics = backfill_language_tags(Path(directory))
            details = json.loads((artifact / "details.json").read_text(encoding="utf-8"))

            self.assertEqual(1, statistics["updated"])
            self.assertEqual("Chinese", details["tags"][0]["name"])
            self.assertEqual("Chinese", details["languages"][0]["name"])
            expected_description = (
                "Chinese Language Novels. This tag is to be used for novels that are "
                "originally written in Chinese."
            )
            self.assertEqual(expected_description, details["tags"][0]["description"])
            self.assertEqual(expected_description, details["languages"][0]["description"])
            self.assertEqual(
                "https://www.novelupdates.com/language/chinese/",
                details["tags"][0]["url"],
            )

    def test_content_type_backfill_updates_all_details_statuses_recursively(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            artifacts = Path(directory)
            completed = artifacts / "00001-completed"
            skipped = artifacts / "manual" / "00001-skipped"
            completed.mkdir(parents=True)
            skipped.mkdir(parents=True)
            (completed / "details.json").write_text(
                json.dumps({"status": "ok"}), encoding="utf-8"
            )
            (skipped / "details.json").write_text(
                json.dumps({"status": "manually-skipped"}), encoding="utf-8"
            )

            statistics = backfill_content_types(artifacts)

            self.assertEqual(2, statistics["scanned"])
            self.assertEqual(2, statistics["updated"])
            self.assertEqual(
                "Novel",
                json.loads((completed / "details.json").read_text(encoding="utf-8"))[
                    "contentType"
                ],
            )
            self.assertEqual(
                "Novel",
                json.loads((skipped / "details.json").read_text(encoding="utf-8"))[
                    "contentType"
                ],
            )

    def test_language_normalization_replaces_an_old_tag_description(self) -> None:
        details = {
            "tags": [
                {
                    "name": "Japanese",
                    "description": "View All Series in Japanese",
                    "url": "https://www.novelupdates.com/language/japanese/",
                }
            ]
        }
        languages = normalize_language_tags(details["tags"])

        changed = add_languages_to_details_tags(details, languages)

        self.assertTrue(changed)
        self.assertEqual(
            "Japanese Language Novels. This tag is to be used for novels that are "
            "originally written in Japanese.",
            details["tags"][0]["description"],
        )

    def test_single_page_job_accepts_a_novelupdates_series_url(self) -> None:
        row, urls = create_single_page_job(
            "https://www.novelupdates.com/series/babys-toy/"
        )

        self.assertEqual("Babys Toy", row["primaryTitle"])
        self.assertEqual("Novel", row["contentType"])
        self.assertEqual("Reading", row["status"])
        self.assertEqual(
            "https://www.novelupdates.com/series/babys-toy/", urls[0]
        )
        self.assertEqual("NovelUpdates", json.loads(row["links"])[0]["SourceType"])

    def test_single_page_job_rejects_non_novelupdates_urls(self) -> None:
        with self.assertRaisesRegex(ValueError, "www.novelupdates.com"):
            create_single_page_job("https://example.com/series/book/")

    def test_prepare_links_adds_importable_links_for_every_content_type(self) -> None:
        rows = [
            {"primaryTitle": "A Novel", "contentType": "Novel", "links": ""},
            {"primaryTitle": "A Manga", "contentType": "Manga", "links": ""},
        ]

        urls = prepare_links(rows)

        self.assertEqual("https://www.novelupdates.com/series/a-novel/", urls[0])
        self.assertEqual("https://www.anime-planet.com/manga/a-manga", urls[1])
        self.assertEqual("NovelUpdates", json.loads(rows[0]["links"])[0]["SourceType"])
        self.assertEqual("Anime-Planet", json.loads(rows[1]["links"])[0]["SourceType"])

    def test_scrape_jobs_filter_novel_and_manga_modes(self) -> None:
        rows = [
            {"primaryTitle": "A Manhua", "contentType": "Manhua", "links": ""},
            {"primaryTitle": "A Novel", "contentType": "Novel", "links": ""},
            {"primaryTitle": "A Manhwa", "contentType": "Manhwa", "links": ""},
            {"primaryTitle": "A Manga", "contentType": "Manga", "links": ""},
        ]
        urls = prepare_links(rows)

        novel_jobs = ordered_scrape_jobs(rows, urls, "novel")
        manga_jobs = ordered_scrape_jobs(rows, urls, "manga")

        self.assertEqual(["Novel"], [row["contentType"] for _, row, _ in novel_jobs])
        self.assertEqual(
            ["Manga", "Manhwa", "Manhua"],
            [row["contentType"] for _, row, _ in manga_jobs],
        )

    def test_artifact_checkpoint_survives_canonical_url_change(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            artifacts = Path(directory)
            original = artifacts / "00224-title-oldhash"
            original.mkdir()
            (original / "details.json").write_text(
                json.dumps(
                    {
                        "status": "ok",
                        "requestedUrl": "https://www.anime-planet.com/manga/old-slug",
                        "canonicalUrl": "https://www.anime-planet.com/manga/canonical-slug",
                    }
                ),
                encoding="utf-8",
            )

            resolved = resolve_artifact_directory(
                artifacts,
                224,
                "Title",
                "https://www.anime-planet.com/manga/canonical-slug",
            )

            self.assertEqual(original, resolved)

    def test_anime_planet_fixture_extracts_only_primary_entry_metadata(self) -> None:
        fixture = Path(__file__).resolve().parents[1] / "animeplanet.html"

        details = extract_anime_planet_details_from_html(
            fixture.read_text(encoding="utf-8"),
            "https://www.anime-planet.com/manga/solo-leveling",
            "https://www.anime-planet.com/manga/solo-leveling",
            "Manhwa",
        )

        self.assertEqual("Solo Leveling", details["title"])
        self.assertEqual("Manhwa", details["contentType"])
        self.assertEqual("Chugong", details["authors"][0])
        self.assertIn("Na Honjaman Level-Up", details["associatedTitles"])
        self.assertIn("나 혼자만 레벨업", details["associatedTitles"])
        self.assertIn("Action", [genre["name"] for genre in details["genres"]])
        self.assertIn("Dungeon", [tag["name"] for tag in details["tags"]])
        self.assertNotIn("Isekai", [tag["name"] for tag in details["tags"]])
        self.assertTrue(details["coverSourceUrl"].endswith("solo-leveling-1.jpg?t=1625826980"))
        self.assertIn("E-class hunter Jinwoo Sung", details["description"])

    def test_title_match_accepts_formatting_typo_and_csv_alias(self) -> None:
        formatting_row = {
            "primaryTitle": "R-18 Hero Academy",
            "alternativeTitles": "",
        }
        typo_details = {
            "title": "R18 Hero Academyy",
            "associatedTitles": [],
        }
        alias_row = {
            "primaryTitle": "Miss Komi is Bad at Communication",
            "alternativeTitles": json.dumps(
                [{"Title": "Komi Can't Communicate"}]
            ),
        }
        alias_details = {
            "title": "Komi Can't Communicate",
            "associatedTitles": [],
        }

        self.assertTrue(details_match_row_title(formatting_row, 1, typo_details))
        self.assertTrue(details_match_row_title(alias_row, 1, alias_details))

    def test_title_match_rejects_unrelated_valid_page(self) -> None:
        row = {"primaryTitle": "Expected Manga", "alternativeTitles": ""}
        details = {
            "title": "Completely Different Series",
            "associatedTitles": ["Another Name"],
        }

        self.assertFalse(details_match_row_title(row, 1, details))

    def test_apply_details_merges_metadata_and_uses_canonical_url(self) -> None:
        row = {
            "primaryTitle": "Primary",
            "contentType": "Novel",
            "authorName": "",
            "alternativeTitles": "",
            "genres": "Fantasy",
            "tags": "",
            "description": "",
            "links": "",
        }
        prepare_links([row])
        details = {
            "authors": ["Author One", "Author Two"],
            "description": "Description",
            "genres": [
                {"name": "Fantasy", "description": "Fantasy description", "url": "https://example/1"},
                {"name": "Action", "description": "Action description", "url": "https://example/2"},
            ],
            "tags": [
                {"name": "Cultivation", "description": "Cultivation description", "url": "https://example/3"}
            ],
            "associatedTitles": ["Primary", "Alternative"],
            "canonicalUrl": "https://novelupdates.com/series/canonical-primary/",
        }

        apply_details_to_row(row, 1, details, overwrite_existing=False)

        self.assertEqual("Author One", row["authorName"])
        self.assertEqual("Fantasy; Action", row["genres"])
        self.assertEqual("Cultivation", row["tags"])
        self.assertEqual("Alternative", json.loads(row["alternativeTitles"])[0]["Title"])
        self.assertEqual(
            "https://novelupdates.com/series/canonical-primary/",
            json.loads(row["links"])[0]["Url"],
        )

    def test_csv_round_trip_detects_semicolon_and_preserves_fields(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "books.csv"
            source.write_text(
                "primaryTitle;contentType;status\nBook;Novel;Reading\n",
                encoding="utf-8",
            )
            rows, fieldnames, delimiter = read_rows(source)
            prepare_links(rows)
            destination = Path(directory) / "result.csv"

            write_rows_atomic(destination, rows, fieldnames, delimiter)
            result_rows, _, result_delimiter = read_rows(destination)

            self.assertEqual(";", result_delimiter)
            self.assertEqual("Book", result_rows[0]["primaryTitle"])
            self.assertEqual("NovelUpdates", json.loads(result_rows[0]["links"])[0]["SourceType"])


if __name__ == "__main__":
    unittest.main()
