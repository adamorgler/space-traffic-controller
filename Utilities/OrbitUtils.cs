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
        var orbitRadius = orbit.GetRadiusFromFoci(orbitalAngle) / GameConstants.Scale;
        var distance = Math.Abs(mouseRadius - orbitRadius);
        if (distance < threshold)
        {
            Vector2 screenPos = MathUtils.PolarToCartesian(mouseTheta, orbitRadius);
            screenPos.Rotate(orbit.ArgumentOfPeriapsis);
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
        double mu = PhysicalConstants.G * PhysicalConstants.MassOfPlanet; // μ = GM

        var r = pos.Length();
        var v = velocity.Length();

        // Specific angular momentum (scalar in 2D)
        double h = pos.X * velocity.Y - pos.Y * velocity.X;

        // Eccentricity vector
        Vector2 eVec = ((float)(v * v - mu / r) * pos - Vector2.Dot(pos, velocity) * velocity) / (float)mu;
        double e = eVec.Length();

        // Semi-major axis from vis-viva equation
        double a = 1 / ((2 / r) - (v * v / mu));

        // True anomaly (angle between position and eccentricity vector)
        double cosTheta = Vector2.Dot(eVec, pos) / (e * r);
        cosTheta = Math.Clamp(cosTheta, -1.0, 1.0); // avoid NaNs
        double trueAnomaly = Math.Acos(cosTheta);
        if (Vector2.Dot(pos, velocity) < 0)
            trueAnomaly = 2 * Math.PI - trueAnomaly;

        // Argument of periapsis (angle between x-axis and eccentricity vector)
        double argumentOfPeriapsis = Math.Atan2(eVec.Y, eVec.X);

        // Periapsis and apoapsis
        double periapsis = a * (1 - e) - PhysicalConstants.RadiusOfPlanet;
        double apoapsis = a * (1 + e) - PhysicalConstants.RadiusOfPlanet;

        return new Orbit(apoapsis, periapsis, argumentOfPeriapsis, trueAnomaly);
    }
}


public class OrbitPosition
{
    public double TrueAnomaly { get; set; }
    public Vector2 ScreenPosition { get; set; }

}
