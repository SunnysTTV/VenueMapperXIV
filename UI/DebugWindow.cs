using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class DebugWindow : Window, IDisposable
{
    private readonly PlayerPositionTracker tracker;
    private readonly VenueMapperPlugin plugin;
    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    public DebugWindow(PlayerPositionTracker tracker, VenueMapperPlugin plugin)
        : base("VenueMapper Debug##VenueMapperDebug", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.tracker = tracker;
        this.plugin  = plugin;
        IsOpen = false;
    }

    public override void PreDraw() => UIConstants.PushWindowChrome();
    public override void PostDraw() => UIConstants.PopWindowChrome();

    public override void Draw()
    {
        try { DrawContent(); }
        catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "[VenueMapper] DebugWindow draw failed"); }
    }

    private void DrawContent()
    {
        ImGui.TextColored(UIConstants.Heading, Lang.DebugInfo);
        ImGui.Separator();
        ImGui.Spacing();

        Line(Lang.TerritoryId, tracker.CurrentTerritoryId.ToString());
        Line(Lang.MapId, tracker.CurrentMapId.ToString());
        Line("Ward/Plot:", tracker.CurrentWard >= 0 ? $"W{tracker.CurrentWard + 1} P{tracker.CurrentPlot + 1}" : "N/A");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        Line("Server:", string.IsNullOrEmpty(tracker.CurrentServerName) ? "N/A" : tracker.CurrentServerName);
        Line("District:", string.IsNullOrEmpty(tracker.CurrentHousingDistrict) ? "N/A" : tracker.CurrentHousingDistrict);

        var isInGarden = tracker.IsInHousingWard && tracker.CurrentPlot >= 0;
        var locationLabel = tracker.IsInsideHouse ? "Inside"
            : isInGarden ? "Garden"
            : "Not in Housing";
        var locationColor = tracker.IsInsideHouse ? UIConstants.Glow
            : isInGarden ? UIConstants.Accent
            : UIConstants.TextPrimary;
        Line("Location:", locationLabel, locationColor);

        var matchedVenue = plugin.ConfigManager.Config != null
            ? plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(plugin.ConfigManager.Config)
            : null;
        Line("Matched Venue:", matchedVenue?.Name ?? "None",
            matchedVenue != null ? UIConstants.Glow : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f));

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.PlayerPos);
        ImGui.TextColored(UIConstants.TextPrimary, $"  X: {tracker.PlayerX:F2}");
        ImGui.TextColored(UIConstants.TextPrimary, $"  Y: {tracker.PlayerZ:F2}");
        ImGui.TextColored(UIConstants.TextPrimary, $"  Z: {tracker.PlayerY:F2}");

        ImGui.Spacing();
        Line(Lang.CurrentFloor, tracker.CurrentFloorName.ToUpperInvariant(), UIConstants.Glow);

#if DEBUG
        DrawDeveloperTools(matchedVenue);
#endif

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (UIConstants.AccentButton("COPY COORDS"))
        {
            ImGui.SetClipboardText($"X: {tracker.PlayerX:F2}, Y: {tracker.PlayerZ:F2}, Z: {tracker.PlayerY:F2}");
            plugin.Toasts.Show("Coordinates copied", ToastKind.Success, 1.8);
        }

        ImGui.SameLine();

        if (UIConstants.AccentButton("COPY TERRITORY"))
        {
            ImGui.SetClipboardText(tracker.CurrentTerritoryId.ToString());
            plugin.Toasts.Show("Territory ID copied", ToastKind.Success, 1.8);
        }

        if (UIConstants.AccentButton("COPY OWNER ID JSON", disabled: matchedVenue == null,
                tooltip: "Make sure you're standing inside YOUR OWN venue first -\nthis registers whichever venue is currently matched at your position.")
            && matchedVenue != null)
        {
            var hash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
            var json = $"{{ \"venueId\": \"{matchedVenue.VenueId}\", \"ownerIdHash\": \"{hash}\" }}";
            ImGui.SetClipboardText(json);
            plugin.Toasts.Show("Owner ID JSON copied - paste it in a Discord DM to sunnysofficial", ToastKind.Success, 3.0);
        }

        HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName);
    }

    private static void Line(string label, string value, System.Numerics.Vector4? color = null)
    {
        ImGui.TextColored(UIConstants.TextSecondary, label);
        ImGui.SameLine();
        ImGui.TextColored(color ?? UIConstants.TextPrimary, value);
    }

#if DEBUG
    private string shotFolder = string.Empty;

    private void DrawDeveloperTools(Models.Venue? matchedVenue)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(UIConstants.Heading, "DEVELOPER");

        ImGui.TextColored(UIConstants.TextSecondary, $"Z Range: {tracker.CurrentFloorYMin:F1} - {tracker.CurrentFloorYMax:F1}");
        Line("Floor (game map):", tracker.CurrentMapFloor ?? "-",
            tracker.CurrentMapFloor != null ? UIConstants.Glow : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f));
        Line("Floor (height):", (matchedVenue != null ? tracker.GetFloorByHeight(matchedVenue)?.Name : null) ?? "-");
        Line("Game floor index:", tracker.IndoorFloorIndex >= 0 ? tracker.IndoorFloorIndex.ToString() : "-");
        Line("Original house territory:", tracker.OriginalHouseTerritoryId > 0 ? tracker.OriginalHouseTerritoryId.ToString() : "-");

        var nearestPlot = tracker.CurrentDistrict != HousingDistrict.Unknown && !tracker.IsInsideHouse
            ? plugin.HousingData.NearestPlot(tracker.CurrentDistrict, tracker.PlayerX, tracker.PlayerZ)
            : 0;
        Line("Nearest plot sign:",
            nearestPlot > 0 && plugin.VenueSignPosition(tracker.CurrentDistrict, nearestPlot, out var signPos)
                ? $"Plot {nearestPlot} at height {signPos.Y:F1} (you: {tracker.PlayerY:F1})"
                : "-");
        Line("Game map:", plugin.GameMapMarkers.DebugInfo());
        Line("Map house icons:", plugin.GameMapMarkers.HouseIconInfo);
        Line("Placards:", plugin.Placards.DebugInfo);
        Line("Marker label textures:", plugin.PictomancyMarkers.CachedTextures.ToString());

        var outdoorDistrict = tracker.IsInsideHouse ? null : tracker.CurrentHousingDistrict;
        var mapInfo = plugin.MapLoader.GetMapInfoByMapId(tracker.CurrentMapId)
                      ?? plugin.MapLoader.GetMapInfo(tracker.CurrentTerritoryId, outdoorDistrict);
        ImGui.TextColored(UIConstants.TextSecondary, "Map Path:");
        ImGui.TextColored(mapInfo.Path != null ? UIConstants.Glow : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            mapInfo.Path ?? "(no map for this territory)");
        if (mapInfo.Path != null)
        {
            var tex = plugin.MapLoader.GetMapTexture(tracker.CurrentTerritoryId, tracker.CurrentMapId, outdoorDistrict);
            var loaded = tex != null && tex.TryGetWrap(out var w, out _) && w != null;
            Line("Texture:", loaded ? "Loaded" : "Loading / not found", loaded ? UIConstants.Glow : UIConstants.Accent);
        }

        if (UIConstants.AccentButton("TEST HOUSE RULES", disabled: matchedVenue == null) && matchedVenue != null)
            plugin.HouseRules.ShowSample(matchedVenue);
        if (UIConstants.AccentButton("RESET PASSPORT STAMP HERE", disabled: matchedVenue == null) && matchedVenue != null)
        {
            var reset = plugin.Passport.ResetStamp(VenueMapperPlugin.PlayerState.ContentId, matchedVenue.VenueId);
            plugin.Toasts.Show(reset ? "Stamp set to 1 visit" : "No stamp for this venue", ToastKind.Info, 2.5);
        }

        DrawScreenshotAssistant();
    }

    private void DrawScreenshotAssistant()
    {
        var shots = plugin.Shots;
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(UIConstants.Heading, "SCREENSHOT ASSISTANT");

        var screenshotMode = plugin.ScreenshotMode;
        if (ImGui.Checkbox("Screenshot mode", ref screenshotMode))
            plugin.ScreenshotMode = screenshotMode;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Keeps VenueMapper visible when the game UI is hidden (Scroll Lock or GPose), uses the standard marker look and hides your name and owner line. Resets when the plugin reloads.");
        ImGui.SameLine();
        var demoPassport = plugin.DemoPassport != null;
        if (ImGui.Checkbox("Demo passport", ref demoPassport))
            shots.SetDemoPassport(demoPassport, out _);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Adds demo stamps in memory only. Nothing is saved.");
        ImGui.SameLine();
        if (plugin.ConfigManager.DemoFestival == null)
        {
            if (UIConstants.AccentButton("DEMO FESTIVAL HERE") && !shots.PlaceDemoFestival(out var problem))
                plugin.Toasts.Show(problem, ToastKind.Warning, 3.0);
        }
        else if (UIConstants.AccentButton("REMOVE DEMO FESTIVAL", UIConstants.Danger))
        {
            shots.RemoveDemoFestival();
        }

        if (shotFolder.Length == 0) shotFolder = shots.Folder;
        ImGui.SetNextItemWidth(340f);
        if (ImGui.InputText("##shotFolder", ref shotFolder, 260))
            shots.Folder = shotFolder.Trim();
        ImGui.SameLine();
        if (UIConstants.AccentButton("OPEN FOLDER"))
            shots.OpenFolder();

        using (ImRaii.Disabled(shots.Busy))
        {
            for (var i = 0; i < ScreenshotAssistant.Shots.Length; i++)
            {
                var spec = ScreenshotAssistant.Shots[i];
                if (i % 4 != 0) ImGui.SameLine();
                if (UIConstants.AccentButton($"{spec.Label}##shot{i}", i == shots.Selected ? UIConstants.Success : UIConstants.Accent, 120f))
                    shots.Take(i);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{spec.File}.png\n{spec.Hint}");
            }
        }
        ImGui.TextColored(UIConstants.TextMuted, "Each button sets up the window and saves the picture. Pause key: take the last shot again.");
        ImGui.TextColored(shots.Busy ? UIConstants.Warning : UIConstants.TextPrimary,
            shots.Busy && shots.SecondsLeft > 0 ? $"Shot in {shots.SecondsLeft:0.0} s" : shots.Status);
        if (UIConstants.AccentButton("END SCREENSHOT SESSION", UIConstants.Warning))
            shots.EndSession();
        ImGui.Spacing();
    }
#endif

    public void Dispose()
    {
    }
}
