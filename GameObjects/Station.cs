using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using SpaceTrafficController.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.GameObjects;

public class Station : HasOrbit
{
    public const int DefaultApproachLaneCount = 3;
    public const double DefaultControlAreaHalfAltitudeMeters = GameConstants.ControlLaneHalfWidthMeters + (DefaultApproachLaneCount * GameConstants.ControlLaneWidthMeters);
    public const double DefaultControlAreaArrivalExtentMeters = 400e3;
    public const double DefaultControlAreaDepartureExtentMeters = 100e3;

    public Station(Orbit orbit) : base(orbit)
    {
    }

    public string Name { get; set; }

    public int NumberOfRunways { get; set; }

    public int ApproachLaneCount { get; set; } = DefaultApproachLaneCount;

    public double ControlAreaHalfAltitudeMeters { get; set; } = DefaultControlAreaHalfAltitudeMeters;

    public double ControlAreaArrivalExtentMeters { get; set; } = DefaultControlAreaArrivalExtentMeters;

    public double ControlAreaDepartureExtentMeters { get; set; } = DefaultControlAreaDepartureExtentMeters;

    public override void UpdateExtension(double timeStep)
    {
        return;
    }
}
