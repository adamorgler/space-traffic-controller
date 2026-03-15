using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.GameObjects;

public class Station : HasOrbit
{
    public const double DefaultControlAreaHalfAltitudeMeters = 75e3;
    public const double DefaultControlAreaHalfForwardMeters = 250e3;

    public Station(Orbit orbit) : base(orbit)
    {
    }

    public string Name { get; set; }

    public int NumberOfRunways { get; set; }

    public double ControlAreaHalfAltitudeMeters { get; set; } = DefaultControlAreaHalfAltitudeMeters;

    public double ControlAreaHalfForwardMeters { get; set; } = DefaultControlAreaHalfForwardMeters;

    public override void UpdateExtension(double timeStep)
    {
        return;
    }
}
