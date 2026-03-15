# SpaceTrafficController Agent Guide

This document helps coding agents (and new contributors) make safe, useful changes quickly.

## Project snapshot

- **Type:** MonoGame desktop game (`net8.0-windows`)
- **Theme:** Orbital traffic control (ships and stations orbiting Titan)
- **Setting reminder:** This project is set in **low orbit around Titan**, not Earth. Prefer Titan-specific language, assumptions, and physics constants unless a task explicitly changes the setting.
- **Entry point:** `Program.cs` → `MyGame`
- **Main loop owner:** `MyGame.cs`

## Build and run

- Build: `dotnet build`
- Run: `dotnet run --project SpaceTrafficController.csproj`

Notes:
- Content is built through MonoGame Content Builder (`MonoGame.Content.Builder.Task`).
- Current restore/build shows dependency compatibility warnings from `MenuBuddy` transitive packages; the project still builds.

## High-level architecture

- `MyGame.cs`
	- Sets up graphics/window, content loading, update/draw loop.
	- Calls `GameState.Update(...)`, `InputHandler.Update(...)`, and `SimulationRenderer` draw methods.

- `Core/`
	- `GameState.cs`: central simulation state, object lists, time warp, separation checks.
	- `GameConstants.cs`: simulation/render constants (`ShipSepration`, `RenderingScale`, etc.).

- `GameObjects/`
	- `Ship`, `Station`, `CelestialBody`, `ManeuverNode`.
	- `HasOrbit` base class for orbiting entities.

- `Simulation/`
	- `Orbit.cs`: orbital math/state propagation and anomaly/time calculations.

- `Input/`
	- `InputHandler.cs`: camera movement/zoom, ship selection, maneuver-node drag/edit, warp controls.

- `UI/`
	- `SimulationRenderer.cs`: draws world + overlays/debug text.
	- `Camera2D.cs`: world/screen transforms and zoom.
	- `UIConstants.cs`: maneuver node visual tuning.

- `Utilities/`
	- Math/vector helpers, orbital helper methods, physics constants.

## Controls (current behavior)

- `W/A/S/D` or arrow keys: move camera
- Middle mouse drag: pan camera
- Mouse wheel: zoom camera
- `,` (comma): decrease time warp
- `.` (period): increase time warp
- Left click ship: select ship
- Left click selected orbit: create maneuver node
- Left click/drag node + buttons: adjust maneuver components

## Important implementation rules

1. **Initialize `GameState` first**
	 - `GameState.Init()` must run before any orbit calculations that require `GameState.CentralBody`.

2. **Respect world-vs-screen scaling**
	 - Simulation uses physical-ish units; renderer uses `GameConstants.RenderingScale`.
	 - Always confirm if a value is in world units or scaled screen space before editing calculations.

3. **Keep update and draw responsibilities separate**
	 - State mutation in update path.
	 - Draw methods should avoid side effects except ephemeral debug UI state.

4. **Preserve existing naming/API unless requested**
	 - Some identifiers intentionally contain typos (`Manuever`, `ShipSepration`).
	 - Avoid broad renames unless task explicitly asks for cleanup.

## Safe change workflow for agents

1. Read relevant feature area (`Input`, `Simulation`, `UI`, `Core`).
2. Make minimal, localized edits.
3. Build with `dotnet build`.
4. If behavior changed, manually run and verify camera control, selection, orbit rendering, and maneuver interactions.

## Common feature entry points

- New gameplay state/rules: `Core/GameState.cs`
- New ship/station properties/logic: `GameObjects/`
- Orbital mechanics tweaks: `Simulation/Orbit.cs` and `Utilities/OrbitUtils.cs`
- Input bindings/interaction: `Input/InputHandler.cs`
- Visual/UI updates: `UI/SimulationRenderer.cs`, `UI/UIConstants.cs`, content assets in `Content/`

## Content pipeline notes

- Content root is `Content/`.
- Sprite fonts currently include:
	- `DebugFont.spritefont`
	- `ManueverNode.spritefont`
- If adding assets, ensure they are included in `Content/Content.mgcb` and load them from `MyGame.LoadContent()`.

## Out-of-scope by default

- Large package/framework migrations
- Renaming broad public APIs/namespaces
- Reworking coordinate system/units globally

Do those only when explicitly requested.
