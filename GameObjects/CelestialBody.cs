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
    public double Radius { get; init; }
    public double Mass { get; init; }
    public Orbit Orbit { get; init; }
    public double BaseAtmosphereDensity { get; init; }
    public List<AtmosphereLayer> AtmosphereLayers { get; init; } = new List<AtmosphereLayer>();
    // Control altitude above the surface at which control is relinquished/despawn occurs (meters).
    // Default: 1,000,000 km
    public double ControlAltitudeMeters { get; init; }
    // Convenience: radius from system center to control altitude
    public double ControlRadius => Radius + ControlAltitudeMeters;
}


public class AtmosphereLayer
{
    public double Altitude { get; init; }
    public double Density { get; init; }
    public double Pressure { get; init; }
    public double Thickness { get; init; }
}