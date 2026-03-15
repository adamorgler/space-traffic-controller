using Microsoft.Xna.Framework;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using SpaceTrafficController.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SpaceTrafficController.Core;

public class GameState
{
    private const double ScorePerSuccessBase = 0.5d;
    private const double ScorePenaltyPerMistake = 1d;
    private const int MaxMultiplier = 8;
    private const double ArrivalEdgeBufferMeters = 15e3;
    private const double ArrivalLaneOffsetMeters = 50e3;
    private const double ArrivalSpawnInnerBufferMeters = 25e3;
    private const double DepartureDriftOffsetMeters = 100e3;
    private const double DepartureLaneSpawnBufferMeters = 50e3;
    private const double InboundExitOvershootMeters = 250e3;
    private const double InboundPeriapsisRandomRangeMeters = 450e3;
    private const double HighEllipseSpawnChance = 0.8d;
    private const double MinSpawnAltitudeAboveAtmosphereBufferMeters = 25e3;

    private readonly Random _rng = new();
    private double _timeSinceOutboundSpawnSeconds;
    private double _nextOutboundSpawnDelaySeconds;

    public static CelestialBody CentralBody { get; set; }
    public List<HasOrbit> OrbitingObjects { get; set; }
    public List<Ship> Ships { get { return OrbitingObjects.OfType<Ship>().ToList(); } }
    public List<Station> Stations { get { return OrbitingObjects.OfType<Station>().ToList(); } }
    public HasOrbit SelectedOrbitingObject { get; set; }
    public Ship SelectedShip
    {
        get => SelectedOrbitingObject as Ship;
        set => SelectedOrbitingObject = value;
    }

    public double ElapsedTimeSeconds { get; private set; }
    public int CurrentWarpMultiplier => Warp;
    public bool IsPaused { get; private set; }
    public double Score { get; private set; }
    public int ScoreMultiplier { get; private set; }
    public int TargetActiveShips => GetTargetShipCount();

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
        ElapsedTimeSeconds = 0d;
        IsPaused = false;
        Score = 6d;
        ScoreMultiplier = 1;
        _timeSinceOutboundSpawnSeconds = 0d;
        _nextOutboundSpawnDelaySeconds = NextOutboundSpawnDelaySeconds();

        InitializeScenario();
    }

    public void Update(GameTime gameTime)
    {
        var timeStep = IsPaused ? 0d : gameTime.ElapsedGameTime.TotalSeconds * Warp;
        ElapsedTimeSeconds += timeStep;

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

        CheckShipSeperation();
        RemoveDespawnedShips();

        if (timeStep > 0d)
        {
            UpdateTrafficSpawning(timeStep);
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

    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public void CheckShipSeperation()
    {
        var cellSize = GameConstants.ShipSepration;
        var grid = new Dictionary<(int, int), List<Ship>>();

        foreach (var ship in Ships)
        {
            ship.Status.WasEncroachedLastFrame = ship.Status.IsEncroached;
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

        var newlyEncroachedShipCount = Ships.Count(ship => ship.Status.IsEncroached && !ship.Status.WasEncroachedLastFrame);
        var mistakeEvents = Math.Max(0, (int)Math.Ceiling(newlyEncroachedShipCount / 2d));
        for (int i = 0; i < mistakeEvents; i++)
        {
            RegisterMistake();
        }
    }

    private void RemoveDespawnedShips()
    {
        var controlRadius = CentralBody.ControlRadius;
        var despawnedShips = new List<Ship>();

        foreach (var ship in Ships)
        {
            var reachedDestination = ship.Destination?.HasArrived(ship) ?? false;
            var outsideControlArea = ship.PositionD.Length() >= controlRadius;
            var failedArrival = !reachedDestination
                && ship.Destination is StationDestination
                && outsideControlArea;

            var shouldDespawn = reachedDestination
                || failedArrival
                || ship.ShouldDespawn();

            if (!shouldDespawn)
            {
                continue;
            }

            if (reachedDestination)
            {
                RegisterSuccessfulDestination();
            }
            else if (failedArrival)
            {
                RegisterMistake();
            }

            despawnedShips.Add(ship);
        }

        if (despawnedShips.Count == 0)
        {
            return;
        }

        foreach (var ship in despawnedShips)
        {
            if (SelectedOrbitingObject == ship)
            {
                SelectedOrbitingObject = null;
            }

            OrbitingObjects.Remove(ship);
        }
    }

    private void InitializeScenario()
    {
        OrbitingObjects.Clear();

        const double stationAltitude = 750e3;
        var station = new Station(new Orbit(stationAltitude, stationAltitude, 0d, 0d))
        {
            Name = "Port Atlas",
        };

        OrbitingObjects.Add(station);

        var initialInboundShipCount = _rng.Next(4, 6);
        for (int i = 0; i < initialInboundShipCount; i++)
        {
            SpawnInboundShip(station);
        }
    }

    private void UpdateTrafficSpawning(double timeStep)
    {
        var station = Stations.FirstOrDefault();
        if (station is null)
        {
            return;
        }

        var targetShipCount = GetTargetShipCount();
        _timeSinceOutboundSpawnSeconds += timeStep;
        if (Ships.Count < targetShipCount && _timeSinceOutboundSpawnSeconds >= _nextOutboundSpawnDelaySeconds)
        {
            var availableDepartureLanes = GetAvailableDepartureLanes(station);
            if (availableDepartureLanes.Count > 0)
            {
                var lane = availableDepartureLanes[_rng.Next(availableDepartureLanes.Count)];
                SpawnOutboundShip(station, lane);
                _timeSinceOutboundSpawnSeconds = 0d;
                _nextOutboundSpawnDelaySeconds = NextOutboundSpawnDelaySeconds();
            }
        }

        while (Ships.Count < targetShipCount)
        {
            SpawnInboundShip(station);
        }
    }

    private int GetTargetShipCount()
    {
        return Math.Max(1, (int)Math.Round(Score, MidpointRounding.AwayFromZero));
    }

    private void SpawnInboundShip(Station station)
    {
        var orbit = CreateRandomInboundOrbit(station);
        var spawnApoapsis = orbit.Apoapsis;
        var minSpawnAltitude = Math.Min(
            spawnApoapsis,
            Math.Max(
                orbit.Periapsis + ArrivalSpawnInnerBufferMeters,
                station.Orbit.Periapsis + station.ControlAreaHalfAltitudeMeters + ArrivalSpawnInnerBufferMeters));
        var maxSpawnAltitude = Math.Min(spawnApoapsis, CentralBody.ControlAltitudeMeters - ArrivalEdgeBufferMeters);
        if (maxSpawnAltitude < minSpawnAltitude)
        {
            maxSpawnAltitude = minSpawnAltitude;
        }

        var spawnAltitude = minSpawnAltitude + (_rng.NextDouble() * Math.Max(1d, maxSpawnAltitude - minSpawnAltitude));
        var spawnRadius = CentralBody.Radius + spawnAltitude;
        orbit.TrueAnomaly = GetInboundSpawnTrueAnomaly(orbit, spawnRadius);

        var ship = new Ship(orbit)
        {
            Name = $"Inbound-{ElapsedTimeSeconds:F0}",
            Destination = new StationDestination(station),
        };

        OrbitingObjects.Add(ship);
    }

    private Orbit CreateRandomInboundOrbit(Station station)
    {
        var stationAltitude = station.Orbit.Periapsis;
        var laneOffset = _rng.Next(0, 2) == 0 ? -ArrivalLaneOffsetMeters : ArrivalLaneOffsetMeters;
        var basePeriapsis = stationAltitude + laneOffset;
        var periapsisJitter = ((_rng.NextDouble() * 2d) - 1d) * InboundPeriapsisRandomRangeMeters;
        var minimumSpawnAltitude = GetMinimumSpawnAltitude();
        var periapsis = Math.Max(minimumSpawnAltitude, basePeriapsis + periapsisJitter);

        var isHighlyElliptical = _rng.NextDouble() < HighEllipseSpawnChance;
        var apoapsis = isHighlyElliptical
            ? CentralBody.ControlAltitudeMeters + (_rng.NextDouble() * InboundExitOvershootMeters)
            : Math.Max(periapsis + 100e3, CentralBody.ControlAltitudeMeters - ArrivalEdgeBufferMeters - (_rng.NextDouble() * 250e3));

        apoapsis = Math.Max(periapsis + 50e3, apoapsis);

        return new Orbit(
            apoapsis: apoapsis,
            periapsis: periapsis,
            argumentOfPeriapsis: _rng.NextDouble() * Math.PI * 2d,
            trueAnomaly: Math.PI);
    }

    private void SpawnOutboundShip(Station station, ShipTrafficLane lane)
    {
        var stationPosition = station.Orbit.PositionVectorD;
        var stationAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var stationRadius = stationPosition.Length();
        var departureAngle = stationRadius <= 0d ? 0d : station.ControlAreaDepartureExtentMeters / stationRadius;

        var isUpperDepartureLane = lane == ShipTrafficLane.StationDepartureUpper;
        var laneAngle = stationAngle + (isUpperDepartureLane ? -departureAngle : departureAngle);
        var targetApoapsis = Math.Max(
            GetMinimumSpawnAltitude(),
            station.Orbit.Periapsis + (isUpperDepartureLane ? DepartureDriftOffsetMeters : -DepartureDriftOffsetMeters));

        var ship = new Ship(new Orbit(
            apoapsis: targetApoapsis,
            periapsis: targetApoapsis,
            argumentOfPeriapsis: 0d,
            trueAnomaly: laneAngle))
        {
            Name = $"Outbound-{ElapsedTimeSeconds:F0}",
            Destination = new ExitControlAreaDestination(),
            TrafficLane = lane,
        };

        OrbitingObjects.Add(ship);
    }

    private double GetMinimumSpawnAltitude()
    {
        return PhysicalConstants.ATMOS_THICKNESS_TITAN + MinSpawnAltitudeAboveAtmosphereBufferMeters;
    }

    private List<ShipTrafficLane> GetAvailableDepartureLanes(Station station)
    {
        var availableLanes = new List<ShipTrafficLane>();
        foreach (var lane in new[] { ShipTrafficLane.StationDepartureUpper, ShipTrafficLane.StationDepartureLower })
        {
            if (CanSpawnOutboundShipInLane(station, lane))
            {
                availableLanes.Add(lane);
            }
        }

        return availableLanes;
    }

    private bool CanSpawnOutboundShipInLane(Station station, ShipTrafficLane lane)
    {
        var minimumClearDistance = GameConstants.ShipSepration + DepartureLaneSpawnBufferMeters;
        var laneSpawnPosition = GetOutboundLaneSpawnPosition(station, lane);

        foreach (var ship in Ships)
        {
            if (ship.Destination is not ExitControlAreaDestination || ship.TrafficLane != lane)
            {
                continue;
            }

            var distanceFromSpawnPoint = (ship.PositionD - laneSpawnPosition).Length();
            if (distanceFromSpawnPoint < minimumClearDistance)
            {
                return false;
            }
        }

        return true;
    }

    private DVector2 GetOutboundLaneSpawnPosition(Station station, ShipTrafficLane lane)
    {
        var stationPosition = station.Orbit.PositionVectorD;
        var stationAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var stationRadius = stationPosition.Length();
        var departureAngle = stationRadius <= 0d ? 0d : station.ControlAreaDepartureExtentMeters / stationRadius;

        var isUpperDepartureLane = lane == ShipTrafficLane.StationDepartureUpper;
        var laneAngle = stationAngle + (isUpperDepartureLane ? -departureAngle : departureAngle);
        var spawnAltitude = Math.Max(1d, station.Orbit.Periapsis + (isUpperDepartureLane ? DepartureDriftOffsetMeters : -DepartureDriftOffsetMeters));

        return new Orbit(
            apoapsis: spawnAltitude,
            periapsis: spawnAltitude,
            argumentOfPeriapsis: 0d,
            trueAnomaly: laneAngle).PositionVectorD;
    }

    private static double GetInboundSpawnTrueAnomaly(Orbit orbit, double spawnRadius)
    {
        if (orbit.Eccentricity <= 1e-8d)
        {
            return Math.PI;
        }

        var cosTrueAnomaly = ((orbit.SemiLatusRectum / spawnRadius) - 1d) / orbit.Eccentricity;
        cosTrueAnomaly = Math.Clamp(cosTrueAnomaly, -1d, 1d);

        var baseTrueAnomaly = Math.Acos(cosTrueAnomaly);
        return (2d * Math.PI) - baseTrueAnomaly;
    }

    private double NextOutboundSpawnDelaySeconds()
    {
        var difficultyScale = Math.Clamp(1d - (Score / 40d), 0.45d, 1.15d);
        var minDelay = 30d * difficultyScale;
        var maxDelay = 60d * difficultyScale;
        return minDelay + (_rng.NextDouble() * (maxDelay - minDelay));
    }

    private void RegisterSuccessfulDestination()
    {
        Score += ScorePerSuccessBase;
        ScoreMultiplier = Math.Min(MaxMultiplier, ScoreMultiplier + 1);
    }

    private void RegisterMistake()
    {
        Score -= ScorePenaltyPerMistake;
        Score = Math.Max(0d, Score);
        ScoreMultiplier = 1;
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
