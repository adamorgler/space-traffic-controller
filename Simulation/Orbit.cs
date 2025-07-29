using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SpaceTrafficController.Utilities;

namespace SpaceTrafficController.Simulation;

public class Orbit
{
    public Orbit(double apoapsis, double periapsis, double argumentOfPeriapsis, double trueAnomaly)
    {
        if (apoapsis >= periapsis)
        {
            Apoapsis = apoapsis;
            Periapsis = periapsis;
        }
        else
        {
            Apoapsis = periapsis;
            Periapsis = apoapsis;
        }
        ArgumentOfPeriapsis = argumentOfPeriapsis;
        TrueAnomaly = trueAnomaly;
    }

    public double Apoapsis { get; init; }
    public double Periapsis { get; init; }
    public double ArgumentOfPeriapsis { get; set; } // angle of ellipse in radians
    public double TrueAnomaly { get; set; } // position in orbit in radians

    public double Apogee { get { return Apoapsis + PhysicalConstants.RadiusOfPlanet; } }
    public double Perigee { get { return Periapsis + PhysicalConstants.RadiusOfPlanet; } }
    public double SemiMajorAxis { get { return (Apogee + Perigee) / 2; } }
    public double SemiMinorAxis { get { return Math.Sqrt(Apogee * Perigee); } }
    public double Eccentricity { get { return Math.Sqrt(1 - (Math.Pow(SemiMinorAxis, 2) / Math.Pow(SemiMajorAxis, 2))); } }
    public double RadiusFromFoci { get { return GetRadiusFromFoci(TrueAnomaly); } }
    public Vector2 PositionVector { get { return GetPositionAtAngle(TrueAnomaly); } }
    public Vector2 VelocityVector { get { return GetVelocityAtAngle(TrueAnomaly); } }
    public double Velocity { get { return Math.Sqrt(PhysicalConstants.G * PhysicalConstants.MassOfPlanet * ((2 / RadiusFromFoci) - (1 / SemiMajorAxis))); } }

    public void Update(double timeStep)
    {
        TrueAnomaly += GetTrueAnomalyDelta(timeStep);
        if (TrueAnomaly > 2 * Math.PI) TrueAnomaly -= 2 * Math.PI;
    }

    public double GetTrueAnomalyDelta(double timeStep)
    {
        return (SemiMajorAxis * SemiMinorAxis * (1 / Math.Sqrt(Math.Pow(SemiMajorAxis, 3) / (PhysicalConstants.G * PhysicalConstants.MassOfPlanet))) * timeStep) / Math.Pow(RadiusFromFoci, 2);
    }

    public double GetRadiusFromFoci(double Angle)
    {
        return (SemiMajorAxis * (1 - Math.Pow(Eccentricity, 2))) / (1 + Eccentricity * Math.Cos(Angle));
    }

    public Vector2 GetPositionAtAngle(double Angle)
    {
        return MathUtils.PolarToCartesian(Angle, GetRadiusFromFoci(Angle)).Rotate(ArgumentOfPeriapsis);
    }

    public Vector2 GetVelocityAtAngle(double Angle)
    {
        return new Vector2
        {
            X = (float)(Velocity * (0 -Math.Sin(Angle)) / Math.Sqrt(1 + Math.Pow(Eccentricity, 2) + 2 * Eccentricity * Math.Cos(Angle))),
            Y = (float)(Velocity * (Eccentricity + Math.Cos(Angle)) / Math.Sqrt(1 + Math.Pow(Eccentricity, 2) + 2 * Eccentricity * Math.Cos(Angle))),
        }.Rotate(ArgumentOfPeriapsis);
    }
}
