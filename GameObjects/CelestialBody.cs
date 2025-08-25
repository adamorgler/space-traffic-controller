using SpaceTrafficController.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.GameObjects;

public class CelestialBody
{
    public string Name { get; init; }
    public float Radius { get; init; }
    public float Mass { get; init; }
    public Orbit Orbit { get; init; }
    public float BaseAtmosphereDensity { get; init; }
    public List<AtmosphereLayer> AtmosphereLayers { get; init; } = new List<AtmosphereLayer>();
}


public class AtmosphereLayer
{
    public float Altitude { get; init; }
    public float Density { get; init; }
    public float Pressure { get; init; }
    public float Thickness { get; init; }
}