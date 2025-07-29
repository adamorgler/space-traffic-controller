using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpaceTrafficController.Core;
using MonoGame.Extended;

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
        HandleCameraMovement(gameTime);
        HandleCameraZoom();
        HandleWarpControl();

        PrevKeyboardState = Keyboard.GetState();
        PrevMouseState = Mouse.GetState();
    }

    private void HandleCameraMovement(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float moveSpeed = 1000f * dt / Camera.Zoom;

        Vector2 move = Vector2.Zero;

        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) move.Y -= moveSpeed;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) move.Y += moveSpeed;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) move.X -= moveSpeed;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) move.X += moveSpeed;

        Camera.Move(move);
    }

    private void HandleCameraZoom()
    {
        var mouse = Mouse.GetState();
        int scrollDelta = mouse.ScrollWheelValue - PrevMouseState.ScrollWheelValue;

        if (scrollDelta != 0)
        {
            float zoomChange = scrollDelta > 0 ? 0.1f : -0.1f;
            Camera.AdjustZoom(zoomChange);
        }
    }

    private void HandleWarpControl()
    {
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Keys.OemComma) && PrevKeyboardState.IsKeyUp(Keys.OemComma))
        {
            GameState.DecreaseWarp();
        }

        if (keyboard.IsKeyDown(Keys.OemPeriod) && PrevKeyboardState.IsKeyUp(Keys.OemPeriod))
        {
            GameState.IncreaseWarp();
        }
    }
}
