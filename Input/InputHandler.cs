using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.Linq;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Utilities;
using System;
using SpaceTrafficController.UI;
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

    private bool _followingShip = false;
    private Vector2 _cameraFollowOffset = Vector2.Zero;

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

        HandleCameraMovement(gameTime);
        HandleCameraZoom();
        HandleWarpControl();

        // left click
        HandleLeftClick();
        HandleLeftDrag();
        HandleManeuverDeltaVDrag();

        if (_followingShip && GameState.SelectedOrbitingObject is not null)
        {
            var selectedObjectPos = GameState.SelectedOrbitingObject.Orbit.PositionVector / GameConstants.RenderingScale;
            float targetRotation = Camera.Rotation;

            var velocity = GameState.SelectedOrbitingObject.Orbit.VelocityVector;
            if (velocity.LengthSquared() > 1e-6f)
            {
                var tangentAngle = MathF.Atan2(velocity.Y, velocity.X);
                var baseRotation = -tangentAngle;

                // Convert local offset to world using the base tangent rotation to find approximate camera position.
                var baseWorldOffset = Vector2.Transform(_cameraFollowOffset, Matrix.CreateRotationZ(-baseRotation));
                var baseCameraFollowPos = selectedObjectPos + baseWorldOffset;

                float additionalOffsetRotation = 0f;
                if (_cameraFollowOffset.LengthSquared() > 1e-6f
                    && selectedObjectPos.LengthSquared() > 1e-6f
                    && baseCameraFollowPos.LengthSquared() > 1e-6f)
                {
                    var selectedAngle = MathF.Atan2(selectedObjectPos.Y, selectedObjectPos.X);
                    var cameraAngle = MathF.Atan2(baseCameraFollowPos.Y, baseCameraFollowPos.X);
                    additionalOffsetRotation = MathHelper.WrapAngle(cameraAngle - selectedAngle);
                }

                targetRotation = -(tangentAngle + additionalOffsetRotation);
            }

            // Recompute world offset from the final rotation so the local offset stays visually stable.
            var worldOffset = Vector2.Transform(_cameraFollowOffset, Matrix.CreateRotationZ(-targetRotation));
            var cameraFollowPos = selectedObjectPos + worldOffset;
            Camera.SetPose(cameraFollowPos, targetRotation);
        }

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

        PrevKeyboardState = KeyboardState;
        PrevMouseState = MouseState;
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
            if (_followingShip && GameState.SelectedOrbitingObject is not null)
            {
                // Offset is camera-local: add the raw (unrotated) input directly.
                _cameraFollowOffset += move;
            }
            else
            {
                var inputToWorldRotation = Matrix.CreateRotationZ(-Camera.Rotation);
                Camera.Move(Vector2.Transform(move, inputToWorldRotation));
            }
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
                    _followingShip = true;
                    _cameraFollowOffset = Vector2.Zero;
                    Camera.StartSelectionTransition();
                    return;
                }
            }

            if (GameState.SelectedOrbitingObject is not null)
                GameState.SelectedOrbitingObject.IsSelected = false;

            GameState.SelectedOrbitingObject = null;
            _followingShip = false;
            _cameraFollowOffset = Vector2.Zero;
            Camera.StartSelectionTransition();
            Camera.SetPose(Camera.Position, 0f);
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
                    ship.ManeuverNode.IsConfirmed = true;
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
}
