using System;

namespace SpaceTrafficController.Utilities;

public static class MathExtensions
{
    public static double ToRadians(this double degrees) => 
        degrees * (Math.PI / 180);

    public static double ToDegrees(this double radians) =>
        radians * (180 / Math.PI);

    public static float ToRadians(this float degrees) =>
        degrees * (MathF.PI / 180);

    public static float ToDegrees(this float radians) =>
        radians * (180 / MathF.PI);

    public static double NormalizeAngle(this double angle)
    {
        angle %= (2 * Math.PI);
        if (angle < 0)
            angle += 2 * Math.PI;
        return angle;
    }

    public static Microsoft.Xna.Framework.Vector2 ToXna(this System.Numerics.Vector2 v)
    => new Microsoft.Xna.Framework.Vector2(v.X, v.Y);

    public static System.Numerics.Vector2 ToNumerics(this Microsoft.Xna.Framework.Vector2 v)
        => new System.Numerics.Vector2(v.X, v.Y);
}
