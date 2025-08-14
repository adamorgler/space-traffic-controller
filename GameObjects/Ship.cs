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
    public ShipState State { get; set; }
    public ShipStatus Status { get; set; } = new();

    public Vector2 Position { get { return Orbit.PositionVector; } }
}

public enum ShipState
{
    Orbiting,
    Launching,
    Deorbiting
}

public class ShipStatus
{
    public bool IsSelected { get; set; } = false;
    public bool IsEncroached { get; set; } = false;
}
