using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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

    public override void Draw()
    {
        ImGui.TextColored(UIConstants.Primary, Lang.DebugInfo);
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.TerritoryId);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, tracker.CurrentTerritoryId.ToString());

        ImGui.TextColored(UIConstants.TextSecondary, Lang.MapId);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, tracker.CurrentMapId.ToString());

        ImGui.TextColored(UIConstants.TextSecondary, "Ward/Plot:");
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, tracker.CurrentWard >= 0
            ? $"W{tracker.CurrentWard + 1} P{tracker.CurrentPlot + 1}"
            : "N/A");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, "Server:");
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary,
            string.IsNullOrEmpty(tracker.CurrentServerName) ? "N/A" : tracker.CurrentServerName);

        ImGui.TextColored(UIConstants.TextSecondary, "District:");
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary,
            string.IsNullOrEmpty(tracker.CurrentHousingDistrict) ? "N/A" : tracker.CurrentHousingDistrict);

        var isInGarden = tracker.IsInHousingWard && tracker.CurrentPlot >= 0;

        ImGui.TextColored(UIConstants.TextSecondary, "Location:");
        ImGui.SameLine();
        var locationLabel = tracker.IsInsideHouse ? "Inside"
            : isInGarden ? "Garden"
            : "Not in Housing";
        var locationColor = tracker.IsInsideHouse ? UIConstants.Glow
            : isInGarden ? UIConstants.Primary
            : UIConstants.TextPrimary;
        ImGui.TextColored(locationColor, locationLabel);

        var matchedVenue = plugin.ConfigManager.Config != null
            ? plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(plugin.ConfigManager.Config)
            : null;
        ImGui.TextColored(UIConstants.TextSecondary, "Matched Venue:");
        ImGui.SameLine();
        ImGui.TextColored(matchedVenue != null ? UIConstants.Glow : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            matchedVenue?.Name ?? "None");

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.PlayerPos);
        ImGui.TextColored(UIConstants.TextPrimary, $"  X: {tracker.PlayerX:F2}");
        ImGui.TextColored(UIConstants.TextPrimary, $"  Y: {tracker.PlayerZ:F2}");
        ImGui.TextColored(UIConstants.TextPrimary, $"  Z: {tracker.PlayerY:F2}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.CurrentFloor);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.Glow, tracker.CurrentFloorName.ToUpperInvariant());

        ImGui.TextColored(UIConstants.TextSecondary, $"Z Range: {tracker.CurrentFloorYMin:F1} - {tracker.CurrentFloorYMax:F1}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

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
            ImGui.TextColored(UIConstants.TextSecondary, "Texture:");
            ImGui.SameLine();
            ImGui.TextColored(loaded ? UIConstants.Glow : UIConstants.Primary,
                loaded ? "Loaded" : "Loading / not found");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawAccentButton("COPY COORDS", () =>
        {
            ImGui.SetClipboardText($"X: {tracker.PlayerX:F2}, Y: {tracker.PlayerZ:F2}, Z: {tracker.PlayerY:F2}");
            plugin.Toasts.Show("Coordinates copied", ToastKind.Success, 1.8);
        });

        ImGui.SameLine();

        DrawAccentButton("COPY TERRITORY", () =>
        {
            ImGui.SetClipboardText(tracker.CurrentTerritoryId.ToString());
            plugin.Toasts.Show("Territory ID copied", ToastKind.Success, 1.8);
        });

        var canCopyOwnerId = matchedVenue != null;
        if (!canCopyOwnerId) ImGui.BeginDisabled();
        DrawAccentButton("COPY OWNER ID JSON", () =>
        {
            var hash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
            var json = $"{{ \"venueId\": \"{matchedVenue!.VenueId}\", \"ownerIdHash\": \"{hash}\" }}";
            ImGui.SetClipboardText(json);
            plugin.Toasts.Show("Owner ID JSON copied - paste it in a Discord DM to sunnysofficial", ToastKind.Success, 3.0);
        });
        if (!canCopyOwnerId) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Make sure you're standing inside YOUR OWN venue first -\nthis registers whichever venue is currently matched at your position.");

        HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName);
    }

    private static void DrawAccentButton(string label, Action onClick)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.Primary);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.PrimaryHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.Primary);
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.Glow);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);

        if (ImGui.Button(label, new Vector2(150, 30)))
        {
            onClick();
        }

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(5);
    }

    public void Dispose()
    {
    }
}
