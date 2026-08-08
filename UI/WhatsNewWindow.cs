using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace VenueMapper.UI;

public class WhatsNewWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;

    public WhatsNewWindow(VenueMapperPlugin plugin)
        : base("VenueMapper - What's New##WhatsNew",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        Size = new Vector2(480, 640);
        SizeCondition = ImGuiCond.Always;

        ShowCloseButton = false;
        RespectCloseHotkey = false;
    }

    public void Open() => IsOpen = true;

    public override void PreDraw()
    {
        UIConstants.PushWindowChrome(UIConstants.Glow, 2f);
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    }

    public override void PostDraw() => UIConstants.PopWindowChrome();

    public override void Draw()
    {

        try { DrawContent(); }
        catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "[VenueMapper] WhatsNewWindow draw failed"); }
    }

    private void DrawContent()
    {
        ImGui.PushTextWrapPos(0);
        try
        {
            CenteredText($"{Lang.WhatsNewTitle} {ChangelogData.PluginVersion}", UIConstants.Glow);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            CenteredText(Lang.WhatsNewFeatures, UIConstants.Glow);
            ImGui.Spacing();

            CenteredIconRow(FontAwesomeIcon.InfoCircle, UIConstants.Primary, Lang.WhatsNewVenueDetails, Lang.WhatsNewVenueDetailsDesc);
            CenteredIconRow(FontAwesomeIcon.DoorOpen, UIConstants.Secondary, Lang.ShowQuickPopupOnEnter, Lang.WhatsNewQuickPopup);
            CenteredIconRow(FontAwesomeIcon.Bell, new Vector4(1f, 0.5f, 0.2f, 1f), Lang.WhatsNewSmartNotify, Lang.WhatsNewSmartNotifyDesc);
            CenteredIconRow(FontAwesomeIcon.Crown, new Vector4(1f, 0.84f, 0f, 1f), Lang.WhatsNewCrownFilter, Lang.WhatsNewCrownFilterDesc);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            CenteredText(Lang.WhatsNewNewSettings, UIConstants.Glow);
            ImGui.Spacing();

            CenteredIconRow(FontAwesomeIcon.Cube, new Vector4(0.2f, 1f, 0.5f, 1f), Lang.HideMarkersInOwnVenue, Lang.HideMarkersInOwnVenueTip);
            CenteredIconRow(FontAwesomeIcon.IdBadge, new Vector4(1f, 0.84f, 0f, 1f), Lang.AutoOpenOwnVenue, Lang.AutoOpenOwnVenueTip);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            CenteredWrappedText(Lang.WhatsNewSeeMore, UIConstants.TextSecondary);
            ImGui.Spacing();

            if (UIConstants.AccentButton($"{Lang.WhatsNewGotIt}##whatsNewGotIt", UIConstants.Glow, width: -1))
            {
                plugin.Configuration.PendingWhatsNew = false;
                plugin.Configuration.Save();
                IsOpen = false;
            }
        }
        finally
        {
            ImGui.PopTextWrapPos();
        }
    }

    private static void CenteredText(string text, Vector4 color)
    {
        var w = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - w) / 2f + ImGui.GetCursorPosX());
        ImGui.TextColored(color, text);
    }

    private static void CenteredIconRow(FontAwesomeIcon icon, Vector4 col, string title, string? desc = null)
    {
        var iconFont = UiBuilder.IconFont;
        var iconStr = icon.ToIconString();

        ImGui.PushFont(iconFont);
        var iconW = ImGui.CalcTextSize(iconStr).X;
        ImGui.PopFont();

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var titleW = ImGui.CalcTextSize(title).X;
        var totalW = iconW + spacing + titleW;
        var avail = ImGui.GetContentRegionAvail().X;

        ImGui.SetCursorPosX((avail - totalW) / 2f + ImGui.GetCursorPosX());
        ImGui.PushFont(iconFont);
        ImGui.TextColored(col, iconStr);
        ImGui.PopFont();
        ImGui.SameLine(0, spacing);
        ImGui.TextColored(UIConstants.TextPrimary, title);

        if (desc != null)
            CenteredWrappedText(desc, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.55f));

        ImGui.Spacing();
    }

    private static void CenteredWrappedText(string text, Vector4 color)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        foreach (var line in UIConstants.WrapText(text, avail))
        {
            var w = ImGui.CalcTextSize(line).X;
            ImGui.SetCursorPosX((avail - w) / 2f + ImGui.GetCursorPosX());
            ImGui.TextColored(color, line);
        }
    }

    public void Dispose() { }
}
