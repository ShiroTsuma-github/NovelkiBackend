from __future__ import annotations

import argparse
import getpass
import os
import sys
from pathlib import Path

from scraper_import.api import ApiError, NovelkiApi
from scraper_import.catalog import load_catalog, write_review_report
from scraper_import.workflow import ImportStatistics, run_import


ROOT = Path(__file__).resolve().parents[1]


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Import completed scraper artifacts into Novelki. "
            "The order is global tags, global genres, authors, then books and covers."
        )
    )
    parser.add_argument(
        "--storage-dir",
        type=Path,
        default=ROOT / "storage" / "novelupdates-scraper",
    )
    parser.add_argument(
        "--corrected-csv",
        type=Path,
        default=ROOT / "KSIAZKI_POPRAWIONE.csv",
        help="Source of progress, status, rating, priority and other personal fields.",
    )
    parser.add_argument(
        "--review-output",
        type=Path,
        default=ROOT / "storage" / "novelupdates-import-review.csv",
        help="CSV containing fields deliberately left empty or records requiring manual review.",
    )
    parser.add_argument(
        "--base-url",
        default=os.getenv("NOVELKI_API_URL", "http://localhost:5232"),
    )
    parser.add_argument("--token", default=os.getenv("NOVELKI_ADMIN_TOKEN"))
    parser.add_argument("--email", default=os.getenv("NOVELKI_ADMIN_EMAIL"))
    parser.add_argument("--username", default=os.getenv("NOVELKI_ADMIN_USERNAME"))
    parser.add_argument(
        "--manga",
        action="store_true",
        help="Import only Manga, Manhwa and Manhua artifacts; exclude Novel records.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only inspect storage and write the review report; do not call the API.",
    )
    parser.add_argument(
        "--limit-books",
        type=positive_integer,
        help="Import only the first N ready books after preparing all metadata and authors.",
    )
    parser.add_argument("--skip-covers", action="store_true")
    parser.add_argument(
        "--overwrite-covers",
        action="store_true",
        help="Upload storage covers even when an existing book already has a ready cover.",
    )
    parser.add_argument("--retries", type=non_negative_integer, default=6)
    parser.add_argument("--request-delay", type=non_negative_float, default=0.0)
    return parser.parse_args()


def positive_integer(value: str) -> int:
    parsed = int(value)
    if parsed < 1:
        raise argparse.ArgumentTypeError("must be at least 1")
    return parsed


def non_negative_integer(value: str) -> int:
    parsed = int(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("must not be negative")
    return parsed


def non_negative_float(value: str) -> float:
    parsed = float(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("must not be negative")
    return parsed


def print_catalog_summary(catalog: object) -> None:
    print(
        "Storage: "
        f"scanned={catalog.scanned}, skipped={catalog.skipped}, readyBooks={len(catalog.books)}, "
        f"tags={len(catalog.tags)}, genres={len(catalog.genres)}, authors={len(catalog.authors)}, "
        f"reviewItems={len(catalog.issues)}"
    )


def print_import_summary(statistics: ImportStatistics) -> None:
    print(
        "Import: "
        f"tags={statistics.tags_created} created/{statistics.tags_existing} existing, "
        f"genres={statistics.genres_created} created/{statistics.genres_existing} existing, "
        f"authors={statistics.authors_created} created/{statistics.authors_existing} existing, "
        f"books={statistics.books_created} created/{statistics.books_existing} existing, "
        f"covers={statistics.covers_uploaded}, failed={statistics.failed}"
    )


def main() -> int:
    arguments = parse_arguments()
    storage_dir = arguments.storage_dir.expanduser().resolve()
    corrected_csv = arguments.corrected_csv.expanduser().resolve()
    review_output = arguments.review_output.expanduser().resolve()
    try:
        catalog = load_catalog(storage_dir, corrected_csv, manga_only=arguments.manga)
    except (OSError, UnicodeError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1

    print_catalog_summary(catalog)
    if arguments.dry_run:
        write_review_report(review_output, catalog.issues)
        print(f"Review report: {review_output}")
        return 0

    api = NovelkiApi(
        arguments.base_url,
        token=arguments.token,
        retries=arguments.retries,
        request_delay=arguments.request_delay,
    )
    if not api.token:
        if not arguments.username and not arguments.email:
            print(
                "ERROR: provide --token, --email, --username, or the corresponding NOVELKI_ADMIN_* variable.",
                file=sys.stderr,
            )
            return 1
        password = os.getenv("NOVELKI_ADMIN_PASSWORD") or getpass.getpass(
            "Novelki admin password: "
        )
        try:
            api.login(arguments.username, arguments.email, password)
        except ApiError as error:
            print(f"ERROR: login failed: {error}", file=sys.stderr)
            return 1

    try:
        statistics = run_import(
            api,
            catalog,
            limit_books=arguments.limit_books,
            skip_covers=arguments.skip_covers,
            overwrite_covers=arguments.overwrite_covers,
        )
    except ApiError as error:
        print(f"ERROR: import stopped before item processing: {error}", file=sys.stderr)
        write_review_report(review_output, catalog.issues)
        return 1

    write_review_report(review_output, catalog.issues)
    print_import_summary(statistics)
    print(f"Review report: {review_output}")
    return 2 if statistics.failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
