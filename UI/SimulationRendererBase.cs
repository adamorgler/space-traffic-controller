using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Utilities;
using SpaceTrafficController.Simulation;
using System;
using System.Collections.Generic;

namespace SpaceTrafficController.UI;

public abstract class SimulationRendererBase
{
    protected readonly GraphicsDevice GraphicsDevice;
    protected readonly SpriteBatch SpriteBatch;
    protected readonly Camera2D Camera;

    protected MouseState MouseState;

    protected const int Scale = GameConstants.RenderingScale;

    // Shared colors for consistent visuals between renderers.
    protected static readonly Color OrbitDefaultColor = Color.LightGray * 0.6f;
    protected static readonly Color TargetOrbitColor = Color.Cyan * 0.55f;
    protected static readonly Color TargetApsisColor = Color.Cyan * 0.75f;
    protected static readonly Color SelectedOrbitColor = Color.White;

    protected static readonly Color UncontrolledShipColor = Color.LightGray;
    protected static readonly Color SelectedShipColor = Color.Gold;
    protected static readonly Color ActiveShipColor = Color.LimeGreen;
    protected static readonly Color EncroachedSeparationColor = Color.Red;
    protected static readonly Color SafeSeparationColor = Color.Green;

    protected static readonly Color SelectedStationColor = Color.Gold;
    protected static readonly Color StationColor = Color.AliceBlue;
    protected static readonly Color ControlAreaColor = Color.LightSkyBlue;
    protected static readonly Color ArrivalArrowColor = Color.LimeGreen;
    protected static readonly Color DepartureArrowColor = Color.Red;

    protected static readonly Color[] ClosestApproachColors = new[] { Color.Purple, Color.Orange };
    protected static readonly Color ClosestApproachDashColor = Color.White;

    protected const int ClosestApproachCoarseSamples = 120;
    protected const int ClosestApproachFineSamples = 40;
    protected const double ClosestApproachFineWindow = 0.15d;
    protected const double ClosestApproachExclusionWindow = 0.6d;
    protected const double ClosestApproachLineThresholdMeters = 5000d;

    protected SimulationRendererBase(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Camera2D camera)
    {
        GraphicsDevice = graphicsDevice;
        SpriteBatch = spriteBatch;
        Camera = camera;
    }

    protected static Orbit? GetDestinationOrbit(Ship ship)
    {
        if (ship.Destination is StationDestination stationDest)
        {
            return stationDest.Station.Orbit;
        }

        return null;
    }

    protected static string FormatDistance(double meters)
    {
        if (meters >= 1_000_000d)
            return $"{meters / 1000d:0}km";
        if (meters >= 10_000d)
            return $"{meters / 1000d:0.0}km";
        return $"{meters:0}m";
    }

    protected List<List<Vector2>> BuildStationControlPaths(
        DVector2 stationPosition,
        double arrivalExtent,
        double departureExtent,
        double halfAltitude)
    {
        var orbitRadius = stationPosition.Length();
        if (orbitRadius <= 0d)
        {
            return new List<List<Vector2>>();
        }

        var centerAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var arrivalAngle = Math.Min(arrivalExtent / orbitRadius, Math.PI - 1e-4d);
        var departureAngle = Math.Min(departureExtent / orbitRadius, Math.PI - 1e-4d);
        var outerRadius = orbitRadius + halfAltitude;
        var innerRadius = Math.Max(1d, orbitRadius - halfAltitude);
        var upperSegments = Math.Max(12, (int)Math.Ceiling((arrivalAngle + departureAngle) / (5d.ToRadians())));
        var lowerSegments = Math.Max(12, (int)Math.Ceiling((arrivalAngle + departureAngle) / (5d.ToRadians())));
        var positiveConnectorSegments = Math.Max(4, (int)Math.Ceiling(Math.Abs(arrivalAngle - departureAngle) / (5d.ToRadians())));
        var negativeConnectorSegments = Math.Max(4, (int)Math.Ceiling(Math.Abs(arrivalAngle - departureAngle) / (5d.ToRadians())));

        return new List<List<Vector2>>
        {
            BuildArcPath(outerRadius, centerAngle - departureAngle, centerAngle + arrivalAngle, upperSegments),
            BuildArcPath(innerRadius, centerAngle - arrivalAngle, centerAngle + departureAngle, lowerSegments),
            BuildArcPath(orbitRadius, centerAngle + departureAngle, centerAngle + arrivalAngle, positiveConnectorSegments),
            BuildArcPath(orbitRadius, centerAngle - arrivalAngle, centerAngle - departureAngle, negativeConnectorSegments),
            BuildRadialPath(centerAngle + arrivalAngle, orbitRadius, outerRadius),
            BuildRadialPath(centerAngle + departureAngle, innerRadius, orbitRadius),
            BuildRadialPath(centerAngle - departureAngle, orbitRadius, outerRadius),
            BuildRadialPath(centerAngle - arrivalAngle, innerRadius, orbitRadius),
        };

        List<Vector2> BuildArcPath(double radius, double startAngle, double endAngle, int segmentCount)
        {
            var points = new List<Vector2>(segmentCount + 1);
            for (int i = 0; i <= segmentCount; i++)
            {
                var t = (double)i / segmentCount;
                var angle = startAngle + ((endAngle - startAngle) * t);
                points.Add(ProjectPolarPoint(radius, angle));
            }

            return points;
        }

        List<Vector2> BuildRadialPath(double angle, double startRadius, double endRadius)
        {
            return new List<Vector2>
            {
                ProjectPolarPoint(startRadius, angle),
                ProjectPolarPoint(endRadius, angle),
            };
        }
    }

    protected abstract Vector2 ProjectPolarPoint(double radius, double angle);

    protected void DrawDashedPolyline(IReadOnlyList<Vector2> points, Color color, float thickness, double dashLength, double gapLength)
    {
        if (points.Count < 2)
        {
            return;
        }

        var patternLength = dashLength + gapLength;
        if (patternLength <= 0d)
        {
            return;
        }

        var patternOffset = 0d;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var segmentStart = points[i];
            var segmentEnd = points[i + 1];
            var segment = segmentEnd - segmentStart;
            var segmentLength = segment.Length();
            if (segmentLength <= 0f)
            {
                continue;
            }

            var direction = segment / segmentLength;
            var distanceAlongSegment = 0d;
            while (distanceAlongSegment < segmentLength)
            {
                var cyclePosition = patternOffset % patternLength;
                var remainingInCycle = patternLength - cyclePosition;
                var stepLength = Math.Min(remainingInCycle, segmentLength - distanceAlongSegment);

                if (cyclePosition < dashLength)
                {
                    var drawLength = Math.Min(stepLength, dashLength - cyclePosition);
                    var dashStart = segmentStart + (direction * (float)distanceAlongSegment);
                    var dashEnd = segmentStart + (direction * (float)(distanceAlongSegment + drawLength));
                    SpriteBatch.DrawLine(dashStart, dashEnd, color, thickness);
                }

                distanceAlongSegment += stepLength;
                patternOffset += stepLength;
            }
        }
    }

    protected void DrawDashedLine(Vector2 start, Vector2 end, Color color, float thickness, float dashLength, float gapLength)
    {
        var segment = end - start;
        var totalLength = segment.Length();
        if (totalLength <= 0f) return;
        var dir = segment / totalLength;
        var patternLength = dashLength + gapLength;
        var traveled = 0f;
        while (traveled < totalLength)
        {
            var dashStart = start + dir * traveled;
            var dashEnd = start + dir * Math.Min(traveled + dashLength, totalLength);
            SpriteBatch.DrawLine(dashStart, dashEnd, color, thickness);
            traveled += patternLength;
        }
    }
}
