using Microsoft.Xna.Framework;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation.OrbitingObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SpaceTrafficController.Core;

public class GameState
{
    public List<HasOrbit> OrbitingObjects { get; set; }
    public List<Ship> Ships { get { return OrbitingObjects.OfType<Ship>().ToList(); } }
    public List<Station> Stations { get { return Stations.OfType<Station>().ToList(); } }

    public void Init()
    {
        OrbitingObjects = new List<HasOrbit>();
    }

    public void Update(GameTime gameTime)
    {
        var timeStep = gameTime.ElapsedGameTime.TotalSeconds * Warp;

        foreach (var orbiter in OrbitingObjects)
        {
            orbiter.Update(timeStep);
        }
        CheckShipSeperation();
    }

    public int WarpState { get; set; } = 1;
    private int Warp
    {
        get
        {
            return WarpState switch
            {
                1 => 1,
                2 => 2,
                3 => 4,
                4 => 8,
                5 => 16,
                6 => 32,
                7 => 64,
                _ => 1
            };
        }
    }

    public void IncreaseWarp()
    {
        WarpState = Math.Clamp(WarpState + 1, 1, 7);
    }

    public void DecreaseWarp()
    {
        WarpState = Math.Clamp(WarpState - 1, 1, 7);
    }

    public void CheckShipSeperation()
    {
        var cellSize = GameConstants.ShipSepration;
        var grid = new Dictionary<(int, int), List<Ship>>();

        foreach (var ship in Ships)
        {
            ship.ShipStatus.IsEncroached = false;

            int cx = (int)MathF.Floor(ship.Position.X / cellSize);
            int cy = (int)MathF.Floor(ship.Position.Y / cellSize);            
            var cell = (cx,  cy);
            
            if (!grid.ContainsKey(cell))
                grid[cell] = new List<Ship>();

            grid[cell].Add(ship);
        }

        foreach(var cell in grid.Keys)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var neighborCell = (cell.Item1 + dx, cell.Item2 + dy);
                    if (!grid.ContainsKey(neighborCell)) continue;

                    var shipsA = grid[cell];
                    var shipsB = grid[neighborCell];

                    foreach (var shipA in shipsA)
                    {
                        foreach (var shipB in shipsB)
                        {
                            // Avoid double-checking or self-check
                            if (shipA == shipB) continue;

                            // Optional: avoid double-checks across neighboring cells
                            if (cell == neighborCell && shipA.GetHashCode() > shipB.GetHashCode()) continue;

                            if (Vector2.Distance(shipA.Position, shipB.Position) <= GameConstants.ShipSepration)
                            {
                                shipA.ShipStatus.IsEncroached = true;
                                shipB.ShipStatus.IsEncroached = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
