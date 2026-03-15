using System;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using SpaceTrafficController.Core;
using SpaceTrafficController.Utilities;
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
    public DVector2 PositionD { get { return Orbit.PositionVectorD; } }
    public Vector2 Position { get { return Orbit.PositionVector; } }
    public ManeuverNode ManeuverNode { get; set; }
    public ShipDestination Destination { get; set; }

    public override void UpdateExtension(double gameTime)
    {
        CheckIfManueverNodeIsCrossed(gameTime);
    }
    private void CheckIfManueverNodeIsCrossed(double gameTime)
    {
        if (ManeuverNode is null || !ManeuverNode.IsConfirmed)
            return;
        var predictedOrbit = ManeuverNode.GetPredictedOrbit(Orbit);
        if (predictedOrbit is null)
            return;
        var timeToNode = ManeuverNode.GetTimeToNode(Orbit);
        if (double.IsFinite(timeToNode) && gameTime >= timeToNode)
        {
            Orbit = predictedOrbit;
            ManeuverNode = null;
        }
    }

    public bool ShouldDespawn()
    {
        if (Destination is not null && Destination.HasArrived(this))
        {
            return true;
        }

        if (!Orbit.IsEscapeTrajectory)
        {
            return false;
        }

        // Despawn when escape trajectory passes beyond the control altitude
        var controlRadius = GameState.CentralBody.ControlRadius;
        return PositionD.Length() >= controlRadius;
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
    public bool IsControllable { get; set; } = true;
}