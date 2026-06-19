# Unity source

This folder contains the Unity project source for **Artificial Life City Simulation**.

See **`../README.md`** for an overview of the full submission and how to use the project webpage.

## Open in Unity

1. Install Unity **6000.3.6f1**
2. Add this folder as a project in Unity Hub
3. Open `Assets/Scenes/MainSimulation.unity`
4. Press **Play**

Civilians and soldiers spawn on start. Use the **Spawn Predator** button to add predators.

## Key scripts

- `Assets/Scripts/Agents/PreyAgent.cs` — prey / crowd behavior
- `Assets/Scripts/Agents/PredatorAgent.cs` — predator pursuit
- `Assets/Scripts/Simulation/SimulationManager.cs` — spawning and simulation state
- `Assets/Scripts/Simulation/CityNavigation.cs` — waypoint navigation

The browser build in `../webgl/` is exported from this project via Unity WebGL.
