using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using VenueMapper.Models;

namespace VenueMapper.Services;

public class PlayerPositionTracker
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, string> districtNameCache = new();

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

    // FFXIVClientStructs' HousingManager has no dedicated flag for Private Chambers - it reuses the
    // same IndoorTerritory pointer as a normal house interior. These are the Private Chambers
    // TerritoryType IDs per district (Mist, Lavender Beds, Goblet, Shirogane, Empyreum), the same
    // approach Lifestream uses since no cleaner signal exists.
    private static readonly HashSet<uint> PrivateChambersTerritoryIds = new() { 983, 384, 652, 386, 385 };
    public string CurrentServerName { get; private set; } = "";
    public string CurrentHousingDistrict { get; private set; } = "";

    public string CurrentFloorName { get; private set; } = "Unknown";
    public float CurrentFloorYMin { get; private set; }
    public float CurrentFloorYMax { get; private set; }

    private float _lastLoggedY;
    private string _lastFloor = "";

    public PlayerPositionTracker(IClientState clientState, IObjectTable objectTable, IDataManager dataManager, IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dataManager = dataManager;
        this.log = log;
    }

    private string ResolveDistrictName(uint territoryId)
    {
        if (districtNameCache.TryGetValue(territoryId, out var cached))
            return cached;

        string name;
        try
        {
            var row = dataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId);
            name = row?.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] District name lookup failed for territory {territoryId}: {ex.Message}");
            name = "";
        }

        districtNameCache[territoryId] = name;
        return name;
    }

    public void Update(VenueConfig? config = null)
    {
        var player = objectTable.LocalPlayer;
        if (player == null)
            return;

        LastPosition = player.Position;

        try
        {
            var worldName = player.CurrentWorld.ValueNullable?.Name.ExtractText();
            if (!string.IsNullOrEmpty(worldName))
                CurrentServerName = worldName;
        }
        catch { }

        CurrentTerritoryId = clientState.TerritoryType;
        CurrentMapId = clientState.MapId;

        PlayerX = player.Position.X;
        PlayerY = player.Position.Y;
        PlayerZ = player.Position.Z;

        try
        {
            unsafe
            {
                var hm = HousingManager.Instance();
                if (hm != null)
                {
                    CurrentWard = hm->GetCurrentWard();
                    CurrentPlot = hm->GetCurrentPlot();
                    IsInsideHouse = hm->IndoorTerritory != null;
                    IsInHousingWard = hm->IsOutside();
                    IsInWorkshop = hm->IsInWorkshop();
                    IsInPrivateChambers = PrivateChambersTerritoryIds.Contains(CurrentTerritoryId);

                    var districtTerritoryId = IsInsideHouse
                        ? HousingManager.GetOriginalHouseTerritoryTypeId()
                        : CurrentTerritoryId;

                    CurrentHousingDistrict = ResolveDistrictName(districtTerritoryId);
                }
                else
                {
                    CurrentWard = -1;
                    CurrentPlot = -1;
                    IsInsideHouse = false;
                    IsInHousingWard = false;
                    IsInWorkshop = false;
                    IsInPrivateChambers = false;
                    CurrentHousingDistrict = "";
                }
            }
        }
        catch
        {
            CurrentWard = -1;
            CurrentPlot = -1;
            IsInsideHouse = false;
            IsInHousingWard = false;
            IsInWorkshop = false;
            IsInPrivateChambers = false;
            CurrentHousingDistrict = "";
        }

        if (config == null)
            return;

        var venue = GetCurrentVenue(config);
        if (venue == null)
        {
            if (CurrentWard >= 0 && _lastFloor != "none")
            {
                log.Warning($"[VenueMapper] No venue match: Territory={CurrentTerritoryId} Ward={CurrentWard}({CurrentWard+1}) Plot={CurrentPlot}({CurrentPlot+1})");
                _lastFloor = "none";
            }
            CurrentFloorName = "Unknown";
            return;
        }

        var floor = GetCurrentFloor(venue);
        if (floor != null)
        {
            CurrentFloorName = floor.Name;
            CurrentFloorYMin = floor.YMin;
            CurrentFloorYMax = floor.YMax;

            if (floor.Name != _lastFloor)
            {
                log.Information($"[VenueMapper] Floor changed: {_lastFloor} -> {floor.Name} (Y={PlayerY:F2})");
                _lastFloor = floor.Name;
            }
        }
        else
        {
            CurrentFloorName = "Unknown";
        }

        if (MathF.Abs(PlayerY - _lastLoggedY) > 1.0f)
        {
            log.Debug($"[VenueMapper] Y={PlayerY:F2} Territory={CurrentTerritoryId} Floor={CurrentFloorName}");
            _lastLoggedY = PlayerY;
        }
    }

    private bool IsInVenueDatacenter(Venue venue)
    {
        if (string.IsNullOrEmpty(venue.Datacenter) || string.IsNullOrEmpty(CurrentServerName))
            return true;
        return ServerData.DatacenterServers.TryGetValue(venue.Datacenter, out var servers)
            && servers.Any(s => string.Equals(s, CurrentServerName, StringComparison.OrdinalIgnoreCase));
    }

    // Datacenter alone isn't enough to disambiguate - ward/plot numbers repeat identically across
    // every server, so two venues in the same datacenter but different servers could otherwise match
    // each other. Falls back to true (no extra filtering) if a venue hasn't been backfilled with a
    // server value yet, so this never regresses existing data.
    private bool IsOnVenueServer(Venue venue)
    {
        if (string.IsNullOrEmpty(venue.Server) || string.IsNullOrEmpty(CurrentServerName))
            return true;
        return string.Equals(venue.Server, CurrentServerName, StringComparison.OrdinalIgnoreCase);
    }

    private Venue? FindVenueAtCurrentPlot(VenueConfig config)
    {
        if (CurrentWard < 0 || CurrentPlot < 0)
            return null;

        foreach (var venue in config.Venues)
        {
            if (!IsInVenueDatacenter(venue))
                continue;

            if (!IsOnVenueServer(venue))
                continue;

            if (venue.Ward > 0 && venue.Plot > 0
                && venue.Ward == CurrentWard + 1
                && venue.Plot == CurrentPlot + 1)
                return venue;
        }

        return null;
    }

    public Venue? GetCurrentVenue(VenueConfig config)
    {
        if (IsInWorkshop || IsInPrivateChambers) return null;
        if (!IsInsideHouse) return null;
        return FindVenueAtCurrentPlot(config);
    }

    public Venue? GetVenueAtCurrentPlotIncludingGarden(VenueConfig config)
    {
        if (IsInWorkshop || IsInPrivateChambers) return null;
        if (!IsInsideHouse && !(IsInHousingWard && CurrentPlot >= 0))
            return null;
        return FindVenueAtCurrentPlot(config);
    }

    public Floor? GetCurrentFloor(Venue venue)
    {
        var y = LastPosition.Y;

        foreach (var floor in venue.Floors)
        {
            if (y >= floor.YMin && y <= floor.YMax)
                return floor;
        }

        return venue.Floors.Count > 0 ? venue.Floors[0] : null;
    }
}
