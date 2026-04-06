using MenuBuddy;
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

public class SimulationRenderer : SimulationRendererBase
{
    private readonly BasicEffect BasicEffect;

    public SimulationRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Camera2D camera)
        : base(graphicsDevice, spriteBatch, camera)
    {
        BasicEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            Projection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1),
            View = Matrix.Identity,
            World = Matrix.Identity,
        };
    }

    public void DrawWorld(GameState gameState)
    {
        MouseState = Mouse.GetState();

        BasicEffect.View = Camera.GetTransform();

        DrawBody(gameState);

        // default orbit color for the global/maneuver displays
        var orbitDefaultColor = OrbitDefaultColor;

        // Global orbit visibility (top-right toggle)
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
                    // ignore any drawing errors per-object
                }
            }
        }

        // Show orbits for any craft with an accepted maneuver node and their predicted orbit
        if (gameState.ShowAllManeuvers)
        {
            foreach (var ship in gameState.Ships)
            {
                var firstNode = ship.ManeuverNode;
                if (firstNode is null || !firstNode.IsConfirmed) continue;
                try
                {
                    // draw the current orbit faintly and then use DrawManueverNode
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
                    // swallow per-ship draw errors
                }
            }
        }

        DrawStations(gameState.Stations, gameState.SelectedShip);
        DrawShips(gameState.Ships, gameState);

        var hoveredShip = GetHoveredShip(gameState.Ships);
        if (hoveredShip is not null && !hoveredShip.IsSelected)
        {
            DrawHoveredShipPreview(hoveredShip);
        }

        // if a ship is selected and the user has right-clicked another orbiting object,
        // render that target orbit
        var selectedShip = gameState.SelectedShip;
        var target = gameState.TargetOrbitingObject;
        if (selectedShip is not null && target is not null)
        {
            var targetOrbit = target.Orbit;
            DrawOrbit(targetOrbit, TargetOrbitColor);
            DrawApsisMarkers(targetOrbit, TargetApsisColor);
        }
    }

    private void DrawBody(GameState gameState)
    {
        var body = GameState.CentralBody;
        int radius = (int)(body.Radius / Scale);
        SpriteBatch.DrawCircle(new Vector2(0, 0), radius, 360, Color.Wheat, radius);
        DrawPlanetLongitudeLines(body);
        DrawPlanetOutline(body);
        DrawAtmosphere(body);
        DrawControlAltitude(body, gameState.ShowControlAreaLanes);
    }

    private void DrawPlanetLongitudeLines(CelestialBody body)
    {
        var radius = (float)(body.Radius / Scale);
        var thickness = 2.5f / Camera.Zoom;
        const int longitudeCount = 8;

        for (int i = 0; i < longitudeCount; i++)
        {
            var angle = MathHelper.TwoPi * (i / (float)longitudeCount);
            var end = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var color = i == 0 ? Color.Red : Color.Black * 0.9f;
            SpriteBatch.DrawLine(Vector2.Zero, end, color, thickness);
        }
    }

    private void DrawPlanetOutline(CelestialBody body)
    {
        var radius = (float)(body.Radius / Scale);
        SpriteBatch.DrawCircle(Vector2.Zero, radius, 96, Color.Black * 0.9f, 3f / Camera.Zoom);
    }

    private void DrawControlAltitude(CelestialBody body, bool showControlAreaLanes)
    {
        var radius = (float)((body.Radius + body.ControlAltitudeMeters) / Scale);
        var atmosphereTopAltitude = body.AtmosphereLayers.Count > 0
            ? body.AtmosphereLayers.Max(layer => layer.Altitude + layer.Thickness)
            : 0d;
        var dashDeg = 2d;
        var gapDeg = 1d;
        for (double angle = 0d; angle < 360d; angle += dashDeg + gapDeg)
        {
            var start = (angle).ToRadians();
            var end = (angle + dashDeg).ToRadians();
            var p1 = new Vector2((float)(Math.Cos(start) * radius), (float)(Math.Sin(start) * radius));
            var p2 = new Vector2((float)(Math.Cos(end) * radius), (float)(Math.Sin(end) * radius));
            SpriteBatch.DrawLine(p1, p2, Color.LightGray, 1f / Camera.Zoom);
        }

        if (showControlAreaLanes)
        {
            foreach (var edgeAltitude in EnumeratePlanetControlLaneEdgeAltitudes(body.ControlAltitudeMeters, atmosphereTopAltitude))
            {
                var laneRadius = (float)((body.Radius + edgeAltitude) / Scale);
                for (double angle = 0d; angle < 360d; angle += dashDeg + gapDeg)
                {
                    var start = (angle).ToRadians();
                    var end = (angle + dashDeg).ToRadians();
                    var p1 = new Vector2((float)(Math.Cos(start) * laneRadius), (float)(Math.Sin(start) * laneRadius));
                    var p2 = new Vector2((float)(Math.Cos(end) * laneRadius), (float)(Math.Sin(end) * laneRadius));
                    SpriteBatch.DrawLine(p1, p2, ControlAreaLaneColor, 1f / Camera.Zoom);
                }
            }
        }
    }

    private void DrawAtmosphere(CelestialBody body)
    {
        var layers = body.AtmosphereLayers;
        var baseDensity = body.BaseAtmosphereDensity;
        var baseColor = Color.SkyBlue;
        foreach (var layer in layers)
        {
            var thickness = (float)(layer.Thickness / Scale);
            var radius = (float)(((layer.Altitude + body.Radius) / Scale) - 1d);
            var alpha = (float)(layer.Density / baseDensity);
            var color = new Color(baseColor.R, baseColor.G, baseColor.B) * alpha;
            GraphicsDevice.DrawRing(new Vector2(0, 0), radius, radius + thickness, 180, color, BasicEffect);
        }
    }

    private void DrawShips(List<Ship> ships, GameState gameState)
    {
        int size = 5;
        foreach (Ship ship in ships)
        {
            Vector2 position = ship.Orbit.PositionVector / Scale;

            // ship selected
            if (ship.IsSelected)
            {
                var orbit = ship.Orbit;
                DrawOrbit(orbit, SelectedOrbitColor);
                DrawApsisMarkers(orbit, SelectedOrbitColor);

                if (ship.ManeuverNode is not null)
                {
                    // Normal maneuver node rendering (also used during Hohmann dialog)
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
                else if (ship.ManeuverNode is null)
                {
                    DrawOrbitMouseIntersection(orbit);
                }

                // draw destination orbit
                var destinationOrbit = GetDestinationOrbit(ship);
                if (destinationOrbit is not null)
                {
                    DrawOrbit(destinationOrbit, TargetOrbitColor);
                    DrawApsisMarkers(destinationOrbit, TargetApsisColor);
                }
            }

            // ship square: uncontrolled ships render as light gray
            Color shipColor;
            Color seperationCircleColor;
            if (!ship.Status.IsControllable)
            {
                shipColor = UncontrolledShipColor;
                seperationCircleColor = UncontrolledShipColor;
            }
            else
            {
                shipColor = ship.IsSelected
                    ? SelectedShipColor
                    : ship.Destination is ExitControlAreaDestination
                        ? ExitDestinationShipColor
                        : ActiveShipColor;
                seperationCircleColor = ship.Status.IsEncroached ? EncroachedSeparationColor : SafeSeparationColor;

            }
            SpriteBatch.DrawRectangle(position.X - (size / 2), position.Y - (size / 2), size, size, shipColor, 1.5f);

            if (ShouldDrawActivationFlash(ship))
            {
                var flashRadius = 10f / Camera.Zoom;
                var flashThickness = 2f / Camera.Zoom;
                SpriteBatch.DrawCircle(new CircleF() { Center = position, Radius = flashRadius }, 24, ActivationFlashColor, flashThickness);
            }

            var nodeCount = (ship.ManeuverNode is not null ? 1 : 0) + (ship.NextManeuverNode is not null ? 1 : 0);
            if (nodeCount > 0)
            {
                var markerFont = Fonts.ManueverNode ?? Fonts.DebugFont;
                var markerScale = ManeuverIndicatorScale;
                var markerLabel = ManeuverIndicatorLabel;
                var textSize = markerFont.MeasureString(markerLabel) * markerScale;
                var separationRadius = (float)(GameConstants.ControlLaneLongitudinalHalfExtentMeters / Scale);
                var markerPadding = ManeuverIndicatorPadding;
                var markerStartPos = position + new Vector2(
                    separationRadius + markerPadding,
                    -(separationRadius + markerPadding + textSize.Y));

                var markerCircleRadius = MathF.Max(textSize.X, textSize.Y) * ManeuverIndicatorCircleRadiusFactor + ManeuverIndicatorCircleRadiusOffset;
                var markerStepX = (markerCircleRadius * 2f) + ManeuverIndicatorSpacing;

                var markerIndex = 0;
                if (ship.ManeuverNode is not null)
                {
                    var markerColor = ship.ManeuverNode.IsConfirmed ? ManeuverIndicatorConfirmedColor : ManeuverIndicatorPendingColor;
                    var markerPos = markerStartPos + new Vector2(markerStepX * markerIndex, 0f);
                    var markerCenter = markerPos + (textSize * 0.5f);
                    SpriteBatch.DrawCircle(new CircleF() { Center = markerCenter, Radius = markerCircleRadius }, 20, markerColor, 1f / Camera.Zoom);
                    SpriteBatch.DrawString(markerFont, markerLabel, markerPos, markerColor, 0f, Vector2.Zero, markerScale, SpriteEffects.None, 0f);
                    markerIndex++;
                }

                if (ship.NextManeuverNode is not null)
                {
                    var markerColor = ship.NextManeuverNode.IsConfirmed ? ManeuverIndicatorConfirmedColor : ManeuverIndicatorPendingColor;
                    var markerPos = markerStartPos + new Vector2(markerStepX * markerIndex, 0f);
                    var markerCenter = markerPos + (textSize * 0.5f);
                    SpriteBatch.DrawCircle(new CircleF() { Center = markerCenter, Radius = markerCircleRadius }, 20, markerColor, 1f / Camera.Zoom);
                    SpriteBatch.DrawString(markerFont, markerLabel, markerPos, markerColor, 0f, Vector2.Zero, markerScale, SpriteEffects.None, 0f);
                }
            }

            // control zone arcs (same lane, ±50 km along track)
            if (!ship.Status.IsInStationControlArea)
            {
                DrawPolarShipControlZone(ship, seperationCircleColor, 1.5f / Camera.Zoom);
            }
        }
    }

    private void DrawPolarShipControlZone(Ship ship, Color color, float thickness)
    {
        var body = GameState.CentralBody;
        var altitude = ship.PositionD.Length() - body.Radius;
        if (!ControlLaneUtils.TryGetShipEffectiveLaneBounds(body, altitude, out var laneLowerAltitude, out var laneUpperAltitude, out _))
        {
            return;
        }

        var shipRadiusMeters = ship.PositionD.Length();
        if (!double.IsFinite(shipRadiusMeters) || shipRadiusMeters <= 0d)
        {
            return;
        }

        var halfAngle = GameConstants.ControlLaneLongitudinalHalfExtentMeters / shipRadiusMeters;
        var centerAngle = Math.Atan2(ship.PositionD.Y, ship.PositionD.X);
        var startAngle = centerAngle - halfAngle;
        var endAngle = centerAngle + halfAngle;
        var innerRadius = (float)((body.Radius + laneLowerAltitude) / Scale);
        var outerRadius = (float)((body.Radius + laneUpperAltitude) / Scale);

        DrawWrappedArc(innerRadius, startAngle, endAngle, color, thickness);
        DrawWrappedArc(outerRadius, startAngle, endAngle, color, thickness);

        var innerStart = ProjectPolarPoint(body.Radius + laneLowerAltitude, startAngle);
        var outerStart = ProjectPolarPoint(body.Radius + laneUpperAltitude, startAngle);
        var innerEnd = ProjectPolarPoint(body.Radius + laneLowerAltitude, endAngle);
        var outerEnd = ProjectPolarPoint(body.Radius + laneUpperAltitude, endAngle);
        SpriteBatch.DrawLine(innerStart, outerStart, color, thickness);
        SpriteBatch.DrawLine(innerEnd, outerEnd, color, thickness);
    }

    private void DrawWrappedArc(float radius, double startAngle, double endAngle, Color color, float thickness)
    {
        var twoPi = Math.PI * 2d;
        var normalizedStart = startAngle % twoPi;
        if (normalizedStart < 0d)
        {
            normalizedStart += twoPi;
        }

        var span = endAngle - startAngle;
        if (span <= 0d)
        {
            return;
        }

        var normalizedEnd = normalizedStart + span;
        if (normalizedEnd <= twoPi)
        {
            DrawArc(radius, normalizedStart, normalizedEnd, color, thickness);
            return;
        }

        DrawArc(radius, normalizedStart, twoPi, color, thickness);
        DrawArc(radius, 0d, normalizedEnd - twoPi, color, thickness);
    }

    private void DrawArc(float radius, double startAngle, double endAngle, Color color, float thickness)
    {
        var delta = endAngle - startAngle;
        var segmentCount = Math.Max(6, (int)Math.Ceiling(delta / (2d.ToRadians())));
        var previous = new Vector2((float)(Math.Cos(startAngle) * radius), (float)(Math.Sin(startAngle) * radius));
        for (int i = 1; i <= segmentCount; i++)
        {
            var t = (double)i / segmentCount;
            var angle = startAngle + (delta * t);
            var current = new Vector2((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius));
            SpriteBatch.DrawLine(previous, current, color, thickness);
            previous = current;
        }
    }

    private void DrawStations(List<Station> stations, Ship selectedShip)
    {
        int size = 4;
        foreach (Station station in stations)
        {
            Vector2 position = station.Orbit.PositionVector / Scale;

            if (station.IsSelected)
            {
                DrawOrbit(station.Orbit, SelectedOrbitColor);
            }

            bool shipTargetsStation = selectedShip?.Destination is StationDestination stationDestination
                && stationDestination.Station == station;
            bool shouldDrawArrows = station.IsSelected || shipTargetsStation;

            DrawStationControlArea(station, shouldDrawArrows);

            Color stationColor = station.IsSelected ? SelectedStationColor : StationColor;
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
            DrawDashedPolyline(path, ControlAreaColor, 1f / Camera.Zoom, dashLength: 1.5d, gapLength: 0.75d);
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
        var peWorldPos = orbit.GetPositionAtAngle(0d) / Scale;
        DrawApsisMarker(peWorldPos, "PE", color);

        if (!orbit.IsEscapeTrajectory)
        {
            var apWorldPos = orbit.GetPositionAtAngle(Math.PI) / Scale;
            DrawApsisMarker(apWorldPos, "AP", color);
        }
    }

    private void DrawApsisMarker(Vector2 worldPos, string label, Color color)
    {
        var markerSize = 5f / Camera.Zoom;
        var thickness = 1.5f / Camera.Zoom;

        // draw diamond
        SpriteBatch.DrawLine(worldPos + new Vector2(-markerSize, 0f), worldPos + new Vector2(0f, -markerSize), color, thickness);
        SpriteBatch.DrawLine(worldPos + new Vector2(0f, -markerSize), worldPos + new Vector2(markerSize, 0f), color, thickness);
        SpriteBatch.DrawLine(worldPos + new Vector2(markerSize, 0f), worldPos + new Vector2(0f, markerSize), color, thickness);
        SpriteBatch.DrawLine(worldPos + new Vector2(0f, markerSize), worldPos + new Vector2(-markerSize, 0f), color, thickness);

        // draw label scaled and counter-rotated so it stays upright
        // increase scale for readability and add a simple outline for thickness
        var textScale = 0.9f / Camera.Zoom;
        var labelOffset = Vector2.Transform(new Vector2(markerSize + 6f / Camera.Zoom, -markerSize), Matrix.CreateRotationZ(-Camera.Rotation));
        var labelPos = worldPos + labelOffset;
        var outlineOffset = 1.2f / Camera.Zoom;

        // outline (draw black text in four directions)
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(-outlineOffset, 0f), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(outlineOffset, 0f), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, -outlineOffset), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, outlineOffset), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);

        // main label
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos, color, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    protected override Vector2 ProjectPolarPoint(double radius, double angle)
    {
        var point = MathUtils.PolarToCartesian(angle, radius) / Scale;
        return point.ToVector2();
    }

    private void DrawOrbit(Orbit orbit, Color color)
    {
        if (orbit.IsEscapeTrajectory)
        {
            var maxAngle = orbit.GetHyperbolicTrueAnomalyLimit() - 0.01d;
            var start = orbit.GetPositionAtAngle(-maxAngle) / Scale;
            const int segments = 180;

            for (int i = 1; i <= segments; i++)
            {
                var angle = -maxAngle + ((2d * maxAngle) * i / segments);
                var end = orbit.GetPositionAtAngle(angle) / Scale;
                SpriteBatch.DrawLine(start, end, color, 1f / Camera.Zoom);
                start = end;
            }

            return;
        }

        var startElliptic = orbit.GetPositionAtAngle(0d) / Scale;
        for (int i = 2; i <= 360; i += 2)
        {
            var end = orbit.GetPositionAtAngle(((double)i).ToRadians()) / Scale;
            SpriteBatch.DrawLine(startElliptic, end, color, 1f / Camera.Zoom);
            startElliptic = end;
        };
    }

    private void DrawOrbitMouseIntersection(Orbit orbit)
    {
        var mousePos = Camera.ScreenToWorld(MouseState.Position.ToVector2());
        var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(orbit, mousePos.ToNumerics());
        if (orbitPos is not null)
        {
            CircleF intersectionCircle = new CircleF() { Center = orbitPos.ScreenPosition, Radius = 5 };
            SpriteBatch.DrawCircle(intersectionCircle, 12, Color.LightGray);
        }
    }

    private Ship? GetHoveredShip(List<Ship> ships)
    {
        var mouseWorld = Camera.ScreenToWorld(MouseState.Position.ToVector2());
        var hitRadius = 8f / Camera.Zoom;
        var hitRadiusSq = hitRadius * hitRadius;

        Ship? hovered = null;
        var bestDistSq = float.MaxValue;
        foreach (var ship in ships)
        {
            var pos = ship.Orbit.PositionVector / Scale;
            var distSq = Vector2.DistanceSquared(mouseWorld, pos);
            if (distSq <= hitRadiusSq && distSq < bestDistSq)
            {
                bestDistSq = distSq;
                hovered = ship;
            }
        }

        return hovered;
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

        var nodePosition = baseOrbit.GetPositionAtAngle(maneuverNode.TrueAnomaly) / Scale;
        var nodeRadius = UIConstants.NodeRadius / Camera.Zoom;
        var nodeThickness = UIConstants.NodeThickness / Camera.Zoom;
        SpriteBatch.DrawCircle(new CircleF() { Center = nodePosition, Radius = nodeRadius }, 12, HoverNodeColor, nodeThickness);

        return predictedOrbit;
    }

    private void DrawManueverNode(ManeuverNode manueverNode, Orbit baseOrbit, bool drawButtons = true)
    {
        var predictedOrbit = manueverNode.GetPredictedOrbit(baseOrbit);
        if (predictedOrbit is not null)
        {
            var predictedColor = manueverNode.IsConfirmed ? Color.LightGreen : Color.Yellow;
            DrawOrbit(predictedOrbit, predictedColor);
        }

        manueverNode.ScreenPosition = baseOrbit.GetPositionAtAngle(manueverNode.TrueAnomaly) / Scale;

        var nodeRadius = UIConstants.NodeRadius / Camera.Zoom;
        var intersectionCircle = new CircleF() { Center = manueverNode.ScreenPosition, Radius = nodeRadius };
        var nodeColor = manueverNode.IsConfirmed ? Color.LightGreen : Color.Yellow;
        var nodeThickness = UIConstants.NodeThickness / Camera.Zoom;
        SpriteBatch.DrawCircle(intersectionCircle, 12, nodeColor, nodeThickness);
        DrawManeuverDeltaVLabel(manueverNode, nodeColor);

        if (!drawButtons)
            return;

        var mousePos = Camera.ScreenToWorld(MouseState.Position.ToVector2());
        var threshhold = UIConstants.NodeButtonRadius;
        var offset = UIConstants.NodeButtonOffset / Camera.Zoom;
        if (Vector2.Distance(mousePos, manueverNode.ScreenPosition) < threshhold + offset + manueverNode.DragOffset.Length() * MathF.Sqrt(2) && !manueverNode.IsDragged)
        {
            manueverNode.ButtonOffset = offset;
            manueverNode.ButtonRadius = UIConstants.NodeButtonRadius / Camera.Zoom;
            manueverNode.ButtonThickness = UIConstants.NodeButtonThickness / Camera.Zoom;
            manueverNode.VelocityDir = Vector2.Normalize(baseOrbit.GetVelocityAtAngle(manueverNode.TrueAnomaly)).ToNumerics();
            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Prograde)
                DrawButton(manueverNode.ProgradeButton, mousePos);
            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Retrograde)
                DrawButton(manueverNode.RetrogradeButton, mousePos);
            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Normal)
                DrawButton(manueverNode.NormalButton, mousePos);
            if (manueverNode.DragType is ManeuverDragType.None || manueverNode.DragType == ManeuverDragType.Antinormal)
                DrawButton(manueverNode.AntinormalButton, mousePos);

            if (!manueverNode.IsConfirmed )
            {
                if (manueverNode.DragType is ManeuverDragType.None)
                    DrawButton(manueverNode.ConfirmButton, mousePos);
            }
            if (manueverNode.DragType is ManeuverDragType.None)
                DrawButton(manueverNode.CancelButton, mousePos);
        }
    }

    private void DrawManeuverDeltaVLabel(ManeuverNode manueverNode, Color color)
    {
        var totalDeltaV = Math.Sqrt(
            (manueverNode.ProgradeDeltaV * manueverNode.ProgradeDeltaV)
            + (manueverNode.NormalDeltaV * manueverNode.NormalDeltaV));

        if (!double.IsFinite(totalDeltaV))
            return;

        var label = $"dV {totalDeltaV:0.0} m/s";
        var textScale = 0.8f / Camera.Zoom;
        var labelOffset = Vector2.Transform(
            new Vector2(10f / Camera.Zoom, -12f / Camera.Zoom),
            Matrix.CreateRotationZ(-Camera.Rotation));
        var labelPos = manueverNode.ScreenPosition + labelOffset;
        var outlineOffset = 1.1f / Camera.Zoom;

        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(-outlineOffset, 0f), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(outlineOffset, 0f), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, -outlineOffset), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos + new Vector2(0f, outlineOffset), Color.Black, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(Fonts.DebugFont, label, labelPos, color, -Camera.Rotation, Vector2.Zero, textScale, SpriteEffects.None, 0f);
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
        var hoverOffset = Vector2.Distance(pos, mousePos) < radius ? new Vector2(-1, -1) : new Vector2(0, 0);
        SpriteBatch.DrawCircle(new CircleF() { Center = pos + hoverOffset, Radius = radius }, 16, color, thickness);
        DrawButtonLabel(button, mousePos);
    }

    private void DrawButtonLabel(Button button, Vector2 mousePos)
    {
        var pos = button.Position;
        var radius = button.Radius;
        var color = button.Color;
        var thickness = button.Thickness;
        var hoverOffset = Vector2.Distance(pos, mousePos) < radius ? new Vector2(-1, -1) : new Vector2(0, 0);

        Vector2 RotateIconOffset(Vector2 localOffset)
        {
            return Vector2.Transform(localOffset, Matrix.CreateRotationZ(-Camera.Rotation));
        }

        void DrawIconLine(Vector2 startOffset, Vector2 endOffset)
        {
            var start = pos + RotateIconOffset(startOffset) + hoverOffset;
            var end = pos + RotateIconOffset(endOffset) + hoverOffset;
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

    

    private void DrawOrbitChevronArrow(double angle, double radius, int motionSign, bool alongOrbit, Color color)
    {
        var center = (MathUtils.PolarToCartesian(angle, radius) / Scale).ToVector2();
        var radial = Vector2.Normalize(center);
        if (radial.LengthSquared() <= 0f)
        {
            return;
        }

        var tangent = new Vector2(-radial.Y, radial.X) * motionSign;
        if (!alongOrbit)
        {
            tangent = -tangent;
        }

        var headLength = 9f / Camera.Zoom;
        var headWidth = 4.5f / Camera.Zoom;
        var tip = center + (tangent * (headLength * 0.5f));
        var tailCenter = center - (tangent * (headLength * 0.5f));
        var wingOffset = radial * headWidth;
        var thickness = 1.5f / Camera.Zoom;
        var arrowColor = color * 0.5f;

        SpriteBatch.DrawLine(tip, tailCenter + wingOffset, arrowColor, thickness);
        SpriteBatch.DrawLine(tip, tailCenter - wingOffset, arrowColor, thickness);
    }
}
