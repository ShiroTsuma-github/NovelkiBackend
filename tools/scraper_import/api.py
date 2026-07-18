from __future__ import annotations

import json
import mimetypes
import random
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path
from typing import Any


RETRYABLE_STATUS = {429, 502, 503, 504}


class ApiError(RuntimeError):
    def __init__(self, status: int | None, detail: str, headers: Any = None) -> None:
        super().__init__(f"API {status or 'network'}: {detail}")
        self.status = status
        self.detail = detail
        self.headers = headers


class NovelkiApi:
    def __init__(
        self,
        base_url: str,
        token: str | None = None,
        retries: int = 6,
        request_delay: float = 0.0,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.retries = retries
        self.request_delay = request_delay

    def login(self, username: str | None, email: str | None, password: str) -> None:
        response = self.request(
            "POST",
            "/api/v1/account/login",
            {"username": username, "email": email, "password": password},
            authenticated=False,
        )
        token = response.get("accessToken") if isinstance(response, dict) else None
        if not token:
            raise ApiError(None, "login response did not contain accessToken")
        self.token = str(token)

    def ensure_admin(self) -> None:
        self.request("GET", "/api/v1/admin/tags?take=1")

    def create_global_tag(self, name: str, description: str | None) -> dict[str, Any]:
        return self.request("POST", "/api/v1/admin/tags", {"name": name, "description": description})

    def create_genre(self, name: str, description: str | None) -> dict[str, Any]:
        return self.request("POST", "/api/v1/admin/genres", {"name": name, "description": description})

    def genre_by_name(self, name: str) -> dict[str, Any]:
        encoded = urllib.parse.quote(name, safe="")
        return self.request("GET", f"/api/v1/genre/by-name/{encoded}")

    def dictionary_by_name(self, dictionary: str, name: str) -> dict[str, Any]:
        encoded = urllib.parse.quote(name, safe="")
        return self.request("GET", f"/api/v1/{dictionary}/by-name/{encoded}")

    def create_author(self, primary_name: str, other_names: tuple[str, ...]) -> dict[str, Any]:
        return self.request(
            "POST",
            "/api/v1/author",
            {"primaryName": primary_name, "otherNames": list(other_names)},
        )

    def search_authors(self, name: str) -> list[dict[str, Any]]:
        query = urllib.parse.urlencode({"search": name, "take": 50})
        response = self.request("GET", f"/api/v1/author?{query}")
        return response if isinstance(response, list) else []

    def update_author(self, author_id: str, other_names: tuple[str, ...]) -> dict[str, Any]:
        return self.request(
            "PUT",
            f"/api/v1/author/{author_id}",
            {"otherNames": list(other_names)},
        )

    def create_book(self, payload: dict[str, Any]) -> str:
        response = self.request("POST", "/api/v1/book", payload)
        book_id = response.get("id") if isinstance(response, dict) else None
        if not book_id:
            raise ApiError(None, "book create response did not contain id")
        return str(book_id)

    def search_books(self, title: str) -> list[dict[str, Any]]:
        query = urllib.parse.urlencode({"query": title, "skip": 0, "take": 100})
        response = self.request("GET", f"/api/v1/book?{query}")
        data = response.get("data") if isinstance(response, dict) else None
        return data if isinstance(data, list) else []

    def book(self, book_id: str) -> dict[str, Any]:
        return self.request("GET", f"/api/v1/book/{book_id}")

    def upload_cover(self, book_id: str, path: Path, mime_type: str | None) -> dict[str, Any]:
        content_type = mime_type or mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        boundary = f"novelki-{uuid.uuid4().hex}"
        header = (
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="file"; filename="{path.name.replace(chr(34), "")}"\r\n'
            f"Content-Type: {content_type}\r\n\r\n"
        ).encode("utf-8")
        body = header + path.read_bytes() + f"\r\n--{boundary}--\r\n".encode("ascii")
        return self.request(
            "PUT",
            f"/api/v1/book/{book_id}/cover",
            raw_body=body,
            content_type=f"multipart/form-data; boundary={boundary}",
        )

    def request(
        self,
        method: str,
        path: str,
        json_body: dict[str, Any] | None = None,
        *,
        raw_body: bytes | None = None,
        content_type: str = "application/json",
        authenticated: bool = True,
    ) -> Any:
        if authenticated and not self.token:
            raise ApiError(None, "admin token or login credentials are required")
        body = raw_body
        if json_body is not None:
            body = json.dumps(json_body, ensure_ascii=False).encode("utf-8")
        headers = {"Accept": "application/json"}
        if body is not None:
            headers["Content-Type"] = content_type
        if authenticated and self.token:
            headers["Authorization"] = f"Bearer {self.token}"

        for attempt in range(self.retries + 1):
            request = urllib.request.Request(
                f"{self.base_url}{path}", data=body, headers=headers, method=method
            )
            try:
                with urllib.request.urlopen(request, timeout=60) as response:
                    payload = response.read()
                    if self.request_delay:
                        time.sleep(self.request_delay)
                    return _decode(payload, response.headers.get_content_charset())
            except urllib.error.HTTPError as error:
                payload = error.read()
                detail = _error_detail(payload, error.headers.get_content_charset())
                if error.code not in RETRYABLE_STATUS or attempt >= self.retries:
                    raise ApiError(error.code, detail, error.headers) from error
                time.sleep(_retry_delay(error.headers.get("Retry-After"), attempt))
            except (urllib.error.URLError, TimeoutError, OSError) as error:
                if attempt >= self.retries:
                    raise ApiError(None, str(error)) from error
                time.sleep(_retry_delay(None, attempt))
        raise AssertionError("unreachable")


def _decode(payload: bytes, charset: str | None) -> Any:
    if not payload:
        return None
    text = payload.decode(charset or "utf-8")
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return text


def _error_detail(payload: bytes, charset: str | None) -> str:
    decoded = _decode(payload, charset)
    if isinstance(decoded, dict):
        return str(decoded.get("detail") or decoded.get("title") or decoded)
    return str(decoded or "empty error response")


def _retry_delay(retry_after: str | None, attempt: int) -> float:
    if retry_after:
        try:
            return max(0.0, float(retry_after))
        except ValueError:
            pass
    return min(60.0, (2**attempt) + random.random())
