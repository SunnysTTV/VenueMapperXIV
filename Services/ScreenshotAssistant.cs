#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Keys;
using Newtonsoft.Json;
using VenueMapper.Models;

namespace VenueMapper.Services;

public enum ShotTarget { MainWindow, Details, UpdateNotes, HouseRules, Chat }

public sealed record ShotSpec(string File, string Label, ShotTarget Target, string Hint);

public sealed class ScreenshotAssistant
{
    public static readonly ShotSpec[] Shots =
    [
        new("02-directory", "02 Directory", ShotTarget.MainWindow, "Directory tab. Hover a venue and press Pause to get the tooltip too."),
        new("03-map", "03 Map", ShotTarget.MainWindow, "Inside a venue. Opens the map and starts the guide to a bar on this floor."),
        new("05-festivals", "05 Festivals", ShotTarget.MainWindow, "Places the demo festival here if needed, opens the Festivals tab with the schedule unfolded."),
        new("06-passport", "06 Passport", ShotTarget.MainWindow, "Turns on the demo passport and opens the passport without the easter egg list."),
        new("08-house-rules", "08 House rules", ShotTarget.HouseRules, "Inside a venue. Shows the sample house rules card."),
        new("09-update-notes", "09 Update notes", ShotTarget.UpdateNotes, "Opens the Update Notes."),
        new("10-settings", "10 Settings", ShotTarget.MainWindow, "Opens the settings with the sidebar on Notifications."),
        new("11-details", "11 Details", ShotTarget.Details, "Opens Venue Details of the venue you are in."),
        new("12-chat", "12 Chat", ShotTarget.Chat, "Keep the game UI visible with an Echo-only chat tab. Prints two lines with venue links."),
    ];

    private const string DemoFestivalId = "moonpetal-lantern-fair-demo";
    private static readonly TimeSpan WindowDelay = TimeSpan.FromSeconds(1.6);
    private static readonly TimeSpan RetakeDelay = TimeSpan.FromSeconds(0.4);
    private static readonly TimeSpan HideDebugBefore = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan DismissBefore = TimeSpan.FromSeconds(0.35);

    private static readonly (string Id, int Visits, bool Night, int DaysAgo)[] DemoStamps =
    [
        ("gardenofmadness", 4, true, 30),
        ("churchofmetal", 2, false, 26),
        ("cryptonite", 1, false, 21),
        ("tempest", 3, false, 17),
        ("sodapop", 1, true, 12),
        ("flux", 2, false, 8),
        ("icecrowncitadel", 1, false, 4),
    ];

    private readonly VenueMapperPlugin plugin;
    private Pending? pending;
    private volatile bool saving;
    private bool pauseWasDown;

    private sealed class Pending
    {
        public required ShotSpec Spec;
        public DateTime CaptureAt;
        public DateTime? ActionAt;
        public Action? Action;
        public bool KeepToasts;
        public bool ReopenDebug;
        public bool DebugHidden;
        public bool Dismissed;
    }

    public ScreenshotAssistant(VenueMapperPlugin plugin)
    {
        this.plugin = plugin;
    }

    public string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "VenueMapper shots");
    public string Status { get; private set; } = "Ready";
    public int Selected { get; private set; } = -1;
    public bool Busy => pending != null || saving;

    public double SecondsLeft => pending == null ? 0 : Math.Max(0, (pending.CaptureAt - DateTime.Now).TotalSeconds);

    private Venue? CurrentVenue()
    {
        var config = plugin.ConfigManager.Config;
        return config == null ? null : plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config);
    }

    private void CloseWindows(bool keepMain = false)
    {
        if (!keepMain) plugin.VenueMapWindow.IsOpen = false;
        plugin.VenueDetailWindow.IsOpen = false;
        plugin.UpdateNotesWindow.IsOpen = false;
        plugin.NotificationHistoryWindow.IsOpen = false;
        plugin.VenueQuickPopupWindow.IsOpen = false;
        plugin.SettingsWindow.IsOpen = false;
        plugin.SetupWindow.IsOpen = false;
        plugin.OwnerSubmitWindow.IsOpen = false;
        plugin.OwnerUpdateWindow.IsOpen = false;
        plugin.OwnerVerifyWindow.IsOpen = false;
        plugin.FestivalSubmitWindow.IsOpen = false;
    }

    public void Take(int index)
    {
        if (Busy || index < 0 || index >= Shots.Length) return;
        var spec = Shots[index];
        Selected = index;
        plugin.ScreenshotMode = true;

        var job = new Pending
        {
            Spec = spec,
            CaptureAt = DateTime.Now + WindowDelay,
            ReopenDebug = plugin.DebugWindow.IsOpen,
        };

        if (!Stage(spec, job, out var problem))
        {
            Status = problem;
            return;
        }
        pending = job;
        Status = $"{spec.Label}: shot in {SecondsLeft:0} s";
    }

    public void Retake()
    {
        if (Busy || Selected < 0) return;
        var spec = Shots[Selected];
        pending = new Pending
        {
            Spec = spec,
            CaptureAt = DateTime.Now + RetakeDelay,
            ReopenDebug = plugin.DebugWindow.IsOpen,
            KeepToasts = spec.Target == ShotTarget.HouseRules,
        };
        Status = $"{spec.Label}: taking it again";
    }

    private bool Stage(ShotSpec spec, Pending job, out string problem)
    {
        problem = string.Empty;
        var venue = CurrentVenue();
        switch (spec.File)
        {
            case "02-directory":
                CloseWindows(true);
                plugin.VenueMapWindow.Stage("dir", new Vector2(520, 820));
                return true;
            case "03-map":
                if (venue == null) { problem = "Go inside a venue first."; return false; }
                CloseWindows(true);
                StartGuide(venue);
                plugin.VenueMapWindow.Stage("map", new Vector2(520, 760));
                return true;
            case "05-festivals":
                if (plugin.ConfigManager.DemoFestival == null && !PlaceDemoFestival(out problem)) return false;
                CloseWindows(true);
                plugin.VenueMapWindow.ExpandFestival(DemoFestivalId);
                plugin.VenueMapWindow.Stage("fes", new Vector2(520, 760));
                return true;
            case "06-passport":
                if (!SetDemoPassport(true, out problem)) return false;
                CloseWindows(true);
                plugin.VenueMapWindow.Stage("egg", new Vector2(560, 760));
                return true;
            case "08-house-rules":
                if (venue == null) { problem = "Go inside a venue first."; return false; }
                CloseWindows();
                job.KeepToasts = true;
                job.ActionAt = job.CaptureAt - TimeSpan.FromSeconds(1.0);
                job.Action = () =>
                {
                    plugin.Toasts.DismissAll(true);
                    plugin.HouseRules.ShowSample(venue);
                };
                return true;
            case "09-update-notes":
                CloseWindows();
                plugin.UpdateNotesWindow.Open();
                return true;
            case "10-settings":
                CloseWindows(true);
                plugin.SettingsWindow.SelectGroup(3);
                plugin.VenueMapWindow.Stage("set", new Vector2(760, 760));
                return true;
            case "11-details":
                if (venue == null) { problem = "Go inside a venue first."; return false; }
                CloseWindows();
                plugin.VenueDetailWindow.Stage(venue, new Vector2(430, 680));
                return true;
            case "12-chat":
                if (venue == null) { problem = "Go inside a venue first."; return false; }
                CloseWindows();
                job.ActionAt = job.CaptureAt - TimeSpan.FromSeconds(1.0);
                job.Action = () => PrintChatDemo(venue);
                return true;
        }
        problem = "Unknown shot.";
        return false;
    }

    private void StartGuide(Venue venue)
    {
        if (plugin.Guide.Active) return;
        var tracker = plugin.PositionTracker;
        var player = new Vector3(tracker.PlayerX, tracker.PlayerY, tracker.PlayerZ);
        var floor = tracker.IsInsideHouse ? tracker.GetCurrentFloor(venue) : null;
        var here = (floor?.Services ?? new List<Service>()).Where(s => Vector3.Distance(IndoorGuide.World(s), player) > 8f).ToList();
        var inside = venue.Floors.Where(f => f != floor && !HousingData.IsGarden(f.Name)).SelectMany(f => f.Services).ToList();
        static bool Spot(Service s) => s.Type is not ("upstairs" or "downstairs" or "entrance");
        var target = here.FirstOrDefault(s => s.Type == "bar")
                     ?? here.FirstOrDefault(Spot)
                     ?? inside.FirstOrDefault(s => s.Type == "bar")
                     ?? inside.FirstOrDefault(Spot)
                     ?? here.OrderByDescending(s => Vector3.Distance(IndoorGuide.World(s), player)).FirstOrDefault();
        if (target != null)
            plugin.Guide.Start(target, venue, null);
    }

    private void PrintChatDemo(Venue venue)
    {
        var district = PassportManager.District(venue);
        if (district.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) district = district[4..];
        if (plugin.Configuration.Language == "DE")
        {
            plugin.ChatLinks.PrintDemo($"Heute Abend Party bei {venue.Name}!");
            plugin.ChatLinks.PrintDemo($"Treffpunkt: {district} W{venue.Ward} P{venue.Plot}");
        }
        else
        {
            plugin.ChatLinks.PrintDemo($"Party tonight at {venue.Name}!");
            plugin.ChatLinks.PrintDemo($"Meet us at {district} W{venue.Ward} P{venue.Plot}");
        }
    }

    public bool SetDemoPassport(bool on, out string problem)
    {
        problem = string.Empty;
        if (!on)
        {
            plugin.DemoPassport = null;
            return true;
        }
        var venues = plugin.ConfigManager.Config?.Venues;
        if (venues == null || venues.Count == 0)
        {
            problem = "No venue list loaded.";
            return false;
        }
        var book = new PassportBook();
        var real = plugin.Passport.Find(VenueMapperPlugin.PlayerState.ContentId);
        if (real != null)
            foreach (var (id, stamp) in real.Stamps)
                book.Stamps[id] = stamp;

        foreach (var (id, visits, night, daysAgo) in DemoStamps)
        {
            var venue = venues.FirstOrDefault(v => v.VenueId == id);
            if (venue == null || venue.Closed || book.Stamps.ContainsKey(id)) continue;
            var first = DateTime.Now.Date.AddDays(-daysAgo).AddHours(night ? 1.25 : 22.25);
            book.Stamps[id] = new PassportStamp
            {
                Name = venue.Name,
                Color = venue.Colors?.Primary ?? string.Empty,
                Color2 = venue.Colors?.Secondary ?? string.Empty,
                District = PassportManager.District(venue),
                Datacenter = venue.Datacenter,
                FirstVisit = first,
                LastVisit = first.AddDays(Math.Max(0, visits - 1) * 3),
                Visits = visits,
                Night = night,
            };
        }
        book.Badges = PassportManager.EarnedBadges(book, venues);
        plugin.DemoPassport = book;
        return true;
    }

    public bool PlaceDemoFestival(out string problem)
    {
        problem = string.Empty;
        var tracker = plugin.PositionTracker;
        var housing = plugin.HousingData;
        var center = new Vector3(tracker.PlayerX, tracker.PlayerY, tracker.PlayerZ);
        var location = new FestivalLocation { Radius = 60f };

        var inWard = tracker.CurrentWard >= 0 && tracker.CurrentDistrict != HousingDistrict.Unknown && (tracker.IsInsideHouse || tracker.IsInHousingWard);
        if (inWard)
        {
            var territory = tracker.CurrentTerritoryId;
            var mapId = tracker.CurrentMapId;
            if (tracker.IsInsideHouse)
            {
                if (tracker.CurrentPlot < 0 || !housing.TryGetPlotMapLink(tracker.CurrentDistrict, tracker.CurrentPlot + 1, out territory, out mapId, out var plotPos))
                {
                    problem = "Step outside onto the plot first.";
                    return false;
                }
                center = plotPos;
            }
            location.Type = FestivalLocation.WardType;
            location.District = HousingData.EnglishName(tracker.CurrentDistrict);
            location.Ward = tracker.CurrentWard + 1;
            location.TerritoryId = territory;
            location.MapId = mapId;
            location.PlaceName = housing.TerritoryName(territory);
            if (tracker.CurrentPlot >= 0)
                location.Houses.Add(new FestivalHouse { Plot = tracker.CurrentPlot + 1 });
        }
        else
        {
            location.Type = FestivalLocation.WorldType;
            location.TerritoryId = tracker.CurrentTerritoryId;
            location.MapId = tracker.CurrentMapId;
            location.PlaceName = housing.TerritoryName(tracker.CurrentTerritoryId);
            location.AetheryteId = housing.NearestAetheryte(tracker.CurrentTerritoryId, center.X, center.Z).Id;
        }
        location.X = center.X;
        location.Y = center.Z;
        location.Z = center.Y;

        var world = tracker.CurrentServerName;
        var datacenter = ServerData.DatacenterServers.FirstOrDefault(kv => kv.Value.Contains(world, StringComparer.OrdinalIgnoreCase)).Key ?? string.Empty;
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(-1);
        var tomorrow = DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc);
        var days = new List<FestivalDay>
        {
            new() { Start = start, End = start.AddHours(5) },
            new() { Start = tomorrow.AddHours(18), End = tomorrow.AddHours(23) },
        };

        Service Stall(string type, string label, float dx, float dz) => new()
        {
            Type = type,
            Label = label,
            X = MathF.Round(center.X + dx, 2),
            Y = MathF.Round(center.Z + dz, 2),
            Z = MathF.Round(center.Y, 2),
        };

        plugin.ConfigManager.SetDemoFestival(new Festival
        {
            FestivalId = DemoFestivalId,
            Name = "Moonpetal Lantern Fair",
            Organizer = "lantern.crew",
            Description = "A cozy evening market with lanterns, tea and live music.",
            Datacenter = datacenter,
            Server = world,
            Start = days[0].Start,
            End = days[1].End,
            Days = days,
            Location = location,
            Colors = new VenueColors { Primary = "#f2a93b", Accent = "#2a9d8f", Secondary = "#2a9d8f" },
            Services =
            [
                Stall("stall", "Moonpetal Tea House", 7f, 2f),
                Stall("stage", "Lantern Stage", -6f, 5f),
                Stall("bar", "Glowfish Lemonade", 1f, -8f),
            ],
        });
        return true;
    }

    public void RemoveDemoFestival() => plugin.ConfigManager.SetDemoFestival(null);

    public void EndSession()
    {
        pending = null;
        plugin.DemoPassport = null;
        RemoveDemoFestival();
        if (plugin.Guide.Active) plugin.Guide.Stop();
        plugin.ScreenshotMode = false;
        Status = "Ended. Demo data removed, screenshot mode off.";
    }

    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            Process.Start(new ProcessStartInfo { FileName = Folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"Could not open the folder: {ex.Message}";
        }
    }

    private ScreenRect? TargetRect(ShotTarget target) => target switch
    {
        ShotTarget.Details => plugin.VenueDetailWindow.LastRect,
        ShotTarget.UpdateNotes => plugin.UpdateNotesWindow.LastRect,
        ShotTarget.HouseRules => plugin.HouseRules.LastRect,
        ShotTarget.Chat => null,
        _ => plugin.VenueMapWindow.LastRect,
    };

    public void Tick()
    {
        var keys = VenueMapperPlugin.KeyState;
        var down = keys.IsVirtualKeyValid(VirtualKey.PAUSE) && keys[VirtualKey.PAUSE];
        if (down && !pauseWasDown) Retake();
        pauseWasDown = down;

        var job = pending;
        if (job == null) return;
        var now = DateTime.Now;

        if (job.Action != null && job.ActionAt is { } actionAt && now >= actionAt)
        {
            var action = job.Action;
            job.Action = null;
            action();
        }
        if (!job.DebugHidden && now >= job.CaptureAt - HideDebugBefore)
        {
            job.DebugHidden = true;
            plugin.DebugWindow.IsOpen = false;
        }
        if (!job.Dismissed && !job.KeepToasts && now >= job.CaptureAt - DismissBefore)
        {
            job.Dismissed = true;
            plugin.Toasts.DismissAll(true);
        }
        if (now < job.CaptureAt) return;

        pending = null;
        var rect = TargetRect(job.Spec.Target);
        var frame = ScreenCapture.CaptureGameWindow(out var error);
        if (job.ReopenDebug) plugin.DebugWindow.IsOpen = true;
        if (frame == null)
        {
            Status = $"{job.Spec.Label}: capture failed, {error}.";
            return;
        }
        if (ScreenCapture.LooksBlack(frame))
        {
            Status = $"{job.Spec.Label}: the picture came out black. Switch the game to borderless window mode and try again.";
            return;
        }

        var path = NextPath(job.Spec.File);
        saving = true;
        Status = $"{job.Spec.Label}: saving...";
        Task.Run(() =>
        {
            try
            {
                ScreenCapture.SavePng(frame, path);
                var info = new
                {
                    shot = job.Spec.File,
                    image = new { width = frame.Width, height = frame.Height },
                    window = rect is { } r ? new { x = MathF.Round(r.X), y = MathF.Round(r.Y), width = MathF.Round(r.Width), height = MathF.Round(r.Height) } : null,
                };
                File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonConvert.SerializeObject(info, Formatting.Indented));
                Status = $"Saved {Path.GetFileName(path)} ({frame.Width} x {frame.Height})";
            }
            catch (Exception ex)
            {
                Status = $"{job.Spec.Label}: saving failed, {ex.Message}";
            }
            finally
            {
                saving = false;
            }
        });
    }

    private string NextPath(string name)
    {
        for (var i = 1; ; i++)
        {
            var path = Path.Combine(Folder, $"{name}-{i}.png");
            if (!File.Exists(path)) return path;
        }
    }
}
#endif
