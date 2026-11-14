using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using VenueMapper.Models;
using VenueMapper.UI;

namespace VenueMapper.Services;

public sealed record GuideRoute(IReadOnlyList<Vector3> Points, bool Complete, Vector3 Goal, bool Final, FontAwesomeIcon? WaypointIcon, string? WaypointText, IReadOnlyList<Vector4> Palette);

public sealed class IndoorGuide
{
    private const float ArriveDistance = 4.5f;
    private const float ArriveTouch = 1.5f;
    private const float ArriveHeight = 3f;
    private const float SightHeight = 1.6f;
    private static readonly TimeSpan LostGrace = TimeSpan.FromSeconds(12);

    private readonly VenueMapperPlugin plugin;
    private string? venueId;
    private string? festivalId;
    private string? targetFloor;
    private DateTime lostSince = DateTime.MaxValue;
    private List<Vector4> palette = new();

    public Service? Target { get; private set; }
    public string Hint { get; private set; } = string.Empty;
    public bool Active => Target != null;
    public GuideRoute? Current { get; private set; }

    public IndoorGuide(VenueMapperPlugin plugin)
    {
        this.plugin = plugin;
    }

    public static Vector3 World(Service service) => new(service.X, service.Z, service.Y);

    public bool IsTarget(Service service) => ReferenceEquals(Target, service);

    private static List<Vector4> Palette(VenueColors? colors)
    {
        if (colors == null) return new List<Vector4>();
        return new[] { colors.PrimaryVec, colors.AccentVec, colors.SecondaryVec }
            .Select(UIConstants.Readable)
            .Distinct()
            .ToList();
    }

    public void Start(Service service, Venue? venue, Festival? festival)
    {
        Target = service;
        venueId = venue?.VenueId;
        festivalId = venue == null ? festival?.FestivalId : null;
        var floor = venue?.Floors.FirstOrDefault(f => f.Services.Contains(service))?.Name;
        targetFloor = floor == null || HousingData.IsGarden(floor) ? null : floor;
        palette = Palette(venue?.Colors ?? festival?.Colors);
        lostSince = DateTime.MaxValue;
        Current = null;
        Hint = Lang.GuideFollow(Label(service));
        plugin.Toasts.Show(Lang.GuideStarted(Label(service)), ToastKind.Info, 3.0, detail: Lang.GuideStartedDetail);
    }

    public void Stop(bool notify = false)
    {
        if (Target == null) return;
        Target = null;
        venueId = null;
        festivalId = null;
        targetFloor = null;
        Hint = string.Empty;
        Current = null;
        if (notify)
            plugin.Toasts.Show(Lang.GuideStopped, ToastKind.Info, 2.5);
    }

    private static string Label(Service service) => string.IsNullOrWhiteSpace(service.Label) ? service.Type : service.Label;

    private static int Level(string? floor) => floor?.ToLowerInvariant() switch
    {
        "cellar" or "basement" => -1,
        "second" or "first" or "upstairs" => 1,
        "third" => 2,
        _ => 0,
    };

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private static Service? Nearest(Floor? floor, Vector3 player, params string[] types)
        => floor?.Services.Where(s => types.Contains(s.Type)).OrderBy(s => Flat(World(s), player)).FirstOrDefault();

    private static unsafe bool CanSee(Vector3 feet, Vector3 targetFeet)
    {
        try
        {
            var module = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->BGCollisionModule;
            if (module == null) return true;
            var from = feet + new Vector3(0f, SightHeight, 0f);
            var to = targetFeet + new Vector3(0f, SightHeight, 0f);
            var dir = to - from;
            var distance = dir.Length();
            if (distance < 0.01f) return true;
            dir /= distance;
            var flags = stackalloc int[] { 0, 0, 0, 0 };
            RaycastHit hit;
            return !module->RaycastMaterialFilter(&hit, &from, &dir, distance, -1, flags);
        }
        catch
        {
            return true;
        }
    }

    public void Tick(VenueConfig? config)
    {
        try
        {
            Current = Compute(config);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Debug($"[VenueMapper] Guide: {ex.Message}");
            Current = null;
        }
    }

    private GuideRoute? Compute(VenueConfig? config)
    {
        var target = Target;
        if (target == null || config == null) return null;

        var tracker = plugin.PositionTracker;
        var player = new Vector3(tracker.PlayerX, tracker.PlayerY, tracker.PlayerZ);
        var venue = venueId != null ? tracker.GetVenueAtCurrentPlotIncludingGarden(config) : null;
        var here = venueId != null
            ? venue?.VenueId == venueId
            : festivalId != null && tracker.GetFestivalHere(config)?.FestivalId == festivalId;

        if (!here)
        {
            var now = DateTime.Now;
            if (lostSince == DateTime.MaxValue) lostSince = now;
            else if (now - lostSince > LostGrace) Stop(notify: true);
            Hint = Lang.GuideLost;
            return null;
        }
        lostSince = DateTime.MaxValue;

        var goal = World(target);
        var playerFloor = venue != null && tracker.IsInsideHouse ? tracker.GetCurrentFloor(venue) : null;
        var playerFloorName = playerFloor?.Name;
        var bothInside = targetFloor != null && tracker.IsInsideHouse;
        var sameLevel = venue == null
                        || (targetFloor == null && !tracker.IsInsideHouse)
                        || (bothInside && (playerFloorName == null || playerFloorName == targetFloor));

        if (sameLevel)
        {
            var distance = Flat(player, goal);
            var level = MathF.Abs(player.Y - goal.Y) < ArriveHeight;
            if (level && (distance < ArriveTouch || (distance < ArriveDistance && CanSee(player, goal))))
            {
                plugin.Toasts.Show(Lang.GuideArrived(Label(target)), ToastKind.Success, 3.0);
                Stop();
                return null;
            }
            Hint = Lang.GuideFollow(Label(target));
            return new GuideRoute([goal], true, goal, true, null, null, palette);
        }

        if (targetFloor == null)
        {
            var exit = Nearest(playerFloor, player, "entrance")
                       ?? (Level(playerFloorName) < 0 ? Nearest(playerFloor, player, "upstairs") : Level(playerFloorName) > 0 ? Nearest(playerFloor, player, "downstairs") : null);
            Hint = Lang.GuideHeadOutside;
            return exit != null ? Waypoint(World(exit), FontAwesomeIcon.DoorOpen) : null;
        }

        if (!tracker.IsInsideHouse)
        {
            Hint = Lang.GuideGoInside;
            return null;
        }

        var floorLabel = VenueMapWindow.TranslateFloorName(targetFloor);
        var down = Level(targetFloor) < Level(playerFloorName);
        var stairs = Nearest(playerFloor, player, down ? "downstairs" : "upstairs") ?? Nearest(playerFloor, player, "upstairs", "downstairs");
        Hint = down ? Lang.GuideStairsDown(floorLabel) : Lang.GuideStairsUp(floorLabel);
        return stairs != null ? Waypoint(World(stairs), down ? FontAwesomeIcon.ArrowDown : FontAwesomeIcon.ArrowUp) : null;
    }

    private GuideRoute Waypoint(Vector3 position, FontAwesomeIcon icon)
        => new([position], true, position, false, icon, Hint, palette);
}
