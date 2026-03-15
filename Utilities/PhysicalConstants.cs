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
    public const double MASS_EARTH = 5.98e24; // mass of Earth in kg
    public const double RADIUS_EARTH = 6.371e6; // radius of Earth in meters
    public const double ATMOS_THICKNESS_EARTH = 100e3;
    public const int EARTH_DAY_LENGTH = 86400; // number of seconds in each day
    // TITAN
    public const double MASS_TITAN = 1.345e23;
    public const double RADIUS_TITAN = 2574e3;
    public const double ATMOS_THICKNESS_TITAN = 500e3;
    public const double ATMOS_BASE_DENSITY_TITAN = 5.3; // kg/m^3
    public const double ATMOS_BASE_PRESSURE_TITAN = 152000; // pascals

    // physics constants
    public const double G = 6.673e-11; // gravitational constant
}
