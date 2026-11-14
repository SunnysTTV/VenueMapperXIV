using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Services;

namespace VenueMapper.UI;

public partial class SettingsWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private bool isPulling;
    private float aboutHeaderAlpha;
    private const float ToastBaselineSeconds = 3.0f;
    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    public SettingsWindow(VenueMapperPlugin plugin)
        : base("VenueMapper Settings##VenueMapperSettings", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar)
    {
        this.plugin = plugin;
        Size = new Vector2(620, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 380),
            MaximumSize = new Vector2(900, 1200),
        };
    }

    public override void PreDraw() => UIConstants.PushWindowChrome();
    public override void PostDraw() => UIConstants.PopWindowChrome();

    public override void Draw()
    {
        try
        {
            using (ImRaii.Disabled(UIConstants.IsHackerBooting))
                DrawSettingsTab();

            HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Error(ex, "[VenueMapper] SettingsWindow draw failed");
        }
    }

    private static void SectionDivider()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static string SignsLabel(VenueSignsMode mode) => mode switch
    {
        VenueSignsMode.Open      => Lang.VenueSignsOpen,
        VenueSignsMode.Favorites => Lang.VenueSignsFavorites,
        VenueSignsMode.Off       => Lang.VenueSignsOff,
        _                        => Lang.VenueSignsAll,
    };

    private static string GameMapLabel(GameMapMode mode) => mode switch
    {
        GameMapMode.Native  => Lang.GameMapNative,
        GameMapMode.Overlay => Lang.GameMapOverlay,
        GameMapMode.Both    => Lang.GameMapBoth,
        _                   => Lang.GameMapOff,
    };

    private void SetLanguage(Configuration config, string code)
    {
        config.Language = code;
        Lang.Set(code);
        ChangelogData.CurrentLanguage = code;
        config.Save();
        plugin.VenueMapWindow.ShowSettings();
    }

    private void ResetCache()
    {
        try
        {
            if (System.IO.File.Exists(plugin.ConfigManager.CacheFilePath))
                System.IO.File.Delete(plugin.ConfigManager.CacheFilePath);
            var etagPath = System.IO.Path.Combine(plugin.ConfigManager.ConfigDirectory, "venues.etag");
            if (System.IO.File.Exists(etagPath)) System.IO.File.Delete(etagPath);
            var bundled = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(VenueMapperPlugin.PluginInterface.AssemblyLocation.FullName) ?? "",
                "Resources", "venues.json");
            plugin.ConfigManager.Load(bundled);
            plugin.Toasts.Show(Lang.ToastCacheCleared, ToastKind.Success, 2.5);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Error(ex, "Reset cache failed");
            plugin.Toasts.Show(Lang.ToastCacheClearFailed, ToastKind.Warning, 3.0);
        }
    }

    private static string DefaultTabLabel(string tab) => tab switch
    {
        "map" => Lang.Map,
        "dir" => Lang.Directory,
        "evt" => Lang.Events,
        "fes" => Lang.Festivals,
        _ => Lang.DefaultTabRemember,
    };

    private static string CornerLabel(ToastCorner corner) => corner switch
    {
        ToastCorner.TopLeft => Lang.PosTopLeft,
        ToastCorner.BottomRight => Lang.PosBottomRight,
        ToastCorner.BottomLeft => Lang.PosBottomLeft,
        _ => Lang.PosTopRight,
    };

    public void DrawEasterEggsTab()
    {
        var eggs = plugin.EasterEggManager;

        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##eggScroll", new Vector2(-1, -1));
        if (!child) return;

        if (plugin.DemoPassport != null)
        {
            PassportView.Draw(plugin, 0f);
            return;
        }

        var rowH = ImGui.GetFrameHeightWithSpacing();
        var reserved = 10f + ImGui.GetTextLineHeightWithSpacing() + 6f + EasterEggIds.All.Length * rowH + 4f;
        PassportView.Draw(plugin, reserved);
        ImGui.Dummy(new Vector2(1f, 10f));

        ImGui.TextColored(UIConstants.Heading, Lang.EasterEggs.ToUpperInvariant());
        var lineY = ImGui.GetCursorScreenPos().Y;
        var lineX = ImGui.GetCursorScreenPos().X;
        ImGui.GetWindowDrawList().AddLine(new Vector2(lineX, lineY), new Vector2(lineX + ImGui.GetContentRegionAvail().X, lineY),
            ImGui.ColorConvertFloat4ToU32(UIConstants.Line), 1f);
        ImGui.Dummy(new Vector2(1f, 4f));

        foreach (var id in EasterEggIds.All)
        {
            using var scope = ImRaii.PushId(id);
            if (eggs.IsDiscovered(id))
                DrawDiscoveredEgg(eggs, id);
            else
                DrawUndiscoveredEgg(id);
        }
    }

    private void DrawDiscoveredEgg(EasterEggManager eggs, string id)
    {
        var enabled = eggs.IsEnabled(id);
        if (UIConstants.Toggle($"##enabled_{id}", ref enabled))
        {
            eggs.SetEnabled(id, enabled);
            if (id == EasterEggIds.HackerMode && enabled)
                eggs.StartHackerModeBoot();
            if (id == EasterEggIds.RandomTitle && enabled)
                plugin.VenueMapWindow.TriggerRandomTitlePreview();
        }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, EasterEggUI.GetName(id));

        var discoveredAt = eggs.GetDiscoveredAt(id);
        if (!discoveredAt.HasValue) return;
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            $"  ({Lang.EggDiscoveredOn(discoveredAt.Value.ToString("yyyy-MM-dd"))})");
    }

    private static void DrawUndiscoveredEgg(string id)
    {
        using (ImRaii.Disabled())
        {
            var off = false;
            ImGui.Checkbox("##undiscovered", ref off);
            ImGui.SameLine();
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f), "???");
        }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), EasterEggUI.GetHint(id));
    }

    public void DrawAboutTab()
    {
        using (ImRaii.TextWrapPos(0))
            DrawAboutContent();

        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##changelogScroll", new Vector2(-1, -1));
        if (!child) return;

        using var wrap = ImRaii.TextWrapPos(0);
        if (ChangelogData.Changelogs.TryGetValue(ChangelogData.PluginVersion, out var sections))
            UpdateNotesWindow.DrawSections(sections, ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 8f, "about");
    }

    private void DrawAboutContent()
    {
        aboutHeaderAlpha = MathF.Min(aboutHeaderAlpha + ImGui.GetIO().DeltaTime * 3f, 1f);
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, aboutHeaderAlpha))
        {
            ImGui.TextColored(UIConstants.Heading, "VenueMapper");
            ImGui.SameLine(0, 6);
            ImGui.TextColored(UIConstants.Glow, ChangelogData.PluginVersion);
            ImGui.TextWrapped(Lang.PluginDesc);
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), "by SunnysOfficial");
        }
        SectionDivider();

        var btnW = (ImGui.GetContentRegionAvail().X - 4) / 2f;

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), Lang.GotIdeas);
        ImGui.Spacing();

        LinkBtn(Lang.Website, SiteLinks.Home,
            UIConstants.Glow, btnW, SiteLinks.Host);
        ImGui.SameLine(0, 4);
        LinkBtn(Lang.OwnerGuide, SiteLinks.Owners,
            UIConstants.Heading, btnW, Lang.OwnerGuideTip);
        LinkBtn("Support Discord", "https://discord.com/invite/agKWEzK5nR",
            new Vector4(0.34f, 0.40f, 0.93f, 1f), btnW, "Join Discord");
        ImGui.SameLine(0, 4);
        LinkBtn("GitHub", "https://github.com/SunnysTTV/VenueMapperXIV",
            new Vector4(0.6f, 0.6f, 0.6f, 1f), btnW, "Source code");
        LinkBtn("Support on Ko-Fi", "https://ko-fi.com/sunnysofficial",
            new Vector4(1f, 0.4f, 0.4f, 1f), btnW, "Support development");
        ImGui.SameLine(0, 4);
        LinkBtn("Send Feedback", "https://docs.google.com/forms/d/1WQeblPTkupR3gvQnaDw0dJjy0OduWGX64nDI9b-tIQw/viewform",
            UIConstants.Success, btnW, "Open feedback form");

        SectionDivider();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.35f),
            "Dalamud  |  Lumina  |  Lifestream IPC  |  Pictomancy  |  Partake.gg API  |  FFXIVVenues API");

        SectionDivider();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.45f), Lang.WantVenue);
        ImGui.Spacing();

        if (UIConstants.AccentButton(Lang.SubmitVenue, width: -1))
            plugin.OwnerSubmitWindow.IsOpen = true;

        ImGui.Spacing();

        if (UIConstants.AccentButton($"{Lang.SubmitFestival}##aboutSubmitFestival", UIConstants.Rose, -1))
            plugin.FestivalSubmitWindow.IsOpen = true;

        ImGui.Spacing();

        var canUpdate = plugin.OwnerUpdateWindow.CanLoadCurrentVenue();
        if (UIConstants.AccentButton(Lang.UpdateVenue, UIConstants.Glow, -1, !canUpdate, tooltip: Lang.UpdateVenueTip))
        {
            var config = plugin.ConfigManager.Config;
            var venue = config != null ? plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) : null;
            if (venue != null)
                plugin.OwnerVerifyWindow.BeginVerify(venue);
        }

        SectionDivider();

        ImGui.TextColored(UIConstants.Heading, $"{ChangelogData.PluginVersion} - {Lang.CurRelease}");
        if (ChangelogData.Versions.Length > 0)
        {
            ImGui.SameLine(0, 6);
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), ChangelogData.Versions[0].Date);
        }
        ImGui.Spacing();
    }

    private static void LinkBtn(string label, string url, Vector4 col, float w, string tooltip)
    {
        if (UIConstants.AccentButton($"{label}##{url}", col, w, height: 26, tooltip: tooltip))
            UIConstants.OpenUrl(url);
    }

    private async Task PullAsync()
    {
        try { await plugin.ManualPullConfigAsync(); }
        finally { isPulling = false; }
    }

    private static async Task TestAllToastKinds(VenueMapperPlugin plugin)
    {
        var kinds = new[] { ToastKind.Info, ToastKind.Success, ToastKind.Warning, ToastKind.Egg };
        foreach (var kind in kinds)
        {
            plugin.Toasts.Show(Lang.TestNotificationText, kind, 3.0);
            await Task.Delay(500);
        }
    }

    public void Dispose() { }
}
