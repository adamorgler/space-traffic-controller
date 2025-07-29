using Microsoft.Xna.Framework;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation.OrbitingObjects;
using System;
using System.Collections.Generic;
using System.Linq;

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
}
