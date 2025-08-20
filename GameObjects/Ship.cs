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

    public override void UpdateExtension(double gameTime)
    {
        CheckIfManueverNodeIsCrossed(gameTime);
    }
    private void CheckIfManueverNodeIsCrossed(double gameTime)
    {
        if (ManeuverNode is null || !ManeuverNode.IsConfirmed || ManeuverNode.PredictedOrbit is null)
            return;
        if (gameTime >= ManeuverNode.NodeTime)
        {
            Orbit = ManeuverNode.PredictedOrbit;
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