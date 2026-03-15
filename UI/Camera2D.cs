using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;

public class Camera2D
{
    private readonly GraphicsDevice GraphicsDevice;

    private Vector2 _desiredPosition = Vector2.Zero;
    private float _desiredRotation = 0f;

    private bool _isTransitioning = false;
    private float _transitionElapsedSeconds = 0f;
    private float _transitionDurationSeconds = 0.5f;
    private Vector2 _transitionStartPosition = Vector2.Zero;
    private float _transitionStartRotation = 0f;

    public Vector2 Position { get; set; } = Vector2.Zero;
    public float Zoom { get; set; } = 1f;
    public float Rotation { get; set; } = 0f;

    public float SelectionTransitionDurationSeconds
    {
        get => _transitionDurationSeconds;
        set => _transitionDurationSeconds = Math.Max(0.01f, value);
    }

    public Camera2D(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice;
        _desiredPosition = Position;
        _desiredRotation = Rotation;
    }

    public void Move(Vector2 delta)
    {
        Position += delta;
        _desiredPosition = Position;
    }

    public void AdjustZoom(float delta)
    {
        Zoom = Math.Clamp(Zoom + delta, 0.5f, 2f); // adjust as needed
    }

    public void SetPose(Vector2 position, float rotation)
    {
        _desiredPosition = position;
        _desiredRotation = rotation;

        if (!_isTransitioning)
        {
            Position = _desiredPosition;
            Rotation = _desiredRotation;
        }
    }

    public void StartSelectionTransition(float? durationSeconds = null)
    {
        _transitionStartPosition = Position;
        _transitionStartRotation = Rotation;
        _transitionElapsedSeconds = 0f;
        _isTransitioning = true;

        if (durationSeconds.HasValue)
        {
            SelectionTransitionDurationSeconds = durationSeconds.Value;
        }
    }

    public void Update(GameTime gameTime)
    {
        if (!_isTransitioning)
        {
            return;
        }

        _transitionElapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
        float t = Math.Clamp(_transitionElapsedSeconds / _transitionDurationSeconds, 0f, 1f);
        float easedT = MathHelper.SmoothStep(0f, 1f, t);

        Position = Vector2.Lerp(_transitionStartPosition, _desiredPosition, easedT);
        var delta = MathHelper.WrapAngle(_desiredRotation - _transitionStartRotation);
        Rotation = _transitionStartRotation + (delta * easedT);

        if (t >= 1f)
        {
            Position = _desiredPosition;
            Rotation = _desiredRotation;
            _isTransitioning = false;
        }
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
