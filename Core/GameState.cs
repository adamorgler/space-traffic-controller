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

public partial class GameState
{
    public enum ViewMode { Default, Projected }
    public ViewMode CurrentViewMode { get; set; } = ViewMode.Projected;
    public float ProjectedPanX { get; set; } = 0f;
    public bool IsProjectedCameraStationCentered { get; set; } = true;
    private const double ScorePerSuccessBase = 0.5d;
    private const double ScorePenaltyPerMistake = 1d;
    private const int MaxMultiplier = 8;
    private const double ArrivalEdgeBufferMeters = 15e3;
    private const double ArrivalLaneOffsetMeters = 50e3;
    private const double ArrivalSpawnInnerBufferMeters = 25e3;
    private const double DepartureDriftOffsetMeters = 100e3;
    private const double DepartureLaneSpawnBufferMeters = 30e3;
    private const double InboundExitOvershootMeters = 250e3;
    private const double InboundPeriapsisRandomRangeMeters = 450e3;
    private const double HighEllipseSpawnChance = 0.8d;
    private const double MinSpawnAltitudeAboveAtmosphereBufferMeters = 25e3;
    private const double DepartureSpawnControlAreaDepthFactor = 0.2d;
    private const double DepartureSpawnDriftDeltaMeters = 250e3;
    private const double StationArrivalDespawnHoldSeconds = 300d;
    private const double ActivationFlashDurationSeconds = 2.4d;

    private readonly Random _rng = new();
    private double _timeSinceOutboundSpawnSeconds;
    private double _nextOutboundSpawnDelaySeconds;
    private double _timeSinceDepartureLaneUpperSpawnSeconds;
    private double _nextDepartureLaneUpperSpawnDelaySeconds;
    private double _timeSinceDepartureLaneLowerSpawnSeconds;
    private double _nextDepartureLaneLowerSpawnDelaySeconds;
    private double _timeSinceInboundSpawnSeconds;
    private double _nextInboundSpawnDelaySeconds;

    public static CelestialBody CentralBody { get; set; }
    public List<HasOrbit> OrbitingObjects { get; set; }
    public List<Ship> Ships { get { return OrbitingObjects.OfType<Ship>().ToList(); } }
    public List<Station> Stations { get { return OrbitingObjects.OfType<Station>().ToList(); } }
    public HasOrbit SelectedOrbitingObject { get; set; }
    // An auxiliary right-click-selected orbiting object used for comparing/previewing
    public HasOrbit TargetOrbitingObject { get; set; }
    // Toggle to show/hide all orbit traces
    public bool ShowAllOrbits { get; set; } = false;
    // When true, render any craft with a maneuver node and its predicted orbit
    public bool ShowAllManeuvers { get; set; } = false;
    public Ship SelectedShip
    {
        get => SelectedOrbitingObject as Ship;
        set => SelectedOrbitingObject = value;
    }

    public double ElapsedTimeSeconds { get; private set; }
    public int CurrentWarpMultiplier => Warp;
    public bool IsPaused { get; private set; }
    public bool IsCameraFocusedOnSelected { get; set; }
    public double HohmannTransferTargetAltitudeMeters { get; set; }
    public bool IsHohmannTransferDialogOpen { get; set; } = false;
    public bool IsHohmannTransferMouseTargetSelectionActive { get; set; } = false;
    public bool HohmannTransferStartImmediate { get; set; } = false;
    public double Score { get; private set; }
    public int ScoreMultiplier { get; private set; }
    public int TargetActiveShips => GetTargetShipCount() * 2;

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
        IsCameraFocusedOnSelected = false;
        IsProjectedCameraStationCentered = true;
        HohmannTransferTargetAltitudeMeters = Math.Clamp(500e3, 0d, CentralBody.ControlAltitudeMeters);
        IsHohmannTransferMouseTargetSelectionActive = false;
        HohmannTransferStartImmediate = false;
        Score = 6d;
        ScoreMultiplier = 1;
        _timeSinceOutboundSpawnSeconds = 0d;
        _nextOutboundSpawnDelaySeconds = NextOutboundSpawnDelaySeconds();
        _timeSinceDepartureLaneUpperSpawnSeconds = 0d;
        _nextDepartureLaneUpperSpawnDelaySeconds = NextDepartureLaneSpawnDelaySeconds();
        _timeSinceDepartureLaneLowerSpawnSeconds = 0d;
        _nextDepartureLaneLowerSpawnDelaySeconds = NextDepartureLaneSpawnDelaySeconds();
        _timeSinceInboundSpawnSeconds = 0d;
        _nextInboundSpawnDelaySeconds = NextInboundSpawnDelaySeconds();

        InitializeScenario();
    }

    public void Update(GameTime gameTime)
    {
        var realElapsedSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        var timeStep = IsPaused ? 0d : realElapsedSeconds * Warp;
        ElapsedTimeSeconds += timeStep;

        foreach (var orbiter in OrbitingObjects)
        {
            orbiter.Update(timeStep);
        }

        var station = Stations.FirstOrDefault();

        // Update ship controllability based on control altitude and station-control-area rules.
        var controlRadius = CentralBody.ControlRadius;
        foreach (var ship in Ships)
        {
            var dist = ship.PositionD.Length();
            var wasControllable = ship.Status.IsControllable;
            var wasInStationControlArea = ship.Status.IsInStationControlArea;
            ship.Status.IsInStationControlArea = station is not null && IsInsideStationControlArea(ship.PositionD, station);
            var isControllable = ship.Orbit.IsEscapeTrajectory || dist < controlRadius;

            if (station is not null && ship.Status.IsLockedUntilOutsideStationControlArea)
            {
                if (!IsInsideStationControlArea(ship.PositionD, station))
                {
                    ship.Status.IsLockedUntilOutsideStationControlArea = false;
                    BeginActivationFlash(ship);
                }
                else
                {
                    isControllable = false;
                }
            }

            if (ship.Status.IsStationArrivalHoldingForDespawn)
            {
                isControllable = false;
            }

            ship.Status.IsControllable = isControllable;

            // Flash when entering station control area for the first time (inbound ships)
            if (!wasInStationControlArea && ship.Status.IsInStationControlArea && !ship.Status.HasEnteredStationControlAreaOnce)
            {
                ship.Status.HasEnteredStationControlAreaOnce = true;
                BeginActivationFlash(ship);
            }

            if (!wasControllable && isControllable)
            {
                BeginActivationFlash(ship);
            }

            // Decrement flash timer using real elapsed time (not warped time) so it displays consistently
            if (realElapsedSeconds > 0d && ship.Status.ActivationFlashTimeRemainingSeconds > 0d)
            {
                ship.Status.ActivationFlashTimeRemainingSeconds = Math.Max(0d, ship.Status.ActivationFlashTimeRemainingSeconds - realElapsedSeconds);
            }
        }

        CheckShipSeperation();
        RemoveDespawnedShips(timeStep);

        if (timeStep > 0d)
        {
            UpdateTrafficSpawning(timeStep);
        }
    }

    public int WarpState { get; set; } = 5;
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
                11 => 1024,
                12 => 2048,
                _ => 16
            };
        }
    }

    public void IncreaseWarp()
    {
        WarpState = Math.Clamp(WarpState + 1, 5, 12);
    }

    public void DecreaseWarp()
    {
        WarpState = Math.Clamp(WarpState - 1, 5, 12);
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

            // Ships inside station control area are intentionally exempt from separation conflicts.
            if (ship.Status.IsInStationControlArea)
            {
                continue;
            }

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

    private void RemoveDespawnedShips(double timeStep)
    {
        var despawnedShips = new List<Ship>();

        foreach (var ship in Ships)
        {
            var reachedDestination = ship.Destination?.HasArrived(ship) ?? false;
            var shouldRegisterSuccess = false;
            var shouldDespawn = false;

            if (ship.Destination is StationDestination)
            {
                if (reachedDestination && !ship.Status.IsStationArrivalHoldingForDespawn)
                {
                    ship.Status.IsStationArrivalHoldingForDespawn = true;
                    ship.Status.StationArrivalDespawnTimerSeconds = StationArrivalDespawnHoldSeconds;
                }

                if (ship.Status.IsStationArrivalHoldingForDespawn)
                {
                    ship.Status.StationArrivalDespawnTimerSeconds = Math.Max(0d, ship.Status.StationArrivalDespawnTimerSeconds - timeStep);
                    ship.Status.IsControllable = false;
                    if (ship.Status.StationArrivalDespawnTimerSeconds <= 0d)
                    {
                        shouldDespawn = true;
                        shouldRegisterSuccess = true;
                    }
                }
            }
            else if (reachedDestination)
            {
                shouldDespawn = true;
                shouldRegisterSuccess = true;
            }

            if (ship.ShouldDespawn())
            {
                shouldDespawn = true;
            }

            if (!shouldDespawn)
            {
                continue;
            }

            if (shouldRegisterSuccess)
            {
                RegisterSuccessfulDestination();
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

        const double stationAltitude = 1000e3;
        var station = new Station(new Orbit(stationAltitude, stationAltitude, 0d, 0d))
        {
            Name = "Port Atlas",
        };

        OrbitingObjects.Add(station);

        var targetPerFlow = GetTargetShipCount();
        for (int i = 0; i < targetPerFlow; i++)
        {
            SpawnInboundShip(station);
        }

        for (int i = 0; i < targetPerFlow; i++)
        {
            var availableDepartureLanes = GetAvailableDepartureLanes(station);
            if (availableDepartureLanes.Count == 0)
            {
                break;
            }

            var lane = availableDepartureLanes[_rng.Next(availableDepartureLanes.Count)];
            SpawnOutboundShip(station, lane);
        }
    }

    private void UpdateTrafficSpawning(double timeStep)
    {
        var station = Stations.FirstOrDefault();
        if (station is null)
        {
            return;
        }

        var targetPerFlow = GetTargetShipCount();
        var inboundCount = Ships.Count(ship => ship.Destination is StationDestination);
        var outboundCount = Ships.Count(ship => ship.Destination is ExitControlAreaDestination);

        // Update inbound spawn timer
        _timeSinceInboundSpawnSeconds += timeStep;
        if (inboundCount < targetPerFlow && _timeSinceInboundSpawnSeconds >= _nextInboundSpawnDelaySeconds)
        {
            SpawnInboundShip(station);
            inboundCount++;
            _timeSinceInboundSpawnSeconds = 0d;
            _nextInboundSpawnDelaySeconds = NextInboundSpawnDelaySeconds();
        }

        // Update upper departure lane spawn timer
        _timeSinceDepartureLaneUpperSpawnSeconds += timeStep;
        if (outboundCount < targetPerFlow && _timeSinceDepartureLaneUpperSpawnSeconds >= _nextDepartureLaneUpperSpawnDelaySeconds)
        {
            if (CanSpawnOutboundShipInLane(station, ShipTrafficLane.StationDepartureUpper))
            {
                SpawnOutboundShip(station, ShipTrafficLane.StationDepartureUpper);
                outboundCount++;
                _timeSinceDepartureLaneUpperSpawnSeconds = 0d;
                _nextDepartureLaneUpperSpawnDelaySeconds = NextDepartureLaneSpawnDelaySeconds();
            }
        }

        // Update lower departure lane spawn timer
        _timeSinceDepartureLaneLowerSpawnSeconds += timeStep;
        if (outboundCount < targetPerFlow && _timeSinceDepartureLaneLowerSpawnSeconds >= _nextDepartureLaneLowerSpawnDelaySeconds)
        {
            if (CanSpawnOutboundShipInLane(station, ShipTrafficLane.StationDepartureLower))
            {
                SpawnOutboundShip(station, ShipTrafficLane.StationDepartureLower);
                outboundCount++;
                _timeSinceDepartureLaneLowerSpawnSeconds = 0d;
                _nextDepartureLaneLowerSpawnDelaySeconds = NextDepartureLaneSpawnDelaySeconds();
            }
        }
    }

    private int GetTargetShipCount()
    {
        return Math.Max(1, (int)Math.Round(Score, MidpointRounding.AwayFromZero));
    }

    private void SpawnInboundShip(Station station)
    {
        Orbit orbit;
        const int maxSpawnAttempts = 12;
        int attempt = 0;
        do
        {
            orbit = CreateRandomInboundOrbit(station);
            // Ships spawn at apoapsis (outside control area), so we only need to verify
            // the periapsis passes through or near the station control area
            attempt++;
        }
        while (attempt < maxSpawnAttempts && IsInsideStationControlArea(orbit.PositionVectorD, station));

        var ship = new Ship(orbit)
        {
            Name = $"Inbound-{ElapsedTimeSeconds:F0}",
            Destination = new StationDestination(station),
        };

        BeginActivationFlash(ship);
        OrbitingObjects.Add(ship);
    }

    private Orbit CreateRandomInboundOrbit(Station station)
    {
        var stationAltitude = station.Orbit.Periapsis;
        var laneOffset = _rng.Next(0, 2) == 0 ? -ArrivalLaneOffsetMeters : ArrivalLaneOffsetMeters;
        var basePeriapsis = stationAltitude + laneOffset;
        var periapsisJitter = ((_rng.NextDouble() * 2d) - 1d) * InboundPeriapsisRandomRangeMeters;
        var minimumSpawnAltitude = GetMinimumSpawnAltitude();
        
        // Periapsis should be near the station (with some jitter)
        var periapsis = Math.Max(minimumSpawnAltitude, basePeriapsis + periapsisJitter);

        // Apoapsis should be outside the control area so the ship spawns there
        var controlAltitude = CentralBody.ControlAltitudeMeters;
        var spawnAltitudeAboveControl = 250e3 + (_rng.NextDouble() * InboundExitOvershootMeters);
        var apoapsis = controlAltitude + spawnAltitudeAboveControl;

        // Ensure apoapsis > periapsis
        apoapsis = Math.Max(periapsis + 50e3, apoapsis);

        return new Orbit(
            apoapsis: apoapsis,
            periapsis: periapsis,
            argumentOfPeriapsis: _rng.NextDouble() * Math.PI * 2d,
            trueAnomaly: Math.PI); // Start at apoapsis (outside control area)
    }

    private void SpawnOutboundShip(Station station, ShipTrafficLane lane)
    {
        var ship = new Ship(CreateOutboundSpawnOrbit(station, lane))
        {
            Name = $"Outbound-{ElapsedTimeSeconds:F0}",
            Destination = new ExitControlAreaDestination(),
            TrafficLane = lane,
        };

        ship.Status.IsLockedUntilOutsideStationControlArea = true;
        ship.Status.IsControllable = false;

        BeginActivationFlash(ship);
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
        return CreateOutboundSpawnOrbit(station, lane).PositionVectorD;
    }

    private Orbit CreateOutboundSpawnOrbit(Station station, ShipTrafficLane lane)
    {
        var stationPosition = station.Orbit.PositionVectorD;
        var stationAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var stationRadius = stationPosition.Length();
        var departureAngle = stationRadius <= 0d ? 0d : station.ControlAreaDepartureExtentMeters / stationRadius;

        var isUpperDepartureLane = lane == ShipTrafficLane.StationDepartureUpper;
        var laneAngle = stationAngle + (isUpperDepartureLane ? -departureAngle : departureAngle);
        var spawnAltitude = GetOutboundSpawnAltitude(station, lane);

        if (isUpperDepartureLane)
        {
            // Upper lane: spawn near periapsis and drift outward to +100 km.
            var periapsis = spawnAltitude;
            var apoapsis = Math.Max(periapsis + 1d, spawnAltitude + DepartureSpawnDriftDeltaMeters);
            return new Orbit(
                apoapsis: apoapsis,
                periapsis: periapsis,
                argumentOfPeriapsis: laneAngle,
                trueAnomaly: 0d);
        }

        // Lower lane: spawn near apoapsis and drift inward to -100 km.
        var targetPeriapsis = Math.Max(GetMinimumSpawnAltitude(), spawnAltitude - DepartureSpawnDriftDeltaMeters);
        var targetApoapsis = Math.Max(targetPeriapsis + 1d, spawnAltitude);
        return new Orbit(
            apoapsis: targetApoapsis,
            periapsis: targetPeriapsis,
            argumentOfPeriapsis: laneAngle - Math.PI,
            trueAnomaly: Math.PI);
    }

    private double GetOutboundSpawnAltitude(Station station, ShipTrafficLane lane)
    {
        var laneSign = lane == ShipTrafficLane.StationDepartureUpper ? 1d : -1d;
        var insideControlOffset = station.ControlAreaHalfAltitudeMeters * DepartureSpawnControlAreaDepthFactor;
        var spawnAltitude = station.Orbit.Periapsis + (laneSign * insideControlOffset);
        return Math.Max(GetMinimumSpawnAltitude(), spawnAltitude);
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

    private double NextDepartureLaneSpawnDelaySeconds()
    {
        var difficultyScale = Math.Clamp(1d - (Score / 40d), 0.45d, 1.15d);
        // Each lane has its own shorter delay to avoid synchronized spawning
        var minDelay = 15d * difficultyScale;
        var maxDelay = 45d * difficultyScale;
        return minDelay + (_rng.NextDouble() * (maxDelay - minDelay));
    }

    private double NextInboundSpawnDelaySeconds()
    {
        var difficultyScale = Math.Clamp(1d - (Score / 40d), 0.45d, 1.15d);
        // Inbound ships spawn on a staggered delay to feel more natural
        var minDelay = 5d * difficultyScale;
        var maxDelay = 20d * difficultyScale;
        return minDelay + (_rng.NextDouble() * (maxDelay - minDelay));
    }

    private static bool IsInsideStationControlArea(DVector2 shipPosition, Station station)
    {
        var stationPosition = station.Orbit.PositionVectorD;
        var stationRadius = stationPosition.Length();
        if (stationRadius <= 0d)
        {
            return false;
        }

        var shipRadius = shipPosition.Length();
        var innerRadius = Math.Max(1d, stationRadius - station.ControlAreaHalfAltitudeMeters);
        var outerRadius = stationRadius + station.ControlAreaHalfAltitudeMeters;
        if (shipRadius < innerRadius || shipRadius > outerRadius)
        {
            return false;
        }

        var arrivalAngle = station.ControlAreaArrivalExtentMeters / stationRadius;
        var departureAngle = station.ControlAreaDepartureExtentMeters / stationRadius;
        var stationAngle = Math.Atan2(stationPosition.Y, stationPosition.X);
        var shipAngle = Math.Atan2(shipPosition.Y, shipPosition.X);
        var stationVelocity = station.Orbit.VelocityVectorD;
        var motionSign = Math.Sign((stationPosition.X * stationVelocity.Y) - (stationPosition.Y * stationVelocity.X));
        if (motionSign == 0)
        {
            motionSign = 1;
        }

        var signedOffset = NormalizeSignedAngle(shipAngle - stationAngle) * motionSign;
        if (shipRadius >= stationRadius)
        {
            return signedOffset >= -departureAngle && signedOffset <= arrivalAngle;
        }

        return signedOffset >= -arrivalAngle && signedOffset <= departureAngle;
    }

    private static void BeginActivationFlash(Ship ship)
    {
        ship.Status.ActivationFlashTimeRemainingSeconds = ActivationFlashDurationSeconds;
    }

    private static double NormalizeSignedAngle(double angle)
    {
        angle %= 2d * Math.PI;
        if (angle > Math.PI)
        {
            angle -= 2d * Math.PI;
        }
        else if (angle < -Math.PI)
        {
            angle += 2d * Math.PI;
        }

        return angle;
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
