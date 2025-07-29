using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Utilities;

public static class MathExtensions
{
    public static double ToRadians(this double degrees) => 
        degrees * (Math.PI / 180);

    public static double ToDegrees(this double radians) =>
        radians * (180 / Math.PI);
}
