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
    public ManeuverNode NextManeuverNode { get; set; }
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

            ManeuverNode = NextManeuverNode;
            NextManeuverNode = null;
        }
    }

    public bool ShouldDespawn()
    {
        if (!Orbit.IsEscapeTrajectory)
        {
            return false;
        }

        // Despawn when escape trajectory passes beyond the control altitude
        var controlRadius = GameState.CentralBody.ControlRadius;
        return PositionD.Length() >= controlRadius;
    }

    public Orbit GetQuickHohmannTransferOrbit(double targetAltitudeMeters, bool startImmediate = false)
    {
        var baseOrbit = Orbit;
        if (ManeuverNode is not null && ManeuverNode.IsConfirmed)
        {
            var predicted = ManeuverNode.GetPredictedOrbit(Orbit);
            if (predicted is not null)
                baseOrbit = predicted;
        }

        if (!TryBuildQuickHohmannNodes(targetAltitudeMeters, baseOrbit, out var firstBurnNode, out _, startImmediate))
        {
            return null;
        }

        return firstBurnNode.GetPredictedOrbit(baseOrbit);
    }

    public bool ApplyQuickHohmannTransferToAltitude(double targetAltitudeMeters, bool startImmediate = false)
    {
        Orbit baseOrbit;
        var writeAsSecondNode = false;

        // Only append/replace as a second node when the first node is already confirmed.
        // If nodes are unconfirmed (e.g. transfer dialog preview), rebuild both nodes from current orbit.
        if (NextManeuverNode is not null && ManeuverNode is not null && ManeuverNode.IsConfirmed)
        {
            var predictedFirstForReplace = ManeuverNode.GetPredictedOrbit(Orbit);
            if (predictedFirstForReplace is null)
            {
                return false;
            }

            baseOrbit = predictedFirstForReplace;
            writeAsSecondNode = true;
        }
        else if (ManeuverNode is not null && ManeuverNode.IsConfirmed)
        {
            var predictedFirst = ManeuverNode.GetPredictedOrbit(Orbit);
            if (predictedFirst is null)
            {
                return false;
            }

            baseOrbit = predictedFirst;
            writeAsSecondNode = true;
        }
        else
        {
            baseOrbit = Orbit;
        }

        if (!TryBuildQuickHohmannNodes(targetAltitudeMeters, baseOrbit, out var firstBurnNode, out var secondBurnNode, startImmediate))
        {
            return false;
        }

        if (writeAsSecondNode)
        {
            NextManeuverNode = firstBurnNode;
        }
        else
        {
            ManeuverNode = firstBurnNode;
            NextManeuverNode = secondBurnNode;
        }

        return true;
    }

    private static bool TryBuildQuickHohmannNodes(
        double targetAltitudeMeters,
        Orbit baseOrbit,
        out ManeuverNode firstBurnNode,
        out ManeuverNode secondBurnNode,
        bool startImmediate)
    {
        firstBurnNode = null;
        secondBurnNode = null;

        if (baseOrbit.IsEscapeTrajectory)
        {
            return false;
        }

        var targetRadius = GameState.CentralBody.Radius + targetAltitudeMeters;
        if (!double.IsFinite(targetRadius) || targetRadius <= 0d)
        {
            return false;
        }

        double burnTrueAnomaly;
        double burnRadius;
        if (startImmediate)
        {
            const double leadAngleRadians = 5d * Math.PI / 180d;
            burnTrueAnomaly = NormalizeAngle(baseOrbit.TrueAnomaly + leadAngleRadians);
            burnRadius = baseOrbit.GetRadiusFromFoci(burnTrueAnomaly);
        }
        else
        {
            var burnAtPeriapsis = IsPeriapsisSooner(baseOrbit);
            burnTrueAnomaly = burnAtPeriapsis ? 0d : Math.PI;
            burnRadius = burnAtPeriapsis ? baseOrbit.Perigee : baseOrbit.Apogee;
        }

        if (!double.IsFinite(burnRadius) || burnRadius <= 0d)
        {
            return false;
        }

        var mu = PhysicalConstants.G * GameState.CentralBody.Mass;
        var transferSemiMajorAxis = (burnRadius + targetRadius) / 2d;
        if (transferSemiMajorAxis <= 0d || !double.IsFinite(transferSemiMajorAxis))
        {
            return false;
        }

        var transferSpeedSquared = mu * ((2d / burnRadius) - (1d / transferSemiMajorAxis));
        if (transferSpeedSquared <= 0d || !double.IsFinite(transferSpeedSquared))
        {
            return false;
        }

        var transferSpeed = Math.Sqrt(transferSpeedSquared);

        var burnPosition = baseOrbit.GetPositionAtAngleD(burnTrueAnomaly);
        var burnVelocity = baseOrbit.GetVelocityAtAngleD(burnTrueAnomaly);
        var progradeDir = DVector2.Normalize(burnVelocity);
        if (progradeDir.Length() <= 0d)
        {
            return false;
        }

        var firstBurnProgradeDeltaV = transferSpeed - baseOrbit.GetVelocityMagnitudeAtAngle(burnTrueAnomaly);
        var firstBurnNormalDeltaV = 0d;

        if (startImmediate)
        {
            var radialDir = DVector2.Normalize(burnPosition);
            if (radialDir.Length() <= 0d)
            {
                return false;
            }

            var motionSign = Math.Sign((burnPosition.X * burnVelocity.Y) - (burnPosition.Y * burnVelocity.X));
            if (motionSign == 0d)
            {
                motionSign = 1;
            }

            var tangentialDir = new DVector2(-radialDir.Y * motionSign, radialDir.X * motionSign);
            var desiredVelocity = tangentialDir * transferSpeed;
            var deltaVVector = desiredVelocity - burnVelocity;
            var normalDir = new DVector2(-progradeDir.Y, progradeDir.X);

            firstBurnProgradeDeltaV = DVector2.Dot(deltaVVector, progradeDir);
            firstBurnNormalDeltaV = DVector2.Dot(deltaVVector, normalDir);
        }

        firstBurnNode = new ManeuverNode()
        {
            TrueAnomaly = burnTrueAnomaly,
            ScreenPosition = (baseOrbit.GetPositionAtAngleD(burnTrueAnomaly) / GameConstants.RenderingScale).ToVector2(),
            ProgradeDeltaV = firstBurnProgradeDeltaV,
            NormalDeltaV = firstBurnNormalDeltaV,
        };

        var transferOrbit = firstBurnNode.GetPredictedOrbit(baseOrbit);
        if (transferOrbit is null)
        {
            return false;
        }

        // Determine whether the first-burn point is periapsis or apoapsis on the transfer orbit.
        // If transfer speed at burn radius is above circular speed, burn point is periapsis; otherwise apoapsis.
        var circularSpeedAtBurnRadius = Math.Sqrt(mu / burnRadius);
        var firstBurnPointIsPeriapsis = transferSpeed >= circularSpeedAtBurnRadius;
        var secondBurnTrueAnomaly = firstBurnPointIsPeriapsis ? Math.PI : 0d;
        var secondBurnSpeed = transferOrbit.GetVelocityMagnitudeAtAngle(secondBurnTrueAnomaly);
        if (!double.IsFinite(secondBurnSpeed))
        {
            return false;
        }

        secondBurnNode = new ManeuverNode()
        {
            TrueAnomaly = secondBurnTrueAnomaly,
            ScreenPosition = (transferOrbit.GetPositionAtAngleD(secondBurnTrueAnomaly) / GameConstants.RenderingScale).ToVector2(),
            ProgradeDeltaV = Math.Sqrt(mu / targetRadius) - secondBurnSpeed,
        };

        return true;
    }

    private static bool IsPeriapsisSooner(Orbit orbit)
    {
        var timeToPeriapsis = orbit.TimeToTrueAomaly(0d);
        var timeToApoapsis = orbit.TimeToTrueAomaly(Math.PI);

        if (!double.IsFinite(timeToPeriapsis)) return false;
        if (!double.IsFinite(timeToApoapsis)) return true;

        return timeToPeriapsis <= timeToApoapsis;
    }

    private static double NormalizeAngle(double angle)
    {
        var twoPi = 2d * Math.PI;
        angle %= twoPi;
        if (angle < 0d)
        {
            angle += twoPi;
        }

        return angle;
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
    public bool IsInStationControlArea { get; set; } = false;
    public bool IsLockedUntilOutsideStationControlArea { get; set; } = false;
    public bool IsStationArrivalHoldingForDespawn { get; set; } = false;
    public bool HasEnteredStationControlAreaOnce { get; set; } = false;
    public double StationArrivalDespawnTimerSeconds { get; set; } = 0d;
    public double ActivationFlashTimeRemainingSeconds { get; set; } = 0d;
}