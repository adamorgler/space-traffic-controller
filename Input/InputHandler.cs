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
            var manueverNode = selectedShip?.ManeuverNode ?? null;

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
        var manueverNode = selectedShip?.ManeuverNode;

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
            }
        }
    }

    private void HandleCameraMovement(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
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
        var selectedShip = GameState.SelectedShip;
        var manueverNode = selectedShip?.ManeuverNode ?? null;
        if (MouseState.LeftButton == ButtonState.Pressed && PrevMouseState.LeftButton == ButtonState.Released)
        {
            // UI panel buttons take priority (screen-space)
            var uiResult = UIRenderer.GetActionAt(MouseState.Position.ToVector2());
            if (uiResult is not null)
            {
                ApplyUIAction(uiResult);
                return;
            }
            if (selectedShip is not null)
            {
                if (manueverNode is not null)
                {
                    if (Vector2.Distance(mousePos, manueverNode.ProgradeButton.Position) < manueverNode.ProgradeButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Prograde, mousePos);
                        return;
                    }
                    else if (Vector2.Distance(mousePos, manueverNode.RetrogradeButton.Position) < manueverNode.RetrogradeButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Retrograde, mousePos);
                        return;
                    }
                    else if (Vector2.Distance(mousePos, manueverNode.NormalButton.Position) < manueverNode.NormalButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Normal, mousePos);
                        return;
                    }
                    else if (Vector2.Distance(mousePos, manueverNode.AntinormalButton.Position) < manueverNode.AntinormalButton.Radius)
                    {
                        StartManeuverDrag(ManeuverDragType.Antinormal, mousePos);
                        return;
                    }
                    else if (Vector2.Distance(mousePos, manueverNode.ConfirmButton.Position) < manueverNode.ConfirmButton.Radius)
                    {
                        manueverNode.IsConfirmed = true;
                        // auto-deselect the ship when maneuver is confirmed
                        selectedShip.IsSelected = false;
                        GameState.SelectedOrbitingObject = null;
                        GameState.TargetOrbitingObject = null;
                        return;
                    }
                    else if (Vector2.Distance(mousePos, manueverNode.CancelButton.Position) < manueverNode.CancelButton.Radius)
                    {
                        selectedShip.ManeuverNode = null;
                        return;
                    }
                    if (Vector2.Distance(mousePos, manueverNode.ScreenPosition) < UIConstants.NodeRadius)
                    {
                        manueverNode.IsDragged = true;
                        DraggedNode = manueverNode;
                        return;
                    }
                }
                var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(selectedShip.Orbit, mousePos.ToNumerics());
                if (orbitPos is not null && manueverNode is null)
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
            if (ship is null || ship.ManeuverNode is null)
                return;

            var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(ship.Orbit, mousePos.ToNumerics(), float.MaxValue);
            if (orbitPos is not null)
            {
                var radiusAtPos = ship.Orbit.GetRadiusFromFoci(orbitPos.TrueAnomaly);
                var controlRadius = GameState.CentralBody.ControlRadius;
                if (radiusAtPos < controlRadius)
                {
                    ship.ManeuverNode.TrueAnomaly = orbitPos.TrueAnomaly;
                    ship.ManeuverNode.ScreenPosition = orbitPos.ScreenPosition;
                    ship.ManeuverNode.IsConfirmed = false;
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
        var node = ship?.ManeuverNode;
        if (node == null) return;

        Vector2 currentMouseWorldPos = GetMouseWorldPosition();
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
        if (GameState.IsPaused && result.Action != UIAction.PauseToggle)
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
            case UIAction.ToggleShowManeuvers:
                GameState.ShowAllManeuvers = !GameState.ShowAllManeuvers;
                return;
            case UIAction.CameraFocusSelected:
                FocusCameraOnSelectedObject();
                return;
            case UIAction.CameraResetView:
                ResetCameraView();
                return;
        }

        var ship = GameState.SelectedShip;
        if (ship is null) return;

        switch (result.Action)
        {
            case UIAction.CircularizeAtPE:
                ApplyCircularize(ship, atPeriapsis: true);
                break;
            case UIAction.CircularizeAtAP:
                ApplyCircularize(ship, atPeriapsis: false);
                break;
            case UIAction.ManeuverAccept:
                if (ship.ManeuverNode is not null)
                {
                    ship.ManeuverNode.IsConfirmed = true;
                    // auto-deselect ship after accepting maneuver
                    ship.IsSelected = false;
                    GameState.SelectedOrbitingObject = null;
                    GameState.TargetOrbitingObject = null;
                }
                break;
            case UIAction.ManeuverCancel:
                ship.ManeuverNode = null;
                break;
            case UIAction.ManeuverProgradeStep:
                if (ship.ManeuverNode is not null)
                {
                    ship.ManeuverNode.ProgradeDeltaV += result.StepValue;
                    ship.ManeuverNode.IsConfirmed = false;
                }
                break;
            case UIAction.ManeuverNormalStep:
                if (ship.ManeuverNode is not null)
                {
                    ship.ManeuverNode.NormalDeltaV += result.StepValue;
                    ship.ManeuverNode.IsConfirmed = false;
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
    }

    private Vector2 GetMouseWorldPosition()
    {
        var screenPos = MouseState.Position.ToVector2();
        return Camera.ScreenToWorld(screenPos);
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
    }

    private void ResetCameraView()
    {
        if (!_hasCameraPreFocusPose)
        {
            return;
        }

        // start a transition back to the pre-focus pose (including zoom)
        Camera.StartSelectionTransition();
        Camera.SetPose(_cameraPreFocusPosition, _cameraPreFocusRotation, _cameraPreFocusZoom);

        _hasCameraPreFocusPose = false;
        _cameraFollowSelected = false;
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
