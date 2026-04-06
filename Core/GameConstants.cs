using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Core;

public static class GameConstants
{
    public const int ShipSepration = 150000;

    public const double ControlLaneWidthMeters = 50e3;
    public const double ControlLaneHalfWidthMeters = ControlLaneWidthMeters / 2d;
    public const double ControlLaneLongitudinalHalfExtentMeters = 50e3;

    // A ship within this distance of a lane boundary is considered to occupy both adjacent lanes.
    public const double LaneBoundaryProximityMeters = 5000d;

    public const int RenderingScale = 5000;

    public const int UnitScale = 1000;
}
