using System;
using System.Numerics;
using Dalamud.Interface;
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
        // The window has no title bar, so this string is never actually displayed - it's purely
        // the ImGui ID used for position/size persistence. Must stay a fixed, non-localized
        // constant like every other window's ID here, or the position "resets" whenever the
        // resolved language string differs, since ImGui hashes the whole Begin() string for ID.
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
        // A bit more vertical breathing room than the shared window chrome's default (10, 8).
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 16));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        UIConstants.PopWindowChrome();
    }

    public override void Draw()
    {
        // An uncaught exception here would propagate out of Draw() and could skip PostDraw()
        // (which pops PushWindowChrome's styles), leaking them onto the shared ImGui stack for
        // every window drawn afterward, including other plugins'. Catch+log instead.
        try { DrawContent(); }
        catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "[VenueMapper] VenueQuickPopupWindow draw failed"); }
    }

    // A hairline divider tinted with the accent glow, matching the reference mockup - visually
    // thinner/quieter than a plain ImGui.Separator(), which renders far more prominently.
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

        var closeGlyph = FontAwesomeIcon.Times.ToIconString();
        ImGui.PushFont(iconFont);
        var closeSize = ImGui.CalcTextSize(closeGlyph).X + 8f;
        ImGui.PopFont();

        // Avatar box - door glyph on a bordered card, standing in for a venue icon/logo.
        const float boxSize = 30f;
        var boxMin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var boxDrawMin = boxMin + new Vector2(0, 4);
        dl.AddRectFilled(boxDrawMin, boxDrawMin + new Vector2(boxSize, boxSize),
            ImGui.ColorConvertFloat4ToU32(UIConstants.CardBackground), UIConstants.ChipRounding);
        dl.AddRect(boxDrawMin, boxDrawMin + new Vector2(boxSize, boxSize),
            ImGui.ColorConvertFloat4ToU32(UIConstants.Primary), UIConstants.ChipRounding);
        ImGui.PushFont(iconFont);
        var doorGlyph = FontAwesomeIcon.DoorOpen.ToIconString();
        var doorSize = ImGui.CalcTextSize(doorGlyph);
        dl.AddText(boxDrawMin + (new Vector2(boxSize, boxSize) - doorSize) / 2f,
            ImGui.ColorConvertFloat4ToU32(UIConstants.Primary), doorGlyph);
        ImGui.PopFont();
        ImGui.Dummy(new Vector2(boxSize, boxSize));
        ImGui.SameLine(boxSize + 14f);

        ImGui.BeginGroup();
        ImGui.TextColored(UIConstants.TextPrimary, v.Name);

        var addrParts = v.Address?.Split(" - ") ?? Array.Empty<string>();
        var district = addrParts.Length >= 3 ? addrParts[2] : "";

        var xivId = VenueMapWindow.ExtractXivVenuesId(v.Links?.FfxivVenues);
        if (xivId != null) plugin.XivVenues.RequestSchedule(xivId);
        var sched = xivId != null ? plugin.XivVenues.GetSchedule(xivId) : null;

        ImGui.TextColored(UIConstants.TextSecondary, district);
        if (sched != null)
        {
            var statusText = sched.GetStatusText();
            if (statusText.Length > 0)
            {
                ImGui.SameLine(0, 6);
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), "·");
                ImGui.SameLine(0, 6);
                ImGui.TextColored(sched.IsOpenNow ? UIConstants.Success : UIConstants.TextSecondary, statusText);
            }
        }
        ImGui.EndGroup();

        // Force at least a small gap between the name/status group and the close button - a plain
        // "avail - closeSize" would let the X sit flush against a long name with no breathing room,
        // since AlwaysAutoResize only grows the window to fit whatever's actually drawn.
        const float minNameGap = 16f;
        var nameGroupEndX = boxSize + 14f + ImGui.GetItemRectSize().X;
        ImGui.SameLine(Math.Max(avail - closeSize, nameGroupEndX + minNameGap));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.25f));
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.TextSecondary);
        ImGui.PushFont(iconFont);
        if (ImGui.Button($"{closeGlyph}##qpClose", new Vector2(closeSize, closeSize)))
            IsOpen = false;
        ImGui.PopFont();
        ImGui.PopStyleColor(4);

        ImGui.Spacing();
        ThinDivider(avail);

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f), Lang.QuickSettings.ToUpperInvariant());
        ImGui.Spacing();

        // Label-left, toggle-right (right-aligned to the window edge) to match the mockup, rather
        // than this codebase's usual toggle-then-label order used in the full Settings window.
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
        const float swatchSize = 18f;
        const float swatchGap = 12f;
        var colorOverride = config.MarkerColorOverrideEnabled;
        if (colorOverride)
        {
            ImGui.SameLine(avail - toggleW - swatchSize - swatchGap);
            var ov = config.MarkerOverrideColor;
            var previewCol = new Vector4(ov.X, ov.Y, ov.Z, 1f);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, swatchSize / 2f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.5f);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 1f));
            if (ImGui.ColorButton("##qpMarkerPreview", previewCol, ImGuiColorEditFlags.None, new Vector2(swatchSize, swatchSize)))
                ImGui.OpenPopup("##qpMarkerColorPickerPopup");
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.MarkerColorPickerTip);

            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, UIConstants.ChipRounding * 2f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding);
            ImGui.PushStyleColor(ImGuiCol.PopupBg, UIConstants.CardBackground);
            ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.WithAlpha(UIConstants.Glow, 0.5f));
            if (ImGui.BeginPopup("##qpMarkerColorPickerPopup"))
            {
                var pickerCol = ov;
                if (ImGui.ColorPicker3("##qpMarkerColorPicker3", ref pickerCol))
                {
                    config.MarkerOverrideColor = pickerCol;
                    config.Save();
                }
                ImGui.EndPopup();
            }
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
            ImGui.SameLine(0, swatchGap);
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

        if (UIConstants.AccentButton(copyLabel, UIConstants.Primary, width: btnW))
        {
            // Same format as the Directory right-click "Copy Address" context menu entry.
            ImGui.SetClipboardText($"{v.Name} // {v.Address}");
            plugin.Toasts.Show(Lang.ToastAddressCopied, ToastKind.Success, 2.0);
        }
        ImGui.SameLine(0, 8);
        if (UIConstants.AccentButton(dirLabel, UIConstants.Primary, width: btnW))
        {
            plugin.VenueMapWindow.ShowDirectory();
            plugin.VenueMapWindow.IsOpen = true;
        }
    }

    public void Dispose() { }
}
