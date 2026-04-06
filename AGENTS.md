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
- Current restore/build shows dependency compatibility warnings from `MenuBuddy`/legacy packages and RID warnings in .NET 8; the project still builds.

## High-level architecture

- `MyGame.cs`
  - Sets up graphics/window, content loading, update/draw loop.
  - Calls `GameState.Update(...)`, `InputHandler.Update(...)`, and draw methods.
  - Owns startup/shutdown behavior including autosave load/save.

- `Core/`
  - `GameState.cs`: central simulation state, object lists, time warp, separation checks, and view mode.
  - `GameState.Persistence.cs`: autosave snapshot creation/restoration logic.
  - `GameConstants.cs`: simulation/render constants (`ShipSepration`, `RenderingScale`, etc.).

- `GameObjects/`
  - `Ship`, `Station`, `CelestialBody`, `ManeuverNode`.
  - `HasOrbit` base class for orbiting entities.

- `Simulation/`
  - `Orbit.cs`: orbital math/state propagation and anomaly/time calculations.

- `Input/`
  - `InputHandler.cs`: camera movement/zoom, ship selection, maneuver-node drag/edit, warp controls, projected-view input mapping, and pause menu interactions.

- `UI/`
  - `SimulationRenderer.cs`: default (polar/orbital) world renderer.
  - `CartesianSimulationRenderer.cs`: projected/cartesian renderer (x=angle, y=altitude).
  - `SimulationRendererBase.cs`: shared renderer fields/helpers/colors/constants.
  - `UIRenderer.cs`: panels/buttons, pause menu, and screen dimensions for input mapping.
  - `Camera2D.cs`: world/screen transforms and zoom.
  - `UIConstants.cs`: maneuver node visual tuning.

## Save-state persistence rules

When changing `GameState` or adding gameplay data, always decide whether the new data must survive autosave/load.

If the answer is yes, update persistence in the same change.

### Files that must stay in sync

- `Core/GameState.cs`
- `Core/GameState.Persistence.cs`
- `MyGame.cs` if startup/shutdown behavior changes

### What to update when adding savable state

1. Add the new value to the snapshot model in `Core/GameState.Persistence.cs`.
2. Write the value in `CreateSnapshot()`.
3. Restore the value in `ApplySnapshot(...)`.
4. If the state references another object, store a stable lookup form that can be reconstructed safely.
5. If a new orbiting object / destination / node type is introduced, extend the persistence discriminators and restore logic.
6. Build with `dotnet build` after the change.
7. Manually verify both flows:
   - close game → reopen → state restored
   - pause menu → `New Game` starts fresh state without loading the old autosave immediately

### Persistence checklist

- Scores and timers restored
- Pause/view/UI mode restored only if still valid
- Selected/targeted objects restored safely
- Ship/station custom fields restored
- Maneuver nodes/destinations restored
- New collections or nested objects included
- Invalid/old save data handled safely without crashing

### Important rule

Do **not** add new `GameState` fields that should persist without updating autosave logic.
If unsure, leave a clear code comment or TODO in `Core/GameState.Persistence.cs`.

## Important implementation rules

1. **Initialize `GameState` first**
   - `GameState.Init()` must run before any orbit calculations that require `GameState.CentralBody`.

2. **Respect world-vs-screen scaling**
   - Simulation uses physical-ish units; renderer uses `GameConstants.RenderingScale`.
   - Always confirm if a value is in world units or scaled screen space before editing calculations.

3. **Keep update and draw responsibilities separate**
   - State mutation in update path.
   - Draw methods should avoid side effects except ephemeral debug UI state.

4. **Use shared renderer values first**
   - If you need a common color/threshold used by both views, add it to `UI/SimulationRendererBase.cs`.
   - Keep closest-approach behavior visually consistent between `SimulationRenderer` and `CartesianSimulationRenderer`.

5. **Preserve existing naming/API unless requested**
   - Some identifiers intentionally contain typos (`Manuever`, `ShipSepration`).
   - Avoid broad renames unless task explicitly asks for cleanup.

## Safe change workflow for agents

1. Read relevant feature area (`Input`, `Simulation`, `UI`, `Core`).
2. Make minimal, localized edits.
3. Build with `dotnet build`.
4. If behavior changed, manually run and verify camera control, selection, orbit rendering, pause menu flow, and maneuver interactions.

## Common feature entry points

- New gameplay state/rules: `Core/GameState.cs`
- Save/load logic: `Core/GameState.Persistence.cs`
- New ship/station properties/logic: `GameObjects/`
- Orbital mechanics tweaks: `Simulation/Orbit.cs` and `Utilities/OrbitUtils.cs`
- Input bindings/interaction: `Input/InputHandler.cs`
- Visual/UI updates (default view): `UI/SimulationRenderer.cs`
- Visual/UI updates (projected view): `UI/CartesianSimulationRenderer.cs`
- Shared renderer logic/colors: `UI/SimulationRendererBase.cs`
- Panels/buttons: `UI/UIRenderer.cs`

## Out-of-scope by default

- Large package/framework migrations
- Renaming broad public APIs/namespaces
- Reworking coordinate system/units globally

Do those only when explicitly requested.
