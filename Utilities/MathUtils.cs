using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Utilities;

public static class MathUtils
{
    public static Vector2 PolarToCartesian(double angle, double radius)
    {
        return new Vector2()
        {
            X = (float) (radius * Math.Cos(angle)),
            Y = (float) (radius * Math.Sin(angle)),
        };
    }

    public static Vector2 Rotate(this Vector2 v, double radians)
    {
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        return new Vector2(
            (float) (v.X * cos - v.Y * sin),
            (float) (v.X * sin + v.Y * cos)
        );
    }
}
