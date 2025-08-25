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
        DrawStations(gameState.Stations);
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
        DrawAtmosphere(body);
    }

    private void DrawAtmosphere(CelestialBody body)
    {
        var layers = body.AtmosphereLayers;
        var baseDensity = body.BaseAtmosphereDensity;
        var baseColor = Color.SkyBlue;
        foreach (var layer in layers)
        {
            var thickness = layer.Thickness / Scale;
            var radius = (layer.Altitude + body.Radius) / Scale - 1;
            var alpha = layer.Density / baseDensity;
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
            if (ship.Status.IsSelected)
            {
                var orbit = ship.Orbit;
                DrawOrbit(orbit, Color.White);
                if (ship.ManeuverNode is null)
                    DrawOrbitMouseIntersection(orbit);
                if (ship.ManeuverNode is not null)
                    DrawManueverNode(ship);

                DebugText.Add($"Ship: Position: {ship.Position}");
                DebugText.Add($"Orbit: AP: {ship.Orbit.Apoapsis}, PE: {ship.Orbit.Periapsis}, TrueAnomaly: {ship.Orbit.TrueAnomaly}");

                var manueverNode = ship.ManeuverNode;
                if (manueverNode is not null)
                {
                    DebugText.Add($"Manuever Node: TrueAnomaly: {manueverNode.TrueAnomaly} Position: {manueverNode.ScreenPosition}, DeltaV:{manueverNode.ProgradeDeltaV} + {manueverNode.NormalDeltaV} ");
                    var predictedOrbit = manueverNode.GetPredictedOrbit(orbit);
                    if (predictedOrbit is not null)
                        DebugText.Add($"PredOrbit: AP: {predictedOrbit.Apoapsis}, PE: {predictedOrbit.Periapsis}, V: {predictedOrbit.Velocity}, P: {predictedOrbit.PositionVector}");
                }
            }

            // ship square
            Color shipColor = ship.Status.IsSelected ? Color.Gold : Color.LimeGreen;
            SpriteBatch.DrawRectangle(position.X - (size / 2 ), position.Y - (size / 2 ), size , size , shipColor, 1.5f);
            
            // seperation circles
            CircleF seperationCircle = new CircleF() { Center = position, Radius = GameConstants.ShipSepration / 2 / Scale };
            Color seperationCircleColor = ship.Status.IsEncroached ? Color.Red : Color.Green;
            SpriteBatch.DrawCircle(seperationCircle, 20, seperationCircleColor, 1.5f);
        }
    }

    private void DrawStations(List<Station> stations)
    {
        int size = 7;
        foreach (Station station in stations)
        {
            Vector2 position = station.Orbit.PositionVector / Scale;

            var orbit = station.Orbit;
            DrawOrbit(orbit, Color.White);

            // ship square
            Color shipColor = Color.Red;
            SpriteBatch.DrawRectangle(position.X - (size / 2), position.Y - (size / 2), size, size, shipColor, 1.5f);
        }
    }

    private void DrawOrbit(Orbit orbit, Color color)
    {
        var start = orbit.GetPositionAtAngle(0f.ToRadians()) / Scale;
        for (int i = 2; i <= 360; i += 2)
        {
            var end = orbit.GetPositionAtAngle(((float)i).ToRadians()) / Scale;
            SpriteBatch.DrawLine(start, end, color, 1f / Camera.Zoom);
            start = end;
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
        switch (button.Label)
        {
            case ButtonLabel.Plus:
                SpriteBatch.DrawLine(new Vector2(pos.X, pos.Y + radius / 2) + hoverOffset, new Vector2(pos.X, pos.Y - radius / 2) + hoverOffset, color, thickness);
                SpriteBatch.DrawLine(new Vector2(pos.X + radius / 2, pos.Y) + hoverOffset, new Vector2(pos.X - radius / 2, pos.Y) + hoverOffset, color, thickness);
                break;
            case ButtonLabel.Minus:
                SpriteBatch.DrawLine(new Vector2(pos.X + radius / 2, pos.Y) + hoverOffset, new Vector2(pos.X - radius / 2, pos.Y) + hoverOffset, color, thickness);
                break;
            case ButtonLabel.V:
                SpriteBatch.DrawLine(new Vector2(pos.X - radius / 2, pos.Y) + hoverOffset, new Vector2(pos.X, pos.Y - radius / 2) + hoverOffset, color, thickness);
                SpriteBatch.DrawLine(new Vector2(pos.X + radius / 2, pos.Y) + hoverOffset, new Vector2(pos.X, pos.Y - radius / 2) + hoverOffset, color, thickness);
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
}
