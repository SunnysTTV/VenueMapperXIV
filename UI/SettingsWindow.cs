using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class SettingsWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private bool isPulling;
    private float aboutHeaderAlpha;
    private const float ToastBaselineSeconds = 3.0f;
    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    public SettingsWindow(VenueMapperPlugin plugin)
        : base("VenueMapper Settings##VenueMapperSettings", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        Size = new Vector2(420, 220);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var hackerBooting = UIConstants.IsHackerBooting;
        if (hackerBooting) ImGui.BeginDisabled();

        DrawSettingsTab();

        if (hackerBooting) ImGui.EndDisabled();

        HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName);
    }

    public void DrawSettingsTab()
    {
        var config = plugin.Configuration;

        ImGui.TextColored(UIConstants.Primary, Lang.Settings.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Language);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.CardBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.GlowDim);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.SetNextItemWidth(120);
        var langLabel = config.Language == "DE" ? Lang.LangGerman : Lang.LangEnglish;
        if (ImGui.BeginCombo("##lang", langLabel))
        {
            if (ImGui.Selectable(Lang.LangEnglish, config.Language == "EN"))
            { config.Language = "EN"; Lang.Set("EN"); ChangelogData.CurrentLanguage = "EN"; config.Save(); plugin.VenueMapWindow.ShowSettings(); }
            if (ImGui.Selectable(Lang.LangGerman, config.Language == "DE"))
            { config.Language = "DE"; Lang.Set("DE"); ChangelogData.CurrentLanguage = "DE"; config.Save(); plugin.VenueMapWindow.ShowSettings(); }
            ImGui.EndCombo();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Glow);
        var markers = plugin.PictomancyMarkers.Enabled;
        if (ImGui.Checkbox(Lang.Markers3D, ref markers))
            plugin.PictomancyMarkers.Enabled = markers;
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Primary);
        var autoPull = config.AutoPullOnStartup;
        if (ImGui.Checkbox(Lang.AutoPullCfg, ref autoPull))
        { config.AutoPullOnStartup = autoPull; config.Save(); }
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.GithubConfig);
        ImGui.Spacing();

        DrawAccentButton(isPulling ? "..." : Lang.PullNow, () =>
        { isPulling = true; _ = PullAsync(); },
        disabled: isPulling || string.IsNullOrWhiteSpace(config.GitHubConfigUrl));
        ImGui.SameLine();
        DrawAccentButton(Lang.ResetCache, () =>
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
                plugin.Toasts.Show(Lang.ToastCacheClearFailed, ToastKind.Info, 3.0);
            }
        });

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Notifications);
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Glow);
        var notifEnabled = config.NotificationsEnabled;
        if (ImGui.Checkbox(Lang.EnableNotifications, ref notifEnabled))
        { config.NotificationsEnabled = notifEnabled; config.Save(); }
        ImGui.PopStyleColor();

        if (!config.NotificationsEnabled) ImGui.BeginDisabled();

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.NotificationPosition);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.CardBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.GlowDim);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.SetNextItemWidth(160);
        var posLabel = CornerLabel(config.ToastPosition);
        if (ImGui.BeginCombo("##toastPos", posLabel))
        {
            foreach (var corner in new[] { ToastCorner.TopRight, ToastCorner.TopLeft, ToastCorner.BottomRight, ToastCorner.BottomLeft })
            {
                if (ImGui.Selectable(CornerLabel(corner), config.ToastPosition == corner))
                { config.ToastPosition = corner; config.Save(); }
            }
            ImGui.EndCombo();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.MaxVisibleToasts);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        var maxVisible = config.MaxVisibleToasts;
        if (ImGui.DragInt("##maxVisibleToasts", ref maxVisible, 1, 1, 10))
        { config.MaxVisibleToasts = Math.Clamp(maxVisible, 1, 10); config.Save(); }

        ImGui.TextColored(UIConstants.TextSecondary, Lang.ToastDurationLabel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        var durationSeconds = config.ToastDurationMultiplier * ToastBaselineSeconds;
        if (ImGui.SliderFloat("##toastDuration", ref durationSeconds, 1.5f, 9.0f, "%.1fs"))
        { config.ToastDurationMultiplier = durationSeconds / ToastBaselineSeconds; config.Save(); }

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Primary);
        var suppressCombat = config.SuppressInCombat;
        if (ImGui.Checkbox(Lang.SuppressInCombat, ref suppressCombat))
        { config.SuppressInCombat = suppressCombat; config.Save(); }
        ImGui.PopStyleColor();

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Primary);
        var eventReminders = config.EventReminders;
        if (ImGui.Checkbox(Lang.EventReminders, ref eventReminders))
        { config.EventReminders = eventReminders; config.Save(); }
        ImGui.PopStyleColor();

        if (config.EventReminders)
        {
            ImGui.Indent();
            ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Primary);
            var favOnly = config.EventRemindersFavoritesOnly;
            if (ImGui.Checkbox(Lang.EventRemindersFavOnly, ref favOnly))
            { config.EventRemindersFavoritesOnly = favOnly; config.Save(); }
            ImGui.PopStyleColor();

            ImGui.TextColored(UIConstants.TextSecondary, Lang.EventReminderMinutesLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            var reminderMin = config.EventReminderMinutes;
            if (ImGui.DragInt("##eventReminderMin", ref reminderMin, 1, 5, 60))
            { config.EventReminderMinutes = Math.Clamp(reminderMin, 5, 60); config.Save(); }
            ImGui.Unindent();
        }

        ImGui.Spacing();
        DrawAccentButton(Lang.TestNotification, () =>
            plugin.Toasts.Show(Lang.TestNotificationText, ToastKind.Info, 3.0));

        if (!config.NotificationsEnabled) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.MiscSettings);
        ImGui.Spacing();

        DrawAccentButton(Lang.ResetWindowPosition, () =>
        {
            plugin.VenueMapWindow.ResetWindowPosition();
            plugin.Toasts.Show(Lang.ToastWindowReset, ToastKind.Success, 2.5);
        });
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.ResetWindowPositionTip);
        ImGui.SameLine();
        DrawAccentButton(Lang.UnhideAllVenues, () =>
        {
            var count = config.HiddenVenueIds.Count;
            config.HiddenVenueIds.Clear();
            config.Save();
            plugin.Toasts.Show(Lang.ToastAllVenuesUnhidden(count), ToastKind.Success, 2.5);
        }, disabled: config.HiddenVenueIds.Count == 0);
        if (config.HiddenVenueIds.Count > 0 && ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.UnhideAllVenuesTip(config.HiddenVenueIds.Count));

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.DefaultTabLabel);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.CardBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.GlowDim);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.SetNextItemWidth(140);
        if (ImGui.BeginCombo("##defaultTab", DefaultTabLabel(config.DefaultTab)))
        {
            foreach (var tab in new[] { "remember", "map", "dir", "evt" })
            {
                if (ImGui.Selectable(DefaultTabLabel(tab), config.DefaultTab == tab))
                { config.DefaultTab = tab; config.Save(); }
            }
            ImGui.EndCombo();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Primary);
        var boostOpen = config.BoostOpenVenues;
        if (ImGui.Checkbox(Lang.BoostOpenVenues, ref boostOpen))
        { config.BoostOpenVenues = boostOpen; config.Save(); }
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.BoostOpenVenuesTip);
    }

    private static string DefaultTabLabel(string tab) => tab switch
    {
        "map" => Lang.Map,
        "dir" => Lang.Directory,
        "evt" => Lang.Events,
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
        eggs.RegisterFirstSeen(EasterEggIds.WindowWobble);

        ImGui.TextColored(UIConstants.Primary, Lang.EasterEggs.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var id in EasterEggIds.All)
        {
            var discovered = eggs.IsDiscovered(id);

            ImGui.PushID(id);
            if (discovered)
            {
                var enabled = eggs.IsEnabled(id);
                ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Glow);
                if (ImGui.Checkbox("##enabled", ref enabled))
                {
                    eggs.SetEnabled(id, enabled);
                    if (id == EasterEggIds.HackerMode && enabled)
                        eggs.StartHackerModeBoot();
                    if (id == EasterEggIds.RandomTitle && enabled)
                        plugin.VenueMapWindow.TriggerRandomTitlePreview();
                }
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextColored(UIConstants.TextPrimary, EasterEggUI.GetName(id));

                var discoveredAt = eggs.GetDiscoveredAt(id);
                if (discoveredAt.HasValue)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
                        $"  ({Lang.EggDiscoveredOn(discoveredAt.Value.ToString("yyyy-MM-dd"))})");
                }
            }
            else
            {
                ImGui.BeginDisabled();
                var off = false;
                ImGui.Checkbox("##undiscovered", ref off);
                ImGui.SameLine();
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f), "???");
                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), EasterEggUI.GetHint(id));
            }
            ImGui.PopID();
        }
    }

    public void DrawAboutTab()
    {
        ImGui.PushTextWrapPos(0);

        aboutHeaderAlpha = MathF.Min(aboutHeaderAlpha + ImGui.GetIO().DeltaTime * 3f, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, aboutHeaderAlpha);

        ImGui.TextColored(UIConstants.Primary, "VenueMapper");
        ImGui.SameLine(0, 6);
        ImGui.TextColored(UIConstants.Glow, ChangelogData.PluginVersion);
        ImGui.TextWrapped(Lang.PluginDesc);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), "by SunnysOfficial");

        ImGui.PopStyleVar();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var btnW = (ImGui.GetContentRegionAvail().X - 4) / 2f;

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), Lang.GotIdeas);
        ImGui.Spacing();

        LinkBtn("Support Discord", "https://discord.com/invite/agKWEzK5nR",
            new Vector4(0.34f, 0.40f, 0.93f, 1f), btnW, "Join Discord");
        ImGui.SameLine(0, 4);
        LinkBtn("GitHub", "https://github.com/SunnysTTV/VenueMapperXIV",
            new Vector4(0.6f, 0.6f, 0.6f, 1f), btnW, "Source code");
        LinkBtn("Support on Ko-Fi", "https://ko-fi.com/sunnysofficial",
            new Vector4(1f, 0.4f, 0.4f, 1f), -1, "Support development");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.35f),
            "Dalamud  |  Lumina  |  Lifestream IPC  |  Pictomancy  |  Partake.gg API  |  FFXIVVenues API");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.45f),
            Lang.WantVenue);
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Primary, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Primary, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(UIConstants.Primary, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Primary);
        if (ImGui.Button(Lang.SubmitVenue, new Vector2(-1, 26)))
            plugin.OwnerSubmitWindow.IsOpen = true;
        ImGui.PopStyleColor(4);

        ImGui.Spacing();

        var canUpdate = plugin.OwnerUpdateWindow.CanLoadCurrentVenue();
        if (!canUpdate) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Glow, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Glow, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(UIConstants.Glow, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Glow);
        if (ImGui.Button(Lang.UpdateVenue, new Vector2(-1, 26)))
        {
            var config = plugin.ConfigManager.Config;
            var venue = config != null ? plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) : null;
            if (venue != null)
                plugin.OwnerVerifyWindow.BeginVerify(venue);
        }
        ImGui.PopStyleColor(4);
        if (!canUpdate) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(Lang.UpdateVenueTip);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.Primary, $"{ChangelogData.PluginVersion} - {Lang.CurRelease}");
        if (ChangelogData.Versions.Length > 0)
        {
            ImGui.SameLine(0, 6);
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), ChangelogData.Versions[0].Date);
        }
        ImGui.Spacing();

        ImGui.PopTextWrapPos();

        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 0f);
        if (ImGui.BeginChild("##changelogScroll", new Vector2(-1, -1)))
        {
            ImGui.PushTextWrapPos(0);
            if (ChangelogData.Changelogs.TryGetValue(ChangelogData.PluginVersion, out var sections))
                UIConstants.DrawChangelog(sections);
            ImGui.PopTextWrapPos();
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
    }


    private static void LinkBtn(string label, string url, Vector4 col, float w, string tooltip)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(col, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(col, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(col, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.Text, col);
        if (ImGui.Button($"{label}##{url}", new Vector2(w, 26)))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = url, UseShellExecute = true }); } catch { }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        ImGui.PopStyleColor(4);
    }

    private async System.Threading.Tasks.Task PullAsync()
    {
        try { await plugin.ManualPullConfigAsync(); }
        finally { isPulling = false; }
    }

    private static void DrawAccentButton(string label, Action onClick, bool disabled = false)
    {
        if (disabled) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.Primary);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.PrimaryHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.Primary);
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.TextPrimary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        if (ImGui.Button(label, new Vector2(120, 26))) onClick();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        if (disabled) ImGui.EndDisabled();
    }

    public void Dispose() { }
}
