using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using System.Numerics;

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
    public ManeuverNode ManeuverNode { get; set; }

    public override void UpdateExtension(float gameTime)
    {
        CheckIfManueverNodeIsCrossed(gameTime);
    }
    private void CheckIfManueverNodeIsCrossed(float gameTime)
    {
        if (ManeuverNode is null || !ManeuverNode.IsConfirmed)
            return;
        var predictedOrbit = ManeuverNode.GetPredictedOrbit(Orbit);
        if (predictedOrbit is null)
            return;
        if (gameTime >= ManeuverNode.GetTimeToNode(Orbit))
        {
            Orbit = predictedOrbit;
            ManeuverNode = null;
        }
    }
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