using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.Simulation.OrbitingObjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpaceTrafficController.Core;

public partial class GameState
{
    private static readonly JsonSerializerOptions AutoSaveJsonOptions = new()
    {
        WriteIndented = true,
    };

    public void InitOrLoadAutoSave()
    {
        if (!TryLoadAutoSave())
        {
            Init();
        }
    }

    public void SaveAutoSave()
    {
        if (OrbitingObjects is null)
        {
            return;
        }

        try
        {
            var snapshot = CreateSnapshot();
            var autoSavePath = GetAutoSavePath();
            var directory = Path.GetDirectoryName(autoSavePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot, AutoSaveJsonOptions);
            File.WriteAllText(autoSavePath, json);
        }
        catch
        {
            // Ignore autosave failures so shutdown is never blocked by file IO issues.
        }
    }

    private bool TryLoadAutoSave()
    {
        var autoSavePath = GetAutoSavePath();
        if (!File.Exists(autoSavePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(autoSavePath);
            var snapshot = JsonSerializer.Deserialize<GameStateSnapshot>(json, AutoSaveJsonOptions);
            if (snapshot is null || snapshot.Version != GameStateSnapshot.CurrentVersion)
            {
                return false;
            }

            Init();
            ApplySnapshot(snapshot);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetAutoSavePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "SpaceTrafficController", "autosave.json");
    }

    private GameStateSnapshot CreateSnapshot()
    {
        var orbitingObjects = OrbitingObjects ?? new List<HasOrbit>();
        var objectIndexLookup = orbitingObjects
            .Select((orbitingObject, index) => (orbitingObject, index))
            .ToDictionary(x => x.orbitingObject, x => x.index);

        return new GameStateSnapshot
        {
            CurrentViewMode = CurrentViewMode,
            ProjectedPanX = ProjectedPanX,
            IsProjectedCameraStationCentered = IsProjectedCameraStationCentered,
            ShowAllOrbits = ShowAllOrbits,
            ShowAllManeuvers = ShowAllManeuvers,
            ElapsedTimeSeconds = ElapsedTimeSeconds,
            WarpState = WarpState,
            IsPaused = IsPaused,
            IsCameraFocusedOnSelected = IsCameraFocusedOnSelected,
            HohmannTransferTargetAltitudeMeters = HohmannTransferTargetAltitudeMeters,
            IsHohmannTransferDialogOpen = IsHohmannTransferDialogOpen,
            IsHohmannTransferMouseTargetSelectionActive = IsHohmannTransferMouseTargetSelectionActive,
            HohmannTransferStartImmediate = HohmannTransferStartImmediate,
            Score = Score,
            ScoreMultiplier = ScoreMultiplier,
            TimeSinceOutboundSpawnSeconds = _timeSinceOutboundSpawnSeconds,
            NextOutboundSpawnDelaySeconds = _nextOutboundSpawnDelaySeconds,
            TimeSinceDepartureLaneUpperSpawnSeconds = _timeSinceDepartureLaneUpperSpawnSeconds,
            NextDepartureLaneUpperSpawnDelaySeconds = _nextDepartureLaneUpperSpawnDelaySeconds,
            TimeSinceDepartureLaneLowerSpawnSeconds = _timeSinceDepartureLaneLowerSpawnSeconds,
            NextDepartureLaneLowerSpawnDelaySeconds = _nextDepartureLaneLowerSpawnDelaySeconds,
            TimeSinceInboundSpawnSeconds = _timeSinceInboundSpawnSeconds,
            NextInboundSpawnDelaySeconds = _nextInboundSpawnDelaySeconds,
            SelectedOrbitingObjectIndex = GetOrbitingObjectIndexOrNull(SelectedOrbitingObject, objectIndexLookup),
            TargetOrbitingObjectIndex = GetOrbitingObjectIndexOrNull(TargetOrbitingObject, objectIndexLookup),
            OrbitingObjects = orbitingObjects.Select(orbitingObject => CreateOrbitingObjectSnapshot(orbitingObject, objectIndexLookup)).ToList(),
        };
    }

    private void ApplySnapshot(GameStateSnapshot snapshot)
    {
        CurrentViewMode = snapshot.CurrentViewMode;
        ProjectedPanX = snapshot.ProjectedPanX;
        IsProjectedCameraStationCentered = snapshot.IsProjectedCameraStationCentered;
        ShowAllOrbits = snapshot.ShowAllOrbits;
        ShowAllManeuvers = snapshot.ShowAllManeuvers;
        ElapsedTimeSeconds = Math.Max(0d, snapshot.ElapsedTimeSeconds);
        WarpState = Math.Clamp(snapshot.WarpState, 1, 12);
        IsPaused = snapshot.IsPaused;
        IsCameraFocusedOnSelected = snapshot.IsCameraFocusedOnSelected;
        HohmannTransferTargetAltitudeMeters = Math.Clamp(snapshot.HohmannTransferTargetAltitudeMeters, 0d, CentralBody.ControlAltitudeMeters);
        IsHohmannTransferDialogOpen = snapshot.IsHohmannTransferDialogOpen;
        IsHohmannTransferMouseTargetSelectionActive = snapshot.IsHohmannTransferMouseTargetSelectionActive;
        HohmannTransferStartImmediate = snapshot.HohmannTransferStartImmediate;
        Score = Math.Max(0d, snapshot.Score);
        ScoreMultiplier = Math.Clamp(snapshot.ScoreMultiplier, 1, MaxMultiplier);
        _timeSinceOutboundSpawnSeconds = Math.Max(0d, snapshot.TimeSinceOutboundSpawnSeconds);
        _nextOutboundSpawnDelaySeconds = snapshot.NextOutboundSpawnDelaySeconds > 0d ? snapshot.NextOutboundSpawnDelaySeconds : NextOutboundSpawnDelaySeconds();
        _timeSinceDepartureLaneUpperSpawnSeconds = Math.Max(0d, snapshot.TimeSinceDepartureLaneUpperSpawnSeconds);
        _nextDepartureLaneUpperSpawnDelaySeconds = snapshot.NextDepartureLaneUpperSpawnDelaySeconds > 0d ? snapshot.NextDepartureLaneUpperSpawnDelaySeconds : NextDepartureLaneSpawnDelaySeconds();
        _timeSinceDepartureLaneLowerSpawnSeconds = Math.Max(0d, snapshot.TimeSinceDepartureLaneLowerSpawnSeconds);
        _nextDepartureLaneLowerSpawnDelaySeconds = snapshot.NextDepartureLaneLowerSpawnDelaySeconds > 0d ? snapshot.NextDepartureLaneLowerSpawnDelaySeconds : NextDepartureLaneSpawnDelaySeconds();
        _timeSinceInboundSpawnSeconds = Math.Max(0d, snapshot.TimeSinceInboundSpawnSeconds);
        _nextInboundSpawnDelaySeconds = snapshot.NextInboundSpawnDelaySeconds > 0d ? snapshot.NextInboundSpawnDelaySeconds : NextInboundSpawnDelaySeconds();

        OrbitingObjects.Clear();
        var restoredObjects = new List<HasOrbit>();
        var deferredShipDestinations = new List<(Ship Ship, DestinationSnapshot Destination)>();

        foreach (var snapshotObject in snapshot.OrbitingObjects)
        {
            var restoredObject = CreateOrbitingObjectFromSnapshot(snapshotObject, deferredShipDestinations);
            if (restoredObject is null)
            {
                continue;
            }

            restoredObjects.Add(restoredObject);
            OrbitingObjects.Add(restoredObject);
        }

        foreach (var deferredDestination in deferredShipDestinations)
        {
            deferredDestination.Ship.Destination = RestoreDestination(deferredDestination.Destination, restoredObjects);
        }

        SelectedOrbitingObject = GetOrbitingObjectByIndex(snapshot.SelectedOrbitingObjectIndex, restoredObjects);
        if (SelectedOrbitingObject is not null)
        {
            SelectedOrbitingObject.IsSelected = true;
        }

        TargetOrbitingObject = GetOrbitingObjectByIndex(snapshot.TargetOrbitingObjectIndex, restoredObjects);
    }

    private static int? GetOrbitingObjectIndexOrNull(HasOrbit orbitingObject, IReadOnlyDictionary<HasOrbit, int> objectIndexLookup)
    {
        if (orbitingObject is null)
        {
            return null;
        }

        return objectIndexLookup.TryGetValue(orbitingObject, out var index) ? index : null;
    }

    private OrbitingObjectSnapshot CreateOrbitingObjectSnapshot(HasOrbit orbitingObject, IReadOnlyDictionary<HasOrbit, int> objectIndexLookup)
    {
        var snapshot = new OrbitingObjectSnapshot
        {
            Kind = GetOrbitingObjectKind(orbitingObject),
            IsSelected = orbitingObject.IsSelected,
            Orbit = CreateOrbitSnapshot(orbitingObject.Orbit),
        };

        if (orbitingObject is Station station)
        {
            snapshot.Name = station.Name;
            snapshot.NumberOfRunways = station.NumberOfRunways;
            snapshot.ControlAreaHalfAltitudeMeters = station.ControlAreaHalfAltitudeMeters;
            snapshot.ControlAreaArrivalExtentMeters = station.ControlAreaArrivalExtentMeters;
            snapshot.ControlAreaDepartureExtentMeters = station.ControlAreaDepartureExtentMeters;
        }
        else if (orbitingObject is Ship ship)
        {
            snapshot.Name = ship.Name;
            snapshot.ShipState = ship.State;
            snapshot.Status = CreateShipStatusSnapshot(ship.Status);
            snapshot.ManeuverNode = CreateManeuverNodeSnapshot(ship.ManeuverNode);
            snapshot.NextManeuverNode = CreateManeuverNodeSnapshot(ship.NextManeuverNode);
            snapshot.Destination = CreateDestinationSnapshot(ship.Destination, objectIndexLookup);
            snapshot.TrafficLane = ship.TrafficLane;
        }

        return snapshot;
    }

    private static HasOrbit CreateOrbitingObjectFromSnapshot(OrbitingObjectSnapshot snapshot, List<(Ship Ship, DestinationSnapshot Destination)> deferredShipDestinations)
    {
        var orbit = CreateOrbitFromSnapshot(snapshot.Orbit);
        if (orbit is null)
        {
            return null;
        }

        HasOrbit restoredObject = snapshot.Kind switch
        {
            OrbitingObjectKind.Station => new Station(orbit)
            {
                Name = snapshot.Name,
                NumberOfRunways = snapshot.NumberOfRunways,
                ControlAreaHalfAltitudeMeters = snapshot.ControlAreaHalfAltitudeMeters,
                ControlAreaArrivalExtentMeters = snapshot.ControlAreaArrivalExtentMeters,
                ControlAreaDepartureExtentMeters = snapshot.ControlAreaDepartureExtentMeters,
            },
            OrbitingObjectKind.Ship => new Ship(orbit)
            {
                Name = snapshot.Name,
                State = snapshot.ShipState,
                Status = CreateShipStatusFromSnapshot(snapshot.Status),
                ManeuverNode = CreateManeuverNodeFromSnapshot(snapshot.ManeuverNode),
                NextManeuverNode = CreateManeuverNodeFromSnapshot(snapshot.NextManeuverNode),
                TrafficLane = snapshot.TrafficLane,
            },
            _ => null,
        };

        if (restoredObject is null)
        {
            return null;
        }

        restoredObject.IsSelected = snapshot.IsSelected;

        if (restoredObject is Ship ship && snapshot.Destination is not null)
        {
            deferredShipDestinations.Add((ship, snapshot.Destination));
        }

        return restoredObject;
    }

    private static OrbitingObjectKind GetOrbitingObjectKind(HasOrbit orbitingObject)
    {
        return orbitingObject switch
        {
            Station => OrbitingObjectKind.Station,
            Ship => OrbitingObjectKind.Ship,
            _ => throw new InvalidOperationException($"Unsupported orbiting object type: {orbitingObject.GetType().Name}"),
        };
    }

    private static OrbitSnapshot CreateOrbitSnapshot(Orbit orbit)
    {
        return new OrbitSnapshot
        {
            Apoapsis = orbit.Apoapsis,
            Periapsis = orbit.Periapsis,
            ArgumentOfPeriapsis = orbit.ArgumentOfPeriapsis,
            TrueAnomaly = orbit.TrueAnomaly,
            PreviousTrueAnomaly = orbit.PreviousTrueAnomaly,
            ExplicitEccentricity = orbit.ExplicitEccentricity,
        };
    }

    private static Orbit CreateOrbitFromSnapshot(OrbitSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new Orbit(
            snapshot.Apoapsis,
            snapshot.Periapsis,
            snapshot.ArgumentOfPeriapsis,
            snapshot.TrueAnomaly,
            snapshot.ExplicitEccentricity)
        {
            PreviousTrueAnomaly = snapshot.PreviousTrueAnomaly,
        };
    }

    private static ShipStatusSnapshot CreateShipStatusSnapshot(ShipStatus status)
    {
        if (status is null)
        {
            return new ShipStatusSnapshot();
        }

        return new ShipStatusSnapshot
        {
            IsSelected = status.IsSelected,
            IsEncroached = status.IsEncroached,
            WasEncroachedLastFrame = status.WasEncroachedLastFrame,
            IsControllable = status.IsControllable,
            IsInStationControlArea = status.IsInStationControlArea,
            IsLockedUntilOutsideStationControlArea = status.IsLockedUntilOutsideStationControlArea,
            IsStationArrivalHoldingForDespawn = status.IsStationArrivalHoldingForDespawn,
            HasEnteredStationControlAreaOnce = status.HasEnteredStationControlAreaOnce,
            StationArrivalDespawnTimerSeconds = status.StationArrivalDespawnTimerSeconds,
            ActivationFlashTimeRemainingSeconds = status.ActivationFlashTimeRemainingSeconds,
        };
    }

    private static ShipStatus CreateShipStatusFromSnapshot(ShipStatusSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return new ShipStatus();
        }

        return new ShipStatus
        {
            IsSelected = snapshot.IsSelected,
            IsEncroached = snapshot.IsEncroached,
            WasEncroachedLastFrame = snapshot.WasEncroachedLastFrame,
            IsControllable = snapshot.IsControllable,
            IsInStationControlArea = snapshot.IsInStationControlArea,
            IsLockedUntilOutsideStationControlArea = snapshot.IsLockedUntilOutsideStationControlArea,
            IsStationArrivalHoldingForDespawn = snapshot.IsStationArrivalHoldingForDespawn,
            HasEnteredStationControlAreaOnce = snapshot.HasEnteredStationControlAreaOnce,
            StationArrivalDespawnTimerSeconds = snapshot.StationArrivalDespawnTimerSeconds,
            ActivationFlashTimeRemainingSeconds = snapshot.ActivationFlashTimeRemainingSeconds,
        };
    }

    private static ManeuverNodeSnapshot CreateManeuverNodeSnapshot(ManeuverNode node)
    {
        if (node is null)
        {
            return null;
        }

        return new ManeuverNodeSnapshot
        {
            TrueAnomaly = node.TrueAnomaly,
            ProgradeDeltaV = node.ProgradeDeltaV,
            NormalDeltaV = node.NormalDeltaV,
            IsConfirmed = node.IsConfirmed,
        };
    }

    private static ManeuverNode CreateManeuverNodeFromSnapshot(ManeuverNodeSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new ManeuverNode
        {
            TrueAnomaly = snapshot.TrueAnomaly,
            ProgradeDeltaV = snapshot.ProgradeDeltaV,
            NormalDeltaV = snapshot.NormalDeltaV,
            IsConfirmed = snapshot.IsConfirmed,
        };
    }

    private static DestinationSnapshot CreateDestinationSnapshot(ShipDestination destination, IReadOnlyDictionary<HasOrbit, int> objectIndexLookup)
    {
        return destination switch
        {
            null => null,
            ExitControlAreaDestination => new DestinationSnapshot
            {
                Kind = DestinationKind.ExitControlArea,
            },
            StationDestination stationDestination => new DestinationSnapshot
            {
                Kind = DestinationKind.Station,
                StationIndex = GetOrbitingObjectIndexOrNull(stationDestination.Station, objectIndexLookup),
            },
            _ => throw new InvalidOperationException($"Unsupported ship destination type: {destination.GetType().Name}"),
        };
    }

    private static ShipDestination RestoreDestination(DestinationSnapshot snapshot, IReadOnlyList<HasOrbit> restoredObjects)
    {
        if (snapshot is null)
        {
            return null;
        }

        return snapshot.Kind switch
        {
            DestinationKind.ExitControlArea => new ExitControlAreaDestination(),
            DestinationKind.Station => GetOrbitingObjectByIndex(snapshot.StationIndex, restoredObjects) is Station station
                ? new StationDestination(station)
                : null,
            _ => null,
        };
    }

    private static HasOrbit GetOrbitingObjectByIndex(int? index, IReadOnlyList<HasOrbit> orbitingObjects)
    {
        if (!index.HasValue || index.Value < 0 || index.Value >= orbitingObjects.Count)
        {
            return null;
        }

        return orbitingObjects[index.Value];
    }

    private sealed class GameStateSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public ViewMode CurrentViewMode { get; set; }
        public float ProjectedPanX { get; set; }
        public bool IsProjectedCameraStationCentered { get; set; }
        public bool ShowAllOrbits { get; set; }
        public bool ShowAllManeuvers { get; set; }
        public double ElapsedTimeSeconds { get; set; }
        public int WarpState { get; set; }
        public bool IsPaused { get; set; }
        public bool IsCameraFocusedOnSelected { get; set; }
        public double HohmannTransferTargetAltitudeMeters { get; set; }
        public bool IsHohmannTransferDialogOpen { get; set; }
        public bool IsHohmannTransferMouseTargetSelectionActive { get; set; }
        public bool HohmannTransferStartImmediate { get; set; }
        public double Score { get; set; }
        public int ScoreMultiplier { get; set; }
        public double TimeSinceOutboundSpawnSeconds { get; set; }
        public double NextOutboundSpawnDelaySeconds { get; set; }
        public double TimeSinceDepartureLaneUpperSpawnSeconds { get; set; }
        public double NextDepartureLaneUpperSpawnDelaySeconds { get; set; }
        public double TimeSinceDepartureLaneLowerSpawnSeconds { get; set; }
        public double NextDepartureLaneLowerSpawnDelaySeconds { get; set; }
        public double TimeSinceInboundSpawnSeconds { get; set; }
        public double NextInboundSpawnDelaySeconds { get; set; }
        public int? SelectedOrbitingObjectIndex { get; set; }
        public int? TargetOrbitingObjectIndex { get; set; }
        public List<OrbitingObjectSnapshot> OrbitingObjects { get; set; } = new();
    }

    private sealed class OrbitingObjectSnapshot
    {
        public OrbitingObjectKind Kind { get; set; }
        public bool IsSelected { get; set; }
        public string Name { get; set; }
        public OrbitSnapshot Orbit { get; set; }
        public int NumberOfRunways { get; set; }
        public double ControlAreaHalfAltitudeMeters { get; set; } = Station.DefaultControlAreaHalfAltitudeMeters;
        public double ControlAreaArrivalExtentMeters { get; set; } = Station.DefaultControlAreaArrivalExtentMeters;
        public double ControlAreaDepartureExtentMeters { get; set; } = Station.DefaultControlAreaDepartureExtentMeters;
        public ShipState ShipState { get; set; }
        public ShipStatusSnapshot Status { get; set; }
        public ManeuverNodeSnapshot ManeuverNode { get; set; }
        public ManeuverNodeSnapshot NextManeuverNode { get; set; }
        public DestinationSnapshot Destination { get; set; }
        public ShipTrafficLane TrafficLane { get; set; }
    }

    private sealed class OrbitSnapshot
    {
        public double Apoapsis { get; set; }
        public double Periapsis { get; set; }
        public double ArgumentOfPeriapsis { get; set; }
        public double TrueAnomaly { get; set; }
        public double PreviousTrueAnomaly { get; set; }
        public double? ExplicitEccentricity { get; set; }
    }

    private sealed class ShipStatusSnapshot
    {
        public bool IsSelected { get; set; }
        public bool IsEncroached { get; set; }
        public bool WasEncroachedLastFrame { get; set; }
        public bool IsControllable { get; set; }
        public bool IsInStationControlArea { get; set; }
        public bool IsLockedUntilOutsideStationControlArea { get; set; }
        public bool IsStationArrivalHoldingForDespawn { get; set; }
        public bool HasEnteredStationControlAreaOnce { get; set; }
        public double StationArrivalDespawnTimerSeconds { get; set; }
        public double ActivationFlashTimeRemainingSeconds { get; set; }
    }

    private sealed class ManeuverNodeSnapshot
    {
        public double TrueAnomaly { get; set; }
        public double ProgradeDeltaV { get; set; }
        public double NormalDeltaV { get; set; }
        public bool IsConfirmed { get; set; }
    }

    private sealed class DestinationSnapshot
    {
        public DestinationKind Kind { get; set; }
        public int? StationIndex { get; set; }
    }

    private enum OrbitingObjectKind
    {
        Station = 1,
        Ship = 2,
    }

    private enum DestinationKind
    {
        ExitControlArea = 1,
        Station = 2,
    }
}
