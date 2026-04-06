using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using System;
using System.Collections.Generic;

namespace SpaceTrafficController.Utilities;

public static class ControlLaneUtils
{
    public static double GetAtmosphereTopAltitudeMeters(CelestialBody body)
    {
        if (body is null || body.AtmosphereLayers is null || body.AtmosphereLayers.Count == 0)
        {
            return 0d;
        }

        var atmosphereTop = 0d;
        foreach (var layer in body.AtmosphereLayers)
        {
            atmosphereTop = Math.Max(atmosphereTop, layer.Altitude + layer.Thickness);
        }

        return atmosphereTop;
    }

    public static bool TryGetControlLaneIndex(CelestialBody body, double altitudeMeters, out int laneIndex)
    {
        laneIndex = -1;
        if (body is null || !double.IsFinite(altitudeMeters))
        {
            return false;
        }

        var atmosphereTop = GetAtmosphereTopAltitudeMeters(body);
        if (altitudeMeters <= atmosphereTop || altitudeMeters >= body.ControlAltitudeMeters)
        {
            return false;
        }

        laneIndex = (int)Math.Floor((altitudeMeters - GameConstants.ControlLaneHalfWidthMeters) / GameConstants.ControlLaneWidthMeters);
        return laneIndex >= 0;
    }

    /// <summary>
    /// Returns all lane indices the ship occupies. A ship within
    /// <see cref="GameConstants.LaneBoundaryProximityMeters"/> of a shared lane boundary
    /// occupies both adjacent lanes.
    /// </summary>
    public static List<int> GetShipOccupiedLaneIndices(CelestialBody body, double altitudeMeters)
    {
        var indices = new List<int>();
        if (!TryGetControlLaneBounds(body, altitudeMeters, out var lower, out var upper, out var laneIndex))
            return indices;

        indices.Add(laneIndex);

        var distToLower = altitudeMeters - lower;
        var distToUpper = upper - altitudeMeters;

        if (distToLower <= GameConstants.LaneBoundaryProximityMeters && laneIndex > 0)
            indices.Add(laneIndex - 1);
        else if (distToUpper <= GameConstants.LaneBoundaryProximityMeters && upper < body.ControlAltitudeMeters)
            indices.Add(laneIndex + 1);

        return indices;
    }

    /// <summary>
    /// Like <see cref="TryGetControlLaneBounds"/> but expands the returned altitude range to span
    /// two lanes when the ship is within <see cref="GameConstants.LaneBoundaryProximityMeters"/>
    /// of a shared lane boundary.
    /// </summary>
    public static bool TryGetShipEffectiveLaneBounds(
        CelestialBody body, double altitudeMeters,
        out double lowerAltitudeMeters, out double upperAltitudeMeters,
        out bool spansAdjacentLane)
    {
        lowerAltitudeMeters = 0d;
        upperAltitudeMeters = 0d;
        spansAdjacentLane = false;

        if (!TryGetControlLaneBounds(body, altitudeMeters, out var ownLower, out var ownUpper, out var laneIndex))
            return false;

        var distToLower = altitudeMeters - ownLower;
        var distToUpper = ownUpper - altitudeMeters;

        if (distToLower <= GameConstants.LaneBoundaryProximityMeters && laneIndex > 0)
        {
            lowerAltitudeMeters = ownLower - GameConstants.ControlLaneWidthMeters;
            upperAltitudeMeters = ownUpper;
            spansAdjacentLane = true;
        }
        else if (distToUpper <= GameConstants.LaneBoundaryProximityMeters && ownUpper < body.ControlAltitudeMeters)
        {
            lowerAltitudeMeters = ownLower;
            upperAltitudeMeters = ownUpper + GameConstants.ControlLaneWidthMeters;
            spansAdjacentLane = true;
        }
        else
        {
            lowerAltitudeMeters = ownLower;
            upperAltitudeMeters = ownUpper;
        }

        return true;
    }

    public static bool TryGetControlLaneBounds(CelestialBody body, double altitudeMeters, out double lowerAltitudeMeters, out double upperAltitudeMeters, out int laneIndex)
    {
        lowerAltitudeMeters = 0d;
        upperAltitudeMeters = 0d;

        if (!TryGetControlLaneIndex(body, altitudeMeters, out laneIndex))
        {
            return false;
        }

        lowerAltitudeMeters = GameConstants.ControlLaneHalfWidthMeters + (laneIndex * GameConstants.ControlLaneWidthMeters);
        upperAltitudeMeters = lowerAltitudeMeters + GameConstants.ControlLaneWidthMeters;

        return upperAltitudeMeters > 0d && lowerAltitudeMeters < body.ControlAltitudeMeters;
    }

    public static int GetStationApproachLaneCount(Station station)
    {
        if (station is null)
        {
            return Station.DefaultApproachLaneCount;
        }

        return Math.Max(1, station.ApproachLaneCount);
    }

    public static double GetStationControlHalfAltitudeMeters(Station station)
    {
        return GameConstants.ControlLaneHalfWidthMeters
            + (GetStationApproachLaneCount(station) * GameConstants.ControlLaneWidthMeters);
    }

    public static double GetStationApproachExtentMeters(Station station, int relativeLaneIndex)
    {
        if (station is null)
        {
            return 0d;
        }

        var laneDepth = Math.Max(1, Math.Abs(relativeLaneIndex));
        return station.ControlAreaArrivalExtentMeters * laneDepth;
    }

    public static bool TryGetRelativeStationLaneIndex(Station station, DVector2 shipPosition, out int relativeLaneIndex, out double stationRadius, out double shipRadius)
    {
        relativeLaneIndex = 0;
        stationRadius = 0d;
        shipRadius = 0d;

        if (station?.Orbit is null)
        {
            return false;
        }

        var stationPosition = station.Orbit.PositionVectorD;
        stationRadius = stationPosition.Length();
        shipRadius = shipPosition.Length();
        if (stationRadius <= 0d || shipRadius <= 0d)
        {
            return false;
        }

        var deltaAltitude = shipRadius - stationRadius;
        relativeLaneIndex = (int)Math.Floor((deltaAltitude + GameConstants.ControlLaneHalfWidthMeters) / GameConstants.ControlLaneWidthMeters);
        return Math.Abs(relativeLaneIndex) <= GetStationApproachLaneCount(station);
    }

    public static bool TryGetStationLaneRadiusBounds(Station station, int relativeLaneIndex, out double lowerRadius, out double upperRadius)
    {
        lowerRadius = 0d;
        upperRadius = 0d;

        if (station?.Orbit is null)
        {
            return false;
        }

        var stationRadius = station.Orbit.PositionVectorD.Length();
        if (stationRadius <= 0d)
        {
            return false;
        }

        lowerRadius = stationRadius + (relativeLaneIndex * GameConstants.ControlLaneWidthMeters) - GameConstants.ControlLaneHalfWidthMeters;
        upperRadius = lowerRadius + GameConstants.ControlLaneWidthMeters;
        return upperRadius > 0d;
    }

    public static bool TryGetStationLaneSignedOffsetRange(Station station, int relativeLaneIndex, double stationRadius, out double minSignedOffset, out double maxSignedOffset)
    {
        minSignedOffset = 0d;
        maxSignedOffset = 0d;
        if (station is null || stationRadius <= 0d)
        {
            return false;
        }

        var departureAngle = station.ControlAreaDepartureExtentMeters / stationRadius;
        if (relativeLaneIndex == 0)
        {
            minSignedOffset = -departureAngle;
            maxSignedOffset = departureAngle;
            return true;
        }

        var arrivalAngle = GetStationApproachExtentMeters(station, relativeLaneIndex) / stationRadius;
        if (relativeLaneIndex > 0)
        {
            minSignedOffset = relativeLaneIndex == 1 ? -departureAngle : 0d;
            maxSignedOffset = arrivalAngle;
            return true;
        }

        minSignedOffset = -arrivalAngle;
        maxSignedOffset = relativeLaneIndex == -1 ? departureAngle : 0d;
        return true;
    }

    public static bool IsInsideStationControlArea(Station station, DVector2 shipPosition)
    {
        if (!TryGetRelativeStationLaneIndex(station, shipPosition, out var relativeLaneIndex, out var stationRadius, out _))
        {
            return false;
        }

        if (!TryGetStationLaneSignedOffsetRange(station, relativeLaneIndex, stationRadius, out var minSignedOffset, out var maxSignedOffset))
        {
            return false;
        }

        var stationPosition = station.Orbit.PositionVectorD;
        var stationAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var shipAngle = Math.Atan2(shipPosition.Y, shipPosition.X);
        var stationVelocity = station.Orbit.VelocityVectorD;
        double motionSign = Math.Sign((stationPosition.X * stationVelocity.Y) - (stationPosition.Y * stationVelocity.X));
        if (motionSign == 0d)
        {
            motionSign = 1d;
        }

        var signedOffset = ShortestSignedAngleDelta(stationAngle, shipAngle) * motionSign;
        return signedOffset >= minSignedOffset && signedOffset <= maxSignedOffset;
    }

    public static double ShortestSignedAngleDelta(double fromAngle, double toAngle)
    {
        var delta = toAngle - fromAngle;
        while (delta > Math.PI)
        {
            delta -= Math.PI * 2d;
        }

        while (delta < -Math.PI)
        {
            delta += Math.PI * 2d;
        }

        return delta;
    }
}
