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
    public Station(Orbit orbit) : base(orbit)
    {
    }

    public string Name { get; set; }

    public int NumberOfRunways { get; set; }
}
