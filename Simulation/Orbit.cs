using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SpaceTrafficController.Core;
using SpaceTrafficController.Utilities;

namespace SpaceTrafficController.Simulation;

public class Orbit
{
    public Orbit(double apoapsis, double periapsis, double argumentOfPeriapsis, double trueAnomaly, double? eccentricity = null)
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
        ExplicitEccentricity = eccentricity;
    }

    public double Apoapsis { get; init; }
    public double Periapsis { get; init; }
    public double ArgumentOfPeriapsis { get; set; } // angle of ellipse in radians
    public double TrueAnomaly { get; set; } // position in orbit in radians
    // previous true anomaly recorded before the most recent update
    public double PreviousTrueAnomaly { get; set; }
    public double? ExplicitEccentricity { get; init; }
    public bool IsEscapeTrajectory => ExplicitEccentricity is > 1d || double.IsPositiveInfinity(Apoapsis);

    public double Apogee { get { return IsEscapeTrajectory ? double.PositiveInfinity : Apoapsis + GameState.CentralBody.Radius; } }
    public double Perigee { get { return Periapsis + GameState.CentralBody.Radius; } }
    public double SemiMajorAxis { get { return IsEscapeTrajectory ? double.PositiveInfinity : (Apogee + Perigee) / 2d; } }
    public double SemiMinorAxis { get { return IsEscapeTrajectory ? double.NaN : Math.Sqrt(Apogee * Perigee); } }
    public double SemiLatusRectum { get { return Perigee * (1d + Eccentricity); } }
    public double Eccentricity
    {
        get
        {
            if (ExplicitEccentricity.HasValue)
            {
                return ExplicitEccentricity.Value;
            }

            var ratio = Math.Pow(SemiMinorAxis, 2d) / Math.Pow(SemiMajorAxis, 2d);
            var inside = 1d - ratio;
            inside = Math.Max(0d, inside);
            return Math.Sqrt(inside);
        }
    }
    public double RadiusFromFoci { get { return GetRadiusFromFoci(TrueAnomaly); } }
    public DVector2 PositionVectorD { get { return GetPositionAtAngleD(TrueAnomaly); } }
    public Vector2 PositionVector { get { return PositionVectorD.ToVector2(); } }
    public DVector2 VelocityVectorD { get { return GetVelocityAtAngleD(TrueAnomaly); } }
    public Vector2 VelocityVector { get { return VelocityVectorD.ToVector2(); } }
    public double Velocity { get { return GetVelocityMagnitudeAtAngle(TrueAnomaly); } }

    public void Update(double timeStep)
    {
        TrueAnomaly += GetTrueAnomalyDelta(timeStep);

        if (IsEscapeTrajectory)
        {
            var maxTrueAnomaly = GetHyperbolicTrueAnomalyLimit() - 1e-6d;
            TrueAnomaly = Math.Clamp(TrueAnomaly, -maxTrueAnomaly, maxTrueAnomaly);
            return;
        }

        if (TrueAnomaly < 0d) TrueAnomaly += 2d * Math.PI;
        if (TrueAnomaly >= 2d * Math.PI) TrueAnomaly -= 2d * Math.PI;
    }

    public double GetTrueAnomalyDelta(double timeStep)
    {
        var h = Math.Sqrt(PhysicalConstants.G * GameState.CentralBody.Mass * SemiLatusRectum);
        return (h * timeStep) / Math.Pow(RadiusFromFoci, 2d);
    }

    public double GetRadiusFromFoci(double angle)
    {
        double numerator = SemiLatusRectum;
        double denominator = 1d + (Eccentricity * Math.Cos(angle));

        if (Math.Abs(denominator) < 1e-15d)
        {
            denominator = denominator < 0d ? -1e-15d : 1e-15d;
        }

        var radius = numerator / denominator;
        if (!double.IsFinite(radius) || radius <= 0d)
        {
            radius = Math.Max(1d, Perigee);
        }

        return radius;
    }

    public DVector2 GetPositionAtAngleD(double angle)
    {
        return MathUtils.PolarToCartesian(angle, GetRadiusFromFoci(angle)).Rotate(ArgumentOfPeriapsis);
    }

    public Vector2 GetPositionAtAngle(double angle)
    {
        return GetPositionAtAngleD(angle).ToVector2();
    }

    public DVector2 GetVelocityAtAngleD(double angle)
    {
        double v = GetVelocityMagnitudeAtAngle(angle);
        double denom = Math.Sqrt(1d + (Eccentricity * Eccentricity) + (2d * Eccentricity * Math.Cos(angle)));

        double vx_orb = v * (-Math.Sin(angle)) / denom;
        double vy_orb = v * (Eccentricity + Math.Cos(angle)) / denom;

        return new DVector2(vx_orb, vy_orb).Rotate(ArgumentOfPeriapsis);
    }

    public Vector2 GetVelocityAtAngle(double angle)
    {
        return GetVelocityAtAngleD(angle).ToVector2();
    }

    public double GetVelocityMagnitudeAtAngle(double angle)
    {
        double speedFactor = Math.Sqrt(PhysicalConstants.G * GameState.CentralBody.Mass / SemiLatusRectum);
        double inside = 1d + (2d * Eccentricity * Math.Cos(angle)) + (Eccentricity * Eccentricity);
        inside = Math.Max(0d, inside);

        double v2 = (speedFactor * speedFactor) * inside;
        if (!double.IsFinite(v2) || v2 < 0d)
        {
            return 0d;
        }

        return Math.Sqrt(v2);
    }

    public double TimeToTrueAomaly(double targetTrueAnomaly)
    {
        if (IsEscapeTrajectory)
        {
            return double.PositiveInfinity;
        }

        double mu = PhysicalConstants.G * GameState.CentralBody.Mass;

        // Mean motion (rad/s)
        double a = (Apoapsis + Periapsis + (2d * GameState.CentralBody.Radius)) / 2d;
        double n = Math.Sqrt(mu / Math.Pow(a, 3d));

        // Eccentric anomaly (E) from true anomaly
        double e = (Apoapsis - Periapsis) / (Apoapsis + Periapsis + (2d * GameState.CentralBody.Radius));
        double E_current = 2d * Math.Atan(Math.Sqrt((1d - e) / (1d + e)) * Math.Tan(TrueAnomaly / 2d));
        double E_target = 2d * Math.Atan(Math.Sqrt((1d - e) / (1d + e)) * Math.Tan(targetTrueAnomaly / 2d));

        double M_current = E_current - (e * Math.Sin(E_current));
        double M_target = E_target - (e * Math.Sin(E_target));

        double deltaM = M_target - M_current;
        if (deltaM < 0d) deltaM += 2d * Math.PI;

        double deltaT = deltaM / n; // time = mean anomaly / mean motion

        return deltaT;
    }

    public double GetHyperbolicTrueAnomalyLimit()
    {
        if (!IsEscapeTrajectory)
        {
            return Math.PI;
        }

        return Math.Acos(-1d / Eccentricity);
    }
}
