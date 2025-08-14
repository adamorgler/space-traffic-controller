using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.Linq;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Utilities;
using System;

namespace SpaceTrafficController.Input;

public class InputHandler
{
    private readonly Camera2D Camera;
    private readonly GameState GameState;
    private KeyboardState KeyboardState;
    private MouseState MouseState;
    private KeyboardState PrevKeyboardState;
    private MouseState PrevMouseState;

    public InputHandler(Camera2D camera, GameState gameState)
    {
        Camera = camera;
        GameState = gameState;
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

        Camera.Move(move);
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
        if (MouseState.LeftButton == ButtonState.Pressed && PrevMouseState.LeftButton == ButtonState.Released)
        {
            Vector2 mousePos = GetMouseWorldPosition();

            var selectedShip = GameState.SelectedShip;
            if (selectedShip is not null)
            {
                var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(selectedShip.Orbit, mousePos.ToNumerics());
                if (orbitPos is not null)
                {
                    selectedShip.ManeuverNode = new ManeuverNode
                    {
                        TrueAnomaly = orbitPos.TrueAnomaly,
                        Position = orbitPos.WorldPosition,
                    };
                    return;
                }
            }

            var ships = GameState.Ships.OrderBy(x => Vector2.Distance(mousePos, x.Position / GameConstants.Scale)).ToList();
            float clickRadius = 10f;
            foreach (var ship in ships)
            {
                if (Vector2.Distance(mousePos, ship.Position / GameConstants.Scale) < clickRadius && !ship.Status.IsSelected)
                {
                    if (GameState.SelectedShip is not null)
                        GameState.SelectedShip.Status.IsSelected = false;
                    ship.Status.IsSelected = true;
                    GameState.SelectedShip = ship;
                    return;
                }
                ship.Status.IsSelected = false;
            }
            GameState.SelectedShip = null;
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        var screenPos = MouseState.Position.ToVector2();
        return Camera.ScreenToWorld(screenPos);
    }
}
