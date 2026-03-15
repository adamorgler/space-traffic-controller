using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Utilities;

public static class MathUtils
{
    public static DVector2 PolarToCartesian(double angle, double radius)
    {
        return new DVector2(
            x: radius * Math.Cos(angle),
            y: radius * Math.Sin(angle)
        );
    }

    public static Vector2 PolarToCartesian(float angle, float radius)
    {
        return new Vector2()
        {
            X = radius * MathF.Cos(angle),
            Y = radius * MathF.Sin(angle),
        };
    }

    public static Vector2 Rotate(this Vector2 v, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(
            v.X * cos - v.Y * sin,
            v.X * sin + v.Y * cos
        );
    }

    public static DVector2 Rotate(this DVector2 v, double radians)
    {
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        return new DVector2(
            x: (v.X * cos) - (v.Y * sin),
            y: (v.X * sin) + (v.Y * cos)
        );
    }
}
