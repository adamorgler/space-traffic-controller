using System;

namespace SpaceTrafficController.Utilities;

public static class MathExtensions
{
    public static double ToRadians(this double degrees) => 
        degrees * (Math.PI / 180);

    public static double ToDegrees(this double radians) =>
        radians * (180 / Math.PI);

    public static Microsoft.Xna.Framework.Vector2 ToXna(this System.Numerics.Vector2 v)
    => new Microsoft.Xna.Framework.Vector2(v.X, v.Y);

    public static System.Numerics.Vector2 ToNumerics(this Microsoft.Xna.Framework.Vector2 v)
        => new System.Numerics.Vector2(v.X, v.Y);
}
