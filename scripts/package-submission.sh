#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOCS_DIR="$ROOT_DIR/docs"
META_FILE="$ROOT_DIR/submission-guide/submission.meta"
OUTPUT_ZIP="${1:-$ROOT_DIR/275-project-submission.zip}"
STAGING_DIR="$(mktemp -d)"
SUBMISSION_NAME="275-project"
STAGING_ROOT="$STAGING_DIR/$SUBMISSION_NAME"

REQUIRED_IMAGES=(
  "navigation-graph.png"
  "evacuation-flow-field.png"
  "soldier-rally.png"
)
REQUIRED_VIDEOS=(
  "overview.mp4"
  "naive-defense.mp4"
  "rally-defense.mp4"
)

cleanup() {
  rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

fail() {
  echo "error: $*" >&2
  exit 1
}

echo "Preparing submission zip: $OUTPUT_ZIP"

if [[ ! -f "$META_FILE" ]]; then
  fail "missing submission-guide/submission.meta (see submission-guide/submission.meta.example)"
fi

# shellcheck disable=SC1090
source "$META_FILE"

[[ -n "${TEAM_MEMBERS:-}" ]] || fail "TEAM_MEMBERS is empty in submission.meta"
[[ "$TEAM_MEMBERS" == *"Name One"* ]] && fail "update TEAM_MEMBERS in submission-guide/submission.meta"
[[ -n "${HOSTED_DEMO_URL:-}" ]] || fail "HOSTED_DEMO_URL is empty in submission.meta"
[[ -n "${GITHUB_URL:-}" ]] || fail "GITHUB_URL is empty in submission.meta"
[[ -f "$DOCS_DIR/report/report.pdf" ]] || fail "missing docs/report/report.pdf"

for image in "${REQUIRED_IMAGES[@]}"; do
  [[ -f "$DOCS_DIR/images/$image" ]] || fail "missing docs/images/$image"
done

for video in "${REQUIRED_VIDEOS[@]}"; do
  [[ -f "$DOCS_DIR/video/$video" ]] || fail "missing docs/video/$video"
done

mkdir -p "$STAGING_ROOT"

bash "$ROOT_DIR/scripts/render-submission-site.sh" "$STAGING_ROOT/index.html"

mkdir -p "$STAGING_ROOT/report" "$STAGING_ROOT/images" "$STAGING_ROOT/video"
cp "$DOCS_DIR/report/report.pdf" "$STAGING_ROOT/report/report.pdf"
cp "$DOCS_DIR/images/"*.png "$STAGING_ROOT/images/" 2>/dev/null || true
cp "$DOCS_DIR/images/"*.jpg "$STAGING_ROOT/images/" 2>/dev/null || true
cp "$DOCS_DIR/images/"*.jpeg "$STAGING_ROOT/images/" 2>/dev/null || true
cp "$DOCS_DIR/video/"*.mp4 "$STAGING_ROOT/video/"

if [[ -d "$DOCS_DIR/webgl" && -f "$DOCS_DIR/webgl/index.html" ]]; then
  mkdir -p "$STAGING_ROOT/webgl"
  rsync -a "$DOCS_DIR/webgl/" "$STAGING_ROOT/webgl/"
elif [[ -d "$ROOT_DIR/Build/WebGL" && -f "$ROOT_DIR/Build/WebGL/index.html" ]]; then
  mkdir -p "$STAGING_ROOT/webgl"
  rsync -a "$ROOT_DIR/Build/WebGL/" "$STAGING_ROOT/webgl/"
else
  fail "missing WebGL build (docs/webgl/ or Build/WebGL/)"
fi

mkdir -p "$STAGING_ROOT/source"
rsync -a \
  --exclude '.DS_Store' \
  "$ROOT_DIR/Assets/" "$STAGING_ROOT/source/Assets/"
rsync -a \
  --exclude '.DS_Store' \
  "$ROOT_DIR/Packages/" "$STAGING_ROOT/source/Packages/"
rsync -a \
  --exclude '.DS_Store' \
  "$ROOT_DIR/ProjectSettings/" "$STAGING_ROOT/source/ProjectSettings/"
cp "$ROOT_DIR/submission-guide/README.submission.md" "$STAGING_ROOT/README.md"
cp "$ROOT_DIR/submission-guide/README.source.md" "$STAGING_ROOT/source/README.md"

(
  cd "$STAGING_DIR"
  rm -f "$OUTPUT_ZIP"
  zip -rq "$OUTPUT_ZIP" "$SUBMISSION_NAME"
)

echo "Created $OUTPUT_ZIP"
echo "Contents:"
find "$STAGING_ROOT" -maxdepth 2 -type f | sed "s|$STAGING_ROOT|  |" | sort
