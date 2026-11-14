using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class VenueQuickPopupWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private Venue? venue;

    public VenueQuickPopupWindow(VenueMapperPlugin plugin)

        : base("Venue Quick Info##VenueQuickPopup",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
    }

    public void Open(Venue v)
    {
        venue = v;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        UIConstants.PushWindowChrome();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 16));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        UIConstants.PopWindowChrome();
    }

    public override void Draw()
    {

        try { DrawContent(); }
        catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "[VenueMapper] VenueQuickPopupWindow draw failed"); }
    }

    private static void ThinDivider(float width)
    {
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(p, p + new Vector2(width, 0),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.15f)));
        ImGui.Dummy(new Vector2(width, 1));
        ImGui.Spacing();
    }

    private void DrawContent()
    {
        var v = venue;
        if (v == null) { IsOpen = false; return; }

        var config = plugin.Configuration;
        var avail = ImGui.GetContentRegionAvail().X;
        var iconFont = UiBuilder.IconFont;
        var toggleW = UIConstants.ToggleWidth();

        DrawHeader(v, avail, iconFont);

        ImGui.Spacing();
        ThinDivider(avail);

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f), Lang.QuickSettings.ToUpperInvariant());
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextPrimary, Lang.Markers3D);
        ImGui.SameLine(avail - toggleW);
        var markers = plugin.PictomancyMarkers.Enabled;
        if (UIConstants.Toggle("##qpMarkers3d", ref markers))
        {
            plugin.PictomancyMarkers.Enabled = markers;
            config.Markers3DEnabled = markers;
            config.Save();
        }

        ImGui.TextColored(UIConstants.TextPrimary, Lang.MarkerStrongPulse);
        ImGui.SameLine(avail - toggleW);
        var strongPulse = config.MarkerStrongPulse;
        if (UIConstants.Toggle("##qpStrongPulse", ref strongPulse))
        { config.MarkerStrongPulse = strongPulse; config.Save(); }

        ImGui.TextColored(UIConstants.TextPrimary, Lang.MarkerColorOverride);
        var colorOverride = config.MarkerColorOverrideEnabled;
        if (colorOverride)
        {
            ImGui.SameLine(avail - toggleW - SwatchSize - SwatchGap);
            DrawColorSwatch(config);
            ImGui.SameLine(0, SwatchGap);
        }
        else
        {
            ImGui.SameLine(avail - toggleW);
        }
        if (UIConstants.Toggle("##qpColorOverride", ref colorOverride))
        { config.MarkerColorOverrideEnabled = colorOverride; config.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.MarkerColorOverrideTip);

        ImGui.Spacing();
        ThinDivider(avail);

        var copyLabel = $"{Lang.CopyAddress}##qpCopyAddr";
        var dirLabel = $"{Lang.Directory}##qpDirectory";
        var btnW = Math.Max(ImGui.CalcTextSize(Lang.CopyAddress).X, ImGui.CalcTextSize(Lang.Directory).X) + 28f;
        btnW = Math.Max(btnW, (avail - 8f) / 2f);

        if (UIConstants.AccentButton(copyLabel, UIConstants.Accent, width: btnW))
        {

            ImGui.SetClipboardText($"{v.Name} // {v.Address}");
            plugin.Toasts.Show(Lang.ToastAddressCopied, ToastKind.Success, 2.0);
        }
        ImGui.SameLine(0, 8);
        if (UIConstants.AccentButton(dirLabel, UIConstants.Accent, width: btnW))
        {
            plugin.VenueMapWindow.ShowDirectory();
            plugin.VenueMapWindow.IsOpen = true;
        }
    }

    private const float BoxSize = 30f;
    private const float SwatchSize = 18f;
    private const float SwatchGap = 12f;

    private void DrawHeader(Venue v, float avail, ImFontPtr iconFont)
    {
        var closeGlyph = FontAwesomeIcon.Times.ToIconString();
        float closeSize;
        using (ImRaii.PushFont(iconFont))
            closeSize = ImGui.CalcTextSize(closeGlyph).X + 8f;

        DrawDoorBadge(iconFont);
        ImGui.SameLine(BoxSize + 14f);

        using (ImRaii.Group())
            DrawNameAndStatus(v);

        const float minNameGap = 16f;
        var nameGroupEndX = BoxSize + 14f + ImGui.GetItemRectSize().X;
        ImGui.SameLine(Math.Max(avail - closeSize, nameGroupEndX + minNameGap));
        using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero)
                     .Push(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.15f))
                     .Push(ImGuiCol.ButtonActive, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.25f))
                     .Push(ImGuiCol.Text, UIConstants.TextSecondary))
        using (ImRaii.PushFont(iconFont))
        {
            if (ImGui.Button($"{closeGlyph}##qpClose", new Vector2(closeSize, closeSize)))
                IsOpen = false;
        }
    }

    private static void DrawDoorBadge(ImFontPtr iconFont)
    {
        var dl = ImGui.GetWindowDrawList();
        var boxDrawMin = ImGui.GetCursorScreenPos() + new Vector2(0, 4);
        var box = new Vector2(BoxSize, BoxSize);
        dl.AddRectFilled(boxDrawMin, boxDrawMin + box,
            ImGui.ColorConvertFloat4ToU32(UIConstants.CardBackground), UIConstants.ChipRounding);
        dl.AddRect(boxDrawMin, boxDrawMin + box,
            ImGui.ColorConvertFloat4ToU32(UIConstants.Accent), UIConstants.ChipRounding);
        using (ImRaii.PushFont(iconFont))
        {
            var doorGlyph = FontAwesomeIcon.DoorOpen.ToIconString();
            var doorSize = ImGui.CalcTextSize(doorGlyph);
            dl.AddText(boxDrawMin + (box - doorSize) / 2f,
                ImGui.ColorConvertFloat4ToU32(UIConstants.Accent), doorGlyph);
        }
        ImGui.Dummy(box);
    }

    private void DrawNameAndStatus(Venue v)
    {
        ImGui.TextColored(UIConstants.TextPrimary, UIConstants.DisplayName(v.Name));

        var addrParts = v.Address?.Split(" - ") ?? Array.Empty<string>();
        var district = addrParts.Length >= 3 ? addrParts[2] : "";

        var xivId = VenueMapWindow.ExtractXivVenuesId(v.Links?.FfxivVenues);
        if (xivId != null) plugin.XivVenues.RequestSchedule(xivId);
        var sched = xivId != null ? plugin.XivVenues.GetSchedule(xivId) : null;

        ImGui.TextColored(UIConstants.TextSecondary, district);
        if (sched == null) return;

        var statusText = sched.GetStatusText();
        if (statusText.Length == 0) return;
        ImGui.SameLine(0, 6);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), "·");
        ImGui.SameLine(0, 6);
        ImGui.TextColored(sched.IsOpenNow ? UIConstants.Success : UIConstants.TextSecondary, statusText);
    }

    private static void DrawColorSwatch(Configuration config)
    {
        var ov = config.MarkerOverrideColor;
        var previewCol = new Vector4(ov.X, ov.Y, ov.Z, 1f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3f);
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, SwatchSize / 2f).Push(ImGuiStyleVar.FrameBorderSize, 0.5f))
        using (ImRaii.PushColor(ImGuiCol.Border, Vector4.One))
        {
            if (ImGui.ColorButton("##qpMarkerPreview", previewCol, ImGuiColorEditFlags.None, new Vector2(SwatchSize, SwatchSize)))
                ImGui.OpenPopup("##qpMarkerColorPickerPopup");
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.MarkerColorPickerTip);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupRounding, UIConstants.ChipRounding * 2f)
                                .Push(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding);
        using var colors = ImRaii.PushColor(ImGuiCol.PopupBg, UIConstants.CardBackground)
                                 .Push(ImGuiCol.Border, UIConstants.WithAlpha(UIConstants.Glow, 0.5f));
        using var popup = ImRaii.Popup("##qpMarkerColorPickerPopup");
        if (!popup) return;

        var pickerCol = ov;
        if (ImGui.ColorPicker3("##qpMarkerColorPicker3", ref pickerCol))
        {
            config.MarkerOverrideColor = pickerCol;
            config.Save();
        }
    }

    public void Dispose() { }
}
