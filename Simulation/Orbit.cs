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
    public Orbit(float apoapsis, float periapsis, float argumentOfPeriapsis, float trueAnomaly)
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

    public float Apoapsis { get; init; }
    public float Periapsis { get; init; }
    public float ArgumentOfPeriapsis { get; set; } // angle of ellipse in radians
    public float TrueAnomaly { get; set; } // position in orbit in radians

    public float Apogee { get { return Apoapsis + PhysicalConstants.RadiusOfPlanet; } }
    public float Perigee { get { return Periapsis + PhysicalConstants.RadiusOfPlanet; } }
    public float SemiMajorAxis { get { return (Apogee + Perigee) / 2; } }
    public float SemiMinorAxis { get { return MathF.Sqrt(Apogee * Perigee); } }
    public float Eccentricity { get { return MathF.Sqrt(1 - (MathF.Pow(SemiMinorAxis, 2) / MathF.Pow(SemiMajorAxis, 2))); } }
    public float RadiusFromFoci { get { return GetRadiusFromFoci(TrueAnomaly); } }
    public Vector2 PositionVector { get { return GetPositionAtAngle(TrueAnomaly); } }
    public Vector2 VelocityVector { get { return GetVelocityAtAngle(TrueAnomaly); } }
    public float Velocity { get { return GetVelocityMagnitudeAtAngle(TrueAnomaly); } }

    public void Update(float timeStep)
    {
        TrueAnomaly += GetTrueAnomalyDelta(timeStep);
        if (TrueAnomaly < 0) TrueAnomaly += 2 * MathF.PI;
        if (TrueAnomaly >= 2 * MathF.PI) TrueAnomaly -= 2 * MathF.PI;
    }

    public float GetTrueAnomalyDelta(float timeStep)
    {
        return (SemiMajorAxis * SemiMinorAxis * (1 / MathF.Sqrt(MathF.Pow(SemiMajorAxis, 3) / (PhysicalConstants.G * PhysicalConstants.MassOfPlanet))) * timeStep) / MathF.Pow(RadiusFromFoci, 2);
    }

    public float GetRadiusFromFoci(float Angle)
    {
        return (SemiMajorAxis * (1 - MathF.Pow(Eccentricity, 2))) / (1 + Eccentricity * MathF.Cos(Angle));
    }

    public Vector2 GetPositionAtAngle(float Angle)
    {
        return MathUtils.PolarToCartesian(Angle, GetRadiusFromFoci(Angle)).Rotate(ArgumentOfPeriapsis);
    }

    public Vector2 GetVelocityAtAngle(float angle)
    {
        float v = GetVelocityMagnitudeAtAngle(angle);
        float denom = MathF.Sqrt(1 + Eccentricity * Eccentricity + 2 * Eccentricity * MathF.Cos(angle));

        float vx_orb = v * (-MathF.Sin(angle)) / denom;
        float vy_orb = v * (Eccentricity + MathF.Cos(angle)) / denom;

        return new Vector2(vx_orb, vy_orb).Rotate(ArgumentOfPeriapsis);
    }


    public float GetVelocityMagnitudeAtAngle(float angle)
    {
        float r = GetRadiusFromFoci(angle);
        return MathF.Sqrt(PhysicalConstants.G * PhysicalConstants.MassOfPlanet * ((2 / r) - (1 / SemiMajorAxis)));
    }

    public float TimeToTrueAomaly(float targetTrueAnomaly)
    {
        float mu = PhysicalConstants.G * PhysicalConstants.MassOfPlanet;

        // Mean motion (rad/s)
        float a = (Apoapsis + Periapsis + 2 * PhysicalConstants.RadiusOfPlanet) / 2;
        float n = MathF.Sqrt(mu / MathF.Pow(a, 3));

        // Eccentric anomaly (E) from true anomaly
        float e = (Apoapsis - Periapsis) / (Apoapsis + Periapsis + 2 * PhysicalConstants.RadiusOfPlanet);
        float E_current = 2 * MathF.Atan(MathF.Sqrt((1 - e) / (1 + e)) * MathF.Tan(TrueAnomaly / 2));
        float E_target = 2 * MathF.Atan(MathF.Sqrt((1 - e) / (1 + e)) * MathF.Tan(targetTrueAnomaly / 2));

        float M_current = E_current - e * MathF.Sin(E_current);
        float M_target = E_target - e * MathF.Sin(E_target);

        float deltaM = M_target - M_current;
        if (deltaM < 0) deltaM += 2 * MathF.PI;

        float deltaT = deltaM / n; // time = mean anomaly / mean motion

        return deltaT;
    }
}
