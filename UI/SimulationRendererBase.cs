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
using System.Linq;

namespace SpaceTrafficController.UI;

public abstract class SimulationRendererBase
{
    protected readonly GraphicsDevice GraphicsDevice;
    protected readonly SpriteBatch SpriteBatch;
    protected readonly Camera2D Camera;

    protected MouseState MouseState;

    protected const int Scale = GameConstants.RenderingScale;

    // Shared colors for consistent visuals between renderers.
    protected static readonly Color OrbitDefaultColor = Color.LightGray * 0.8f;
    protected static readonly Color TargetOrbitColor = Color.Cyan * 0.8f;
    protected static readonly Color TargetApsisColor = Color.Cyan * 0.8f;
    protected static readonly Color SelectedOrbitColor = Color.White;
    protected static readonly Color HoverOrbitColor = Color.LightGray * 0.8f;
    protected static readonly Color HoverPredictedOrbitColor = Color.DarkGray * 0.8f;
    protected static readonly Color HoverNodeColor = Color.DarkGray;
    protected static readonly Color ManeuverIndicatorPendingColor = Color.Yellow;
    protected static readonly Color ManeuverIndicatorConfirmedColor = Color.LimeGreen;

    // Shared maneuver-indicator layout constants.
    protected const string ManeuverIndicatorLabel = "M";
    protected const float ManeuverIndicatorScale = 0.8f;
    protected const float ManeuverIndicatorPadding = 1f;
    protected const float ManeuverIndicatorCircleRadiusFactor = 0.55f;
    protected const float ManeuverIndicatorCircleRadiusOffset = 0.75f;
    protected const float ManeuverIndicatorSpacing = 2f;

    protected static readonly Color UncontrolledShipColor = Color.LightGray;
    protected static readonly Color SelectedShipColor = Color.Gold;
    protected static readonly Color ActiveShipColor = Color.LimeGreen;
    protected static readonly Color ExitDestinationShipColor = Color.MediumPurple;
    protected static readonly Color EncroachedSeparationColor = Color.Red;
    protected static readonly Color SafeSeparationColor = Color.Green;
    protected static readonly Color ActivationFlashColor = Color.Yellow;

    protected static readonly Color SelectedStationColor = Color.Gold;
    protected static readonly Color StationColor = Color.AliceBlue;
    protected static readonly Color ControlAreaColor = Color.LightSkyBlue;
    protected static readonly Color ControlAreaLaneColor = Color.LightSkyBlue * 0.3f;
    protected static readonly Color ArrivalArrowColor = Color.LimeGreen;
    protected static readonly Color DepartureArrowColor = Color.Red;

    protected const double ControlAreaLaneCenterSpacingMeters = GameConstants.ControlLaneWidthMeters;
    protected const double ControlAreaLaneHalfWidthMeters = GameConstants.ControlLaneHalfWidthMeters;
    protected const double ActivationFlashBlinkSeconds = 0.2d;

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

    protected static bool ShouldDrawActivationFlash(Ship ship)
    {
        var remaining = ship.Status.ActivationFlashTimeRemainingSeconds;
        if (remaining <= 0d)
        {
            return false;
        }

        var phase = (int)Math.Floor(remaining / ActivationFlashBlinkSeconds);
        return (phase & 1) == 0;
    }

    protected List<List<Vector2>> BuildStationControlPaths(Station station)
    {
        var stationPosition = station?.Orbit?.PositionVectorD ?? default;
        var orbitRadius = stationPosition.Length();
        if (orbitRadius <= 0d || station is null)
        {
            return new List<List<Vector2>>();
        }

        var centerAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var departureAngle = Math.Min(station.ControlAreaDepartureExtentMeters / orbitRadius, Math.PI - 1e-4d);
        var stationLowerRadius = Math.Max(1d, orbitRadius - GameConstants.ControlLaneHalfWidthMeters);
        var stationUpperRadius = orbitRadius + GameConstants.ControlLaneHalfWidthMeters;
        var rectangles = new List<(double MinX, double MaxX, double MinY, double MaxY)>
        {
            (-departureAngle, departureAngle, stationLowerRadius, stationUpperRadius),
        };

        var approachLaneCount = ControlLaneUtils.GetStationApproachLaneCount(station);
        for (int laneDepth = 1; laneDepth <= approachLaneCount; laneDepth++)
        {
            var arrivalAngle = Math.Min(ControlLaneUtils.GetStationApproachExtentMeters(station, laneDepth) / orbitRadius, Math.PI - 1e-4d);
            var previousUpperRadius = orbitRadius + GameConstants.ControlLaneHalfWidthMeters + ((laneDepth - 1) * GameConstants.ControlLaneWidthMeters);
            var currentUpperRadius = previousUpperRadius + GameConstants.ControlLaneWidthMeters;
            var currentLowerRadius = Math.Max(1d, orbitRadius - GameConstants.ControlLaneHalfWidthMeters - (laneDepth * GameConstants.ControlLaneWidthMeters));
            var previousLowerRadius = Math.Max(1d, currentLowerRadius + GameConstants.ControlLaneWidthMeters);

            rectangles.Add((laneDepth == 1 ? -departureAngle : 0d, arrivalAngle, previousUpperRadius, currentUpperRadius));
            rectangles.Add((-arrivalAngle, laneDepth == 1 ? departureAngle : 0d, currentLowerRadius, previousLowerRadius));
        }

        if (rectangles.Count == 0)
        {
            return new List<List<Vector2>>();
        }

        var xCoordinates = new List<double>();
        var yCoordinates = new List<double>();
        foreach (var rectangle in rectangles)
        {
            xCoordinates.Add(rectangle.MinX);
            xCoordinates.Add(rectangle.MaxX);
            yCoordinates.Add(rectangle.MinY);
            yCoordinates.Add(rectangle.MaxY);
        }

        xCoordinates.Sort();
        yCoordinates.Sort();
        xCoordinates = xCoordinates.Distinct().ToList();
        yCoordinates = yCoordinates.Distinct().ToList();

        if (xCoordinates.Count < 2 || yCoordinates.Count < 2)
        {
            return new List<List<Vector2>>();
        }

        var filled = new bool[xCoordinates.Count - 1, yCoordinates.Count - 1];
        for (int xIndex = 0; xIndex < xCoordinates.Count - 1; xIndex++)
        {
            var sampleX = (xCoordinates[xIndex] + xCoordinates[xIndex + 1]) / 2d;
            for (int yIndex = 0; yIndex < yCoordinates.Count - 1; yIndex++)
            {
                var sampleY = (yCoordinates[yIndex] + yCoordinates[yIndex + 1]) / 2d;
                foreach (var rectangle in rectangles)
                {
                    if (sampleX >= rectangle.MinX && sampleX <= rectangle.MaxX && sampleY >= rectangle.MinY && sampleY <= rectangle.MaxY)
                    {
                        filled[xIndex, yIndex] = true;
                        break;
                    }
                }
            }
        }

        var edges = new Dictionary<(int X, int Y), List<(int X, int Y)>>();

        void AddEdge((int X, int Y) start, (int X, int Y) end)
        {
            if (!edges.TryGetValue(start, out var nextVertices))
            {
                nextVertices = new List<(int X, int Y)>();
                edges[start] = nextVertices;
            }

            nextVertices.Add(end);
        }

        for (int xIndex = 0; xIndex < xCoordinates.Count - 1; xIndex++)
        {
            for (int yIndex = 0; yIndex < yCoordinates.Count - 1; yIndex++)
            {
                if (!filled[xIndex, yIndex])
                {
                    continue;
                }

                if (yIndex == yCoordinates.Count - 2 || !filled[xIndex, yIndex + 1])
                {
                    AddEdge((xIndex, yIndex + 1), (xIndex + 1, yIndex + 1));
                }

                if (xIndex == xCoordinates.Count - 2 || !filled[xIndex + 1, yIndex])
                {
                    AddEdge((xIndex + 1, yIndex + 1), (xIndex + 1, yIndex));
                }

                if (yIndex == 0 || !filled[xIndex, yIndex - 1])
                {
                    AddEdge((xIndex + 1, yIndex), (xIndex, yIndex));
                }

                if (xIndex == 0 || !filled[xIndex - 1, yIndex])
                {
                    AddEdge((xIndex, yIndex), (xIndex, yIndex + 1));
                }
            }
        }

        var paths = new List<List<Vector2>>();
        while (true)
        {
            var remainingEdge = edges.FirstOrDefault(entry => entry.Value.Count > 0);
            if (remainingEdge.Value is null || remainingEdge.Value.Count == 0)
            {
                break;
            }

            var startVertex = remainingEdge.Key;
            var currentVertex = startVertex;
            var pathVertices = new List<(int X, int Y)> { startVertex };

            while (edges.TryGetValue(currentVertex, out var nextVertices) && nextVertices.Count > 0)
            {
                var nextVertex = nextVertices[0];
                nextVertices.RemoveAt(0);
                pathVertices.Add(nextVertex);
                currentVertex = nextVertex;

                if (currentVertex == startVertex)
                {
                    break;
                }
            }

            if (pathVertices.Count < 2)
            {
                continue;
            }

            var points = new List<Vector2>();
            for (int i = 0; i < pathVertices.Count - 1; i++)
            {
                var segmentStartVertex = pathVertices[i];
                var segmentEndVertex = pathVertices[i + 1];
                var startAngle = centerAngle + xCoordinates[segmentStartVertex.X];
                var endAngle = centerAngle + xCoordinates[segmentEndVertex.X];
                var startRadius = yCoordinates[segmentStartVertex.Y];
                var endRadius = yCoordinates[segmentEndVertex.Y];

                if (segmentStartVertex.Y == segmentEndVertex.Y)
                {
                    var segmentCount = Math.Max(1, (int)Math.Ceiling(Math.Abs(endAngle - startAngle) / (5d.ToRadians())));
                    for (int segmentIndex = 0; segmentIndex <= segmentCount; segmentIndex++)
                    {
                        if (points.Count > 0 && segmentIndex == 0)
                        {
                            continue;
                        }

                        var t = (double)segmentIndex / segmentCount;
                        var angle = startAngle + ((endAngle - startAngle) * t);
                        points.Add(ProjectPolarPoint(startRadius, angle));
                    }
                }
                else
                {
                    if (points.Count == 0)
                    {
                        points.Add(ProjectPolarPoint(startRadius, startAngle));
                    }

                    points.Add(ProjectPolarPoint(endRadius, endAngle));
                }
            }

            paths.Add(points);
        }

        return paths;
    }

    protected static IEnumerable<double> EnumeratePlanetControlLaneEdgeAltitudes(double controlAltitudeMeters, double atmosphereTopAltitudeMeters)
    {
        if (controlAltitudeMeters <= ControlAreaLaneHalfWidthMeters)
        {
            yield break;
        }

        var atmosphereTop = Math.Max(0d, atmosphereTopAltitudeMeters);

        // Lane centers are spaced every 50 km from the surface, so lane boundaries are
        // at 25 km, 75 km, 125 km, ... within the planet control area.
        for (var edgeAltitude = ControlAreaLaneHalfWidthMeters; edgeAltitude < controlAltitudeMeters - 1d; edgeAltitude += ControlAreaLaneCenterSpacingMeters)
        {
            if (edgeAltitude <= atmosphereTop)
            {
                continue;
            }

            yield return edgeAltitude;
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
