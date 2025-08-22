using SpaceTrafficController.Core;
using SpaceTrafficController.Simulation;
using System;
using System.Numerics;

namespace SpaceTrafficController.Utilities;

public static class OrbitUtils
{
    public static OrbitPosition? GetOrbitIntersectionNearMouse(Orbit orbit, Vector2 mousePos, float threshold = 20f)
    {
        var mouseTheta = MathF.Atan2(mousePos.Y, mousePos.X);
        var mouseRadius = mousePos.Length();

        var orbitalAngle = mouseTheta - orbit.ArgumentOfPeriapsis;
        var orbitRadius = orbit.GetRadiusFromFoci(orbitalAngle) / GameConstants.Scale;
        var distance = MathF.Abs(mouseRadius - orbitRadius);
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
        float mu = PhysicalConstants.G * PhysicalConstants.MassOfPlanet; // μ = GM

        var r = pos.Length();
        var v = velocity.Length();

        // Specific angular momentum (scalar in 2D)
        float h = pos.X * velocity.Y - pos.Y * velocity.X;

        // Eccentricity vector
        Vector2 eVec = ((v * v - mu / r) * pos - Vector2.Dot(pos, velocity) * velocity) / mu;
        float e = eVec.Length();

        // Semi-major axis from vis-viva equation
        float a = 1 / ((2 / r) - (v * v / mu));

        // True anomaly (angle between position and eccentricity vector)
        float cosTheta = Vector2.Dot(eVec, pos) / (e * r);
        cosTheta = cosTheta.Clamp(-1.0f, 1.0f); // avoid NaNs
        float trueAnomaly = MathF.Acos(cosTheta);
        if (Vector2.Dot(pos, velocity) < 0)
            trueAnomaly = 2 * MathF.PI - trueAnomaly;

        // Argument of periapsis (angle between x-axis and eccentricity vector)
        float argumentOfPeriapsis = MathF.Atan2(eVec.Y, eVec.X);

        // Periapsis and apoapsis
        float periapsis = a * (1 - e) - PhysicalConstants.RadiusOfPlanet;
        float apoapsis = a * (1 + e) - PhysicalConstants.RadiusOfPlanet;

        return new Orbit(apoapsis, periapsis, argumentOfPeriapsis, trueAnomaly);
    }
}


public class OrbitPosition
{
    public float TrueAnomaly { get; set; }
    public Vector2 ScreenPosition { get; set; }

}
