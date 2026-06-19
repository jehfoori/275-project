#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMPLATE="$ROOT_DIR/docs/site.template.html"
META_FILE="$ROOT_DIR/submission-guide/submission.meta"
OUTPUT="${1:-$ROOT_DIR/docs/index.html}"

if [[ ! -f "$TEMPLATE" ]]; then
  echo "Missing template: $TEMPLATE" >&2
  exit 1
fi

if [[ ! -f "$META_FILE" ]]; then
  echo "Missing $META_FILE" >&2
  echo "Copy submission-guide/submission.meta.example to submission.meta and fill it in." >&2
  exit 1
fi

# shellcheck disable=SC1090
source "$META_FILE"

for var in TEAM_MEMBERS HOSTED_DEMO_URL GITHUB_URL; do
  if [[ -z "${!var:-}" ]]; then
    echo "submission.meta must set $var" >&2
    exit 1
  fi
done

python3 - "$TEMPLATE" "$OUTPUT" "$TEAM_MEMBERS" "$HOSTED_DEMO_URL" "$GITHUB_URL" <<'PY'
import html
import sys
from pathlib import Path

template_path, output_path, team_members, hosted_demo_url, github_url = sys.argv[1:6]
content = Path(template_path).read_text(encoding="utf-8")
replacements = {
    "{{TEAM_MEMBERS}}": html.escape(team_members, quote=False),
    "{{HOSTED_DEMO_URL}}": html.escape(hosted_demo_url, quote=True),
    "{{GITHUB_URL}}": html.escape(github_url, quote=True),
}
for key, value in replacements.items():
    content = content.replace(key, value)
Path(output_path).write_text(content, encoding="utf-8")
PY

echo "Rendered $OUTPUT"
