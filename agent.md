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
	- Selects renderer by view mode:
		- `GameState.ViewMode.Default` → `SimulationRenderer` with camera transform.
		- `GameState.ViewMode.Projected` → `CartesianSimulationRenderer` in screen-space.

- `Core/`
	- `GameState.cs`: central simulation state, object lists, time warp, separation checks, and view mode.
	- `GameConstants.cs`: simulation/render constants (`ShipSepration`, `RenderingScale`, etc.).

- `GameObjects/`
	- `Ship`, `Station`, `CelestialBody`, `ManeuverNode`.
	- `HasOrbit` base class for orbiting entities.

- `Simulation/`
	- `Orbit.cs`: orbital math/state propagation and anomaly/time calculations.

- `Input/`
	- `InputHandler.cs`: camera movement/zoom, ship selection, maneuver-node drag/edit, warp controls, projected-view input mapping.
	- Camera move/zoom are disabled in projected mode.

- `UI/`
	- `SimulationRenderer.cs`: default (polar/orbital) world renderer.
	- `CartesianSimulationRenderer.cs`: projected/cartesian renderer (x=angle, y=altitude).
	- `SimulationRendererBase.cs`: shared renderer fields/helpers/colors/constants.
	- `UIRenderer.cs`: panels/buttons and screen dimensions for input mapping.
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
- Right click (with selected ship): set/clear target orbiting object for closest-approach preview
- Orbit panel toggle: `Projected View [On/Off]`

Projected mode specifics:
- Camera movement and zoom are intentionally disabled.
- Maneuver-node/button hit testing uses screen-space interaction in projected mode.

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
4. If behavior changed, manually run and verify camera control, selection, orbit rendering, and maneuver interactions.

Minimum regression checks after UI/renderer changes:
- Toggle `Projected View` on/off; verify draw path switches correctly.
- Verify closest-approach visuals in both modes (dashed line, chevrons, safe-distance overlays).
- Verify station control-area boundaries/arrows render in both modes.
- Verify maneuver-node drag/buttons still work in both modes.

## Common feature entry points

- New gameplay state/rules: `Core/GameState.cs`
- New ship/station properties/logic: `GameObjects/`
- Orbital mechanics tweaks: `Simulation/Orbit.cs` and `Utilities/OrbitUtils.cs`
- Input bindings/interaction: `Input/InputHandler.cs`
- Visual/UI updates (default view): `UI/SimulationRenderer.cs`
- Visual/UI updates (projected view): `UI/CartesianSimulationRenderer.cs`
- Shared renderer logic/colors: `UI/SimulationRendererBase.cs`
- Panels/buttons: `UI/UIRenderer.cs`
- Shared node style values: `UI/UIConstants.cs`

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

## Recent implemented changes (current baseline)

- Added dual-view rendering architecture:
	- `Default` (camera-transformed polar/orbital view)
	- `Projected` (flat/cartesian projection)
- Added UI toggle for projected view in orbit visibility panel.
- Added projected mode handling in input:
	- camera lock (no pan/zoom)
	- projected screen→world mapping
	- maneuver interaction in screen-space
- Added `SimulationRendererBase` and moved shared items into it:
	- renderer fields (`GraphicsDevice`, `SpriteBatch`, `Camera`, `MouseState`, `Scale`)
	- shared helpers (`GetDestinationOrbit`, `FormatDistance`, dashed/polyline helpers, station control path builder)
	- shared colors/constants, including closest-approach tuning values
- Closest-approach parity work completed for projected renderer:
	- same approach color scheme/order as default renderer
	- same coarse/fine/exclusion search behavior
	- predicted destination marker and safe-separation overlays
- Gameplay rule adjustment:
	- ships with station destinations are no longer auto-despawned solely for crossing control boundary.
