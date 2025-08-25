using SpaceTrafficController.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Utilities;

public static class PhysicalConstants
{
    // EARTH
    public const float MASS_EARTH = 5.98e24f; // mass of Earth in kg
    public const float RADIUS_EARTH = 6.371e6f; // radius of Earth in meters
    public const float ATMOS_THICKNESS_EARTH = 100e3f;
    public const int EARTH_DAY_LENGTH = 86400; // number of seconds in each day
    // TITAN
    public const float MASS_TITAN = 1.345e23f;
    public const float RADIUS_TITAN = 2574e3f;
    public const float ATMOS_THICKNESS_TITAN = 500e3f;
    public const float ATMOS_BASE_DENSITY_TITAN = 5.3f; // kg/m^3
    public const float ATMOS_BASE_PRESSURE_TITAN = 152000f; // pascals

    // physics constants
    public const float G = 6.673e-11f; // gravitational constant
}
