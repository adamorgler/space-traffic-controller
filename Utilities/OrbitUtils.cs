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
            Vector2 worldPos = MathUtils.PolarToCartesian(mouseTheta, orbitRadius);
            worldPos.Rotate(orbit.ArgumentOfPeriapsis);
            return new OrbitPosition
            {
                TrueAnomaly = orbitalAngle,
                WorldPosition = worldPos,
            };
        }

        return null;
    }
}


public class OrbitPosition
{
    public double TrueAnomaly { get; set; }
    public Vector2 WorldPosition { get; set; }

}
