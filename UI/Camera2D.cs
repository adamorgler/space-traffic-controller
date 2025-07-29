using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;

public class Camera2D
{
    private readonly GraphicsDevice GraphicsDevice;

    public Vector2 Position { get; set; } = Vector2.Zero;
    public float Zoom { get; set; } = 1f;
    public float Rotation { get; set; } = 0f;

    public Camera2D(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice;
    }

    public void Move(Vector2 delta)
    {
        Position += delta;
    }

    public void AdjustZoom(float delta)
    {
        Zoom = Math.Clamp(Zoom + delta, 0.5f, 2f); // adjust as needed
    }

    public Matrix GetTransform()
    {
        var viewport = GraphicsDevice.Viewport;
        var screenCenter = new Vector2(viewport.Width / 2f, viewport.Height / 2f);

        return
            Matrix.CreateTranslation(-Position.X, -Position.Y, 0f) *
            Matrix.CreateRotationZ(Rotation) *
            Matrix.CreateScale(Zoom, Zoom, 1f) *
            Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0f);
    }

    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        Matrix inverse = Matrix.Invert(GetTransform());
        return Vector2.Transform(screenPos, inverse);
    }

    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        return Vector2.Transform(worldPos, GetTransform());
    }
}
