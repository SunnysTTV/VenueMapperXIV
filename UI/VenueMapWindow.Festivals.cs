using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public partial class VenueMapWindow
{
    private readonly HashSet<string> expandedFestivals = new();

    private void DrawFestivalsTab(VenueConfig config)
    {
        var now = DateTime.UtcNow;
        var festivals = config.Festivals
            .Where(f => !f.IsOver(now))
            .OrderBy(f => f.IsLive(now) ? 0 : 1)
            .ThenBy(f => f.Start)
            .ToList();

        DrawFestivalsHeader(festivals.Count);
        ImGui.Spacing();

        using var scrollbar = UIConstants.ScrollbarStyle();
        using var child = ImRaii.Child("##festivalScroll", new Vector2(-1, -1));
        if (!child) return;

        if (festivals.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(UIConstants.TextMuted, Lang.NoFestivals);
            return;
        }

        var here = plugin.PositionTracker.GetFestivalHere(config);
        for (var i = 0; i < festivals.Count; i++)
            DrawFestivalRow(festivals[i], i == 0, festivals[i] == here, now);
    }

    private void DrawFestivalsHeader(int count)
    {
        var dl = ImGui.GetWindowDrawList();
        ImGui.TextColored(UIConstants.Heading, Lang.FestivalsHeader);
        ImGui.SameLine(0, 0);
        ImGui.TextColored(UIConstants.TextMuted, Lang.FestivalCount(count));

        var btnW = ImGui.CalcTextSize(Lang.SubmitFestival).X + 28f;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX() + 8f, ImGui.GetContentRegionMax().X - btnW));
        if (UIConstants.AccentButton($"{Lang.SubmitFestival}##submitFestival", UIConstants.Accent, btnW))
            plugin.FestivalSubmitWindow.IsOpen = true;

        var y = ImGui.GetCursorScreenPos().Y;
        var x0 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMin().X;
        var x1 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), U32(UIConstants.Line), 1f);
        ImGui.Dummy(new Vector2(0, 2));
    }

    private void DrawFestivalRow(Festival festival, bool first, bool isHere, DateTime now)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var avW = ImGui.GetContentRegionAvail().X;
        var live = festival.IsLive(now);
        var expanded = expandedFestivals.Contains(festival.FestivalId);

        var rowH = RowPadY * 2 + lineH * 3 + RowLineGap * 2;
        var tpSize = TeleportButtonSize(live, lineH);
        var rowMin = ImGui.GetCursorScreenPos();
        var rowMax = rowMin + new Vector2(avW, rowH);
        var hitW = Math.Max(avW - tpSize.X - 10f, 60f);

        ImGui.InvisibleButton($"##festival_{festival.FestivalId}", new Vector2(hitW, rowH));
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();
        var rowHot = hovered || (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(rowMin, rowMax));
        var (accent, secondary) = FestivalPalette(festival);

        if (!first)
            dl.AddLine(rowMin, new Vector2(rowMax.X, rowMin.Y), U32(UIConstants.Line));
        var tint = rowHot || isHere || expanded ? 0.13f : 0.06f;
        dl.AddRectFilledMultiColor(rowMin, rowMax, U32(UIConstants.WithAlpha(accent, tint)), U32(UIConstants.WithAlpha(accent, 0f)),
            U32(UIConstants.WithAlpha(accent, 0f)), U32(UIConstants.WithAlpha(accent, tint)));
        if (rowHot)
            dl.AddRectFilled(rowMin, rowMax, U32(UIConstants.WithAlpha(UIConstants.Hover, 0.35f)));
        dl.AddRectFilledMultiColor(new Vector2(rowMin.X, rowMin.Y + 6f), new Vector2(rowMin.X + 3f, rowMax.Y - 6f),
            U32(accent), U32(accent), U32(secondary), U32(secondary));

        var tileW = lineH * 2.6f;
        var tileMin = new Vector2(rowMin.X + RowPadX, rowMin.Y + RowPadY);
        var tileMax = new Vector2(tileMin.X + tileW, rowMax.Y - RowPadY);
        DrawFestivalDateTile(dl, festival, tileMin, tileMax, accent, live, now);

        DrawFestivalRowText(dl, festival, rowMin, rowMax, tileMax.X + 12f, rowMin.X + hitW - 4f, lineH, accent, isHere, live, now);

        ImGui.SetCursorScreenPos(new Vector2(rowMax.X - tpSize.X, rowMin.Y + (rowH - tpSize.Y) / 2f));
        var (pressed, tpHovered) = TeleportGlyphButton($"##ftp_{festival.FestivalId}", dl, tpSize, live, true, false);
        if (pressed)
            plugin.TeleportToFestival(festival);
        if (tpHovered)
        {
            using var tooltip = ImRaii.Tooltip();
            ImGui.TextColored(UIConstants.Glow, Lang.TeleportVia);
            ImGui.TextColored(UIConstants.TextSecondary, FestivalLocationText(festival));
        }

        if (clicked && !expandedFestivals.Remove(festival.FestivalId))
            expandedFestivals.Add(festival.FestivalId);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##fctx_{festival.FestivalId}");
        DrawFestivalContextMenu(festival);

        if (hovered)
            ImGui.SetTooltip(Lang.RightClickHint);

        ImGui.SetCursorScreenPos(new Vector2(rowMin.X, rowMax.Y));
        if (expanded)
            DrawFestivalDetails(festival, accent, now);
    }

    private static (Vector4 Accent, Vector4 Secondary) FestivalPalette(Festival festival)
    {
        if (UIConstants.OverrideMode != ColorOverrideMode.None || festival.Colors == null)
            return (UIConstants.Accent, UIConstants.Accent);
        var primary = UIConstants.Readable(festival.Colors.PrimaryVec);
        var secondary = festival.Colors.Secondary.Equals(festival.Colors.Primary, StringComparison.OrdinalIgnoreCase)
            ? primary
            : UIConstants.Readable(festival.Colors.SecondaryVec);
        return (primary, secondary);
    }

    private static void DrawFestivalDateTile(ImDrawListPtr dl, Festival festival, Vector2 min, Vector2 max, Vector4 accent, bool live, DateTime now)
    {
        var day = festival.CurrentOrNextDay(now) ?? festival.Schedule[^1];
        var date = day.Start;
        dl.AddRectFilled(min, max, U32(UIConstants.WithAlpha(UIConstants.Background, 0.7f)), 4f);
        dl.AddRectFilled(min, max, U32(UIConstants.WithAlpha(accent, 0.12f)), 4f);
        var border = live ? UIConstants.Success : accent;
        var pulse = live ? 0.55f + 0.45f * (MathF.Sin((float)ImGui.GetTime() * 3f) + 1f) / 2f : 0.55f;
        dl.AddRect(min, max, U32(UIConstants.WithAlpha(border, pulse)), 4f, ImDrawFlags.None, live ? 1.6f : 1f);

        var font = ImGui.GetFont();
        var baseSize = ImGui.GetFontSize();
        var top = live ? Lang.FestivalLive.ToUpperInvariant() : date.ToString("MMM", Lang.Culture).ToUpperInvariant().TrimEnd('.');
        var topSize = ImGui.CalcTextSize(top) * 0.78f;
        var number = date.Day.ToString();
        var numberSize = ImGui.CalcTextSize(number) * 1.45f;
        var height = topSize.Y + numberSize.Y - 2f;
        var y = min.Y + (max.Y - min.Y - height) / 2f;
        var cx = (min.X + max.X) / 2f;
        dl.AddText(font, baseSize * 0.78f, new Vector2(cx - topSize.X / 2f, y), U32(live ? UIConstants.Success : accent), top);
        dl.AddText(font, baseSize * 1.45f, new Vector2(cx - numberSize.X / 2f, y + topSize.Y - 2f), U32(UIConstants.TextPrimary), number);
    }

    private void DrawFestivalRowText(ImDrawListPtr dl, Festival festival, Vector2 rowMin, Vector2 rowMax, float x, float textRight,
        float lineH, Vector4 accent, bool isHere, bool live, DateTime now)
    {
        var y = rowMin.Y + RowPadY;
        dl.PushClipRect(new Vector2(x - 2f, rowMin.Y), new Vector2(textRight, rowMax.Y), true);

        var festivalName = UIConstants.DisplayName(festival.Name);
        dl.AddText(new Vector2(x, y), U32(UIConstants.TextPrimary), festivalName);
        var cx = x + ImGui.CalcTextSize(festivalName).X + 8f;
        if (isHere)
            cx = DrawPill(dl, cx, y, Lang.Here2.Trim(), UIConstants.Success) + 6f;
        if (festival.IsLocalPreview)
            cx = DrawPill(dl, cx, y, Lang.FestivalPreviewBadge, UIConstants.Warning) + 6f;
        if (festival.MultiDay)
            cx = DrawPill(dl, cx, y, Lang.FestivalDays(festival.Schedule.Count), accent) + 6f;
        if (festival.Nsfw)
            DrawPill(dl, cx, y, Lang.NsfwBadge, UIConstants.Rose);

        var line2 = y + lineH + RowLineGap;
        var current = festival.CurrentDay(now);
        if (live && current != null)
        {
            var pulse = (MathF.Sin((float)ImGui.GetTime() * 3f) + 1f) / 2f;
            dl.AddCircleFilled(new Vector2(x + 3f, line2 + lineH / 2f), 3f, U32(UIConstants.WithAlpha(UIConstants.Success, 0.35f + 0.65f * pulse)));
            var text = $"{Lang.FestivalLive} · {Lang.FestivalUntil(UIConstants.FormatServerTime(current.End))}";
            if (festival.MultiDay)
                text += $" · {Lang.FestivalDayOf(festival.DayNumber(current), festival.Schedule.Count)}";
            dl.AddText(new Vector2(x + 11f, line2), U32(UIConstants.Success), text);
        }
        else
        {
            dl.AddText(new Vector2(x, line2), U32(UIConstants.TextSecondary), FestivalWhenText(festival, now));
        }

        var line3 = line2 + lineH + RowLineGap;
        var meta = new List<string> { FestivalLocationText(festival) };
        if (!string.IsNullOrWhiteSpace(festival.Organizer)) meta.Add(Lang.FestivalBy(festival.Organizer));
        var metaText = string.Join(" · ", meta);
        dl.AddText(new Vector2(x, line3), U32(UIConstants.TextMuted), metaText);

        var iconX = x + ImGui.CalcTextSize(metaText).X + 10f;
        var iconScale = 0.8f;
        foreach (var type in festival.Services.Select(s => s.Type).Distinct().Take(6))
        {
            var glyph = ServiceIcons.GetIcon(type).ToIconString();
            var size = CalcIconSize(glyph) * iconScale;
            if (iconX + size.X > textRight) break;
            dl.AddText(UiBuilder.IconFont, UiBuilder.IconFont.FontSize * iconScale, new Vector2(iconX, line3 + (lineH - size.Y) / 2f),
                U32(UIConstants.WithAlpha(PictomancyMarkerManager.TypeColor(type), 0.9f)), glyph);
            iconX += size.X + 6f;
        }

        dl.PopClipRect();
    }

    private static string FestivalWhenText(Festival festival, DateTime now)
    {
        var day = festival.CurrentOrNextDay(now) ?? festival.Schedule[^1];
        var text = $"{day.Start.ToString("ddd, MMM d", Lang.Culture)} · {UIConstants.FormatServerTime(day.Start)} - {UIConstants.FormatServerTime(day.End)}";
        if (!festival.MultiDay) return text;
        var last = festival.Schedule[^1];
        return $"{text} · {Lang.FestivalDayOf(festival.DayNumber(day), festival.Schedule.Count)} ({Lang.FestivalUntilDate(last.End.ToString("ddd, MMM d", Lang.Culture))})";
    }

    private string FestivalLocationText(Festival festival)
    {
        var location = festival.Location;
        string place;
        if (festival.IsWard)
        {
            place = $"{HousingData.EnglishName(HousingData.ParseDistrict(location.District))} · {Lang.WardLabel} {location.Ward}";
            if (location.Houses.Count == 1)
                place += $" · {Lang.PlotLabel(location.Houses[0].Plot)}";
            else if (location.Houses.Count > 1)
                place += $" · {Lang.FestivalHouses(location.Houses.Count)}";
        }
        else
        {
            var zone = plugin.HousingData.TerritoryName(location.TerritoryId);
            place = zone.Length > 0 ? zone : location.PlaceName;
        }
        return string.IsNullOrEmpty(festival.Server) ? place : $"{festival.Server} · {place}";
    }

    private void DrawFestivalDetails(Festival festival, Vector4 accent, DateTime now)
    {
        using var indent = ImRaii.PushIndent(RowPadX + 4f, scaled: false);
        ImGui.Spacing();
        if (!string.IsNullOrWhiteSpace(festival.Description))
        {
            using var wrap = ImRaii.TextWrapPos(0);
            ImGui.TextColored(UIConstants.TextSecondary, festival.Description);
            ImGui.Spacing();
        }

        if (festival.MultiDay)
        {
            ImGui.TextColored(UIConstants.Heading, Lang.FestivalSchedule.ToUpperInvariant());
            var schedule = festival.Schedule;
            for (var i = 0; i < schedule.Count; i++)
            {
                var day = schedule[i];
                var state = day.End <= now ? UIConstants.TextMuted
                    : day.Start <= now ? UIConstants.Success
                    : UIConstants.TextPrimary;
                ImGui.TextColored(UIConstants.WithAlpha(accent, day.End <= now ? 0.5f : 1f), Lang.FestivalDayOf(i + 1, schedule.Count));
                ImGui.SameLine(0, 8f);
                ImGui.TextColored(state, $"{day.Start.ToString("ddd, MMM d", Lang.Culture)} · {UIConstants.FormatServerTime(day.Start)} - {UIConstants.FormatServerTime(day.End)}");
            }
            ImGui.Spacing();
        }

        if (festival.IsWard && festival.Location.Houses.Count > 0)
        {
            ImGui.TextColored(UIConstants.Heading, Lang.FestivalHousesTitle.ToUpperInvariant());
            foreach (var house in festival.Location.Houses)
            {
                ImGui.TextColored(accent, Lang.PlotLabel(house.Plot));
                if (string.IsNullOrWhiteSpace(house.Label)) continue;
                ImGui.SameLine(0, 8f);
                ImGui.TextColored(UIConstants.TextPrimary, house.Label);
            }
            ImGui.Spacing();
        }

        if (festival.Services.Count > 0)
        {
            ImGui.TextColored(UIConstants.Heading, Lang.FestivalStallsTitle.ToUpperInvariant());
            var avail = ImGui.GetContentRegionAvail().X;
            var x = 0f;
            foreach (var group in festival.Services.GroupBy(s => s.Type))
            {
                var label = VenueMapWindow.ChipLabel(group.Key);
                var count = group.Count();
                var width = StallChipWidth(group.Key, label, count);
                if (x > 0f && x + width > avail) x = 0f;
                else if (x > 0f) ImGui.SameLine(0, 6f);
                DrawStallChip(group.Key, label, count, width);
                x += width + 6f;
            }
            ImGui.Spacing();
        }

        var btnW = (ImGui.GetContentRegionAvail().X - 8f) / 2f;
        if (UIConstants.AccentButton($"{Lang.ShowOnMap}##fmap_{festival.FestivalId}", UIConstants.Accent, btnW))
            plugin.ShowFestivalOnMap(festival);
        ImGui.SameLine(0, 8f);
        if (UIConstants.AccentButton($"{Lang.Visit}##fvisit_{festival.FestivalId}", UIConstants.Glow, btnW))
            plugin.TeleportToFestival(festival);

        var links = festival.Links;
        if (links != null && links.HasAny)
        {
            var entries = new List<(string Label, string Url, Vector4 Color)>();
            if (!string.IsNullOrEmpty(links.Discord)) entries.Add(("Discord", links.Discord, new Vector4(0.34f, 0.40f, 0.93f, 1f)));
            if (!string.IsNullOrEmpty(links.Partake)) entries.Add(("Partake", links.Partake, new Vector4(0.95f, 0.55f, 0.15f, 1f)));
            if (!string.IsNullOrEmpty(links.Website)) entries.Add(("Website", links.Website, UIConstants.Glow));

            var linkW = (ImGui.GetContentRegionAvail().X - 8f * (entries.Count - 1)) / Math.Max(1, entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0) ImGui.SameLine(0, 8f);
                var (label, url, color) = entries[i];
                if (UIConstants.AccentButton($"{label}##flink_{festival.FestivalId}_{label}", color, linkW))
                    UIConstants.OpenUrl(url);
            }
        }
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private const float StallChipPadX = 10f;
    private const float StallChipIconGap = 6f;
    private const float StallChipCountGap = 6f;
    private const float StallChipIconScale = 0.8f;

    private static Vector2 StallChipIconSize(string type) => CalcIconSize(ServiceIcons.GetIcon(type).ToIconString()) * StallChipIconScale;

    private static float StallChipWidth(string type, string label, int count)
    {
        var width = StallChipPadX + StallChipIconSize(type).X + StallChipIconGap + ImGui.CalcTextSize(label).X + StallChipPadX;
        if (count > 1) width += StallChipCountGap + ImGui.CalcTextSize(count.ToString()).X;
        return width;
    }

    private static void DrawStallChip(string type, string label, int count, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        var height = lineH + 6f;
        ImGui.Dummy(new Vector2(width, height));
        var color = PictomancyMarkerManager.TypeColor(type);
        dl.AddRectFilled(pos, pos + new Vector2(width, height), U32(UIConstants.WithAlpha(color, 0.12f)), height / 2f);
        dl.AddRect(pos, pos + new Vector2(width, height), U32(UIConstants.WithAlpha(color, 0.45f)), height / 2f, ImDrawFlags.None, 1f);

        var glyphSize = StallChipIconSize(type);
        var x = pos.X + StallChipPadX;
        dl.AddText(UiBuilder.IconFont, UiBuilder.IconFont.FontSize * StallChipIconScale, new Vector2(x, pos.Y + (height - glyphSize.Y) / 2f), U32(color), ServiceIcons.GetIcon(type).ToIconString());
        x += glyphSize.X + StallChipIconGap;
        var textY = pos.Y + (height - lineH) / 2f;
        dl.AddText(new Vector2(x, textY), U32(UIConstants.TextPrimary), label);
        if (count <= 1) return;
        x += ImGui.CalcTextSize(label).X + StallChipCountGap;
        dl.AddText(new Vector2(x, textY), U32(UIConstants.TextMuted), count.ToString());
    }

    private void DrawFestivalContextMenu(Festival festival)
    {
        using var menuStyle = UIConstants.MenuStyle();
        using var popup = ImRaii.Popup($"##fctx_{festival.FestivalId}");
        if (!popup) return;

        if (ImGui.MenuItem(Lang.ShowOnMap))
        {
            plugin.ShowFestivalOnMap(festival);
            ImGui.CloseCurrentPopup();
        }
        if (ImGui.MenuItem(Lang.TeleportVia))
        {
            plugin.TeleportToFestival(festival);
            ImGui.CloseCurrentPopup();
        }
    }
}
