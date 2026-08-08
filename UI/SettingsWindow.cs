using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
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
        : base("VenueMapper Settings##VenueMapperSettings", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar)
    {
        this.plugin = plugin;
        Size = new Vector2(420, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 380),
            MaximumSize = new Vector2(600, 1000),
        };
    }

    public override void PreDraw() => UIConstants.PushWindowChrome();
    public override void PostDraw() => UIConstants.PopWindowChrome();

    public override void Draw()
    {

        var hackerBooting = UIConstants.IsHackerBooting;
        try
        {
            if (hackerBooting) ImGui.BeginDisabled();
            UIConstants.PushScrollbarStyle();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
            try
            {
                if (ImGui.BeginChild("##settingsScroll", new Vector2(-4, -4)))
                    DrawSettingsTab();
            }
            finally
            {
                ImGui.EndChild();
                ImGui.PopStyleColor();
                UIConstants.PopScrollbarStyle();
                if (hackerBooting) ImGui.EndDisabled();
            }

            HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Error(ex, "[VenueMapper] SettingsWindow draw failed");
        }
    }

    private static void DrawSectionHeader(string label)
    {
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var barCol = ImGui.ColorConvertFloat4ToU32(UIConstants.Glow);
        dl.AddRectFilled(pos + new Vector2(0, 2), pos + new Vector2(3, 15), barCol, 1.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.TextColored(UIConstants.TextSecondary, label.ToUpperInvariant());
    }

    public void DrawSettingsTab()
    {
        var config = plugin.Configuration;
        var myHash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
        var ownsAnyVenue = plugin.ConfigManager.Config?.Venues.Any(v => v.OwnerIdHashes.Contains(myHash)) ?? false;

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleColor(ImGuiCol.Separator, UIConstants.WithAlpha(UIConstants.Glow, 0.25f));

        try
        {
        ImGui.TextColored(UIConstants.Primary, Lang.Settings.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();

        UIConstants.BeginSection();

        try
        {

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Language);
        ImGui.SameLine();
        var langLabel = config.Language == "DE" ? Lang.LangGerman : Lang.LangEnglish;
        UIConstants.StyledCombo("##lang", langLabel, 120, () =>
        {
            if (ImGui.Selectable(Lang.LangEnglish, config.Language == "EN"))
            { config.Language = "EN"; Lang.Set("EN"); ChangelogData.CurrentLanguage = "EN"; config.Save(); plugin.VenueMapWindow.ShowSettings(); }
            if (ImGui.Selectable(Lang.LangGerman, config.Language == "DE"))
            { config.Language = "DE"; Lang.Set("DE"); ChangelogData.CurrentLanguage = "DE"; config.Save(); plugin.VenueMapWindow.ShowSettings(); }
        });

        ImGui.SameLine(0, 12);
        var autoPull = config.AutoPullOnStartup;
        if (UIConstants.Toggle("##autoPull", ref autoPull))
        { config.AutoPullOnStartup = autoPull; config.Save(); }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.AutoPullCfg);

        ImGui.Spacing();

        var markers = plugin.PictomancyMarkers.Enabled;
        if (UIConstants.Toggle("##markers3d", ref markers))
        {
            plugin.PictomancyMarkers.Enabled = markers;
            config.Markers3DEnabled = markers;
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.Markers3D);

        ImGui.SameLine(0, 12);
        if (!ownsAnyVenue) ImGui.BeginDisabled();
        var hideOwn = config.HideMarkersInOwnVenue;
        if (UIConstants.Toggle("##hideMarkersOwnVenue", ref hideOwn))
        { config.HideMarkersInOwnVenue = hideOwn; config.Save(); }
        if (!ownsAnyVenue) ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.HideMarkersInOwnVenue);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(ownsAnyVenue ? Lang.HideMarkersInOwnVenueTip : Lang.HideMarkersInOwnVenueNeedsOwnerTip);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawSectionHeader(Lang.GithubConfig);
        ImGui.SameLine(0, 12);

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
                plugin.Toasts.Show(Lang.ToastCacheClearFailed, ToastKind.Warning, 3.0);
            }
        });

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawSectionHeader(Lang.Notifications);
        ImGui.SameLine(0, 12);
        {
            var testLabel = Lang.TestNotification;
            var testW = ImGui.CalcTextSize(testLabel).X + 28f;
            DrawAccentButton(testLabel, () => _ = TestAllToastKinds(plugin),
                disabled: !config.NotificationsEnabled, width: testW);
        }
        ImGui.Spacing();

        var notifEnabled = config.NotificationsEnabled;
        if (UIConstants.Toggle("##notifEnabled", ref notifEnabled))
        { config.NotificationsEnabled = notifEnabled; config.Save(); }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.EnableNotifications);

        if (!config.NotificationsEnabled) ImGui.BeginDisabled();

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.NotificationPosition);
        ImGui.SameLine();
        var posLabel = CornerLabel(config.ToastPosition);
        UIConstants.StyledCombo("##toastPos", posLabel, 160, () =>
        {
            foreach (var corner in new[] { ToastCorner.TopRight, ToastCorner.TopLeft, ToastCorner.BottomRight, ToastCorner.BottomLeft })
            {
                if (ImGui.Selectable(CornerLabel(corner), config.ToastPosition == corner))
                {
                    config.ToastPosition = corner;
                    config.Save();
                    plugin.Toasts.Show(Lang.ToastPositionChanged, ToastKind.Info, 3.5, tag: "toastPositionChanged");
                }
            }
        });

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.MaxVisibleToasts);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        var maxVisible = config.MaxVisibleToasts;
        if (ImGui.DragInt("##maxVisibleToasts", ref maxVisible, 1, 1, 10))
        { config.MaxVisibleToasts = Math.Clamp(maxVisible, 1, 10); config.Save(); }

        UIConstants.FlowNext(ImGui.CalcTextSize(Lang.ToastDurationLabel).X + 100f);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.ToastDurationLabel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        var durationSeconds = config.ToastDurationMultiplier * ToastBaselineSeconds;
        if (ImGui.SliderFloat("##toastDuration", ref durationSeconds, 1.5f, 9.0f, "%.1fs"))
        { config.ToastDurationMultiplier = durationSeconds / ToastBaselineSeconds; config.Save(); }

        var suppressCombat = config.SuppressInCombat;
        if (UIConstants.Toggle("##suppressCombat", ref suppressCombat))
        { config.SuppressInCombat = suppressCombat; config.Save(); }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.SuppressInCombat);

        var eventReminders = config.EventReminders;
        if (UIConstants.Toggle("##eventReminders", ref eventReminders))
        { config.EventReminders = eventReminders; config.Save(); }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.EventReminders);

        if (config.EventReminders)
        {
            var favOnly = config.EventRemindersFavoritesOnly;
            if (UIConstants.Toggle("##favOnly", ref favOnly))
            { config.EventRemindersFavoritesOnly = favOnly; config.Save(); }
            ImGui.SameLine();
            ImGui.TextColored(UIConstants.TextPrimary, Lang.EventRemindersFavOnly);

            UIConstants.FlowNext(ImGui.CalcTextSize(Lang.EventReminderMinutesLabel).X + 70f);
            ImGui.TextColored(UIConstants.TextSecondary, Lang.EventReminderMinutesLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70);
            var reminderMin = config.EventReminderMinutes;
            if (ImGui.DragInt("##eventReminderMin", ref reminderMin, 1, 5, 60))
            { config.EventReminderMinutes = Math.Clamp(reminderMin, 5, 60); config.Save(); }
        }

        if (!config.NotificationsEnabled) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawSectionHeader(Lang.MiscSettings);
        ImGui.SameLine(0, 12);

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
        UIConstants.StyledCombo("##defaultTab", DefaultTabLabel(config.DefaultTab), 120, () =>
        {
            foreach (var tab in new[] { "remember", "map", "dir", "evt" })
            {
                if (ImGui.Selectable(DefaultTabLabel(tab), config.DefaultTab == tab))
                { config.DefaultTab = tab; config.Save(); }
            }
        });

        void ToggleCell(string id, bool value, Action<bool> setter, string label, string tooltip, bool disabled = false, string? disabledTooltip = null)
        {
            if (disabled) ImGui.BeginDisabled();
            if (UIConstants.Toggle(id, ref value)) setter(value);
            if (disabled) ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextColored(UIConstants.TextPrimary, label);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(disabled ? (disabledTooltip ?? tooltip) : tooltip);
        }

        if (ImGui.BeginTable("##miscToggles2col", 2))
        {

            try
            {
            ImGui.TableSetupColumn("##c0", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##c1", ImGuiTableColumnFlags.WidthStretch, 1f);

            ImGui.TableNextColumn();
            ToggleCell("##boostOpen", config.BoostOpenVenues, v => { config.BoostOpenVenues = v; config.Save(); },
                Lang.BoostOpenVenues, Lang.BoostOpenVenuesTip);

            ImGui.TableNextColumn();
            ToggleCell("##autoOpenVenue", config.AutoOpenOnVenueEnter, v => { config.AutoOpenOnVenueEnter = v; config.Save(); },
                Lang.AutoOpenOnVenueEnter, Lang.AutoOpenOnVenueEnterTip);

            ImGui.TableNextColumn();
            ToggleCell("##autoOpenOwnVenue", config.AutoOpenOwnVenue, v => { config.AutoOpenOwnVenue = v; config.Save(); },
                Lang.AutoOpenOwnVenue, Lang.AutoOpenOwnVenueTip,
                disabled: !ownsAnyVenue, disabledTooltip: Lang.HideMarkersInOwnVenueNeedsOwnerTip);

            ImGui.TableNextColumn();
            ToggleCell("##showQuickPopup", config.ShowQuickPopupOnEnter, v => { config.ShowQuickPopupOnEnter = v; config.Save(); },
                Lang.ShowQuickPopupOnEnter, Lang.ShowQuickPopupOnEnterTip);
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawSectionHeader(Lang.AccessibilitySettings);
        ImGui.SameLine(0, 12);

        ImGui.TextColored(UIConstants.TextSecondary, Lang.MarkerSizeLabel);
        ImGui.SameLine();
        var sizeLabel = config.MarkerSizeScale switch
        {
            >= 1.9f => Lang.MarkerSizeExtraLarge,
            >= 1.4f => Lang.MarkerSizeLarge,
            _ => Lang.MarkerSizeNormal,
        };
        UIConstants.StyledCombo("##markerSize", sizeLabel, 160, () =>
        {
            if (ImGui.Selectable(Lang.MarkerSizeNormal, config.MarkerSizeScale < 1.4f))
            { config.MarkerSizeScale = 1.0f; config.Save(); }
            if (ImGui.Selectable(Lang.MarkerSizeLarge, config.MarkerSizeScale is >= 1.4f and < 1.9f))
            { config.MarkerSizeScale = 1.5f; config.Save(); }
            if (ImGui.Selectable(Lang.MarkerSizeExtraLarge, config.MarkerSizeScale >= 1.9f))
            { config.MarkerSizeScale = 2.0f; config.Save(); }
        });

        var strongPulse = config.MarkerStrongPulse;
        if (UIConstants.Toggle("##strongPulse", ref strongPulse))
        { config.MarkerStrongPulse = strongPulse; config.Save(); }
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.MarkerStrongPulse);

        UIConstants.FlowNext(UIConstants.ToggleWidth() + 6f + ImGui.CalcTextSize(Lang.MarkerColorOverride).X);
        var colorOverride = config.MarkerColorOverrideEnabled;
        if (UIConstants.Toggle("##colorOverride", ref colorOverride))
        { config.MarkerColorOverrideEnabled = colorOverride; config.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.MarkerColorOverrideTip);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.MarkerColorOverride);

        if (config.MarkerColorOverrideEnabled)
        {
            var ov = config.MarkerOverrideColor;
            var r = (int)MathF.Round(ov.X * 255);
            var g = (int)MathF.Round(ov.Y * 255);
            var b = (int)MathF.Round(ov.Z * 255);
            var changed = false;

            ImGui.TextColored(UIConstants.TextSecondary, "R");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            changed |= ImGui.SliderInt("##markerR", ref r, 0, 255);
            ImGui.SameLine();
            ImGui.TextColored(UIConstants.TextSecondary, "G");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            changed |= ImGui.SliderInt("##markerG", ref g, 0, 255);
            ImGui.SameLine();
            ImGui.TextColored(UIConstants.TextSecondary, "B");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            changed |= ImGui.SliderInt("##markerB", ref b, 0, 255);

            if (changed)
            {
                config.MarkerOverrideColor = new Vector3(r / 255f, g / 255f, b / 255f);
                config.Save();
            }

            var previewCol = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
            ImGui.SameLine();
            if (ImGui.ColorButton("##markerPreview", previewCol, ImGuiColorEditFlags.None, new Vector2(24, 24)))
                ImGui.OpenPopup("##markerColorPickerPopup");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.MarkerColorPickerTip);

            if (ImGui.BeginPopup("##markerColorPickerPopup"))
            {
                var pickerCol = new Vector3(r / 255f, g / 255f, b / 255f);
                if (ImGui.ColorPicker3("##markerColorPicker3", ref pickerCol))
                {
                    config.MarkerOverrideColor = pickerCol;
                    config.Save();
                }
                ImGui.EndPopup();
            }
        }

        }
        finally
        {
            UIConstants.EndSection();
        }
        }
        finally
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }
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

        try
        {

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
            new Vector4(1f, 0.4f, 0.4f, 1f), btnW, "Support development");
        ImGui.SameLine(0, 4);
        LinkBtn("Send Feedback", "https://docs.google.com/forms/d/1WQeblPTkupR3gvQnaDw0dJjy0OduWGX64nDI9b-tIQw/viewform",
            UIConstants.Success, btnW, "Open feedback form");

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

        DrawAccentButton(Lang.SubmitVenue, () => plugin.OwnerSubmitWindow.IsOpen = true, width: -1);

        ImGui.Spacing();

        var canUpdate = plugin.OwnerUpdateWindow.CanLoadCurrentVenue();
        DrawAccentButton(Lang.UpdateVenue, () =>
        {
            var config = plugin.ConfigManager.Config;
            var venue = config != null ? plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) : null;
            if (venue != null)
                plugin.OwnerVerifyWindow.BeginVerify(venue);
        }, disabled: !canUpdate, accent: UIConstants.Glow, width: -1);
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

        }
        finally
        {
            ImGui.PopTextWrapPos();
        }

        UIConstants.PushScrollbarStyle();
        try
        {
            if (ImGui.BeginChild("##changelogScroll", new Vector2(-1, -1)))
            {
                ImGui.PushTextWrapPos(0);
                try
                {
                    if (ChangelogData.Changelogs.TryGetValue(ChangelogData.PluginVersion, out var sections))
                        UIConstants.DrawChangelog(sections);
                }
                finally
                {
                    ImGui.PopTextWrapPos();
                }
            }
        }
        finally
        {
            ImGui.EndChild();
            UIConstants.PopScrollbarStyle();
        }
    }

    private static void LinkBtn(string label, string url, Vector4 col, float w, string tooltip)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(col, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(col, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(col, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.WithAlpha(col, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.Text, col);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding);
        var clicked = ImGui.Button($"{label}##{url}", new Vector2(w, 26));
        if (clicked)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = url, UseShellExecute = true }); } catch { }
        }
        UIConstants.DrawHoverPulseOverlay(url, ImGui.IsItemHovered(), clicked, col);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }

    private async System.Threading.Tasks.Task PullAsync()
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

    private static void DrawAccentButton(string label, Action onClick, bool disabled = false, Vector4? accent = null, float width = 0)
    {
        var col = accent ?? UIConstants.Primary;
        if (disabled) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(col, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(col, 0.35f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(col, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.WithAlpha(col, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.Text, col);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(14, ImGui.GetStyle().FramePadding.Y));
        var clicked = ImGui.Button(label, new Vector2(width, ImGui.GetFrameHeight()));
        if (clicked) onClick();
        if (!disabled)
            UIConstants.DrawHoverPulseOverlay(label, ImGui.IsItemHovered(), clicked, col);
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(5);
        if (disabled) ImGui.EndDisabled();
    }

    public void Dispose() { }
}
