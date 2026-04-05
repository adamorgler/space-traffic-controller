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

        DrawBackground(body);
        DrawBodyAndReferenceLines(body);

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

        var selectedShip = gameState.SelectedShip;
        var target = gameState.TargetOrbitingObject;
        if (selectedShip is not null && target is not null)
        {
            var targetOrbit = target.Orbit;
            DrawOrbit(targetOrbit, TargetOrbitColor);
            DrawApsisMarkers(targetOrbit, TargetApsisColor);

            Orbit shipOrbitForApproach = GetOrbitAfterPlannedManeuvers(selectedShip);

            Orbit targetOrbitForApproach = targetOrbit;
            if (target is Ship targetShip)
            {
                targetOrbitForApproach = GetOrbitAfterPlannedManeuvers(targetShip);
            }

            DrawClosestApproach(shipOrbitForApproach, targetOrbitForApproach, selectedShip, target);
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

    private void DrawBodyAndReferenceLines(CelestialBody body)
    {
        var w = GraphicsDevice.Viewport.Width;
        var h = GraphicsDevice.Viewport.Height;
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

        // longitude ticks every 45°
        for (int deg = -180; deg <= 180; deg += 45)
        {
            var x = ProjectAngleToX(MathHelper.ToRadians(deg));
            var isMajor = deg % 90 == 0;
            SpriteBatch.DrawLine(
                new Vector2(x, surfaceY - (isMajor ? 10f : 6f)),
                new Vector2(x, surfaceY),
                isMajor ? Color.White * 0.65f : Color.Gray * 0.55f,
                isMajor ? 1.5f : 1f);
        }
    }

    private void DrawShips(List<Ship> ships, GameState gameState)
    {
        const float size = 5f;
        foreach (var ship in ships)
        {
            var position = ProjectPosition(ship.Orbit.PositionVectorD);

            if (ship.IsSelected)
            {
                DrawOrbit(ship.Orbit, SelectedOrbitColor);
                DrawApsisMarkers(ship.Orbit, SelectedOrbitColor);

                if (ship.ManeuverNode is null)
                {
                    DrawOrbitMouseIntersection(ship.Orbit);
                }
                else
                {
                    var firstNode = ship.ManeuverNode;
                    DrawManueverNode(firstNode, ship.Orbit, drawButtons: ship.NextManeuverNode is null);

                    var secondBaseOrbit = firstNode.GetPredictedOrbit(ship.Orbit);
                    if (ship.NextManeuverNode is null && firstNode.IsConfirmed && secondBaseOrbit is not null)
                    {
                        DrawOrbitMouseIntersection(secondBaseOrbit);
                    }

                    if (ship.NextManeuverNode is not null && secondBaseOrbit is not null)
                    {
                        DrawManueverNode(ship.NextManeuverNode, secondBaseOrbit, drawButtons: true);
                    }
                }

                var destinationOrbit = GetDestinationOrbit(ship);
                if (destinationOrbit is not null)
                {
                    DrawOrbit(destinationOrbit, TargetOrbitColor);
                    DrawApsisMarkers(destinationOrbit, TargetApsisColor);

                    var orbitForApproach = GetOrbitAfterPlannedManeuvers(ship);

                    DrawClosestApproach(orbitForApproach, destinationOrbit);
                }
            }

            var shipColor = !ship.Status.IsControllable
                ? UncontrolledShipColor
                : ship.IsSelected ? SelectedShipColor : ActiveShipColor;

            var separationColor = !ship.Status.IsControllable
                ? UncontrolledShipColor
                : ship.Status.IsEncroached ? EncroachedSeparationColor : SafeSeparationColor;

            SpriteBatch.DrawRectangle(position.X - (size / 2f), position.Y - (size / 2f), size, size, shipColor, 1.5f);

            DrawProjectedSeparationEllipse(
                center: position,
                worldRadiusMeters: GameConstants.ShipSepration / 2d,
                atRadiusMeters: ship.Orbit.PositionVectorD.Length(),
                color: separationColor,
                thickness: 1.2f);
        }
    }

    private void DrawOrbitMouseIntersection(Orbit orbit)
    {
        var mousePos = GetProjectedMouseWorldPosition();
        var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(orbit, mousePos.ToNumerics());
        if (orbitPos is null)
            return;

        var intersectionPos = ProjectPosition(orbit.GetPositionAtAngleD(orbitPos.TrueAnomaly));
        SpriteBatch.DrawCircle(new CircleF() { Center = intersectionPos, Radius = 5f }, 12, Color.LightGray);
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
        var arrivalExtent = station.ControlAreaArrivalExtentMeters;
        var departureExtent = station.ControlAreaDepartureExtentMeters;
        var halfAltitude = station.ControlAreaHalfAltitudeMeters;
        if ((arrivalExtent <= 0d && departureExtent <= 0d) || halfAltitude <= 0d)
        {
            return;
        }

        var paths = BuildStationControlPaths(
            stationPosition: station.Orbit.PositionVectorD,
            arrivalExtent: arrivalExtent,
            departureExtent: departureExtent,
            halfAltitude: halfAltitude);

        foreach (var path in paths)
        {
            DrawWrappedDashedPolyline(path, ControlAreaColor, 1f, dashLength: 8d, gapLength: 4d);
        }

        if (drawArrows)
        {
            DrawStationControlArrows(station, arrivalExtent, departureExtent, halfAltitude);
        }
    }

    private void DrawStationControlArrows(Station station, double arrivalExtent, double departureExtent, double halfAltitude)
    {
        var stationPosition = station.Orbit.PositionVectorD;
        var orbitRadius = stationPosition.Length();
        if (orbitRadius <= 0d)
        {
            return;
        }

        var centerAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var arrivalAngle = Math.Min(arrivalExtent / orbitRadius, Math.PI - 1e-4d);
        var departureAngle = Math.Min(departureExtent / orbitRadius, Math.PI - 1e-4d);
        var outerRadius = orbitRadius + halfAltitude / 2;
        var innerRadius = Math.Max(1d, orbitRadius - halfAltitude / 2);
        var arrowOffsetAngle = 1d.ToRadians();
        var motionSign = Math.Sign((stationPosition.X * station.Orbit.VelocityVectorD.Y) - (stationPosition.Y * station.Orbit.VelocityVectorD.X));
        if (motionSign == 0)
        {
            motionSign = 1;
        }

        DrawOrbitChevronArrow(centerAngle + arrivalAngle + arrowOffsetAngle, outerRadius, motionSign, alongOrbit: false, ArrivalArrowColor);
        DrawOrbitChevronArrow(centerAngle - arrivalAngle - arrowOffsetAngle, innerRadius, motionSign, alongOrbit: true, ArrivalArrowColor);
        DrawOrbitChevronArrow(centerAngle + departureAngle + arrowOffsetAngle, innerRadius, motionSign, alongOrbit: true, DepartureArrowColor);
        DrawOrbitChevronArrow(centerAngle - departureAngle - arrowOffsetAngle, outerRadius, motionSign, alongOrbit: false, DepartureArrowColor);
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

    private void DrawClosestApproach(Orbit shipOrbit, Orbit destOrbit, HasOrbit? shipObj = null, HasOrbit? destObj = null)
    {
        var (bestShipAngle, bestDestAngle) = FindClosestApproachAngles(shipOrbit, destOrbit, ClosestApproachCoarseSamples);
        (bestShipAngle, bestDestAngle) = RefineClosestApproach(
            shipOrbit, destOrbit,
            bestShipAngle, bestDestAngle,
            ClosestApproachFineWindow, ClosestApproachFineSamples);

        var (secondShipAngle, secondDestAngle, secondDistSq) = FindClosestApproachAnglesWithExclusion(
            shipOrbit, destOrbit, ClosestApproachCoarseSamples, bestShipAngle, ClosestApproachExclusionWindow);

        var approaches = new List<(double shipAngle, double destAngle)> { (bestShipAngle, bestDestAngle) };
        if (double.IsFinite(secondDistSq) && secondDistSq < double.MaxValue)
        {
            (secondShipAngle, secondDestAngle) = RefineClosestApproach(
                shipOrbit, destOrbit,
                secondShipAngle, secondDestAngle,
                ClosestApproachFineWindow, ClosestApproachFineSamples);

            var angSep = AngularDistance(bestShipAngle, secondShipAngle);
            if (angSep > 0.05d)
            {
                approaches.Add((secondShipAngle, secondDestAngle));
            }
        }

        for (int idx = 0; idx < approaches.Count; idx++)
        {
            var (sa, da) = approaches[idx];
            var approachColor = ClosestApproachColors[Math.Min(idx, ClosestApproachColors.Length - 1)];

            var shipPosD = shipOrbit.GetPositionAtAngleD(sa);
            var destPosD = destOrbit.GetPositionAtAngleD(da);
            var dx = shipPosD.X - destPosD.X;
            var dy = shipPosD.Y - destPosD.Y;
            var approachDistanceMeters = Math.Sqrt((dx * dx) + (dy * dy));

            if (approachDistanceMeters > ClosestApproachLineThresholdMeters)
            {
                var shipScreen = ProjectPosition(shipPosD);
                var destScreen = ProjectPosition(destPosD);
                DrawDashedLine(shipScreen, destScreen, ClosestApproachDashColor, 1.2f, 6f, 4f);
            }

            DrawInboundChevronAtOrbitPoint(shipOrbit, sa, approachColor);
            DrawInboundChevronAtOrbitPoint(destOrbit, da, approachColor);

            try
            {
                var timeToShip = shipOrbit.TimeToTrueAomaly(sa);
                if (!double.IsInfinity(timeToShip) && !double.IsNaN(timeToShip))
                {
                    var predictedDestAngle = TrueAnomalyAfterTime(destOrbit, timeToShip);
                    DrawInboundChevronAtOrbitPoint(destOrbit, predictedDestAngle, approachColor);

                    if (shipObj is Ship && destObj is Ship)
                    {
                        try
                        {
                            var shipPredictedD = shipOrbit.GetPositionAtAngleD(sa);
                            var destPredictedD = destOrbit.GetPositionAtAngleD(predictedDestAngle);
                            var distMeters = Math.Sqrt(Math.Pow(shipPredictedD.X - destPredictedD.X, 2) + Math.Pow(shipPredictedD.Y - destPredictedD.Y, 2));

                            var shipCenter = ProjectPosition(shipPredictedD);
                            var destCenter = ProjectPosition(destPredictedD);
                            var safe = distMeters >= GameConstants.ShipSepration;
                            var col = safe ? ActiveShipColor : EncroachedSeparationColor;

                            DrawProjectedSeparationEllipse(
                                center: shipCenter,
                                worldRadiusMeters: GameConstants.ShipSepration / 2d,
                                atRadiusMeters: Math.Max(1d, shipPredictedD.Length()),
                                color: col,
                                thickness: 1.5f);

                            DrawProjectedSeparationEllipse(
                                center: destCenter,
                                worldRadiusMeters: GameConstants.ShipSepration / 2d,
                                atRadiusMeters: Math.Max(1d, destPredictedD.Length()),
                                color: col,
                                thickness: 1.5f);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }

    private void DrawInboundChevronAtOrbitPoint(Orbit orbit, double trueAnomaly, Color color)
    {
        var pos = ProjectPosition(orbit.GetPositionAtAngleD(trueAnomaly));

        const double tangentSample = 0.01d;
        var prev = ProjectPosition(orbit.GetPositionAtAngleD(trueAnomaly - tangentSample));
        var next = ProjectPosition(orbit.GetPositionAtAngleD(trueAnomaly + tangentSample));
        var tangent = next - prev;
        if (tangent.LengthSquared() <= 1e-6f)
        {
            tangent = Vector2.UnitX;
        }
        tangent.Normalize();

        var inward = Vector2.UnitY;
        var headLength = 10f;
        var headWidth = 6f;
        var tip = pos;
        // keep tip on the orbit, but orient chevron toward the planet (downward in projected view)
        var tailCenter = pos - inward * headLength;
        var wingOffset = tangent * headWidth;

        SpriteBatch.DrawLine(tip, tailCenter + wingOffset, color, 1.8f);
        SpriteBatch.DrawLine(tip, tailCenter - wingOffset, color, 1.8f);
    }

    private static (double shipAngle, double destAngle) FindClosestApproachAngles(Orbit shipOrbit, Orbit destOrbit, int samples)
    {
        var bestDistSq = double.MaxValue;
        var bestShip = 0d;
        var bestDest = 0d;

        var shipMax = shipOrbit.IsEscapeTrajectory ? shipOrbit.GetHyperbolicTrueAnomalyLimit() - 0.01d : Math.PI;
        var destMax = destOrbit.IsEscapeTrajectory ? destOrbit.GetHyperbolicTrueAnomalyLimit() - 0.01d : Math.PI;

        for (int i = 0; i <= samples; i++)
        {
            var a = -shipMax + (2d * shipMax * i / samples);
            var pA = shipOrbit.GetPositionAtAngleD(a);
            for (int j = 0; j <= samples; j++)
            {
                var b = -destMax + (2d * destMax * j / samples);
                var pB = destOrbit.GetPositionAtAngleD(b);
                var dx = pA.X - pB.X;
                var dy = pA.Y - pB.Y;
                var d2 = dx * dx + dy * dy;
                if (d2 < bestDistSq)
                {
                    bestDistSq = d2;
                    bestShip = a;
                    bestDest = b;
                }
            }
        }

        return (bestShip, bestDest);
    }

    private static double AngularDistance(double a, double b)
    {
        var diff = (a - b + Math.PI) % (2d * Math.PI) - Math.PI;
        return Math.Abs(diff);
    }

    private static (double shipAngle, double destAngle, double distSq) FindClosestApproachAnglesWithExclusion(
        Orbit shipOrbit, Orbit destOrbit, int sampleCount, double excludeShipAngle, double excludeHalfWidth)
    {
        double bestDistSq = double.MaxValue;
        double bestShipAngle = 0d;
        double bestDestAngle = 0d;

        var shipAngles = SampleOrbitAngles(shipOrbit, sampleCount);
        var destAngles = SampleOrbitAngles(destOrbit, sampleCount);

        var shipPositions = shipAngles.Select(a => shipOrbit.GetPositionAtAngle(a)).ToList();
        var destPositions = destAngles.Select(a => destOrbit.GetPositionAtAngle(a)).ToList();

        for (int i = 0; i < sampleCount; i++)
        {
            var sa = shipAngles[i];
            if (AngularDistance(sa, excludeShipAngle) < excludeHalfWidth)
                continue;

            var sp = shipPositions[i];
            for (int j = 0; j < sampleCount; j++)
            {
                var dp = destPositions[j];
                var dx = sp.X - dp.X;
                var dy = sp.Y - dp.Y;
                var distSq = (dx * dx) + (dy * dy);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestShipAngle = sa;
                    bestDestAngle = destAngles[j];
                }
            }
        }

        return (bestShipAngle, bestDestAngle, bestDistSq);
    }

    private static (double shipAngle, double destAngle) RefineClosestApproach(
        Orbit shipOrbit, Orbit destOrbit,
        double coarseShipAngle, double coarseDestAngle,
        double window, int sampleCount)
    {
        double bestDistSq = double.MaxValue;
        double bestShipAngle = coarseShipAngle;
        double bestDestAngle = coarseDestAngle;

        var shipAngles = SampleOrbitAnglesAround(shipOrbit, coarseShipAngle, window, sampleCount);
        var destAngles = SampleOrbitAnglesAround(destOrbit, coarseDestAngle, window, sampleCount);

        foreach (var sa in shipAngles)
        {
            var sp = shipOrbit.GetPositionAtAngle(sa);
            foreach (var da in destAngles)
            {
                var dp = destOrbit.GetPositionAtAngle(da);
                var dx = sp.X - dp.X;
                var dy = sp.Y - dp.Y;
                var distSq = (dx * dx) + (dy * dy);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestShipAngle = sa;
                    bestDestAngle = da;
                }
            }
        }

        return (bestShipAngle, bestDestAngle);
    }

    private static IReadOnlyList<double> SampleOrbitAngles(Orbit orbit, int count)
    {
        var angles = new List<double>(count);
        if (orbit.IsEscapeTrajectory)
        {
            var limit = orbit.GetHyperbolicTrueAnomalyLimit() - 0.01d;
            for (int i = 0; i < count; i++)
            {
                angles.Add(-limit + (2d * limit * i / (count - 1)));
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                angles.Add(2d * Math.PI * i / count);
            }
        }
        return angles;
    }

    private static IReadOnlyList<double> SampleOrbitAnglesAround(Orbit orbit, double center, double window, int count)
    {
        var angles = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            var angle = center - window + (2d * window * i / (count - 1));
            if (!orbit.IsEscapeTrajectory)
            {
                angle = ((angle % (2d * Math.PI)) + (2d * Math.PI)) % (2d * Math.PI);
            }
            else
            {
                var limit = orbit.GetHyperbolicTrueAnomalyLimit() - 0.01d;
                angle = Math.Clamp(angle, -limit, limit);
            }
            angles.Add(angle);
        }
        return angles;
    }

    private static double TrueAnomalyAfterTime(Orbit orbit, double deltaT)
    {
        if (orbit.IsEscapeTrajectory)
        {
            return orbit.TrueAnomaly;
        }

        double mu = PhysicalConstants.G * GameState.CentralBody.Mass;
        double a = (orbit.Apoapsis + orbit.Periapsis + (2d * GameState.CentralBody.Radius)) / 2d;
        double n = Math.Sqrt(mu / Math.Pow(a, 3d));

        double e = (orbit.Apoapsis - orbit.Periapsis) / (orbit.Apoapsis + orbit.Periapsis + (2d * GameState.CentralBody.Radius));

        double f0 = orbit.TrueAnomaly;
        double E0;
        if (Math.Abs(e) < 1e-12)
        {
            E0 = f0;
        }
        else
        {
            E0 = 2d * Math.Atan(Math.Sqrt((1d - e) / (1d + e)) * Math.Tan(f0 / 2d));
        }

        double M0 = E0 - (e * Math.Sin(E0));
        double Mtarget = M0 + n * deltaT;
        Mtarget = ((Mtarget % (2d * Math.PI)) + (2d * Math.PI)) % (2d * Math.PI);

        double E = Mtarget;
        for (int i = 0; i < 60; i++)
        {
            double f = E - e * Math.Sin(E) - Mtarget;
            double fp = 1d - e * Math.Cos(E);
            if (Math.Abs(fp) < 1e-12) break;
            double dE = f / fp;
            E -= dE;
            if (Math.Abs(dE) < 1e-12) break;
        }

        double ft;
        if (Math.Abs(e) < 1e-12)
        {
            ft = E;
        }
        else
        {
            ft = 2d * Math.Atan(Math.Sqrt((1d + e) / (1d - e)) * Math.Tan(E / 2d));
        }

        if (ft < 0d) ft += 2d * Math.PI;
        return ft;
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
