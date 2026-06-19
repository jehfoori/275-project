# Artificial Life City Simulation

CS 275 Artificial Life — term project submission.

## Start here

Open **`index.html`** in a web browser. It is the main project page and contains all required materials organized into tabs.

## Contents

| Location | Description |
|----------|-------------|
| `index.html` | Project webpage — overview, demo, report, images, videos, and source links |
| `report/report.pdf` | Written project report |
| `images/` | Captioned screenshots |
| `video/` | Screen recordings of the simulation |
| `webgl/` | Browser executable (interactive WebGL demo) |
| `source/` | Unity project source code |

## Using the webpage

| Tab | What to look at |
|-----|-----------------|
| **Overview** | Title, team members, abstract, behavior model |
| **Live Demo** | Interactive WebGL simulation |
| **Report** | Link to the PDF report |
| **Gallery** | Representative captioned images |
| **Videos** | Recorded demo clips |
| **Source Code** | Link to the `source/` folder and repository |

### Live Demo controls

- Click **Spawn Predator** (bottom-left) to add predators
- Use **Q / E** or the view selector (bottom-center) to change camera angles
- Stats appear in the HUD (top-right)

The demo preloads when the page opens and continues running while you browse other tabs.

### If the interactive demo does not load locally

WebGL requires HTTP in most browsers. If you opened `index.html` directly from the extracted zip, use the **Videos** tab or the hosted demo link on the **Live Demo** tab instead.

## Running the Unity project (optional)

The C# source is in `source/`. To run the desktop version:

1. Install **Unity 6000.3.6f1** (Unity Hub)
2. Open the `source/` folder as a Unity project
3. Open `Assets/Scenes/MainSimulation.unity`
4. Press **Play** — agents spawn automatically; click **Spawn Predator** to add predators

## Technology

- **Engine:** Unity 6000.3.6f1 (Universal Render Pipeline)
- **Language:** C#
- **Main scene:** `Assets/Scenes/MainSimulation.unity`
