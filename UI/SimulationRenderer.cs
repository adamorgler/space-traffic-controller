using MenuBuddy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Utilities;
using System;
using System.Collections.Generic;

namespace SpaceTrafficController.UI;

public class SimulationRenderer
{
    private readonly GraphicsDevice GraphicsDevice;
    private readonly SpriteBatch SpriteBatch;
    private readonly Camera2D Camera;
    private readonly BasicEffect BasicEffect;
    private MouseState MouseState;

    private const int Scale = GameConstants.RenderingScale;

    private List<string> DebugText = new();

    public SimulationRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Camera2D camera)
    {
        GraphicsDevice = graphicsDevice;
        SpriteBatch = spriteBatch;
        Camera = camera;
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
        DrawStations(gameState.Stations, gameState.SelectedShip);
        DrawShips(gameState.Ships);
    }

    public void DrawScreen(GameState gameState)
    {
        DrawDebugText();
    }

    private void DrawBody()
    {
        var body = GameState.CentralBody;
        int radius = (int) (body.Radius / Scale);
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

    private void DrawShips(List<Ship> ships)
    {
        int size = 5;
        foreach (Ship ship in ships)
        {
            Vector2 position = ship.Orbit.PositionVector / Scale;

            // ship selected
            if (ship.IsSelected)
            {
                var orbit = ship.Orbit;
                DrawOrbit(orbit, Color.White);
                if (ship.ManeuverNode is null)
                    DrawOrbitMouseIntersection(orbit);
                if (ship.ManeuverNode is not null)
                    DrawManueverNode(ship);

                DebugText.Add($"Ship: Position: {ship.Position}");
                var orbitType = ship.Orbit.IsEscapeTrajectory ? "Escape" : "Bound";
                var apoapsisText = ship.Orbit.IsEscapeTrajectory ? "N/A (escape)" : ship.Orbit.Apoapsis.ToString();
                DebugText.Add($"Orbit: Type: {orbitType}, AP: {apoapsisText}, PE: {ship.Orbit.Periapsis}, TrueAnomaly: {ship.Orbit.TrueAnomaly}");

                var manueverNode = ship.ManeuverNode;
                if (manueverNode is not null)
                {
                    DebugText.Add($"Manuever Node: TrueAnomaly: {manueverNode.TrueAnomaly} Position: {manueverNode.ScreenPosition}, DeltaV:{manueverNode.ProgradeDeltaV} + {manueverNode.NormalDeltaV} ");
                    var predictedOrbit = manueverNode.GetPredictedOrbit(orbit);
                    if (predictedOrbit is not null)
                    {
                        var predictedOrbitType = predictedOrbit.IsEscapeTrajectory ? "Escape" : "Bound";
                        var predictedApoapsisText = predictedOrbit.IsEscapeTrajectory ? "N/A (escape)" : predictedOrbit.Apoapsis.ToString();
                        DebugText.Add($"PredOrbit: Type: {predictedOrbitType}, AP: {predictedApoapsisText}, PE: {predictedOrbit.Periapsis}, V: {predictedOrbit.Velocity}, P: {predictedOrbit.PositionVector}");
                    }
                }
            }

            // ship square: uncontrolled ships render as light gray
            Color shipColor;
            Color seperationCircleColor;
            if (!ship.Status.IsControllable)
            {
                shipColor = Color.LightGray;
                seperationCircleColor = Color.LightGray;
            }
            else
            {
                shipColor = ship.IsSelected ? Color.Gold : Color.LimeGreen;
                seperationCircleColor = ship.Status.IsEncroached ? Color.Red : Color.Green;

            }
            SpriteBatch.DrawRectangle(position.X - (size / 2 ), position.Y - (size / 2 ), size , size , shipColor, 1.5f);
            
            // seperation circles
            CircleF seperationCircle = new CircleF() { Center = position, Radius = GameConstants.ShipSepration / 2 / Scale };
            SpriteBatch.DrawCircle(seperationCircle, 20, seperationCircleColor, 1.5f);
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
                DrawOrbit(station.Orbit, Color.White);
            }

            bool shipTargetsStation = selectedShip?.Destination is StationDestination stationDestination
                && stationDestination.Station == station;
            bool shouldDrawArrows = station.IsSelected || shipTargetsStation;

            DrawStationControlArea(station, shouldDrawArrows);

            Color stationColor = station.IsSelected ? Color.Gold : Color.AliceBlue;
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
            DrawDashedPolyline(path, Color.LightSkyBlue, 1f / Camera.Zoom, dashLength: 1.5d, gapLength: 0.75d);
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

        DrawOrbitChevronArrow(centerAngle + arrivalAngle + arrowOffsetAngle, outerRadius, motionSign, alongOrbit: false, Color.LimeGreen);
        DrawOrbitChevronArrow(centerAngle - arrivalAngle - arrowOffsetAngle, innerRadius, motionSign, alongOrbit: true, Color.LimeGreen);
        DrawOrbitChevronArrow(centerAngle + departureAngle + arrowOffsetAngle, innerRadius, motionSign, alongOrbit: true, Color.Red);
        DrawOrbitChevronArrow(centerAngle - departureAngle - arrowOffsetAngle, outerRadius, motionSign, alongOrbit: false, Color.Red);
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

    private void DrawManueverNode(Ship ship)
    {
        var manueverNode = ship.ManeuverNode;

        var predictedOrbit = manueverNode.GetPredictedOrbit(ship.Orbit);
        if (predictedOrbit is not null)
        {
            DrawOrbit(predictedOrbit, Color.LightGray);
        }

        var nodeRadius = UIConstants.NodeRadius / Camera.Zoom;
        var intersectionCircle = new CircleF() { Center = manueverNode.ScreenPosition, Radius = nodeRadius };
        var nodeColor = manueverNode.IsConfirmed ? Color.LightGreen : Color.Yellow;
        var nodeThickness = UIConstants.NodeThickness / Camera.Zoom;
        SpriteBatch.DrawCircle(intersectionCircle, 12, nodeColor, nodeThickness);

        var mousePos = Camera.ScreenToWorld(MouseState.Position.ToVector2());
        var threshhold = UIConstants.NodeButtonRadius;
        var offset = UIConstants.NodeButtonOffset / Camera.Zoom;
        if (Vector2.Distance(mousePos, manueverNode.ScreenPosition) < threshhold + offset + manueverNode.DragOffset.Length() * MathF.Sqrt(2) && !manueverNode.IsDragged)
        {
            manueverNode.ButtonOffset = offset;
            manueverNode.ButtonRadius = UIConstants.NodeButtonRadius / Camera.Zoom;
            manueverNode.ButtonThickness = UIConstants.NodeButtonThickness / Camera.Zoom;
            manueverNode.VelocityDir = Vector2.Normalize(ship.Orbit.GetVelocityAtAngle(manueverNode.TrueAnomaly)).ToNumerics();
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

    private void DrawDebugText()
    {
        var offset = 0;
        var offsetStep = 15;
        foreach(var text in DebugText)
        {
            SpriteBatch.DrawString(Fonts.DebugFont, text, new Vector2(10, 10 + offset), Color.White);
            offset += offsetStep;
        }
        DebugText.Clear();
    }

    private static List<List<Vector2>> BuildStationControlPaths(
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
                points.Add(ToScaledPoint(radius, angle));
            }

            return points;
        }

        List<Vector2> BuildRadialPath(double angle, double startRadius, double endRadius)
        {
            return new List<Vector2>
            {
                ToScaledPoint(startRadius, angle),
                ToScaledPoint(endRadius, angle),
            };
        }

        Vector2 ToScaledPoint(double radius, double angle)
        {
            var point = MathUtils.PolarToCartesian(angle, radius) / Scale;
            return point.ToVector2();
        }
    }

    private void DrawDashedPolyline(IReadOnlyList<Vector2> points, Color color, float thickness, double dashLength, double gapLength)
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
