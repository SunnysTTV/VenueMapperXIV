using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class FestivalSubmitWindow : HudWindow, IDisposable
{
    private static readonly string[] StallTypes = ["stall", "bar", "stage", "dj_booth", "event", "entrance", "gambling", "vip"];
    private static readonly string[] StallTypeLabels = Array.ConvertAll(StallTypes, VenueMapWindow.ChipLabel);
    private const int MaxDays = 14;

    private readonly VenueMapperPlugin plugin;

    private string name = "";
    private string organizer = "";
    private string description = "";
    private string selectedDc = "";
    private string selectedServer = "";
    private bool isNsfw;
    private Vector3 colorVec = new(0.89f, 0.76f, 0.49f);
    private Vector3 colorVec2 = new(0.34f, 0.84f, 0.93f);

    private string discordLink = "";
    private string partakeLink = "";
    private string websiteLink = "";

    private bool hasLocation;
    private readonly FestivalLocation location = new();

    private readonly List<DayEntry> days = new()
    {
        new DayEntry { Date = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
    };

    private readonly List<StallEntry> stalls = new();
    private double copyJsonStart;

    public FestivalSubmitWindow(VenueMapperPlugin plugin)
        : base(plugin, "FestivalSubmit", new Vector2(560, 680))
    {
        this.plugin = plugin;
    }

    protected override string Title => Lang.FestivalFormTitle;

    protected override void DrawBody()
    {
        using (ImRaii.TextWrapPos(0))
            ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalFormDesc);
        ImGui.Spacing();

        using var tabBar = ImRaii.TabBar("##festivalTabs");
        if (!tabBar) return;

        ScrollTab($"{Lang.FestivalInfoTab}##fInfo", DrawInfo);
        ScrollTab($"{Lang.FestivalWhereWhenTab}##fWhere", DrawWhereAndWhen);
        ScrollTab($"{Lang.FestivalStallsTab}##fStalls", DrawStalls);
        ScrollTab($"{Lang.FestivalSendTab}##fSend", DrawSend);
    }

    private static void ScrollTab(string label, Action draw)
    {
        using var tab = ImRaii.TabItem(label);
        if (!tab) return;

        using var scrollbar = UIConstants.ScrollbarStyle();
        using var childBg = ImRaii.PushColor(ImGuiCol.ChildBg, Vector4.Zero);
        using var child = ImRaii.Child($"##fscroll_{label}", new Vector2(0, 0));
        if (child)
            draw();
    }

    private static IDisposable FieldStyle() => UIConstants.Scope(
        ImRaii.PushColor(ImGuiCol.FrameBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.9f))
              .Push(ImGuiCol.Border, UIConstants.GlowDim),
        ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f));

    private static void Field(string label, string id, ref string value, string hint, int maxLength = 256)
    {
        ImGui.TextColored(UIConstants.TextSecondary, label);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint(id, hint, ref value, maxLength);
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Heading, title.ToUpperInvariant());
        var dl = ImGui.GetWindowDrawList();
        var y = ImGui.GetCursorScreenPos().Y;
        var x = ImGui.GetCursorScreenPos().X;
        dl.AddLine(new Vector2(x, y), new Vector2(x + ImGui.GetContentRegionAvail().X, y), ImGui.ColorConvertFloat4ToU32(UIConstants.Line), 1f);
        ImGui.Dummy(new Vector2(0, 4f));
    }

    private void DrawInfo()
    {
        ImGui.Spacing();
        using var field = FieldStyle();

        Field(Lang.FestivalName, "##fName", ref name, "");
        Field(Lang.FestivalOrganizer, "##fOrganizer", ref organizer, Lang.FestivalOrganizerHint);
        Field(Lang.Description, "##fDescription", ref description, "", 600);
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Datacenter);
        UIConstants.StyledCombo("##fDc", selectedDc.Length > 0 ? selectedDc : Lang.SelectHint, ImGui.GetContentRegionAvail().X, () =>
        {
            foreach (var dc in ServerData.AllDatacenters)
            {
                if (!ImGui.Selectable(dc, dc == selectedDc)) continue;
                selectedDc = dc;
                var servers = ServerData.GetServers(dc);
                selectedServer = servers.Length > 0 ? servers[0] : "";
            }
        });

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Server);
        var dcServers = ServerData.GetServers(selectedDc);
        UIConstants.StyledCombo("##fServer", selectedServer.Length > 0 ? selectedServer : Lang.SelectHint, ImGui.GetContentRegionAvail().X, () =>
        {
            foreach (var srv in dcServers)
                if (ImGui.Selectable(srv, srv == selectedServer))
                    selectedServer = srv;
        });

        ImGui.Spacing();
        UIConstants.Toggle("##fNsfw", ref isNsfw);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.FestivalNsfw);

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.VenueColors);
        ImGui.ColorEdit3("##fColor", ref colorVec, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoLabel);
        ImGui.SameLine(0, 8f);
        ImGui.ColorEdit3("##fColor2", ref colorVec2, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoLabel);

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.Links);
        Field("Discord Invite", "##fDiscord", ref discordLink, "https://discord.gg/...");
        Field("Partake", "##fPartake", ref partakeLink, "https://partake.gg/...");
        Field("Website", "##fWebsite", ref websiteLink, "https://...");
    }

    private bool InWardPlot(out PlayerPositionTracker tracker)
    {
        tracker = plugin.PositionTracker;
        return tracker.CurrentWard >= 0 && tracker.CurrentPlot >= 0 && tracker.CurrentDistrict != HousingDistrict.Unknown
               && (tracker.IsInsideHouse || tracker.IsInHousingWard);
    }

    private void DrawWhereAndWhen()
    {
        Section(Lang.FestivalWhere);
        var half = (ImGui.GetContentRegionAvail().X - 8f) / 2f;
        if (UIConstants.AccentButton($"{Lang.FestivalSetCenter}##fSetCenter", UIConstants.Glow, half))
            SetCenterFromPosition();
        ImGui.SameLine(0, 8f);
        var canAddHouse = InWardPlot(out _);
        if (UIConstants.AccentButton($"{Lang.FestivalAddHouse}##fAddHouse", UIConstants.Accent, half, !canAddHouse,
                tooltip: canAddHouse ? Lang.FestivalAddHouseTip : Lang.FestivalAddHouseNeedsPlot))
            AddCurrentHouse();
        ImGui.Spacing();

        if (!hasLocation)
        {
            using var wrap = ImRaii.TextWrapPos(0);
            ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalNoLocation);
        }
        else
        {
            DrawLocationDetails();
        }

        Section(Lang.FestivalWhen);
        DrawDays();
    }

    private void DrawLocationDetails()
    {
        ImGui.TextColored(UIConstants.Accent, LocationSummary());
        ImGui.TextColored(UIConstants.TextMuted, $"X {location.X:F1}  Z {location.Y:F1}");
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.FestivalRadius);
        ImGui.SetNextItemWidth(-1);
        var radius = location.Radius;
        if (ImGui.SliderFloat("##fRadius", ref radius, 10f, 200f, "%.0f y"))
            location.Radius = radius;

        var tracker = plugin.PositionTracker;
        if (tracker.CurrentTerritoryId == location.TerritoryId)
        {
            var dx = tracker.PlayerX - location.X;
            var dz = tracker.PlayerZ - location.Y;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            ImGui.TextColored(distance <= location.Radius ? UIConstants.Success : UIConstants.Warning, $"{distance:0} / {location.Radius:0} y");
        }

        if (location.Type == FestivalLocation.WorldType)
        {
            ImGui.Spacing();
            ImGui.TextColored(UIConstants.TextSecondary, Lang.FestivalAetheryte);
            var aetheryte = plugin.HousingData.AetheryteName(location.AetheryteId);
            ImGui.TextColored(UIConstants.TextPrimary, aetheryte.Length > 0 ? aetheryte : "-");
            return;
        }

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.FestivalHousesTitle);
        if (location.Houses.Count == 0)
        {
            using var wrap = ImRaii.TextWrapPos(0);
            ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalNoHouses);
            return;
        }

        using var field = FieldStyle();
        var remove = -1;
        for (var i = 0; i < location.Houses.Count; i++)
        {
            using var id = ImRaii.PushId($"house{i}");
            var house = location.Houses[i];
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(UIConstants.Accent, Lang.PlotLabel(house.Plot));
            ImGui.SameLine(110f);
            var label = house.Label;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 36f);
            if (ImGui.InputTextWithHint("##houseLabel", Lang.FestivalHouseLabelHint, ref label, 64))
                house.Label = label;
            ImGui.SameLine(0, 6f);
            if (UIConstants.AccentButton("X##houseDel", UIConstants.Danger, 28f))
                remove = i;
        }
        if (remove >= 0)
            location.Houses.RemoveAt(remove);
    }

    private string LocationSummary()
    {
        var server = selectedServer.Length > 0 ? $"{selectedServer} · " : "";
        if (location.Type == FestivalLocation.WardType)
            return $"{server}{location.District} · {Lang.WardLabel} {location.Ward}";
        var zone = plugin.HousingData.TerritoryName(location.TerritoryId);
        return $"{server}{(zone.Length > 0 ? zone : location.PlaceName)}";
    }

    private void FillServer()
    {
        var world = plugin.PositionTracker.CurrentServerName;
        if (string.IsNullOrEmpty(world)) return;
        foreach (var (dc, servers) in ServerData.DatacenterServers)
        {
            if (!servers.Contains(world, StringComparer.OrdinalIgnoreCase)) continue;
            selectedDc = dc;
            selectedServer = world;
            return;
        }
    }

    private void SetCenterFromPosition()
    {
        var tracker = plugin.PositionTracker;
        FillServer();

        if (tracker.IsInsideHouse)
        {
            if (!InWardPlot(out _) || !plugin.HousingData.TryGetPlotMapLink(tracker.CurrentDistrict, tracker.CurrentPlot + 1, out var territory, out var mapId, out var plotPos))
            {
                plugin.Toasts.Show(Lang.FestivalCenterInHouse, ToastKind.Warning, 3.5);
                return;
            }
            SetWard(tracker, territory, mapId, plotPos.X, plotPos.Z, plotPos.Y);
            AddHouse(tracker.CurrentPlot + 1);
            hasLocation = true;
            return;
        }

        var isWard = tracker.IsInHousingWard && tracker.CurrentWard >= 0 && tracker.CurrentDistrict != HousingDistrict.Unknown;
        if (isWard)
        {
            SetWard(tracker, tracker.CurrentTerritoryId, tracker.CurrentMapId, tracker.PlayerX, tracker.PlayerZ, tracker.PlayerY);
        }
        else
        {
            location.Type = FestivalLocation.WorldType;
            location.District = "";
            location.Ward = 0;
            location.Houses.Clear();
            location.TerritoryId = tracker.CurrentTerritoryId;
            location.MapId = tracker.CurrentMapId;
            location.X = tracker.PlayerX;
            location.Y = tracker.PlayerZ;
            location.Z = tracker.PlayerY;
            location.PlaceName = plugin.HousingData.TerritoryName(tracker.CurrentTerritoryId);
            location.AetheryteId = plugin.HousingData.NearestAetheryte(location.TerritoryId, location.X, location.Y).Id;
        }
        hasLocation = true;
    }

    private void SetWard(PlayerPositionTracker tracker, uint territory, uint mapId, float x, float z, float height)
    {
        var district = HousingData.EnglishName(tracker.CurrentDistrict);
        var ward = tracker.CurrentWard + 1;
        if (location.Type != FestivalLocation.WardType || location.Ward != ward || location.District != district)
            location.Houses.Clear();
        location.Type = FestivalLocation.WardType;
        location.District = district;
        location.Ward = ward;
        location.TerritoryId = territory;
        location.MapId = mapId;
        location.X = x;
        location.Y = z;
        location.Z = height;
        location.PlaceName = plugin.HousingData.TerritoryName(territory);
        location.AetheryteId = 0;
    }

    private void AddCurrentHouse()
    {
        if (!InWardPlot(out var tracker)) return;
        if (!hasLocation)
        {
            SetCenterFromPosition();
            AddHouse(tracker.CurrentPlot + 1);
            return;
        }
        if (location.Type != FestivalLocation.WardType || location.Ward != tracker.CurrentWard + 1
            || location.District != HousingData.EnglishName(tracker.CurrentDistrict))
        {
            plugin.Toasts.Show(Lang.FestivalHouseOtherWard, ToastKind.Warning, 3.5);
            return;
        }
        AddHouse(tracker.CurrentPlot + 1);
    }

    private void AddHouse(int plot)
    {
        if (location.Houses.Any(h => h.Plot == plot))
        {
            plugin.Toasts.Show(Lang.FestivalHouseAlready(plot), ToastKind.Info, 2.5);
            return;
        }
        location.Houses.Add(new FestivalHouse { Plot = plot });
        location.Houses.Sort((a, b) => a.Plot.CompareTo(b.Plot));
        plugin.Toasts.Show(Lang.FestivalHouseAdded(plot), ToastKind.Success, 2.5);
    }

    private static bool TryGetDay(DayEntry day, out FestivalDay result)
    {
        result = new FestivalDay();
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (!DateTime.TryParseExact($"{day.Date.Trim()} {day.Start.Trim()}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, styles, out var start))
            return false;
        if (!DateTime.TryParseExact($"{day.Date.Trim()} {day.End.Trim()}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, styles, out var end))
            return false;
        if (end <= start)
            end = end.AddDays(1);
        result = new FestivalDay { Start = start, End = end };
        return true;
    }

    private bool TryGetSchedule(out List<FestivalDay> schedule)
    {
        schedule = new List<FestivalDay>();
        foreach (var day in days)
        {
            if (!TryGetDay(day, out var parsed)) return false;
            schedule.Add(parsed);
        }
        schedule.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 1; i < schedule.Count; i++)
            if (schedule[i].Start < schedule[i - 1].End) return false;
        return schedule.Count > 0;
    }

    private void DrawDays()
    {
        using (ImRaii.TextWrapPos(0))
            ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalDaysHint);
        ImGui.Spacing();

        var avail = ImGui.GetContentRegionAvail().X;
        var timeW = 64f;
        var dateW = Math.Max(110f, avail - timeW * 2 - 36f - 24f);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.FestivalDate);
        ImGui.SameLine(dateW + 14f);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.FestivalStartShort);
        ImGui.SameLine(dateW + timeW + 22f);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.FestivalEndShort);

        using (FieldStyle())
        {
            var remove = -1;
            for (var i = 0; i < days.Count; i++)
            {
                using var id = ImRaii.PushId($"day{i}");
                var day = days[i];
                var valid = TryGetDay(day, out _);
                using (ImRaii.PushColor(ImGuiCol.Border, UIConstants.WithAlpha(UIConstants.Danger, 0.8f), !valid))
                {
                    ImGui.SetNextItemWidth(dateW);
                    ImGui.InputTextWithHint("##date", "2026-10-31", ref day.Date, 16);
                    ImGui.SameLine(0, 8f);
                    ImGui.SetNextItemWidth(timeW);
                    ImGui.InputTextWithHint("##start", "20:00", ref day.Start, 8);
                    ImGui.SameLine(0, 8f);
                    ImGui.SetNextItemWidth(timeW);
                    ImGui.InputTextWithHint("##end", "23:00", ref day.End, 8);
                }
                if (days.Count <= 1) continue;
                ImGui.SameLine(0, 8f);
                if (UIConstants.AccentButton("X##dayDel", UIConstants.Danger, 28f))
                    remove = i;
            }
            if (remove >= 0)
                days.RemoveAt(remove);
        }

        ImGui.Spacing();
        if (days.Count < MaxDays && UIConstants.AccentButton($"{Lang.FestivalAddDay}##fAddDay", UIConstants.Glow, -1))
            AddDay();
        ImGui.Spacing();

        if (TryGetSchedule(out var schedule))
        {
            for (var i = 0; i < schedule.Count; i++)
            {
                var d = schedule[i];
                var prefix = schedule.Count > 1 ? $"{Lang.FestivalDayOf(i + 1, schedule.Count)} · " : "";
                ImGui.TextColored(UIConstants.Accent,
                    $"{prefix}{d.Start.ToString("ddd, MMM d", Lang.Culture)} · {UIConstants.FormatServerTime(d.Start)} - {UIConstants.FormatServerTime(d.End)}");
            }
        }
        else
        {
            ImGui.TextColored(UIConstants.Danger, Lang.FestivalTimeInvalid);
        }
    }

    private void AddDay()
    {
        var last = days[^1];
        var next = new DayEntry { Start = last.Start, End = last.End };
        next.Date = DateTime.TryParseExact(last.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : last.Date;
        days.Add(next);
    }

    private void DrawStalls()
    {
        ImGui.Spacing();
        if (UIConstants.AccentButton($"{Lang.FestivalAddStall}##fAddStall", UIConstants.Glow, width: -1))
            stalls.Add(new StallEntry());
        ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalStallsHint);
        ImGui.Spacing();

        using var field = FieldStyle();
        using var colors = ImRaii.PushColor(ImGuiCol.Header, UIConstants.WithAlpha(UIConstants.Accent, 0.18f))
                                 .Push(ImGuiCol.HeaderHovered, UIConstants.WithAlpha(UIConstants.Accent, 0.28f))
                                 .Push(ImGuiCol.HeaderActive, UIConstants.WithAlpha(UIConstants.Accent, 0.38f));

        var removeIndex = -1;
        for (var i = 0; i < stalls.Count; i++)
        {
            using (ImRaii.PushId(i))
            {
                if (DrawStall(stalls[i], i))
                    removeIndex = i;
            }
            ImGui.Spacing();
        }
        if (removeIndex >= 0)
            stalls.RemoveAt(removeIndex);
    }

    private bool DrawStall(StallEntry stall, int index)
    {
        var label = stall.Name.Length > 0 ? stall.Name : $"#{index + 1}";
        if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.FramePadding))
            return false;

        using var indent = ImRaii.PushIndent(4f, scaled: false);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.ServiceType);
        UIConstants.StyledCombo("##sType", StallTypeLabels, ref stall.TypeIndex, ImGui.GetContentRegionAvail().X);
        Field(Lang.ServiceName, "##sName", ref stall.Name, Lang.ServiceNameHint);

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Coordinates);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.6f);
        ImGui.DragFloat3("##sCoords", ref stall.Coords, 0.1f);
        ImGui.SameLine();
        if (UIConstants.AccentButton($"{Lang.UseMyPos}##sPos", UIConstants.Glow))
        {
            var tracker = plugin.PositionTracker;
            stall.Coords = new Vector3(tracker.PlayerX, tracker.PlayerZ, tracker.PlayerY);
            stall.TerritoryId = tracker.CurrentTerritoryId;
        }

        if (hasLocation && stall.TerritoryId != 0 && IsOutsideArea(stall))
            ImGui.TextColored(UIConstants.Warning, Lang.FestivalStallOutside);

        return UIConstants.AccentButton($"{Lang.Delete}##sDel", UIConstants.Danger, width: -1);
    }

    private bool IsOutsideArea(StallEntry stall)
    {
        if (stall.TerritoryId != location.TerritoryId) return true;
        var dx = stall.Coords.X - location.X;
        var dz = stall.Coords.Y - location.Y;
        return dx * dx + dz * dz > location.Radius * location.Radius;
    }

    private bool IsValid(out bool timesValid)
    {
        timesValid = TryGetSchedule(out _);
        return name.Trim().Length > 0 && organizer.Trim().Length > 0 && hasLocation && timesValid && selectedServer.Length > 0;
    }

    private void DrawSend()
    {
        ImGui.Spacing();
        using var wrap = ImRaii.TextWrapPos(0);

        var valid = IsValid(out var timesValid);
        if (!valid)
        {
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Danger, 0.8f), Lang.FillRequired);
            if (!hasLocation) ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Danger, 0.8f), Lang.FestivalNoLocation);
            if (!timesValid) ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Danger, 0.8f), Lang.FestivalTimeInvalid);
            ImGui.Spacing();
        }

        var festival = valid ? BuildFestival() : null;
        Section(Lang.FestivalPreviewTitle);
        ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalPreviewDesc);
        ImGui.Spacing();
        var previewing = festival != null && plugin.ConfigManager.HasLocalFestival(festival.FestivalId);
        var half = (ImGui.GetContentRegionAvail().X - 8f) / 2f;
        if (UIConstants.AccentButton($"{(previewing ? Lang.FestivalPreviewUpdate : Lang.FestivalPreviewButton)}##fPreview", UIConstants.Glow,
                previewing ? half : -1, festival == null) && festival != null)
        {
            if (plugin.ConfigManager.SaveLocalFestival(festival))
                plugin.Toasts.Show(Lang.ToastFestivalPreview, ToastKind.Success, 3.5, detail: Lang.ToastFestivalPreviewDetail);
            else
                plugin.Toasts.Show(Lang.ToastFestivalPreviewFailed, ToastKind.Warning, 3.0);
        }
        if (previewing)
        {
            ImGui.SameLine(0, 8f);
            if (UIConstants.AccentButton($"{Lang.FestivalPreviewRemove}##fPreviewRemove", UIConstants.Danger, half)
                && plugin.ConfigManager.RemoveLocalFestival(festival!.FestivalId))
                plugin.Toasts.Show(Lang.ToastFestivalPreviewRemoved, ToastKind.Info, 2.5);
        }

        Section(Lang.FestivalSendTitle);
        ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalExportDesc);
        ImGui.Spacing();

        var copiedRecently = copyJsonStart > 0 && ImGui.GetTime() - copyJsonStart < 3.0;
        if (copiedRecently)
        {
            UIConstants.AccentButton($"{Lang.Copied}##fCopyJson", UIConstants.Success, width: -1, disabled: true);
        }
        else if (UIConstants.AccentButton($"{Lang.CopyJson}##fCopyJson", UIConstants.Accent, width: -1))
        {
            if (festival == null)
            {
                plugin.Toasts.Show(Lang.ToastRequiredFieldsMissing, ToastKind.Warning, 3.0);
            }
            else
            {
                ImGui.SetClipboardText(JsonConvert.SerializeObject(festival, Formatting.Indented));
                copyJsonStart = ImGui.GetTime();
                plugin.Toasts.Show(Lang.ToastJsonCopied, ToastKind.Success, 2.0);
            }
        }

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextMuted, Lang.Preview);
        using var childBg = ImRaii.PushColor(ImGuiCol.ChildBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.3f));
        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##fJson", new Vector2(-1, 200), true);
        if (!child) return;
        using var previewWrap = ImRaii.TextWrapPos(0);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.7f), JsonConvert.SerializeObject(BuildFestival(), Formatting.Indented));
    }

    private static string Slug(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private static string Hex(Vector3 c) => $"#{(int)(c.X * 255):x2}{(int)(c.Y * 255):x2}{(int)(c.Z * 255):x2}";

    private Festival BuildFestival()
    {
        TryGetSchedule(out var schedule);
        var start = schedule.Count > 0 ? schedule[0].Start : DateTime.UtcNow;
        var end = schedule.Count > 0 ? schedule.Max(d => d.End) : start.AddHours(3);
        return new Festival
        {
            FestivalId = $"{Slug(name)}-{start:yyyyMMdd}",
            Name = name.Trim(),
            Organizer = organizer.Trim(),
            Description = description.Trim(),
            Datacenter = selectedDc,
            Server = selectedServer,
            Nsfw = isNsfw,
            Start = start,
            End = end,
            Days = schedule.Count > 1 ? schedule : new List<FestivalDay>(),
            Location = location,
            Colors = new VenueColors { Primary = Hex(colorVec), Accent = Hex(colorVec2), Secondary = Hex(colorVec2) },
            Links = new VenueSocialLinks { Discord = discordLink.Trim(), Partake = partakeLink.Trim(), Website = websiteLink.Trim() },
            Services = stalls.Select(s => new Service
            {
                Type = StallTypes[Math.Clamp(s.TypeIndex, 0, StallTypes.Length - 1)],
                Label = s.Name.Trim(),
                X = MathF.Round(s.Coords.X, 2),
                Y = MathF.Round(s.Coords.Y, 2),
                Z = MathF.Round(s.Coords.Z, 2),
            }).ToList(),
        };
    }

    public void Dispose() { }

    private class DayEntry
    {
        public string Date = "";
        public string Start = "20:00";
        public string End = "23:00";
    }

    private class StallEntry
    {
        public int TypeIndex;
        public string Name = "";
        public Vector3 Coords;
        public uint TerritoryId;
    }
}
