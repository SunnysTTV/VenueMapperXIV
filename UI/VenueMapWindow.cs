using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public partial class VenueMapWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private string currentFloorName = string.Empty;
    private string lastDetectedFloor = string.Empty;

    private float _fadeAlpha = 0f;
    private bool  _wasClosed = true;

    private float mapZoom = 10.0f;
    private float mapPanX, mapPanY;
    private uint  lastMapId;
    private const float ZoomMin = 8.0f;
    private const float ZoomMax = 12.0f;
    private const float ZoomDefault = 10.0f;
    private const float WardZoomMax = 10f;

    private bool selectDirTab;
    private bool selectEvtTab;
    private bool selectFesTab;
    private bool selectMapTab;
    private bool selectSettingsTab;
    private bool selectEggTab;
    private bool selectAbtTab;
    private string lastActiveTab = "map";
    private string currentTab = "map";
    private string searchText = string.Empty;
    private bool showHidden;
    private bool showOwnedOnly;
    private readonly HashSet<string> selectedDcs = new();
    private readonly HashSet<string> selectedServers = new();
    private readonly EventsView eventsView;
    private readonly Dictionary<string, double> favAnimStart = new();
    private readonly Dictionary<string, double> copyAnimStart = new();
    private readonly Dictionary<string, double> tpAnimStart = new();

    private static readonly Random EggRng = new();
    private const string DefaultTitle = "VenueMapper";
    private string rawTitle = DefaultTitle;

    private int titleClickCount;
    private DateTime lastTitleClickTime = DateTime.MinValue;
    private Vector2? pendingTitlePinPos;
    private bool titlePinHeld;
    private Vector2? pendingResetPos;
    private Vector2? pendingResetSize;

    private static readonly Vector2 DefaultWindowPos = new(100, 100);
    private static readonly Vector2 DefaultWindowSize = new(450, 700);

    public void ResetWindowPosition()
    {
        pendingResetPos = DefaultWindowPos;
        pendingResetSize = DefaultWindowSize;
        plugin.Configuration.WindowPosition = null;
        plugin.Configuration.WindowSize = null;
        plugin.Configuration.Save();
    }

    private static readonly string[] RandomTitlePool =
    [
        "Club Chaos Manager 9000",
        "Venue Vibes (TM)",
        "Not A Bug, A Feature Mapper",
        "Where Am I Even",
        "Certified Club Whisperer",
        "VenueMapper (Trust Me Bro Edition)",
        "Definitely Not Spyware",
        "Ward Wanderer 3000",
        "Plot Twist Finder",
        "Professional Lurker.exe",
        "Emergency Dance Floor Locator",
        "The Grand Opening Never Ends",
        "Certified Housing Goblin",
        "VenueMapper But Cooler",
        "Totally Legit Housing Tool",
        "Serial Plot Enthusiast",
        "Aetheryte Adjacent",
        "Map? I Barely Know Her",
        "The Venue Whisperer's Apprentice",
        "404: Subtlety Not Found",
    ];

    public VenueMapWindow(VenueMapperPlugin plugin)
        : base(DefaultTitle + "###VenueMapper",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.eventsView = new EventsView(plugin.PartakeApi);
        lastActiveTab = plugin.Configuration.LastActiveTab ?? "map";
        currentTab = plugin.Configuration.DefaultTab == "remember" ? lastActiveTab : (plugin.Configuration.DefaultTab ?? "map");
        Size = DefaultWindowSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = DefaultWindowSize };

        var savedPos = plugin.Configuration.WindowPosition;
        if (savedPos.HasValue)
        {
            Position = savedPos.Value;
            PositionCondition = ImGuiCond.FirstUseEver;
        }

        var savedSize = plugin.Configuration.WindowSize;
        if (savedSize.HasValue)
            Size = savedSize.Value;
    }

    public override void PreDraw()
    {
        if (_wasClosed) { _fadeAlpha = 0f; _wasClosed = false; }
        _fadeAlpha = MathF.Min(1f, _fadeAlpha + ImGui.GetIO().DeltaTime * (1f / 0.30f));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, _fadeAlpha);

        WindowName = rawTitle + "###VenueMapper";

        ImGui.PushStyleColor(ImGuiCol.WindowBg,          Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip,        Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive,  Vector4.Zero);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
    }

    private const float FrameCut = 12f;
    private const float TitleBarH = 30f;
    private const float TitleToolSize = 24f;
    private Vector2 titleMin, titleMax;

    private void DrawWindowFrame()
    {
        var min = ImGui.GetWindowPos();
        UIConstants.DrawHudFrame(ImGui.GetWindowDrawList(), min, min + ImGui.GetWindowSize(), FrameCut,
            ImGui.GetStyle().Alpha, plugin.EasterEggManager.IsRgbOverloadActive);
    }

    private void DrawTitleBar()
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var lineH = ImGui.GetTextLineHeight();
        var a = ImGui.GetStyle().Alpha;
        const float toolGap = 2f;
        const int toolCount = 4;
        var toolsW = TitleToolSize * toolCount + toolGap * (toolCount - 1);
        var dragW = Math.Max(avail - toolsW - 8f, 40f);

        ImGui.Dummy(new Vector2(dragW, TitleBarH));
        titleMin = start;
        titleMax = start + new Vector2(dragW, TitleBarH);

        HudIcons.Crystal(dl, new Vector2(start.X + 6f, start.Y + TitleBarH / 2f), lineH * 1.1f,
            U32(UIConstants.WithAlpha(UIConstants.Glow, a)));
        var shownTitle = UIConstants.FitText(rawTitle, dragW - 24f);
        if (UIConstants.OverrideMode != ColorOverrideMode.Hacker)
            dl.AddText(new Vector2(start.X + 20f, start.Y + (TitleBarH - lineH) / 2f),
                U32(UIConstants.WithAlpha(UIConstants.Heading, a)), shownTitle);
        if (shownTitle != rawTitle && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(titleMin, titleMax))
            ImGui.SetTooltip(rawTitle);
        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(titleMin, titleMax))
            ToggleMinimize();

        var toolPos = new Vector2(start.X + avail - toolsW, start.Y + (TitleBarH - TitleToolSize) / 2f);
        var step = new Vector2(TitleToolSize + toolGap, 0f);
        ImGui.SetCursorScreenPos(toolPos);
        if (TitleTool("##vmNotifHistory", FontAwesomeIcon.Bell, Lang.NotificationHistory))
            plugin.NotificationHistoryWindow.Open();
        ImGui.SetCursorScreenPos(toolPos + step);
        if (TitleTool("##vmChangelog", FontAwesomeIcon.FileAlt, Lang.UpdateNotes))
            plugin.UpdateNotesWindow.Open();
        ImGui.SetCursorScreenPos(toolPos + step * 2f);
        if (TitleTool("##vmMinimize", minimized ? FontAwesomeIcon.WindowMaximize : FontAwesomeIcon.WindowMinimize,
                minimized ? Lang.Restore : Lang.Minimize))
            ToggleMinimize();
        ImGui.SetCursorScreenPos(toolPos + step * 3f);
        if (TitleTool("##vmClose", FontAwesomeIcon.Times, Lang.Close))
            IsOpen = false;

        var winPos = ImGui.GetWindowPos();
        var winW = ImGui.GetWindowSize().X;
        if (!minimized)
            UIConstants.DrawTrimRule(dl, winPos.X + 14f, winPos.X + winW - 14f, start.Y + TitleBarH + 2f);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + TitleBarH));
        ImGui.Dummy(new Vector2(0, 8f));
    }

    private static bool TitleTool(string id, FontAwesomeIcon icon, string? tooltip)
    {
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(TitleToolSize, TitleToolSize);
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
            dl.AddRectFilled(pos, pos + size, U32(UIConstants.CardBackground), 3f);
        UIConstants.DrawIconCentered(dl, UiBuilder.IconFont.FontSize, pos + size / 2f, icon,
            U32(hovered ? UIConstants.TextPrimary : UIConstants.TextSecondary));
        if (hovered && tooltip != null)
            ImGui.SetTooltip(tooltip);
        return clicked;
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(4);
    }

    public void Stage(string tab, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport().Size;
        pendingResetPos = Vector2.Max(Vector2.Zero, (viewport - size * ImGuiHelpers.GlobalScale) / 2f);
        pendingResetSize = size;
        rawTitle = DefaultTitle;
        minimized = false;
        restorePending = false;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = DefaultWindowSize };
        selectMapTab = tab == "map";
        selectDirTab = tab == "dir";
        selectEvtTab = tab == "evt";
        selectFesTab = tab == "fes";
        selectSettingsTab = tab == "set";
        selectEggTab = tab == "egg";
        selectAbtTab = tab == "abt";
        IsOpen = true;
    }

    public void ExpandFestival(string festivalId) => expandedFestivals.Add(festivalId);

    public void ShowDirectory() { selectDirTab = true; }
    public void HideDirectory() { selectMapTab = true; }
    public void ShowEvents() { selectEvtTab = true; }
    public void ShowFestivals() { selectFesTab = true; }
    public void ShowSettings() { selectSettingsTab = true; }

    private bool IsMapAvailable(VenueConfig config)
        => plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) != null
           || plugin.PositionTracker.GetFestivalHere(config) != null
           || plugin.PositionTracker.GetWardVenues(config).Count > 0;

    public void TriggerRandomTitlePreview()
    {
        pendingTitlePinPos = ImGui.GetWindowPos();
        rawTitle = RandomTitlePool[EggRng.Next(RandomTitlePool.Length)];
    }

    public override void OnOpen()
    {
        otherGroupOpen = true;
        if (minimized)
        {
            minimized = false;
            restorePending = true;
        }

        if (plugin.ScreenshotMode)
        {
            rawTitle = DefaultTitle;
            return;
        }

        var eggs = plugin.EasterEggManager;

        if (eggs.IsEnabled(EasterEggIds.RandomTitle))
        {
            rawTitle = RandomTitlePool[EggRng.Next(RandomTitlePool.Length)];
        }
        else if (!eggs.IsDiscovered(EasterEggIds.RandomTitle) && EggRng.Next(30) == 0)
        {
            eggs.Unlock(EasterEggIds.RandomTitle);
            rawTitle = RandomTitlePool[EggRng.Next(RandomTitlePool.Length)];
        }
        else
        {
            rawTitle = DefaultTitle;
        }

        var defaultTab = plugin.Configuration.DefaultTab;
        var effectiveTab = defaultTab == "remember" ? lastActiveTab : defaultTab;

        if (effectiveTab == "map")
        {
            var cfg = plugin.ConfigManager.Config;
            if (cfg == null || !IsMapAvailable(cfg)) effectiveTab = "dir";
        }

        switch (effectiveTab)
        {
            case "dir": selectDirTab = true; break;
            case "evt": selectEvtTab = true; break;
            case "fes": selectFesTab = true; break;
            case "set": selectSettingsTab = true; break;
            case "egg": selectEggTab = true; break;
            case "abt": selectAbtTab = true; break;
            default: selectMapTab = true; break;
        }
    }

    private void SetActiveTab(string tab)
    {
        if (lastActiveTab == tab) return;
        lastActiveTab = tab;
        if (plugin.Configuration.DefaultTab != "remember") return;
        plugin.Configuration.LastActiveTab = tab;
        plugin.Configuration.Save();
    }

    private void CheckTitleClick()
    {
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left)) return;
        if (!ImGui.IsWindowHovered() || !ImGui.IsMouseHoveringRect(titleMin, titleMax)) return;

        if ((DateTime.Now - lastTitleClickTime).TotalSeconds > 2.5)
            titleClickCount = 0;
        titleClickCount++;
        lastTitleClickTime = DateTime.Now;

        if (titleClickCount < 5) return;
        titleClickCount = 0;

        plugin.EasterEggManager.Unlock(EasterEggIds.RandomTitle);
        TriggerRandomTitlePreview();
    }

    public override void OnClose()
    {
        _wasClosed = true;
        plugin.Configuration.WindowPosition = ImGui.GetWindowPos();
        plugin.Configuration.WindowSize = minimized ? restoreSize * ImGuiHelpers.GlobalScale : ImGui.GetWindowSize();
        plugin.Configuration.Save();
    }

    private readonly Dictionary<string, float> navTabAnim = new();

    private const float NavTabPadX = 10f;
    private const float NavTabGap = 6f;

    private bool DrawNavTab(string id, FontAwesomeIcon icon, string label, bool active, bool disabled = false)
    {
        var iconFont = UiBuilder.IconFont;
        var iconStr = icon.ToIconString();
        var iconSz = CalcIconSize(iconStr);
        var labelSz = ImGui.CalcTextSize(label);

        var height = ImGui.GetFrameHeight() + 6f;
        var collapsedW = iconSz.X + NavTabPadX * 2;
        var expandedW = iconSz.X + NavTabGap + labelSz.X + NavTabPadX * 2;

        navTabAnim.TryGetValue(id, out var t);
        var target = active ? 1f : 0f;
        var dt = ImGui.GetIO().DeltaTime;
        t += (target - t) * MathF.Min(1f, dt * 14f);
        navTabAnim[id] = t;

        var w = collapsedW + (expandedW - collapsedW) * t;
        var pos = ImGui.GetCursorScreenPos();

        bool hovered, clicked;
        using (ImRaii.Disabled(disabled))
        {
            ImGui.InvisibleButton($"##navtab_{id}", new Vector2(w, height));
            hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            clicked = !disabled && ImGui.IsItemClicked();
        }

        var dl = ImGui.GetWindowDrawList();

        if (t > 0.01f)
        {
            var barW = w * t;
            var barX = pos.X + (w - barW) / 2f;
            dl.AddRectFilled(new Vector2(barX, pos.Y + height - 2f), new Vector2(barX + barW, pos.Y + height),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Accent, t)));
        }

        var col = disabled ? UIConstants.WithAlpha(UIConstants.TextMuted, 0.6f)
                : active || hovered ? UIConstants.TextPrimary : UIConstants.TextSecondary;
        var iconCol = active ? UIConstants.Accent : col;

        var iconX = pos.X + NavTabPadX;
        var iconY = pos.Y + (height - iconSz.Y) * 0.5f;
        dl.AddText(iconFont, iconFont.FontSize, new Vector2(iconX, iconY), ImGui.ColorConvertFloat4ToU32(iconCol), iconStr);

        if (t > 0.05f)
        {
            var labelX = iconX + iconSz.X + NavTabGap;
            var labelY = pos.Y + (height - labelSz.Y) * 0.5f;
            dl.AddText(new Vector2(labelX, labelY), ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(col, t)), label);
        }

        return clicked;
    }

    private bool minimized;
    private bool restorePending;
    private Vector2 restoreSize = DefaultWindowSize;
    private const float MinimizedHeight = TitleBarH + 18f;

    private void ToggleMinimize()
    {
        if (!minimized)
        {
            restoreSize = ImGui.GetWindowSize() / ImGuiHelpers.GlobalScale;
            minimized = true;
        }
        else
        {
            minimized = false;
            restorePending = true;
        }
    }

    private void ApplyMinimizeSize()
    {
        if (minimized)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(DefaultWindowSize.X, MinimizedHeight),
                MaximumSize = new Vector2(10000f, MinimizedHeight),
            };
            return;
        }

        if (!restorePending) return;
        restorePending = false;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = DefaultWindowSize };
        Size = restoreSize;
        SizeCondition = ImGuiCond.Always;
        titlePinHeld = true;
    }

    public ScreenRect LastRect { get; private set; }

    public override void Draw()
    {
        LastRect = ScreenRect.FromCurrentWindow();
        try { DrawInner(); }
        catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "[VenueMapper] VenueMapWindow draw failed"); }
    }

    private void DrawInner()
    {
        if (pendingTitlePinPos.HasValue)
        {
            Position = pendingTitlePinPos.Value;
            PositionCondition = ImGuiCond.Always;
            pendingTitlePinPos = null;
            titlePinHeld = true;
        }
        else if (pendingResetPos.HasValue)
        {
            Position = pendingResetPos.Value;
            PositionCondition = ImGuiCond.Always;
            Size = pendingResetSize;
            SizeCondition = ImGuiCond.Always;
            pendingResetPos = null;
            pendingResetSize = null;
            titlePinHeld = true;
        }
        else if (titlePinHeld)
        {
            Position = null;
            Size = null;
            titlePinHeld = false;
        }

        ApplyMinimizeSize();
        DrawWindowFrame();
        DrawTitleBar();
        if (minimized) return;
        plugin.KonamiDetector.Update(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));

        var config = plugin.ConfigManager.Config;
        if (config == null || config.Venues.Count == 0)
        {
            ImGui.TextColored(UIConstants.TextSecondary, Lang.NoVenues);
            return;
        }

        var inVenue = IsMapAvailable(config);

        if (selectMapTab) currentTab = "map";
        if (selectDirTab) currentTab = "dir";
        if (selectEvtTab) currentTab = "evt";
        if (selectFesTab) currentTab = "fes";
        if (selectSettingsTab) currentTab = "set";
        if (selectEggTab) currentTab = "egg";
        if (selectAbtTab) currentTab = "abt";
        selectMapTab = selectDirTab = selectEvtTab = selectFesTab = selectSettingsTab = selectEggTab = selectAbtTab = false;

        if (currentTab == "map" && !inVenue) currentTab = "dir";

        var hackerBooting = plugin.EasterEggManager.IsHackerModeBooting;
        using (ImRaii.Disabled(hackerBooting))
            DrawTabs(config, inVenue);

        if (!hackerBooting) CheckTitleClick();

        HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, rawTitle, TitleBarH, FrameCut, 34f, 8f);
    }

    private void DrawTabs(VenueConfig config, bool inVenue)
    {
        var navTabs = new (string Id, FontAwesomeIcon Icon, string Label, bool Disabled)[]
        {
            ("map", FontAwesomeIcon.Map, Lang.Map, !inVenue),
            ("dir", FontAwesomeIcon.List, Lang.Directory, false),
            ("evt", FontAwesomeIcon.CalendarAlt, Lang.Events, false),
            ("fes", FontAwesomeIcon.Flag, Lang.Festivals, false),
            ("set", FontAwesomeIcon.Cog, Lang.Settings, false),
            ("egg", FontAwesomeIcon.Certificate, Lang.EasterEggs, false),
            ("abt", FontAwesomeIcon.InfoCircle, Lang.About, false),
        };

        for (var i = 0; i < navTabs.Length; i++)
        {
            var (id, icon, label, disabled) = navTabs[i];
            var isActive = id == currentTab;

            if (DrawNavTab(id, icon, label, isActive, disabled))
                currentTab = id;

            if (id == "map" && !inVenue && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                using var tooltip = ImRaii.Tooltip();
                using var wrap = ImRaii.TextWrapPos(250f);
                ImGui.TextColored(UIConstants.TextSecondary, Lang.MapUnavailable);
                ImGui.TextUnformatted(Lang.MapNotInVenue);
            }

            if (i < navTabs.Length - 1)
                ImGui.SameLine(0, 2);
        }

        var tabBottom = ImGui.GetItemRectMax().Y;
        var x0 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMin().X;
        var x1 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        ImGui.GetWindowDrawList().AddLine(new Vector2(x0, tabBottom), new Vector2(x1, tabBottom),
            ImGui.ColorConvertFloat4ToU32(UIConstants.Line), 1f);
        ImGui.Spacing();
        ImGui.Spacing();

        SetActiveTab(currentTab);
        switch (currentTab)
        {
            case "map":
                DrawMapOrDirectory(config);
                break;
            case "dir":
                DrawDirectoryTab(config);
                break;
            case "evt":
                DrawEventsTab(config);
                break;
            case "fes":
                DrawFestivalsTab(config);
                break;
            case "set":
                plugin.SettingsWindow.DrawSettingsTab();
                break;
            case "egg":
                plugin.SettingsWindow.DrawEasterEggsTab();
                break;
            case "abt":
                plugin.SettingsWindow.DrawAboutTab();
                break;
        }
    }

    private void DrawEventsTab(VenueConfig config)
    {
        eventsView.SetVenues(config.Venues, plugin.Configuration.HiddenVenueIds);
        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##eventsScroll", new Vector2(-1, -1));
        if (child)
            eventsView.Draw(ref showHidden);
    }

    private static Vector2 CalcIconSize(string iconStr)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
            return ImGui.CalcTextSize(iconStr);
    }

    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    private readonly Dictionary<string, (bool isOpen, double checkedAt)> openNowCache = new();
    private const double OpenNowCacheSeconds = 5.0;

    private bool IsVenueOpenCached(Venue v)
    {
        var now = ImGui.GetTime();
        if (openNowCache.TryGetValue(v.VenueId, out var cached) && now - cached.checkedAt < OpenNowCacheSeconds)
            return cached.isOpen;

        var isOpen = IsVenueOpenNow(v);
        openNowCache[v.VenueId] = (isOpen, now);
        return isOpen;
    }

    private bool IsVenueOpenNow(Venue v) => plugin.GetVenueOpening(v).Open;

    private readonly Dictionary<string, (bool today, double checkedAt)> openTodayCache = new();

    private bool IsVenueOpenTodayCached(Venue v)
    {
        var now = ImGui.GetTime();
        if (openTodayCache.TryGetValue(v.VenueId, out var cached) && now - cached.checkedAt < OpenNowCacheSeconds)
            return cached.today;

        var today = plugin.IsOpenToday(v, DateTime.UtcNow);
        openTodayCache[v.VenueId] = (today, now);
        return today;
    }

    private void DrawDirectoryTab(VenueConfig config)
    {
        DrawDirectoryHeader(config.Venues.Count);
        ImGui.Spacing();
        DrawDirectoryFilters();
        ImGui.Spacing();

        var filtered = config.Venues.AsEnumerable();
        if (!showHidden)
            filtered = filtered.Where(v => !plugin.Configuration.HiddenVenueIds.Contains(v.VenueId));
        if (showOwnedOnly)
        {
            var myHash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
            filtered = filtered.Where(v => v.OwnerIdHashes.Contains(myHash));
        }
        if (!string.IsNullOrEmpty(searchText))
            filtered = filtered.Where(v =>
                v.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                v.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        if (selectedDcs.Count > 0)
            filtered = filtered.Where(v => selectedDcs.Contains(v.Datacenter));
        if (selectedServers.Count > 0)
            filtered = filtered.Where(v => selectedServers.Any(s =>
                string.Equals(s, v.Server, StringComparison.OrdinalIgnoreCase)));

        var now = DateTime.UtcNow;
        var baseList = filtered.ToList();
        DrawOverviewStats(config, baseList, now);
        DrawQuickChips();

        IEnumerable<Venue> quick = baseList;
        if (quickOpen)
            quick = quick.Where(IsVenueOpenCached);
        if (quickFavorites)
            quick = quick.Where(v => plugin.Configuration.FavoriteVenueIds.Contains(v.VenueId));
        if (quickToday)
            quick = quick.Where(IsVenueOpenTodayCached);
        if (quickMyDc && plugin.PositionTracker.CurrentDatacenter is { Length: > 0 } myDc)
            quick = quick.Where(v => string.Equals(v.Datacenter, myDc, StringComparison.OrdinalIgnoreCase));

        var venues = quick
            .OrderBy(v =>
            {
                if (!plugin.Configuration.FavoriteVenueIds.Contains(v.VenueId)) return 1;
                if (favAnimStart.TryGetValue(v.VenueId, out var t) && ImGui.GetTime() - t < 3.0) return 1;
                return 0;
            })
            .ToList();

        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##venueScroll", new Vector2(-1, -1));
        if (!child) return;

        if (venues.Count == 0)
        {
            ImGui.TextColored(UIConstants.TextMuted, showOwnedOnly ? Lang.NoOwnedVenues : Lang.DirNoMatch);
            return;
        }

        if (!plugin.Configuration.BoostOpenVenues)
        {
            DrawVenueDirectory(config, venues);
            return;
        }

        var hereVenue = plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config);
        var open = venues.Where(IsVenueOpenCached).ToList();
        var soon = venues.Where(v => !open.Contains(v) && NextOpening(v, now) is { } start && start - now <= SoonWindow).ToList();
        var other = venues.Where(v => !open.Contains(v) && !soon.Contains(v)).ToList();

        DrawVenueGroup(Lang.DirGroupOpen, open, hereVenue, false);
        DrawVenueGroup(Lang.DirGroupSoon, soon, hereVenue, false);
        DrawVenueGroup(open.Count + soon.Count > 0 ? Lang.DirGroupOther : Lang.DirGroupAll, other, hereVenue, open.Count + soon.Count > 0);
        DrawEnterVenueHint();
    }

    private static readonly TimeSpan SoonWindow = TimeSpan.FromHours(12);
    private bool quickOpen;
    private bool quickFavorites;
    private bool quickToday;
    private bool quickMyDc;
    private bool otherGroupOpen = true;

    private DateTime? NextOpening(Venue v, DateTime now)
        => plugin.GetVenueOpening(v, false).Next is { } start && start > now ? start : null;

    private void DrawOverviewStats(VenueConfig config, List<Venue> venues, DateTime now)
    {
        var openCount = venues.Count(IsVenueOpenCached);
        var todayCount = venues.Count(IsVenueOpenTodayCached);
        var festivalCount = config.Festivals.Count(f => f.IsLive(now));

        const float gap = 6f;
        var avW = ImGui.GetContentRegionAvail().X;
        var tileW = (avW - gap * 2f) / 3f;
        var lineH = ImGui.GetTextLineHeight();
        var tileH = lineH * 2.3f + 10f;

        if (StatTile("##statOpen", openCount, Lang.StatOpenNow, UIConstants.Success, tileW, tileH, quickOpen))
            quickOpen = !quickOpen;
        ImGui.SameLine(0, gap);
        if (StatTile("##statToday", todayCount, Lang.StatOpenToday, UIConstants.TextPrimary, tileW, tileH, quickToday))
            quickToday = !quickToday;
        ImGui.SameLine(0, gap);
        if (StatTile("##statFestivals", festivalCount, Lang.StatFestivalsLive, UIConstants.Rose, tileW, tileH, false))
            selectFesTab = true;
        ImGui.Dummy(new Vector2(0, 2f));
    }

    private static bool StatTile(string id, int value, string label, Vector4 color, float width, float height, bool active)
    {
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        var max = pos + new Vector2(width, height);

        dl.AddRectFilled(pos, max, U32(hovered ? UIConstants.Hover : UIConstants.PanelRaised), 3f);
        dl.AddRect(pos, max, U32(active ? UIConstants.WithAlpha(UIConstants.Accent, 0.7f) : UIConstants.Line), 3f, ImDrawFlags.None, 1f);

        var font = ImGui.GetFont();
        var big = ImGui.GetFontSize() * 1.35f;
        var text = value.ToString();
        dl.AddText(font, big, pos + new Vector2(9f, 4f), U32(value > 0 ? color : UIConstants.TextMuted), text);
        var labelText = UIConstants.FitText(label, width - 14f);
        dl.AddText(pos + new Vector2(9f, 4f + big + 1f), U32(UIConstants.TextMuted), labelText);
        if (hovered && labelText != label)
            ImGui.SetTooltip(label);
        return clicked;
    }

    private void DrawQuickChips()
    {
        QuickChip("##qOpen", Lang.ChipOpenNow, ref quickOpen, true);
        QuickChip("##qFav", Lang.ChipFavorites, ref quickFavorites, false);
        QuickChip("##qToday", Lang.ChipOpenToday, ref quickToday, false);
        if (plugin.PositionTracker.CurrentDatacenter.Length > 0)
            QuickChip("##qMyDc", Lang.ChipMyDatacenter, ref quickMyDc, false);
        ImGui.Dummy(new Vector2(0, 2f));
    }

    private static void QuickChip(string id, string label, ref bool value, bool first)
    {
        var textSize = ImGui.CalcTextSize(label);
        var size = new Vector2(textSize.X + 20f, textSize.Y + 6f);
        if (!first)
            UIConstants.FlowNext(size.X, 5f);
        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton(id, size))
            value = !value;
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        var max = pos + size;
        var r = size.Y / 2f;
        var accent = UIConstants.Accent;

        dl.AddRectFilled(pos, max, U32(value ? UIConstants.WithAlpha(accent, 0.14f) : hovered ? UIConstants.Hover : UIConstants.WithAlpha(UIConstants.PanelRaised, 0.8f)), r);
        dl.AddRect(pos, max, U32(value ? UIConstants.WithAlpha(accent, 0.7f) : UIConstants.LineStrong), r, ImDrawFlags.None, 1f);
        dl.AddText(pos + new Vector2(10f, 3f), U32(value ? accent : hovered ? UIConstants.TextPrimary : UIConstants.TextSecondary), label);
    }

    private void DrawVenueGroup(string title, List<Venue> venues, Venue? hereVenue, bool collapsible)
    {
        if (venues.Count == 0) return;

        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var avW = ImGui.GetContentRegionAvail().X;
        var lineH = ImGui.GetTextLineHeight();
        var headerH = lineH + 8f;
        var expanded = !collapsible || otherGroupOpen;

        var clicked = ImGui.InvisibleButton($"##group_{title}", new Vector2(avW, headerH));
        var hovered = collapsible && ImGui.IsItemHovered();
        if (clicked && collapsible)
            otherGroupOpen = !otherGroupOpen;

        var y = pos.Y + (headerH - lineH) / 2f + 2f;
        var x = pos.X + 2f;
        if (collapsible)
        {
            var chevron = (expanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight).ToIconString();
            var iconFont = UiBuilder.IconFont;
            var iconSize = iconFont.FontSize * 0.7f;
            dl.AddText(iconFont, iconSize, new Vector2(x, pos.Y + (headerH - iconSize) / 2f + 2f),
                U32(hovered ? UIConstants.Accent : UIConstants.TextMuted), chevron);
            x += 14f;
        }
        dl.AddText(new Vector2(x, y), U32(UIConstants.Heading), title.ToUpperInvariant());
        var count = venues.Count.ToString();
        dl.AddText(new Vector2(pos.X + avW - ImGui.CalcTextSize(count).X - 4f, y), U32(UIConstants.TextMuted), count);

        if (!expanded)
        {
            var more = Lang.DirShowMore(venues.Count);
            var moreY = pos.Y + headerH;
            ImGui.SetCursorScreenPos(new Vector2(pos.X, moreY));
            if (ImGui.InvisibleButton($"##groupMore_{title}", new Vector2(avW, lineH + 10f)))
                otherGroupOpen = true;
            var moreHovered = ImGui.IsItemHovered();
            dl.AddLine(new Vector2(pos.X, moreY), new Vector2(pos.X + avW, moreY), U32(UIConstants.Line));
            dl.AddText(new Vector2(pos.X + RowPadX + 14f, moreY + 5f), U32(moreHovered ? UIConstants.Accent : UIConstants.TextMuted), more);
            return;
        }

        for (var i = 0; i < venues.Count; i++)
            DrawVenueRow(venues[i], i == 0, venues[i] == hereVenue);
        ImGui.Dummy(new Vector2(0, 4f));
    }

    private void DrawEnterVenueHint()
    {
        ImGui.Spacing();
        ImGui.Spacing();
        var hint = Lang.EnterVenue;
        var hSz = ImGui.CalcTextSize(hint);
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - hSz.X) / 2f + ImGui.GetCursorPosX());
        ImGui.TextColored(UIConstants.TextMuted, hint);
    }

    private readonly Dictionary<string, Floor> gardenFloors = new();

    private Floor GardenFloorOf(Venue venue)
    {
        var existing = venue.Floors.FirstOrDefault(f => HousingData.IsGarden(f.Name));
        if (existing != null) return existing;
        if (!gardenFloors.TryGetValue(venue.VenueId, out var floor))
            gardenFloors[venue.VenueId] = floor = new Floor { Name = HousingData.GardenFloor };
        return floor;
    }

    private void DrawMapOrDirectory(VenueConfig config)
    {
        var tracker = plugin.PositionTracker;
        var currentVenue = tracker.GetVenueAtCurrentPlotIncludingGarden(config);

        var wardVenues = tracker.GetWardVenues(config);

        if (currentVenue == null)
        {
            var festival = tracker.GetFestivalHere(config);
            if (festival != null)
            {
                DrawFestivalMapView(festival);
                return;
            }
            if (wardVenues.Count > 0)
            {
                DrawWardHeader(wardVenues.Count);
                ImGui.Spacing();
                DrawWardView(wardVenues, null);
                return;
            }
            DrawDirectoryHeader(config.Venues.Count);
            ImGui.Spacing();
            DrawVenueDirectory(config, config.Venues);
            return;
        }

        var inside = tracker.IsInsideHouse;
        var garden = GardenFloorOf(currentVenue);
        var floors = inside
            ? currentVenue.Floors.Where(f => !HousingData.IsGarden(f.Name)).Append(garden).ToList()
            : new List<Floor> { garden, WardOverviewFloor };
        var detectedFloor = inside ? tracker.GetCurrentFloor(currentVenue) : garden;

        if (detectedFloor != null && detectedFloor.Name != lastDetectedFloor)
        {
            currentFloorName = detectedFloor.Name;
            lastDetectedFloor = detectedFloor.Name;
        }

        var selectedFloor = floors.FirstOrDefault(f => f.Name == currentFloorName)
                            ?? detectedFloor
                            ?? floors.FirstOrDefault();

        DrawHeaderBar(currentVenue, UIConstants.DisplayName(currentVenue.Name).ToUpperInvariant(),
            selectedFloor != null ? TranslateFloorName(selectedFloor.Name) : "-");
        ImGui.Spacing();
        DrawFloorTabs(floors, ref selectedFloor);
        currentFloorName = selectedFloor?.Name ?? string.Empty;

        ImGui.Spacing();
        DrawGuideBar();

        if (selectedFloor != null)
        {
            if (selectedFloor == WardOverviewFloor)
            {
                DrawWardView(wardVenues, currentVenue);
            }
            else if (HousingData.IsGarden(selectedFloor.Name))
            {
                DrawGardenMap(currentVenue, selectedFloor);
            }
            else
            {
                var territoryId = tracker.CurrentTerritoryId;
                var floorMapId = plugin.HousingData.FloorMapId(territoryId, selectedFloor.Name);
                if (floorMapId == 0) floorMapId = selectedFloor.MapId;
                if (floorMapId == 0 && selectedFloor == detectedFloor) floorMapId = tracker.CurrentMapId;
                DrawMapPanel(selectedFloor.Services, territoryId, floorMapId);
            }
        }

        DrawFilterChips(currentVenue.Floors.SelectMany(f => f.Services));
    }

    private static readonly Floor WardOverviewFloor = new() { Name = "ward" };

    private sealed record MapPin(Vector2 World, Vector4 Color, Vector4 Edge, string Label, Venue Venue);

    private void DrawWardHeader(int count)
    {
        var tracker = plugin.PositionTracker;
        var title = $"{HousingData.EnglishName(tracker.CurrentDistrict)} · {Lang.WardLabel} {tracker.CurrentWard + 1}";
        DrawHeaderBar(null, title.ToUpperInvariant(), $"{tracker.CurrentServerName}{Lang.Location(count)}");
    }

    private void DrawWardView(IReadOnlyList<Venue> venues, Venue? current)
    {
        var tracker = plugin.PositionTracker;
        var district = tracker.CurrentDistrict;
        var territoryId = tracker.CurrentTerritoryId;

        var pinsByMap = new Dictionary<uint, List<MapPin>>();
        foreach (var venue in venues)
        {
            if (!plugin.HousingData.TryGetPlotMapLink(district, venue.Plot, out _, out var pinMap, out var pos)) continue;
            if (!pinsByMap.TryGetValue(pinMap, out var list))
                pinsByMap[pinMap] = list = new List<MapPin>();
            list.Add(new MapPin(new Vector2(pos.X, pos.Z), VenueAccent(venue), VenuePalette(venue).Secondary, UIConstants.DisplayName(venue.Name), venue));
        }

        if (pinsByMap.Count == 0)
        {
            ImGui.TextColored(UIConstants.TextMuted, Lang.GardenNoMap);
            return;
        }

        var mapId = pinsByMap.ContainsKey(tracker.CurrentMapId)
            ? tracker.CurrentMapId
            : pinsByMap.OrderByDescending(kv => kv.Value.Count).First().Key;
        var pins = pinsByMap[mapId];

        var mapInfo = plugin.MapLoader.GetMapInfoByMapId(mapId);
        var sizeFactor = mapInfo?.SizeFactor > 0 ? mapInfo.SizeFactor : (ushort)200;
        var center = new Vector2(-(mapInfo?.OffsetX ?? 0), -(mapInfo?.OffsetY ?? 0));
        var span = 2048f * 100f / sizeFactor;

        var services = current != null ? GardenFloorOf(current).Services : (IReadOnlyList<Service>)Array.Empty<Service>();
        DrawMapPanel(services, territoryId, mapId, center, span, 0f, pins, current, fullMap: true);
    }

    private void DrawGardenMap(Venue venue, Floor floor)
    {
        var district = plugin.PositionTracker.GetVenueDistrict(venue);
        if (!plugin.HousingData.TryGetPlotMapLink(district, venue.Plot, out var territoryId, out var mapId, out var plotPos))
        {
            ImGui.TextColored(UIConstants.TextMuted, Lang.GardenNoMap);
            return;
        }

        var span = venue.HouseSize switch
        {
            "L" => 70f,
            "M" => 55f,
            _   => 42f,
        };
        DrawMapPanel(floor.Services, territoryId, mapId, new Vector2(plotPos.X, plotPos.Z), span);
    }

    private void DrawFestivalMapView(Festival festival)
    {
        var now = DateTime.UtcNow;
        var status = festival.CurrentDay(now) is { } today
            ? $"{Lang.FestivalLive} · {Lang.FestivalUntil(UIConstants.FormatServerTime(today.End))}"
            : FestivalWhenText(festival, now);
        DrawHeaderBar(null, festival.Name.ToUpperInvariant(), status);
        ImGui.Spacing();
        DrawGuideBar();

        var (territoryId, mapId) = plugin.FestivalMap(festival);
        var location = festival.Location;
        DrawMapPanel(festival.Services, territoryId, mapId, new Vector2(location.X, location.Y),
            Math.Max(40f, location.Radius * 2.4f), location.Radius);
        DrawFilterChips(festival.Services);
    }

    private void DrawDirectoryHeader(int count)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();

        ImGui.TextColored(UIConstants.Heading, Lang.VenueDirectory);
        ImGui.SameLine(0, 0);
        ImGui.TextColored(UIConstants.TextMuted, Lang.Location(count));

        var lsLoaded = plugin.Lifestream.IsLoaded;
        var lsLabel = lsLoaded ? "Lifestream" : $"Lifestream: {Lang.NotInstalled}";
        const float dotSpace = 12f;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(lsLabel).X - dotSpace));
        var dotPos = ImGui.GetCursorScreenPos() + new Vector2(3f, lineH * 0.5f);
        var pulse = (MathF.Sin((float)ImGui.GetTime() * 2.5f) + 1f) / 2f;
        dl.AddCircleFilled(dotPos, 3f, ImGui.ColorConvertFloat4ToU32(lsLoaded
            ? UIConstants.WithAlpha(UIConstants.Success, 0.35f + 0.65f * pulse)
            : UIConstants.Rose));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + dotSpace);
        ImGui.TextColored(lsLoaded ? UIConstants.TextSecondary : UIConstants.Rose, lsLabel);
        if (lsLoaded && ImGui.IsItemHovered())
            ImGui.SetTooltip($"Lifestream: {Lang.Active}");

        var y = ImGui.GetCursorScreenPos().Y;
        var x0 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMin().X;
        var x1 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), ImGui.ColorConvertFloat4ToU32(UIConstants.Line), 1f);
        ImGui.Dummy(new Vector2(0, 2));
    }

    private void DrawDirectoryFilters()
    {
        var avW = ImGui.GetContentRegionAvail().X;

        using var colors = ImRaii.PushColor(ImGuiCol.FrameBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.9f))
                                 .Push(ImGuiCol.Border, UIConstants.GlowDim);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f)
                                .Push(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding);

        var toggleGlyph = (showHidden ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash).ToIconString();
        var crownGlyph = FontAwesomeIcon.Crown.ToIconString();
        var toggleW = CalcIconSize(toggleGlyph).X + 12f;
        var crownW = CalcIconSize(crownGlyph).X + 12f;

        ImGui.SetNextItemWidth(avW - toggleW - crownW - 8);
        ImGui.InputTextWithHint("##venueSearch", Lang.Search, ref searchText, 128);

        ImGui.SameLine(0, 4);
        if (FilterIconButton($"{toggleGlyph}##showHidden", toggleW, showHidden, UIConstants.Glow, null))
            showHidden = !showHidden;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Lang.ShowHidden);

        ImGui.SameLine(0, 4);
        if (FilterIconButton($"{crownGlyph}##showOwnedOnly", crownW, showOwnedOnly, UIConstants.GoldAccent,
                showOwnedOnly ? UIConstants.GoldAccent : UIConstants.TextPrimary))
            showOwnedOnly = !showOwnedOnly;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Lang.ShowOwnedOnly);

        DrawServerFilters(avW);
    }

    private static bool FilterIconButton(string label, float width, bool active, Vector4 accent, Vector4? text)
    {
        using var colors = ImRaii.PushColor(ImGuiCol.Button, active
                                     ? UIConstants.WithAlpha(accent, 0.3f)
                                     : UIConstants.WithAlpha(UIConstants.CardBackground, 0.9f))
                                 .Push(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(accent, 0.4f))
                                 .Push(ImGuiCol.ButtonActive, UIConstants.WithAlpha(accent, 0.5f))
                                 .Push(ImGuiCol.Text, text ?? Vector4.Zero, text.HasValue);
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.Button(label, new Vector2(width, 0));
    }

    private void DrawServerFilters(float avW)
    {
        var hasServerFilter = selectedDcs.Count > 0;
        var dcW = hasServerFilter ? 0.5f : 1.0f;

        var dcLabel = selectedDcs.Count == 0 ? Lang.AllDc : string.Join(", ", selectedDcs);
        UIConstants.StyledCombo("##dcFilter", dcLabel, avW * dcW - (hasServerFilter ? 2f : 0f), () =>
        {
            if (ImGui.Selectable(Lang.AllDc, selectedDcs.Count == 0))
            {
                selectedDcs.Clear();
                selectedServers.Clear();
            }
            ImGui.Separator();
            foreach (var dc in new[] { "Aether", "Primal", "Crystal", "Dynamis", "Light", "Chaos", "Materia", "Elemental", "Gaia", "Mana", "Meteor" })
            {
                var on = selectedDcs.Contains(dc);
                if (ImGui.Checkbox(dc, ref on))
                {
                    if (on) selectedDcs.Add(dc);
                    else
                    {
                        selectedDcs.Remove(dc);
                        foreach (var srv in Models.ServerData.GetServers(dc))
                            selectedServers.Remove(srv);
                    }
                }
            }
        });

        if (hasServerFilter)
        {
            ImGui.SameLine(0, 4);
            var srvLabel = selectedServers.Count == 0 ? Lang.AllServers : string.Join(", ", selectedServers);
            UIConstants.StyledCombo("##srvFilter", srvLabel, avW * 0.5f - 2f, () =>
            {
                if (ImGui.Selectable(Lang.AllServers, selectedServers.Count == 0))
                    selectedServers.Clear();
                ImGui.Separator();
                foreach (var dc in selectedDcs)
                {
                    ImGui.TextColored(UIConstants.Heading, dc);
                    foreach (var srv in Models.ServerData.GetServers(dc))
                    {
                        var on = selectedServers.Contains(srv);
                        if (ImGui.Checkbox($"{srv}##{dc}", ref on))
                        {
                            if (on) selectedServers.Add(srv);
                            else selectedServers.Remove(srv);
                        }
                    }
                    ImGui.Separator();
                }
            });
        }
    }

    private const float RowPadX = 12f;
    private const float RowPadY = 9f;
    private const float RowLineGap = 3f;

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    private void DrawVenueDirectory(VenueConfig config, List<Venue> venues)
    {
        var tracker = plugin.PositionTracker;
        var hereVenue = tracker.GetVenueAtCurrentPlotIncludingGarden(config);
        for (var i = 0; i < venues.Count; i++)
            DrawVenueRow(venues[i], i == 0, venues[i] == hereVenue);
        DrawEnterVenueHint();
    }

    private (string Text, bool Open) GetVenueStatus(Venue v)
    {
        var status = plugin.GetVenueStatus(v);
        return (status.Text, status.Open);
    }

    private VenueEvent? GetNextEvent(Venue v)
    {
        if (v.TeamId <= 0) return null;
        var now = DateTime.UtcNow;
        VenueEvent? next = null;
        foreach (var e in plugin.PartakeApi.GetEvents(v.TeamId))
        {
            if (e.EndTime < now || e.StartTime - now > TimeSpan.FromDays(7) || string.IsNullOrWhiteSpace(e.Title)) continue;
            if (next == null || e.StartTime < next.StartTime) next = e;
        }
        return next;
    }

    private static (Vector4 Primary, Vector4 Secondary, Vector4 Accent) VenuePalette(Venue v)
    {
        if (UIConstants.OverrideMode != ColorOverrideMode.None)
            return (UIConstants.Accent, UIConstants.Accent, UIConstants.Accent);
        if (v.Colors == null)
            return (UIConstants.TextPrimary, UIConstants.TextSecondary, UIConstants.TextPrimary);
        return (v.Colors.PrimaryVec, v.Colors.SecondaryVec, v.Colors.AccentVec);
    }

    private static Vector4 VenueAccent(Venue v)
        => UIConstants.OverrideMode != ColorOverrideMode.None ? UIConstants.Accent
            : v.Colors == null ? UIConstants.TextPrimary
            : v.Colors.PrimaryVec;

    private static string FormatAddress(string? address)
    {
        if (string.IsNullOrEmpty(address)) return "";
        var parts = address.Split(" - ");
        return parts.Length == 5
            ? $"{parts[1]} · {parts[2]} · {parts[3]}, {parts[4]}"
            : string.Join(" · ", parts);
    }

    private static Vector2 TeleportButtonSize(bool prominent, float lineH)
        => prominent
            ? new Vector2(ImGui.CalcTextSize(Lang.Visit).X + lineH + 22f, lineH + 12f)
            : new Vector2(lineH + 14f, lineH + 12f);

    private void DrawVenueRow(Venue v, bool first, bool isHere)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var avW = ImGui.GetContentRegionAvail().X;
        var time = ImGui.GetTime();

        var (statusText, isOpen) = GetVenueStatus(v);
        var nextEvent = GetNextEvent(v);
        var isFav = plugin.Configuration.FavoriteVenueIds.Contains(v.VenueId);
        var isHiddenVenue = plugin.Configuration.HiddenVenueIds.Contains(v.VenueId);
        var hasAddr = !string.IsNullOrEmpty(v.Address);
        var tpActive = tpAnimStart.TryGetValue(v.VenueId, out var tpT) && time - tpT < 4.0;

        var rowH = RowPadY * 2 + lineH * 2 + RowLineGap + (nextEvent != null ? lineH + RowLineGap : 0f);
        var tpSize = TeleportButtonSize(isOpen && hasAddr, lineH);
        var rowMin = ImGui.GetCursorScreenPos();
        var rowMax = rowMin + new Vector2(avW, rowH);
        var hitW = Math.Max(avW - tpSize.X - 10f, 60f);

        ImGui.InvisibleButton($"##venue_{v.VenueId}", new Vector2(hitW, rowH));
        ImGui.SetItemAllowOverlap();
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();
        var rowHot = hovered || (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(rowMin, rowMax));
        var (accent, secondary, gem) = VenuePalette(v);

        if (!first)
            dl.AddLine(rowMin, new Vector2(rowMax.X, rowMin.Y), U32(UIConstants.Line));
        if (rowHot || isHere)
        {
            var strength = rowHot ? 0.12f : 0.07f;
            var left = U32(UIConstants.Mix(UIConstants.Background, accent, strength));
            var right = U32(UIConstants.Mix(UIConstants.Background, secondary, strength * 0.5f));
            dl.AddRectFilledMultiColor(rowMin, rowMax, left, right, right, left);
        }
        var barMin = new Vector2(rowMin.X, rowMin.Y + 7f);
        var barMax = new Vector2(rowMin.X + 3f, rowMax.Y - 7f);
        if (rowHot)
            dl.AddRectFilled(barMin - new Vector2(0f, 2f), barMax + new Vector2(3f, 2f), U32(UIConstants.WithAlpha(accent, 0.25f)), 2f);
        var barAlpha = rowHot || isHere ? 1f : 0.85f;
        var barTop = U32(UIConstants.WithAlpha(accent, barAlpha));
        var barBottom = U32(UIConstants.WithAlpha(secondary, barAlpha));
        dl.AddRectFilledMultiColor(barMin, barMax, barTop, barTop, barBottom, barBottom);
        if (UIConstants.IsDark(accent) || UIConstants.IsDark(secondary))
            dl.AddRect(barMin - new Vector2(1f), barMax + new Vector2(1f), U32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.45f)), 2f, ImDrawFlags.None, 1f);

        var textRight = rowMin.X + hitW - 4f;
        if (isFav)
            textRight = DrawFavStar(dl, v.VenueId, textRight, rowMin.Y + rowH / 2f, lineH) - 8f;
        var adSeen = plugin.PartyFinderAds.SeenAt(v.VenueId);
        if (adSeen != null)
        {
            var iconMax = new Vector2(textRight, rowMin.Y + rowH / 2f + lineH / 2f);
            textRight = DrawPartyFinderIcon(dl, textRight, rowMin.Y + rowH / 2f, lineH);
            var iconMin = new Vector2(textRight, rowMin.Y + rowH / 2f - lineH / 2f);
            textRight -= 8f;
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(iconMin, iconMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip(Lang.PartyFinderOpenTip);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    clicked = false;
                    OpenPartyFinder(v);
                }
            }
        }
        if (rowHot && hasAddr)
            textRight = DrawRowActions(v, textRight, rowMin.Y + rowH / 2f) - 8f;
        else if (statusText.Length > 0)
            textRight = DrawRowStatus(dl, statusText, isOpen, textRight, rowMin.Y + rowH / 2f, lineH) - 10f;

        DrawRowText(dl, v, rowMin, rowMax, textRight, lineH, rowHot, accent, gem, isHere, nextEvent);

        ImGui.SetCursorScreenPos(new Vector2(rowMax.X - tpSize.X, rowMin.Y + (rowH - tpSize.Y) / 2f));
        DrawTeleportButton(v, dl, tpSize, isOpen && hasAddr, hasAddr, tpActive);

        if (clicked)
        {
            if (isHere) selectMapTab = true;
            else plugin.VenueDetailWindow.Open(v);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##ctx_{v.VenueId}");
        DrawVenueContextMenu(v, isFav, isHiddenVenue);
        VenueShare.Popup(plugin, v, $"##share_{v.VenueId}");

        if (hovered)
        {
            using var tooltip = ImRaii.Tooltip();
            var (banner, _) = VenueBanner.Get(plugin, v);
            if (banner != null)
            {
                var size = new Vector2(280f, 84f);
                var (uv0, uv1) = VenueBanner.CoverUv(banner, size);
                ImGui.Image(banner.Handle, size, uv0, uv1);
            }
            UIConstants.TextReadable(VenueAccent(v), UIConstants.DisplayName(v.Name));
            if (hasAddr)
                ImGui.TextColored(UIConstants.TextSecondary, v.Address);
            if (adSeen is { } seen)
                ImGui.TextColored(UIConstants.Glow, Lang.PartyFinderSeen(MinutesAgo(seen)));
            ImGui.TextColored(UIConstants.TextMuted, Lang.ViewVenueDetails);
            ImGui.TextColored(UIConstants.TextMuted, Lang.RightClickHint);
        }

        DrawFavAnimation(dl, v.VenueId, rowMin, rowMax);
        if (isHiddenVenue)
            dl.AddRectFilled(rowMin, rowMax, U32(UIConstants.WithAlpha(UIConstants.Background, 0.55f)));
        DrawCopyAnimation(dl, v.VenueId, rowMin, rowMax);
        DrawTeleportAnimation(dl, v.VenueId, rowMin, rowMax);

        ImGui.SetCursorScreenPos(new Vector2(rowMin.X, rowMax.Y));
    }

    private float DrawRowActions(Venue v, float right, float centerY)
    {
        var size = ImGui.GetFrameHeight();
        const float gap = 2f;
        var actions = new List<(string Id, FontAwesomeIcon Icon, string Tip, Action Click)>
        {
            ("share", FontAwesomeIcon.ShareAlt, Lang.Share, () => ImGui.OpenPopup($"##share_{v.VenueId}")),
        };
        var links = v.Links;
        if (!string.IsNullOrEmpty(links?.Partake)) actions.Add(("partake", FontAwesomeIcon.CalendarAlt, "Partake", () => UIConstants.OpenUrl(links!.Partake)));
        if (!string.IsNullOrEmpty(links?.Website)) actions.Add(("web", FontAwesomeIcon.Globe, Lang.Website, () => UIConstants.OpenUrl(links!.Website)));
        if (!string.IsNullOrEmpty(links?.Discord)) actions.Add(("discord", FontAwesomeIcon.Comments, "Discord", () => UIConstants.OpenUrl(links!.Discord)));

        var restore = ImGui.GetCursorScreenPos();
        var x = right;
        foreach (var (id, icon, tip, click) in actions)
        {
            x -= size;
            if (VenueShare.IconButton($"##{id}_{v.VenueId}", icon, tip, new Vector2(x, centerY - size / 2f), size))
                click();
            x -= gap;
        }
        ImGui.SetCursorScreenPos(restore);
        return x;
    }

    private static float DrawRowStatus(ImDrawListPtr dl, string statusText, bool isOpen, float right, float centerY, float lineH)
    {
        var size = ImGui.CalcTextSize(statusText);
        var x = right - size.X;
        var y = centerY - lineH / 2f;
        dl.AddText(new Vector2(x, y), U32(isOpen ? UIConstants.Success : UIConstants.TextSecondary), statusText);
        if (!isOpen) return x;

        var pulse = (MathF.Sin((float)ImGui.GetTime() * 3f) + 1f) / 2f;
        dl.AddCircleFilled(new Vector2(x - 8f, centerY), 3f, U32(UIConstants.WithAlpha(UIConstants.Success, 0.35f + 0.65f * pulse)));
        return x - 13f;
    }

    private static void DrawRowText(ImDrawListPtr dl, Venue v, Vector2 rowMin, Vector2 rowMax, float textRight, float lineH,
        bool rowHot, Vector4 accent, Vector4 gem, bool isHere, VenueEvent? nextEvent)
    {
        var x = rowMin.X + RowPadX;
        var y = rowMin.Y + RowPadY;
        var nameX = x + 14f;

        dl.PushClipRect(new Vector2(x - 4f, rowMin.Y), new Vector2(textRight, rowMax.Y), true);

        var gemCenter = new Vector2(x + 3.5f, y + lineH / 2f);
        UIConstants.DrawGem(dl, gemCenter, 3.5f, gem);
        if (UIConstants.IsDark(gem))
            dl.AddQuad(gemCenter + new Vector2(0, -3.5f), gemCenter + new Vector2(3.5f, 0), gemCenter + new Vector2(0, 3.5f), gemCenter + new Vector2(-3.5f, 0),
                U32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.6f)), 1f);
        var name = UIConstants.DisplayName(v.Name);
        UIConstants.AddTextReadable(dl, new Vector2(nameX, y), rowHot ? UIConstants.Lighten(accent, 0.12f) : accent, name);

        var cx = nameX + ImGui.CalcTextSize(name).X + 10f;
        if (isHere)
            cx = DrawPill(dl, cx, y, Lang.Here2.Trim(), UIConstants.Success) + 6f;
        if (v.Nsfw)
            DrawPill(dl, cx, y, Lang.NsfwBadge, UIConstants.Rose);

        var addrY = y + lineH + RowLineGap;
        dl.AddText(new Vector2(nameX, addrY), U32(UIConstants.TextMuted), FormatAddress(v.Address));

        if (nextEvent != null)
        {
            var evY = addrY + lineH + RowLineGap;
            var iconFont = UiBuilder.IconFont;
            var iconSize = lineH * 0.8f;
            dl.AddText(iconFont, iconSize, new Vector2(nameX, evY + (lineH - iconSize) / 2f),
                U32(UIConstants.Accent), FontAwesomeIcon.CalendarAlt.ToIconString());
            var when = $"{nextEvent.StartTime.ToString("ddd", Lang.Culture)} {UIConstants.FormatServerTime(nextEvent.StartTime)}";
            dl.AddText(new Vector2(nameX + iconSize + 6f, evY), U32(UIConstants.TextSecondary), $"{UIConstants.DisplayName(nextEvent.Title)} · {when}");
        }

        dl.PopClipRect();
    }

    private static float DrawPill(ImDrawListPtr dl, float x, float y, string text, Vector4 color)
    {
        const float scale = 0.8f;
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * scale;
        var size = ImGui.CalcTextSize(text) * scale;
        var lineH = ImGui.GetTextLineHeight();
        var pad = new Vector2(5f, 1f);
        var min = new Vector2(x, y + (lineH - size.Y - pad.Y * 2) / 2f);
        var max = min + size + pad * 2;
        dl.AddRectFilled(min, max, U32(UIConstants.WithAlpha(color, 0.14f)), 2f);
        dl.AddText(font, fontSize, min + pad, U32(color), text);
        return max.X;
    }

    private void OpenPartyFinder(Venue v)
    {
        if (!plugin.PartyFinderAds.OpenListing(v.VenueId))
            plugin.Toasts.Show(Lang.PartyFinderGone, ToastKind.Warning, 3.0);
    }

    internal static int MinutesAgo(DateTime utc) => Math.Max(1, (int)Math.Round((DateTime.UtcNow - utc).TotalMinutes));

    private static float DrawPartyFinderIcon(ImDrawListPtr dl, float right, float centerY, float lineH)
    {
        var size = lineH * 0.95f;
        UIConstants.DrawIconCentered(dl, size, new Vector2(right - size / 2f, centerY), FontAwesomeIcon.Bullhorn, U32(UIConstants.Glow));
        return right - size;
    }

    private float DrawFavStar(ImDrawListPtr dl, string venueId, float right, float centerY, float lineH)
    {
        var iconFont = UiBuilder.IconFont;
        var glyph = FontAwesomeIcon.Star.ToIconString();
        var size = lineH * 1.05f;
        var glyphSz = CalcIconSize(glyph) * (size / iconFont.FontSize);
        var pos = new Vector2(right - glyphSz.X, centerY - glyphSz.Y / 2f);
        var animating = favAnimStart.TryGetValue(venueId, out var t) && ImGui.GetTime() - t < 1.6;
        if (!animating)
            dl.AddText(iconFont, size, pos, U32(UIConstants.GoldAccent), glyph);
        return pos.X;
    }

    private void DrawFavAnimation(ImDrawListPtr dl, string venueId, Vector2 rowMin, Vector2 rowMax)
    {
        if (!favAnimStart.TryGetValue(venueId, out var animT)) return;
        var elapsed = (float)(ImGui.GetTime() - animT);
        const float dur = 3.0f;
        if (elapsed >= dur)
        {
            favAnimStart.Remove(venueId);
            return;
        }

        var progress = elapsed / dur;
        var gold = UIConstants.GoldAccent;
        var rowW = rowMax.X - rowMin.X;
        dl.PushClipRect(rowMin, rowMax, true);

        if (progress < 0.45f)
        {
            var fillW = rowW * (progress / 0.45f);
            dl.AddRectFilled(rowMin, new Vector2(rowMin.X + fillW, rowMax.Y), U32(UIConstants.WithAlpha(gold, 0.12f)));
            dl.AddRectFilled(new Vector2(rowMin.X + fillW - 2f, rowMin.Y), new Vector2(rowMin.X + fillW, rowMax.Y),
                U32(UIConstants.WithAlpha(gold, 0.8f)));
        }
        else if (progress < 0.6f)
        {
            var flashP = (progress - 0.45f) / 0.15f;
            dl.AddRectFilled(rowMin, rowMax, U32(UIConstants.WithAlpha(gold, 0.18f * (1f - flashP))));
        }

        dl.PopClipRect();
    }

    private void DrawCopyAnimation(ImDrawListPtr dl, string venueId, Vector2 rowMin, Vector2 rowMax)
    {
        if (!copyAnimStart.TryGetValue(venueId, out var copyT)) return;
        var copyElapsed = (float)(ImGui.GetTime() - copyT);
        const float copyDur = 2.5f;
        if (copyElapsed >= copyDur)
        {
            copyAnimStart.Remove(venueId);
            return;
        }

        var copyP = copyElapsed / copyDur;
        var copiedAlpha = copyP < 0.1f ? copyP / 0.1f : Math.Clamp(1f - (copyP - 0.7f) / 0.3f, 0f, 1f);
        dl.AddRectFilled(rowMin, rowMax, U32(UIConstants.WithAlpha(UIConstants.Background, 0.75f * copiedAlpha)));

        var copiedText = Lang.Copied;
        var copiedSz = ImGui.CalcTextSize(copiedText);
        var startX = rowMin.X + (rowMax.X - rowMin.X - copiedSz.X) * 0.5f;
        var startY = rowMin.Y + (rowMax.Y - rowMin.Y - copiedSz.Y) * 0.5f - copyP * 6f;
        var rainT = (float)ImGui.GetTime() * 0.5f;
        for (var ci = 0; ci < copiedText.Length; ci++)
        {
            var charCol = HsvToRgba((rainT + ci * 0.1f) % 1.0f, 0.85f, 1.0f);
            charCol.W = copiedAlpha;
            var charX = startX + ImGui.CalcTextSize(copiedText[..ci]).X;
            dl.AddText(new Vector2(charX, startY), U32(charCol), copiedText[ci].ToString());
        }
    }

    private void DrawTeleportAnimation(ImDrawListPtr dl, string venueId, Vector2 rowMin, Vector2 rowMax)
    {
        if (!tpAnimStart.TryGetValue(venueId, out var tpAnimT)) return;
        var tpElapsed = (float)(ImGui.GetTime() - tpAnimT);
        const float tpDur = 4.0f;
        if (tpElapsed >= tpDur)
        {
            tpAnimStart.Remove(venueId);
            return;
        }

        var tpP = tpElapsed / tpDur;
        var tpAlpha = tpP < 0.1f ? tpP / 0.1f : Math.Clamp(1f - (tpP - 0.6f) / 0.4f, 0f, 1f);
        dl.AddRectFilled(rowMin, rowMax, U32(UIConstants.WithAlpha(UIConstants.Background, 0.6f * tpAlpha)));

        var dots = (int)(tpElapsed * 3f) % 4;
        var tpText = Lang.Teleporting.TrimEnd('.') + new string('.', dots);
        var tpSz = ImGui.CalcTextSize(tpText);
        var lineH = ImGui.GetTextLineHeight();
        var totalW = lineH + 8f + tpSz.X;
        var x = rowMin.X + (rowMax.X - rowMin.X - totalW) * 0.5f;
        var y = rowMin.Y + (rowMax.Y - rowMin.Y - tpSz.Y) * 0.5f;
        var col = U32(UIConstants.WithAlpha(UIConstants.Glow, tpAlpha));
        HudIcons.Aetheryte(dl, new Vector2(x + lineH / 2f, y + lineH / 2f), lineH, col);
        dl.AddText(new Vector2(x + lineH + 8f, y), col, tpText);
    }

    private static (bool Pressed, bool Hovered) TeleportGlyphButton(string id, ImDrawListPtr dl, Vector2 size,
        bool prominent, bool enabled, bool running)
    {
        var pos = ImGui.GetCursorScreenPos();
        bool pressed, hovered;
        using (ImRaii.Disabled(!enabled))
        {
            pressed = ImGui.InvisibleButton(id, size);
            hovered = enabled && ImGui.IsItemHovered();
        }

        var max = pos + size;
        var aether = UIConstants.Glow;
        if (enabled && (prominent || hovered))
        {
            UIConstants.Chamfer(dl, pos, max, 6f);
            dl.PathFillConvex(U32(UIConstants.WithAlpha(aether, hovered ? 0.2f : 0.08f)));
            UIConstants.Chamfer(dl, pos, max, 6f);
            dl.PathStroke(U32(UIConstants.WithAlpha(aether, hovered ? 1f : 0.8f)), ImDrawFlags.Closed, 1f);
        }

        var lineH = ImGui.GetTextLineHeight();
        var col = !enabled ? UIConstants.WithAlpha(UIConstants.TextMuted, 0.35f)
                : prominent || hovered || running ? aether : UIConstants.TextMuted;
        if (running)
            col = UIConstants.WithAlpha(col, 0.55f + 0.45f * (MathF.Sin((float)ImGui.GetTime() * 5f) + 1f) / 2f);
        var iconCenter = prominent
            ? new Vector2(pos.X + 8f + lineH / 2f, pos.Y + size.Y / 2f)
            : pos + size / 2f;
        HudIcons.Aetheryte(dl, iconCenter, lineH, U32(col));
        if (prominent)
            dl.AddText(new Vector2(pos.X + 8f + lineH + 5f, pos.Y + (size.Y - lineH) / 2f), U32(col), running ? "..." : Lang.Visit);

        return (pressed && enabled, hovered);
    }

    private void DrawTeleportButton(Venue v, ImDrawListPtr dl, Vector2 size, bool prominent, bool enabled, bool tpActive)
    {
        var running = IsTeleportRunning(v, tpActive);
        var (pressed, hovered) = TeleportGlyphButton($"##tp_{v.VenueId}", dl, size, prominent, enabled, running);

        if (pressed)
        {
            if (running) CancelTeleport(v);
            else TeleportTo(v);
        }

        if (!hovered) return;
        using var tooltip = ImRaii.Tooltip();
        if (running)
        {
            ImGui.TextColored(UIConstants.Glow, Lang.TeleportCancelTip);
            return;
        }
        ImGui.TextColored(UIConstants.Glow, Lang.TeleportVia);
        ImGui.TextColored(UIConstants.TextSecondary, v.Address);
    }

    private string? activeTeleportVenueId;

    private bool IsTeleportRunning(Venue v, bool tpActive)
    {
        if (activeTeleportVenueId != v.VenueId) return false;
        if (tpActive || plugin.Lifestream.IsBusyCached) return true;
        activeTeleportVenueId = null;
        return false;
    }

    private void CancelTeleport(Venue v)
    {
        plugin.CancelTeleport();
        tpAnimStart.Remove(v.VenueId);
        activeTeleportVenueId = null;
    }

    private void TeleportTo(Venue v)
    {
        if (!plugin.Lifestream.IsLoaded)
        {
            plugin.Toasts.Show(Lang.ToastLifestreamMissing, ToastKind.Warning, 3.5);
            return;
        }

        var teleportOk = plugin.Lifestream.NavigateTo(v.Address);
        plugin.Toasts.Show(
            teleportOk ? Lang.ToastTeleportingTo(v.Name) : Lang.ToastTeleportFailed,
            teleportOk ? ToastKind.Success : ToastKind.Warning, 2.5);
        tpAnimStart[v.VenueId] = ImGui.GetTime();
        if (teleportOk) activeTeleportVenueId = v.VenueId;
    }

    private void DrawVenueContextMenu(Venue v, bool isFav, bool isHiddenVenue)
    {
        using var menuStyle = UIConstants.MenuStyle();
        using var popup = ImRaii.Popup($"##ctx_{v.VenueId}");
        if (!popup) return;

        if (ImGui.MenuItem(Lang.ViewVenueDetails))
        {
            plugin.VenueDetailWindow.Open(v);
            ImGui.CloseCurrentPopup();
        }
        if (ImGui.MenuItem(Lang.ShowOnMap))
        {
            plugin.ShowVenueOnMap(v);
            ImGui.CloseCurrentPopup();
        }
        if (ImGui.MenuItem(isFav ? Lang.RemoveFavorite : Lang.AddFavorite))
        {
            if (isFav)
            {
                plugin.Configuration.FavoriteVenueIds.Remove(v.VenueId);
                favAnimStart.Remove(v.VenueId);
                plugin.Toasts.Show(Lang.ToastFavoriteRemoved(v.Name), ToastKind.Info, 2.0);
            }
            else
            {
                plugin.Configuration.FavoriteVenueIds.Add(v.VenueId);
                favAnimStart[v.VenueId] = ImGui.GetTime();
                plugin.Toasts.Show(Lang.ToastFavoriteAdded(v.Name), ToastKind.Success, 2.0);
            }
            plugin.Configuration.Save();
        }
        if (!string.IsNullOrEmpty(v.Address))
        {
            using var share = ImRaii.Menu(Lang.Share);
            if (share)
                VenueShare.DrawMenu(plugin, v);
        }
        if (!string.IsNullOrEmpty(v.Address) && ImGui.MenuItem(Lang.CopyAddress))
        {
            ImGui.SetClipboardText($"{v.Name} // {v.Address}");
            copyAnimStart[v.VenueId] = ImGui.GetTime();
            plugin.Toasts.Show(Lang.ToastAddressCopied, ToastKind.Success, 2.0);
        }
        if (ImGui.MenuItem(isHiddenVenue ? Lang.UnhideVenue : Lang.HideVenue))
        {
            if (isHiddenVenue)
            {
                plugin.Configuration.HiddenVenueIds.Remove(v.VenueId);
                plugin.Toasts.Show(Lang.ToastVenueUnhidden(v.Name), ToastKind.Info, 2.0);
            }
            else
            {
                plugin.Configuration.HiddenVenueIds.Add(v.VenueId);
                plugin.Toasts.Show(Lang.ToastVenueHidden(v.Name), ToastKind.Info, 2.0);
            }
            plugin.Configuration.Save();
        }
    }

    private void DrawHeaderBar(Venue? venue, string title, string floorStr)
    {
        var dl   = ImGui.GetWindowDrawList();
        var winP = ImGui.GetWindowPos();
        var winW = ImGui.GetWindowWidth();
        var cy   = ImGui.GetCursorScreenPos().Y;
        var tint = venue != null ? VenueAccent(venue) : UIConstants.Heading;
        var tint2 = venue != null ? VenuePalette(venue).Secondary : UIConstants.Heading;

        dl.AddRectFilledMultiColor(
            new Vector2(winP.X, cy - 2),
            new Vector2(winP.X + winW, cy + 30),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(tint, 0.16f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(tint2, 0.10f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(tint2, 0f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(tint, 0f)));

        using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(4, 4)))
        using (var table = ImRaii.Table("##mapHeader", 3))
        {
            if (table)
                DrawHeaderColumns(venue, dl, title, floorStr);
        }

        var lineY = ImGui.GetCursorScreenPos().Y;
        dl.AddLine(
            new Vector2(winP.X + 8, lineY),
            new Vector2(winP.X + winW - 8, lineY),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Trim, 0.8f)), 1f);
    }

    private void DrawHeaderColumns(Venue? venue, ImDrawListPtr dl, string title, string floorStr)
    {
        ImGui.TableSetupColumn("##hName",  ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##hFloor", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##h3D",    ImGuiTableColumnFlags.WidthFixed, 50);

        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        var iconFont = UiBuilder.IconFont;
        var infoGlyph = FontAwesomeIcon.InfoCircle.ToIconString();
        var infoIconSz = CalcIconSize(infoGlyph);
        const float infoGap = 5f;

        var nameW = ImGui.CalcTextSize(title).X;
        var lineH = ImGui.GetTextLineHeight();
        var totalW = infoIconSz.X + infoGap + nameW;
        var colW  = ImGui.GetColumnWidth();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colW - totalW) / 2f));

        var namePos = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##venueInfoBtn", new Vector2(totalW, lineH));
        var nameHovered = venue != null && ImGui.IsItemHovered();
        if (venue != null && ImGui.IsItemClicked()) plugin.VenueDetailWindow.Open(venue);
        if (venue != null)
            UIConstants.DrawHoverPulseOverlay("venueInfoBtn", nameHovered, false, UIConstants.Glow);

        var titleCol = venue != null ? VenueAccent(venue) : UIConstants.Heading;
        var infoCol = nameHovered ? UIConstants.Accent : UIConstants.WithAlpha(titleCol, 0.6f);
        var iconY = namePos.Y + (lineH - infoIconSz.Y) * 0.5f;
        dl.AddText(iconFont, iconFont.FontSize, new Vector2(namePos.X, iconY), ImGui.ColorConvertFloat4ToU32(infoCol),
            venue != null ? infoGlyph : FontAwesomeIcon.Flag.ToIconString());
        UIConstants.AddTextReadable(dl, new Vector2(namePos.X + infoIconSz.X + infoGap, namePos.Y),
            nameHovered ? UIConstants.Lighten(titleCol, 0.12f) : titleCol, title);
        if (nameHovered) ImGui.SetTooltip(Lang.ViewVenueDetails);

        ImGui.TableSetColumnIndex(1);
        var floorW = ImGui.CalcTextSize(floorStr).X;
        colW = ImGui.GetColumnWidth();
        var rules = venue != null ? HouseRulesCard.RulesFor(venue, plugin.Configuration.Language) : Array.Empty<VenueRule>();
        var ruleGlyph = FontAwesomeIcon.BookOpen.ToIconString();
        var ruleGlyphSize = CalcIconSize(ruleGlyph) * 0.75f;
        var ruleLabel = $"{Lang.HouseRulesPill} {rules.Count}";
        var pillW = rules.Count > 0 ? ImGui.CalcTextSize(ruleLabel).X + ruleGlyphSize.X + 18f : 0f;
        var floorTotal = floorW + (pillW > 0f ? pillW + 8f : 0f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colW - floorTotal) / 2f));
        ImGui.TextColored(UIConstants.TextSecondary, floorStr);
        if (rules.Count > 0 && venue != null)
        {
            ImGui.SameLine(0, 8f);
            var pillPos = ImGui.GetCursorScreenPos();
            var pillSize = new Vector2(pillW, lineH);
            if (ImGui.InvisibleButton("##rulesPill", pillSize))
                plugin.HouseRules.Show(venue);
            var pillHot = ImGui.IsItemHovered();
            var amber = UIConstants.Warning;
            dl.AddRectFilled(pillPos, pillPos + pillSize, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(amber, pillHot ? 0.22f : 0.1f)), lineH / 2f);
            dl.AddRect(pillPos, pillPos + pillSize, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(amber, 0.55f)), lineH / 2f, ImDrawFlags.None, 1f);
            dl.AddText(iconFont, iconFont.FontSize * 0.75f, new Vector2(pillPos.X + 7f, pillPos.Y + (lineH - ruleGlyphSize.Y) / 2f),
                ImGui.ColorConvertFloat4ToU32(amber), ruleGlyph);
            dl.AddText(new Vector2(pillPos.X + 11f + ruleGlyphSize.X, pillPos.Y), ImGui.ColorConvertFloat4ToU32(amber), ruleLabel);
            if (pillHot) ImGui.SetTooltip(Lang.HouseRulesShow);
        }

        ImGui.TableSetColumnIndex(2);
        using var checkMark = ImRaii.PushColor(ImGuiCol.CheckMark, UIConstants.Glow);
        var markers3d = plugin.PictomancyMarkers.Enabled;
        if (ImGui.Checkbox("3D##markers", ref markers3d))
        {
            plugin.PictomancyMarkers.Enabled = markers3d;
            plugin.Configuration.Markers3DEnabled = markers3d;
            plugin.Configuration.Save();
        }
    }

    private void DrawFloorTabs(IReadOnlyList<Floor> floors, ref Floor? selectedFloor)
    {
        if (floors.Count == 0) return;
        const float gap = 6f;
        var totalGap = gap * (floors.Count - 1);
        var tabW = (ImGui.GetContentRegionAvail().X - totalGap) / floors.Count;

        for (var i = 0; i < floors.Count; i++)
        {
            var f      = floors[i];
            var active = f == selectedFloor;
            if (i > 0) ImGui.SameLine(0, gap);

            using (ImRaii.PushColor(ImGuiCol.Text,          active ? UIConstants.TextPrimary : UIConstants.TextSecondary)
                         .Push(ImGuiCol.Button,        active ? UIConstants.WithAlpha(UIConstants.Accent, 0.25f) : UIConstants.WithAlpha(UIConstants.CardBackground, 0.9f))
                         .Push(ImGuiCol.ButtonHovered, active ? UIConstants.WithAlpha(UIConstants.Accent, 0.4f)  : UIConstants.WithAlpha(UIConstants.Glow, 0.12f))
                         .Push(ImGuiCol.ButtonActive,  UIConstants.Accent)
                         .Push(ImGuiCol.Border,        active ? UIConstants.GlowBright : UIConstants.GlowDim))
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, active ? 2f : 1f).Push(ImGuiStyleVar.FrameRounding, 0f))
            {
                if (ImGui.Button(TranslateFloorName(f.Name), new Vector2(tabW, 28)))
                    selectedFloor = f;
            }

            if (active)
            {
                var dl   = ImGui.GetWindowDrawList();
                var imin = ImGui.GetItemRectMin();
                var imax = ImGui.GetItemRectMax();
                dl.AddLine(new Vector2(imin.X, imax.Y + 2), new Vector2(imax.X, imax.Y + 2),
                    ImGui.ColorConvertFloat4ToU32(UIConstants.Accent), 3f);
                dl.AddLine(new Vector2(imin.X, imax.Y + 6), new Vector2(imax.X, imax.Y + 6),
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.3f)), 2f);
            }
        }
    }

    private Vector2? lastMapFocus;

    private void DrawMapPanel(IReadOnlyList<Service> services, uint territoryId, uint mapId = 0,
        Vector2? focus = null, float focusSpan = 0f, float areaRadius = 0f,
        IReadOnlyList<MapPin>? pins = null, Venue? highlight = null, bool fullMap = false)
    {
        var tracker = plugin.PositionTracker;
        var zoomMin = fullMap ? ZoomDefault : ZoomMin;
        var zoomMax = fullMap ? ZoomDefault * WardZoomMax : ZoomMax;
        var outdoorDistrict = tracker.IsInsideHouse ? null : tracker.CurrentHousingDistrict;

        var filters = plugin.Configuration.ServiceFilters;
        var visible = services
            .Where(s => !filters.TryGetValue(s.Type, out var v) || v)
            .ToList();

        var avW   = ImGui.GetContentRegionAvail().X;
        var avH   = ImGui.GetContentRegionAvail().Y - 52f;
        var mapSz = Math.Max(Math.Min(avW, avH), 160f);

        var mapInfo = mapId > 0 ? plugin.MapLoader.GetMapInfoByMapId(mapId) : null;
        mapInfo ??= plugin.MapLoader.GetMapInfo(territoryId, outdoorDistrict);
        var sizeFactor = mapInfo?.SizeFactor > 0 ? mapInfo.SizeFactor : (ushort)200;
        var offsetX    = mapInfo?.OffsetX ?? (short)0;
        var offsetY    = mapInfo?.OffsetY ?? (short)0;

        void ResetView()
        {
            mapPanX = 0f;
            mapPanY = 0f;
            if (!fullMap)
            {
                mapZoom = ZoomDefault;
                return;
            }
            mapZoom = ZoomDefault * Math.Clamp(plugin.Configuration.WardMapZoom, 1f, WardZoomMax);
        }

        if (mapId != lastMapId || focus != lastMapFocus)
        {
            lastMapId = mapId;
            lastMapFocus = focus;
            if (fullMap) ResetView();
            else
            {
                mapPanX = 0f;
                mapPanY = 0f;
            }
        }
        mapZoom = Math.Clamp(mapZoom, zoomMin, zoomMax);

        var padX = (avW - mapSz) / 2f;
        if (padX > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);

        var mapMin = ImGui.GetCursorScreenPos();
        var mapMax = new Vector2(mapMin.X + mapSz, mapMin.Y + mapSz);

        ImGui.InvisibleButton("##mapArea", new Vector2(mapSz, mapSz));
        var hovering = ImGui.IsItemHovered();

        var focused = focus.HasValue && focusSpan > 0f;
        var zoomFactor = focused
            ? 2048f / (focusSpan * sizeFactor / 100f) * (mapZoom / ZoomDefault)
            : mapZoom;

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var delta = ImGui.GetIO().MouseDelta;
            mapPanX -= delta.X / mapSz / zoomFactor;
            mapPanY -= delta.Y / mapSz / zoomFactor;
        }

        if (hovering)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (MathF.Abs(wheel) > 0.01f)
                mapZoom = Math.Clamp(mapZoom * (1f + wheel * 0.12f), zoomMin, zoomMax);

            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                ResetView();
        }

        zoomFactor = focused
            ? 2048f / (focusSpan * sizeFactor / 100f) * (mapZoom / ZoomDefault)
            : mapZoom;
        var focusU = focused ? HousingMapLoader.WorldToUV(focus!.Value.X, offsetX, sizeFactor) : 0.5f;
        var focusV = focused ? HousingMapLoader.WorldToUV(focus!.Value.Y, offsetY, sizeFactor) : 0.5f;
        var halfUV = 0.5f / zoomFactor;
        var viewCX = focusU + mapPanX;
        var viewCY = focusV + mapPanY;
        var uv0 = new Vector2(viewCX - halfUV, viewCY - halfUV);
        var uv1 = new Vector2(viewCX + halfUV, viewCY + halfUV);

        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(mapMin, mapMax, true);

        bool hasTexture = false;
        var mapTex = plugin.MapLoader.GetMapTexture(territoryId, mapId, outdoorDistrict);
        if (mapTex != null && mapTex.TryGetWrap(out IDalamudTextureWrap? wrap, out _) && wrap != null)
        {
            dl.AddImage(wrap.Handle, mapMin, mapMax, uv0, uv1);
            hasTexture = true;
        }

        if (!hasTexture)
            DrawSchematic(dl, mapMin, mapMax, mapSz);

        if (focused && areaRadius > 0f)
        {
            var center = UvToScreen(focusU, focusV, uv0, uv1, mapMin, mapSz);
            var radiusPx = areaRadius * (sizeFactor / 100f) / 2048f / (uv1.X - uv0.X) * mapSz;
            dl.AddCircleFilled(center, radiusPx, U32(UIConstants.WithAlpha(UIConstants.Accent, 0.08f)), 64);
            dl.AddCircle(center, radiusPx, U32(UIConstants.WithAlpha(UIConstants.Accent, 0.7f)), 64, 1.5f);
        }

        Service? hoveredSvc = null;
        Vector2 hoveredPos = default;
        Vector2? guideScreen = null;
        Vector2? guideFrom = null;
        var guideOnFloor = false;
        var waypoint = plugin.Guide.Current is { Final: false } wp ? wp.Goal : (Vector3?)null;
        foreach (var svc in visible)
        {
            var svcU = HousingMapLoader.WorldToUV(svc.X, offsetX, sizeFactor);
            var svcV = HousingMapLoader.WorldToUV(svc.Y, offsetY, sizeFactor);
            var screenPos = UvToScreen(svcU, svcV, uv0, uv1, mapMin, mapSz);
            if (plugin.Guide.IsTarget(svc)) { guideScreen = screenPos; guideOnFloor = true; }
            else if (!guideOnFloor && waypoint is { } goal && Vector3.Distance(IndoorGuide.World(svc), goal) < 0.05f) guideScreen = screenPos;
            var isHovered = DrawMarkerBase(dl, screenPos, svc, mapSz);
            if (isHovered) { hoveredSvc = svc; hoveredPos = screenPos; }
        }

        if (guideOnFloor && waypoint is { } arrival)
        {
            var stairs = services
                .Where(s => s.Type is "upstairs" or "downstairs")
                .OrderBy(s => new Vector2(s.X - arrival.X, s.Y - arrival.Z).LengthSquared())
                .FirstOrDefault();
            if (stairs != null)
                guideFrom = UvToScreen(HousingMapLoader.WorldToUV(stairs.X, offsetX, sizeFactor),
                    HousingMapLoader.WorldToUV(stairs.Y, offsetY, sizeFactor), uv0, uv1, mapMin, mapSz);
        }

        if (hoveredSvc != null && hovering && ImGui.IsMouseReleased(ImGuiMouseButton.Left)
            && ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f).Length() < 4f)
            ToggleGuide(hoveredSvc);

        if (pins != null)
            DrawMapPins(dl, pins, highlight, uv0, uv1, mapMin, mapSz, offsetX, offsetY, sizeFactor, hovering);

        if (tracker.CurrentTerritoryId == territoryId)
        {
            var pU = HousingMapLoader.WorldToUV(tracker.PlayerX, offsetX, sizeFactor);
            var pV = HousingMapLoader.WorldToUV(tracker.PlayerZ, offsetY, sizeFactor);
            var playerScreen = UvToScreen(pU, pV, uv0, uv1, mapMin, mapSz);

            var time = (float)ImGui.GetTime();
            if (guideScreen is { } target)
            {
                var line = new List<Vector2>();
                if (plugin.Guide.Current is not { Final: true } && !guideOnFloor)
                {
                    line.Add(playerScreen);
                    line.Add(target);
                }
                else if (plugin.Guide.Current is not { Final: true } && guideFrom is { } from)
                {
                    line.Add(from);
                    line.Add(target);
                }
                else if (plugin.Guide.Current is { Final: true } route)
                {
                    line.Add(playerScreen);
                    for (var i = 0; i < route.Points.Count; i++)
                        line.Add(route.Complete && i == route.Points.Count - 1
                            ? target
                            : UvToScreen(HousingMapLoader.WorldToUV(route.Points[i].X, offsetX, sizeFactor),
                                HousingMapLoader.WorldToUV(route.Points[i].Z, offsetY, sizeFactor), uv0, uv1, mapMin, mapSz));
                }
                for (var i = 1; i < line.Count; i++)
                {
                    dl.AddLine(line[i - 1], line[i], U32(UIConstants.WithAlpha(UIConstants.Accent, 0.22f)), 6f);
                    dl.AddLine(line[i - 1], line[i], U32(UIConstants.WithAlpha(UIConstants.Accent, 0.9f)), 2f);
                }
                var ring = MarkerRadius(mapSz) + 4f + (time * 0.8f % 1f) * 6f;
                dl.AddCircle(target, ring, U32(UIConstants.WithAlpha(UIConstants.Accent, 0.7f)), 32, 1.5f);
            }
            for (var wave = 0; wave < 3; wave++)
            {
                var progress = ((time * 0.25f + wave * 0.33f) % 1.0f);
                var radius = 6f + progress * 22f;
                var alpha  = (1f - progress) * 0.4f;
                dl.AddCircle(playerScreen, radius,
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Accent, alpha)), 24, 1.5f);
            }
            dl.AddCircleFilled(playerScreen, 5f,
                ImGui.ColorConvertFloat4ToU32(UIConstants.Accent), 16);
            dl.AddCircle(playerScreen, 5f,
                ImGui.ColorConvertFloat4ToU32(UIConstants.GlowBright), 16, 1.5f);
        }

        if (hoveredSvc != null)
            DrawMarkerHover(dl, hoveredPos, hoveredSvc, mapSz);

        dl.PopClipRect();

        dl.AddRect(
            new Vector2(mapMin.X - 1, mapMin.Y - 1),
            new Vector2(mapMax.X + 1, mapMax.Y + 1),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.18f)),
            0f, ImDrawFlags.None, 3f);
        dl.AddRect(mapMin, mapMax,
            ImGui.ColorConvertFloat4ToU32(UIConstants.GlowDim), 0f, ImDrawFlags.None, 1.5f);

        if (fullMap || MathF.Abs(mapZoom - ZoomDefault) > 0.05f)
        {
            var badge = $"{mapZoom / ZoomDefault:0.0}x";
            var bSz   = ImGui.CalcTextSize(badge);
            var bMin  = new Vector2(mapMax.X - bSz.X - 8, mapMax.Y - bSz.Y - 5);
            dl.AddRectFilled(bMin - new Vector2(4f, 2f), bMin + bSz + new Vector2(4f, 2f),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, fullMap ? 0.75f : 0f)), 3f);
            dl.AddText(bMin,
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, fullMap ? 0.9f : 0.3f)), badge);
        }
    }

    private void DrawMapPins(ImDrawListPtr dl, IReadOnlyList<MapPin> pins, Venue? highlight, Vector2 uv0, Vector2 uv1,
        Vector2 mapMin, float mapSz, short offsetX, short offsetY, ushort sizeFactor, bool mapHovered)
    {
        var lineH = ImGui.GetTextLineHeight();
        var mouse = ImGui.GetMousePos();
        MapPin? hoveredPin = null;

        foreach (var pin in pins)
        {
            var u = HousingMapLoader.WorldToUV(pin.World.X, offsetX, sizeFactor);
            var v = HousingMapLoader.WorldToUV(pin.World.Y, offsetY, sizeFactor);
            var pos = UvToScreen(u, v, uv0, uv1, mapMin, mapSz);
            var isCurrent = pin.Venue == highlight;
            var radius = isCurrent ? 7f : 5.5f;

            if (mapHovered && Vector2.DistanceSquared(mouse, pos) <= 12f * 12f)
                hoveredPin = pin;
            var hot = hoveredPin == pin || isCurrent;

            UIConstants.DrawGem(dl, pos, radius + 3f, UIConstants.Background);
            UIConstants.DrawGem(dl, pos, radius + 1.5f, pin.Edge);
            UIConstants.DrawGem(dl, pos, radius, pin.Color);
            if (UIConstants.IsDark(pin.Color))
                dl.AddQuad(pos + new Vector2(0, -radius), pos + new Vector2(radius, 0), pos + new Vector2(0, radius), pos + new Vector2(-radius, 0),
                    U32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.6f)), 1f);
            if (hot)
                dl.AddQuad(pos + new Vector2(0, -radius - 3f), pos + new Vector2(radius + 3f, 0),
                    pos + new Vector2(0, radius + 3f), pos + new Vector2(-radius - 3f, 0), U32(UIConstants.Accent), 1.2f);

            var label = UIConstants.FitText(pin.Label, 120f);
            var labelSz = ImGui.CalcTextSize(label);
            var labelPos = new Vector2(pos.X + radius + 6f, pos.Y - lineH / 2f);
            dl.AddRectFilled(labelPos - new Vector2(3f, 1f), labelPos + labelSz + new Vector2(3f, 1f),
                U32(UIConstants.WithAlpha(UIConstants.Background, 0.78f)), 2f);
            dl.AddText(labelPos, U32(hot ? UIConstants.Lighten(pin.Color, 0.12f) : UIConstants.TextPrimary), label);
        }

        if (hoveredPin == null) return;

        var (statusText, isOpen) = GetVenueStatus(hoveredPin.Venue);
        using (ImRaii.Tooltip())
        {
            UIConstants.TextReadable(hoveredPin.Color, UIConstants.DisplayName(hoveredPin.Venue.Name));
            ImGui.TextColored(UIConstants.TextSecondary, FormatAddress(hoveredPin.Venue.Address));
            if (statusText.Length > 0)
                ImGui.TextColored(isOpen ? UIConstants.Success : UIConstants.TextSecondary, statusText);
            ImGui.TextColored(UIConstants.TextMuted, Lang.ViewVenueDetails);
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f).Length() < 4f)
            plugin.VenueDetailWindow.Open(hoveredPin.Venue);
    }

    private static Vector2 UvToScreen(float u, float v, Vector2 uv0, Vector2 uv1, Vector2 mapMin, float mapSz)
    {
        var sx = mapMin.X + (u - uv0.X) / (uv1.X - uv0.X) * mapSz;
        var sy = mapMin.Y + (v - uv0.Y) / (uv1.Y - uv0.Y) * mapSz;
        return new Vector2(sx, sy);
    }

    private void DrawSchematic(ImDrawListPtr dl, Vector2 mapMin, Vector2 mapMax, float mapSz)
    {
        dl.AddRectFilled(mapMin, mapMax, ImGui.ColorConvertFloat4ToU32(UIConstants.Background));

        const int divs = 8;
        var dim = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.07f));
        var mid = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.15f));
        for (var g = 1; g < divs; g++)
        {
            var t   = g / (float)divs;
            var gx  = mapMin.X + t * mapSz;
            var gy  = mapMin.Y + t * mapSz;
            var col = (g == divs / 2) ? mid : dim;
            dl.AddLine(new Vector2(gx, mapMin.Y), new Vector2(gx, mapMax.Y), col, 1f);
            dl.AddLine(new Vector2(mapMin.X, gy), new Vector2(mapMax.X, gy), col, 1f);
        }

        const float cL = 18f;
        var acc = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.5f));
        dl.AddLine(mapMin, new Vector2(mapMin.X + cL, mapMin.Y), acc, 2f);
        dl.AddLine(mapMin, new Vector2(mapMin.X, mapMin.Y + cL), acc, 2f);
        dl.AddLine(new Vector2(mapMax.X, mapMin.Y), new Vector2(mapMax.X - cL, mapMin.Y), acc, 2f);
        dl.AddLine(new Vector2(mapMax.X, mapMin.Y), new Vector2(mapMax.X, mapMin.Y + cL), acc, 2f);
        dl.AddLine(new Vector2(mapMin.X, mapMax.Y), new Vector2(mapMin.X + cL, mapMax.Y), acc, 2f);
        dl.AddLine(new Vector2(mapMin.X, mapMax.Y), new Vector2(mapMin.X, mapMax.Y - cL), acc, 2f);
        dl.AddLine(mapMax, new Vector2(mapMax.X - cL, mapMax.Y), acc, 2f);
        dl.AddLine(mapMax, new Vector2(mapMax.X, mapMax.Y - cL), acc, 2f);
    }

    private static float MarkerRadius(float mapSz) => Math.Clamp(mapSz * 0.035f, 9f, 18f);

    private bool DrawMarkerBase(ImDrawListPtr dl, Vector2 pos, Service svc, float mapSz)
    {
        var r = MarkerRadius(mapSz);

        var hovered = ImGui.IsMouseHoveringRect(
            new Vector2(pos.X - r - 5, pos.Y - r - 5),
            new Vector2(pos.X + r + 5, pos.Y + r + 5));

        if (svc.Type == "entrance")
        {
            var ep = (MathF.Sin((float)ImGui.GetTime() * 2f) + 1f) / 2f;
            dl.AddCircle(pos, r + 6 + ep * 6,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1f, 0.5f, 0.15f + 0.2f * ep)), 24, 2f);
        }

        dl.AddCircleFilled(pos, r + 5,
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, hovered ? 0.35f : 0.15f)), 24);

        dl.AddCircleFilled(pos, r,
            ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.Accent : UIConstants.CardBackground), 24);

        dl.AddCircle(pos, r,
            ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.GlowBright : UIConstants.Glow), 24,
            hovered ? 2.5f : 1.5f);

        var iconCol = ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.Background : UIConstants.Glow);
        UIConstants.DrawIconCentered(dl, r * 1.1f, pos, ServiceIcons.GetIcon(svc.Type), iconCol);

        if (hovered)
        {
            var auraTime = (float)ImGui.GetTime();
            for (var ring = 0; ring < 2; ring++)
            {
                var p = ((auraTime * 0.6f + ring * 0.5f) % 1.0f);
                var auraR = r + 4 + p * 14;
                var auraA = (1f - p) * 0.3f;
                dl.AddCircle(pos, auraR,
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, auraA)), 24, 1.5f);
            }
        }

        return hovered;
    }

    private void DrawMarkerHover(ImDrawListPtr dl, Vector2 pos, Service svc, float mapSz)
    {
        var r = MarkerRadius(mapSz);
        var labelText = svc.Label.ToUpperInvariant();
        var lSz = ImGui.CalcTextSize(labelText);
        var lx = pos.X - lSz.X / 2f;
        var ly = pos.Y - r - 16f;
        dl.AddRectFilled(
            new Vector2(lx - 3, ly - 1),
            new Vector2(lx + lSz.X + 3, ly + lSz.Y + 1),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0.85f)));
        dl.AddText(new Vector2(lx, ly),
            ImGui.ColorConvertFloat4ToU32(UIConstants.TextPrimary), labelText);

        using var tooltip = ImRaii.Tooltip();
        ImGui.TextColored(UIConstants.Accent, svc.Label.ToUpperInvariant());
        if (!string.IsNullOrEmpty(svc.Description))
            ImGui.TextColored(UIConstants.TextSecondary, svc.Description);
        ImGui.TextColored(UIConstants.TextMuted, plugin.Guide.IsTarget(svc) ? Lang.GuideClickStop : Lang.GuideClickHint);
    }

    private void ToggleGuide(Service svc)
    {
        if (plugin.Guide.IsTarget(svc))
        {
            plugin.Guide.Stop(notify: true);
            return;
        }
        var config = plugin.ConfigManager.Config;
        if (config == null) return;
        var tracker = plugin.PositionTracker;
        var venue = tracker.GetVenueAtCurrentPlotIncludingGarden(config);
        if (venue != null && venue.Floors.Any(f => f.Services.Contains(svc)))
        {
            plugin.Guide.Start(svc, venue, null);
            return;
        }
        if (venue != null && GardenFloorOf(venue).Services.Contains(svc))
        {
            plugin.Guide.Start(svc, venue, null);
            return;
        }
        var festival = tracker.GetFestivalHere(config);
        if (festival != null && festival.Services.Contains(svc))
            plugin.Guide.Start(svc, null, festival);
    }

    private void DrawGuideBar()
    {
        var guide = plugin.Guide;
        if (!guide.Active) return;

        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight() + 6f;
        var max = start + new Vector2(width, height);
        dl.AddRectFilled(start, max, U32(UIConstants.WithAlpha(UIConstants.Accent, 0.1f)), 3f);
        dl.AddRectFilled(start, new Vector2(start.X + 3f, max.Y), U32(UIConstants.Accent), 3f, ImDrawFlags.RoundCornersLeft);

        var glyph = FontAwesomeIcon.Route.ToIconString();
        Vector2 glyphSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            glyphSize = ImGui.CalcTextSize(glyph);
        dl.AddText(UiBuilder.IconFont, UiBuilder.IconFont.FontSize, new Vector2(start.X + 12f, start.Y + (height - glyphSize.Y) / 2f), U32(UIConstants.Accent), glyph);

        var lineH = ImGui.GetTextLineHeight();
        var stopLabel = Lang.GuideStop.ToUpperInvariant();
        var stopW = ImGui.CalcTextSize(stopLabel).X + 32f;
        var stopH = height - 8f;
        var stopPos = new Vector2(max.X - stopW - 4f, start.Y + (height - stopH) / 2f);

        var textX = start.X + 20f + glyphSize.X;
        var hint = UIConstants.FitText(guide.Hint, MathF.Max(40f, stopPos.X - 10f - textX));
        dl.AddText(new Vector2(textX, start.Y + (height - lineH) / 2f), U32(UIConstants.TextPrimary), hint);

        ImGui.SetCursorScreenPos(stopPos);
        using (ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f, 0.5f)))
        {
            if (UIConstants.AccentButton($"{stopLabel}##guideStop", null, stopW, height: stopH))
                guide.Stop(notify: true);
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, max.Y + 4f));
        ImGui.Dummy(new Vector2(width, 1f));
    }

    private void DrawFilterChips(IEnumerable<Service> services)
    {
        ImGui.Spacing();

        var dl    = ImGui.GetWindowDrawList();
        var lineP = ImGui.GetCursorScreenPos();
        var lineW = ImGui.GetContentRegionAvail().X;
        dl.AddLine(lineP, new Vector2(lineP.X + lineW, lineP.Y),
            ImGui.ColorConvertFloat4ToU32(UIConstants.Line), 1f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

        var allTypes = services
            .Select(s => s.Type)
            .Distinct()
            .ToList();

        var filters = plugin.Configuration.ServiceFilters;
        bool changed;
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding)
                     .Push(ImGuiStyleVar.FrameBorderSize, 1f)
                     .Push(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4)))
            changed = DrawServiceChips(allTypes, filters);

        ImGui.NewLine();

        if (changed) plugin.Configuration.Save();
    }

    private static bool DrawServiceChips(List<string> allTypes, Dictionary<string, bool> filters)
    {
        var changed = false;
        var iconFont = Dalamud.Interface.UiBuilder.IconFont;

        foreach (var type in allTypes)
        {
            var on    = !filters.TryGetValue(type, out var fv) || fv;
            var label = ChipLabel(type);
            var icon  = ServiceIcons.GetIcon(type).ToIconString();

            var chipW = ImGui.CalcTextSize(label).X + 28f;
            using (ImRaii.PushColor(ImGuiCol.Button,
                             on ? UIConstants.WithAlpha(UIConstants.Accent, 0.22f)
                                : UIConstants.WithAlpha(UIConstants.CardBackground, 0.85f))
                         .Push(ImGuiCol.ButtonHovered,
                             on ? UIConstants.WithAlpha(UIConstants.Accent, 0.38f)
                                : UIConstants.WithAlpha(UIConstants.Glow, 0.10f))
                         .Push(ImGuiCol.ButtonActive, UIConstants.WithAlpha(UIConstants.Accent, 0.55f))
                         .Push(ImGuiCol.Border,
                             on ? UIConstants.WithAlpha(UIConstants.Accent, 0.75f) : UIConstants.GlowDim))
            {
                if (ImGui.Button($"##{type}_chip", new Vector2(chipW, 26)))
                {
                    filters[type] = !on;
                    changed = true;
                }
            }

            var bMin = ImGui.GetItemRectMin();
            var bMax = ImGui.GetItemRectMax();
            var bDl  = ImGui.GetWindowDrawList();
            var icC  = ImGui.ColorConvertFloat4ToU32(on ? UIConstants.Glow : UIConstants.TextSecondary);
            bDl.AddText(iconFont, 11f,
                new Vector2(bMin.X + 5, bMin.Y + (bMax.Y - bMin.Y - 11f) / 2f), icC, icon);

            var txC = ImGui.ColorConvertFloat4ToU32(on ? UIConstants.TextPrimary : UIConstants.TextSecondary);
            var txSz = ImGui.CalcTextSize(label);
            bDl.AddText(
                new Vector2(bMin.X + 19, bMin.Y + (bMax.Y - bMin.Y - txSz.Y) / 2f),
                txC, label);

            ImGui.SameLine(0, 4);
            if (ImGui.GetCursorPosX() + 80f > ImGui.GetContentRegionMax().X)
                ImGui.NewLine();
        }

        return changed;
    }

    internal static string? ExtractXivVenuesId(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.Contains("ffxivvenues.com")) return null;

        var hashIdx = url.IndexOf('#');
        if (hashIdx >= 0 && hashIdx < url.Length - 1) return url[(hashIdx + 1)..];

        if (!url.Contains("ffxivvenues.com/venue/")) return null;
        var idx = url.LastIndexOf('/');
        return idx >= 0 && idx < url.Length - 1 ? url[(idx + 1)..] : null;
    }

    internal static string ChipLabel(string type) => type switch
    {
        "entrance"   => Lang.SvcEntrance,
        "bar"        => Lang.SvcBar,
        "stage"      => Lang.SvcStage,
        "gambling"   => Lang.SvcGambling,
        "dj_booth"   => Lang.SvcDjBooth,
        "vip"        => Lang.SvcVip,
        "bath"       => Lang.SvcBath,
        "spa"        => Lang.SvcSpa,
        "event"      => Lang.SvcEvent,
        "upstairs"   => Lang.SvcUpstairs,
        "downstairs" => Lang.SvcDownstairs,
        "stall"      => Lang.SvcStall,
        _ => char.ToUpperInvariant(type.Replace('_', ' ')[0]) + type.Replace('_', ' ')[1..],
    };


    public void Dispose() { }

    private static Vector4 HsvToRgba(float h, float s, float v) => UIConstants.HsvToRgba(h, s, v);

    internal static string TranslateFloorName(string name) => name.ToLowerInvariant() switch
    {
        "ground" => Lang.FloorGround,
        "second" => Lang.FloorSecond,
        "cellar" => Lang.FloorCellar,
        "garden" => Lang.FloorGarden,
        "ward"   => Lang.FloorWard,
        _        => name.ToUpperInvariant(),
    };
}
