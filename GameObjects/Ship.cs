using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;

namespace SpaceTrafficController.GameObjects;

public class Ship : HasOrbit
{
    public Ship(Orbit orbit) : base(orbit)
    {
    }

    public string Name { get; set; }
    public ShipState ShipState { get; set; }
}

public enum ShipState
{
    Orbiting,
    Launching,
    Deorbiting
}
