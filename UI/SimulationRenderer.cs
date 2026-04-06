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

        DrawBody();

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
        // render that target orbit and show closest-approach between the selected ship and the target
        var selectedShip = gameState.SelectedShip;
        var target = gameState.TargetOrbitingObject;
        if (selectedShip is not null && target is not null)
        {
            var targetOrbit = target.Orbit;
            DrawOrbit(targetOrbit, TargetOrbitColor);
            DrawApsisMarkers(targetOrbit, TargetApsisColor);

            // prefer chained predicted orbits when maneuver nodes are present
            Orbit shipOrbitForApproach = GetOrbitAfterPlannedManeuvers(selectedShip);

            // prefer predicted orbit for target if it's a ship with a maneuver node
            Orbit targetOrbitForApproach = targetOrbit;
            if (target is Ship targetShip)
            {
                targetOrbitForApproach = GetOrbitAfterPlannedManeuvers(targetShip);
            }

            DrawClosestApproach(shipOrbitForApproach, targetOrbitForApproach, selectedShip, target);
        }
    }

    private void DrawBody()
    {
        var body = GameState.CentralBody;
        int radius = (int)(body.Radius / Scale);
        SpriteBatch.DrawCircle(new Vector2(0, 0), radius, 360, Color.Wheat, radius);
        DrawPlanetLongitudeLines(body);
        DrawPlanetOutline(body);
        DrawAtmosphere(body);
        DrawControlAltitude(body);
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

    private void DrawControlAltitude(CelestialBody body)
    {
        var radius = (float)((body.Radius + body.ControlAltitudeMeters) / Scale);
        var dashDeg = 3d;
        var gapDeg = 1d;
        for (double angle = 0d; angle < 360d; angle += dashDeg + gapDeg)
        {
            var start = (angle).ToRadians();
            var end = (angle + dashDeg).ToRadians();
            var p1 = new Vector2((float)(Math.Cos(start) * radius), (float)(Math.Sin(start) * radius));
            var p2 = new Vector2((float)(Math.Cos(end) * radius), (float)(Math.Sin(end) * radius));
            SpriteBatch.DrawLine(p1, p2, Color.LightGray, 1f / Camera.Zoom);
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

                // draw destination orbit and closest approach
                var destinationOrbit = GetDestinationOrbit(ship);
                if (destinationOrbit is not null)
                {
                    DrawOrbit(destinationOrbit, TargetOrbitColor);
                    DrawApsisMarkers(destinationOrbit, TargetApsisColor);

                    Orbit orbitForApproach = GetOrbitAfterPlannedManeuvers(ship);

                    DrawClosestApproach(orbitForApproach, destinationOrbit);
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
                var separationRadius = (float)(GameConstants.ShipSepration / 2d / Scale);
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

            // seperation circles
            if (!ship.Status.IsInStationControlArea)
            {
                CircleF seperationCircle = new CircleF() { Center = position, Radius = GameConstants.ShipSepration / 2 / Scale };
                SpriteBatch.DrawCircle(seperationCircle, 20, seperationCircleColor, 1.5f);
            }
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
            DrawDashedPolyline(path, ControlAreaColor, 1f / Camera.Zoom, dashLength: 1.5d, gapLength: 0.75d);
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

    private void DrawClosestApproach(Orbit shipOrbit, Orbit destOrbit, HasOrbit? shipObj = null, HasOrbit? destObj = null)
    {
        // coarse pass: primary best approach
        var (bestShipAngle, bestDestAngle) = FindClosestApproachAngles(shipOrbit, destOrbit, ClosestApproachCoarseSamples);
        // refine primary
        (bestShipAngle, bestDestAngle) = RefineClosestApproach(
            shipOrbit, destOrbit,
            bestShipAngle, bestDestAngle,
            ClosestApproachFineWindow, ClosestApproachFineSamples);

        // try to find a secondary approach (for bisecting/crossing orbits)
        var (secondShipAngle, secondDestAngle, secondDistSq) = FindClosestApproachAnglesWithExclusion(
            shipOrbit, destOrbit, ClosestApproachCoarseSamples, bestShipAngle, ClosestApproachExclusionWindow);

        var approaches = new List<(double shipAngle, double destAngle)> { (bestShipAngle, bestDestAngle) };
        // if second approach found and meaningfully different, refine and include
        if (double.IsFinite(secondDistSq) && secondDistSq < double.MaxValue)
        {
            // refine secondary
            (secondShipAngle, secondDestAngle) = RefineClosestApproach(
                shipOrbit, destOrbit,
                secondShipAngle, secondDestAngle,
                ClosestApproachFineWindow, ClosestApproachFineSamples);

            // ensure second is not a duplicate of the first (angular separation)
            double angSep = AngularDistance(bestShipAngle, secondShipAngle);
            if (angSep > 0.05d)
            {
                approaches.Add((secondShipAngle, secondDestAngle));
            }
        }

        // predefined single colors per approach so the user can match approach <-> predicted spot
        for (int idx = 0; idx < approaches.Count; idx++)
        {
            var (sa, da) = approaches[idx];

            var approachColor = ClosestApproachColors[Math.Min(idx, ClosestApproachColors.Length - 1)];

            // compute approach positions and distance (meters)
            var shipPosD = shipOrbit.GetPositionAtAngleD(sa);
            var destPosD = destOrbit.GetPositionAtAngleD(da);
            var dx = shipPosD.X - destPosD.X;
            var dy = shipPosD.Y - destPosD.Y;
            var approachDistanceMeters = Math.Sqrt((dx * dx) + (dy * dy));

            // draw white dotted line on the closest approach only if distance > 5km
            if (approachDistanceMeters > ClosestApproachLineThresholdMeters)
            {
                var shipScreen = shipOrbit.GetPositionAtAngle(sa) / Scale;
                var destScreen = destOrbit.GetPositionAtAngle(da) / Scale;
                DrawDashedLine(shipScreen, destScreen, ClosestApproachDashColor, 1.2f / Camera.Zoom, 6f / Camera.Zoom, 4f / Camera.Zoom);
            }

            // draw inbound chevrons at the approach points (color-coded, solid)
            DrawInboundChevronAtOrbitPoint(shipOrbit, sa, approachColor);
            DrawInboundChevronAtOrbitPoint(destOrbit, da, approachColor);

            // compute predicted destination position when ship reaches its approach angle and draw matching chevron
            try
            {
                var timeToShip = shipOrbit.TimeToTrueAomaly(sa);
                if (!double.IsInfinity(timeToShip) && !double.IsNaN(timeToShip))
                {
                    var predictedDestAngle = TrueAnomalyAfterTime(destOrbit, timeToShip);
                    DrawInboundChevronAtOrbitPoint(destOrbit, predictedDestAngle, approachColor);

                    // if both the selected object and target object are ships, draw minimum safe-distance circles
                    if (shipObj is Ship && destObj is Ship)
                    {
                        try
                        {
                            // predicted positions in meters
                            var shipPredictedD = shipOrbit.GetPositionAtAngleD(sa);
                            var destPredictedD = destOrbit.GetPositionAtAngleD(predictedDestAngle);
                            var distMeters = Math.Sqrt(Math.Pow(shipPredictedD.X - destPredictedD.X, 2) + Math.Pow(shipPredictedD.Y - destPredictedD.Y, 2));

                            // draw circle radius = ShipSepration/2 at each predicted position
                            var circleRadiusScreen = (float)(GameConstants.ShipSepration / 2 / Scale);
                            var shipCenter = (shipPredictedD / Scale).ToVector2();
                            var destCenter = (destPredictedD / Scale).ToVector2();
                            var safe = distMeters >= GameConstants.ShipSepration;
                            var col = safe ? ActiveShipColor : EncroachedSeparationColor;
                            SpriteBatch.DrawCircle(shipCenter, circleRadiusScreen, 36, col, 1.5f / Camera.Zoom);
                            SpriteBatch.DrawCircle(destCenter, circleRadiusScreen, 36, col, 1.5f / Camera.Zoom);
                        }
                        catch
                        {
                            // swallow any prediction/drawing errors
                        }
                    }
                }
            }
            catch
            {
                // ignore prediction failures
            }
        }
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

    private void DrawApproachChevronAtOrbitPoint(Orbit orbit, double trueAnomaly, Color color)
    {
        var posD = orbit.GetPositionAtAngleD(trueAnomaly);
        var velD = orbit.GetVelocityAtAngleD(trueAnomaly);
        var centerAngle = Math.Atan2(posD.Y, posD.X);
        var radius = posD.Length();
        var motionSign = Math.Sign((posD.X * velD.Y) - (posD.Y * velD.X));
        if (motionSign == 0) motionSign = 1;

        DrawOrbitChevronArrow(centerAngle, radius, motionSign, alongOrbit: true, color: color);
    }

    private void DrawInboundChevronAtOrbitPoint(Orbit orbit, double trueAnomaly, Color color)
    {
        var posD = orbit.GetPositionAtAngleD(trueAnomaly);
        var velD = orbit.GetVelocityAtAngleD(trueAnomaly);

        // screen-space position
        var pos = orbit.GetPositionAtAngle(trueAnomaly) / Scale;

        var radial = Vector2.Normalize(pos);
        if (radial.LengthSquared() <= 0f)
            return;

        var motionSign = Math.Sign((posD.X * velD.Y) - (posD.Y * velD.X));
        if (motionSign == 0) motionSign = 1;

        // tangent direction (used to spread chevron wings)
        var tangent = new Vector2(-radial.Y, radial.X) * motionSign;

        var headLength = 10f / Camera.Zoom; // distance the chevron extends inward from the orbit
        var headWidth = 6f / Camera.Zoom; // half-width of wing spread

        var tip = pos; // tip sits ON the orbit (pointed inward)
        var tailCenter = pos + radial * headLength; // outward from the tip (flip to point inward)

        var wingOffset = tangent * headWidth;
        var thickness = 1.8f / Camera.Zoom;

        // solid color, no opacity multiplication
        SpriteBatch.DrawLine(tip, tailCenter + wingOffset, color, thickness);
        SpriteBatch.DrawLine(tip, tailCenter - wingOffset, color, thickness);
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

        // eccentric anomaly from current true anomaly
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

        // normalize Mtarget into [0, 2π)
        Mtarget = ((Mtarget % (2d * Math.PI)) + (2d * Math.PI)) % (2d * Math.PI);

        // solve Kepler's equation for E: E - e*sin(E) = Mtarget
        double E = Mtarget; // initial guess
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

    private static (double shipAngle, double destAngle) FindClosestApproachAngles(
        Orbit shipOrbit, Orbit destOrbit, int sampleCount)
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
                    bestShipAngle = shipAngles[i];
                    bestDestAngle = destAngles[j];
                }
            }
        }

        return (bestShipAngle, bestDestAngle);
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
                // wrap into [0, 2π)
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

    private void DrawApproachMarker(Vector2 worldPos, Color color)
    {
        var size = 6f / Camera.Zoom;
        var thickness = 1.5f / Camera.Zoom;
        SpriteBatch.DrawLine(worldPos + new Vector2(-size, 0f), worldPos + new Vector2(size, 0f), color, thickness);
        SpriteBatch.DrawLine(worldPos + new Vector2(0f, -size), worldPos + new Vector2(0f, size), color, thickness);
        // draw a small circle around the crosshair
        SpriteBatch.DrawCircle(new CircleF() { Center = worldPos, Radius = size * 0.7f }, 12, color * 0.7f, thickness);
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
