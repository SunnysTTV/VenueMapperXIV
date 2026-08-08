using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class SetupWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private int step;
    private int langIdx;
    private bool markers3d = true;
    private bool notifications;
    private bool boostOpenVenues;
    private bool eventReminders;
    private bool autoPullOnStartup;
    private bool suppressInCombat;
    private ToastCorner toastPosition;
    private int maxVisibleToasts = 4;
    private float toastDurationSeconds = 3.0f;
    private bool eventRemindersFavOnly;
    private int eventReminderMinutes = 15;
    private string defaultTab = "remember";
    private const float ToastBaselineSeconds = 3.0f;
    public bool Forced { get; set; }
    private int lastRenderedStep = -1;
    private double stepEnteredTime;
    private const double StepWaitSeconds = 5.0;
    private const int StepCount = 4;

    public SetupWindow(VenueMapperPlugin plugin)
        : base("VenueMapper##Setup",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        Size = new Vector2(680, 680);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;

        RefreshFromConfig();
    }

    public void RefreshFromConfig()
    {
        step = 0;
        markers3d = plugin.Configuration.HasSeenSetup ? plugin.PictomancyMarkers.Enabled : true;
        notifications = plugin.Configuration.NotificationsEnabled;
        boostOpenVenues = plugin.Configuration.BoostOpenVenues;
        eventReminders = plugin.Configuration.EventReminders;
        autoPullOnStartup = plugin.Configuration.AutoPullOnStartup;
        suppressInCombat = plugin.Configuration.SuppressInCombat;
        toastPosition = plugin.Configuration.ToastPosition;
        maxVisibleToasts = plugin.Configuration.MaxVisibleToasts;
        toastDurationSeconds = plugin.Configuration.ToastDurationMultiplier * ToastBaselineSeconds;
        eventRemindersFavOnly = plugin.Configuration.EventRemindersFavoritesOnly;
        eventReminderMinutes = plugin.Configuration.EventReminderMinutes;
        defaultTab = plugin.Configuration.DefaultTab;
        langIdx = plugin.Configuration.Language == "DE" ? 1 : 0;
    }

    public override void PreDraw()
    {
        ShowCloseButton = !Forced;
        UIConstants.PushWindowChrome(UIConstants.Glow, 2f);

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    }

    public override void PostDraw()
    {
        UIConstants.PopWindowChrome();
    }

    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    public override void Draw()
    {

        var hackerBooting = UIConstants.IsHackerBooting;
        try
        {
            DrawContent(hackerBooting);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Error(ex, "[VenueMapper] SetupWindow draw failed");
        }
    }

    private void DrawContent(bool hackerBooting)
    {
        if (hackerBooting) ImGui.BeginDisabled();
        try
        {
            DrawContentInner();
        }
        finally
        {
            if (hackerBooting) ImGui.EndDisabled();
        }
    }

    private void DrawContentInner()
    {
        if (step != lastRenderedStep)
        {
            lastRenderedStep = step;
            stepEnteredTime = ImGui.GetTime();
        }
        var waitElapsed = ImGui.GetTime() - stepEnteredTime;
        var waitRemaining = Forced ? Math.Max(0.0, StepWaitSeconds - waitElapsed) : 0.0;

        DrawStepDots();
        ImGui.Spacing();

        ImGui.PushTextWrapPos(0);
        try
        {
            if (Forced)
            {
                DrawForcedBanner();
                ImGui.Spacing();
            }

            UIConstants.PushScrollbarStyle();
            var childOpen = ImGui.BeginChild("##setupContent", new Vector2(-1, -40));
            try
            {
                if (childOpen)
                {
                    switch (step)
                    {
                        case 0: DrawLanguage(); break;
                        case 1: DrawWelcome(); break;
                        case 2: DrawFeatures(); break;
                        case 3: DrawSettings(); break;
                    }
                }
            }
            finally
            {
                ImGui.EndChild();
                UIConstants.PopScrollbarStyle();
            }

            ImGui.Separator();
            ImGui.Spacing();

            if (!Forced)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.2f));
                ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f));
                if (ImGui.Button(Lang.SetupSkip, new Vector2(50, 26)))
                    IsOpen = false;
                ImGui.PopStyleColor(3);
                ImGui.SameLine();
            }

            if (step > 0)
            {
                if (ImGui.Button(Lang.SetupBack, new Vector2(80, 26)))
                    step--;
                ImGui.SameLine();
            }

            var rightX = ImGui.GetContentRegionAvail().X - 100;
            ImGui.Dummy(new Vector2(rightX, 0));
            ImGui.SameLine();

            var waiting = waitRemaining > 0;
            var nextLabel = waiting ? $"{Lang.SetupNext} ({(int)Math.Ceiling(waitRemaining)})" : Lang.SetupNext;
            var doneLabel = waiting ? $"{Lang.SetupDone} ({(int)Math.Ceiling(waitRemaining)})" : Lang.SetupDone;

            if (waiting) ImGui.BeginDisabled();
            try
            {
                if (step < 3)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Glow, 0.2f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Glow, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Glow);
                    if (ImGui.Button(nextLabel, new Vector2(waiting ? 130 : 100, 26)))
                        step++;
                    ImGui.PopStyleColor(3);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Primary, 0.3f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Primary, 0.5f));
                    ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Primary);
                    if (ImGui.Button(doneLabel, new Vector2(waiting ? 130 : 100, 26)))
                        Finish();
                    ImGui.PopStyleColor(3);
                }
            }
            finally
            {
                if (waiting) ImGui.EndDisabled();
            }
        }
        finally
        {
            ImGui.PopTextWrapPos();
        }

        HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName);
    }

    private void DrawStepDots()
    {
        var avail = ImGui.GetContentRegionAvail().X;
        const float spacing = 56f;
        const float r = 5f;
        var totalW = (StepCount - 1) * spacing;
        var startX = MathF.Max(0f, (avail - totalW) / 2f);
        ImGui.Dummy(new Vector2(1, 4));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);

        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var y = origin.Y + r;

        for (var i = 0; i < StepCount; i++)
        {
            var cx = origin.X + i * spacing;
            var center = new Vector2(cx, y);

            if (i < StepCount - 1)
            {
                var lineCol = i < step
                    ? UIConstants.WithAlpha(UIConstants.Glow, 0.6f)
                    : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.2f);
                dl.AddLine(new Vector2(cx + r, y), new Vector2(cx + spacing - r, y),
                    ImGui.ColorConvertFloat4ToU32(lineCol), 2f);
            }

            if (i == step)
                dl.AddCircleFilled(center, r + 2f, ImGui.ColorConvertFloat4ToU32(UIConstants.Glow), 20);
            else if (i < step)
                dl.AddCircleFilled(center, r, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.65f)), 20);
            else
                dl.AddCircle(center, r, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.35f)), 20, 1.4f);
        }

        ImGui.Dummy(new Vector2(avail, r * 2 + 8));
    }

    internal static void IconRow(FontAwesomeIcon icon, Vector4 col, string title, string? desc = null)
    {
        var iconFont = UiBuilder.IconFont;
        ImGui.PushFont(iconFont);
        ImGui.TextColored(col, icon.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine(28);
        ImGui.BeginGroup();
        ImGui.TextColored(UIConstants.TextPrimary, title);
        if (desc != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.55f));
            ImGui.TextWrapped(desc);
            ImGui.PopStyleColor();
        }
        ImGui.EndGroup();
        ImGui.Spacing();
    }

    private static void DrawForcedBanner()
    {
        var dl = ImGui.GetWindowDrawList();
        var avail = ImGui.GetContentRegionAvail().X;
        var pos = ImGui.GetCursorScreenPos();
        const float pad = 12f;
        var wrapWidth = avail - pad * 2;

        var titleLines = UIConstants.WrapText(Lang.SetupForcedBannerTitle, wrapWidth);
        var descLines = UIConstants.WrapText(Lang.SetupForcedBannerDesc, wrapWidth);
        var lineH = ImGui.GetTextLineHeightWithSpacing();
        var boxH = (titleLines.Count + descLines.Count) * lineH + pad * 2;

        dl.AddRectFilled(pos, pos + new Vector2(avail, boxH),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.12f)), 6f);
        dl.AddRect(pos, pos + new Vector2(avail, boxH),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.4f)), 6f, ImDrawFlags.None, 1.2f);

        var centerX = pos.X + avail / 2f;
        var y = pos.Y + pad;
        var titleCol = ImGui.ColorConvertFloat4ToU32(UIConstants.Glow);
        foreach (var line in titleLines)
        {
            var w = ImGui.CalcTextSize(line).X;
            dl.AddText(new Vector2(centerX - w / 2f, y), titleCol, line);
            y += lineH;
        }

        var descCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.75f));
        foreach (var line in descLines)
        {
            var w = ImGui.CalcTextSize(line).X;
            dl.AddText(new Vector2(centerX - w / 2f, y), descCol, line);
            y += lineH;
        }

        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + boxH + 4f));
    }

    private static void CenteredBigText(string text, Vector4 col, float scale)
    {
        ImGui.SetWindowFontScale(scale);
        var w = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - w) / 2f + ImGui.GetCursorPosX());
        ImGui.TextColored(col, text);
        ImGui.SetWindowFontScale(1f);
    }

    private static void CenteredPill(string text, Vector4 col)
    {
        var dl = ImGui.GetWindowDrawList();
        var textSz = ImGui.CalcTextSize(text);
        const float padX = 8f;
        const float padY = 3f;
        var pillW = textSz.X + padX * 2;
        var x = (ImGui.GetContentRegionAvail().X - pillW) / 2f + ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(x);
        var pos = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(pos, pos + new Vector2(pillW, textSz.Y + padY * 2),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(col, 0.18f)), (textSz.Y + padY * 2) / 2f);
        dl.AddText(pos + new Vector2(padX, padY), ImGui.ColorConvertFloat4ToU32(col), text);
        ImGui.Dummy(new Vector2(pillW, textSz.Y + padY * 2));
    }

    private void DrawWelcome()
    {
        ImGui.Spacing();
        CenteredBigText("VenueMapper", UIConstants.Primary, 1.6f);
        ImGui.Spacing();
        CenteredPill(ChangelogData.PluginVersion, UIConstants.Glow);
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextPrimary, Lang.SetupWelcomeTitle);
        ImGui.Spacing();
        ImGui.TextWrapped(Lang.SetupWelcomeDesc);

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Glow, 0.8f), Lang.SetupWhatYouGet);
        ImGui.Spacing();

        IconRow(FontAwesomeIcon.MapMarkedAlt, UIConstants.Primary, Lang.SetupFeature1);
        IconRow(FontAwesomeIcon.CalendarAlt, UIConstants.Secondary, Lang.SetupFeature2);
        IconRow(FontAwesomeIcon.Cube, new Vector4(0.2f, 1f, 0.5f, 1f), Lang.SetupFeature3);
        IconRow(FontAwesomeIcon.ListUl, UIConstants.Glow, Lang.SetupFeature4);
        IconRow(FontAwesomeIcon.IdBadge, new Vector4(1f, 0.84f, 0f, 1f), Lang.SetupFeature5);
        IconRow(FontAwesomeIcon.SyncAlt, UIConstants.TextSecondary, Lang.SetupFeature6);
    }

    private void DrawLanguage()
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Glow, Lang.SetupChooseLang);
        ImGui.Spacing();
        ImGui.Spacing();

        var languages = new[] { "English", "Deutsch" };
        var flags = new[] { "EN", "DE" };

        for (var i = 0; i < languages.Length; i++)
        {
            var selected = langIdx == i;
            ImGui.PushStyleColor(ImGuiCol.Button,
                selected ? UIConstants.WithAlpha(UIConstants.Glow, 0.22f) : UIConstants.WithAlpha(UIConstants.CardBackground, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Glow, 0.3f));
            ImGui.PushStyleColor(ImGuiCol.Text, selected ? UIConstants.Glow : UIConstants.TextPrimary);
            ImGui.PushStyleColor(ImGuiCol.Border, selected ? UIConstants.Glow : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.2f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding);
            var clicked = ImGui.Button($"{flags[i]}   {languages[i]}", new Vector2(-1, 38));
            UIConstants.DrawHoverPulseOverlay($"##lang{i}", ImGui.IsItemHovered(), clicked && langIdx != i, UIConstants.Glow);
            if (clicked && langIdx != i)
            {
                langIdx = i;
                Lang.Set(flags[i]);
                ChangelogData.CurrentLanguage = flags[i];
                plugin.Configuration.Language = flags[i];
                plugin.Configuration.Save();
            }
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(4);
            ImGui.Spacing();
        }

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            langIdx == 1 ? Lang.SetupLangHintDe : Lang.SetupLangHintEn);
    }

    private void DrawFeatures()
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Glow, Lang.SetupKeyFeatures);
        ImGui.Spacing();

        IconRow(FontAwesomeIcon.MapMarkedAlt, UIConstants.Primary, Lang.SetupFeatMap, Lang.SetupFeatMapDesc);
        IconRow(FontAwesomeIcon.ListUl, UIConstants.Glow, Lang.SetupFeatDir, Lang.SetupFeatDirDesc);
        IconRow(FontAwesomeIcon.CalendarAlt, UIConstants.Secondary, Lang.SetupFeatEvents, Lang.SetupFeatEventsDesc);
        IconRow(FontAwesomeIcon.Cube, new Vector4(0.2f, 1f, 0.5f, 1f), Lang.SetupFeat3D, Lang.SetupFeat3DDesc);
        IconRow(FontAwesomeIcon.Bell, new Vector4(1f, 0.5f, 0.2f, 1f), Lang.SetupFeatNotify, Lang.SetupFeatNotifyDesc);
        IconRow(FontAwesomeIcon.IdBadge, new Vector4(1f, 0.84f, 0f, 1f), Lang.SetupFeatOwner, Lang.SetupFeatOwnerDesc);
        IconRow(FontAwesomeIcon.SyncAlt, UIConstants.TextSecondary, Lang.SetupFeatUpdate, Lang.SetupFeatUpdateDesc);
    }

    private static void SectionHeader(FontAwesomeIcon icon, Vector4 col, string text)
    {
        var iconFont = UiBuilder.IconFont;
        ImGui.PushFont(iconFont);
        ImGui.TextColored(col, icon.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine(22);
        ImGui.TextColored(col, text.ToUpperInvariant());
        ImGui.Spacing();
    }

    private void DrawSettings()
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Glow, Lang.SetupQuickSettings);
        ImGui.Spacing();
        ImGui.Spacing();

        SectionHeader(FontAwesomeIcon.Bell, new Vector4(1f, 0.5f, 0.2f, 1f), Lang.Notifications);

        UIConstants.Toggle("##setupNotifications", ref notifications);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.EnableNotifications);

        if (!notifications) ImGui.BeginDisabled();
        ImGui.Indent();

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.NotificationPosition);
        ImGui.SameLine();
        var posLabel = CornerLabel(toastPosition);
        UIConstants.StyledCombo("##setupToastPos", posLabel, 150, () =>
        {
            foreach (var corner in new[] { ToastCorner.TopRight, ToastCorner.TopLeft, ToastCorner.BottomRight, ToastCorner.BottomLeft })
            {
                if (ImGui.Selectable(CornerLabel(corner), toastPosition == corner))
                    toastPosition = corner;
            }
        });

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.MaxVisibleToasts);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        ImGui.DragInt("##setupMaxVisible", ref maxVisibleToasts, 1, 1, 10);
        maxVisibleToasts = Math.Clamp(maxVisibleToasts, 1, 10);

        ImGui.SameLine(0, 24);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.ToastDurationLabel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.SliderFloat("##setupToastDuration", ref toastDurationSeconds, 1.5f, 9.0f, "%.1fs");

        ImGui.Spacing();
        UIConstants.Toggle("##setupSuppressCombat", ref suppressInCombat);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.SuppressInCombat);

        UIConstants.Toggle("##setupEventReminders", ref eventReminders);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.EventReminders);

        if (eventReminders)
        {
            UIConstants.Toggle("##setupEventRemindersFavOnly", ref eventRemindersFavOnly);
            ImGui.SameLine();
            ImGui.TextColored(UIConstants.TextPrimary, Lang.EventRemindersFavOnly);

            ImGui.SameLine();
            ImGui.TextColored(UIConstants.TextSecondary, Lang.EventReminderMinutesLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            ImGui.DragInt("##setupEventReminderMin", ref eventReminderMinutes, 1, 5, 60);
            eventReminderMinutes = Math.Clamp(eventReminderMinutes, 5, 60);
        }

        ImGui.Unindent();
        if (!notifications) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        SectionHeader(FontAwesomeIcon.SlidersH, UIConstants.Glow, Lang.MiscSettings);

        UIConstants.Toggle("##setupMarkers3d", ref markers3d);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.SetupEnable3D);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), Lang.SetupEnable3DDesc);

        ImGui.Spacing();

        UIConstants.Toggle("##setupBoostOpen", ref boostOpenVenues);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.BoostOpenVenuesTip);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.BoostOpenVenues);

        ImGui.SameLine(0, 24);
        UIConstants.Toggle("##setupAutoPull", ref autoPullOnStartup);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.AutoPullCfg);

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.DefaultTabLabel);
        ImGui.SameLine();
        UIConstants.StyledCombo("##setupDefaultTab", DefaultTabLabel(defaultTab), 140, () =>
        {
            foreach (var tab in new[] { "remember", "map", "dir", "evt" })
            {
                if (ImGui.Selectable(DefaultTabLabel(tab), defaultTab == tab))
                    defaultTab = tab;
            }
        });

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            Lang.SetupAllSet);
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f),
            Lang.SetupCommand);

        ImGui.Spacing();
        IconRow(FontAwesomeIcon.IdBadge, new Vector4(1f, 0.84f, 0f, 1f), Lang.SetupOwnerIdNoteTitle, Lang.SetupOwnerIdNoteDesc);
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

    private void Finish()
    {
        var codes = new[] { "EN", "DE" };
        var lang = codes[langIdx];

        plugin.Configuration.HasSeenSetup = true;
        plugin.Configuration.PendingForcedSetup = false;
        plugin.Configuration.Language = lang;
        plugin.Configuration.NotificationsEnabled = notifications;
        plugin.Configuration.BoostOpenVenues = boostOpenVenues;
        plugin.Configuration.EventReminders = eventReminders;
        plugin.Configuration.AutoPullOnStartup = autoPullOnStartup;
        plugin.Configuration.SuppressInCombat = suppressInCombat;
        plugin.Configuration.ToastPosition = toastPosition;
        plugin.Configuration.MaxVisibleToasts = maxVisibleToasts;
        plugin.Configuration.ToastDurationMultiplier = toastDurationSeconds / ToastBaselineSeconds;
        plugin.Configuration.EventRemindersFavoritesOnly = eventRemindersFavOnly;
        plugin.Configuration.EventReminderMinutes = eventReminderMinutes;
        plugin.Configuration.DefaultTab = defaultTab;
        plugin.Configuration.Markers3DEnabled = markers3d;
        plugin.Configuration.Save();

        Lang.Set(lang);
        ChangelogData.CurrentLanguage = lang;
        plugin.PictomancyMarkers.Enabled = markers3d;

        IsOpen = false;
        plugin.VenueMapWindow.IsOpen = true;
        plugin.VenueMapWindow.ShowDirectory();

        plugin.Toasts.Show(Lang.ToastSetupComplete, ToastKind.Success, 3.5);
    }

    public void Dispose() { }
}
