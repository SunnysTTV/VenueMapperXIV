using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using VenueMapper.Models;

namespace VenueMapper.Services;

public class PlayerPositionTracker
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;

    public Vector3 LastPosition { get; private set; }
    public uint CurrentTerritoryId { get; private set; }
    public uint CurrentMapId { get; private set; }

    public float PlayerX { get; private set; }
    public float PlayerY { get; private set; }
    public float PlayerZ { get; private set; }

    public string CurrentFloorName { get; private set; } = "Unknown";
    public float CurrentFloorYMin { get; private set; }
    public float CurrentFloorYMax { get; private set; }

    private float _lastLoggedY;
    private string _lastFloor = "";

    public PlayerPositionTracker(IClientState clientState, IObjectTable objectTable, IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.log = log;
    }

    public void Update(VenueConfig? config = null)
    {
        var player = objectTable.LocalPlayer;
        if (player == null)
            return;

        LastPosition = player.Position;
        CurrentTerritoryId = clientState.TerritoryType;
        CurrentMapId = clientState.MapId;

        PlayerX = player.Position.X;
        PlayerY = player.Position.Y;
        PlayerZ = player.Position.Z;

        if (config == null)
            return;

        var venue = GetCurrentVenue(config);
        if (venue == null)
        {
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
                log.Information($"[VenueMapper] Floor changed: {_lastFloor} â†’ {floor.Name} (Y={PlayerY:F2})");
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

    public Venue? GetCurrentVenue(VenueConfig config)
    {
        foreach (var venue in config.Venues)
        {
            if (venue.TerritoryIds.Contains(CurrentTerritoryId))
                return venue;
        }

        return null;
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
