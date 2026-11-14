using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class UpdateNotesWindow : Window, IDisposable
{
    private const string DiscordUrl = "https://discord.com/invite/agKWEzK5nR";
    private const string KofiUrl = "https://ko-fi.com/sunnysofficial";

    private const float FrameCut = 14f;
    private const float BannerH = 104f;
    private const float PadX = 18f;
    private const float IconBox = 36f;
    private const float TagColumn = 72f;

    private static readonly Random Rng = new();

    private readonly VenueMapperPlugin plugin;
    private readonly IFontHandle titleFont;
    private readonly string? iconPath = FindIcon();
    private readonly HashSet<string> expandedVersions = new();
    private readonly List<VersionGroup> groups = BuildGroups();
    private uint artTerritory = 339;
    private bool showCredits;
    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    private sealed record VersionEntry(string Ver, string Date, List<(string Ver, string Date)> Hotfixes);
    private sealed record VersionGroup(string Series, List<VersionEntry> Versions);
    private sealed record Credit(string Name, string? Author, Func<string> Role, string? Url);

    public UpdateNotesWindow(VenueMapperPlugin plugin)
        : base("Update Notes###VenueMapperUpdateNotes",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        Size = new Vector2(540, 700);
        SizeCondition = ImGuiCond.Always;
        titleFont = VenueMapperPlugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
            new GameFontStyle(GameFontFamilyAndSize.Jupiter23));
    }

    private static List<VersionGroup> BuildGroups()
    {
        var mains = new List<VersionEntry>();
        var byVersion = new Dictionary<string, VersionEntry>();
        foreach (var (ver, date) in Enumerable.Reverse(ChangelogData.Versions))
        {
            var parsed = ChangelogData.ParseVersion(ver);
            if (parsed.Revision > 0 && byVersion.TryGetValue($"v{parsed.Major}.{parsed.Minor}.{parsed.Build}", out var parent))
            {
                parent.Hotfixes.Insert(0, (ver, date));
                continue;
            }
            var entry = new VersionEntry(ver, date, new List<(string, string)>());
            mains.Add(entry);
            byVersion[ver] = entry;
        }
        mains.Reverse();

        var groups = new List<VersionGroup>();
        foreach (var entry in mains)
        {
            var parsed = ChangelogData.ParseVersion(entry.Ver);
            var series = $"{parsed.Major}.{parsed.Minor}";
            if (groups.Count == 0 || groups[^1].Series != series)
                groups.Add(new VersionGroup(series, new List<VersionEntry>()));
            groups[^1].Versions.Add(entry);
        }
        return groups;
    }

    public void Open(string? version = null)
    {
        expandedVersions.Clear();
        var target = version ?? (groups.Count > 0 && groups[0].Versions.Count > 0 ? groups[0].Versions[0].Ver : null);
        if (target != null) expandedVersions.Add(target);
        artTerritory = HousingData.WardTerritory((HousingDistrict)Rng.Next(1, 6));
        showCredits = false;
        IsOpen = true;
    }

    public override void OnClose()
    {
        if (!plugin.Configuration.PendingWhatsNew) return;
        plugin.Configuration.PendingWhatsNew = false;
        plugin.Configuration.Save();
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(PadX, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor();
    }

    public ScreenRect LastRect { get; private set; }

    public override void Draw()
    {
        LastRect = ScreenRect.FromCurrentWindow();
        try
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var max = min + ImGui.GetWindowSize();
            var alpha = ImGui.GetStyle().Alpha;
            var rgb = plugin.EasterEggManager.IsRgbOverloadActive;

            UIConstants.DrawHudFrame(dl, min, max, FrameCut, alpha, rgb, grip: false);
            DrawBanner(dl, min, max);
            if (rgb)
            {
                UIConstants.Chamfer(dl, min + new Vector2(0.5f, 0.5f), max - new Vector2(0.5f, 0.5f), FrameCut);
                dl.PathStroke(U32(UIConstants.WithAlpha(UIConstants.Accent, alpha)), ImDrawFlags.Closed, 3f);
            }
            else
            {
                UIConstants.DrawFrameCorner(dl, min, FrameCut, alpha);
            }

            using (ImRaii.Disabled(UIConstants.IsHackerBooting))
            {
                DrawCloseButton(min, max);
                DrawHeader(dl, min);
                DrawTabs();
                DrawBody();
                DrawFooter();
            }

            HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName, 30f, FrameCut, 20f, 4f);
        }
        catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "[VenueMapper] UpdateNotesWindow draw failed"); }
    }

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    private void DrawBanner(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        var min = winMin + new Vector2(1f, 1f);
        var max = new Vector2(winMax.X - 1f, winMin.Y + BannerH);
        var art = VenueBanner.GameArt(plugin, artTerritory);

        if (art != null)
        {
            var (uv0, uv1) = VenueBanner.CoverUv(art, max - min);
            Vector2 Uv(Vector2 p) => uv0 + (p - min) / (max - min) * (uv1 - uv0);
            var cutX = min.X + FrameCut;
            var cutY = min.Y + FrameCut;
            dl.AddImage(art.Handle, new Vector2(cutX, min.Y), max, Uv(new Vector2(cutX, min.Y)), uv1);
            var a = new Vector2(min.X, cutY);
            var b = new Vector2(cutX, min.Y);
            var c = new Vector2(cutX, max.Y);
            var d = new Vector2(min.X, max.Y);
            dl.AddImageQuad(art.Handle, a, b, c, d, Uv(a), Uv(b), Uv(c), Uv(d));
            dl.AddRectFilled(min + new Vector2(FrameCut, 0f), max, U32(UIConstants.WithAlpha(UIConstants.Background, 0.2f)));
        }
        else
        {
            dl.AddRectFilledMultiColor(new Vector2(min.X + FrameCut, min.Y), max,
                U32(UIConstants.Mix(UIConstants.PanelRaised, UIConstants.Accent, 0.12f)),
                U32(UIConstants.PanelRaised), U32(UIConstants.PanelRaised), U32(UIConstants.PanelRaised));
        }
        VenueBanner.DrawFade(dl, min, max, 0.25f);

        const string title = "VENUE MAPPER";
        using (titleFont.Push())
        {
            var font = ImGui.GetFont();
            var size = ImGui.GetFontSize();
            var ts = ImGui.CalcTextSize(title);
            var pos = new Vector2((min.X + max.X - ts.X) / 2f, min.Y + BannerH * 0.36f - ts.Y / 2f);
            dl.AddText(font, size, pos + new Vector2(0f, 1.5f), U32(new Vector4(0f, 0f, 0f, 0.8f)), title);
            dl.AddText(font, size, pos, U32(UIConstants.TextPrimary), title);
        }
    }

    private void DrawCloseButton(Vector2 min, Vector2 max)
    {
        const float size = 24f;
        var pos = new Vector2(max.X - size - 10f, min.Y + 10f);
        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton("##unClose", new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + new Vector2(size, size), U32(UIConstants.WithAlpha(hovered ? UIConstants.Hover : UIConstants.Background, hovered ? 0.95f : 0.55f)), 3f);
        UIConstants.DrawIconCentered(dl, UiBuilder.IconFont.FontSize, pos + new Vector2(size / 2f), FontAwesomeIcon.Times,
            U32(hovered ? UIConstants.TextPrimary : UIConstants.TextSecondary));
        if (hovered) ImGui.SetTooltip(Lang.Close);
        if (clicked) IsOpen = false;
    }

    private void DrawHeader(ImDrawListPtr dl, Vector2 winMin)
    {
        var lineH = ImGui.GetTextLineHeight();
        var boxMin = new Vector2(winMin.X + PadX, winMin.Y + BannerH + 6f);
        var boxMax = boxMin + new Vector2(IconBox, IconBox);

        var icon = PluginIcon();
        if (icon != null)
            dl.AddImage(icon.Handle, boxMin, boxMax);
        else
            HudIcons.Crystal(dl, (boxMin + boxMax) / 2f, IconBox * 0.6f, U32(UIConstants.Accent));

        var textX = boxMax.X + 10f;
        var titleY = boxMin.Y + (IconBox - lineH * 2f - 2f) / 2f;
        dl.AddText(new Vector2(textX, titleY), U32(UIConstants.TextPrimary), Lang.UpdateNotes);
        DrawPill(dl, new Vector2(textX + ImGui.CalcTextSize(Lang.UpdateNotes).X + 8f, titleY), ChangelogData.PluginVersion, UIConstants.Heading);
        dl.AddText(new Vector2(textX, titleY + lineH + 2f), U32(UIConstants.TextMuted), Lang.UpdateTagline);

        ImGui.SetCursorScreenPos(new Vector2(winMin.X + PadX, boxMax.Y + 10f));
    }

    private IDalamudTextureWrap? PluginIcon()
        => iconPath != null && VenueMapperPlugin.TextureProvider.GetFromFile(iconPath).TryGetWrap(out var wrap, out _) ? wrap : null;

    private static string? FindIcon()
    {
        var dir = Path.GetDirectoryName(VenueMapperPlugin.PluginInterface.AssemblyLocation.FullName);
        if (dir == null) return null;
        var path = Path.Combine(dir, "images", "icon.png");
        return File.Exists(path) ? path : null;
    }

    private static void DrawPill(ImDrawListPtr dl, Vector2 pos, string text, Vector4 color)
    {
        const float padX = 7f;
        var ts = ImGui.CalcTextSize(text);
        var min = new Vector2(pos.X, pos.Y);
        var max = new Vector2(pos.X + ts.X + padX * 2f, pos.Y + ts.Y);
        var r = (max.Y - min.Y) / 2f;
        dl.AddRect(min, max, U32(UIConstants.WithAlpha(color, 0.5f)), r, ImDrawFlags.None, 1f);
        dl.AddText(new Vector2(pos.X + padX, pos.Y), U32(color), text);
    }

    private void DrawTabs()
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        const float tabH = 26f;

        if (TabButton("##unTabLog", Lang.ChangelogTab, !showCredits))
            showCredits = false;
        ImGui.SameLine(0, 18f);
        if (TabButton("##unTabCredits", Lang.CreditsTab, showCredits))
            showCredits = true;

        dl.AddLine(new Vector2(start.X, start.Y + tabH), new Vector2(start.X + width, start.Y + tabH), U32(UIConstants.Line), 1f);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + tabH + 8f));
    }

    private static bool TabButton(string id, string label, bool active)
    {
        var textSize = ImGui.CalcTextSize(label);
        var size = new Vector2(textSize.X, 26f);
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        var col = active || hovered ? UIConstants.TextPrimary : UIConstants.TextMuted;
        dl.AddText(new Vector2(pos.X, pos.Y + (size.Y - textSize.Y) / 2f - 1f), U32(col), label);
        if (active)
            dl.AddRectFilled(new Vector2(pos.X, pos.Y + size.Y - 2f), new Vector2(pos.X + size.X, pos.Y + size.Y), U32(UIConstants.Accent));
        return clicked;
    }

    private float FooterHeight => ImGui.GetFrameHeight() + 20f;

    private void DrawBody()
    {
        var height = ImGui.GetContentRegionAvail().Y - FooterHeight;
        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##unBody", new Vector2(-1, height));
        if (!child) return;

        if (showCredits)
            DrawCredits();
        else
            DrawVersions();
    }

    private void DrawVersions()
    {
        var first = true;
        foreach (var group in groups)
        {
            SeriesHeader(group.Series, first);
            foreach (var entry in group.Versions)
            {
                DrawVersionCard(entry, first);
                first = false;
            }
        }
    }

    private static void SeriesHeader(string series, bool first)
    {
        if (!first) ImGui.Dummy(new Vector2(0, 4f));
        ImGui.TextColored(UIConstants.Heading, series);
        ImGui.Dummy(new Vector2(0, 1f));
    }

    private void DrawVersionCard(VersionEntry entry, bool latest)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X - 8f;
        var lineH = ImGui.GetTextLineHeight();
        var headerH = lineH + 14f;
        var hasNotes = ChangelogData.Changelogs.ContainsKey(entry.Ver) || entry.Hotfixes.Count > 0;
        var expanded = hasNotes && expandedVersions.Contains(entry.Ver);

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        if (ImGui.InvisibleButton($"##unVer{entry.Ver}", new Vector2(width, headerH)) && hasNotes)
        {
            if (!expandedVersions.Remove(entry.Ver))
                expandedVersions.Add(entry.Ver);
        }
        var hovered = hasNotes && ImGui.IsItemHovered();
        var bottom = ImGui.GetItemRectMax().Y;

        var y = start.Y + (headerH - lineH) / 2f;
        var x = start.X + 10f;
        if (hasNotes)
        {
            var chevron = (expanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight).ToIconString();
            var iconFont = UiBuilder.IconFont;
            var iconSize = iconFont.FontSize * 0.65f;
            dl.AddText(iconFont, iconSize, new Vector2(x, start.Y + (headerH - iconSize) / 2f),
                U32(hovered ? UIConstants.Accent : UIConstants.TextMuted), chevron);
        }
        x += 16f;
        dl.AddText(new Vector2(x, y), U32(UIConstants.TextPrimary), entry.Ver);
        x += ImGui.CalcTextSize(entry.Ver).X + 8f;
        if (latest)
        {
            dl.AddText(new Vector2(x, y), U32(UIConstants.Heading), Lang.LatestBadge);
            x += ImGui.CalcTextSize(Lang.LatestBadge).X + 8f;
        }
        foreach (var (hotfix, _) in entry.Hotfixes)
        {
            var badge = Lang.HotfixBadge(hotfix.TrimStart('v'));
            var bs = ImGui.CalcTextSize(badge);
            var bMin = new Vector2(x, y);
            var bMax = new Vector2(x + bs.X + 12f, y + bs.Y);
            dl.AddRect(bMin, bMax, U32(UIConstants.LineStrong), bs.Y / 2f, ImDrawFlags.None, 1f);
            dl.AddText(new Vector2(x + 6f, y), U32(UIConstants.TextSecondary), badge);
            x = bMax.X + 6f;
        }

        var dw = ImGui.CalcTextSize(entry.Date).X;
        dl.AddText(new Vector2(start.X + width - 10f - dw, y), U32(UIConstants.TextMuted), entry.Date);

        if (expanded)
        {
            dl.AddLine(new Vector2(start.X + 10f, start.Y + headerH), new Vector2(start.X + width - 10f, start.Y + headerH),
                U32(UIConstants.Line), 1f);
            ImGui.Dummy(new Vector2(0, 2f));
            var wrapX = ImGui.GetCursorPosX() + width - 12f;
            using (ImRaii.PushIndent(26f, scaled: false))
            {
                if (ChangelogData.Changelogs.TryGetValue(entry.Ver, out var sections))
                    DrawSections(sections, wrapX, entry.Ver);
                foreach (var (hotfix, date) in entry.Hotfixes)
                {
                    if (!ChangelogData.Changelogs.TryGetValue(hotfix, out var hotfixSections)) continue;
                    ImGui.Dummy(new Vector2(0, 2f));
                    var lineStart = ImGui.GetCursorScreenPos();
                    DrawDashedLine(dl, lineStart.X, start.X + width - 12f, lineStart.Y);
                    ImGui.Dummy(new Vector2(0, 4f));
                    SectionTitle(Lang.HotfixHeader(hotfix.TrimStart('v'), date), FontAwesomeIcon.Wrench);
                    DrawSections(hotfixSections, wrapX, hotfix, false);
                }
            }
            ImGui.Dummy(new Vector2(0, 6f));
            bottom = ImGui.GetItemRectMax().Y;
        }

        var end = new Vector2(start.X + width, bottom);
        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(start, end, U32(hovered ? UIConstants.Mix(UIConstants.PanelRaised, UIConstants.Hover, 0.6f) : UIConstants.PanelRaised), 3f);
        dl.AddRect(start, end, U32(latest ? UIConstants.WithAlpha(UIConstants.Heading, 0.35f) : UIConstants.LineStrong), 3f, ImDrawFlags.None, 1f);
        if (latest)
            dl.AddRectFilled(new Vector2(start.X, start.Y + 4f), new Vector2(start.X + 2f, end.Y - 4f), U32(UIConstants.Accent));
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(start.X, end.Y + 5f));
        ImGui.Dummy(Vector2.Zero);
    }

    private static void DrawDashedLine(ImDrawListPtr dl, float x0, float x1, float y)
    {
        var col = U32(UIConstants.LineStrong);
        for (var x = x0; x < x1; x += 7f)
            dl.AddLine(new Vector2(x, y), new Vector2(MathF.Min(x + 4f, x1), y), col, 1f);
    }

    private static void SectionTitle(string title, FontAwesomeIcon? icon = null)
    {
        ImGui.Dummy(new Vector2(0, 3f));
        if (icon is { } glyph)
        {
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var lineH = ImGui.GetTextLineHeight();
            var iconFont = UiBuilder.IconFont;
            var size = iconFont.FontSize * 0.8f;
            dl.AddText(iconFont, size, new Vector2(pos.X, pos.Y + (lineH - size) / 2f), U32(UIConstants.Accent), glyph.ToIconString());
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + size + 7f);
        }
        using (ImRaii.PushColor(ImGuiCol.Text, UIConstants.Heading))
            ImGui.TextUnformatted(title.ToUpperInvariant());
    }

    private static FontAwesomeIcon SectionIcon(string? englishTitle) => englishTitle switch
    {
        null                                       => FontAwesomeIcon.Wrench,
        "New Look"                                 => FontAwesomeIcon.PaintBrush,
        "Directory"                                => FontAwesomeIcon.List,
        "3D Markers"                               => FontAwesomeIcon.Cube,
        "Venues"                                   => FontAwesomeIcon.Store,
        "Festivals"                                => FontAwesomeIcon.Flag,
        "Gardens"                                  => FontAwesomeIcon.Leaf,
        "Location & Map"                           => FontAwesomeIcon.MapMarkedAlt,
        "Events & Schedules"                       => FontAwesomeIcon.CalendarAlt,
        "Security"                                 => FontAwesomeIcon.ShieldAlt,
        "Notifications"                            => FontAwesomeIcon.Bell,
        "Quick Popup"                              => FontAwesomeIcon.Bolt,
        "Venue Details"                            => FontAwesomeIcon.InfoCircle,
        "Owner Verification" or "Owner Setup"      => FontAwesomeIcon.IdCard,
        "Setup Wizard"                             => FontAwesomeIcon.Magic,
        _                                          => FontAwesomeIcon.Star,
    };

    private static int TagOrder(string tag) => tag switch
    {
        "ADDED"    => 0,
        "IMPROVED" => 1,
        "CHANGED"  => 2,
        "FIXED"    => 3,
        "REMOVED"  => 4,
        _          => 5,
    };

    private static string TagWord(string tag) => tag switch
    {
        "ADDED"    => Lang.TagNew,
        "IMPROVED" => Lang.TagBetter,
        "CHANGED"  => Lang.TagChanged,
        "FIXED"    => Lang.TagFixed,
        "REMOVED"  => Lang.TagRemoved,
        _          => "",
    };

    private static readonly Vector4 FixedColor = UIConstants.HexToVec4("#B49CFF");

    private static Vector4 TagColor(string tag) => tag switch
    {
        "ADDED"    => UIConstants.Success,
        "IMPROVED" => UIConstants.Accent,
        "CHANGED"  => UIConstants.Warning,
        "FIXED"    => FixedColor,
        "REMOVED"  => UIConstants.Danger,
        _          => UIConstants.TextMuted,
    };

    private static void TagPill(string tag)
    {
        var word = TagWord(tag);
        if (word.Length == 0) return;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        var color = TagColor(tag);
        var font = ImGui.GetFont();
        var size = ImGui.GetFontSize() * 0.82f;
        var textW = ImGui.CalcTextSize(word).X * 0.82f;
        var pillW = TagColumn - 10f;
        var min = new Vector2(pos.X, pos.Y + 1f);
        var max = new Vector2(pos.X + pillW, pos.Y + lineH);
        dl.AddRectFilled(min, max, U32(UIConstants.WithAlpha(color, 0.14f)), 3f);
        dl.AddRect(min, max, U32(UIConstants.WithAlpha(color, 0.35f)), 3f, ImDrawFlags.None, 1f);
        dl.AddText(font, size, new Vector2(pos.X + (pillW - textW) / 2f, pos.Y + (lineH - size) / 2f + 0.5f), U32(color), word);
        ImGui.Dummy(new Vector2(pillW, lineH));
    }

    private static readonly HashSet<string> CollapsedSections = new();

    private static bool SectionHeader(string id, string title, FontAwesomeIcon icon, int count, bool collapsed, float wrapX)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        var width = MathF.Max(wrapX - ImGui.GetCursorPosX(), 40f);
        var height = lineH + 6f;
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (hovered)
            dl.AddRectFilled(pos - new Vector2(4f, 0f), pos + new Vector2(width, height), U32(UIConstants.WithAlpha(UIConstants.Hover, 0.7f)), 3f);

        var y = pos.Y + (height - lineH) / 2f;
        var iconFont = UiBuilder.IconFont;
        var iconSize = iconFont.FontSize * 0.8f;
        dl.AddText(iconFont, iconSize, new Vector2(pos.X, pos.Y + (height - iconSize) / 2f), U32(UIConstants.Accent), icon.ToIconString());
        var x = pos.X + iconSize + 7f;
        var text = title.ToUpperInvariant();
        dl.AddText(new Vector2(x, y), U32(UIConstants.Heading), text);
        x += ImGui.CalcTextSize(text).X + 8f;
        if (collapsed)
            dl.AddText(new Vector2(x, y), U32(UIConstants.TextMuted), count.ToString());

        var chevron = (collapsed ? FontAwesomeIcon.ChevronRight : FontAwesomeIcon.ChevronDown).ToIconString();
        var chevronSize = iconFont.FontSize * 0.6f;
        dl.AddText(iconFont, chevronSize, new Vector2(pos.X + width - chevronSize - 4f, pos.Y + (height - chevronSize) / 2f),
            U32(hovered ? UIConstants.Accent : UIConstants.TextMuted), chevron);
        return clicked;
    }

    internal static void DrawSections(ChangelogSection[] sections, float wrapX, string key, bool showUntitled = true)
    {
        var german = ChangelogData.CurrentLanguage == "DE";
        var first = true;
        foreach (var section in sections)
        {
            var title = german && section.TitleDE != null ? section.TitleDE : section.Title;
            if (title == null && showUntitled && sections.Length > 1)
                title = Lang.MoreChanges;

            if (!first)
            {
                ImGui.Dummy(new Vector2(0, 2f));
                var linePos = ImGui.GetCursorScreenPos();
                var lineEnd = linePos.X + MathF.Max(wrapX - ImGui.GetCursorPosX(), 0f);
                ImGui.GetWindowDrawList().AddLine(linePos, new Vector2(lineEnd, linePos.Y), U32(UIConstants.LineStrong), 1f);
                ImGui.Dummy(new Vector2(0, 2f));
            }
            first = false;

            var sectionKey = $"{key}|{section.Title ?? "more"}";
            var collapsed = CollapsedSections.Contains(sectionKey);
            if (title != null)
            {
                if (SectionHeader($"##sec{sectionKey}", title, SectionIcon(section.Title), section.Entries.Length, collapsed, wrapX))
                {
                    if (!CollapsedSections.Remove(sectionKey))
                        CollapsedSections.Add(sectionKey);
                }
                if (collapsed) continue;
            }

            foreach (var entry in section.Entries.OrderBy(e => TagOrder(e.Tag)))
            {
                var lineStartX = ImGui.GetCursorPosX();
                TagPill(entry.Tag);
                ImGui.SameLine(lineStartX + TagColumn);
                using (ImRaii.Group())
                using (ImRaii.TextWrapPos(wrapX))
                    ImGui.TextColored(UIConstants.TextSecondary, german && entry.TextDE != null ? entry.TextDE : entry.Text);
                ImGui.Dummy(new Vector2(0, 1f));
            }
        }
    }
    private static readonly Credit[] Libraries =
    [
        new("Dalamud", "goatcorp", () => Lang.CreditsDalamud, "https://github.com/goatcorp/Dalamud"),
        new("Pictomancy", "sourpuh", () => Lang.CreditsPictomancy, "https://github.com/sourpuh/ffxiv_pictomancy"),
        new("Lifestream", "NightmareXIV", () => Lang.CreditsLifestream, "https://github.com/NightmareXIV/Lifestream"),
        new("FFXIVClientStructs", "aers", () => Lang.CreditsClientStructs, "https://github.com/aers/FFXIVClientStructs"),
        new("Lumina", "NotAdam", () => Lang.CreditsLumina, "https://github.com/NotAdam/Lumina"),
    ];

    private static readonly Credit[] DataSources =
    [
        new("Partake", null, () => Lang.CreditsPartake, "https://www.partake.gg"),
        new("FFXIVVenues", null, () => Lang.CreditsXivVenues, "https://ffxivvenues.com"),
    ];

    private static readonly Credit Developer =
        new("SunnysOfficial", null, () => "Venue Mapper", "https://github.com/SunnysTTV/VenueMapperXIV");

    private static readonly Credit[] Artwork =
    [
        new("Hultay", null, () => Lang.CreditsLogo, null),
    ];

    private void DrawCredits()
    {
        SectionTitle(Lang.CreditsDeveloper);
        CreditRow(Developer);

        SectionTitle(Lang.CreditsArtwork);
        foreach (var credit in Artwork)
            CreditRow(credit);

        SectionTitle(Lang.CreditsLibraries);
        foreach (var credit in Libraries)
            CreditRow(credit);

        SectionTitle(Lang.CreditsData);
        foreach (var credit in DataSources)
            CreditRow(credit);

        SectionTitle(Lang.CreditsCommunity);
        var width = ImGui.GetContentRegionAvail().X - 8f;
        using (ImRaii.TextWrapPos(ImGui.GetCursorPosX() + width))
            ImGui.TextColored(UIConstants.TextSecondary, Lang.CreditsCommunityText);
        ImGui.Dummy(new Vector2(0, 2f));
        if (UIConstants.AccentButton($"{Lang.Website}##unSite", tooltip: SiteLinks.Host))
            UIConstants.OpenUrl(SiteLinks.Home);
        ImGui.SameLine(0, 6f);
        if (UIConstants.AccentButton($"{Lang.DiscordServer}##unDiscord"))
            UIConstants.OpenUrl(DiscordUrl);
        ImGui.SameLine(0, 6f);
        if (UIConstants.AccentButton($"{Lang.SupportKofi}##unKofi", UIConstants.Rose))
            UIConstants.OpenUrl(KofiUrl);
        ImGui.Dummy(new Vector2(0, 6f));
    }

    private static void CreditRow(Credit credit)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X - 8f;
        var lineH = ImGui.GetTextLineHeight();
        var height = lineH + 12f;
        var end = start + new Vector2(width, height);

        var clicked = ImGui.InvisibleButton($"##unCredit{credit.Name}", new Vector2(width, height));
        var hovered = credit.Url != null && ImGui.IsItemHovered();
        if (hovered)
            dl.AddRectFilled(start, end, U32(UIConstants.Hover), 3f);

        var y = start.Y + 6f;
        dl.AddText(new Vector2(start.X + 8f, y), U32(hovered ? UIConstants.Accent : UIConstants.TextPrimary), credit.Name);
        var x = start.X + 8f + ImGui.CalcTextSize(credit.Name).X + 6f;
        if (credit.Author != null)
        {
            dl.AddText(new Vector2(x, y), U32(UIConstants.TextMuted), credit.Author);
            x += ImGui.CalcTextSize(credit.Author).X + 6f;
        }
        var role = credit.Role();
        dl.AddText(new Vector2(end.X - 30f - ImGui.CalcTextSize(role).X, y), U32(UIConstants.TextSecondary), role);

        if (credit.Url == null) return;

        var link = FontAwesomeIcon.ExternalLinkAlt.ToIconString();
        var iconFont = UiBuilder.IconFont;
        var iconSize = iconFont.FontSize * 0.75f;
        dl.AddText(iconFont, iconSize, new Vector2(end.X - 10f - iconSize, start.Y + (height - iconSize) / 2f),
            U32(hovered ? UIConstants.Accent : UIConstants.TextMuted), link);

        if (hovered)
            ImGui.SetTooltip(credit.Url);
        if (clicked)
            UIConstants.OpenUrl(credit.Url);
    }

    private void DrawFooter()
    {
        var dl = ImGui.GetWindowDrawList();
        var winPos = ImGui.GetWindowPos();
        var winW = ImGui.GetWindowSize().X;
        var y = ImGui.GetCursorScreenPos().Y + 2f;
        dl.AddLine(new Vector2(winPos.X + PadX, y), new Vector2(winPos.X + winW - PadX, y), U32(UIConstants.Line), 1f);

        const float buttonW = 180f;
        ImGui.SetCursorScreenPos(new Vector2(winPos.X + (winW - buttonW) / 2f, y + 9f));
        if (UIConstants.AccentButton($"{Lang.WhatsNewGotIt}##unGotIt", width: buttonW))
            IsOpen = false;
    }

    public void Dispose() => titleFont.Dispose();
}
