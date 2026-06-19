#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${1:-$ROOT_DIR/Build/WebGL}"
PORT="${2:-8080}"

if [[ ! -f "$BUILD_DIR/index.html" ]]; then
  echo "WebGL build not found at: $BUILD_DIR"
  echo ""
  echo "Build it first in Unity:"
  echo "  Build > WebGL > Build Web Demo"
  exit 1
fi

exec python3 "$ROOT_DIR/scripts/webgl_http_server.py" "$BUILD_DIR" --port "$PORT"
