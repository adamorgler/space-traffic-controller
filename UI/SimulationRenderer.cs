using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.UI;

public class SimulationRenderer
{
    private readonly SpriteBatch SpriteBatch;
    private readonly Camera2D Camera;

    private const int SCALE = 10000;

    public SimulationRenderer(SpriteBatch spriteBatch, Camera2D camera)
    {
        SpriteBatch = spriteBatch;
        Camera = camera;
    }

    public void Draw(GameState gameState)
    {
        DrawPlanet();
        DrawShips(gameState.Ships);
    }

    private void DrawPlanet()
    {
        int radius = (int) (PhysicalConstants.RadiusOfPlanet / SCALE);
        SpriteBatch.DrawCircle(new Vector2(0, 0), radius, 360, Color.Blue, radius);
    }

    private void DrawShips(List<Ship> ships)
    {
        int size = 15;
        foreach (Ship ship in ships)
        {
            Vector2 position = ship.Orbit.PositionVector / SCALE;
            DrawOrbit(ship.Orbit);
            SpriteBatch.DrawRectangle(position.X - (size / 2 / Camera.Zoom), position.Y - (size / 2 / Camera.Zoom), size / Camera.Zoom, size / Camera.Zoom, Color.Green, 2 / Camera.Zoom);
        }
    }

    private void DrawStations(List<Station> stations)
    {

    }

    private void DrawOrbit(Orbit orbit)
    {
        var start = orbit.GetPositionAtAngle(0d.ToRadians()) / SCALE;
        for (int i = 2; i <= 360; i += 2)
        {
            var end = orbit.GetPositionAtAngle(((double)i).ToRadians()) / SCALE;
            SpriteBatch.DrawLine(start, end, Color.White, 1f / Camera.Zoom);
            start = end;
        };
    }
}
