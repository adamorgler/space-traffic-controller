using Microsoft.Xna.Framework;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation.OrbitingObjects;
using SpaceTrafficController.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SpaceTrafficController.Core;

public class GameState
{
    public static CelestialBody CentralBody { get; set; }
    public List<HasOrbit> OrbitingObjects { get; set; }
    public List<Ship> Ships { get { return OrbitingObjects.OfType<Ship>().ToList(); } }
    public List<Station> Stations { get { return OrbitingObjects.OfType<Station>().ToList(); } }
    public Ship SelectedShip { get; set; }

    public void Init()
    {
        CentralBody = new CelestialBody()
        {
            Name = "TITAN",
            Radius = PhysicalConstants.RADIUS_TITAN,
            Mass = PhysicalConstants.MASS_TITAN,
            BaseAtmosphereDensity = PhysicalConstants.ATMOS_BASE_DENSITY_TITAN,
            AtmosphereLayers = GenerateAtmosphereLayers(),
            ControlAltitudeMeters = 2500e3,
        };
        OrbitingObjects = new List<HasOrbit>();
    }

    public void Update(GameTime gameTime)
    {
        var timeStep = gameTime.ElapsedGameTime.TotalSeconds * Warp;

        foreach (var orbiter in OrbitingObjects)
        {
            orbiter.Update(timeStep);
        }

        // Update ship controllability based on control altitude
        var controlRadius = CentralBody.ControlRadius;
        foreach (var ship in Ships)
        {
            var dist = ship.PositionD.Length();
            if (!ship.Orbit.IsEscapeTrajectory && dist >= controlRadius)
            {
                ship.Status.IsControllable = false;
            }
            else
            {
                ship.Status.IsControllable = true;
            }
        }

        RemoveDespawnedShips();
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
                8 => 128,
                9 => 256,
                10 => 512,
                _ => 1
            };
        }
    }

    public void IncreaseWarp()
    {
        WarpState = Math.Clamp(WarpState + 1, 1, 10);
    }

    public void DecreaseWarp()
    {
        WarpState = Math.Clamp(WarpState - 1, 1, 10);
    }

    public void CheckShipSeperation()
    {
        var cellSize = GameConstants.ShipSepration;
        var grid = new Dictionary<(int, int), List<Ship>>();

        foreach (var ship in Ships)
        {
            ship.Status.IsEncroached = false;

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
                                shipA.Status.IsEncroached = true;
                                shipB.Status.IsEncroached = true;
                            }
                        }
                    }
                }
            }
        }
    }

    private void RemoveDespawnedShips()
    {
        var despawnedShips = Ships.Where(ship => ship.ShouldDespawn()).ToList();
        if (despawnedShips.Count == 0)
        {
            return;
        }

        foreach (var ship in despawnedShips)
        {
            if (SelectedShip == ship)
            {
                SelectedShip = null;
            }

            OrbitingObjects.Remove(ship);
        }
    }

    private static List<AtmosphereLayer> GenerateAtmosphereLayers()
    {
        const int layerCount = 5;
        const double topOfAtmosphere = PhysicalConstants.ATMOS_THICKNESS_TITAN;
        const double baseDensity = PhysicalConstants.ATMOS_BASE_DENSITY_TITAN; // kg/m³ at surface
        const double basePressure = PhysicalConstants.ATMOS_BASE_PRESSURE_TITAN; // Pascals at surface

        List<AtmosphereLayer> layers = new();

        var layerThickness = topOfAtmosphere / layerCount;
        for (int i = 0; i < layerCount; i++)
        {
            double layerAltitude = layerThickness * i;
            double normalizedAltitude = layerAltitude / topOfAtmosphere;

            // Simplified exponential density and pressure decay
            double density = baseDensity * Math.Exp(-normalizedAltitude);
            double pressure = basePressure * Math.Exp(-normalizedAltitude);

            layers.Add(new AtmosphereLayer
            {
                Altitude = layerAltitude,
                Density = density,
                Pressure = pressure,
                Thickness = layerThickness,
            });
        }

        return layers;
    }
}
