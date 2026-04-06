using SpaceTrafficController.Core;
using SpaceTrafficController.Simulation;
using System;
using System.Numerics;

namespace SpaceTrafficController.Utilities;

public static class OrbitUtils
{
    public static OrbitPosition? GetOrbitIntersectionNearMouse(Orbit orbit, Vector2 mousePos, float threshold = 20f)
    {
        var mouseTheta = Math.Atan2(mousePos.Y, mousePos.X);
        var mouseRadius = mousePos.Length();

        var orbitalAngle = mouseTheta - orbit.ArgumentOfPeriapsis;
        var orbitRadius = orbit.GetRadiusFromFoci(orbitalAngle) / GameConstants.RenderingScale;
        var distance = Math.Abs(mouseRadius - orbitRadius);
        if (distance < threshold)
        {
            Vector2 screenPos = MathUtils.PolarToCartesian(mouseTheta, orbitRadius).ToVector2();
            return new OrbitPosition
            {
                TrueAnomaly = orbitalAngle,
                ScreenPosition = screenPos,
            };
        }

        return null;
    }

    public static Orbit GetOrbitFromStateVectors(Vector2 pos, Vector2 velocity)
    {
        return GetOrbitFromStateVectors(DVector2.FromVector2(pos), DVector2.FromVector2(velocity));
    }

    public static Orbit GetOrbitFromStateVectors(DVector2 pos, DVector2 velocity)
    {
        double mu = PhysicalConstants.G * GameState.CentralBody.Mass; // μ = GM

        var r = pos.Length();
        var v = velocity.Length();

        if (!double.IsFinite(r) || !double.IsFinite(v) || r <= 0d || mu <= 0d)
        {
            var safeRadius = Math.Max(1d, GameState.CentralBody.Radius + 1d);
            var altitude = safeRadius - GameState.CentralBody.Radius;
            return new Orbit(altitude, altitude, 0d, 0d);
        }

        double h = (pos.X * velocity.Y) - (pos.Y * velocity.X);

        // Eccentricity vector
        DVector2 eVec = ((((v * v) - (mu / r)) * pos) - (DVector2.Dot(pos, velocity) * velocity)) / mu;
        double e = Math.Max(0d, eVec.Length());

        // Argument of periapsis (angle between x-axis and eccentricity vector)
        double argumentOfPeriapsis = e <= 1e-9 ? 0d : Math.Atan2(eVec.Y, eVec.X);

        // True anomaly
        // Use atan2(sinν, cosν) for robust quadrant handling. The old acos-only approach
        // could misidentify ν when radial velocity was ~0 (common near circular burns),
        // causing apparent position jumps at maneuver execution.
        double trueAnomaly;
        if (e <= 1e-9)
        {
            // Circular case: periapsis direction is undefined, so preserve actual inertial angle.
            trueAnomaly = NormalizeAngle0ToTwoPi(Math.Atan2(pos.Y, pos.X));
        }
        else
        {
            var cosTheta = (DVector2.Dot(eVec, pos) / (e * r)).Clamp(-1d, 1d);
            var sinTheta = ((eVec.X * pos.Y) - (eVec.Y * pos.X)) / (e * r);
            trueAnomaly = NormalizeAngle0ToTwoPi(Math.Atan2(sinTheta, cosTheta));
        }

        // Numerically stable ellipse parameters from semi-latus rectum p.
        // This avoids instability when energy is near parabolic and a gets very large.
        double p = (h * h) / mu;
        if (!double.IsFinite(p) || p <= 0d)
        {
            p = r;
        }

        double periRadius = p / (1d + e);
        double apoRadius = e < 1d ? p / (1d - e) : double.PositiveInfinity;

        double periapsis = periRadius - GameState.CentralBody.Radius;
        double apoapsis = apoRadius - GameState.CentralBody.Radius;

        var invalidPeriapsis = double.IsNaN(periapsis) || periapsis < -GameState.CentralBody.Radius;
        var invalidApoapsis = double.IsNaN(apoapsis) || apoapsis < periapsis;

        if (invalidPeriapsis || invalidApoapsis)
        {
            double altitude = Math.Max(1d, r - GameState.CentralBody.Radius);
            return new Orbit(altitude, altitude, argumentOfPeriapsis, trueAnomaly, e);
        }

        return new Orbit(apoapsis, periapsis, argumentOfPeriapsis, trueAnomaly, e);
    }

    private static double NormalizeAngle0ToTwoPi(double angle)
    {
        var wrapped = angle % (2d * Math.PI);
        return wrapped < 0d ? wrapped + (2d * Math.PI) : wrapped;
    }
}


public class OrbitPosition
{
    public double TrueAnomaly { get; set; }
    public Vector2 ScreenPosition { get; set; }

}
