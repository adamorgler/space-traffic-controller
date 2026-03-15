using System;
using System.Numerics;

namespace SpaceTrafficController.Utilities;

public readonly struct DVector2
{
    public DVector2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; init; }
    public double Y { get; init; }

    public static DVector2 Zero => new(0d, 0d);

    public double Length() => Math.Sqrt((X * X) + (Y * Y));

    public static double Dot(DVector2 a, DVector2 b) => (a.X * b.X) + (a.Y * b.Y);

    public static DVector2 Normalize(DVector2 v)
    {
        var length = v.Length();
        if (length <= 0d)
        {
            return Zero;
        }

        return v / length;
    }

    public Vector2 ToVector2() => new(ToSingleClamped(X), ToSingleClamped(Y));

    public static DVector2 FromVector2(Vector2 v) => new(v.X, v.Y);

    public static DVector2 operator +(DVector2 a, DVector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static DVector2 operator -(DVector2 a, DVector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static DVector2 operator -(DVector2 a) => new(-a.X, -a.Y);
    public static DVector2 operator *(DVector2 v, double s) => new(v.X * s, v.Y * s);
    public static DVector2 operator *(double s, DVector2 v) => new(v.X * s, v.Y * s);
    public static DVector2 operator /(DVector2 v, double s) => new(v.X / s, v.Y / s);

    public override string ToString() => $"<{X}, {Y}>";

    private static float ToSingleClamped(double value)
    {
        if (double.IsNaN(value))
        {
            return 0f;
        }

        if (value > float.MaxValue)
        {
            return float.MaxValue;
        }

        if (value < float.MinValue)
        {
            return float.MinValue;
        }

        return (float)value;
    }
}
