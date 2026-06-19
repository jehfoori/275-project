# Artificial Life City Simulation

Unity project for our Artificial Life term project.

The project is a 3D predator-prey simulation set in a small city. Crowd agents move through a waypoint network, predators can be spawned into the city, and prey agents react by fleeing while predators pursue, wander, and consume nearby prey.

This README is for teammates setting up the repo for development.

## Quick Facts

- Engine: Unity
- Unity version: `6000.3.6f1`
- Render pipeline: Universal Render Pipeline
- Language: C#
- Editor: Visual Studio Code
- Main scene: `Assets/Scenes/MainSimulation.unity`
- Remote repo: `https://github.com/jehfoori/275-project.git`

Use the exact Unity version above unless the team agrees to upgrade together.

## Required Tools

Install these before opening the project:

1. Unity Hub
2. Unity Editor `6000.3.6f1`
3. Git
4. Visual Studio Code
5. VS Code extension: `Unity` by Microsoft
6. Git LFS

Git LFS is required because the project includes imported model and texture assets. Install it before cloning or pulling the project.

## Install Unity

1. Open Unity Hub.
2. Go to **Installs**.
3. Click **Install Editor**.
4. Choose Unity `6000.3.6f1`.
5. Install the editor.

Recommended modules:

- Your current operating system's build support, if Unity asks
- WebGL Build Support only if we later decide to make a browser demo

You do not need mobile modules for this project.

## Install Git LFS

Git LFS lets Git handle large binary files without bloating the normal repo history. The repo uses it for imported Unity art assets.

macOS with Homebrew:

```sh
brew install git-lfs
git lfs install
git lfs version
```

Windows:

1. Install Git LFS from <https://git-lfs.com/>.
2. Open Git Bash or PowerShell.
3. Run `git lfs install`.
4. Run `git lfs version` to verify it works.

## Clone the Repo

Clone the project into a normal local folder. Avoid cloud-sync folders such as iCloud Drive, Dropbox, Google Drive, or OneDrive. Unity creates many generated files, and cloud-sync tools can cause conflicts or slow imports.

```sh
git clone https://github.com/jehfoori/275-project.git
cd 275-project
git lfs pull
```

If `git lfs pull` fails because Git LFS is missing, install Git LFS and rerun it.

## Open the Project

1. Open Unity Hub.
2. Click **Add** or **Add project from disk**.
3. Select the cloned `275-project` folder.
4. Open it with Unity `6000.3.6f1`.
5. Wait for Unity to import the project.

The first import may take a while. Unity will generate local folders such as `Library/`, `Temp/`, `UserSettings/`, and `Logs/`. These folders are normal. Do not commit them.

After the project opens, open the main scene:

```text
Assets/Scenes/MainSimulation.unity
```

There may also be a default sample scene in the project. Use `MainSimulation.unity` for project work unless the team decides otherwise.

## Check Unity Settings

These settings should already be stored in the repo. If something looks wrong, check:

```text
Edit > Project Settings > Editor
```

Expected settings:

```text
Version Control Mode: Visible Meta Files
Asset Serialization Mode: Force Text
```

These settings matter because Unity uses `.meta` files to track asset IDs. Visible meta files make those IDs available to Git, and Force Text makes many Unity files easier to review and merge.

## Set Up VS Code

1. Install Visual Studio Code.
2. Install the VS Code extension named `Unity`.
3. Confirm the extension publisher is Microsoft.
4. In Unity, set VS Code as the external script editor:

```text
Unity > Settings/Preferences > External Tools > External Script Editor
```

The repo includes `.vscode/` settings and an **Attach to Unity** debug configuration.

## Project Structure

Most development work should happen under `Assets/`.

```text
Assets/
  Scenes/
  Scripts/
    Agents/
    Behaviors/
    Simulation/
    UI/
    Config/
  Prefabs/
    Agents/
    Environment/
      City/
  Materials/
  Models/
  VFX/
  Art/
Packages/
ProjectSettings/
```

Suggested use:

- `Scenes/`: Unity scenes
- `Scripts/Agents/`: agent controllers, state, and perception
- `Scripts/Behaviors/`: flocking, seeking, fleeing, wandering
- `Scripts/Simulation/`: spawning, metrics, and global simulation logic
- `Scripts/UI/`: sliders, panels, and controls
- `Scripts/Config/`: tunable settings and ScriptableObjects
- `Prefabs/`: reusable Unity objects
- `Materials/`, `Models/`, `VFX/`, `Art/`: visual assets

Move and rename Unity assets inside Unity when possible. This helps keep `.meta` files correct.

Imported art assets under `Assets/Blink/` and `Assets/EmaceArt/` have been trimmed to the files still referenced by the current prefabs and scenes. Do not delete the remaining files unless you also update the Unity references that depend on them.

## Git Workflow

Before starting work, pull the latest changes:

```sh
git pull
git lfs pull
```

Create a feature branch:

```sh
git checkout -b feature/short-description
```

Examples:

```text
feature/prey-flocking
feature/predator-pursuit
feature/simulation-ui
```

Check what changed:

```sh
git status
```

Stage project files:

```sh
git add Assets Packages ProjectSettings .vscode README.md .gitattributes .gitignore
```

Commit and push:

```sh
git commit -m "Describe the change"
git push -u origin feature/short-description
```

Open a pull request on GitHub when the feature is ready to merge.

## What to Commit

Commit:

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.gitignore`
- `.gitattributes`
- `.vscode/`
- `README.md`

Do not commit:

- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Builds/`
- `Logs/`
- `UserSettings/`
- generated `.csproj`, `.sln`, or `.slnx` files

The `.gitignore` should already exclude generated Unity files. Still check `git status` before committing.

## Unity Team Habits

Unity scenes and prefabs can be awkward to merge. To reduce conflicts:

- Avoid two people editing the same scene or prefab at the same time.
- Prefer script changes for behavior logic.
- Keep commits focused.
- Pull before starting work.
- Pull again before pushing.
- Do not delete `.meta` files manually.
- Ask the team before changing Unity versions or package versions.

If you create, move, or rename assets, commit the matching `.meta` files too.

## Current Development Target

The current goal is a working city simulation scene with:

- A bounded city environment
- Multiple crowd/prey agents moving through city waypoints
- Predator spawning and pursuit
- Prey fleeing and simple death/eating behavior
- Camera view controls
- Basic UI controls or visible simulation parameters

Keep implementation simple first. Visual polish can build on top of working behavior.

## WebGL Website Demo

The project supports a browser-hosted WebGL build for presenting the simulation on a website.

### One-time setup

Install **WebGL Build Support** for Unity `6000.3.6f1` in Unity Hub (Add modules on the editor install).

### Build

1. Open `Assets/Scenes/MainSimulation.unity`.
2. Run **Build → WebGL → Build Web Demo**.
3. Output is written to `Build/WebGL/`.

The WebGL player automatically uses lighter settings (fewer agents, lower quality) via `WebGlRuntimeBootstrap`.

### Preview locally

```sh
chmod +x scripts/serve-webgl.sh
./scripts/serve-webgl.sh
```

Open [http://localhost:8080](http://localhost:8080). Do not open `index.html` directly with `file://`.

### Publish

1. Copy the build into the docs site:

```sh
mkdir -p docs/webgl
cp -R Build/WebGL/. docs/webgl/
```

2. Enable GitHub Pages from the `/docs` folder.
3. The landing page at `docs/index.html` embeds the live demo when `docs/webgl/index.html` exists.

Full details: [docs/WEBGL.md](docs/WEBGL.md)

## Troubleshooting

### `git-lfs: command not found`

Install Git LFS, then run `git lfs install` and `git lfs pull`. This means Git has LFS-related settings, but the `git-lfs` executable is not installed or not visible on your PATH.

### Unity asks to upgrade the project

Cancel unless the team has agreed to upgrade Unity. Use Unity `6000.3.6f1`.

### Scripts do not open in VS Code

Check `Unity > Settings/Preferences > External Tools > External Script Editor` and set it to Visual Studio Code.

### Missing packages or strange import errors

Run `git pull` and `git lfs pull`, then reopen Unity. If errors continue, ask the team before deleting generated folders or changing packages.
