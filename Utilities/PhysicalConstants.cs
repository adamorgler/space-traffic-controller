using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Utilities;

public static class PhysicalConstants
{
    // EARTH
    private const float MASS_EARTH = 5.98e24f; // mass of Earth in kg
    private const float RADIUS_EARTH = 6.371e6f; // radius of Earth in meters
    private const float EARTH_ATMOSPHERE = 100e3f; // earth atmosphere height in meters
    private const int EARTH_DAY_LENGTH = 86400; // number of seconds in each day

    // physics constants
    public const float G = 6.673e-11f; // gravitational constant

    public static float RadiusOfPlanet { get { return RADIUS_EARTH; } }
    public static float MassOfPlanet { get { return MASS_EARTH; } }
}
