using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using SpaceTrafficController.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceTrafficController.UI;

public class CartesianSimulationRenderer : SimulationRendererBase
{
    private const float TopBuffer = 18f;
    private const float BottomBuffer = 30f;
    private float _projectedPanX;

    public CartesianSimulationRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Camera2D camera)
        : base(graphicsDevice, spriteBatch, camera)
    {
    }

    public void DrawWorld(GameState gameState)
    {
        MouseState = Mouse.GetState();
        _projectedPanX = gameState.ProjectedPanX;
        var body = GameState.CentralBody;
        var station = gameState.Stations.FirstOrDefault();

        DrawBackground(body);
        DrawBodyAndReferenceLines(body, gameState.ShowControlAreaLanes);

        var orbitDefaultColor = OrbitDefaultColor;

        if (gameState.ShowAllOrbits)
        {
            foreach (var obj in gameState.OrbitingObjects)
            {
                try
                {
                    DrawOrbit(obj.Orbit, orbitDefaultColor);
                    DrawApsisMarkers(obj.Orbit, orbitDefaultColor);
                }
                catch
                {
                }
            }
        }

        if (gameState.ShowAllManeuvers)
        {
            foreach (var ship in gameState.Ships)
            {
                var firstNode = ship.ManeuverNode;
                if (firstNode is null || !firstNode.IsConfirmed) continue;
                try
                {
                    DrawOrbit(ship.Orbit, orbitDefaultColor);
                    DrawManueverNode(firstNode, ship.Orbit, drawButtons: false);

                    var secondNode = ship.NextManeuverNode;
                    var secondBaseOrbit = firstNode.GetPredictedOrbit(ship.Orbit);
                    if (secondNode is not null && secondBaseOrbit is not null)
                    {
                        DrawManueverNode(secondNode, secondBaseOrbit, drawButtons: false);
                    }
                }
                catch
                {
                }
            }
        }

        DrawStations(gameState.Stations, gameState.SelectedShip);
        DrawShips(gameState.Ships, gameState);

        var hoveredShip = GetHoveredShip(gameState.Ships);
        var selectedShip = gameState.SelectedShip;
        var target = gameState.TargetOrbitingObject;
        var relativeReference = target ?? station;

        if (relativeReference is not null)
        {
            if (selectedShip is not null)
            {
                DrawRelativePath(selectedShip, relativeReference, SelectedOrbitColor * 0.85f);
            }

            if (hoveredShip is not null && hoveredShip != selectedShip)
            {
                DrawRelativePath(hoveredShip, relativeReference, HoverOrbitColor * 0.9f);
            }

            if (target is not null && target != selectedShip && target != hoveredShip)
            {
                DrawRelativePath(target, relativeReference, TargetOrbitColor * 0.9f);
            }

            var conflictSubject = selectedShip ?? hoveredShip;
            if (conflictSubject is not null && target is Ship targetShip && conflictSubject != targetShip)
            {
                DrawPotentialRelativeConflicts(conflictSubject, targetShip, relativeReference);
            }
        }

        if (hoveredShip is not null && !hoveredShip.IsSelected)
        {
            DrawHoveredShipPreview(hoveredShip);
        }

        if (selectedShip is not null && target is not null)
        {
            var targetOrbit = target.Orbit;
            DrawOrbit(targetOrbit, TargetOrbitColor);
            DrawApsisMarkers(targetOrbit, TargetApsisColor);
        }
    }

    private void DrawBackground(CelestialBody body)
    {
        var w = GraphicsDevice.Viewport.Width;
        var h = GraphicsDevice.Viewport.Height;
        var surfaceY = GetSurfaceY();

        SpriteBatch.FillRectangle(0, 0, w, h, new Color(8, 12, 28));

        // surface strip
        SpriteBatch.FillRectangle(0, surfaceY, w, h - surfaceY, new Color(56, 42, 30));

        // slight top buffer style strip
        SpriteBatch.FillRectangle(0, 0, w, TopBuffer, new Color(0, 0, 0, 35));

        // atmosphere bands
        foreach (var layer in body.AtmosphereLayers)
        {
            var yBottom = ProjectAltitudeToY(layer.Altitude);
            var yTop = ProjectAltitudeToY(layer.Altitude + layer.Thickness);
            var y = Math.Min(yTop, yBottom);
            var height = Math.Max(1f, Math.Abs(yBottom - yTop));
            var alpha = (float)Math.Clamp(layer.Density / Math.Max(1e-9, body.BaseAtmosphereDensity), 0d, 1d);
            SpriteBatch.FillRectangle(0, y, w, height, new Color(90, 160, 220) * (alpha * 0.45f));
        }
    }

    private void DrawBodyAndReferenceLines(CelestialBody body, bool showControlAreaLanes)
    {
        var w = GraphicsDevice.Viewport.Width;
        var h = GraphicsDevice.Viewport.Height;
        var atmosphereTopAltitude = body.AtmosphereLayers.Count > 0
            ? body.AtmosphereLayers.Max(layer => layer.Altitude + layer.Thickness)
            : 0d;
        var surfaceY = GetSurfaceY();
        var topY = TopBuffer;

        // surface + control top outlines
        SpriteBatch.DrawLine(new Vector2(0, surfaceY), new Vector2(w, surfaceY), Color.Wheat, 2f);
        SpriteBatch.DrawLine(new Vector2(0, topY), new Vector2(w, topY), Color.Gray * 0.8f, 1f);

        // 0° longitude reference line: draw only on the planet strip
        var zeroLongitudeX = ProjectAngleToX(0d);
        SpriteBatch.DrawLine(new Vector2(zeroLongitudeX, surfaceY), new Vector2(zeroLongitudeX, h), Color.Red * 0.75f, 1.5f);

        // control altitude reference
        var controlY = ProjectAltitudeToY(body.ControlAltitudeMeters);
        SpriteBatch.DrawLine(new Vector2(0, controlY), new Vector2(w, controlY), Color.LightGray * 0.65f, 1f);

        if (showControlAreaLanes)
        {
            foreach (var edgeAltitude in EnumeratePlanetControlLaneEdgeAltitudes(body.ControlAltitudeMeters, atmosphereTopAltitude))
            {
                var y = ProjectAltitudeToY(edgeAltitude);
                DrawDashedLine(
                    new Vector2(0f, y),
                    new Vector2(w, y),
                    ControlAreaLaneColor,
                    1f,
                    dashLength: 8f,
                    gapLength: 4f);
            }
        }
    }

    private void DrawShips(List<Ship> ships, GameState gameState)
    {
        const int size = 5;
        foreach (var ship in ships)
        {
            var position = ProjectPosition(ship.Orbit.PositionVectorD);

            if (ship.Status.IsControllable)
            {
                var zoneColor = ship.Status.IsEncroached ? EncroachedSeparationColor : SafeSeparationColor;
                DrawProjectedLaneControlZone(ship, position, zoneColor, 1f);
            }

            if (ship.IsSelected)
            {
                var orbit = ship.Orbit;
                DrawOrbit(orbit, SelectedOrbitColor);
                DrawApsisMarkers(orbit, SelectedOrbitColor);

                if (ship.ManeuverNode is not null)
                {
                    var firstNode = ship.ManeuverNode;
                    DrawManueverNode(firstNode, orbit, drawButtons: ship.NextManeuverNode is null);

                    var secondBaseOrbit = firstNode.GetPredictedOrbit(orbit);
                    if (ship.NextManeuverNode is null && firstNode.IsConfirmed && secondBaseOrbit is not null)
                    {
                        DrawOrbitMouseIntersection(secondBaseOrbit);
                    }

                    if (ship.NextManeuverNode is not null && secondBaseOrbit is not null)
                    {
                        DrawManueverNode(ship.NextManeuverNode, secondBaseOrbit, drawButtons: true);
                    }
                }
                else
                {
                    DrawOrbitMouseIntersection(orbit);
                }

                var destinationOrbit = GetDestinationOrbit(ship);
                if (destinationOrbit is not null)
                {
                    DrawOrbit(destinationOrbit, TargetOrbitColor);
                    DrawApsisMarkers(destinationOrbit, TargetApsisColor);
                }
            }

            var shipColor = !ship.Status.IsControllable
                ? UncontrolledShipColor
                : ship.IsSelected
                    ? SelectedShipColor
                    : ship.Destination is ExitControlAreaDestination
                        ? ExitDestinationShipColor
                        : ActiveShipColor;

            SpriteBatch.DrawRectangle(position.X - (size / 2f), position.Y - (size / 2f), size, size, shipColor, 1.5f);

            if (ShouldDrawActivationFlash(ship))
            {
                SpriteBatch.DrawCircle(new CircleF() { Center = position, Radius = 8f }, 20, ActivationFlashColor, 1.8f);
            }

            var nodeCount = (ship.ManeuverNode is not null ? 1 : 0) + (ship.NextManeuverNode is not null ? 1 : 0);
            if (nodeCount > 0)
            {
                var markerFont = Fonts.ManueverNode ?? Fonts.DebugFont;
                var markerScale = ManeuverIndicatorScale;
                var markerLabel = ManeuverIndicatorLabel;
                var textSize = markerFont.MeasureString(markerLabel) * markerScale;
                var markerPadding = ManeuverIndicatorPadding;
                var markerStartPos = position + new Vector2(
                    (size * 0.5f) + markerPadding + 2f,
                    -((size * 0.5f) + markerPadding + textSize.Y + 2f));

                var markerCircleRadius = MathF.Max(textSize.X, textSize.Y) * ManeuverIndicatorCircleRadiusFactor + ManeuverIndicatorCircleRadiusOffset;
                var markerStepX = (markerCircleRadius * 2f) + ManeuverIndicatorSpacing;

                var markerIndex = 0;
                if (ship.ManeuverNode is not null)
                {
                    var markerColor = ship.ManeuverNode.IsConfirmed ? ManeuverIndicatorConfirmedColor : ManeuverIndicatorPendingColor;
                    var markerPos = markerStartPos + new Vector2(markerStepX * markerIndex, 0f);
                    var markerCenter = markerPos + (textSize * 0.5f);
                    SpriteBatch.DrawCircle(new CircleF() { Center = markerCenter, Radius = markerCircleRadius }, 20, markerColor, 1f);
                    SpriteBatch.DrawString(markerFont, markerLabel, markerPos, markerColor, 0f, Vector2.Zero, markerScale, SpriteEffects.None, 0f);
                    markerIndex++;
                }

                if (ship.NextManeuverNode is not null)
                {
                    var markerColor = ship.NextManeuverNode.IsConfirmed ? ManeuverIndicatorConfirmedColor : ManeuverIndicatorPendingColor;
                    var markerPos = markerStartPos + new Vector2(markerStepX * markerIndex, 0f);
                    var markerCenter = markerPos + (textSize * 0.5f);
                    SpriteBatch.DrawCircle(new CircleF() { Center = markerCenter, Radius = markerCircleRadius }, 20, markerColor, 1f);
                    SpriteBatch.DrawString(markerFont, markerLabel, markerPos, markerColor, 0f, Vector2.Zero, markerScale, SpriteEffects.None, 0f);
                }
            }
        }
    }

    private void DrawProjectedLaneControlZone(Ship ship, Vector2 center, Color color, float thickness)
    {
        var body = GameState.CentralBody;
        var altitudeMeters = ship.Orbit.PositionVectorD.Length() - body.Radius;
        if (!ControlLaneUtils.TryGetShipEffectiveLaneBounds(body, altitudeMeters, out var laneLowerAltitude, out var laneUpperAltitude, out _))
        {
            return;
        }

        var shipRadiusMeters = ship.Orbit.PositionVectorD.Length();
        if (!double.IsFinite(shipRadiusMeters) || shipRadiusMeters <= 0d)
        {
            return;
        }

        var width = GraphicsDevice.Viewport.Width;
        var pixelsPerMeterX = width / Math.Max(1d, MathHelper.TwoPi * shipRadiusMeters);
        var halfWidthPixels = (float)(GameConstants.ControlLaneLongitudinalHalfExtentMeters * pixelsPerMeterX);
        if (halfWidthPixels <= 0f)
        {
            return;
        }

        var top = ProjectAltitudeToY(laneUpperAltitude);
        var bottom = ProjectAltitudeToY(laneLowerAltitude);
        var rectY = Math.Min(top, bottom);
        var rectHeight = Math.Max(1f, Math.Abs(bottom - top));

        var left = center.X - halfWidthPixels;
        var right = center.X + halfWidthPixels;
        if (left >= 0f && right <= width)
        {
            SpriteBatch.DrawRectangle(left, rectY, right - left, rectHeight, color, thickness);
            return;
        }

        if (left < 0f)
        {
            SpriteBatch.DrawRectangle(0f, rectY, right, rectHeight, color, thickness);
            SpriteBatch.DrawRectangle(width + left, rectY, -left, rectHeight, color, thickness);
            return;
        }

        SpriteBatch.DrawRectangle(left, rectY, width - left, rectHeight, color, thickness);
        SpriteBatch.DrawRectangle(0f, rectY, right - width, rectHeight, color, thickness);
    }

    private void DrawOrbitMouseIntersection(Orbit orbit)
    {
        var mouseScreen = MouseState.Position.ToVector2();
        if (!TryGetProjectedOrbitClosestPoint(orbit, mouseScreen, out var screenPos))
            return;

        SpriteBatch.DrawCircle(new CircleF() { Center = screenPos, Radius = 4f }, 16, HoverNodeColor, 1.5f);
    }

    private bool TryGetProjectedOrbitClosestPoint(Orbit orbit, Vector2 mouseScreen, out Vector2 closestScreenPoint)
    {
        closestScreenPoint = Vector2.Zero;

        var sampleCount = orbit.IsEscapeTrajectory ? 220 : 360;
        if (sampleCount < 2)
            return false;

        var points = new List<Vector2>(sampleCount + 1);
        if (orbit.IsEscapeTrajectory)
        {
            var maxAngle = orbit.GetHyperbolicTrueAnomalyLimit() - 0.01d;
            for (int i = 0; i <= sampleCount; i++)
            {
                var angle = -maxAngle + ((2d * maxAngle) * i / sampleCount);
                points.Add(ProjectPosition(orbit.GetPositionAtAngleD(angle)));
            }
        }
        else
        {
            for (int i = 0; i <= sampleCount; i++)
            {
                var angle = MathHelper.TwoPi * i / sampleCount;
                points.Add(ProjectPosition(orbit.GetPositionAtAngleD(angle)));
            }
        }

        var width = GraphicsDevice.Viewport.Width;
        var bestDistSq = float.MaxValue;
        var found = false;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];

            if (width > 0)
            {
                if (b.X - a.X > width * 0.5f) b.X -= width;
                else if (b.X - a.X < -width * 0.5f) b.X += width;
            }

            for (int wrap = -1; wrap <= 1; wrap++)
            {
                var shiftedMouse = mouseScreen + new Vector2(wrap * width, 0f);
                var candidate = ClosestPointOnSegment(a, b, shiftedMouse);
                var distSq = Vector2.DistanceSquared(shiftedMouse, candidate);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    closestScreenPoint = candidate;
                    found = true;
                }
            }
        }

        if (!found)
            return false;

        if (width > 0)
        {
            closestScreenPoint.X = (closestScreenPoint.X % width + width) % width;
        }

        const float thresholdPx = 20f;
        return bestDistSq <= thresholdPx * thresholdPx;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        var abLenSq = ab.LengthSquared();
        if (abLenSq <= 1e-6f)
            return a;

        var t = Vector2.Dot(p - a, ab) / abLenSq;
        t = Math.Clamp(t, 0f, 1f);
        return a + (ab * t);
    }

    private Ship? GetHoveredShip(List<Ship> ships)
    {
        var mouse = MouseState.Position.ToVector2();
        const float threshold = 10f;
        foreach (var ship in ships)
        {
            var shipPos = ProjectPosition(ship.Orbit.PositionVectorD);
            if (Vector2.Distance(mouse, shipPos) <= threshold)
            {
                return ship;
            }
        }

        return null;
    }

    private void DrawRelativePath(HasOrbit subject, HasOrbit reference, Color color)
    {
        if (subject is null || reference is null)
        {
            return;
        }

        var subjectOrbit = subject.Orbit;
        var referenceOrbit = reference.Orbit;
        if (subjectOrbit is null || referenceOrbit is null)
        {
            return;
        }

        var currentReferencePosition = referenceOrbit.PositionVectorD;
        if (currentReferencePosition.Length() <= 0d)
        {
            return;
        }

        var durationSeconds = GetSingleOrbitDurationSeconds(subjectOrbit, referenceOrbit);
        const int samples = 240;

        var planSubject = BuildTrajectoryPlan(subject, durationSeconds);
        var planReference = BuildTrajectoryPlan(reference, durationSeconds);
        var subjectPositions = SamplePlanPositions(planSubject, samples, durationSeconds);
        var referencePositions = SamplePlanPositions(planReference, samples, durationSeconds);
        if (subjectPositions.Count != referencePositions.Count || subjectPositions.Count == 0)
        {
            return;
        }

        var currentReferenceAngle = Math.Atan2(currentReferencePosition.Y, currentReferencePosition.X);
        var points = new List<Vector2>(subjectPositions.Count);
        for (int i = 0; i < subjectPositions.Count; i++)
        {
            points.Add(ToRelativeProjectedScreen(subjectPositions[i], referencePositions[i], currentReferenceAngle));
        }

        if (points.Count < 2)
        {
            return;
        }

        // Resample points to be uniformly spaced by distance so dashes appear evenly sized
        points = ResamplePointsByDistance(points);

        var width = GraphicsDevice.Viewport.Width;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];

            if (Math.Abs(b.X - a.X) > width * 0.5f)
            {
                continue;
            }

            DrawDashedLine(a, b, color, 1f, 4f, 5f);
        }

        DrawRelativePathIntervalMarkers(points, color);
    }

    private List<Vector2> ResamplePointsByDistance(List<Vector2> points)
    {
        if (points.Count < 2)
            return new List<Vector2>(points);

        var width = GraphicsDevice.Viewport.Width;
        
        // First pass: add intermediate points at sharp curves for smooth dashing
        var smoothedPoints = new List<Vector2>();
        smoothedPoints.Add(points[0]);
        
        const float angleThreshold = 0.2f; // radians, ~11 degrees
        for (int i = 1; i < points.Count - 1; i++)
        {
            var prev = points[i - 1];
            var curr = points[i];
            var next = points[i + 1];
            
            var delta1 = curr - prev;
            var delta2 = next - curr;
            
            if (delta1.LengthSquared() > 0.01f && delta2.LengthSquared() > 0.01f)
            {
                delta1.Normalize();
                delta2.Normalize();
                
                // Calculate angle between segments
                var angle = Math.Acos(Math.Clamp(Vector2.Dot(delta1, delta2), -1f, 1f));
                
                // If angle is sharp, add intermediate point
                if (angle > angleThreshold)
                {
                    smoothedPoints.Add(curr);
                }
            }
            
            smoothedPoints.Add(curr);
        }
        smoothedPoints.Add(points[points.Count - 1]);
        
        // Calculate cumulative distances
        var distances = new List<float>(smoothedPoints.Count) { 0f };
        for (int i = 1; i < smoothedPoints.Count; i++)
        {
            var delta = smoothedPoints[i] - smoothedPoints[i - 1];
            // Skip wrapped segments
            if (Math.Abs(delta.X) > width * 0.5f)
            {
                distances.Add(distances[i - 1]);
            }
            else
            {
                distances.Add(distances[i - 1] + delta.Length());
            }
        }

        var totalDistance = distances[distances.Count - 1];
        if (totalDistance <= 0f)
            return new List<Vector2>(points);

        // Resample with 9px spacing (4px dash + 5px gap)
        const float spacing = 9f;
        var resampled = new List<Vector2>();
        
        for (float d = 0f; d <= totalDistance; d += spacing)
        {
            // Find the segment containing this distance
            int idx = distances.BinarySearch(d);
            if (idx >= 0)
            {
                resampled.Add(smoothedPoints[idx]);
            }
            else
            {
                idx = ~idx;
                if (idx == 0)
                {
                    resampled.Add(smoothedPoints[0]);
                }
                else if (idx >= distances.Count)
                {
                    resampled.Add(smoothedPoints[smoothedPoints.Count - 1]);
                }
                else
                {
                    var segmentDist = distances[idx] - distances[idx - 1];
                    if (segmentDist > 0f)
                    {
                        var t = (d - distances[idx - 1]) / segmentDist;
                        resampled.Add(Vector2.Lerp(smoothedPoints[idx - 1], smoothedPoints[idx], t));
                    }
                    else
                    {
                        resampled.Add(smoothedPoints[idx - 1]);
                    }
                }
            }
        }

        // Always add the last point
        if (resampled.Count == 0 || Vector2.Distance(resampled[resampled.Count - 1], smoothedPoints[smoothedPoints.Count - 1]) > 0.1f)
        {
            resampled.Add(smoothedPoints[smoothedPoints.Count - 1]);
        }

        return resampled;
    }

    private void DrawRelativePathIntervalMarkers(IReadOnlyList<Vector2> points, Color baseColor)
    {
        if (points is null || points.Count < 2)
        {
            return;
        }

        var width = GraphicsDevice.Viewport.Width;
        if (width <= 0)
        {
            return;
        }

        var unwrapped = new List<Vector2>(points.Count) { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            var prev = unwrapped[i - 1];
            var curr = points[i];
            var x = curr.X;

            while ((x - prev.X) > (width * 0.5f)) x -= width;
            while ((x - prev.X) < -(width * 0.5f)) x += width;

            unwrapped.Add(new Vector2(x, curr.Y));
        }

        if (unwrapped.Count >= 3)
        {
            var quarterFractions = new[] { 0.25f, 0.5f, 0.75f };
            foreach (var frac in quarterFractions)
            {
                var idx = (int)Math.Round(frac * (unwrapped.Count - 1));
                idx = Math.Clamp(idx, 1, unwrapped.Count - 2);
                DrawPerpendicularMarkerAt(unwrapped, idx, markerHalfLength: 4f, baseColor * 0.85f, thickness: 1f);
            }
        }

        // One full orbit marker at the path end, slightly longer.
        DrawPerpendicularMarkerAt(unwrapped, unwrapped.Count - 1, markerHalfLength: 6f, baseColor, thickness: 1.25f);
    }

    private void DrawPerpendicularMarkerAt(IReadOnlyList<Vector2> unwrappedPoints, int index, float markerHalfLength, Color color, float thickness)
    {
        if (unwrappedPoints.Count < 2)
        {
            return;
        }

        var idx = Math.Clamp(index, 0, unwrappedPoints.Count - 1);
        var prevIdx = Math.Max(0, idx - 1);
        var nextIdx = Math.Min(unwrappedPoints.Count - 1, idx + 1);

        var center = unwrappedPoints[idx];
        var tangent = unwrappedPoints[nextIdx] - unwrappedPoints[prevIdx];
        if (tangent.LengthSquared() <= 1e-6f)
        {
            return;
        }

        tangent.Normalize();
        var normal = new Vector2(-tangent.Y, tangent.X);
        var start = center - (normal * markerHalfLength);
        var end = center + (normal * markerHalfLength);

        var width = GraphicsDevice.Viewport.Width;
        for (int k = -1; k <= 1; k++)
        {
            var shift = new Vector2(k * width, 0f);
            SpriteBatch.DrawLine(start + shift, end + shift, color, thickness);
        }
    }

    private void DrawPotentialRelativeConflicts(Ship shipA, Ship shipB, HasOrbit reference)
    {
        if (shipA is null || shipB is null || reference is null)
        {
            return;
        }

        var referencePosition = reference.Orbit.PositionVectorD;
        if (referencePosition.Length() <= 0d)
        {
            return;
        }

        var durationSeconds = Math.Max(
            GetSingleOrbitDurationSeconds(shipA.Orbit, reference.Orbit),
            GetSingleOrbitDurationSeconds(shipB.Orbit, reference.Orbit));
        const int samples = 300;

        var planA = BuildTrajectoryPlan(shipA, durationSeconds);
        var planB = BuildTrajectoryPlan(shipB, durationSeconds);
        var planReference = BuildTrajectoryPlan(reference, durationSeconds);

        var positionsA = SamplePlanPositions(planA, samples, durationSeconds);
        var positionsB = SamplePlanPositions(planB, samples, durationSeconds);
        var referencePositions = SamplePlanPositions(planReference, samples, durationSeconds);
        if (positionsA.Count != positionsB.Count || positionsA.Count != referencePositions.Count || positionsA.Count == 0)
        {
            return;
        }

        var thresholdMeters = GameConstants.ShipSepration;
        var conflictIndices = new List<int>();
        var inConflict = false;
        var conflictStart = 0;

        for (int i = 0; i < positionsA.Count; i++)
        {
            var delta = positionsA[i] - positionsB[i];
            var distance = delta.Length();
            var altitudeA = positionsA[i].Length() - GameState.CentralBody.Radius;
            var altitudeB = positionsB[i].Length() - GameState.CentralBody.Radius;
            var occupiedLanesA = ControlLaneUtils.GetShipOccupiedLaneIndices(GameState.CentralBody, altitudeA);
            var occupiedLanesB = ControlLaneUtils.GetShipOccupiedLaneIndices(GameState.CentralBody, altitudeB);
            var sharesLane = occupiedLanesA.Count > 0 && occupiedLanesB.Count > 0 && occupiedLanesA.Any(lane => occupiedLanesB.Contains(lane));
            var isConflict = sharesLane && distance <= thresholdMeters;

            if (isConflict && !inConflict)
            {
                inConflict = true;
                conflictStart = i;
            }
            else if (!isConflict && inConflict)
            {
                inConflict = false;
                conflictIndices.Add((conflictStart + i - 1) / 2);
            }
        }

        if (inConflict)
        {
            conflictIndices.Add((conflictStart + positionsA.Count - 1) / 2);
        }

        if (conflictIndices.Count == 0)
        {
            return;
        }

        var currentReferenceAngle = Math.Atan2(referencePosition.Y, referencePosition.X);
        var markersToDraw = Math.Min(6, conflictIndices.Count);
        for (int i = 0; i < markersToDraw; i++)
        {
            var idx = conflictIndices[i];
            var markerA = ToRelativeProjectedScreen(positionsA[idx], referencePositions[idx], currentReferenceAngle);
            var markerB = ToRelativeProjectedScreen(positionsB[idx], referencePositions[idx], currentReferenceAngle);

            DrawDashedLine(markerA, markerB, EncroachedSeparationColor * 0.9f, 1.1f, 5f, 3f);
            SpriteBatch.DrawCircle(new CircleF() { Center = markerA, Radius = 4f }, 14, EncroachedSeparationColor, 1.4f);
            SpriteBatch.DrawCircle(new CircleF() { Center = markerB, Radius = 4f }, 14, EncroachedSeparationColor, 1.4f);
        }
    }

    private static TrajectoryPlan BuildTrajectoryPlan(HasOrbit orbitingObject, double totalDurationSeconds)
    {
        var plan = new TrajectoryPlan();
        if (orbitingObject?.Orbit is null || totalDurationSeconds <= 0d)
        {
            return plan;
        }

        if (orbitingObject is not Ship ship)
        {
            plan.Segments.Add(new TrajectorySegment(CloneOrbitForSampling(orbitingObject.Orbit), totalDurationSeconds));
            return plan;
        }

        var currentOrbit = CloneOrbitForSampling(ship.Orbit);
        if (currentOrbit is null)
        {
            return plan;
        }

        var remaining = totalDurationSeconds;
        var elapsed = 0d;
        var nodes = new List<ManeuverNode>();
        if (ship.ManeuverNode is not null)
        {
            nodes.Add(ship.ManeuverNode);
        }

        if (ship.NextManeuverNode is not null)
        {
            nodes.Add(ship.NextManeuverNode);
        }

        var hasPredictedSegment = false;
        foreach (var node in nodes)
        {
            if (remaining <= 0d)
            {
                break;
            }

            var timeToNode = currentOrbit.TimeToTrueAomaly(node.TrueAnomaly);
            var canReachNode = double.IsFinite(timeToNode) && !double.IsNaN(timeToNode) && timeToNode > 0d;

            if (!canReachNode || timeToNode >= remaining)
            {
                plan.Segments.Add(new TrajectorySegment(currentOrbit, remaining, hasPredictedSegment));
                remaining = 0d;
                break;
            }

            plan.Segments.Add(new TrajectorySegment(currentOrbit, timeToNode, hasPredictedSegment));
            remaining -= timeToNode;
            elapsed += timeToNode;

            var predictedOrbit = node.GetPredictedOrbit(currentOrbit);
            if (predictedOrbit is null)
            {
                break;
            }

            plan.TransitionTimes.Add(elapsed);
            currentOrbit = CloneOrbitForSampling(predictedOrbit);
            hasPredictedSegment = true;
        }

        if (remaining > 0d)
        {
            plan.Segments.Add(new TrajectorySegment(currentOrbit, remaining, hasPredictedSegment));
        }

        return plan;
    }

    private static List<DVector2> SamplePlanPositions(TrajectoryPlan plan, int samples, double totalDurationSeconds)
    {
        var points = new List<DVector2>();
        if (plan.Segments.Count == 0 || samples <= 0 || totalDurationSeconds <= 0d)
        {
            return points;
        }

        var dt = totalDurationSeconds / samples;
        var segIdx = 0;
        var segElapsed = 0d;

        for (int i = 0; i <= samples; i++)
        {
            var seg = plan.Segments[Math.Clamp(segIdx, 0, plan.Segments.Count - 1)];
            points.Add(seg.Orbit.PositionVectorD);

            if (i == samples)
            {
                break;
            }

            var remainingStep = dt;
            while (remainingStep > 0d && segIdx < plan.Segments.Count)
            {
                seg = plan.Segments[segIdx];
                var segRemaining = Math.Max(0d, seg.DurationSeconds - segElapsed);
                if (segRemaining <= 1e-9d)
                {
                    if (segIdx >= plan.Segments.Count - 1)
                    {
                        break;
                    }

                    segIdx++;
                    segElapsed = 0d;
                    continue;
                }

                var step = Math.Min(remainingStep, segRemaining);
                seg.Orbit.Update(step);
                segElapsed += step;
                remainingStep -= step;

                if (segElapsed >= seg.DurationSeconds - 1e-9d && segIdx < plan.Segments.Count - 1)
                {
                    segIdx++;
                    segElapsed = 0d;
                }
            }
        }

        return points;
    }

    private Vector2 ToRelativeProjectedScreen(DVector2 subjectPosition, DVector2 referencePosition, double currentReferenceAngle)
    {
        var subjectAngle = Math.Atan2(subjectPosition.Y, subjectPosition.X);
        var referenceAngle = Math.Atan2(referencePosition.Y, referencePosition.X);
        var relativeAngle = WrapAngleRadians(subjectAngle - referenceAngle);
        var displayAngle = currentReferenceAngle + relativeAngle;

        var subjectAltitude = Math.Max(0d, subjectPosition.Length() - GameState.CentralBody.Radius);
        return new Vector2(
            ProjectAngleToX(displayAngle),
            ProjectAltitudeToY(subjectAltitude));
    }

    private sealed class TrajectoryPlan
    {
        public List<TrajectorySegment> Segments { get; } = new();
        public List<double> TransitionTimes { get; } = new();
    }

    private sealed class TrajectorySegment
    {
        public Orbit Orbit { get; }
        public double DurationSeconds { get; }
        public bool IsPredicted { get; }

        public TrajectorySegment(Orbit orbit, double durationSeconds, bool isPredicted = false)
        {
            Orbit = orbit;
            DurationSeconds = Math.Max(0d, durationSeconds);
            IsPredicted = isPredicted;
        }
    }

    private static Orbit CloneOrbitForSampling(Orbit orbit)
    {
        if (orbit is null)
        {
            return null;
        }

        return new Orbit(
            orbit.Apoapsis,
            orbit.Periapsis,
            orbit.ArgumentOfPeriapsis,
            orbit.TrueAnomaly,
            orbit.ExplicitEccentricity)
        {
            PreviousTrueAnomaly = orbit.PreviousTrueAnomaly,
        };
    }

    private static double GetSingleOrbitDurationSeconds(Orbit shipOrbit, Orbit stationOrbit)
    {
        var mu = PhysicalConstants.G * GameState.CentralBody.Mass;
        if (mu <= 0d)
        {
            return 7_200d;
        }

        double GetPeriodSeconds(Orbit orbit)
        {
            if (orbit.IsEscapeTrajectory)
            {
                return 0d;
            }

            var semiMajorAxis = (orbit.Apogee + orbit.Perigee) / 2d;
            if (!double.IsFinite(semiMajorAxis) || semiMajorAxis <= 0d)
            {
                return 0d;
            }

            return MathHelper.TwoPi * Math.Sqrt((semiMajorAxis * semiMajorAxis * semiMajorAxis) / mu);
        }

        var shipPeriod = GetPeriodSeconds(shipOrbit);
        if (double.IsFinite(shipPeriod) && shipPeriod > 0d)
        {
            return Math.Clamp(shipPeriod, 3_600d, 28_800d);
        }

        var stationPeriod = GetPeriodSeconds(stationOrbit);
        if (double.IsFinite(stationPeriod) && stationPeriod > 0d)
        {
            return Math.Clamp(stationPeriod, 3_600d, 28_800d);
        }

        return 7_200d;
    }

    private static double WrapAngleRadians(double angle)
    {
        var wrapped = (angle + Math.PI) % (Math.PI * 2d);
        if (wrapped < 0d)
        {
            wrapped += Math.PI * 2d;
        }

        return wrapped - Math.PI;
    }

    private void DrawHoveredShipPreview(Ship ship)
    {
        DrawOrbit(ship.Orbit, HoverOrbitColor);

        var firstNode = ship.ManeuverNode;
        if (firstNode is null)
        {
            return;
        }

        var secondBaseOrbit = DrawManeuverNodePreview(firstNode, ship.Orbit);
        var secondNode = ship.NextManeuverNode;
        if (secondNode is not null && secondBaseOrbit is not null)
        {
            DrawManeuverNodePreview(secondNode, secondBaseOrbit);
        }
    }

    private Orbit? DrawManeuverNodePreview(ManeuverNode maneuverNode, Orbit baseOrbit)
    {
        var predictedOrbit = maneuverNode.GetPredictedOrbit(baseOrbit);
        if (predictedOrbit is not null)
        {
            DrawOrbit(predictedOrbit, HoverPredictedOrbitColor);
        }

        var nodePosition = ProjectPosition(baseOrbit.GetPositionAtAngleD(maneuverNode.TrueAnomaly));
        SpriteBatch.DrawCircle(new CircleF() { Center = nodePosition, Radius = UIConstants.NodeRadius }, 16, HoverNodeColor, UIConstants.NodeThickness);

        return predictedOrbit;
    }

    private Vector2 GetProjectedMouseWorldPosition()
    {
        var screenPos = MouseState.Position.ToVector2();
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        var drawableHeight = Math.Max(1f, height - TopBuffer - BottomBuffer);

        var wrappedPanX = width > 0f ? (_projectedPanX % width + width) % width : 0f;
        var wrappedScreenX = width > 0f ? ((screenPos.X + wrappedPanX) % width + width) % width : screenPos.X;

        var xRel = (wrappedScreenX - (width / 2f)) / (width / 2f);
        xRel = Math.Clamp(xRel, -1f, 1f);
        var angle = (double)(xRel * MathF.PI);

        var yRel = ((height - BottomBuffer) - screenPos.Y) / drawableHeight;
        yRel = Math.Clamp(yRel, 0f, 1f);
        var altitudeMeters = yRel * (float)GameState.CentralBody.ControlAltitudeMeters;
        var radius = GameState.CentralBody.Radius + altitudeMeters;

        var worldX = radius * Math.Cos(angle);
        var worldY = radius * Math.Sin(angle);

        return new Vector2(
            (float)(worldX / GameConstants.RenderingScale),
            (float)(worldY / GameConstants.RenderingScale));
    }

    private void DrawStations(List<Station> stations, Ship selectedShip)
    {
        const float size = 4f;
        foreach (var station in stations)
        {
            var position = ProjectPosition(station.Orbit.PositionVectorD);

            if (station.IsSelected)
            {
                DrawOrbit(station.Orbit, SelectedOrbitColor);
            }

            var shipTargetsStation = selectedShip?.Destination is StationDestination stationDestination
                && stationDestination.Station == station;
            var shouldDrawArrows = station.IsSelected || shipTargetsStation;

            DrawStationControlArea(station, shouldDrawArrows);

            var stationColor = station.IsSelected ? SelectedStationColor : StationColor;
            SpriteBatch.DrawCircle(position.X, position.Y, size, 36, stationColor, 1.5f);
        }
    }

    private void DrawStationControlArea(Station station, bool drawArrows)
    {
        if (station is null)
        {
            return;
        }

        var paths = BuildStationControlPaths(station);

        foreach (var path in paths)
        {
            DrawWrappedDashedPolyline(path, ControlAreaColor, 1f, dashLength: 8d, gapLength: 4d);
        }

        if (drawArrows)
        {
            DrawStationControlArrows(station);
        }
    }

    private void DrawStationControlArrows(Station station)
    {
        var stationPosition = station.Orbit.PositionVectorD;
        var orbitRadius = stationPosition.Length();
        if (orbitRadius <= 0d)
        {
            return;
        }

        var centerAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var departureAngle = Math.Min(station.ControlAreaDepartureExtentMeters / orbitRadius, Math.PI - 1e-4d);
        var arrowOffsetAngle = 1d.ToRadians();
        var motionSign = Math.Sign((stationPosition.X * station.Orbit.VelocityVectorD.Y) - (stationPosition.Y * station.Orbit.VelocityVectorD.X));
        if (motionSign == 0)
        {
            motionSign = 1;
        }

        var approachLaneCount = ControlLaneUtils.GetStationApproachLaneCount(station);
        for (int laneDepth = 1; laneDepth <= approachLaneCount; laneDepth++)
        {
            var arrivalAngle = Math.Min(ControlLaneUtils.GetStationApproachExtentMeters(station, laneDepth) / orbitRadius, Math.PI - 1e-4d);
            var upperLaneCenterRadius = orbitRadius + (laneDepth * GameConstants.ControlLaneWidthMeters);
            var lowerLaneCenterRadius = Math.Max(1d, orbitRadius - (laneDepth * GameConstants.ControlLaneWidthMeters));

            DrawOrbitChevronArrow(centerAngle + arrivalAngle + arrowOffsetAngle, upperLaneCenterRadius, motionSign, alongOrbit: false, ArrivalArrowColor);
            DrawOrbitChevronArrow(centerAngle - arrivalAngle - arrowOffsetAngle, lowerLaneCenterRadius, motionSign, alongOrbit: true, ArrivalArrowColor);

            if (laneDepth == 1)
            {
                DrawOrbitChevronArrow(centerAngle + departureAngle + arrowOffsetAngle, lowerLaneCenterRadius, motionSign, alongOrbit: true, DepartureArrowColor);
                DrawOrbitChevronArrow(centerAngle - departureAngle - arrowOffsetAngle, upperLaneCenterRadius, motionSign, alongOrbit: false, DepartureArrowColor);
            }
        }
    }

    private void DrawApsisMarkers(Orbit orbit, Color color)
    {
        var peWorldPos = ProjectPosition(orbit.GetPositionAtAngleD(0d));
        DrawApsisMarker(peWorldPos, "PE", color);

        if (!orbit.IsEscapeTrajectory)
        {
            var apWorldPos = ProjectPosition(orbit.GetPositionAtAngleD(Math.PI));
            DrawApsisMarker(apWorldPos, "AP", color);
        }
    }

    private void DrawApsisMarker(Vector2 screenPos, string label, Color color)
    {
        var markerSize = 5f;
        var thickness = 1.5f;

        SpriteBatch.DrawLine(screenPos + new Vector2(-markerSize, 0f), screenPos + new Vector2(0f, -markerSize), color, thickness);
        SpriteBatch.DrawLine(screenPos + new Vector2(0f, -markerSize), screenPos + new Vector2(markerSize, 0f), color, thickness);
        SpriteBatch.DrawLine(screenPos + new Vector2(markerSize, 0f), screenPos + new Vector2(0f, markerSize), color, thickness);
        SpriteBatch.DrawLine(screenPos + new Vector2(0f, markerSize), screenPos + new Vector2(-markerSize, 0f), color, thickness);

        var labelPos = screenPos + new Vector2(markerSize + 6f, -markerSize);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(-1f, 0f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(1f, 0f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, -1f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, 1f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos, color);
    }

    private void DrawOrbit(Orbit orbit, Color color)
    {
        if (orbit.IsEscapeTrajectory)
        {
            var maxAngle = orbit.GetHyperbolicTrueAnomalyLimit() - 0.01d;
            var start = ProjectPosition(orbit.GetPositionAtAngleD(-maxAngle));
            const int segments = 180;

            for (int i = 1; i <= segments; i++)
            {
                var angle = -maxAngle + ((2d * maxAngle) * i / segments);
                var end = ProjectPosition(orbit.GetPositionAtAngleD(angle));
                if (Math.Abs(end.X - start.X) < GraphicsDevice.Viewport.Width * 0.95f)
                {
                    SpriteBatch.DrawLine(start, end, color, 1f);
                }
                start = end;
            }

            return;
        }

        var startElliptic = ProjectPosition(orbit.GetPositionAtAngleD(0d));
        for (int i = 2; i <= 360; i += 2)
        {
            var end = ProjectPosition(orbit.GetPositionAtAngleD(((double)i).ToRadians()));
            if (Math.Abs(end.X - startElliptic.X) < GraphicsDevice.Viewport.Width * 0.95f)
            {
                SpriteBatch.DrawLine(startElliptic, end, color, 1f);
            }
            startElliptic = end;
        }
    }

    private void DrawManueverNode(ManeuverNode manueverNode, Orbit baseOrbit, bool drawButtons = true)
    {
        var predictedOrbit = manueverNode.GetPredictedOrbit(baseOrbit);
        if (predictedOrbit is not null)
        {
            var predictedColor = manueverNode.IsConfirmed ? Color.LightGreen : Color.Yellow;
            DrawOrbit(predictedOrbit, predictedColor);
        }

        var nodePos = ProjectPosition(baseOrbit.GetPositionAtAngleD(manueverNode.TrueAnomaly));
        manueverNode.ScreenPosition = nodePos.ToNumerics();

        var nodeRadius = UIConstants.NodeRadius;
        var nodeColor = manueverNode.IsConfirmed ? Color.LightGreen : Color.Yellow;
        var nodeThickness = UIConstants.NodeThickness;
        SpriteBatch.DrawCircle(new CircleF() { Center = nodePos, Radius = nodeRadius }, 16, nodeColor, nodeThickness);
        DrawManeuverDeltaVLabel(manueverNode, nodePos, nodeColor);

        if (!drawButtons)
            return;

        // Draw maneuver handles in projected screen-space
        var mousePos = MouseState.Position.ToVector2();
        var threshhold = UIConstants.NodeButtonRadius;
        var offset = UIConstants.NodeButtonOffset;
        if (Vector2.Distance(mousePos, nodePos) < threshhold + offset + manueverNode.DragOffset.Length() * MathF.Sqrt(2) && !manueverNode.IsDragged)
        {
            manueverNode.ButtonOffset = offset;
            manueverNode.ButtonRadius = UIConstants.NodeButtonRadius;
            manueverNode.ButtonThickness = UIConstants.NodeButtonThickness;

            const double tangentSample = 0.01d;
            var prevPos = ProjectPosition(baseOrbit.GetPositionAtAngleD(manueverNode.TrueAnomaly - tangentSample));
            var nextPos = ProjectPosition(baseOrbit.GetPositionAtAngleD(manueverNode.TrueAnomaly + tangentSample));
            var tangent2D = nextPos - prevPos;
            if (tangent2D.LengthSquared() < 1e-6f)
                tangent2D = new Vector2(1f, 0f);
            manueverNode.VelocityDir = Vector2.Normalize(tangent2D).ToNumerics();

            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Prograde)
                DrawButton(manueverNode.ProgradeButton, mousePos);
            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Retrograde)
                DrawButton(manueverNode.RetrogradeButton, mousePos);
            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Normal)
                DrawButton(manueverNode.NormalButton, mousePos);
            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Antinormal)
                DrawButton(manueverNode.AntinormalButton, mousePos);

            if (!manueverNode.IsConfirmed && manueverNode.DragType is ManeuverDragType.None)
                DrawButton(manueverNode.ConfirmButton, mousePos);
            if (manueverNode.DragType is ManeuverDragType.None)
                DrawButton(manueverNode.CancelButton, mousePos);
        }
    }

    private void DrawManeuverDeltaVLabel(ManeuverNode manueverNode, Vector2 nodePos, Color color)
    {
        var totalDeltaV = Math.Sqrt(
            (manueverNode.ProgradeDeltaV * manueverNode.ProgradeDeltaV)
            + (manueverNode.NormalDeltaV * manueverNode.NormalDeltaV));

        if (!double.IsFinite(totalDeltaV))
            return;

        var label = $"dV {totalDeltaV:0.0} m/s";
        var labelPos = nodePos + new Vector2(10f, -18f);

        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(-1f, 0f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(1f, 0f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, -1f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, 1f), Color.Black);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos, color);
    }

    private static Orbit GetOrbitAfterPlannedManeuvers(Ship ship)
    {
        var orbit = ship.Orbit;

        var firstNode = ship.ManeuverNode;
        if (firstNode is not null)
        {
            var predictedFirst = firstNode.GetPredictedOrbit(orbit);
            if (predictedFirst is not null)
                orbit = predictedFirst;
        }

        var secondNode = ship.NextManeuverNode;
        if (secondNode is not null)
        {
            var predictedSecond = secondNode.GetPredictedOrbit(orbit);
            if (predictedSecond is not null)
                orbit = predictedSecond;
        }

        return orbit;
    }

    private void DrawButton(Button button, Vector2 mousePos)
    {
        var pos = button.Position;
        var radius = button.Radius;
        var color = button.Color;
        var thickness = button.Thickness;
        var hoverOffset = Vector2.Distance(pos, mousePos) < radius ? new Vector2(-1, -1) : Vector2.Zero;
        SpriteBatch.DrawCircle(new CircleF() { Center = pos + hoverOffset, Radius = radius }, 16, color, thickness);
        DrawButtonLabel(button, mousePos);
    }

    private void DrawButtonLabel(Button button, Vector2 mousePos)
    {
        var pos = button.Position;
        var radius = button.Radius;
        var color = button.Color;
        var thickness = button.Thickness;
        var hoverOffset = Vector2.Distance(pos, mousePos) < radius ? new Vector2(-1, -1) : Vector2.Zero;

        void DrawIconLine(Vector2 startOffset, Vector2 endOffset)
        {
            var start = pos + startOffset + hoverOffset;
            var end = pos + endOffset + hoverOffset;
            SpriteBatch.DrawLine(start, end, color, thickness);
        }

        switch (button.Label)
        {
            case ButtonLabel.Plus:
                DrawIconLine(new Vector2(0f, radius / 2f), new Vector2(0f, -radius / 2f));
                DrawIconLine(new Vector2(radius / 2f, 0f), new Vector2(-radius / 2f, 0f));
                break;
            case ButtonLabel.Minus:
                DrawIconLine(new Vector2(radius / 2f, 0f), new Vector2(-radius / 2f, 0f));
                break;
            case ButtonLabel.V:
                DrawIconLine(new Vector2(-radius / 2f, 0f), new Vector2(0f, -radius / 2f));
                DrawIconLine(new Vector2(radius / 2f, 0f), new Vector2(0f, -radius / 2f));
                break;
        }
    }



    private void DrawWrappedDashedPolyline(IReadOnlyList<Vector2> points, Color color, float thickness, double dashLength, double gapLength)
    {
        if (points.Count < 2)
            return;

        var width = GraphicsDevice.Viewport.Width;
        if (width <= 0)
        {
            DrawDashedPolyline(points, color, thickness, dashLength, gapLength);
            return;
        }

        // unwrap X to a continuous path so segments at the seam do not draw across the full screen
        var unwrapped = new List<Vector2>(points.Count) { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            var prev = unwrapped[i - 1];
            var curr = points[i];
            var x = curr.X;

            while ((x - prev.X) > (width * 0.5f)) x -= width;
            while ((x - prev.X) < -(width * 0.5f)) x += width;

            unwrapped.Add(new Vector2(x, curr.Y));
        }

        // draw 3 wrapped copies so whichever one is in viewport is visible
        for (int k = -1; k <= 1; k++)
        {
            var shift = k * width;
            var shifted = new List<Vector2>(unwrapped.Count);
            for (int i = 0; i < unwrapped.Count; i++)
            {
                shifted.Add(new Vector2(unwrapped[i].X + shift, unwrapped[i].Y));
            }

            DrawDashedPolyline(shifted, color, thickness, dashLength, gapLength);
        }
    }



    private void DrawOrbitChevronArrow(double angle, double radius, double motionSign, bool alongOrbit, Color color)
    {
        var centerD = MathUtils.PolarToCartesian(angle, radius);
        var center = ProjectPosition(centerD);

        // projected radial direction from small radius differential
        const double radiusSample = 1000d;
        var rPrev = Math.Max(1d, radius - radiusSample);
        var rNext = radius + radiusSample;
        var radial = ProjectPosition(MathUtils.PolarToCartesian(angle, rNext)) - ProjectPosition(MathUtils.PolarToCartesian(angle, rPrev));
        if (radial.LengthSquared() <= 1e-6f)
        {
            radial = new Vector2(0f, -1f);
        }
        radial.Normalize();

        // projected along-orbit direction from small angular differential
        const double angleSample = 0.01d;
        var tPrev = ProjectPosition(MathUtils.PolarToCartesian(angle - angleSample, radius));
        var tNext = ProjectPosition(MathUtils.PolarToCartesian(angle + angleSample, radius));
        var tangent = tNext - tPrev;
        if (tangent.LengthSquared() <= 1e-6f)
        {
            tangent = Vector2.UnitX;
        }
        tangent.Normalize();

        var tangentialSign = (float)(Math.Sign(motionSign) * (alongOrbit ? 1d : -1d));
        if (tangentialSign == 0f)
        {
            tangentialSign = 1f;
        }
        tangent *= tangentialSign;

        // match default-view arrow style
        var headLength = 9f;
        var headWidth = 4.5f;
        var tip = center + (tangent * (headLength * 0.5f));
        var tailCenter = center - (tangent * (headLength * 0.5f));
        var wingOffset = radial * headWidth;
        var thickness = 1.5f;
        var arrowColor = color * 0.5f;

        SpriteBatch.DrawLine(tip, tailCenter + wingOffset, arrowColor, thickness);
        SpriteBatch.DrawLine(tip, tailCenter - wingOffset, arrowColor, thickness);
    }

    private float GetSurfaceY() => GraphicsDevice.Viewport.Height - BottomBuffer;

    private float ProjectAltitudeToY(double altitudeMeters)
    {
        var h = GraphicsDevice.Viewport.Height;
        var drawableHeight = Math.Max(1f, h - TopBuffer - BottomBuffer);
        var t = (float)Math.Clamp(altitudeMeters / Math.Max(1d, GameState.CentralBody.ControlAltitudeMeters), 0d, 1d);
        return (h - BottomBuffer) - (t * drawableHeight);
    }

    private float ProjectAngleToX(double angleRadians)
    {
        var wrapped = MathHelper.WrapAngle((float)angleRadians);
        var w = GraphicsDevice.Viewport.Width;
        var baseX = (w / 2f) + (wrapped / MathF.PI) * (w / 2f);

        if (w <= 0)
        {
            return baseX;
        }

        // Horizontal projected-view camera pan in pixels; wrap into viewport width.
        var wrappedPanX = (_projectedPanX % w + w) % w;
        var x = baseX - wrappedPanX;
        x = (x % w + w) % w;
        return x;
    }

    private Vector2 ProjectPosition(DVector2 position)
    {
        var angle = Math.Atan2(position.Y, position.X);
        var radius = position.Length();
        var altitude = Math.Max(0d, radius - GameState.CentralBody.Radius);

        return new Vector2(
            ProjectAngleToX(angle),
            ProjectAltitudeToY(altitude));
    }

    protected override Vector2 ProjectPolarPoint(double radius, double angle)
    {
        return ProjectPosition(MathUtils.PolarToCartesian(angle, radius));
    }

    private float ProjectDistanceToPixels(double meters, double radiusMeters)
    {
        // Y-axis scale (altitude)
        var drawableHeight = Math.Max(1f, GraphicsDevice.Viewport.Height - TopBuffer - BottomBuffer);
        var pixelsPerMeterY = drawableHeight / Math.Max(1d, GameState.CentralBody.ControlAltitudeMeters);

        // X-axis local scale (arc-length around body at current radius)
        var pixelsPerMeterX = GraphicsDevice.Viewport.Width / Math.Max(1d, MathHelper.TwoPi * radiusMeters);

        // Blend X and Y scales for a better visual match in this non-uniform projection.
        var pixelsPerMeter = (pixelsPerMeterX + pixelsPerMeterY) * 0.5d;
        return Math.Max(1f, (float)(meters * pixelsPerMeter));
    }

    private void DrawProjectedSeparationEllipse(Vector2 center, double worldRadiusMeters, double atRadiusMeters, Color color, float thickness)
    {
        // In projected view, equal world offsets in tangential/radial directions map to different pixel scales.
        var w = GraphicsDevice.Viewport.Width;
        var drawableHeight = Math.Max(1f, GraphicsDevice.Viewport.Height - TopBuffer - BottomBuffer);
        var pixelsPerMeterY = drawableHeight / Math.Max(1d, GameState.CentralBody.ControlAltitudeMeters);
        var pixelsPerMeterX = w / Math.Max(1d, MathHelper.TwoPi * atRadiusMeters);

        var rx = Math.Max(1f, (float)(worldRadiusMeters * pixelsPerMeterX));
        var ry = Math.Max(1f, (float)(worldRadiusMeters * pixelsPerMeterY));

        const int segments = 48;
        var prev = center + new Vector2(rx, 0f);
        for (int i = 1; i <= segments; i++)
        {
            var t = (MathHelper.TwoPi * i) / segments;
            var p = center + new Vector2(rx * MathF.Cos(t), ry * MathF.Sin(t));
            SpriteBatch.DrawLine(prev, p, color, thickness);
            prev = p;
        }
    }

}
