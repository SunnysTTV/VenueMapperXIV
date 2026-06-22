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

        var mapInfo = plugin.MapLoader.GetMapInfoByMapId(tracker.CurrentMapId)
                      ?? plugin.MapLoader.GetMapInfo(tracker.CurrentTerritoryId);
        ImGui.TextColored(UIConstants.TextSecondary, "Map Path:");
        ImGui.TextColored(mapInfo.Path != null ? UIConstants.Glow : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            mapInfo.Path ?? "(no map for this territory)");

        if (mapInfo.Path != null)
        {
            var tex = plugin.MapLoader.GetMapTexture(tracker.CurrentTerritoryId, tracker.CurrentMapId);
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
        });

        ImGui.SameLine();

        DrawAccentButton("COPY TERRITORY", () =>
        {
            ImGui.SetClipboardText(tracker.CurrentTerritoryId.ToString());
        });
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
