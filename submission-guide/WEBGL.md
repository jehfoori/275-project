# WebGL Build and Hosting Guide

This project can be exported as a browser demo using Unity's WebGL target.

## Prerequisites

1. Unity Hub with editor **6000.3.6f1**
2. **WebGL Build Support** module installed for that editor
   - Unity Hub → Installs → gear icon on 6000.3.6f1 → Add modules → WebGL Build Support
3. Git LFS pulled (`git lfs pull`) so character and city assets are present

## Build the Web Demo (Unity)

1. Open the project in Unity.
2. Open `Assets/Scenes/MainSimulation.unity`.
3. Use the menu: **Build → WebGL → Build Web Demo**
4. Wait for the build to finish. Output goes to:

```text
Build/WebGL/
```

The build includes Unity's generated `index.html`, loader, `.wasm`, and data files.

### What the WebGL profile changes

At runtime in the browser, `WebGlRuntimeBootstrap` automatically applies lighter settings:

| Setting | Desktop scene | WebGL build |
|---------|---------------|-------------|
| Civilians | 120 | 40 |
| Soldiers | 50 | 12 |
| Max predators | 20 | 8 |
| Auto-spawn predators | Off | Off (use the button) |
| Quality level | Default | Lowest |

Controls in the browser match the desktop demo:

- **Spawn Predator** button (bottom-left)
- Camera view selector (bottom-center), or **Q** / **E**
- Stats HUD (top-right)

## Preview Locally

WebGL builds **must** be served over HTTP. Opening `index.html` directly (`file://`) will not work.

From the repo root:

```sh
chmod +x scripts/serve-webgl.sh
./scripts/serve-webgl.sh
```

Then open [http://127.0.0.1:8080](http://127.0.0.1:8080) — use **http**, not **https**.

The included server sets `Content-Encoding: gzip` for `.gz` files, which Unity requires when build compression is enabled.

To serve a different folder:

```sh
./scripts/serve-webgl.sh path/to/your/WebGL/build 8080
```

### Gzip / `illegal character U+001F` error

If you see:

```text
Unable to parse Build/WebGL.framework.js.gz!
Content-Encoding: gzip
```

your server is returning compressed files without the gzip header. Use `./scripts/serve-webgl.sh` (not `python3 -m http.server` directly).

For GitHub Pages, the project disables build compression by default so hosting works without custom headers. If your current build still has `.gz` files, either:

1. Use the project serve script locally, or
2. Rebuild in Unity (**Build → WebGL → Build Web Demo**) after pulling the latest project settings.

## Publish as a Website

### Option A: GitHub Pages (recommended)

1. Build in Unity (`Build/WebGL`).
2. Copy the build into `docs/webgl/`:

```sh
mkdir -p docs/webgl
cp -R Build/WebGL/. docs/webgl/
```

3. **Decompress for GitHub Pages** (required — Pages cannot serve `.gz` / `.br` Unity builds):

```sh
chmod +x scripts/prepare-webgl-for-pages.sh
./scripts/prepare-webgl-for-pages.sh docs/webgl
```

4. Commit and push `docs/index.html` and `docs/webgl/`.
4. On GitHub: **Settings → Pages → Build and deployment → Branch: main → Folder: /docs**.
5. Your site will be live at:

```text
https://<username>.github.io/275-project/
```

The landing page embeds the live demo from `docs/webgl/index.html`.

**Note:** WebGL builds are large (often 100MB+). GitHub has a soft limit around 100MB per file. If a single build artifact exceeds that, use itch.io (Option B) or Git LFS for the build output.

### Option B: itch.io

1. Build in Unity.
2. Zip the contents of `Build/WebGL/` (not the folder itself — the files inside).
3. Upload at [itch.io](https://itch.io) as an HTML project.

### Option C: Netlify / Vercel

Drag and drop the `Build/WebGL/` folder into the host's deploy UI, or point it at `docs/webgl/` after copying the build there.

## Troubleshooting

### "Could not switch to the WebGL build target"

Install WebGL Build Support in Unity Hub for editor 6000.3.6f1.

### Blank page or stuck loading

- Confirm you are using a local server, not `file://`.
- Check the browser console for errors.
- First load can take several minutes while the `.data` and `.wasm` files download.
- Try Chrome or Firefox; Safari can be slower with large WebGL builds.

### Out of memory in browser

Increase memory in **Edit → Project Settings → Player → WebGL → Memory**, then rebuild. The repo defaults to 256 MB initial memory.

### Build is very slow or huge

The city scene and character models are heavy. The WebGL profile already reduces agent counts. For further cuts, lower counts in `Assets/Scripts/Simulation/WebGlRuntimeBootstrap.cs` and rebuild.

### Missing pink materials / models

Run `git lfs pull` and rebuild.

## File Layout After Publishing

```text
docs/
  index.html          ← project landing page (GitHub Pages entry)
  WEBGL.md            ← this guide
  webgl/              ← copy Unity Build/WebGL output here
    index.html
    Build/
    TemplateData/
    ...
Build/
  WebGL/              ← local build output (gitignored)
scripts/
  serve-webgl.sh      ← local preview server
```
