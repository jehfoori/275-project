#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${1:-$ROOT_DIR/docs/webgl}"
BUILD_ARTIFACTS="$BUILD_DIR/Build"
INDEX_HTML="$BUILD_DIR/index.html"

if [[ ! -f "$INDEX_HTML" ]]; then
  echo "error: missing $INDEX_HTML" >&2
  exit 1
fi

if [[ ! -d "$BUILD_ARTIFACTS" ]]; then
  echo "error: missing $BUILD_ARTIFACTS" >&2
  exit 1
fi

decompress_file() {
  local compressed_path="$1"
  local output_path="$2"

  if [[ -f "$output_path" ]]; then
    return 0
  fi

  if [[ ! -f "$compressed_path" ]]; then
    return 1
  fi

  case "$compressed_path" in
    *.br)
      if ! command -v brotli >/dev/null 2>&1; then
        echo "error: brotli is required to decompress $compressed_path" >&2
        exit 1
      fi
      brotli -d "$compressed_path" -o "$output_path"
      ;;
    *.gz)
      gunzip -c "$compressed_path" > "$output_path"
      ;;
    *)
      echo "error: unsupported compression extension for $compressed_path" >&2
      exit 1
      ;;
  esac
}

ensure_uncompressed_artifact() {
  local base_name="$1"
  local output_path="$BUILD_ARTIFACTS/$base_name"

  if [[ -f "$output_path" ]]; then
    echo "ok: $base_name"
    return 0
  fi

  if decompress_file "$BUILD_ARTIFACTS/${base_name}.br" "$output_path"; then
    echo "decompressed: ${base_name}.br -> $base_name"
    return 0
  fi

  if decompress_file "$BUILD_ARTIFACTS/${base_name}.gz" "$output_path"; then
    echo "decompressed: ${base_name}.gz -> $base_name"
    return 0
  fi

  echo "error: could not find $base_name, ${base_name}.br, or ${base_name}.gz" >&2
  exit 1
}

ensure_uncompressed_artifact "WebGL.data"
ensure_uncompressed_artifact "WebGL.framework.js"
ensure_uncompressed_artifact "WebGL.wasm"

if [[ ! -f "$BUILD_ARTIFACTS/WebGL.loader.js" ]]; then
  echo "error: missing WebGL.loader.js" >&2
  exit 1
fi

rm -f \
  "$BUILD_ARTIFACTS"/WebGL.data.br \
  "$BUILD_ARTIFACTS"/WebGL.data.gz \
  "$BUILD_ARTIFACTS"/WebGL.framework.js.br \
  "$BUILD_ARTIFACTS"/WebGL.framework.js.gz \
  "$BUILD_ARTIFACTS"/WebGL.wasm.br \
  "$BUILD_ARTIFACTS"/WebGL.wasm.gz

python3 - "$INDEX_HTML" <<'PY'
import re
import sys
from pathlib import Path

index_path = Path(sys.argv[1])
content = index_path.read_text(encoding="utf-8")

replacements = {
    r'buildUrl \+ "/WebGL\.data(?:\.(?:br|gz))?"': 'buildUrl + "/WebGL.data"',
    r'buildUrl \+ "/WebGL\.framework\.js(?:\.(?:br|gz))?"': 'buildUrl + "/WebGL.framework.js"',
    r'buildUrl \+ "/WebGL\.wasm(?:\.(?:br|gz))?"': 'buildUrl + "/WebGL.wasm"',
}

for pattern, replacement in replacements.items():
    content, count = re.subn(pattern, replacement, content)
    if count == 0:
        raise SystemExit(f"error: could not update {pattern!r} in {index_path}")

index_path.write_text(content, encoding="utf-8")
PY

echo "Prepared $BUILD_DIR for GitHub Pages (uncompressed WebGL artifacts)."
