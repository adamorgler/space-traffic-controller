using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.Linq;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Utilities;
using System;
using SpaceTrafficController.UI;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using System.Xml.Linq;

namespace SpaceTrafficController.Input;

public class InputHandler
{
    private readonly Camera2D Camera;
    private readonly GameState GameState;
    private KeyboardState KeyboardState;
    private MouseState MouseState;
    private KeyboardState PrevKeyboardState;
    private MouseState PrevMouseState;

    private ManeuverNode DraggedNode = null;

    private ManeuverDragType CurrentManeuverDrag = ManeuverDragType.None;
    private Vector2 DragStartMouseWorldPos;

    private bool _hasCameraPreFocusPose;
    private Vector2 _cameraPreFocusPosition;
    private float _cameraPreFocusRotation;
    private float _cameraPreFocusZoom;
    private bool _hasDefaultViewCameraPose;
    private Vector2 _defaultViewCameraPosition;
    private float _defaultViewCameraRotation;
    private float _defaultViewCameraZoom;
    private bool _cameraFollowSelected = false;
    private HasOrbit _prevSelectedOrbitingObject = null;

    private readonly UIRenderer UIRenderer;

    public InputHandler(Camera2D camera, GameState gameState, UIRenderer uiRenderer)
    {
        Camera = camera;
        GameState = gameState;
        UIRenderer = uiRenderer;
    }

    public void Update(GameTime gameTime)
    {
        KeyboardState = Keyboard.GetState();
        MouseState = Mouse.GetState();

        UpdateHohmannTransferMousePreview();

        if (KeyboardState.IsKeyDown(Keys.Space) && PrevKeyboardState.IsKeyUp(Keys.Space))
        {
            GameState.TogglePause();
        }

        if (GameState.IsPaused)
        {
            HandlePausedUiClick();
            ClearTransientInputState();
            PrevKeyboardState = KeyboardState;
            PrevMouseState = MouseState;
            return;
        }

        HandleCameraMovement(gameTime);
        HandleCameraZoom();
        HandleWarpControl();

        // left click
        HandleLeftClick();
        HandleLeftDrag();
        HandleManeuverDeltaVDrag();

        if (MouseState.LeftButton == ButtonState.Released && PrevMouseState.LeftButton == ButtonState.Pressed)
        {
            var selectedShip = GameState.SelectedShip;
            var manueverNode = GetActiveManeuverNode(selectedShip);

            if (DraggedNode is not null)
            {
                DraggedNode.IsDragged = false;
                DraggedNode = null;
            }

            CurrentManeuverDrag = ManeuverDragType.None;
            if (manueverNode != null)
            {
                manueverNode.DragType = ManeuverDragType.None;
            }
        }

        // update camera following state (if requested)
        UpdateCameraFollowing();

        // right click target selection
        HandleRightClick();

        // if the camera was focused on a selected object but that object became unselected,
        // reset the camera back to the pre-focus pose
        if (_prevSelectedOrbitingObject is not null && GameState.SelectedOrbitingObject is null)
        {
            if (_cameraFollowSelected || _hasCameraPreFocusPose)
            {
                ResetCameraView();
            }
        }

        _prevSelectedOrbitingObject = GameState.SelectedOrbitingObject;

        PrevKeyboardState = KeyboardState;
        PrevMouseState = MouseState;
    }

    private void HandleRightClick()
    {
        // only consider right-click target selection when a ship is selected
        if (MouseState.RightButton == ButtonState.Pressed && PrevMouseState.RightButton == ButtonState.Released)
        {
            var selectedShip = GameState.SelectedShip;
            if (selectedShip is null)
            {
                // if no ship selected, behave as a clear for target
                GameState.TargetOrbitingObject = null;
                return;
            }

            Vector2 mousePos = GetMouseWorldPosition();
            var orbitingObjects = GameState.OrbitingObjects
                .OrderBy(x => Vector2.Distance(mousePos, x.Orbit.PositionVector / GameConstants.RenderingScale))
                .ToList();
            float clickRadius = 10f;
            foreach (var orbitingObject in orbitingObjects)
            {
                if (orbitingObject == selectedShip) continue; // ignore selecting the same ship
                if (Vector2.Distance(mousePos, orbitingObject.Orbit.PositionVector / GameConstants.RenderingScale) < clickRadius)
                {
                    GameState.TargetOrbitingObject = orbitingObject;
                    return;
                }
            }

            // clicked empty space -> clear target
            GameState.TargetOrbitingObject = null;
        }
    }

    // ensure camera follows selected orbiting object when requested
    private void UpdateCameraFollowing()
    {
        if (!_cameraFollowSelected)
            return;

        var selected = GameState.SelectedOrbitingObject;
        if (selected is null)
        {
            _cameraFollowSelected = false;
            GameState.IsCameraFocusedOnSelected = false;
            return;
        }

        // only snap-follow once the transition is complete to avoid fighting the interpolation
        if (!Camera.IsTransitioning)
        {
            var targetPosition = selected.Orbit.PositionVector / GameConstants.RenderingScale;
            // set pose immediately (keeps desired/actual in sync)
            Camera.SetPose(targetPosition, Camera.Rotation, Camera.Zoom);
        }
    }

    private void ClearTransientInputState()
    {
        var selectedShip = GameState.SelectedShip;
        var manueverNode = GetActiveManeuverNode(selectedShip);

        if (DraggedNode is not null)
        {
            DraggedNode.IsDragged = false;
            DraggedNode = null;
        }

        CurrentManeuverDrag = ManeuverDragType.None;
        if (manueverNode is not null)
        {
            manueverNode.DragType = ManeuverDragType.None;
        }
    }

    private void HandlePausedUiClick()
    {
        if (MouseState.LeftButton == ButtonState.Pressed && PrevMouseState.LeftButton == ButtonState.Released)
        {
            var uiResult = UIRenderer.GetActionAt(MouseState.Position.ToVector2());
            if (uiResult is not null)
            {
                ApplyUIAction(uiResult);
                return;
            }

            if (GameState.IsHohmannTransferDialogOpen && GameState.IsHohmannTransferMouseTargetSelectionActive)
            {
                var ship = GameState.SelectedShip;
                if (ship is not null)
                {
                    UpdateHohmannTransferMousePreview(forceApply: true);
                    GameState.IsHohmannTransferMouseTargetSelectionActive = false;
                }
            }
        }
    }

    private void HandleCameraMovement(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (GameState.CurrentViewMode == GameState.ViewMode.Projected)
        {
            // Projected view supports horizontal-only panning (wrap handled by renderer).
            float projectedMoveSpeed = 1000f * dt;
            float moveX = 0f;

            if (MouseState.MiddleButton == ButtonState.Pressed)
            {
                moveX += PrevMouseState.Position.X - MouseState.Position.X;
            }

            if (KeyboardState.IsKeyDown(Keys.A) || KeyboardState.IsKeyDown(Keys.Left)) moveX -= projectedMoveSpeed;
            if (KeyboardState.IsKeyDown(Keys.D) || KeyboardState.IsKeyDown(Keys.Right)) moveX += projectedMoveSpeed;

            if (Math.Abs(moveX) > float.Epsilon)
            {
                var width = Math.Max(1f, UIRenderer.ScreenWidth);
                var updatedPan = GameState.ProjectedPanX + moveX;
                GameState.ProjectedPanX = (updatedPan % width + width) % width;
            }

            return;
        }

        float moveSpeed = 1000f * dt / Camera.Zoom;

        Vector2 move = Vector2.Zero;

        if (MouseState.MiddleButton == ButtonState.Pressed)
        {
            move += Vector2.Subtract(PrevMouseState.Position.ToVector2(), MouseState.Position.ToVector2());
        }

        if (KeyboardState.IsKeyDown(Keys.W) || KeyboardState.IsKeyDown(Keys.Up)) move.Y -= moveSpeed;
        if (KeyboardState.IsKeyDown(Keys.S) || KeyboardState.IsKeyDown(Keys.Down)) move.Y += moveSpeed;
        if (KeyboardState.IsKeyDown(Keys.A) || KeyboardState.IsKeyDown(Keys.Left)) move.X -= moveSpeed;
        if (KeyboardState.IsKeyDown(Keys.D) || KeyboardState.IsKeyDown(Keys.Right)) move.X += moveSpeed;

        if (move != Vector2.Zero)
        {
            var inputToWorldRotation = Matrix.CreateRotationZ(-Camera.Rotation);
            Camera.Move(Vector2.Transform(move, inputToWorldRotation));
        }
    }

    private void HandleCameraZoom()
    {
        if (GameState.CurrentViewMode == GameState.ViewMode.Projected)
            return;
        int scrollDelta = MouseState.ScrollWheelValue - PrevMouseState.ScrollWheelValue;

        if (scrollDelta != 0)
        {
            float zoomChange = scrollDelta > 0 ? 0.1f : -0.1f;
            Camera.AdjustZoom(zoomChange);
        }
    }

    private void HandleWarpControl()
    {
        if (KeyboardState.IsKeyDown(Keys.OemComma) && PrevKeyboardState.IsKeyUp(Keys.OemComma))
        {
            GameState.DecreaseWarp();
        }

        if (KeyboardState.IsKeyDown(Keys.OemPeriod) && PrevKeyboardState.IsKeyUp(Keys.OemPeriod))
        {
            GameState.IncreaseWarp();
        }
    }

    private void HandleLeftClick()
    {
        Vector2 mousePos = GetMouseWorldPosition();
        Vector2 maneuverMousePos = GetManeuverInteractionPosition();
        var selectedShip = GameState.SelectedShip;
        var manueverNode = GetActiveManeuverNode(selectedShip);
        if (MouseState.LeftButton == ButtonState.Pressed && PrevMouseState.LeftButton == ButtonState.Released)
        {
            // UI panel buttons take priority (screen-space)
            var uiResult = UIRenderer.GetActionAt(MouseState.Position.ToVector2());
            if (uiResult is not null)
            {
                ApplyUIAction(uiResult);
                return;
            }

            if (GameState.IsHohmannTransferDialogOpen)
            {
                if (GameState.IsHohmannTransferMouseTargetSelectionActive && selectedShip is not null)
                {
                    UpdateHohmannTransferMousePreview(forceApply: true);
                    GameState.IsHohmannTransferMouseTargetSelectionActive = false;
                }

                return;
            }

            if (selectedShip is not null)
            {
                if (manueverNode is not null)
                {
                    if (Vector2.Distance(maneuverMousePos, manueverNode.ProgradeButton.Position) < manueverNode.ProgradeButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Prograde, maneuverMousePos);
                        return;
                    }
                    else if (Vector2.Distance(maneuverMousePos, manueverNode.RetrogradeButton.Position) < manueverNode.RetrogradeButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Retrograde, maneuverMousePos);
                        return;
                    }
                    else if (Vector2.Distance(maneuverMousePos, manueverNode.NormalButton.Position) < manueverNode.NormalButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Normal, maneuverMousePos);
                        return;
                    }
                    else if (Vector2.Distance(maneuverMousePos, manueverNode.AntinormalButton.Position) < manueverNode.AntinormalButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Antinormal, maneuverMousePos);
                        return;
                    }
                    else if (Vector2.Distance(maneuverMousePos, manueverNode.ConfirmButton.Position) < manueverNode.ConfirmButton.Radius)
                    {
                        manueverNode.IsConfirmed = true;
                        return;
                    }
                    else if (Vector2.Distance(maneuverMousePos, manueverNode.CancelButton.Position) < manueverNode.CancelButton.Radius)
                    {
                        if (ReferenceEquals(manueverNode, selectedShip.NextManeuverNode))
                        {
                            selectedShip.NextManeuverNode = null;
                        }
                        else
                        {
                            selectedShip.ManeuverNode = null;
                            selectedShip.NextManeuverNode = null;
                        }
                        return;
                    }
                    if (Vector2.Distance(maneuverMousePos, manueverNode.ScreenPosition) < UIConstants.NodeRadius)
                    {
                        manueverNode.IsDragged = true;
                        DraggedNode = manueverNode;
                        return;
                    }
                }

                if (selectedShip.ManeuverNode is null)
                {
                    var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(selectedShip.Orbit, mousePos.ToNumerics());
                    if (orbitPos is not null)
                    {
                        // Disallow creating maneuver nodes at positions outside the control radius
                        var radiusAtPos = selectedShip.Orbit.GetRadiusFromFoci(orbitPos.TrueAnomaly);
                        var controlRadius = GameState.CentralBody.ControlRadius;
                        if (radiusAtPos < controlRadius)
                        {
                            selectedShip.ManeuverNode = new ManeuverNode()
                            {
                                TrueAnomaly = orbitPos.TrueAnomaly,
                                ScreenPosition = orbitPos.ScreenPosition,
                            };
                        }
                        return;
                    }
                }
                else if (selectedShip.NextManeuverNode is null && selectedShip.ManeuverNode.IsConfirmed)
                {
                    var predictedOrbit = selectedShip.ManeuverNode.GetPredictedOrbit(selectedShip.Orbit);
                    if (predictedOrbit is not null)
                    {
                        var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(predictedOrbit, mousePos.ToNumerics());
                        if (orbitPos is not null)
                        {
                            var radiusAtPos = predictedOrbit.GetRadiusFromFoci(orbitPos.TrueAnomaly);
                            var controlRadius = GameState.CentralBody.ControlRadius;
                            if (radiusAtPos < controlRadius)
                            {
                                selectedShip.NextManeuverNode = new ManeuverNode()
                                {
                                    TrueAnomaly = orbitPos.TrueAnomaly,
                                    ScreenPosition = orbitPos.ScreenPosition,
                                };
                            }
                            return;
                        }
                    }
                }
            }

            var orbitingObjects = GameState.OrbitingObjects
                .OrderBy(x => Vector2.Distance(mousePos, x.Orbit.PositionVector / GameConstants.RenderingScale))
                .ToList();
            float clickRadius = 10f;
            foreach (var orbitingObject in orbitingObjects)
            {
                if (Vector2.Distance(mousePos, orbitingObject.Orbit.PositionVector / GameConstants.RenderingScale) < clickRadius)
                {
                    if (GameState.SelectedOrbitingObject is not null && GameState.SelectedOrbitingObject != orbitingObject)
                        GameState.SelectedOrbitingObject.IsSelected = false;

                    orbitingObject.IsSelected = true;
                    GameState.SelectedOrbitingObject = orbitingObject;
                    return;
                }
            }

            if (GameState.SelectedOrbitingObject is not null)
            GameState.SelectedOrbitingObject.IsSelected = false;
            GameState.SelectedOrbitingObject = null;
            // clear any right-click target when selection is cleared
            GameState.TargetOrbitingObject = null;
            if (DraggedNode is not null)
            {
                DraggedNode.IsDragged = false;
                DraggedNode = null;
            }
        }
    }

    private void HandleLeftDrag()
    {
        if (DraggedNode is not null && MouseState.LeftButton == ButtonState.Pressed)
        {
            Vector2 mousePos = GetMouseWorldPosition();
            var ship = GameState.SelectedShip;
            var activeNode = GetActiveManeuverNode(ship);
            var activeOrbit = GetActiveManeuverBaseOrbit(ship);
            if (ship is null || activeNode is null || activeOrbit is null)
                return;

            var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(activeOrbit, mousePos.ToNumerics(), float.MaxValue);
            if (orbitPos is not null)
            {
                var radiusAtPos = activeOrbit.GetRadiusFromFoci(orbitPos.TrueAnomaly);
                var controlRadius = GameState.CentralBody.ControlRadius;
                if (radiusAtPos < controlRadius)
                {
                    activeNode.TrueAnomaly = orbitPos.TrueAnomaly;
                    activeNode.ScreenPosition = orbitPos.ScreenPosition;
                    activeNode.IsConfirmed = false;
                }
            }
        }
    }

    private void StartManeuverDrag(ManeuverDragType type, Vector2 startWorldPos)
    {
        CurrentManeuverDrag = type;
        DragStartMouseWorldPos = startWorldPos;
    }

    private void HandleManeuverDeltaVDrag()
    {
        var ship = GameState.SelectedShip;
        var node = GetActiveManeuverNode(ship);
        if (node == null) return;

        Vector2 currentMouseWorldPos = GetManeuverInteractionPosition();
        Vector2 dragVector = currentMouseWorldPos - DragStartMouseWorldPos;
        node.DragOffset = dragVector.ToNumerics();

        double distanceFromStart = dragVector.Length(); // used to scale sensitivity
        double baseRate = 100d; // base rate of delta-V change per screen inch-ish
        double speedMultiplier = Math.Max(0.2d, distanceFromStart); // prevents zero-speed, tweak as needed

        double delta = Vector2.Dot(dragVector, node.GetDirectionVectorForDrag(CurrentManeuverDrag)) * baseRate * 0.001d * speedMultiplier;
        if (delta == 0d)
            return;

        switch (CurrentManeuverDrag)
        {
            case ManeuverDragType.Prograde:
                node.ProgradeDeltaV += delta;
                break;
            case ManeuverDragType.Retrograde:
                node.ProgradeDeltaV -= delta;
                break;
            case ManeuverDragType.Normal:
                node.NormalDeltaV += delta;
                break;
            case ManeuverDragType.Antinormal:
                node.NormalDeltaV -= delta;
                break;
        }
        node.DragType = CurrentManeuverDrag;


        node.IsConfirmed = false;
    }

    private void ApplyUIAction(UIButtonResult result)
    {
        // Allow UI actions that don't depend on game state, or modal actions
        var allowedWhenPaused = result.Action switch
        {
            UIAction.PauseToggle => true,
            UIAction.WarpDecrease => true,
            UIAction.WarpIncrease => true,
            UIAction.ToggleOrbitsVisibility => true,
            UIAction.ToggleViewMode => true,
            UIAction.ToggleShowManeuvers => true,
            UIAction.CameraFocusSelected => true,
            UIAction.CameraResetView => true,
            UIAction.HohmannOpenDialog => true,
            UIAction.HohmannAltitude10Decrease => true,
            UIAction.HohmannAltitude10Increase => true,
            UIAction.HohmannAltitude50Decrease => true,
            UIAction.HohmannAltitude50Increase => true,
            UIAction.HohmannAltitude100Decrease => true,
            UIAction.HohmannAltitude100Increase => true,
            UIAction.HohmannConfirm => true,
            UIAction.HohmannCancel => true,
            _ => false
        };

        if (GameState.IsPaused && !allowedWhenPaused)
        {
            return;
        }

        switch (result.Action)
        {
            case UIAction.WarpDecrease:
                GameState.DecreaseWarp();
                return;
            case UIAction.WarpIncrease:
                GameState.IncreaseWarp();
                return;
            case UIAction.PauseToggle:
                GameState.TogglePause();
                return;
            case UIAction.ToggleOrbitsVisibility:
                GameState.ShowAllOrbits = !GameState.ShowAllOrbits;
                return;
            case UIAction.ToggleViewMode:
                if (GameState.CurrentViewMode == GameState.ViewMode.Projected)
                {
                    GameState.CurrentViewMode = GameState.ViewMode.Default;
                    if (_hasDefaultViewCameraPose)
                    {
                        Camera.SetPose(_defaultViewCameraPosition, _defaultViewCameraRotation, _defaultViewCameraZoom);
                        _hasDefaultViewCameraPose = false;
                    }
                }
                else
                {
                    _defaultViewCameraPosition = Camera.Position;
                    _defaultViewCameraRotation = Camera.Rotation;
                    _defaultViewCameraZoom = Camera.Zoom;
                    _hasDefaultViewCameraPose = true;

                    GameState.CurrentViewMode = GameState.ViewMode.Projected;

                    // if switching to projected mode, clear camera follow and reset any transitions
                    _cameraFollowSelected = false;
                    _hasCameraPreFocusPose = false;
                    GameState.IsCameraFocusedOnSelected = false;
                }
                return;
            case UIAction.ToggleShowManeuvers:
                GameState.ShowAllManeuvers = !GameState.ShowAllManeuvers;
                return;
            case UIAction.CameraFocusSelected:
                ToggleCameraFocusForSelectedObject();
                return;
            case UIAction.CameraResetView:
                ResetCameraView();
                return;
        }

        var ship = GameState.SelectedShip;
        if (ship is null) return;

        var activeNode = GetActiveManeuverNode(ship);

        switch (result.Action)
        {
            case UIAction.CircularizeAtPE:
                ApplyCircularize(ship, atPeriapsis: true);
                break;
            case UIAction.CircularizeAtAP:
                ApplyCircularize(ship, atPeriapsis: false);
                break;
            case UIAction.HohmannOpenDialog:
                // Cancel any existing maneuver nodes
                ship.ManeuverNode = null;
                ship.NextManeuverNode = null;
                GameState.IsHohmannTransferDialogOpen = true;
                GameState.IsHohmannTransferMouseTargetSelectionActive = true;
                ApplyQuickHohmannTransferToAltitude(ship);
                break;
            case UIAction.HohmannAltitude10Decrease:
                GameState.HohmannTransferTargetAltitudeMeters = Math.Max(0d, GameState.HohmannTransferTargetAltitudeMeters - 10_000d);
                ApplyQuickHohmannTransferToAltitude(ship);
                break;
            case UIAction.HohmannAltitude10Increase:
                GameState.HohmannTransferTargetAltitudeMeters = Math.Min(
                    GameState.CentralBody.ControlAltitudeMeters,
                    GameState.HohmannTransferTargetAltitudeMeters + 10_000d);
                ApplyQuickHohmannTransferToAltitude(ship);
                break;
            case UIAction.HohmannAltitude50Decrease:
                GameState.HohmannTransferTargetAltitudeMeters = Math.Max(0d, GameState.HohmannTransferTargetAltitudeMeters - 50_000d);
                ApplyQuickHohmannTransferToAltitude(ship);
                break;
            case UIAction.HohmannAltitude50Increase:
                GameState.HohmannTransferTargetAltitudeMeters = Math.Min(
                    GameState.CentralBody.ControlAltitudeMeters,
                    GameState.HohmannTransferTargetAltitudeMeters + 50_000d);
                ApplyQuickHohmannTransferToAltitude(ship);
                break;
            case UIAction.HohmannAltitude100Decrease:
                GameState.HohmannTransferTargetAltitudeMeters = Math.Max(0d, GameState.HohmannTransferTargetAltitudeMeters - 100_000d);
                ApplyQuickHohmannTransferToAltitude(ship);
                break;
            case UIAction.HohmannAltitude100Increase:
                GameState.HohmannTransferTargetAltitudeMeters = Math.Min(
                    GameState.CentralBody.ControlAltitudeMeters,
                    GameState.HohmannTransferTargetAltitudeMeters + 100_000d);
                ApplyQuickHohmannTransferToAltitude(ship);
                break;
            case UIAction.HohmannConfirm:
                ApplyQuickHohmannTransferToAltitude(ship);
                if (ship.ManeuverNode is not null)
                {
                    ship.ManeuverNode.IsConfirmed = true;
                }
                if (ship.NextManeuverNode is not null)
                {
                    ship.NextManeuverNode.IsConfirmed = true;
                }
                GameState.IsHohmannTransferDialogOpen = false;
                GameState.IsHohmannTransferMouseTargetSelectionActive = false;
                break;
            case UIAction.HohmannCancel:
                GameState.IsHohmannTransferDialogOpen = false;
                GameState.IsHohmannTransferMouseTargetSelectionActive = false;
                ship.ManeuverNode = null;
                ship.NextManeuverNode = null;
                break;
            case UIAction.ManeuverAccept:
                if (activeNode is not null)
                {
                    activeNode.IsConfirmed = true;
                }
                break;
            case UIAction.ManeuverCancel:
                if (ReferenceEquals(activeNode, ship.NextManeuverNode))
                {
                    ship.NextManeuverNode = null;
                }
                else
                {
                    ship.ManeuverNode = null;
                    ship.NextManeuverNode = null;
                }
                break;
            case UIAction.ManeuverProgradeStep:
                if (activeNode is not null)
                {
                    activeNode.ProgradeDeltaV += result.StepValue;
                    activeNode.IsConfirmed = false;
                }
                break;
            case UIAction.ManeuverNormalStep:
                if (activeNode is not null)
                {
                    activeNode.NormalDeltaV += result.StepValue;
                    activeNode.IsConfirmed = false;
                }
                break;
        }
    }

    private void ApplyCircularize(Ship ship, bool atPeriapsis)
    {
        var orbit = ship.Orbit;
        if (orbit.IsEscapeTrajectory) return;

        var mu = PhysicalConstants.G * GameState.CentralBody.Mass;
        var trueAnomaly = atPeriapsis ? 0d : Math.PI;
        var r = atPeriapsis ? orbit.Perigee : orbit.Apogee;
        var vCirc = Math.Sqrt(mu / r);
        var vCurrent = orbit.GetVelocityMagnitudeAtAngle(trueAnomaly);

        var screenPos = (orbit.GetPositionAtAngleD(trueAnomaly) / GameConstants.RenderingScale).ToVector2();
        ship.ManeuverNode = new ManeuverNode()
        {
            TrueAnomaly = trueAnomaly,
            ScreenPosition = screenPos,
            ProgradeDeltaV = vCirc - vCurrent,
        };
        ship.NextManeuverNode = null;
    }

    private void ApplyQuickHohmannTransferToAltitude(Ship ship)
    {
        ship.ApplyQuickHohmannTransferToAltitude(GameState.HohmannTransferTargetAltitudeMeters);
    }

    private void UpdateHohmannTransferMousePreview(bool forceApply = false)
    {
        if (!GameState.IsHohmannTransferDialogOpen)
        {
            GameState.IsHohmannTransferMouseTargetSelectionActive = false;
            return;
        }

        if (!GameState.IsHohmannTransferMouseTargetSelectionActive)
            return;

        var ship = GameState.SelectedShip;
        if (ship is null)
            return;

        var mouseWorldPos = GetMouseWorldPosition();
        var radiusMeters = mouseWorldPos.ToNumerics().Length() * GameConstants.RenderingScale;
        if (!double.IsFinite(radiusMeters))
            return;

        var altitudeMeters = radiusMeters - GameState.CentralBody.Radius;
        altitudeMeters = Math.Clamp(altitudeMeters, 0d, GameState.CentralBody.ControlAltitudeMeters);
        altitudeMeters = Math.Round(altitudeMeters / 10_000d) * 10_000d;

        var changed = Math.Abs(altitudeMeters - GameState.HohmannTransferTargetAltitudeMeters) > 0.1d;
        if (changed)
        {
            GameState.HohmannTransferTargetAltitudeMeters = altitudeMeters;
        }

        if (changed || forceApply)
        {
            ApplyQuickHohmannTransferToAltitude(ship);
        }
    }

    private static ManeuverNode GetActiveManeuverNode(Ship ship)
    {
        if (ship is null)
            return null;

        return ship.NextManeuverNode ?? ship.ManeuverNode;
    }

    private static Orbit GetActiveManeuverBaseOrbit(Ship ship)
    {
        if (ship is null)
            return null;

        if (ship.NextManeuverNode is not null && ship.ManeuverNode is not null)
        {
            var predicted = ship.ManeuverNode.GetPredictedOrbit(ship.Orbit);
            if (predicted is not null)
                return predicted;
        }

        return ship.Orbit;
    }

    private Vector2 GetMouseWorldPosition()
    {
        var screenPos = MouseState.Position.ToVector2();

        if (GameState.CurrentViewMode == GameState.ViewMode.Projected)
        {
            // Map screen position into polar-world position used by the simulation.
            var width = UIRenderer.ScreenWidth;
            var height = UIRenderer.ScreenHeight;
            const float topBuffer = 18f;
            const float bottomBuffer = 30f;
            var drawableHeight = Math.Max(1f, height - topBuffer - bottomBuffer);

            // account for projected-view horizontal camera pan (wrapped)
            var wrappedPanX = width > 0f ? (GameState.ProjectedPanX % width + width) % width : 0f;
            var wrappedScreenX = width > 0f ? ((screenPos.X + wrappedPanX) % width + width) % width : screenPos.X;

            // angle mapping: center X = angle 0, full width maps to -PI..PI
            var xRel = (wrappedScreenX - (width / 2f)) / (width / 2f);
            xRel = Math.Clamp(xRel, -1f, 1f);
            var angle = (double)(xRel * MathF.PI);

            // altitude mapping: bottom = surface (0), top = control altitude
            var yRel = ((height - bottomBuffer) - screenPos.Y) / drawableHeight;
            yRel = Math.Clamp(yRel, 0f, 1f);
            var altitudeMeters = yRel * (float)GameState.CentralBody.ControlAltitudeMeters;
            var radius = GameState.CentralBody.Radius + altitudeMeters;

            var worldX = radius * Math.Cos(angle);
            var worldY = radius * Math.Sin(angle);

            return new Vector2((float)(worldX / GameConstants.RenderingScale), (float)(worldY / GameConstants.RenderingScale));
        }

        return Camera.ScreenToWorld(screenPos);
    }

    private Vector2 GetManeuverInteractionPosition()
    {
        if (GameState.CurrentViewMode == GameState.ViewMode.Projected)
        {
            return MouseState.Position.ToVector2();
        }

        return GetMouseWorldPosition();
    }

    private void FocusCameraOnSelectedObject()
    {
        var selectedObject = GameState.SelectedOrbitingObject;
        if (selectedObject is null)
        {
            return;
        }

        if (!_hasCameraPreFocusPose)
        {
            _hasCameraPreFocusPose = true;
            _cameraPreFocusPosition = Camera.Position;
            _cameraPreFocusRotation = Camera.Rotation;
            _cameraPreFocusZoom = Camera.Zoom;
        }

        var targetPosition = selectedObject.Orbit.PositionVector / GameConstants.RenderingScale;
        var targetRotation = GetOrbitTangentCameraRotation(selectedObject.Orbit, Camera.Rotation);

        Camera.StartSelectionTransition();
        Camera.SetPose(targetPosition, targetRotation, 2f);
        _cameraFollowSelected = true;
        GameState.IsCameraFocusedOnSelected = true;
    }

    private void ToggleCameraFocusForSelectedObject()
    {
        if (_cameraFollowSelected || _hasCameraPreFocusPose)
        {
            ResetCameraView();
            return;
        }

        FocusCameraOnSelectedObject();
    }

    private void ResetCameraView()
    {
        if (!_hasCameraPreFocusPose)
        {
            _cameraFollowSelected = false;
            GameState.IsCameraFocusedOnSelected = false;
            return;
        }

        // start a transition back to the pre-focus pose (including zoom)
        Camera.StartSelectionTransition();
        Camera.SetPose(_cameraPreFocusPosition, _cameraPreFocusRotation, _cameraPreFocusZoom);

        _hasCameraPreFocusPose = false;
        _cameraFollowSelected = false;
        GameState.IsCameraFocusedOnSelected = false;
    }


    private static float GetOrbitTangentCameraRotation(Orbit orbit, float fallbackRotation)
    {
        var velocity = orbit.VelocityVector;
        if (velocity.LengthSquared() <= 1e-6f)
        {
            return fallbackRotation;
        }

        var tangentAngle = MathF.Atan2(velocity.Y, velocity.X);
        return -tangentAngle;
    }
}
