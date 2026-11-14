using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public static class PassportView
{
    private const float StampGap = 8f;
    private const float StampMax = 92f;
    private const float StampMin = 52f;
    private const float TopArc = 3.6f;
    private const float BottomArc = 2.6f;
    private static readonly float[] ArcScales = [0.72f, 0.66f, 0.6f, 0.55f];

    public static string BadgeName(PassportBadge badge) => badge switch
    {
        PassportBadge.First => Lang.BadgeFirst,
        PassportBadge.Five => Lang.BadgeFive,
        PassportBadge.Ten => Lang.BadgeTen,
        PassportBadge.Regular => Lang.BadgeRegular,
        PassportBadge.Districts => Lang.BadgeDistricts,
        PassportBadge.Night => Lang.BadgeNight,
        PassportBadge.Traveler => Lang.BadgeTraveler,
        _ => Lang.BadgeComplete,
    };

    public static string BadgeDescription(PassportBadge badge) => badge switch
    {
        PassportBadge.First => Lang.BadgeFirstDesc,
        PassportBadge.Five => Lang.BadgeFiveDesc,
        PassportBadge.Ten => Lang.BadgeTenDesc,
        PassportBadge.Regular => Lang.BadgeRegularDesc,
        PassportBadge.Districts => Lang.BadgeDistrictsDesc,
        PassportBadge.Night => Lang.BadgeNightDesc,
        PassportBadge.Traveler => Lang.BadgeTravelerDesc,
        _ => Lang.BadgeCompleteDesc,
    };

    private static FontAwesomeIcon BadgeIcon(PassportBadge badge) => badge switch
    {
        PassportBadge.First => FontAwesomeIcon.Stamp,
        PassportBadge.Five => FontAwesomeIcon.MapMarkedAlt,
        PassportBadge.Ten => FontAwesomeIcon.Compass,
        PassportBadge.Regular => FontAwesomeIcon.GlassCheers,
        PassportBadge.Districts => FontAwesomeIcon.Map,
        PassportBadge.Night => FontAwesomeIcon.Moon,
        PassportBadge.Traveler => FontAwesomeIcon.Globe,
        _ => FontAwesomeIcon.Crown,
    };

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    private readonly record struct Entry(string Id, Venue? Venue, PassportStamp? Stamp);

    public static void Draw(VenueMapperPlugin plugin, float reservedBelow)
    {
        var cfg = plugin.Configuration;
        var venues = plugin.ConfigManager.Config?.Venues ?? new List<Venue>();
        var top = ImGui.GetCursorScreenPos().Y;
        var availY = ImGui.GetContentRegionAvail().Y;

        DrawHeader(cfg);

        var contentId = VenueMapperPlugin.PlayerState.ContentId;
        if (contentId == 0)
        {
            ImGui.TextColored(UIConstants.TextSecondary, Lang.PassportNotLoggedIn);
            return;
        }

        var book = plugin.DemoPassport ?? plugin.Passport.Find(contentId);
        var visited = book != null ? PassportManager.Visited(book, venues) : 0;
        var total = PassportManager.Total(venues);
        var character = plugin.PassportCharacter();
        if (character.Length == 0) character = book?.Character ?? string.Empty;
        if (plugin.ScreenshotMode) character = string.Empty;

        var progress = Lang.PassportProgress(visited, total);
        var progressW = ImGui.CalcTextSize(progress).X;
        ImGui.TextColored(UIConstants.TextMuted, book == null || book.Stamps.Count == 0 ? Lang.PassportEmpty : character);
        ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - progressW));
        ImGui.TextColored(UIConstants.GoldAccent, progress);
        DrawProgressBar(total > 0 ? visited / (float)total : 0f);

        var entries = Entries(book, venues);
        var badgesH = BadgeLayout(ImGui.GetContentRegionAvail().X).Height + 6f;
        var used = ImGui.GetCursorScreenPos().Y - top;
        var budget = availY - used - badgesH - reservedBelow;
        DrawStamps(entries, budget, plugin);
        ImGui.Dummy(new Vector2(1f, 6f));
        DrawBadges(book);
    }

    private static void DrawHeader(Configuration cfg)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UIConstants.Heading, Lang.Passport.ToUpperInvariant());
        var label = Lang.PassportCollect;
        var frameH = ImGui.GetFrameHeight();
        var toggleW = frameH * 0.78f * 1.8f;
        var labelW = ImGui.CalcTextSize(label).X;
        ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - toggleW - labelW - 8f));
        ImGui.TextColored(UIConstants.TextSecondary, label);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Lang.PassportCollectTip);
        ImGui.SameLine(0, 8f);
        var enabled = cfg.PassportEnabled;
        if (UIConstants.Toggle("##passportCollect", ref enabled))
        {
            cfg.PassportEnabled = enabled;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Lang.PassportCollectTip);
    }

    private static void DrawProgressBar(float fraction)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos() + new Vector2(0, 1f);
        var width = ImGui.GetContentRegionAvail().X;
        var height = 3f * ImGuiHelpers.GlobalScale;
        dl.AddRectFilled(min, min + new Vector2(width, height), U32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.15f)), height);
        if (fraction > 0f)
            dl.AddRectFilledMultiColor(min, min + new Vector2(width * Math.Clamp(fraction, 0f, 1f), height),
                U32(UIConstants.WithAlpha(UIConstants.GoldAccent, 0.6f)), U32(UIConstants.GoldAccent),
                U32(UIConstants.GoldAccent), U32(UIConstants.WithAlpha(UIConstants.GoldAccent, 0.6f)));
        ImGui.Dummy(new Vector2(width, height + 6f));
    }

    private static List<Entry> Entries(PassportBook? book, IReadOnlyList<Venue> venues)
    {
        var stamps = book?.Stamps ?? new Dictionary<string, PassportStamp>();
        var known = venues.Select(v => v.VenueId).ToHashSet();
        var visited = venues.Where(v => stamps.ContainsKey(v.VenueId))
            .Select(v => new Entry(v.VenueId, v, stamps[v.VenueId]))
            .OrderBy(e => e.Venue!.Closed)
            .ThenBy(e => e.Stamp!.FirstVisit);
        var removed = stamps.Where(kv => !known.Contains(kv.Key))
            .Select(kv => new Entry(kv.Key, null, kv.Value))
            .OrderBy(e => e.Stamp!.FirstVisit);
        var open = venues.Where(v => !v.Closed && !stamps.ContainsKey(v.VenueId))
            .OrderBy(v => UIConstants.DisplayName(v.Name), StringComparer.OrdinalIgnoreCase)
            .Select(v => new Entry(v.VenueId, v, null));
        return visited.Concat(removed).Concat(open).ToList();
    }

    private static float CellSize(int count, float avail, float budget, float gap, float scale)
    {
        for (var cell = StampMax * scale; cell >= StampMin * scale; cell -= 2f)
        {
            var columns = Math.Max(1, (int)((avail + gap) / (cell + gap)));
            var rows = (count + columns - 1) / columns;
            if (rows * (cell + gap) - gap <= budget) return cell;
        }
        return StampMin * scale;
    }

    private static void DrawStamps(List<Entry> entries, float budget, VenueMapperPlugin plugin)
    {
        if (entries.Count == 0) return;
        var scale = ImGuiHelpers.GlobalScale;
        var gap = StampGap * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var cell = CellSize(entries.Count, avail, budget, gap, scale);
        var columns = Math.Max(1, (int)((avail + gap) / (cell + gap)));
        var spread = columns > 1 ? (avail - columns * cell) / (columns - 1) : 0f;
        var start = ImGui.GetCursorScreenPos();
        var time = (float)ImGui.GetTime();

        for (var i = 0; i < entries.Count; i++)
        {
            var pos = start + new Vector2(i % columns * (cell + spread), i / columns * (cell + gap));
            ImGui.SetCursorScreenPos(pos);
            using var id = ImRaii.PushId(entries[i].Id);
            var clicked = ImGui.InvisibleButton("##stamp", new Vector2(cell));
            var hovered = ImGui.IsItemHovered();
            DrawStamp(entries[i], pos + new Vector2(cell / 2f), cell / 2f - 1f, hovered, time);
            if (hovered)
            {
                if (entries[i].Venue != null) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                DrawStampTooltip(entries[i]);
            }
            if (clicked && entries[i].Venue is { } venue)
                plugin.VenueDetailWindow.Open(venue);
        }

        var rows = (entries.Count + columns - 1) / columns;
        ImGui.SetCursorScreenPos(start + new Vector2(0, rows * (cell + gap) - gap));
        ImGui.Dummy(new Vector2(avail, 1f));
    }

    private static void DrawStamp(Entry entry, Vector2 center, float radius, bool hovered, float time)
    {
        var dl = ImGui.GetWindowDrawList();
        var stamp = entry.Stamp;
        var phase = (entry.Id.GetHashCode() & 0xFFFF) / 65535f * MathF.Tau;

        if (stamp == null)
        {
            var ring = hovered ? UIConstants.Accent : UIConstants.WithAlpha(UIConstants.TextMuted, 0.8f);
            if (hovered)
                dl.AddCircleFilled(center, radius, U32(UIConstants.WithAlpha(UIConstants.Accent, 0.08f)), 48);
            DashedRing(dl, center, radius - 1.5f, 24, phase, ring, 1.5f);
            var markSize = ImGui.CalcTextSize("?") * 1.4f;
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 1.4f, center - markSize / 2f, U32(ring), "?");
            return;
        }

        var faded = entry.Venue == null || entry.Venue.Closed;
        var alpha = faded ? 0.5f : 1f;
        var primary = UIConstants.HexToVec4(stamp.Color.Length > 0 ? stamp.Color : "#E2C27E");
        var secondary = stamp.Color2.Length > 0 ? UIConstants.HexToVec4(stamp.Color2) : primary;
        var ink = UIConstants.WithAlpha(UIConstants.Readable(primary), alpha);
        var ink2 = UIConstants.WithAlpha(UIConstants.Readable(secondary), alpha);
        var name = UIConstants.DisplayName(stamp.Name);

        var (topText, bottomText, scale) = ArcLayout(name, radius);
        var bandH = ImGui.GetTextLineHeight() * scale;
        var outer = radius - 1.2f;
        var textRadius = outer - 2.5f - bandH / 2f;
        var inner = textRadius - bandH / 2f - 2.5f;

        dl.AddCircleFilled(center, radius, U32(UIConstants.WithAlpha(primary, (hovered ? 0.22f : 0.12f) * alpha)), 48);
        dl.AddCircle(center, outer, U32(ink), 48, 2.2f);
        DashedRing(dl, center, inner, 30, phase + (hovered ? time * 0.6f : 0f), UIConstants.WithAlpha(ink2, 0.75f * alpha), 1.2f);

        var fontSize = ImGui.GetFontSize() * scale;
        ArcText(dl, topText, center, textRadius, fontSize, scale, ink, true);
        var bottom = bottomText ?? stamp.FirstVisit.ToString("dd.MM.yy");
        var bottomScale = bottomText != null ? scale : scale * 0.9f;
        ArcText(dl, bottom, center, textRadius, ImGui.GetFontSize() * bottomScale, bottomScale,
            bottomText != null ? ink : UIConstants.WithAlpha(UIConstants.TextSecondary, alpha), false);
        if (bottomText == null)
        {
            DrawStar(dl, center, textRadius, -MathF.PI + 0.5f, scale, ink2);
            DrawStar(dl, center, textRadius, -0.5f, scale, ink2);
        }

        var initials = Initials(name);
        var initialsScale = Math.Clamp(inner / ImGui.GetTextLineHeight() * 0.95f, 0.8f, 1.7f);
        var initialsSize = ImGui.CalcTextSize(initials) * initialsScale;
        var visitText = stamp.Visits > 1 ? $"x{(stamp.Visits > 99 ? "99+" : stamp.Visits.ToString())}" : null;
        var visitScale = 0.7f;
        var visitSize = visitText != null ? ImGui.CalcTextSize(visitText) * visitScale : Vector2.Zero;
        var blockH = initialsSize.Y + (visitText != null ? visitSize.Y - 2f : 0f);
        var y = center.Y - blockH / 2f;
        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * initialsScale, new Vector2(center.X - initialsSize.X / 2f, y), U32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), initials);
        if (visitText != null)
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * visitScale, new Vector2(center.X - visitSize.X / 2f, y + initialsSize.Y - 2f),
                U32(UIConstants.WithAlpha(UIConstants.GoldAccent, alpha)), visitText);
    }

    public static string Initials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 0 && char.IsLetterOrDigit(w[0]))
            .Where(w => !string.Equals(w, "the", StringComparison.OrdinalIgnoreCase) && !string.Equals(w, "of", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (words.Count >= 2) return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
        var single = words.Count == 1 ? words[0] : name;
        var caps = single.Where(char.IsUpper).Take(2).ToArray();
        if (caps.Length == 2) return new string(caps);
        var letters = single.Where(char.IsLetterOrDigit).Take(2).ToArray();
        return letters.Length == 0 ? "?" : char.ToUpperInvariant(letters[0]) + (letters.Length > 1 ? char.ToLowerInvariant(letters[1]).ToString() : "");
    }

    private static float Width(string text, float scale) => ImGui.CalcTextSize(text).X * scale;

    private static (string Top, string? Bottom, float Scale) ArcLayout(string name, float radius)
    {
        var upper = name.ToUpperInvariant();
        foreach (var scale in ArcScales)
        {
            var textRadius = TextRadius(radius, scale);
            if (Width(upper, scale) <= textRadius * TopArc) return (upper, null, scale);
        }

        var words = upper.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            foreach (var scale in ArcScales)
            {
                var textRadius = TextRadius(radius, scale);
                for (var split = words.Length - 1; split >= 1; split--)
                {
                    var top = string.Join(' ', words.Take(split));
                    var bottom = string.Join(' ', words.Skip(split));
                    if (Width(top, scale) <= textRadius * TopArc && Width(bottom, scale) <= textRadius * BottomArc)
                        return (top, bottom, scale);
                }
            }
        }

        var minScale = ArcScales[^1];
        var limit = TextRadius(radius, minScale) * TopArc;
        var cut = upper;
        while (cut.Length > 1 && Width(cut + "...", minScale) > limit)
            cut = cut[..^1];
        return (cut.TrimEnd() + "...", null, minScale);
    }

    private static float TextRadius(float radius, float scale) => radius - 1.2f - 2.5f - ImGui.GetTextLineHeight() * scale / 2f;

    private static void ArcText(ImDrawListPtr dl, string text, Vector2 center, float radius, float fontSize, float scale, Vector4 color, bool top)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = ImGui.GetFont();
        var col = U32(color);
        var widths = text.Select(c => Width(c.ToString(), scale)).ToArray();
        var total = widths.Sum();
        var span = total / radius;
        var angle = top ? -MathF.PI / 2f - span / 2f : MathF.PI / 2f + span / 2f;
        var lineH = ImGui.GetTextLineHeight() * scale;

        for (var i = 0; i < text.Length; i++)
        {
            var step = widths[i] / radius;
            var mid = top ? angle + step / 2f : angle - step / 2f;
            angle = top ? angle + step : angle - step;
            if (char.IsWhiteSpace(text[i])) continue;

            var point = center + new Vector2(MathF.Cos(mid), MathF.Sin(mid)) * radius;
            var rotation = top ? mid + MathF.PI / 2f : mid - MathF.PI / 2f;
            var first = dl.VtxBuffer.Size;
            dl.AddText(font, fontSize, point - new Vector2(widths[i] / 2f, lineH / 2f), col, text[i].ToString());
            Rotate(dl, first, point, rotation);
        }
    }

    private static void Rotate(ImDrawListPtr dl, int first, Vector2 pivot, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var end = dl.VtxBuffer.Size;
        for (var i = first; i < end; i++)
        {
            ref var vertex = ref dl.VtxBuffer.Ref(i);
            var d = vertex.Pos - pivot;
            vertex.Pos = pivot + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
        }
    }

    private static void DrawStar(ImDrawListPtr dl, Vector2 center, float radius, float angle, float scale, Vector4 color)
    {
        var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        var r = ImGui.GetTextLineHeight() * scale * 0.22f;
        dl.AddCircleFilled(point, r, U32(color), 8);
    }

    private static void DashedRing(ImDrawListPtr dl, Vector2 center, float radius, int dashes, float phase, Vector4 color, float thickness)
    {
        var step = MathF.Tau / dashes;
        var col = U32(color);
        for (var i = 0; i < dashes; i++)
        {
            var a = phase + i * step;
            dl.PathArcTo(center, radius, a, a + step * 0.55f, 6);
            dl.PathStroke(col, ImDrawFlags.None, thickness);
        }
    }

    private static void DrawStampTooltip(Entry entry)
    {
        using var tooltip = ImRaii.Tooltip();
        var name = UIConstants.DisplayName(entry.Venue?.Name ?? entry.Stamp?.Name ?? entry.Id);
        var color = entry.Stamp is { Color.Length: > 0 } s ? UIConstants.HexToVec4(s.Color)
                  : entry.Venue?.Colors?.PrimaryVec ?? UIConstants.TextPrimary;
        ImGui.TextColored(UIConstants.Readable(color), name);
        if (entry.Venue != null)
            ImGui.TextColored(UIConstants.TextSecondary, $"{entry.Venue.Server} · {PassportManager.District(entry.Venue)} W{entry.Venue.Ward} P{entry.Venue.Plot}");

        if (entry.Stamp is not { } stamp)
        {
            ImGui.TextColored(UIConstants.TextMuted, Lang.PassportNotVisited);
        }
        else
        {
            ImGui.TextColored(UIConstants.GoldAccent, Lang.PassportVisits(stamp.Visits));
            ImGui.TextColored(UIConstants.TextPrimary, Lang.PassportFirstVisit(stamp.FirstVisit.ToString("yyyy-MM-dd")));
            if (stamp.Visits > 1)
                ImGui.TextColored(UIConstants.TextPrimary, Lang.PassportLastVisit(stamp.LastVisit.ToString("yyyy-MM-dd")));
            if (entry.Venue == null)
                ImGui.TextColored(UIConstants.TextMuted, Lang.PassportRemoved);
            else if (entry.Venue.Closed)
                ImGui.TextColored(UIConstants.TextMuted, Lang.PassportClosed);
        }
        if (entry.Venue != null)
            ImGui.TextColored(UIConstants.Accent, Lang.PassportClickDetails);
    }

    private readonly record struct BadgeSlot(PassportBadge Badge, Vector2 Offset, float Width);

    private static (List<BadgeSlot> Slots, float Height) BadgeLayout(float avail)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = ImGui.GetFrameHeight() - 2f;
        var gap = 6f * scale;
        var pad = 9f * scale;
        var iconW = IconWidth();
        var slots = new List<BadgeSlot>();
        var x = 0f;
        var y = 0f;
        foreach (var badge in PassportManager.AllBadges)
        {
            var width = pad * 2f + iconW + 5f * scale + ImGui.CalcTextSize(BadgeName(badge)).X;
            if (x > 0f && x + width > avail)
            {
                x = 0f;
                y += height + gap;
            }
            slots.Add(new BadgeSlot(badge, new Vector2(x, y), width));
            x += width + gap;
        }
        return (slots, y + height);
    }

    private static float IconWidth()
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
            return ImGui.CalcTextSize(FontAwesomeIcon.Globe.ToIconString()).X * 0.78f;
    }

    private static void DrawBadges(PassportBook? book)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var (slots, totalH) = BadgeLayout(ImGui.GetContentRegionAvail().X);
        var height = ImGui.GetFrameHeight() - 2f;
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 9f * scale;
        var iconScale = 0.78f;

        foreach (var slot in slots)
        {
            var earned = book?.Badges.Contains(slot.Badge) == true;
            var min = start + slot.Offset;
            var max = min + new Vector2(slot.Width, height);
            ImGui.SetCursorScreenPos(min);
            ImGui.InvisibleButton($"##badge_{slot.Badge}", max - min);
            var hovered = ImGui.IsItemHovered();

            var tone = earned ? UIConstants.GoldAccent : UIConstants.TextMuted;
            if (earned)
                dl.AddRectFilled(min, max, U32(UIConstants.WithAlpha(UIConstants.GoldAccent, hovered ? 0.22f : 0.13f)), height / 2f);
            else if (hovered)
                dl.AddRectFilled(min, max, U32(UIConstants.Hover), height / 2f);
            dl.AddRect(min, max, U32(UIConstants.WithAlpha(tone, earned ? 0.45f : 0.5f)), height / 2f, ImDrawFlags.None, 1f);

            var glyph = BadgeIcon(slot.Badge).ToIconString();
            Vector2 glyphSize;
            using (ImRaii.PushFont(UiBuilder.IconFont))
                glyphSize = ImGui.CalcTextSize(glyph) * iconScale;
            var iconPos = new Vector2(min.X + pad, min.Y + (height - glyphSize.Y) / 2f);
            dl.AddText(UiBuilder.IconFont, UiBuilder.IconFont.FontSize * iconScale, iconPos, U32(tone), glyph);
            var label = BadgeName(slot.Badge);
            dl.AddText(new Vector2(iconPos.X + IconWidth() + 5f * scale, min.Y + (height - ImGui.GetTextLineHeight()) / 2f),
                U32(earned ? UIConstants.GoldAccent : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.7f)), label);

            if (hovered)
            {
                using var tooltip = ImRaii.Tooltip();
                ImGui.TextColored(UIConstants.TextSecondary, BadgeDescription(slot.Badge));
                ImGui.TextColored(earned ? UIConstants.Success : UIConstants.TextMuted, earned ? Lang.PassportBadgeEarnedShort : Lang.PassportBadgeLocked);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + totalH));
        ImGui.Dummy(new Vector2(1f, 2f));
    }
}
