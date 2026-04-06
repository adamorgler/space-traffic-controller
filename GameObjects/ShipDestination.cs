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

        var stationPosition = Station.Orbit.PositionVectorD;
        var stationRadius = stationPosition.Length();
        if (stationRadius <= 0d)
        {
            return false;
        }

        var halfAltitude = Station.ControlAreaHalfAltitudeMeters;
        var arrivalExtent = Station.ControlAreaArrivalExtentMeters;
        var departureExtent = Station.ControlAreaDepartureExtentMeters;
        var innerRadius = Math.Max(1d, stationRadius - halfAltitude);
        var outerRadius = stationRadius + halfAltitude;
        var arrivalAngle = arrivalExtent / stationRadius;
        var departureAngle = departureExtent / stationRadius;

        var stationAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var stationVelocity = Station.Orbit.VelocityVectorD;
        double motionSign = Math.Sign(Cross(stationPosition, stationVelocity));
        if (motionSign == 0d)
        {
            motionSign = 1d;
        }

        var previousRadius = ship.PreviousPositionD.Length();
        var currentRadius = ship.PositionD.Length();

        var previousSignedOffset = GetSignedOffset(ship.PreviousPositionD, stationAngle, motionSign);
        var currentSignedOffset = GetSignedOffset(ship.PositionD, stationAngle, motionSign);

        var isCurrentlyInsideControlArea = IsInsideControlArea(
            currentRadius,
            currentSignedOffset,
            stationRadius,
            innerRadius,
            outerRadius,
            arrivalAngle,
            departureAngle);

        if (!isCurrentlyInsideControlArea)
        {
            return false;
        }

        if (IsFrontArrivalSide(currentRadius, stationRadius, currentSignedOffset)
            && IsOrbitCompatibleWithFrontArrival(ship, Station, currentRadius))
        {
            return true;
        }

        if (IsRearArrivalSide(currentRadius, stationRadius, currentSignedOffset)
            && IsOrbitCompatibleWithRearArrival(ship, Station, currentRadius))
        {
            return true;
        }

        var wasWithinRadiusBand = IsWithinRadiusBand(previousRadius, innerRadius, outerRadius);

        var enteredFromFront = wasWithinRadiusBand
            && previousSignedOffset > arrivalAngle
            && currentSignedOffset <= arrivalAngle;
        if (enteredFromFront)
        {
            return IsFrontArrivalSide(currentRadius, stationRadius, currentSignedOffset)
                && IsOrbitCompatibleWithFrontArrival(ship, Station, currentRadius);
        }

        var enteredFromRear = wasWithinRadiusBand
            && previousSignedOffset < -arrivalAngle
            && currentSignedOffset >= -arrivalAngle;
        if (enteredFromRear)
        {
            return IsRearArrivalSide(currentRadius, stationRadius, currentSignedOffset)
                && IsOrbitCompatibleWithRearArrival(ship, Station, currentRadius);
        }

        return false;
    }

    private static bool IsFrontArrivalSide(double currentRadius, double stationRadius, double currentSignedOffset)
    {
        return currentRadius > stationRadius && currentSignedOffset >= 0d;
    }

    private static bool IsRearArrivalSide(double currentRadius, double stationRadius, double currentSignedOffset)
    {
        return currentRadius < stationRadius && currentSignedOffset <= 0d;
    }

    private static bool IsInsideControlArea(
        double currentRadius,
        double currentSignedOffset,
        double stationRadius,
        double innerRadius,
        double outerRadius,
        double arrivalAngle,
        double departureAngle)
    {
        if (!IsWithinRadiusBand(currentRadius, innerRadius, outerRadius))
        {
            return false;
        }

        if (currentRadius >= stationRadius)
        {
            return currentSignedOffset >= -departureAngle
                && currentSignedOffset <= arrivalAngle;
        }

        return currentSignedOffset >= -arrivalAngle
            && currentSignedOffset <= departureAngle;
    }

    private static bool IsOrbitCompatibleWithFrontArrival(Ship ship, Station station, double currentRadius)
    {
        var stationOrbitRadius = Core.GameState.CentralBody.Radius + station.Orbit.Periapsis;
        var upperBound = stationOrbitRadius + station.ControlAreaHalfAltitudeMeters;
        var radialVelocity = GetRadialVelocity(ship);

        return currentRadius >= stationOrbitRadius
            && currentRadius <= upperBound
            && radialVelocity <= 0d;
    }

    private static bool IsOrbitCompatibleWithRearArrival(Ship ship, Station station, double currentRadius)
    {
        var stationOrbitRadius = Core.GameState.CentralBody.Radius + station.Orbit.Periapsis;
        var lowerBound = stationOrbitRadius - station.ControlAreaHalfAltitudeMeters;
        var radialVelocity = GetRadialVelocity(ship);

        return currentRadius >= lowerBound
            && currentRadius <= stationOrbitRadius
            && radialVelocity >= 0d;
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

    private static bool IsWithinRadiusBand(double radius, double innerRadius, double outerRadius)
    {
        return radius >= innerRadius && radius <= outerRadius;
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