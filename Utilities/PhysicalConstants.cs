using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Utilities;

public static class PhysicalConstants
{
    // EARTH
    private const double MASS_EARTH = 5.98e24; // mass of Earth in kg
    private const double RADIUS_EARTH = 6.371e6; // radius of Earth in meters
    private const double EARTH_ATMOSPHERE = 100e3; // earth atmosphere height in meters
    private const int EARTH_DAY_LENGTH = 86400; // number of seconds in each day

    // physics constants
    public const double G = 6.673e-11; // gravitational constant

    public static double RadiusOfPlanet { get { return RADIUS_EARTH; } }
    public static double MassOfPlanet { get { return MASS_EARTH; } }
}
