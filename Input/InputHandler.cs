using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpaceTrafficController.Core;
using MonoGame.Extended;
using SpaceTrafficController.GameObjects;
using System.Threading;

namespace SpaceTrafficController.Input;

public class InputHandler
{
    private readonly Camera2D Camera;
    private readonly GameState GameState;
    private KeyboardState PrevKeyboardState;
    private MouseState PrevMouseState;

    public InputHandler(Camera2D camera, GameState gameState)
    {
        Camera = camera;
        GameState = gameState;
    }

    public void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        HandleCameraMovement(keyboard, mouse, gameTime);
        HandleCameraZoom(mouse);
        HandleWarpControl(keyboard);
        HandleShipSelection(mouse);

        PrevKeyboardState = Keyboard.GetState();
        PrevMouseState = Mouse.GetState();
    }

    private void HandleCameraMovement(KeyboardState keyboard, MouseState mouse, GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float moveSpeed = 1000f * dt / Camera.Zoom;

        Vector2 move = Vector2.Zero;

        if (mouse.MiddleButton == ButtonState.Pressed)
        {
            move += Vector2.Subtract(PrevMouseState.Position.ToVector2(), mouse.Position.ToVector2());
        }

        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) move.Y -= moveSpeed;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) move.Y += moveSpeed;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) move.X -= moveSpeed;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) move.X += moveSpeed;

        Camera.Move(move);
    }

    private void HandleCameraZoom(MouseState mouse)
    {
        int scrollDelta = mouse.ScrollWheelValue - PrevMouseState.ScrollWheelValue;

        if (scrollDelta != 0)
        {
            float zoomChange = scrollDelta > 0 ? 0.1f : -0.1f;
            Camera.AdjustZoom(zoomChange);
        }
    }

    private void HandleWarpControl(KeyboardState keyboard)
    {
        if (keyboard.IsKeyDown(Keys.OemComma) && PrevKeyboardState.IsKeyUp(Keys.OemComma))
        {
            GameState.DecreaseWarp();
        }

        if (keyboard.IsKeyDown(Keys.OemPeriod) && PrevKeyboardState.IsKeyUp(Keys.OemPeriod))
        {
            GameState.IncreaseWarp();
        }
    }

    private void HandleShipSelection(MouseState mouse)
    {
        if (mouse.LeftButton == ButtonState.Pressed && PrevMouseState.LeftButton == ButtonState.Released)
        {
            Vector2 mouseWorldPos = GetMouseWorldPosition(mouse);

            var ships = GameState.Ships.OrderBy(x => Vector2.Distance(mouseWorldPos, x.Position / GameConstants.Scale)).ToList();

            float clickRadius = 10f;
            foreach (var ship in ships)
            {
                if (Vector2.Distance(mouseWorldPos, ship.Position / GameConstants.Scale) < clickRadius && !ship.Status.IsSelected)
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

    private Vector2 GetMouseWorldPosition(MouseState mouse)
    {
        var screenPos = mouse.Position.ToVector2();
        return Camera.ScreenToWorld(screenPos);
    }
}
