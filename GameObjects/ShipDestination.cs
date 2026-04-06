using System;
using SpaceTrafficController.Utilities;

namespace SpaceTrafficController.GameObjects;

public abstract class ShipDestination
{
    public abstract bool HasArrived(Ship ship);
}

public sealed class ExitControlAreaDestination : ShipDestination
{
    public override bool HasArrived(Ship ship)
    {
        if (ship is null)
        {
            return false;
        }

        if (!ship.Orbit.IsEscapeTrajectory)
        {
            return false;
        }

        var controlRadius = Core.GameState.CentralBody.ControlRadius;
        return ship.PositionD.Length() >= controlRadius;
    }
}

public sealed class StationDestination : ShipDestination
{
    public StationDestination(Station station)
    {
        Station = station;
    }

    public Station Station { get; }

    public override bool HasArrived(Ship ship)
    {
        if (Station is null || ship.Orbit.IsEscapeTrajectory)
        {
            return false;
        }

        if (!ControlLaneUtils.IsInsideStationControlArea(Station, ship.PositionD))
        {
            return false;
        }

        if (!ControlLaneUtils.TryGetRelativeStationLaneIndex(Station, ship.PositionD, out var relativeLaneIndex, out var stationRadius, out _))
        {
            return false;
        }

        // Station lane and departure-side buffers are not valid arrival lanes.
        if (relativeLaneIndex == 0)
        {
            return false;
        }

        var stationPosition = Station.Orbit.PositionVectorD;
        var stationAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var stationVelocity = Station.Orbit.VelocityVectorD;
        double motionSign = Math.Sign(Cross(stationPosition, stationVelocity));
        if (motionSign == 0d)
        {
            motionSign = 1d;
        }

        var signedOffset = GetSignedOffset(ship.PositionD, stationAngle, motionSign);
        var radialVelocity = GetRadialVelocity(ship);

        if (relativeLaneIndex > 0)
        {
            var arrivalAngle = ControlLaneUtils.GetStationApproachExtentMeters(Station, relativeLaneIndex) / stationRadius;
            return signedOffset >= 0d && signedOffset <= arrivalAngle && radialVelocity <= 0d;
        }

        var rearArrivalAngle = ControlLaneUtils.GetStationApproachExtentMeters(Station, relativeLaneIndex) / stationRadius;
        return signedOffset <= 0d && signedOffset >= -rearArrivalAngle && radialVelocity >= 0d;
    }

    private static double GetRadialVelocity(Ship ship)
    {
        var position = ship.PositionD;
        var radius = position.Length();
        if (radius <= 0d)
        {
            return 0d;
        }

        var velocity = ship.Orbit.VelocityVectorD;
        return ((position.X * velocity.X) + (position.Y * velocity.Y)) / radius;
    }

    private static double GetSignedOffset(DVector2 shipPosition, double stationAngle, double motionSign)
    {
        var shipAngle = Math.Atan2(shipPosition.Y, shipPosition.X);
        return NormalizeSignedAngle(shipAngle - stationAngle) * motionSign;
    }

    private static double NormalizeSignedAngle(double angle)
    {
        angle %= 2d * Math.PI;
        if (angle > Math.PI)
        {
            angle -= 2d * Math.PI;
        }
        else if (angle < -Math.PI)
        {
            angle += 2d * Math.PI;
        }

        return angle;
    }

    private static double Cross(DVector2 a, DVector2 b)
    {
        return (a.X * b.Y) - (a.Y * b.X);
    }
}