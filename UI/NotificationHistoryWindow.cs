using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class NotificationHistoryWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private bool filterInfo = true, filterSuccess = true, filterWarning = true, filterEgg = true;

    public NotificationHistoryWindow(VenueMapperPlugin plugin)
        : base("Notification History##NotificationHistory",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar)
    {
        this.plugin = plugin;
        Size = new Vector2(480, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 300) };
    }

    public void Open() => IsOpen = true;

    public override void PreDraw() => UIConstants.PushWindowChrome();
    public override void PostDraw() => UIConstants.PopWindowChrome();

    public override void Draw()
    {

        try { DrawContent(); }
        catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "[VenueMapper] NotificationHistoryWindow draw failed"); }
    }

    private void DrawContent()
    {
        var history = plugin.Toasts.History;

        var avail = ImGui.GetContentRegionAvail().X;
        var clearW = ImGui.CalcTextSize(Lang.ClearHistory).X + 28f;
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.55f), Lang.NotificationHistoryDesc);
        ImGui.SameLine(avail - clearW);
        if (UIConstants.AccentButton($"{Lang.ClearHistory}##clearNotifHistory", UIConstants.Warning, width: clearW, disabled: history.Count == 0))
            plugin.Toasts.ClearHistory();

        ImGui.Spacing();
        DrawFilterChip("INFO", UIConstants.Secondary, ref filterInfo);
        ImGui.SameLine(0, 4);
        DrawFilterChip("SUCCESS", UIConstants.Success, ref filterSuccess);
        ImGui.SameLine(0, 4);
        DrawFilterChip("WARNING", UIConstants.Warning, ref filterWarning);
        ImGui.SameLine(0, 4);
        DrawFilterChip("EGG", UIConstants.Glow, ref filterEgg);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##notifHistoryScroll", new Vector2(-1, -1));
        if (!child) return;

        var display = BuildDisplayList(history);
        if (display.Count == 0)
        {
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), Lang.NoNotificationHistory);
            return;
        }

        using var wrap = ImRaii.TextWrapPos(0);
        var lastDate = DateTime.MinValue;
        foreach (var d in display)
        {
            if (d.Timestamp.Date != lastDate)
            {
                if (lastDate != DateTime.MinValue) ImGui.Spacing();
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), DateLabel(d.Timestamp.Date));
                ImGui.Separator();
                lastDate = d.Timestamp.Date;
            }

            DrawKindTag(d.Kind);
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.45f), d.Timestamp.ToString("HH:mm"));
            ImGui.SameLine(0, 6);
            ImGui.TextWrapped(d.Text);
        }
    }

    private bool KindEnabled(ToastKind kind) => kind switch
    {
        ToastKind.Success => filterSuccess,
        ToastKind.Warning => filterWarning,
        ToastKind.Egg     => filterEgg,
        _                 => filterInfo,
    };

    private List<(string Text, ToastKind Kind, DateTime Timestamp)> BuildDisplayList(IReadOnlyList<ToastManager.HistoryEntry> history)
    {
        var display = new List<(string Text, ToastKind Kind, DateTime Timestamp)>();
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var e = history[i];
            if (!KindEnabled(e.Kind)) continue;
            display.Add((e.Text, e.Kind, e.Timestamp));
        }
        return display;
    }

    private static void DrawFilterChip(string label, Vector4 accent, ref bool active)
    {
        using var colors = ImRaii.PushColor(ImGuiCol.Button, UIConstants.WithAlpha(accent, active ? 0.30f : 0.06f))
                                 .Push(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(accent, 0.45f))
                                 .Push(ImGuiCol.ButtonActive, UIConstants.WithAlpha(accent, 0.55f))
                                 .Push(ImGuiCol.Text, active ? accent : UIConstants.WithAlpha(accent, 0.5f))
                                 .Push(ImGuiCol.Border, UIConstants.WithAlpha(accent, active ? 0.6f : 0.2f));
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding)
                                .Push(ImGuiStyleVar.FrameBorderSize, 1f);
        if (ImGui.Button(label))
            active = !active;
    }

    private static string DateLabel(DateTime date)
    {
        if (date == DateTime.Now.Date) return Lang.Today;
        if (date == DateTime.Now.Date.AddDays(-1)) return Lang.Yesterday;
        return date.ToString("MMM d, yyyy");
    }

    private static void DrawKindTag(ToastKind kind)
    {
        var (accent, label) = kind switch
        {
            ToastKind.Success => (UIConstants.Success, "SUCCESS"),
            ToastKind.Warning => (UIConstants.Warning, "WARNING"),
            ToastKind.Egg     => (UIConstants.Glow, "EGG"),
            _                 => (UIConstants.Secondary, "INFO"),
        };

        const float padX = 5f;
        const float colW = 74f;

        var textSize = ImGui.CalcTextSize(label);
        var tagW = textSize.X + padX * 2;
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var xOff = MathF.Floor((colW - tagW) / 2f);

        dl.AddRectFilled(
            new Vector2(pos.X + xOff, pos.Y + 1f),
            new Vector2(pos.X + xOff + tagW, pos.Y + textSize.Y + 1f),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.20f)), 3f);
        dl.AddText(
            new Vector2(pos.X + xOff + padX, pos.Y),
            ImGui.ColorConvertFloat4ToU32(accent), label);

        ImGui.Dummy(new Vector2(colW, textSize.Y));
        ImGui.SameLine(0, 6);
    }

    public void Dispose() { }
}
