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
    public ShipTrafficLane TrafficLane { get; set; } = ShipTrafficLane.None;

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

        // `Orbit.Update(gameTime)` already ran this frame.
        // Reconstruct the pre-update state and check whether the node was crossed
        // during this frame so the burn can be applied at the correct in-frame time.
        var currentTrueAnomaly = Orbit.TrueAnomaly;
        Orbit.TrueAnomaly = Orbit.PreviousTrueAnomaly;
        var timeToNodeFromFrameStart = ManeuverNode.GetTimeToNode(Orbit);
        Orbit.TrueAnomaly = currentTrueAnomaly;

        if (double.IsFinite(timeToNodeFromFrameStart) && timeToNodeFromFrameStart <= gameTime)
        {
            var remainingTimeAfterBurn = Math.Max(0d, gameTime - timeToNodeFromFrameStart);
            Orbit = predictedOrbit;
            if (remainingTimeAfterBurn > 0d)
            {
                Orbit.Update(remainingTimeAfterBurn);
            }

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

public enum ShipTrafficLane
{
    None,
    StationDepartureUpper,
    StationDepartureLower,
}

public class ShipStatus
{
    public bool IsSelected { get; set; } = false;
    public bool IsEncroached { get; set; } = false;
    public bool WasEncroachedLastFrame { get; set; } = false;
    public bool IsControllable { get; set; } = true;
}