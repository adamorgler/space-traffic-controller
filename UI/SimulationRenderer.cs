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
    private readonly SpriteBatch SpriteBatch;
    private readonly Camera2D Camera;
    private MouseState MouseState;

    private const int Scale = GameConstants.Scale;

    private List<string> DebugText = new();

    public SimulationRenderer(SpriteBatch spriteBatch, Camera2D camera)
    {
        SpriteBatch = spriteBatch;
        Camera = camera;
    }

    public void DrawWorld(GameState gameState)
    {
        MouseState = Mouse.GetState();

        DrawPlanet();
        DrawShips(gameState.Ships);
    }

    public void DrawScreen(GameState gameState)
    {
        DrawDebugText();
    }

    private void DrawPlanet()
    {
        int radius = (int) (PhysicalConstants.RadiusOfPlanet / Scale);
        SpriteBatch.DrawCircle(new Vector2(0, 0), radius, 360, Color.Blue, radius);
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
                DrawOrbit(orbit);
                DrawOrbitMouseIntersection(orbit);
                if (ship.ManeuverNode is not null)
                    DrawManueverNode(ship);
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

    }

    private void DrawOrbit(Orbit orbit)
    {
        var start = orbit.GetPositionAtAngle(0d.ToRadians()) / Scale;
        for (int i = 2; i <= 360; i += 2)
        {
            var end = orbit.GetPositionAtAngle(((double)i).ToRadians()) / Scale;
            SpriteBatch.DrawLine(start, end, Color.White, 1f / Camera.Zoom);
            start = end;
        };
    }

    private void DrawOrbitMouseIntersection(Orbit orbit)
    {
        var mousePos = Camera.ScreenToWorld(MouseState.Position.ToVector2());
        var orbitPos = OrbitUtils.GetOrbitIntersectionNearMouse(orbit, mousePos.ToNumerics());
        if (orbitPos is not null)
        {
            CircleF intersectionCircle = new CircleF() { Center = orbitPos.WorldPosition, Radius = 5 };
            SpriteBatch.DrawCircle(intersectionCircle, 12, Color.LightGray);
        }
    }

    private void DrawManueverNode(Ship ship)
    {
        var manueverNode = ship.ManeuverNode;
        var nodeRadius = 12 / Camera.Zoom;
        CircleF intersectionCircle = new CircleF() { Center = manueverNode.Position, Radius = nodeRadius };
        SpriteBatch.DrawCircle(intersectionCircle, 12, Color.Gold);

        var mousePos = Camera.ScreenToWorld(MouseState.Position.ToVector2());
        var threshhold = 10;
        var offset = 24 / Camera.Zoom;
        if (Vector2.Distance(mousePos, manueverNode.Position) < threshhold + offset)
        {
            var orbit = ship.Orbit;
            var velocityDir = Vector2.Normalize(orbit.GetVelocityAtAngle(manueverNode.TrueAnomaly));
            var normalDir = new Vector2(-velocityDir.Y, velocityDir.X);

            var progradeOffset = manueverNode.Position + velocityDir * offset;
            var retrogradeOffset = manueverNode.Position - velocityDir * offset;
            var normalOffset = manueverNode.Position + normalDir * offset;
            var antinormalOffset = manueverNode.Position - normalDir * offset;

            var nodeButtonThickness = 1.5f / Camera.Zoom;
            var nodeButtonRadius = 8 / Camera.Zoom;
            var velocityColor = Color.GreenYellow;
            var normalColor = Color.CornflowerBlue;
            SpriteBatch.DrawCircle(new CircleF() { Center = progradeOffset, Radius = nodeButtonRadius }, 16, velocityColor, nodeButtonThickness);
            SpriteBatch.DrawCircle(new CircleF() { Center = retrogradeOffset, Radius = nodeButtonRadius }, 16, velocityColor, nodeButtonThickness);
            SpriteBatch.DrawCircle(new CircleF() { Center = normalOffset, Radius = nodeButtonRadius }, 16, normalColor, nodeButtonThickness);
            SpriteBatch.DrawCircle(new CircleF() { Center = antinormalOffset, Radius = nodeButtonRadius }, 16, normalColor, nodeButtonThickness);
            SpriteBatch.DrawString(Fonts.ManueverNode, "+", progradeOffset, velocityColor);
            SpriteBatch.DrawString(Fonts.ManueverNode, "-", retrogradeOffset, velocityColor);
            SpriteBatch.DrawString(Fonts.ManueverNode, "+", normalOffset, normalColor);
            SpriteBatch.DrawString(Fonts.ManueverNode, "-", antinormalOffset, normalColor);
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
