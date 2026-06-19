#!/usr/bin/env python3
"""Minimal static server for Unity WebGL builds with gzip-compressed assets."""

from __future__ import annotations

import argparse
import http.server
import os
import socketserver
from pathlib import Path

GZIP_CONTENT_TYPES = {
    ".js": "application/javascript",
    ".wasm": "application/wasm",
    ".data": "application/octet-stream",
    ".json": "application/json",
    ".html": "text/html",
    ".css": "text/css",
}


class WebGLRequestHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, directory: str | None = None, **kwargs):
        super().__init__(*args, directory=directory, **kwargs)

    def send_head(self):
        """Always return 200 so stale 304 cache entries cannot break gzip assets."""
        path = self.translate_path(self.path)
        if os.path.isdir(path):
            parts = urllib_parse(self.path)
            if not parts.path.endswith("/"):
                self.send_response(301)
                self.send_header("Location", parts.path + "/")
                self.end_headers()
                return None
            for index in ("index.html", "index.htm"):
                index_path = os.path.join(path, index)
                if os.path.isfile(index_path):
                    path = index_path
                    break
            else:
                return self.list_directory(path)

        extension = Path(path).suffix.lower()
        if extension in (".exe", ".dll", ".py", ".pyc"):
            self.send_error(403, "Forbidden")
            return None

        try:
            file_handle = open(path, "rb")
        except OSError:
            self.send_error(404, "File not found")
            return None

        try:
            file_stat = os.fstat(file_handle.fileno())
            self.send_response(200)
            self.send_header("Content-type", self.guess_type(path))
            self.send_header("Content-Length", str(file_stat.st_size))
            self.end_headers()
            return file_handle
        except OSError:
            file_handle.close()
            raise

    def end_headers(self) -> None:
        if self.path.split("?", 1)[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store, no-cache, must-revalidate")
        self.send_header("Pragma", "no-cache")
        self.send_header("Expires", "0")
        super().end_headers()

    def guess_type(self, path: str) -> str:
        clean_path = path
        if clean_path.endswith(".gz"):
            clean_path = clean_path[:-3]

        extension = Path(clean_path).suffix.lower()
        if extension in GZIP_CONTENT_TYPES:
            return GZIP_CONTENT_TYPES[extension]

        guessed = super().guess_type(path)
        return guessed or "application/octet-stream"


def urllib_parse(path: str):
    from urllib.parse import urlparse

    return urlparse(path)


def main() -> None:
    parser = argparse.ArgumentParser(description="Serve a Unity WebGL build locally.")
    parser.add_argument("directory", nargs="?", default=".", help="Path to the WebGL build folder")
    parser.add_argument("--port", type=int, default=8080, help="Port to listen on")
    args = parser.parse_args()

    build_dir = Path(args.directory).resolve()
    if not (build_dir / "index.html").is_file():
        raise SystemExit(f"WebGL build not found (missing index.html): {build_dir}")

    os.chdir(build_dir)

    class ReusableTCPServer(socketserver.TCPServer):
        allow_reuse_address = True

    handler = lambda *handler_args, **handler_kwargs: WebGLRequestHandler(  # noqa: E731
        *handler_args, directory=str(build_dir), **handler_kwargs
    )

    url = f"http://127.0.0.1:{args.port}"
    with ReusableTCPServer(("", args.port), handler) as httpd:
        print(f"Serving WebGL build from:\n  {build_dir}\n")
        print("Copy and paste this exact URL into the address bar:")
        print(f"  {url}\n")
        print("Tips:")
        print("  - Include http:// (browsers often guess https if you omit it)")
        print("  - Use 127.0.0.1, not localhost (different cache + HTTPS rules)")
        print("  - Hard refresh once if you still see a gzip error: Cmd+Shift+R")
        print("\nPress Ctrl+C to stop.\n")
        httpd.serve_forever()


if __name__ == "__main__":
    main()
