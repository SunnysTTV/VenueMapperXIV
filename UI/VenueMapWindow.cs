using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class VenueMapWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private string currentFloorName = string.Empty;
    private string lastDetectedFloor = string.Empty;

    private float _fadeAlpha = 0f;
    private bool  _wasClosed = true;

    private float bMinX, bMaxX, bMinY, bMaxY;
    private string boundsVenueId = string.Empty;

    private float mapZoom = 10.0f;
    private float mapPanX, mapPanY;
    private uint  lastMapId;
    private readonly Dictionary<string, uint> floorMapIds = new();
    private const float ZoomMin = 8.0f;
    private const float ZoomMax = 12.0f;
    private const float ZoomDefault = 10.0f;

    private bool selectDirTab;
    private bool selectEvtTab;
    private bool selectMapTab;
    private bool selectSettingsTab;
    private string searchText = string.Empty;
    private readonly HashSet<string> selectedDcs = new();
    private readonly EventsView eventsView;
    private readonly Dictionary<string, double> favAnimStart = new();
    private readonly Dictionary<string, double> copyAnimStart = new();
    private readonly Dictionary<string, double> tpAnimStart = new();

    public VenueMapWindow(VenueMapperPlugin plugin)
        : base("Venue Map##VenueMapper",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.eventsView = new EventsView(plugin.PartakeApi);
        Size = new Vector2(560, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(350, 500) };

        TitleBarButtons =
        [
            new Dalamud.Interface.Windowing.TitleBarButton
            {
                Icon = Dalamud.Interface.FontAwesomeIcon.FileAlt,
                IconOffset = new Vector2(2, 1),
                Click = _ => plugin.ChangelogWindow.Open(),
                ShowTooltip = () => ImGui.SetTooltip("View older versions"),
                Priority = 0,
            },
        ];

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

        ImGui.PushStyleColor(ImGuiCol.WindowBg,      UIConstants.Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,       UIConstants.WithAlpha(UIConstants.Primary, 0.18f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, UIConstants.WithAlpha(UIConstants.Primary, 0.28f));
        ImGui.PushStyleColor(ImGuiCol.Border,        UIConstants.WithAlpha(UIConstants.Glow, 0.55f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.5f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(4);
    }

    public void ShowDirectory() { selectDirTab = true; }
    public void HideDirectory() { selectMapTab = true; }
    public void ShowEvents() { selectEvtTab = true; }
    public void ShowSettings() { selectSettingsTab = true; }

    public override void OnClose()
    {
        _wasClosed = true;
        plugin.Configuration.WindowPosition = ImGui.GetWindowPos();
        plugin.Configuration.WindowSize = ImGui.GetWindowSize();
        plugin.Configuration.Save();
    }

    public override void Draw()
    {
        var config = plugin.ConfigManager.Config;
        if (config == null || config.Venues.Count == 0)
        {
            ImGui.TextColored(UIConstants.TextSecondary, Lang.NoVenues);
            return;
        }

        var inVenue = plugin.PositionTracker.GetCurrentVenue(config) != null;

        ImGui.PushStyleColor(ImGuiCol.Tab,        UIConstants.WithAlpha(UIConstants.CardBackground, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.TabActive,   UIConstants.WithAlpha(UIConstants.Primary, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered,  UIConstants.WithAlpha(UIConstants.Primary, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, UIConstants.WithAlpha(UIConstants.Primary, 0.15f));

        var mapF = selectMapTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var dirF = selectDirTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var evtF = selectEvtTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var setF = selectSettingsTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var abtF = ImGuiTabItemFlags.None;
        selectMapTab = selectDirTab = selectEvtTab = selectSettingsTab = false;

        if (ImGui.BeginTabBar("##mainTabs"))
        {
            if (!inVenue)
            {
                ImGui.BeginDisabled();
                ImGui.TabItemButton($"{Lang.Map}##tab_map_disabled");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(250f);
                    ImGui.TextColored(UIConstants.Primary, Lang.MapUnavailable);
                    ImGui.TextUnformatted(Lang.MapNotInVenue);
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
                }
            }
            else if (ImGui.BeginTabItem($"{Lang.Map}##tab_map", mapF))
            {
                DrawMapOrDirectory(config);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"{Lang.Directory}##tab_dir", dirF))
            {
                DrawDirectoryTab(config);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"{Lang.Events}##tab_evt", evtF))
            {
                eventsView.SetVenues(config.Venues);
                eventsView.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"{Lang.Settings}##tab_set", setF))
            {
                plugin.SettingsWindow.DrawSettingsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"{Lang.About}##tab_abt", abtF))
            {
                plugin.SettingsWindow.DrawAboutTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.PopStyleColor(4);
    }

    private void DrawDirectoryTab(VenueConfig config)
    {
        DrawDirectoryHeader(config.Venues.Count);
        ImGui.Spacing();
        DrawDirectoryFilters();
        ImGui.Spacing();

        var filtered = config.Venues.AsEnumerable();
        if (!string.IsNullOrEmpty(searchText))
            filtered = filtered.Where(v =>
                v.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                v.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        if (selectedDcs.Count > 0)
            filtered = filtered.Where(v => selectedDcs.Contains(v.Datacenter));

        var venues = filtered.ToList();

        DrawVenueDirectory(venues);
    }

    private void DrawMapOrDirectory(VenueConfig config)
    {
        var currentVenue = plugin.PositionTracker.GetCurrentVenue(config);

        if (currentVenue == null)
        {
            DrawDirectoryHeader(config.Venues.Count);
            ImGui.Spacing();
            DrawVenueDirectory(config.Venues);
            return;
        }

        var detectedFloor = plugin.PositionTracker.GetCurrentFloor(currentVenue);

        if (detectedFloor != null)
        {
            foreach (var f in currentVenue.Floors)
            {
                if (f.MapId > 0)
                    floorMapIds.TryAdd(f.Name, f.MapId);
            }

            var curMapId = plugin.PositionTracker.CurrentMapId;
            if (curMapId > 0)
                floorMapIds[detectedFloor.Name] = curMapId;

            if (detectedFloor.Name != lastDetectedFloor)
            {
                currentFloorName = detectedFloor.Name;
                lastDetectedFloor = detectedFloor.Name;
            }
        }

        var selectedFloor = currentVenue.Floors.FirstOrDefault(f => f.Name == currentFloorName)
                            ?? detectedFloor
                            ?? currentVenue.Floors.FirstOrDefault();

        if (currentVenue.VenueId != boundsVenueId) ComputeBounds(currentVenue);

        DrawHeaderBar(currentVenue, selectedFloor);
        ImGui.Spacing();
        DrawFloorTabs(currentVenue, ref selectedFloor);
        currentFloorName = selectedFloor?.Name ?? string.Empty;

        ImGui.Spacing();

        if (selectedFloor != null)
        {
            floorMapIds.TryGetValue(selectedFloor.Name, out var floorMapId);
            DrawMapPanel(currentVenue, selectedFloor, plugin.PositionTracker.CurrentTerritoryId, floorMapId);
        }

        DrawFilterChips(currentVenue);
    }


    private void DrawDirectoryHeader(int count)
    {
        var dl   = ImGui.GetWindowDrawList();
        var winP = ImGui.GetWindowPos();
        var winW = ImGui.GetWindowWidth();
        var cy   = ImGui.GetCursorScreenPos().Y;

        dl.AddRectFilledMultiColor(
            new Vector2(winP.X, cy - 2),
            new Vector2(winP.X + winW, cy + 44),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0.18f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, 0.10f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, 0f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0f)));

        var shimmerPhase = (float)(ImGui.GetTime() % 3.0) / 3.0f;
        var shimmerX     = winP.X + shimmerPhase * (winW + 80) - 40;
        dl.AddRectFilledMultiColor(
            new Vector2(shimmerX,      cy - 2),
            new Vector2(shimmerX + 60, cy + 44),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0f)),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.05f)),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.05f)),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0f)));

        var title = Lang.VenueDirectory;
        var sub   = Lang.Location(count);
        var tW    = ImGui.CalcTextSize(title).X + ImGui.CalcTextSize(sub).X;
        ImGui.SetCursorPosX(Math.Max(0f, (winW - tW) / 2f));
        ImGui.TextColored(UIConstants.Secondary, title);
        ImGui.SameLine(0, 0);
        ImGui.TextColored(UIConstants.TextSecondary, sub);

        var lsLoaded = plugin.Lifestream.IsLoaded;
        if (lsLoaded)
        {
            const string prefix  = "  Lifestream: ";
            var active  = Lang.Active;
            var prefixW = ImGui.CalcTextSize(prefix).X;
            var activeW = ImGui.CalcTextSize(active).X;
            ImGui.SetCursorPosX(Math.Max(0f, (winW - prefixW - activeW) / 2f));
            ImGui.TextColored(UIConstants.TextSecondary, prefix);
            ImGui.SameLine(0, 0);
            var activeScreenStart = ImGui.GetCursorScreenPos();
            var t = (float)ImGui.GetTime() * 0.15f;
            for (var i = 0; i < active.Length; i++)
            {
                var hue = (t + i * (1f / active.Length) * 0.5f) % 1.0f;
                ImGui.TextColored(HsvToRgba(hue, 0.9f, 1.0f), active[i].ToString());
                ImGui.SameLine(0, 0);
            }
            var dotPulse  = (MathF.Sin((float)ImGui.GetTime() * 2.5f) + 1f) / 2f;
            var dotCenter = new Vector2(
                ImGui.GetCursorScreenPos().X + 5,
                activeScreenStart.Y + ImGui.GetTextLineHeight() * 0.5f);
            dl.AddCircleFilled(dotCenter, 3.5f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 1f, 0.55f, 0.35f + 0.65f * dotPulse)));
            ImGui.NewLine();
        }
        else
        {
            const string prefix2 = "  Lifestream: ";
            var notInst = Lang.NotInstalled;
            var offW = ImGui.CalcTextSize(prefix2).X + ImGui.CalcTextSize(notInst).X + 14;
            ImGui.SetCursorPosX(Math.Max(0f, (winW - offW) / 2f));
            ImGui.TextColored(UIConstants.TextSecondary, prefix2);
            ImGui.SameLine(0, 0);
            var redPulse = (MathF.Sin((float)ImGui.GetTime() * 4f) + 1f) / 2f;
            ImGui.TextColored(new Vector4(0.9f, 0.3f, 0.3f, 0.5f + 0.5f * redPulse), notInst);
            ImGui.SameLine(0, 4);
            var rdCenter = new Vector2(
                ImGui.GetCursorScreenPos().X + 4,
                ImGui.GetCursorScreenPos().Y + ImGui.GetTextLineHeight() * 0.5f);
            dl.AddCircleFilled(rdCenter, 3f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.15f, 0.15f, 0.4f + 0.6f * redPulse)));
            ImGui.NewLine();
        }

        var lineY = ImGui.GetCursorScreenPos().Y;
        dl.AddLine(new Vector2(winP.X + 8, lineY), new Vector2(winP.X + winW - 8, lineY),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0.4f)), 1f);
        dl.AddLine(new Vector2(winP.X + 8, lineY + 2), new Vector2(winP.X + winW - 8, lineY + 2),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0.1f)), 2f);
    }

    private void DrawDirectoryFilters()
    {
        var avW = ImGui.GetContentRegionAvail().X;

        ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.GlowDim);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);

        ImGui.SetNextItemWidth(avW * 0.55f);
        ImGui.InputTextWithHint("##venueSearch", Lang.Search, ref searchText, 128);

        ImGui.SameLine(0, 6);
        ImGui.SetNextItemWidth(avW * 0.40f);
        var dcLabel = selectedDcs.Count == 0 ? Lang.AllDc : string.Join(", ", selectedDcs);
        if (ImGui.BeginCombo("##dcFilter", dcLabel))
        {
            if (ImGui.Selectable(Lang.AllDc, selectedDcs.Count == 0))
                selectedDcs.Clear();
            ImGui.Separator();
            foreach (var dc in new[] { "Aether", "Primal", "Crystal", "Dynamis", "Light", "Chaos", "Materia", "Elemental", "Gaia", "Mana", "Meteor" })
            {
                var on = selectedDcs.Contains(dc);
                if (ImGui.Checkbox(dc, ref on))
                {
                    if (on) selectedDcs.Add(dc);
                    else selectedDcs.Remove(dc);
                }
            }
            ImGui.EndCombo();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    private void DrawVenueDirectory(System.Collections.Generic.List<Venue> venues)
    {
        var dl      = ImGui.GetWindowDrawList();
        var avW     = ImGui.GetContentRegionAvail().X;
        const float padX    = 10f;
        const float padY    = 8f;
        const float gap     = 6f;
        const float btnW    = 96f;
        const float btnGap  = 8f;
        var lineH = ImGui.GetTextLineHeight();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, gap));

        for (var i = 0; i < venues.Count; i++)
        {
            var v = venues[i];

            var cardW    = Math.Max(avW - btnW - btnGap, 60f);
            var textMaxW = Math.Max(cardW - padX * 2 - 12, 40f);
            var addrRaw   = string.IsNullOrEmpty(v.Address) ? "No address configured" : v.Address;
            var addrLines = WrapAddress(addrRaw, textMaxW);
            var rowH      = padY * 2 + lineH * (1 + addrLines.Count) + 4;

            var cardMin = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton($"##venue_{v.VenueId}", new Vector2(cardW, rowH));
            var hovered = ImGui.IsItemHovered();
            var clicked = ImGui.IsItemClicked();

            var cardMax = new Vector2(cardMin.X + cardW, cardMin.Y + rowH);

            var bgCol = hovered
                ? UIConstants.WithAlpha(UIConstants.CardBackground, 1f)
                : UIConstants.WithAlpha(UIConstants.CardBackground, 0.75f);
            dl.AddRectFilled(cardMin, cardMax, ImGui.ColorConvertFloat4ToU32(bgCol));

            var accentCol = hovered ? UIConstants.Primary : UIConstants.Secondary;
            dl.AddRectFilled(
                cardMin,
                new Vector2(cardMin.X + 3, cardMax.Y),
                ImGui.ColorConvertFloat4ToU32(accentCol));

            if (hovered)
            {
                var glowPulse = (MathF.Sin((float)ImGui.GetTime() * 3f) + 1f) / 2f;
                dl.AddRect(cardMin, cardMax,
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.35f + 0.35f * glowPulse)),
                    0f, ImDrawFlags.None, 1.5f);
                dl.AddRect(
                    new Vector2(cardMin.X - 2, cardMin.Y - 2),
                    new Vector2(cardMax.X + 2, cardMax.Y + 2),
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.06f + 0.12f * glowPulse)),
                    0f, ImDrawFlags.None, 2f);
            }
            else
            {
                dl.AddRect(cardMin, cardMax,
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.2f)),
                    0f, ImDrawFlags.None, 1f);
            }

            var tracker = plugin.PositionTracker;
            var isHere = v.TerritoryIds.Contains(tracker.CurrentTerritoryId)
                && (v.Ward <= 0 || v.Plot <= 0 || tracker.CurrentWard < 0
                    || (v.Ward == tracker.CurrentWard + 1 && v.Plot == tracker.CurrentPlot + 1));
            var textX  = cardMin.X + padX + 6;
            var maxTextW = cardW - padX * 2 - 12;

            if (isHere)
            {
                var badge    = Lang.Here2;
                var badgeSz  = ImGui.CalcTextSize(badge);
                var badgeMin = new Vector2(cardMax.X - badgeSz.X - 8, cardMin.Y + 4);
                var badgeMax = new Vector2(badgeMin.X + badgeSz.X + 4, badgeMin.Y + badgeSz.Y + 2);
                dl.AddRectFilled(badgeMin, badgeMax,
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(new Vector4(0.1f, 1f, 0.5f, 1f), 0.25f)));
                dl.AddRect(badgeMin, badgeMax,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1f, 0.55f, 0.7f)), 0f, ImDrawFlags.None, 1f);
                dl.AddText(new Vector2(badgeMin.X + 2, badgeMin.Y + 1),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1f, 0.55f, 1f)), badge);
                maxTextW -= badgeSz.X + 12;
            }

            var isFav = plugin.Configuration.FavoriteVenueIds.Contains(v.VenueId);
            var favAnimActive = favAnimStart.ContainsKey(v.VenueId);
            if (isFav)
            {
                var starGlyph = favAnimActive ? "*" : "*";
                var starCol = ImGui.ColorConvertFloat4ToU32(favAnimActive
                    ? new Vector4(1f, 0.84f, 0f, 0.4f)
                    : new Vector4(1f, 0.84f, 0f, 1f));
                dl.AddText(new Vector2(textX, cardMin.Y + padY), starCol, starGlyph);

                dl.PushClipRect(cardMin, cardMax, true);
                var shimPhase = (float)(ImGui.GetTime() % 2.5) / 2.5f;
                var shimX = cardMin.X + shimPhase * (cardW + 40) - 20;
                dl.AddRectFilledMultiColor(
                    new Vector2(shimX, cardMin.Y),
                    new Vector2(shimX + 30, cardMax.Y),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, 0f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, 0.06f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, 0.06f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, 0f)));
                dl.PopClipRect();
            }

            if (favAnimStart.TryGetValue(v.VenueId, out var animT))
            {
                var elapsed = (float)(ImGui.GetTime() - animT);
                const float dur = 3.0f;
                if (elapsed < dur)
                {
                    var progress = elapsed / dur;
                    dl.PushClipRect(cardMin, cardMax, true);

                    dl.AddRectFilled(cardMin, cardMax,
                        ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0.5f)));

                    if (progress < 0.55f)
                    {
                        var fillP = progress / 0.55f;
                        var fillW = cardW * fillP;
                        dl.AddRectFilled(cardMin, new Vector2(cardMin.X + fillW, cardMax.Y),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.7f, 0f, 0.5f)));
                        var edgeX = cardMin.X + fillW;
                        dl.AddRectFilled(
                            new Vector2(edgeX - 6, cardMin.Y),
                            new Vector2(edgeX, cardMax.Y),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.95f, 0.6f, 0.9f)));
                    }

                    if (progress >= 0.55f && progress < 0.70f)
                    {
                        var flashP = (progress - 0.55f) / 0.15f;
                        dl.AddRectFilled(cardMin, cardMax,
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.9f, 0.4f, 0.6f * (1f - flashP))));

                        var starC = new Vector2(textX, cardMin.Y + padY);
                        var starScale = 1f + 0.5f * (1f - flashP);
                        var starAlpha = Math.Min(1f, flashP * 3f);
                        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * starScale,
                            new Vector2(starC.X - ImGui.GetFontSize() * (starScale - 1f) * 0.3f,
                                        starC.Y - ImGui.GetFontSize() * (starScale - 1f) * 0.3f),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, starAlpha)), "*");

                        for (var p = 0; p < 10; p++)
                        {
                            var angle = p / 10f * MathF.PI * 2f + 0.3f;
                            var dist = 8f + 35f * flashP;
                            var pPos = new Vector2(starC.X + 6, cardMin.Y + rowH * 0.5f)
                                       + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
                            dl.AddCircleFilled(pPos, 3.5f * (1f - flashP),
                                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, 1f - flashP)), 8);
                        }
                    }

                    if (progress >= 0.70f)
                    {
                        var fadeP = (progress - 0.70f) / 0.30f;
                        dl.AddRectFilled(cardMin, cardMax,
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, 0.12f * (1f - fadeP))));

                        dl.AddText(new Vector2(textX, cardMin.Y + padY),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.84f, 0f, 1f)), "*");
                    }

                    dl.PopClipRect();
                }
                else
                {
                    favAnimStart.Remove(v.VenueId);
                }
            }

            var nameX = isFav ? textX + 14 : textX;

            var colors = v.Colors;
            uint nameCol;
            if (colors != null)
            {
                var t = (float)(ImGui.GetTime() % 6.0) / 6.0f;
                nameCol = t < 0.33f
                    ? LerpColor(colors.PrimaryVec, colors.AccentVec, t / 0.33f)
                    : t < 0.66f
                        ? LerpColor(colors.AccentVec, colors.SecondaryVec, (t - 0.33f) / 0.33f)
                        : LerpColor(colors.SecondaryVec, colors.PrimaryVec, (t - 0.66f) / 0.34f);
            }
            else
            {
                nameCol = ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.Primary : UIConstants.TextPrimary);
            }
            dl.AddText(new Vector2(nameX, cardMin.Y + padY), nameCol, v.Name.ToUpperInvariant());

            var addrCol = ImGui.ColorConvertFloat4ToU32(
                hovered ? UIConstants.Glow : UIConstants.TextSecondary);
            var addrY = cardMin.Y + padY + lineH + 4;
            for (var li = 0; li < addrLines.Count; li++)
                dl.AddText(new Vector2(textX, addrY + li * lineH), addrCol, addrLines[li]);


            var hasAddr  = !string.IsNullOrEmpty(v.Address);
            var tpActive = tpAnimStart.TryGetValue(v.VenueId, out var tpT) && (ImGui.GetTime() - tpT) < 4.0;
            var btnLabel = tpActive ? $"...##{v.VenueId}_nav" : $"{Lang.Visit}##{v.VenueId}_nav";
            var btnH     = rowH;

            ImGui.SameLine(0, btnGap);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY());

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));

            if (hasAddr)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        UIConstants.WithAlpha(UIConstants.Glow, 0.12f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Glow, 0.28f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive,  UIConstants.WithAlpha(UIConstants.Glow, 0.50f));
                ImGui.PushStyleColor(ImGuiCol.Border,        UIConstants.GlowDim);
                ImGui.PushStyleColor(ImGuiCol.Text,          UIConstants.Glow);

                if (ImGui.Button(btnLabel, new Vector2(btnW, btnH)) && !tpActive)
                {
                    plugin.Lifestream.NavigateTo(v.Address);
                    tpAnimStart[v.VenueId] = ImGui.GetTime();
                }

                ImGui.PopStyleColor(5);

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(UIConstants.Glow, Lang.TeleportVia);
                    ImGui.TextColored(UIConstants.TextSecondary, v.Address);
                    ImGui.EndTooltip();
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        UIConstants.WithAlpha(UIConstants.CardBackground, 0.4f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.CardBackground, 0.4f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive,  UIConstants.WithAlpha(UIConstants.CardBackground, 0.4f));
                ImGui.PushStyleColor(ImGuiCol.Border,        UIConstants.WithAlpha(UIConstants.GlowDim, 0.2f));
                ImGui.PushStyleColor(ImGuiCol.Text,          UIConstants.WithAlpha(UIConstants.TextSecondary, 0.25f));
                ImGui.Button(btnLabel, new Vector2(btnW, btnH));
                ImGui.PopStyleColor(5);
            }

            ImGui.PopStyleVar(3);

            if (clicked)
            {
                if (isHere)
                    selectMapTab = true;
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup($"##ctx_{v.VenueId}");
            if (ImGui.BeginPopup($"##ctx_{v.VenueId}"))
            {
                if (ImGui.MenuItem(isFav ? Lang.RemoveFavorite : Lang.AddFavorite))
                {
                    if (isFav)
                    {
                        plugin.Configuration.FavoriteVenueIds.Remove(v.VenueId);
                        favAnimStart.Remove(v.VenueId);
                    }
                    else
                    {
                        plugin.Configuration.FavoriteVenueIds.Add(v.VenueId);
                        favAnimStart[v.VenueId] = ImGui.GetTime();
                    }
                    plugin.Configuration.Save();
                }
                if (!string.IsNullOrEmpty(v.Address) && ImGui.MenuItem(Lang.CopyAddress))
                {
                    ImGui.SetClipboardText($"{v.Name} // {v.Address}");
                    copyAnimStart[v.VenueId] = ImGui.GetTime();
                }
                ImGui.EndPopup();
            }

            if (hovered)
            {
                ImGui.BeginTooltip();
                ImGui.TextColored(UIConstants.Primary, v.Name);
                if (!string.IsNullOrEmpty(v.Address))
                    ImGui.TextColored(UIConstants.Glow, v.Address);
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
                    Lang.RightClickHint);
                ImGui.EndTooltip();
            }

            if (copyAnimStart.TryGetValue(v.VenueId, out var copyT))
            {
                var copyElapsed = (float)(ImGui.GetTime() - copyT);
                const float copyDur = 2.5f;
                if (copyElapsed < copyDur)
                {
                    var copyP = copyElapsed / copyDur;
                    var copiedAlpha = copyP < 0.1f ? copyP / 0.1f : Math.Clamp(1f - (copyP - 0.7f) / 0.3f, 0f, 1f);

                    dl.AddRectFilled(cardMin, cardMax,
                        ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0.75f * copiedAlpha)));

                    var copiedText = Lang.Copied;
                    var copiedSz = ImGui.CalcTextSize(copiedText);
                    var floatUp = copyP * 6f;
                    var startX = cardMin.X + (cardW - copiedSz.X) * 0.5f;
                    var startY = cardMin.Y + (rowH - copiedSz.Y) * 0.5f - floatUp;
                    var rainT = (float)ImGui.GetTime() * 0.5f;
                    for (var ci = 0; ci < copiedText.Length; ci++)
                    {
                        var hue = (rainT + ci * 0.1f) % 1.0f;
                        var charCol = HsvToRgba(hue, 0.85f, 1.0f);
                        charCol.W = copiedAlpha;
                        var charX = startX + ImGui.CalcTextSize(copiedText[..ci]).X;
                        dl.AddText(new Vector2(charX, startY),
                            ImGui.ColorConvertFloat4ToU32(charCol), copiedText[ci].ToString());
                    }
                }
                else
                {
                    copyAnimStart.Remove(v.VenueId);
                }
            }

            if (tpAnimStart.TryGetValue(v.VenueId, out var tpAnimT))
            {
                var tpElapsed = (float)(ImGui.GetTime() - tpAnimT);
                const float tpDur = 4.0f;
                if (tpElapsed < tpDur)
                {
                    var tpP = tpElapsed / tpDur;
                    var tpAlpha = tpP < 0.1f ? tpP / 0.1f : Math.Clamp(1f - (tpP - 0.6f) / 0.4f, 0f, 1f);

                    dl.AddRectFilled(cardMin, cardMax,
                        ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0.6f * tpAlpha)));

                    var dots = ((int)(tpElapsed * 3f) % 4);
                    var tpText = Lang.Teleporting.TrimEnd('.') + new string('.', dots);
                    var tpSz = ImGui.CalcTextSize(tpText);
                    var tpX = cardMin.X + (cardW - tpSz.X) * 0.5f;
                    var tpY = cardMin.Y + (rowH - tpSz.Y) * 0.5f;

                    dl.AddText(new Vector2(tpX, tpY),
                        ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, tpAlpha)),
                        tpText);
                }
                else
                {
                    tpAnimStart.Remove(v.VenueId);
                }
            }

            if (v.Links != null && v.Links.HasAny)
            {
                var linkCount = 0;
                if (!string.IsNullOrEmpty(v.Links.Discord)) linkCount++;
                if (!string.IsNullOrEmpty(v.Links.Partake)) linkCount++;
                if (!string.IsNullOrEmpty(v.Links.FfxivVenues)) linkCount++;
                if (!string.IsNullOrEmpty(v.Links.Website)) linkCount++;

                if (linkCount > 0)
                {
                    ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Glow, 0.5f), " └─");
                    ImGui.SameLine(0, 4);

                    var curX = ImGui.GetCursorPosX();
                    var lnkW = (avW - curX - 4f * (linkCount - 1)) / linkCount;

                    void SocialLnk(string label, string url, Vector4 col)
                    {
                        if (string.IsNullOrEmpty(url)) return;
                        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(col, 0.12f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(col, 0.3f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(col, 0.5f));
                        ImGui.PushStyleColor(ImGuiCol.Text, col);
                        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
                        if (ImGui.Button($"{label}##{v.VenueId}_{label}", new Vector2(lnkW, 20)))
                        {
                            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                { FileName = url, UseShellExecute = true }); } catch { }
                        }
                        ImGui.PopStyleVar(2);
                        ImGui.PopStyleColor(4);
                        ImGui.SameLine(0, 4);
                    }

                    SocialLnk("Discord",    v.Links.Discord,     new Vector4(0.34f, 0.40f, 0.93f, 1f));
                    SocialLnk("Partake",    v.Links.Partake,     new Vector4(0.95f, 0.55f, 0.15f, 1f));
                    SocialLnk("XIVVenues",  v.Links.FfxivVenues, new Vector4(0.7f, 0.3f, 0.9f, 1f));
                    SocialLnk("Website",    v.Links.Website,     UIConstants.Glow);
                    ImGui.NewLine();
                }
            }

            ImGui.Spacing();
        }

        ImGui.PopStyleVar();

        ImGui.Spacing();
        var hint = Lang.EnterVenue;
        var hSz  = ImGui.CalcTextSize(hint);
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - hSz.X) / 2f + ImGui.GetCursorPosX());
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.35f), hint);
    }


    private void DrawHeaderBar(Venue venue, Floor? floor)
    {
        var dl   = ImGui.GetWindowDrawList();
        var winP = ImGui.GetWindowPos();
        var winW = ImGui.GetWindowWidth();
        var cy   = ImGui.GetCursorScreenPos().Y;

        dl.AddRectFilledMultiColor(
            new Vector2(winP.X, cy - 2),
            new Vector2(winP.X + winW, cy + 30),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, 0.16f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0.10f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, 0f)));

        var title    = venue.Name.ToUpperInvariant();
        var floorStr = (floor?.Name ?? "-").ToUpperInvariant();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(4, 4));
        if (ImGui.BeginTable("##mapHeader", 3))
        {
            ImGui.TableSetupColumn("##hName",  ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##hFloor", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##h3D",    ImGuiTableColumnFlags.WidthFixed, 50);

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            var nameW = ImGui.CalcTextSize(title).X;
            var colW  = ImGui.GetColumnWidth();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colW - nameW) / 2f));
            ImGui.TextColored(UIConstants.Primary, title);

            ImGui.TableSetColumnIndex(1);
            var floorW = ImGui.CalcTextSize(floorStr).X;
            colW = ImGui.GetColumnWidth();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colW - floorW) / 2f));
            ImGui.TextColored(UIConstants.Glow, floorStr);

            ImGui.TableSetColumnIndex(2);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Glow);
            var markers3d = plugin.PictomancyMarkers.Enabled;
            if (ImGui.Checkbox("3D##markers", ref markers3d))
                plugin.PictomancyMarkers.Enabled = markers3d;
            ImGui.PopStyleColor();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        var lineY = ImGui.GetCursorScreenPos().Y;
        dl.AddLine(
            new Vector2(winP.X + 8, lineY),
            new Vector2(winP.X + winW - 8, lineY),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.45f)), 1f);
        dl.AddLine(
            new Vector2(winP.X + 8, lineY + 2),
            new Vector2(winP.X + winW - 8, lineY + 2),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.12f)), 2f);
    }


    private void DrawFloorTabs(Venue venue, ref Floor? selectedFloor)
    {
        const float gap = 6f;
        var totalGap = gap * (venue.Floors.Count - 1);
        var tabW = (ImGui.GetContentRegionAvail().X - totalGap) / venue.Floors.Count;

        for (var i = 0; i < venue.Floors.Count; i++)
        {
            var f      = venue.Floors[i];
            var active = f == selectedFloor;
            if (i > 0) ImGui.SameLine(0, gap);

            ImGui.PushStyleColor(ImGuiCol.Text,          active ? UIConstants.TextPrimary   : UIConstants.TextSecondary);
            ImGui.PushStyleColor(ImGuiCol.Button,        active ? UIConstants.WithAlpha(UIConstants.Primary, 0.25f)  : UIConstants.WithAlpha(UIConstants.CardBackground, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, active ? UIConstants.WithAlpha(UIConstants.Primary, 0.4f)   : UIConstants.WithAlpha(UIConstants.Glow, 0.12f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive,  UIConstants.Primary);
            ImGui.PushStyleColor(ImGuiCol.Border,        active ? UIConstants.GlowBright    : UIConstants.GlowDim);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, active ? 2f : 1f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);

            if (ImGui.Button(f.Name.ToUpperInvariant(), new Vector2(tabW, 28)))
                selectedFloor = f;

            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(5);

            if (active)
            {
                var dl   = ImGui.GetWindowDrawList();
                var imin = ImGui.GetItemRectMin();
                var imax = ImGui.GetItemRectMax();
                dl.AddLine(new Vector2(imin.X, imax.Y + 2), new Vector2(imax.X, imax.Y + 2),
                    ImGui.ColorConvertFloat4ToU32(UIConstants.Primary), 3f);
                dl.AddLine(new Vector2(imin.X, imax.Y + 6), new Vector2(imax.X, imax.Y + 6),
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.3f)), 2f);
            }
        }
    }


    private void DrawMapPanel(Venue venue, Floor floor, uint territoryId, uint mapId = 0)
    {
        var filters = plugin.Configuration.ServiceFilters;
        var visible = floor.Services
            .Where(s => !filters.TryGetValue(s.Type, out var v) || v)
            .ToList();

        if (mapId != lastMapId)
        {
            lastMapId = mapId;
            mapPanX = 0f;
            mapPanY = 0f;
        }

        var avW   = ImGui.GetContentRegionAvail().X;
        var avH   = ImGui.GetContentRegionAvail().Y - 52f;
        var mapSz = Math.Max(Math.Min(avW, avH), 160f);

        var mapInfo = mapId > 0 ? plugin.MapLoader.GetMapInfoByMapId(mapId) : null;
        mapInfo ??= plugin.MapLoader.GetMapInfo(territoryId);
        var sizeFactor = mapInfo?.SizeFactor > 0 ? mapInfo.SizeFactor : (ushort)200;
        var offsetX    = mapInfo?.OffsetX ?? (short)0;
        var offsetY    = mapInfo?.OffsetY ?? (short)0;

        var padX = (avW - mapSz) / 2f;
        if (padX > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);

        var mapMin = ImGui.GetCursorScreenPos();
        var mapMax = new Vector2(mapMin.X + mapSz, mapMin.Y + mapSz);

        ImGui.InvisibleButton("##mapArea", new Vector2(mapSz, mapSz));
        var hovering = ImGui.IsItemHovered();

        if (hovering)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (MathF.Abs(wheel) > 0.01f)
                mapZoom = Math.Clamp(mapZoom * (1f + wheel * 0.12f), ZoomMin, ZoomMax);

            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                var delta = ImGui.GetIO().MouseDelta;
                mapPanX -= delta.X / mapSz / mapZoom;
                mapPanY -= delta.Y / mapSz / mapZoom;
            }

            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                mapZoom = ZoomDefault;
                mapPanX = 0f;
                mapPanY = 0f;
            }
        }

        var halfUV = 0.5f / mapZoom;
        var viewCX = 0.5f + mapPanX;
        var viewCY = 0.5f + mapPanY;
        var uv0 = new Vector2(viewCX - halfUV, viewCY - halfUV);
        var uv1 = new Vector2(viewCX + halfUV, viewCY + halfUV);

        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(mapMin, mapMax, true);

        bool hasTexture = false;
        var mapTex = plugin.MapLoader.GetMapTexture(territoryId, mapId);
        if (mapTex != null && mapTex.TryGetWrap(out IDalamudTextureWrap? wrap, out _) && wrap != null)
        {
            dl.AddImage(wrap.Handle, mapMin, mapMax, uv0, uv1);
            hasTexture = true;
        }

        if (!hasTexture)
        {
            if (venue.VenueId != boundsVenueId) ComputeBounds(venue);
            DrawSchematic(dl, mapMin, mapMax, mapSz);
        }

        Service? hoveredSvc = null;
        Vector2 hoveredPos = default;
        foreach (var svc in visible)
        {
            var svcU = HousingMapLoader.WorldToUV(svc.X, offsetX, sizeFactor);
            var svcV = HousingMapLoader.WorldToUV(svc.Y, offsetY, sizeFactor);
            var screenPos = UvToScreen(svcU, svcV, uv0, uv1, mapMin, mapSz);
            var isHovered = DrawMarkerBase(dl, screenPos, svc, mapSz);
            if (isHovered) { hoveredSvc = svc; hoveredPos = screenPos; }
        }

        var tracker = plugin.PositionTracker;
        if (tracker.CurrentTerritoryId == territoryId)
        {
            var pU = HousingMapLoader.WorldToUV(tracker.PlayerX, offsetX, sizeFactor);
            var pV = HousingMapLoader.WorldToUV(tracker.PlayerZ, offsetY, sizeFactor);
            var playerScreen = UvToScreen(pU, pV, uv0, uv1, mapMin, mapSz);

            var time = (float)ImGui.GetTime();
            for (var wave = 0; wave < 3; wave++)
            {
                var progress = ((time * 0.25f + wave * 0.33f) % 1.0f);
                var radius = 6f + progress * 22f;
                var alpha  = (1f - progress) * 0.4f;
                dl.AddCircle(playerScreen, radius,
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, alpha)), 24, 1.5f);
            }
            dl.AddCircleFilled(playerScreen, 5f,
                ImGui.ColorConvertFloat4ToU32(UIConstants.Primary), 16);
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

        if (MathF.Abs(mapZoom - ZoomDefault) > 0.05f)
        {
            var badge = $"{mapZoom:0.0}x";
            var bSz   = ImGui.CalcTextSize(badge);
            var bMin  = new Vector2(mapMax.X - bSz.X - 6, mapMax.Y - bSz.Y - 4);
            dl.AddText(bMin,
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.3f)), badge);
        }
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

        if (MathF.Abs(mapZoom - 1f) < 0.05f)
        {
            var hint = Lang.ScrollZoom;
            var hSz  = ImGui.CalcTextSize(hint);
            dl.AddText(
                new Vector2(mapMin.X + (mapMax.X - mapMin.X - hSz.X) / 2f, mapMax.Y - hSz.Y - 6),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.25f)),
                hint);
        }
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
            ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.Primary : UIConstants.CardBackground), 24);

        dl.AddCircle(pos, r,
            ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.GlowBright : UIConstants.Glow), 24,
            hovered ? 2.5f : 1.5f);

        var iconFont = Dalamud.Interface.UiBuilder.IconFont;
        var iconSz   = r * 1.1f;
        var glyph    = ServiceIcons.GetIcon(svc.Type).ToIconString();
        var iconCol  = ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.Background : UIConstants.Glow);
        ImGui.PushFont(iconFont);
        var nativeSz = ImGui.CalcTextSize(glyph);
        ImGui.PopFont();
        var scale  = iconSz / iconFont.FontSize;
        var glyphW = nativeSz.X * scale;
        var glyphH = nativeSz.Y * scale;
        dl.AddText(iconFont, iconSz,
            new Vector2(pos.X - glyphW * 0.5f, pos.Y - glyphH * 0.43f),
            iconCol, glyph);

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

        ImGui.BeginTooltip();
        ImGui.TextColored(UIConstants.Primary, svc.Label.ToUpperInvariant());
        ImGui.TextColored(UIConstants.TextSecondary, $"x: {svc.X:0.0}  z: {svc.Y:0.0}");
        if (!string.IsNullOrEmpty(svc.Description))
            ImGui.TextColored(UIConstants.TextSecondary, svc.Description);
        ImGui.EndTooltip();
    }


    private void DrawFilterChips(Venue venue)
    {
        ImGui.Spacing();

        var dl    = ImGui.GetWindowDrawList();
        var lineP = ImGui.GetCursorScreenPos();
        var lineW = ImGui.GetContentRegionAvail().X;
        dl.AddLine(lineP, new Vector2(lineP.X + lineW, lineP.Y),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.2f)), 1f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

        var allTypes = venue.Floors
            .SelectMany(f => f.Services)
            .Select(s => s.Type)
            .Distinct()
            .ToList();

        var filters = plugin.Configuration.ServiceFilters;
        var changed = false;

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));

        var iconFont = Dalamud.Interface.UiBuilder.IconFont;

        foreach (var type in allTypes)
        {
            var on    = !filters.TryGetValue(type, out var fv) || fv;
            var label = ChipLabel(type);
            var icon  = ServiceIcons.GetIcon(type).ToIconString();

            ImGui.PushStyleColor(ImGuiCol.Button,
                on ? UIConstants.WithAlpha(UIConstants.Primary, 0.22f)
                   : UIConstants.WithAlpha(UIConstants.CardBackground, 0.85f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered,
                on ? UIConstants.WithAlpha(UIConstants.Primary, 0.38f)
                   : UIConstants.WithAlpha(UIConstants.Glow, 0.10f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(UIConstants.Primary, 0.55f));
            ImGui.PushStyleColor(ImGuiCol.Border,
                on ? UIConstants.WithAlpha(UIConstants.Primary, 0.75f) : UIConstants.GlowDim);

            var chipW = ImGui.CalcTextSize(label).X + 28f;
            if (ImGui.Button($"##{type}_chip", new Vector2(chipW, 26)))
            {
                filters[type] = !on;
                changed = true;
            }
            ImGui.PopStyleColor(4);

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

        ImGui.PopStyleVar(3);
        ImGui.NewLine();

        if (changed) plugin.Configuration.Save();
    }

    private static string ChipLabel(string type) => type switch
    {
        "dj_booth" => "DJ Stage",
        _ => char.ToUpperInvariant(type.Replace('_', ' ')[0]) + type.Replace('_', ' ')[1..],
    };

    private static List<string> WrapAddress(string addr, float maxW)
    {
        if (ImGui.CalcTextSize(addr).X <= maxW)
            return [addr];

        var parts = addr.Split(" - ");
        var lines = new List<string>();
        var current = "";
        foreach (var part in parts)
        {
            var candidate = current.Length == 0 ? part : $"{current} - {part}";
            if (ImGui.CalcTextSize(candidate).X > maxW && current.Length > 0)
            {
                lines.Add(current);
                current = part;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0)
            lines.Add(current);

        return lines.Count > 0 ? lines : [addr];
    }


    private void ComputeBounds(Venue venue)
    {
        var pts = venue.Floors.SelectMany(f => f.Services).ToList();
        if (pts.Count == 0)
        {
            bMinX = -10; bMaxX = 10; bMinY = -10; bMaxY = 10;
            boundsVenueId = venue.VenueId;
            return;
        }

        bMinX = pts.Min(p => p.X); bMaxX = pts.Max(p => p.X);
        bMinY = pts.Min(p => p.Y); bMaxY = pts.Max(p => p.Y);

        var px = Math.Max((bMaxX - bMinX) * 0.20f, 4f);
        var py = Math.Max((bMaxY - bMinY) * 0.20f, 4f);
        bMinX -= px; bMaxX += px;
        bMinY -= py; bMaxY += py;

        var rx = bMaxX - bMinX; var ry = bMaxY - bMinY;
        if (rx > ry) { var e = (rx - ry) / 2f; bMinY -= e; bMaxY += e; }
        else         { var e = (ry - rx) / 2f; bMinX -= e; bMaxX += e; }

        boundsVenueId = venue.VenueId;
    }

    public void Dispose() { }

    private static Vector4 HsvToRgba(float h, float s, float v)
    {
        h = (h % 1f + 1f) % 1f;
        var i = (int)(h * 6);
        var f = h * 6 - i;
        float p = v * (1 - s), q = v * (1 - f * s), t2 = v * (1 - (1 - f) * s);
        return (i % 6) switch
        {
            0 => new Vector4(v,  t2, p,  1f),
            1 => new Vector4(q,  v,  p,  1f),
            2 => new Vector4(p,  v,  t2, 1f),
            3 => new Vector4(p,  q,  v,  1f),
            4 => new Vector4(t2, p,  v,  1f),
            _ => new Vector4(v,  p,  q,  1f),
        };
    }

    private static uint LerpColor(Vector4 from, Vector4 to, float t)
    {
        var c = new Vector4(
            from.X + (to.X - from.X) * t,
            from.Y + (to.Y - from.Y) * t,
            from.Z + (to.Z - from.Z) * t,
            1f);
        return ImGui.ColorConvertFloat4ToU32(c);
    }
}
