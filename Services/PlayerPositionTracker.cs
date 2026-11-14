using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using VenueMapper.Models;

namespace VenueMapper.Services;

public sealed class PlayerPositionTracker : IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPlayerState playerState;
    private readonly HousingData housing;
    private readonly IPluginLog log;
    private readonly Dictionary<string, HousingDistrict> venueDistricts = new();

    public Vector3 LastPosition { get; private set; }
    public uint CurrentTerritoryId { get; private set; }
    public uint CurrentMapId { get; private set; }
    public short CurrentWard { get; private set; } = -1;
    public short CurrentPlot { get; private set; } = -1;

    public float PlayerX { get; private set; }
    public float PlayerY { get; private set; }
    public float PlayerZ { get; private set; }

    public bool IsInsideHouse { get; private set; }
    public bool IsInHousingWard { get; private set; }
    public bool IsInWorkshop { get; private set; }
    public bool IsInPrivateChambers { get; private set; }

    private static readonly HashSet<uint> PrivateChambersTerritoryIds = new() { 983, 384, 652, 386, 385 };
    public string CurrentServerName { get; private set; } = "";

    public string CurrentDatacenter => string.IsNullOrEmpty(CurrentServerName)
        ? ""
        : ServerData.DatacenterServers.FirstOrDefault(kv =>
            kv.Value.Any(s => string.Equals(s, CurrentServerName, StringComparison.OrdinalIgnoreCase))).Key ?? "";
    public string CurrentHousingDistrict { get; private set; } = "";
    public HousingDistrict CurrentDistrict { get; private set; }
    public uint OriginalHouseTerritoryId { get; private set; }
    public string? CurrentMapFloor { get; private set; }
    public int IndoorFloorIndex { get; private set; } = -1;

    public string CurrentFloorName { get; private set; } = "Unknown";
    public float CurrentFloorYMin { get; private set; }
    public float CurrentFloorYMax { get; private set; }

    private HousingDistrict lastWardDistrict = HousingDistrict.Unknown;
    private uint cachedWorldId;
    private VenueConfig? cachedConfig;
    private Venue? currentVenue;
    private Venue? plotVenue;
    private Festival? festivalHere;
    private readonly List<Venue> wardVenues = new();
    private float lastLoggedY;
    private string lastFloor = "";

    public PlayerPositionTracker(IClientState clientState, IObjectTable objectTable, IPlayerState playerState,
        HousingData housing, IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.playerState = playerState;
        this.housing = housing;
        this.log = log;

        clientState.TerritoryChanged += OnTerritoryChanged;
        OnTerritoryChanged(clientState.TerritoryType);
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        if (HousingData.IsWardTerritory(territoryId))
            lastWardDistrict = housing.DistrictForTerritory(territoryId);
    }

    public void Update(VenueConfig? config = null)
    {
        cachedConfig = config;
        var player = objectTable.LocalPlayer;
        if (player == null)
        {
            currentVenue = null;
            plotVenue = null;
            festivalHere = null;
            wardVenues.Clear();
            return;
        }

        LastPosition = player.Position;
        UpdateWorld();

        CurrentTerritoryId = clientState.TerritoryType;
        CurrentMapId = clientState.MapId;

        PlayerX = player.Position.X;
        PlayerY = player.Position.Y;
        PlayerZ = player.Position.Z;

        UpdateHousingState();

        plotVenue = config != null ? ComputePlotVenue(config) : null;
        currentVenue = IsInsideHouse ? plotVenue : null;
        festivalHere = config != null ? ComputeFestivalHere(config) : null;
        wardVenues.Clear();
        if (config != null)
            FillWardVenues(config, wardVenues);

        if (config == null)
            return;

        if (currentVenue == null)
        {
            if (CurrentWard >= 0 && lastFloor != "none")
            {
                log.Debug($"[VenueMapper] No venue match: Territory={CurrentTerritoryId} District={CurrentDistrict} Ward={CurrentWard + 1} Plot={CurrentPlot + 1}");
                lastFloor = "none";
            }
            CurrentFloorName = "Unknown";
            return;
        }

        var floor = GetCurrentFloor(currentVenue);
        if (floor != null)
        {
            CurrentFloorName = floor.Name;
            CurrentFloorYMin = floor.YMin;
            CurrentFloorYMax = floor.YMax;

            if (floor.Name != lastFloor)
            {
                log.Debug($"[VenueMapper] Floor changed: {lastFloor} -> {floor.Name} (Map={CurrentMapId}, Y={PlayerY:F2})");
                lastFloor = floor.Name;
            }
        }
        else
        {
            CurrentFloorName = "Unknown";
        }

        if (MathF.Abs(PlayerY - lastLoggedY) > 1.0f)
        {
            log.Debug($"[VenueMapper] Y={PlayerY:F2} Territory={CurrentTerritoryId} Floor={CurrentFloorName}");
            lastLoggedY = PlayerY;
        }
    }

    private void UpdateWorld()
    {
        try
        {
            if (!playerState.IsLoaded) return;
            var world = playerState.CurrentWorld;
            if (world.RowId == 0 || world.RowId == cachedWorldId) return;
            var name = world.ValueNullable?.Name.ExtractText();
            if (string.IsNullOrEmpty(name)) return;
            CurrentServerName = name;
            cachedWorldId = world.RowId;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] World lookup failed: {ex.Message}");
        }
    }

    private unsafe void UpdateHousingState()
    {
        try
        {
            var hm = HousingManager.Instance();
            if (hm == null)
            {
                ResetHousingState();
                return;
            }

            CurrentWard = hm->GetCurrentWard();
            CurrentPlot = hm->GetCurrentPlot();
            IsInsideHouse = hm->IndoorTerritory != null;
            IsInHousingWard = hm->IsOutside();
            IsInWorkshop = hm->IsInWorkshop();
            IsInPrivateChambers = PrivateChambersTerritoryIds.Contains(CurrentTerritoryId);

            if (IsInsideHouse)
            {
                OriginalHouseTerritoryId = HousingManager.GetOriginalHouseTerritoryTypeId();
                IndoorFloorIndex = (int)hm->IndoorTerritory->CurrentFloor;
                var district = housing.DistrictForTerritory(OriginalHouseTerritoryId);
                CurrentDistrict = district != HousingDistrict.Unknown ? district : lastWardDistrict;
                CurrentMapFloor = housing.FloorForMap(CurrentMapId);
            }
            else
            {
                OriginalHouseTerritoryId = 0;
                IndoorFloorIndex = -1;
                CurrentDistrict = IsInHousingWard ? housing.DistrictForTerritory(CurrentTerritoryId) : HousingDistrict.Unknown;
                CurrentMapFloor = null;
            }

            CurrentHousingDistrict = HousingData.EnglishName(CurrentDistrict);
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Housing state read failed: {ex.Message}");
            ResetHousingState();
        }
    }

    private void ResetHousingState()
    {
        CurrentWard = -1;
        CurrentPlot = -1;
        IsInsideHouse = false;
        IsInHousingWard = false;
        IsInWorkshop = false;
        IsInPrivateChambers = false;
        CurrentDistrict = HousingDistrict.Unknown;
        CurrentHousingDistrict = "";
        OriginalHouseTerritoryId = 0;
        IndoorFloorIndex = -1;
        CurrentMapFloor = null;
    }

    private bool IsInVenueDatacenter(Venue venue)
    {
        if (string.IsNullOrEmpty(venue.Datacenter) || string.IsNullOrEmpty(CurrentServerName))
            return true;
        return ServerData.DatacenterServers.TryGetValue(venue.Datacenter, out var servers)
            && servers.Any(s => string.Equals(s, CurrentServerName, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsOnVenueServer(Venue venue)
    {
        if (string.IsNullOrEmpty(venue.Server) || string.IsNullOrEmpty(CurrentServerName))
            return true;
        return string.Equals(venue.Server, CurrentServerName, StringComparison.OrdinalIgnoreCase);
    }

    public HousingDistrict GetVenueDistrict(Venue venue)
    {
        var address = venue.Address ?? "";
        if (!venueDistricts.TryGetValue(address, out var district))
        {
            district = HousingData.ParseDistrict(address);
            venueDistricts[address] = district;
        }
        return district;
    }

    private bool IsInVenueDistrict(Venue venue)
    {
        if (CurrentDistrict == HousingDistrict.Unknown) return true;
        var district = GetVenueDistrict(venue);
        return district == HousingDistrict.Unknown || district == CurrentDistrict;
    }

    private Venue? ComputePlotVenue(VenueConfig config)
    {
        if (IsInWorkshop || IsInPrivateChambers) return null;
        if (!IsInsideHouse && !IsInHousingWard) return null;
        if (CurrentWard < 0 || CurrentPlot < 0) return null;

        foreach (var venue in config.Venues)
        {
            if (venue.Ward != CurrentWard + 1 || venue.Plot != CurrentPlot + 1) continue;
            if (!IsOnVenueServer(venue) || !IsInVenueDatacenter(venue) || !IsInVenueDistrict(venue)) continue;
            return venue;
        }

        return null;
    }

    public Venue? GetVenueAtCurrentPlotIncludingGarden(VenueConfig config)
        => ReferenceEquals(config, cachedConfig) ? plotVenue : ComputePlotVenue(config);

    public IReadOnlyList<Venue> GetWardVenues(VenueConfig config)
    {
        if (ReferenceEquals(config, cachedConfig)) return wardVenues;
        var list = new List<Venue>();
        FillWardVenues(config, list);
        return list;
    }

    private void FillWardVenues(VenueConfig config, List<Venue> target)
    {
        if (IsInsideHouse || !IsInHousingWard || CurrentWard < 0) return;
        foreach (var venue in config.Venues)
        {
            if (venue.Ward != CurrentWard + 1) continue;
            if (!IsOnVenueServer(venue) || !IsInVenueDatacenter(venue) || !IsInVenueDistrict(venue)) continue;
            target.Add(venue);
        }
    }

    public Festival? GetFestivalHere(VenueConfig config)
        => ReferenceEquals(config, cachedConfig) ? festivalHere : ComputeFestivalHere(config);

    private Festival? ComputeFestivalHere(VenueConfig config)
    {
        if (config.Festivals.Count == 0) return null;
        var now = DateTime.UtcNow;
        foreach (var festival in config.Festivals)
        {
            if (now < festival.Start.AddHours(-1) || festival.IsOver(now)) continue;
            if (IsInsideHouse && !IsInFestivalHouse(festival)) continue;
            if (!string.IsNullOrEmpty(festival.Server) && !string.IsNullOrEmpty(CurrentServerName)
                && !string.Equals(festival.Server, CurrentServerName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsInsideHouse && !IsAtFestivalArea(festival) && !IsInFestivalHouse(festival)) continue;
            return festival;
        }
        return null;
    }

    private bool IsInFestivalHouse(Festival festival)
    {
        if (!festival.IsWard || festival.Location.Houses.Count == 0) return false;
        if (CurrentWard + 1 != festival.Location.Ward || CurrentPlot < 0) return false;
        var district = HousingData.ParseDistrict(festival.Location.District);
        if (district != HousingDistrict.Unknown && CurrentDistrict != HousingDistrict.Unknown && district != CurrentDistrict)
            return false;
        return festival.Location.Houses.Any(h => h.Plot == CurrentPlot + 1);
    }

    private bool IsAtFestivalArea(Festival festival)
    {
        var location = festival.Location;
        if (festival.IsWard)
        {
            if (!IsInHousingWard || CurrentWard + 1 != location.Ward) return false;
            var district = HousingData.ParseDistrict(location.District);
            if (district != HousingDistrict.Unknown && CurrentDistrict != HousingDistrict.Unknown && district != CurrentDistrict)
                return false;
        }
        else if (CurrentTerritoryId != location.TerritoryId)
        {
            return false;
        }

        var dx = PlayerX - location.X;
        var dz = PlayerZ - location.Y;
        return dx * dx + dz * dz <= location.Radius * location.Radius;
    }

    public Floor? GetCurrentFloor(Venue venue)
    {
        if (!IsInsideHouse)
            return venue.Floors.FirstOrDefault(f => HousingData.IsGarden(f.Name));

        if (CurrentMapFloor != null)
        {
            foreach (var floor in venue.Floors)
                if (string.Equals(floor.Name, CurrentMapFloor, StringComparison.OrdinalIgnoreCase))
                    return floor;
        }
        return GetFloorByHeight(venue);
    }

    public Floor? GetFloorByHeight(Venue venue)
    {
        var y = LastPosition.Y;
        Floor? first = null;
        foreach (var floor in venue.Floors)
        {
            if (HousingData.IsGarden(floor.Name)) continue;
            first ??= floor;
            if (y >= floor.YMin && y <= floor.YMax)
                return floor;
        }
        return first;
    }

    public void Dispose()
    {
        clientState.TerritoryChanged -= OnTerritoryChanged;
    }
}
