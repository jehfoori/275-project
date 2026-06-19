# Term Project Submission (team use only)

This folder is **not** included in the BruinLearn zip. It holds packaging
configuration and instructions for the team.

## Prepare assets in `docs/`

```text
docs/
  report/report.pdf
  images/navigation-graph.png
  images/evacuation-flow-field.png
  images/soldier-rally.png
  video/overview.mp4
  video/naive-defense.mp4
  video/rally-defense.mp4
  webgl/                 ← Unity WebGL build (or use Build/WebGL at package time)
```

## Configure

1. Copy `submission.meta.example` → `submission.meta`
2. Set team names and URLs in `submission.meta`

## Preview the site locally

```sh
./scripts/render-submission-site.sh
open docs/index.html
```

## Create the BruinLearn zip

```sh
./scripts/package-submission.sh
```

The zip contains only:

- `README.md` and `index.html`
- `report/`, `images/`, `video/`, `webgl/`, `source/`

Validation errors print to your terminal only. Nothing instructional is copied
into the archive.

See also: [WEBGL.md](WEBGL.md)
