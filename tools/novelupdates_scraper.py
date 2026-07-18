"""Enrich a Novelki CSV with details collected in a visible Chrome session.

The helper intentionally attaches Playwright to a separately launched, installed
Chrome/Edge process over CDP. The browser uses a persistent profile, remains
visible, and lets the operator complete an interactive challenge. Page HTML and
cover bytes are then obtained from that browser session, not with requests or an
HTTP client impersonating Chrome.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import random
import shutil
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from typing import Any, Iterable
from urllib.parse import urlparse, urlunparse


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))

from t import make_details_link  # noqa: E402


NOVELUPDATES_HOST = "www.novelupdates.com"
NOVEL_CONTENT_TYPE = "Novel"
MAX_COVER_BYTES = 20 * 1024 * 1024
CHALLENGE_TITLE_MARKERS = (
    "just a moment",
    "attention required",
    "security verification",
    "checking your browser",
    "making sure you're not a bot",
    "verify you are human",
)
CHALLENGE_SELECTORS = (
    "#challenge-running",
    "#challenge-stage",
    ".cf-challenge-running",
    ".cf-turnstile",
    "iframe[src*='challenges.cloudflare.com']",
    "iframe[title*='challenge' i]",
    "form[action*='challenge-platform']",
)
DETAIL_FIELDS = (
    "authorName",
    "alternativeTitles",
    "genres",
    "tags",
    "description",
    "links",
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Add details links to a Novelki CSV and collect NovelUpdates metadata "
            "with a visible, persistent Chrome session."
        )
    )
    parser.add_argument("input", nargs="?", type=Path, default=Path("input.csv"))
    parser.add_argument(
        "--output-csv",
        type=Path,
        help="Output CSV. Defaults to <input stem>_NOVELUPDATES.csv next to the input.",
    )
    parser.add_argument(
        "--artifacts-dir",
        type=Path,
        default=Path("storage/novelupdates-scraper"),
        help="Directory for HTML, JSON and cover files.",
    )
    parser.add_argument(
        "--profile-dir",
        type=Path,
        default=Path("storage/novelupdates-browser-profile"),
        help="Persistent Chrome profile used for challenge cookies.",
    )
    parser.add_argument("--chrome-path", type=Path, help="Explicit Chrome/Edge executable.")
    parser.add_argument(
        "--cdp-url",
        help="Attach to an already running Chromium CDP endpoint instead of launching Chrome.",
    )
    parser.add_argument("--prepare-only", action="store_true", help="Only add links; do not open a browser.")
    parser.add_argument(
        "--list-skipped",
        action="store_true",
        help="Print terminally skipped books whose directory contains only details.json.",
    )
    parser.add_argument(
        "--skipped-output",
        type=Path,
        help="Optional text file for the --list-skipped report.",
    )
    parser.add_argument(
        "--backfill-language-tags",
        action="store_true",
        help="Add NovelUpdates languages to tags using already saved page.html files.",
    )
    parser.add_argument(
        "--backfill-csv",
        type=Path,
        help="Optional existing CSV to update in place during --backfill-language-tags.",
    )
    parser.add_argument(
        "--backfill-content-type",
        action="store_true",
        help="Add contentType=Novel to already saved NovelUpdates details.json files.",
    )
    parser.add_argument(
        "--page-url",
        help="Scrape one explicit NovelUpdates series URL without an input CSV.",
    )
    parser.add_argument(
        "--page-title",
        help="Optional title used for the artifact directory created by --page-url.",
    )
    parser.add_argument("--force", action="store_true", help="Scrape rows that already have successful artifacts.")
    parser.add_argument("--overwrite-existing", action="store_true", help="Replace existing author/description values.")
    parser.add_argument("--start-row", type=int, default=1, help="First one-based data row to scrape.")
    parser.add_argument("--limit", type=int, help="Maximum number of NovelUpdates rows to visit.")
    parser.add_argument("--delay-min", type=float, default=2.0, help="Minimum delay between pages in seconds.")
    parser.add_argument("--delay-max", type=float, default=5.0, help="Maximum delay between pages in seconds.")
    parser.add_argument(
        "--challenge-timeout",
        type=float,
        default=900.0,
        help="Seconds to wait for manual challenge completion.",
    )
    arguments = parser.parse_args()
    selected_modes = sum(
        bool(mode)
        for mode in (
            arguments.prepare_only,
            arguments.list_skipped,
            arguments.backfill_language_tags,
            arguments.backfill_content_type,
            arguments.page_url,
        )
    )
    if selected_modes > 1:
        parser.error(
            "--prepare-only, --list-skipped, --backfill-language-tags, "
            "--backfill-content-type and --page-url are mutually exclusive"
        )
    if arguments.backfill_csv and not arguments.backfill_language_tags:
        parser.error("--backfill-csv requires --backfill-language-tags")
    if arguments.page_title and not arguments.page_url:
        parser.error("--page-title requires --page-url")
    if arguments.start_row < 1:
        parser.error("--start-row must be at least 1")
    if arguments.limit is not None and arguments.limit < 1:
        parser.error("--limit must be at least 1")
    if arguments.delay_min < 0 or arguments.delay_max < arguments.delay_min:
        parser.error("delays must satisfy 0 <= --delay-min <= --delay-max")
    if arguments.challenge_timeout <= 0:
        parser.error("--challenge-timeout must be positive")
    return arguments


def detect_delimiter(path: Path) -> str:
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        sample = source.read(64 * 1024)
    first_line = sample.splitlines()[0] if sample else ""
    delimiter_counts = {delimiter: first_line.count(delimiter) for delimiter in (",", ";", "\t")}
    likely_delimiter = max(delimiter_counts, key=delimiter_counts.get)
    if delimiter_counts[likely_delimiter] > 0:
        return likely_delimiter
    try:
        return csv.Sniffer().sniff(sample, delimiters=",;\t").delimiter
    except csv.Error:
        return ","


def read_rows(path: Path) -> tuple[list[dict[str, str]], list[str], str]:
    if not path.is_file():
        raise FileNotFoundError(f"CSV file does not exist: {path}")
    delimiter = detect_delimiter(path)
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source, delimiter=delimiter)
        if not reader.fieldnames:
            raise ValueError("CSV does not contain a header row.")
        required = {"primaryTitle", "contentType"}
        missing = required.difference(reader.fieldnames)
        if missing:
            raise ValueError(f"CSV is missing required columns: {', '.join(sorted(missing))}")
        rows = [{key: value or "" for key, value in row.items() if key is not None} for row in reader]
        fieldnames = list(reader.fieldnames)
    for field in DETAIL_FIELDS:
        if field not in fieldnames:
            fieldnames.append(field)
            for row in rows:
                row[field] = ""
    return rows, fieldnames, delimiter


def write_rows_atomic(path: Path, rows: list[dict[str, str]], fieldnames: list[str], delimiter: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path = path.with_name(f".{path.name}.tmp")
    with temporary_path.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=fieldnames, delimiter=delimiter, extrasaction="raise")
        writer.writeheader()
        writer.writerows(rows)
        destination.flush()
        os.fsync(destination.fileno())
    temporary_path.replace(path)


def parse_links(raw_links: str, row_number: int) -> list[dict[str, Any]]:
    if not raw_links.strip():
        return []
    try:
        value = json.loads(raw_links)
    except json.JSONDecodeError as exception:
        raise ValueError(f"Row {row_number} has invalid JSON in links: {exception.msg}") from exception
    if not isinstance(value, list) or not all(isinstance(item, dict) for item in value):
        raise ValueError(f"Row {row_number} links must be a JSON array of objects.")
    return value


def is_host(url: str, expected_host: str) -> bool:
    try:
        actual = (urlparse(url).hostname or "").casefold().removeprefix("www.")
        expected = expected_host.casefold().removeprefix("www.")
        return actual == expected
    except ValueError:
        return False


def source_for_url(url: str) -> tuple[str, str]:
    if is_host(url, NOVELUPDATES_HOST):
        return "NovelUpdates", "NovelUpdates"
    return "Anime-Planet", "Anime-Planet"


def upsert_details_link(
    row: dict[str, str],
    row_number: int,
    generated_url: str,
    *,
    prefer_existing: bool = True,
) -> str:
    links = parse_links(row.get("links", ""), row_number)
    expected_host = urlparse(generated_url).hostname or ""
    existing = next(
        (
            link
            for link in links
            if isinstance(link.get("Url"), str) and is_host(link["Url"], expected_host)
        ),
        None,
    )
    details_url = (
        str(existing["Url"]).strip()
        if existing and prefer_existing
        else generated_url
    )
    label, source_type = source_for_url(details_url)
    new_link = {
        "Url": details_url,
        "Label": label,
        "SourceType": source_type,
        "IsPrimary": True,
        "LastReadHere": False,
    }
    if existing is None:
        links.append(new_link)
    else:
        existing.update(new_link)
    row["links"] = json.dumps(links, ensure_ascii=False, separators=(",", ":"))
    return details_url


def prepare_links(rows: list[dict[str, str]]) -> list[str]:
    urls: list[str] = []
    for row_number, row in enumerate(rows, start=1):
        url = make_details_link(row.get("primaryTitle", ""), row.get("contentType", ""))
        urls.append(upsert_details_link(row, row_number, url))
    return urls


def create_single_page_job(
    page_url: str, page_title: str | None = None
) -> tuple[dict[str, str], list[str]]:
    """Create one CSV-compatible row for an explicitly supplied series page."""
    normalized_url = page_url.strip()
    try:
        parsed = urlparse(normalized_url)
        port = parsed.port
    except ValueError as exception:
        raise ValueError(f"Invalid --page-url: {exception}") from exception
    if parsed.scheme.casefold() != "https" or not parsed.netloc or port not in (None, 443):
        raise ValueError("--page-url must be an absolute HTTPS URL.")
    if not is_host(normalized_url, NOVELUPDATES_HOST):
        raise ValueError("--page-url must point to www.novelupdates.com.")
    if not parsed.path.casefold().startswith("/series/"):
        raise ValueError("--page-url must point to a NovelUpdates /series/ page.")

    slug = parsed.path.rstrip("/").rsplit("/", 1)[-1]
    derived_title = " ".join(part for part in slug.replace("-", " ").split() if part).title()
    title = (page_title or "").strip() or derived_title or "NovelUpdates series"
    row = {
        "primaryTitle": title,
        "contentType": NOVEL_CONTENT_TYPE,
        "status": "Reading",
        **{field: "" for field in DETAIL_FIELDS},
    }
    details_url = upsert_details_link(row, 1, normalized_url, prefer_existing=False)
    return row, [details_url]


def safe_directory_name(row_number: int, title: str, url: str) -> str:
    clean = "".join(character.lower() if character.isalnum() else "-" for character in title)
    clean = "-".join(part for part in clean.split("-") if part)[:80] or "untitled"
    digest = hashlib.sha256(url.encode("utf-8")).hexdigest()[:10]
    return f"{row_number:05d}-{clean}-{digest}"


def find_browser_executable(explicit_path: Path | None) -> Path:
    if explicit_path:
        resolved = explicit_path.expanduser().resolve()
        if not resolved.is_file():
            raise FileNotFoundError(f"Browser executable does not exist: {resolved}")
        return resolved

    candidates: list[Path] = []
    if os.name == "nt":
        for environment_name in ("PROGRAMFILES", "PROGRAMFILES(X86)", "LOCALAPPDATA"):
            root = os.environ.get(environment_name)
            if root:
                candidates.extend(
                    [
                        Path(root) / "Google/Chrome/Application/chrome.exe",
                        Path(root) / "Microsoft/Edge/Application/msedge.exe",
                    ]
                )
    elif sys.platform == "darwin":
        candidates.extend(
            [
                Path("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"),
                Path("/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"),
            ]
        )
    else:
        for command in ("google-chrome", "google-chrome-stable", "microsoft-edge", "chromium", "chromium-browser"):
            found = shutil.which(command)
            if found:
                candidates.append(Path(found))

    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    raise FileNotFoundError("Could not find Chrome or Edge. Pass its path with --chrome-path.")


def reserve_local_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.bind(("127.0.0.1", 0))
        return int(server.getsockname()[1])


def wait_for_cdp(endpoint: str, process: subprocess.Popen[Any] | None, timeout_seconds: float = 30.0) -> None:
    version_url = endpoint.rstrip("/") + "/json/version"
    deadline = time.monotonic() + timeout_seconds
    last_error: Exception | None = None
    while time.monotonic() < deadline:
        if process is not None and process.poll() is not None:
            raise RuntimeError(f"Browser exited before CDP was ready (exit code {process.returncode}).")
        try:
            with urllib.request.urlopen(version_url, timeout=1) as response:
                if response.status == 200:
                    return
        except (OSError, urllib.error.URLError) as exception:
            last_error = exception
        time.sleep(0.25)
    raise TimeoutError(f"CDP endpoint did not start at {endpoint}: {last_error}")


def launch_visible_browser(browser_path: Path, profile_dir: Path) -> tuple[str, subprocess.Popen[Any]]:
    profile_dir.mkdir(parents=True, exist_ok=True)
    port = reserve_local_port()
    endpoint = f"http://127.0.0.1:{port}"
    command = [
        str(browser_path),
        f"--remote-debugging-port={port}",
        f"--user-data-dir={profile_dir.resolve()}",
        "--no-first-run",
        "--no-default-browser-check",
        "--start-maximized",
        "about:blank",
    ]
    creation_flags = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
    process = subprocess.Popen(
        command,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=creation_flags,
    )
    wait_for_cdp(endpoint, process)
    return endpoint, process


def normalize_url(url: str) -> str:
    parsed = urlparse(url)
    return urlunparse((parsed.scheme, parsed.netloc, parsed.path, parsed.params, parsed.query, ""))


def is_challenge_page(page: Any) -> bool:
    try:
        title = page.title().casefold()
        if any(marker in title for marker in CHALLENGE_TITLE_MARKERS):
            return True
        return any(page.locator(selector).count() > 0 for selector in CHALLENGE_SELECTORS)
    except Exception:
        return False


def has_series_content(page: Any) -> bool:
    return any(
        page.locator(selector).count() > 0
        for selector in ("#seriesgenre", "#showauthors", "#editdescription", "div.seriesimg img")
    )


def is_page_not_found(page: Any) -> bool:
    try:
        heading = page.locator("xpath=/html/body/div/div/h1").first
        if heading.count() > 0 and heading.inner_text().strip().casefold() == "page not found":
            return True
        return page.locator("h1").all_inner_texts() == ["Page not found"]
    except Exception:
        return False


def wait_for_series_page(page: Any, timeout_seconds: float) -> str:
    deadline = time.monotonic() + timeout_seconds
    challenge_announced = False
    stable_non_challenge_checks = 0
    while time.monotonic() < deadline:
        challenge = is_challenge_page(page)
        if challenge and not challenge_announced:
            print("  Challenge detected. Solve it in the visible browser; the script is waiting.")
            challenge_announced = True
        if not challenge and has_series_content(page):
            return "ready"
        if not challenge and is_page_not_found(page):
            return "not-found"
        if not challenge:
            stable_non_challenge_checks += 1
            if stable_non_challenge_checks >= 30:
                return "unrecognized"
        else:
            stable_non_challenge_checks = 0
        page.wait_for_timeout(1000)
    raise TimeoutError("Timed out while waiting for the challenge or series page.")


def wait_for_manual_page_correction(page: Any, timeout_seconds: float) -> bool:
    while True:
        print(
            "  Page not found. Open the correct series page in the visible browser, "
            "then type 'r' to retry or 'c' to skip this book."
        )
        try:
            choice = input("  [r/c] > ").strip().casefold()
        except EOFError:
            choice = "c"
        if choice == "c":
            return False
        if choice != "r":
            print("  Unknown command. Use 'r' or 'c'.")
            continue

        state = wait_for_series_page(page, timeout_seconds)
        if state == "ready":
            print(f"  Using manually selected page: {page.url}")
            return True
        if state == "not-found":
            print("  The currently open page still says 'Page not found'.")
        else:
            print("  The currently open page is not recognized as a NovelUpdates series page.")


def locator_texts(page: Any, selector: str) -> list[str]:
    values = page.locator(selector).all_inner_texts()
    return list(dict.fromkeys(value.strip() for value in values if value.strip()))


def locator_described_links(page: Any, selector: str) -> list[dict[str, str | None]]:
    values = page.locator(selector).evaluate_all(
        """
        elements => elements.map(element => ({
            name: (element.innerText || element.textContent || '').trim(),
            description: element.getAttribute('title'),
            url: element.href || element.getAttribute('href')
        }))
        """
    )
    result: list[dict[str, str | None]] = []
    known_names: set[str] = set()
    for value in values:
        name = str(value.get("name") or "").strip()
        if not name or name.casefold() in known_names:
            continue
        description = str(value.get("description") or "").strip() or None
        url = str(value.get("url") or "").strip() or None
        result.append({"name": name, "description": description, "url": url})
        known_names.add(name.casefold())
    return result


def merge_described_links(
    *collections: Iterable[dict[str, str | None]],
) -> list[dict[str, str | None]]:
    result: list[dict[str, str | None]] = []
    known_names: set[str] = set()
    for collection in collections:
        for value in collection:
            name = str(value.get("name") or "").strip()
            if not name or name.casefold() in known_names:
                continue
            result.append(value)
            known_names.add(name.casefold())
    return result


def locator_text(page: Any, selector: str) -> str | None:
    locator = page.locator(selector).first
    if locator.count() == 0:
        return None
    value = locator.inner_text().strip()
    return value or None


def split_lines(value: str | None) -> list[str]:
    if not value:
        return []
    return list(dict.fromkeys(line.strip() for line in value.splitlines() if line.strip()))


def extract_details(page: Any, requested_url: str) -> dict[str, Any]:
    canonical = page.locator("link[rel='canonical']").first
    canonical_url = canonical.get_attribute("href") if canonical.count() else None
    title = locator_text(page, ".seriestitlenu") or locator_text(page, "h1")
    languages = normalize_language_tags(
        locator_described_links(page, "#showlang a.genre.lang")
    )
    tags = merge_described_links(
        locator_described_links(page, "#showtags a.genre"),
        languages,
    )
    return {
        "status": "ok",
        "contentType": NOVEL_CONTENT_TYPE,
        "requestedUrl": requested_url,
        "pageUrl": page.url,
        "canonicalUrl": canonical_url or page.url,
        "title": title,
        "genres": locator_described_links(page, "#seriesgenre a.genre"),
        "tags": tags,
        "languages": languages,
        "authors": locator_texts(page, "#showauthors a.genre"),
        "description": locator_text(page, "#editdescription"),
        "associatedTitles": split_lines(locator_text(page, "#editassociated")),
        "scrapedAt": datetime.now(timezone.utc).isoformat(),
    }


def detect_image_type(data: bytes) -> tuple[str, str] | None:
    if data.startswith(b"\xff\xd8\xff"):
        return ".jpg", "image/jpeg"
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return ".png", "image/png"
    if len(data) >= 12 and data.startswith(b"RIFF") and data[8:12] == b"WEBP":
        return ".webp", "image/webp"
    if data.startswith((b"GIF87a", b"GIF89a")):
        return ".gif", "image/gif"
    if len(data) >= 12 and data[4:12] in (b"ftypavif", b"ftypavis"):
        return ".avif", "image/avif"
    return None


def save_captured_cover(
    context: Any,
    page: Any,
    artifact_dir: Path,
    image_responses: list[Any],
) -> dict[str, Any] | None:
    image = page.locator("div.seriesimg img").first
    if image.count() == 0:
        return None
    source_url = image.evaluate("element => element.currentSrc || element.src")
    if not source_url:
        return None

    image_bytes: bytes | None = None
    capture_mode = "page-response"
    for response in reversed(image_responses):
        if normalize_url(response.url) != normalize_url(source_url):
            continue
        try:
            response.finished()
            candidate = response.body()
            if detect_image_type(candidate):
                image_bytes = candidate
                break
        except Exception:
            continue

    if image_bytes is None:
        capture_mode = "browser-navigation"
        image_page = context.new_page()
        try:
            response = image_page.goto(source_url, referer=page.url, wait_until="commit", timeout=60_000)
            if response is not None:
                response.finished()
                candidate = response.body()
                if detect_image_type(candidate):
                    image_bytes = candidate
        except Exception as exception:
            print(f"  Browser image navigation failed: {exception}")
        finally:
            image_page.close()

    if image_bytes is not None:
        if len(image_bytes) > MAX_COVER_BYTES:
            raise ValueError(f"Cover exceeds {MAX_COVER_BYTES} bytes.")
        detected = detect_image_type(image_bytes)
        if detected is None:
            raise ValueError("Browser response is not a recognized image.")
        extension, mime_type = detected
        cover_path = artifact_dir / f"cover{extension}"
        cover_path.write_bytes(image_bytes)
        return {
            "sourceUrl": source_url,
            "path": cover_path.name,
            "mimeType": mime_type,
            "sizeBytes": len(image_bytes),
            "captureMode": capture_mode,
        }

    capture_mode = "element-screenshot"
    cover_path = artifact_dir / "cover.png"
    image.screenshot(path=str(cover_path))
    return {
        "sourceUrl": source_url,
        "path": cover_path.name,
        "mimeType": "image/png",
        "sizeBytes": cover_path.stat().st_size,
        "captureMode": capture_mode,
    }


def parse_json_array(raw_value: str, field_name: str, row_number: int) -> list[dict[str, Any]]:
    if not raw_value.strip():
        return []
    try:
        value = json.loads(raw_value)
    except json.JSONDecodeError as exception:
        raise ValueError(f"Row {row_number} has invalid JSON in {field_name}: {exception.msg}") from exception
    if not isinstance(value, list) or not all(isinstance(item, dict) for item in value):
        raise ValueError(f"Row {row_number} {field_name} must be a JSON array of objects.")
    return value


def merge_semicolon_values(existing: str, incoming: Iterable[str]) -> str:
    values = [value.strip() for value in existing.split(";") if value.strip()]
    known = {value.casefold() for value in values}
    for value in incoming:
        clean = value.strip()
        if clean and clean.casefold() not in known:
            values.append(clean)
            known.add(clean.casefold())
    return "; ".join(values)


def detail_names(values: Iterable[Any]) -> list[str]:
    names: list[str] = []
    for value in values:
        if isinstance(value, dict):
            name = str(value.get("name") or "").strip()
        else:
            name = str(value).strip()
        if name:
            names.append(name)
    return names


def merge_alternative_titles(row: dict[str, str], titles: Iterable[str], row_number: int) -> None:
    alternatives = parse_json_array(row.get("alternativeTitles", ""), "alternativeTitles", row_number)
    known = {
        str(item.get("Title", "")).strip().casefold()
        for item in alternatives
        if str(item.get("Title", "")).strip()
    }
    primary_title = row.get("primaryTitle", "").strip().casefold()
    for title in titles:
        clean = title.strip()
        if clean and clean.casefold() not in known and clean.casefold() != primary_title:
            alternatives.append({"Title": clean, "Language": None, "Source": "NovelUpdates"})
            known.add(clean.casefold())
    row["alternativeTitles"] = json.dumps(alternatives, ensure_ascii=False, separators=(",", ":"))


def apply_details_to_row(
    row: dict[str, str],
    row_number: int,
    details: dict[str, Any],
    overwrite_existing: bool,
) -> None:
    authors = [str(value) for value in details.get("authors", []) if str(value).strip()]
    if authors and (overwrite_existing or not row.get("authorName", "").strip()):
        row["authorName"] = authors[0].strip()
    description = details.get("description")
    if isinstance(description, str) and description.strip() and (
        overwrite_existing or not row.get("description", "").strip()
    ):
        row["description"] = description.strip()
    row["genres"] = merge_semicolon_values(
        row.get("genres", ""), detail_names(details.get("genres", []))
    )
    row["tags"] = merge_semicolon_values(
        row.get("tags", ""), detail_names(details.get("tags", []))
    )
    merge_alternative_titles(row, details.get("associatedTitles", []), row_number)
    canonical_url = details.get("canonicalUrl")
    if isinstance(canonical_url, str) and is_host(canonical_url, NOVELUPDATES_HOST):
        upsert_details_link(row, row_number, canonical_url, prefer_existing=False)


def write_json(path: Path, value: dict[str, Any]) -> None:
    temporary_path = path.with_name(f".{path.name}.tmp")
    temporary_path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary_path.replace(path)


def is_skipped_status(value: Any) -> bool:
    return isinstance(value, str) and "skipped" in value.casefold()


def find_skipped_artifacts(artifacts_dir: Path) -> list[dict[str, Any]]:
    if not artifacts_dir.is_dir():
        raise FileNotFoundError(f"Artifacts directory does not exist: {artifacts_dir}")

    skipped: list[dict[str, Any]] = []
    for details_path in artifacts_dir.glob("*/details.json"):
        artifact_files = [path for path in details_path.parent.iterdir() if path.is_file()]
        if len(artifact_files) != 1 or artifact_files[0].name != "details.json":
            continue
        try:
            details = json.loads(details_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        if not is_skipped_status(details.get("status")):
            continue

        directory_prefix = details_path.parent.name.split("-", 1)[0]
        row_number = int(directory_prefix) if directory_prefix.isdigit() else None
        skipped.append(
            {
                "rowNumber": row_number,
                "title": str(details.get("title") or details_path.parent.name),
                "url": str(details.get("requestedUrl") or details.get("pageUrl") or ""),
                "status": str(details.get("status")),
                "detailsPath": str(details_path),
            }
        )

    return sorted(
        skipped,
        key=lambda item: (
            item["rowNumber"] is None,
            item["rowNumber"] or 0,
            item["title"].casefold(),
        ),
    )


def print_skipped_report(artifacts_dir: Path, output_path: Path | None = None) -> int:
    skipped = find_skipped_artifacts(artifacts_dir)
    lines = []
    for item in skipped:
        prefix = f"{item['rowNumber']}." if item["rowNumber"] is not None else "-."
        suffix = f" — {item['url']}" if item["url"] else ""
        lines.append(f"{prefix} {item['title']}{suffix}")

    print(f"Skipped books: {len(skipped)}")
    for line in lines:
        print(line)
    if output_path is not None:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")
        print(f"Saved skipped report: {output_path}")
    return len(skipped)


class NovelUpdatesLanguageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.languages: list[dict[str, str | None]] = []
        self._showlang_depth = 0
        self._current_link: dict[str, str | None] | None = None
        self._current_text: list[str] = []

    def handle_starttag(self, tag: str, attributes: list[tuple[str, str | None]]) -> None:
        attrs = dict(attributes)
        if tag == "div" and attrs.get("id") == "showlang":
            self._showlang_depth = 1
            return
        if self._showlang_depth and tag == "div":
            self._showlang_depth += 1
        if self._showlang_depth and tag == "a":
            classes = set((attrs.get("class") or "").split())
            if {"genre", "lang"}.issubset(classes):
                self._current_link = {
                    "name": None,
                    "description": (attrs.get("title") or "").strip() or None,
                    "url": (attrs.get("href") or "").strip() or None,
                }
                self._current_text = []

    def handle_endtag(self, tag: str) -> None:
        if tag == "a" and self._current_link is not None:
            name = "".join(self._current_text).strip()
            if name:
                self._current_link["name"] = name
                if not any(
                    str(language.get("name") or "").casefold() == name.casefold()
                    for language in self.languages
                ):
                    self.languages.append(self._current_link)
            self._current_link = None
            self._current_text = []
        if tag == "div" and self._showlang_depth:
            self._showlang_depth -= 1

    def handle_data(self, data: str) -> None:
        if self._current_link is not None:
            self._current_text.append(data)


def extract_languages_from_html(html: str) -> list[dict[str, str | None]]:
    parser = NovelUpdatesLanguageParser()
    parser.feed(html)
    parser.close()
    return parser.languages


def normalize_language_tags(
    languages: Iterable[dict[str, str | None]],
) -> list[dict[str, str | None]]:
    normalized: list[dict[str, str | None]] = []
    known_names: set[str] = set()
    for language in languages:
        name = str(language.get("name") or "").strip()
        if not name or name.casefold() in known_names:
            continue
        normalized.append(
            {
                "name": name,
                "description": (
                    f"{name} Language Novels. This tag is to be used for novels "
                    f"that are originally written in {name}."
                ),
                "url": language.get("url"),
            }
        )
        known_names.add(name.casefold())
    return normalized


def backfill_content_types(artifacts_dir: Path) -> dict[str, int]:
    if not artifacts_dir.is_dir():
        raise FileNotFoundError(f"Artifacts directory does not exist: {artifacts_dir}")

    statistics = {"scanned": 0, "updated": 0, "alreadyPresent": 0, "invalid": 0}
    for details_path in artifacts_dir.rglob("details.json"):
        statistics["scanned"] += 1
        try:
            details = json.loads(details_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            statistics["invalid"] += 1
            continue
        if not isinstance(details, dict):
            statistics["invalid"] += 1
            continue
        if details.get("contentType") == NOVEL_CONTENT_TYPE:
            statistics["alreadyPresent"] += 1
            continue
        details["contentType"] = NOVEL_CONTENT_TYPE
        write_json(details_path, details)
        statistics["updated"] += 1
    return statistics


def add_languages_to_details_tags(
    details: dict[str, Any], languages: Iterable[dict[str, str | None]]
) -> bool:
    tags = details.get("tags")
    if not isinstance(tags, list):
        tags = []
        details["tags"] = tags

    changed = False
    for language in languages:
        name = str(language.get("name") or "").strip()
        if not name:
            continue
        expected = {
            "name": name,
            "description": language.get("description"),
            "url": language.get("url"),
        }
        existing_index = next(
            (
                index
                for index, tag in enumerate(tags)
                if (
                    str(tag.get("name") or "") if isinstance(tag, dict) else str(tag)
                ).strip().casefold()
                == name.casefold()
            ),
            None,
        )
        if existing_index is None:
            tags.append(expected)
            changed = True
        elif tags[existing_index] != expected:
            tags[existing_index] = expected
            changed = True
    return changed


def backfill_language_tags(artifacts_dir: Path, csv_path: Path | None = None) -> dict[str, int]:
    if not artifacts_dir.is_dir():
        raise FileNotFoundError(f"Artifacts directory does not exist: {artifacts_dir}")

    statistics = {"scanned": 0, "updated": 0, "alreadyPresent": 0, "missingLanguage": 0}
    details_by_row: dict[int, dict[str, Any]] = {}
    for page_path in artifacts_dir.glob("*/page.html"):
        details_path = page_path.parent / "details.json"
        if not details_path.is_file():
            continue
        statistics["scanned"] += 1
        try:
            details = json.loads(details_path.read_text(encoding="utf-8"))
            languages = normalize_language_tags(
                extract_languages_from_html(page_path.read_text(encoding="utf-8"))
            )
        except (OSError, json.JSONDecodeError):
            statistics["missingLanguage"] += 1
            continue
        if not languages:
            statistics["missingLanguage"] += 1
            continue

        tags_changed = add_languages_to_details_tags(details, languages)
        languages_changed = details.get("languages") != languages
        if languages_changed:
            details["languages"] = languages
        if tags_changed or languages_changed:
            write_json(details_path, details)
            statistics["updated"] += 1
        else:
            statistics["alreadyPresent"] += 1

        directory_prefix = page_path.parent.name.split("-", 1)[0]
        if directory_prefix.isdigit():
            details_by_row[int(directory_prefix)] = details

    if csv_path is not None:
        rows, fieldnames, delimiter = read_rows(csv_path)
        for row_number, details in details_by_row.items():
            if 1 <= row_number <= len(rows):
                apply_details_to_row(rows[row_number - 1], row_number, details, overwrite_existing=False)
        write_rows_atomic(csv_path, rows, fieldnames, delimiter)
        statistics["csvRowsUpdated"] = sum(
            1 for row_number in details_by_row if 1 <= row_number <= len(rows)
        )

    return statistics


def scrape_rows(
    arguments: argparse.Namespace,
    rows: list[dict[str, str]],
    urls: list[str],
    fieldnames: list[str],
    delimiter: str,
    output_csv: Path,
) -> None:
    try:
        from playwright.sync_api import Error as PlaywrightError
        from playwright.sync_api import TimeoutError as PlaywrightTimeoutError
        from playwright.sync_api import sync_playwright
    except ImportError as exception:
        raise RuntimeError(
            "Playwright is not installed. Run: "
            "python -m pip install -r tools/requirements-novelupdates-scraper.txt"
        ) from exception

    browser_process: subprocess.Popen[Any] | None = None
    endpoint = arguments.cdp_url
    if not endpoint:
        browser_path = find_browser_executable(arguments.chrome_path)
        print(f"Launching visible browser: {browser_path}")
        endpoint, browser_process = launch_visible_browser(browser_path, arguments.profile_dir)
    else:
        wait_for_cdp(endpoint, None)

    arguments.artifacts_dir.mkdir(parents=True, exist_ok=True)
    visited = 0
    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.connect_over_cdp(endpoint, timeout=30_000)
            if not browser.contexts:
                raise RuntimeError("The connected browser does not expose a default context.")
            context = browser.contexts[0]
            page = context.pages[0] if context.pages else context.new_page()
            image_responses: list[Any] = []

            def remember_image_response(response: Any) -> None:
                try:
                    if response.request.resource_type == "image":
                        image_responses.append(response)
                except Exception:
                    pass

            page.on("response", remember_image_response)
            for row_number, (row, details_url) in enumerate(zip(rows, urls, strict=True), start=1):
                if row_number < arguments.start_row or not is_host(details_url, NOVELUPDATES_HOST):
                    continue
                title = row.get("primaryTitle", "").strip() or f"row {row_number}"
                artifact_dir = arguments.artifacts_dir / safe_directory_name(row_number, title, details_url)
                artifact_dir.mkdir(parents=True, exist_ok=True)
                details_path = artifact_dir / "details.json"
                if details_path.is_file():
                    try:
                        existing_details = json.loads(details_path.read_text(encoding="utf-8"))
                        if is_skipped_status(existing_details.get("status")):
                            print(f"[{row_number}/{len(rows)}] Terminal skip: {title}")
                            continue
                        if existing_details.get("status") == "ok" and not arguments.force:
                            apply_details_to_row(
                                row, row_number, existing_details, arguments.overwrite_existing
                            )
                            write_rows_atomic(output_csv, rows, fieldnames, delimiter)
                            print(f"[{row_number}/{len(rows)}] Resume: {title}")
                            continue
                    except (OSError, json.JSONDecodeError, ValueError):
                        pass

                if arguments.limit is not None and visited >= arguments.limit:
                    break
                visited += 1

                print(f"[{row_number}/{len(rows)}] {title}\n  {details_url}")
                image_responses.clear()
                try:
                    try:
                        page.goto(details_url, wait_until="domcontentloaded", timeout=60_000)
                    except PlaywrightTimeoutError:
                        print("  Navigation timed out; checking the visible page state.")

                    page_state = wait_for_series_page(page, arguments.challenge_timeout)
                    manually_skipped = False
                    if page_state == "not-found":
                        manually_skipped = not wait_for_manual_page_correction(
                            page, arguments.challenge_timeout
                        )
                        page_state = "ready" if not manually_skipped else "manually-skipped"

                    if page_state != "ready":
                        failure = {
                            "status": page_state,
                            "contentType": NOVEL_CONTENT_TYPE,
                            "requestedUrl": details_url,
                            "pageUrl": page.url,
                            "title": title,
                            "scrapedAt": datetime.now(timezone.utc).isoformat(),
                        }
                        write_json(details_path, failure)
                        if page_state == "manually-skipped":
                            print("  Saved as a terminal skip; later runs will leave it untouched.")
                        else:
                            print("  No NovelUpdates series content found; saved failure details.")
                        if arguments.delay_max > 0:
                            delay = random.uniform(arguments.delay_min, arguments.delay_max)
                            print(f"  Waiting {delay:.1f}s")
                            page.wait_for_timeout(round(delay * 1000))
                        continue

                    try:
                        page.wait_for_load_state("networkidle", timeout=10_000)
                    except PlaywrightTimeoutError:
                        pass
                    page.wait_for_timeout(750)
                    html = page.content()
                    (artifact_dir / "page.html").write_text(html, encoding="utf-8")
                    details = extract_details(page, details_url)
                    cover = None
                    try:
                        cover = save_captured_cover(context, page, artifact_dir, image_responses)
                    except (PlaywrightError, OSError, ValueError) as cover_exception:
                        details["coverError"] = str(cover_exception)
                        print(f"  Cover capture failed: {cover_exception}")
                    if cover:
                        details["cover"] = cover
                    write_json(details_path, details)
                    apply_details_to_row(row, row_number, details, arguments.overwrite_existing)
                    write_rows_atomic(output_csv, rows, fieldnames, delimiter)
                    print(
                        f"  Saved: {len(details['genres'])} genres, {len(details['tags'])} tags, "
                        f"{len(details['authors'])} authors, cover={'yes' if cover else 'no'}"
                    )
                except (PlaywrightError, OSError, ValueError, TimeoutError) as exception:
                    failure = {
                        "status": "error",
                        "contentType": NOVEL_CONTENT_TYPE,
                        "requestedUrl": details_url,
                        "pageUrl": page.url,
                        "title": title,
                        "error": str(exception),
                        "scrapedAt": datetime.now(timezone.utc).isoformat(),
                    }
                    write_json(details_path, failure)
                    print(f"  ERROR: {exception}")

                if arguments.delay_max > 0:
                    delay = random.uniform(arguments.delay_min, arguments.delay_max)
                    print(f"  Waiting {delay:.1f}s")
                    page.wait_for_timeout(round(delay * 1000))

            browser.close()
    finally:
        if browser_process is not None and browser_process.poll() is None:
            browser_process.terminate()
            try:
                browser_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                browser_process.kill()


def main() -> int:
    arguments = parse_arguments()
    arguments.artifacts_dir = arguments.artifacts_dir.expanduser().resolve()
    if arguments.list_skipped:
        skipped_output = (
            arguments.skipped_output.expanduser().resolve()
            if arguments.skipped_output
            else None
        )
        try:
            print_skipped_report(arguments.artifacts_dir, skipped_output)
            return 0
        except FileNotFoundError as exception:
            print(f"ERROR: {exception}", file=sys.stderr)
            return 1

    if arguments.backfill_language_tags:
        backfill_csv = (
            arguments.backfill_csv.expanduser().resolve()
            if arguments.backfill_csv
            else None
        )
        try:
            statistics = backfill_language_tags(arguments.artifacts_dir, backfill_csv)
            print(
                "Language backfill: "
                f"scanned={statistics['scanned']}, updated={statistics['updated']}, "
                f"already-present={statistics['alreadyPresent']}, "
                f"missing-language={statistics['missingLanguage']}"
            )
            if backfill_csv is not None:
                print(
                    f"Updated CSV rows: {statistics.get('csvRowsUpdated', 0)} — {backfill_csv}"
                )
            return 0
        except (FileNotFoundError, ValueError) as exception:
            print(f"ERROR: {exception}", file=sys.stderr)
            return 1

    if arguments.backfill_content_type:
        try:
            statistics = backfill_content_types(arguments.artifacts_dir)
            print(
                "Content type backfill: "
                f"scanned={statistics['scanned']}, updated={statistics['updated']}, "
                f"already-present={statistics['alreadyPresent']}, "
                f"invalid={statistics['invalid']}"
            )
            return 0
        except FileNotFoundError as exception:
            print(f"ERROR: {exception}", file=sys.stderr)
            return 1

    if arguments.page_url:
        arguments.profile_dir = arguments.profile_dir.expanduser().resolve()
        arguments.artifacts_dir = arguments.artifacts_dir / "manual"
        output_csv = (
            arguments.output_csv.expanduser().resolve()
            if arguments.output_csv
            else arguments.artifacts_dir / "manual-page.csv"
        )
        try:
            row, urls = create_single_page_job(arguments.page_url, arguments.page_title)
            rows = [row]
            fieldnames = ["primaryTitle", "contentType", "status", *DETAIL_FIELDS]
            write_rows_atomic(output_csv, rows, fieldnames, ",")
            print(f"Prepared explicit NovelUpdates page: {arguments.page_url}")
            scrape_rows(arguments, rows, urls, fieldnames, ",", output_csv)
            print(f"Finished. Enriched CSV: {output_csv}")
            print(f"Artifacts: {arguments.artifacts_dir}")
            return 0
        except KeyboardInterrupt:
            print("\nStopped by user. Progress already written to the output CSV.", file=sys.stderr)
            return 130
        except (FileNotFoundError, RuntimeError, ValueError) as exception:
            print(f"ERROR: {exception}", file=sys.stderr)
            return 1

    input_path = arguments.input.expanduser().resolve()
    output_csv = (
        arguments.output_csv.expanduser().resolve()
        if arguments.output_csv
        else input_path.with_name(f"{input_path.stem}_NOVELUPDATES.csv")
    )
    arguments.profile_dir = arguments.profile_dir.expanduser().resolve()
    try:
        rows, fieldnames, delimiter = read_rows(input_path)
        urls = prepare_links(rows)
        write_rows_atomic(output_csv, rows, fieldnames, delimiter)
        novel_count = sum(is_host(url, NOVELUPDATES_HOST) for url in urls)
        print(f"Prepared {len(rows)} rows with details links: {output_csv}")
        print(f"NovelUpdates rows to scrape: {novel_count}")
        if arguments.prepare_only:
            return 0
        scrape_rows(arguments, rows, urls, fieldnames, delimiter, output_csv)
        print(f"Finished. Enriched CSV: {output_csv}")
        print(f"Artifacts: {arguments.artifacts_dir}")
        return 0
    except KeyboardInterrupt:
        print("\nStopped by user. Progress already written to the output CSV.", file=sys.stderr)
        return 130
    except (FileNotFoundError, RuntimeError, ValueError) as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
