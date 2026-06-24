using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class EventsView
{
    private readonly PartakeApiService api;
    private List<Venue> venues = new();
    private List<Venue> teamedVenues = new();

    public EventsView(PartakeApiService api)
    {
        this.api = api;
    }

    public void SetVenues(List<Venue> v)
    {
        if (ReferenceEquals(venues, v)) return;
        venues = v;
        teamedVenues = v.Where(ve => ve.TeamId > 0).ToList();
    }

    public void Draw()
    {
        var dl   = ImGui.GetWindowDrawList();
        var winP = ImGui.GetWindowPos();
        var winW = ImGui.GetWindowWidth();
        var cy   = ImGui.GetCursorScreenPos().Y;

        dl.AddRectFilledMultiColor(
            new Vector2(winP.X, cy - 2),
            new Vector2(winP.X + winW, cy + 30),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0.15f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, 0.08f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, 0f)),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Secondary, 0f)));

        var title = Lang.UpcomingEvents;
        ImGui.SetCursorPosX(Math.Max(0f, (winW - ImGui.CalcTextSize(title).X) / 2f));
        ImGui.TextColored(UIConstants.Secondary, title);
        ImGui.Spacing();

        if (teamedVenues.Count == 0)
        {
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), Lang.NoEvents);
            return;
        }

        var anyLoading = false;
        var allEvents = new List<(VenueEvent Evt, Venue Venue)>();

        foreach (var venue in teamedVenues)
        {
            _ = api.FetchTeamAsync(venue.TeamId);

            if (api.IsLoading(venue.TeamId))
            {
                anyLoading = true;
                continue;
            }

            var error = api.GetError(venue.TeamId);
            if (error != null)
            {
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 0.8f), error);
                ImGui.SameLine();
                if (ImGui.SmallButton($"{Lang.Retry}##{venue.VenueId}"))
                    api.Invalidate(venue.TeamId);
                ImGui.Spacing();
                continue;
            }

            foreach (var evt in api.GetEvents(venue.TeamId))
                allEvents.Add((evt, venue));
        }

        if (anyLoading)
        {
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Glow, 0.5f), Lang.Loading);
            ImGui.Spacing();
        }

        if (allEvents.Count == 0 && !anyLoading)
        {
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), Lang.NoEvents);
        }
        else
        {
            foreach (var (evt, venue) in allEvents.OrderBy(e => e.Evt.StartTime))
                DrawEventCard(dl, evt, venue.Colors, venue.Address, venue.Name);
        }

        ImGui.Spacing();
    }

    private static void DrawEventCard(ImDrawListPtr dl, VenueEvent evt, VenueColors? colors = null, string? venueAddress = null, string? venueName = null)
    {
        var avW = ImGui.GetContentRegionAvail().X;
        var cardMin = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        const float dateW = 62f;
        const float cardH = 72f;
        var cardMax = new Vector2(cardMin.X + avW, cardMin.Y + cardH);

        var primary   = colors?.PrimaryVec ?? UIConstants.Primary;     // border + date bg
        var accent    = colors?.AccentVec ?? UIConstants.Glow;         // date text + title hover + link
        var secondary = colors?.SecondaryVec ?? UIConstants.Secondary; // pulse animation
        var gold      = new Vector4(1f, 0.84f, 0f, 1f);               // EVENT badge

        dl.AddRectFilled(cardMin, cardMax,
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.CardBackground, 0.75f)));

        dl.AddRectFilled(cardMin, new Vector2(cardMin.X + 3, cardMax.Y),
            ImGui.ColorConvertFloat4ToU32(primary));

        ImGui.InvisibleButton($"##evt_{evt.EventId}", new Vector2(avW, cardH));
        var hovered = ImGui.IsItemHovered();
        if (hovered && ImGui.IsItemClicked())
            OpenUrl($"https://partake.gg/events/{evt.EventId}");

        var borderPulse = (MathF.Sin((float)ImGui.GetTime() * 1.5f) + 1f) / 2f;
        var borderCol = hovered ? primary : UIConstants.WithAlpha(primary, 0.15f + 0.1f * borderPulse);
        dl.AddRect(cardMin, cardMax,
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(borderCol, hovered ? 0.6f + 0.3f * borderPulse : borderCol.W)),
            0f, ImDrawFlags.None, hovered ? 1.5f : 1f);

        if (hovered)
        {
            dl.AddRect(
                new Vector2(cardMin.X - 1, cardMin.Y - 1),
                new Vector2(cardMax.X + 1, cardMax.Y + 1),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(secondary, 0.1f + 0.15f * borderPulse)),
                0f, ImDrawFlags.None, 2f);

            var shimPhase = (float)(ImGui.GetTime() % 2.0) / 2.0f;
            var shimX = cardMin.X + shimPhase * (avW + 40) - 20;
            dl.PushClipRect(cardMin, cardMax, true);
            dl.AddRectFilledMultiColor(
                new Vector2(shimX, cardMin.Y),
                new Vector2(shimX + 30, cardMax.Y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0f)),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.06f)),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.06f)),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0f)));
            dl.PopClipRect();
        }

        var evtTime = evt.StartTime;
        var dateBlockMax = new Vector2(cardMin.X + dateW + 6, cardMax.Y);
        dl.AddRectFilled(new Vector2(cardMin.X + 3, cardMin.Y), dateBlockMax,
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(primary, 0.15f)));

        var dayStr  = evtTime.ToString("dd");
        var monStr  = evtTime.ToString("MMM").ToUpperInvariant();
        var timeStr = evtTime.ToString("h tt") + " ST";

        var font = ImGui.GetFont();
        var dayFontSz = lineH * 1.5f;
        var dayScale  = dayFontSz / font.FontSize;

        var dayRenderedH = lineH * dayScale;
        var gap = 1f;
        var dateContentH = dayRenderedH + gap + lineH + gap + lineH;
        var dateY  = cardMin.Y + (cardH - dateContentH) * 0.5f;
        var dateCX = cardMin.X + 3 + (dateW + 3) * 0.5f;

        var daySz = ImGui.CalcTextSize(dayStr);
        dl.AddText(font, dayFontSz,
            new Vector2(dateCX - daySz.X * dayScale * 0.5f, dateY),
            ImGui.ColorConvertFloat4ToU32(accent), dayStr);
        var monSz = ImGui.CalcTextSize(monStr);
        dl.AddText(new Vector2(dateCX - monSz.X * 0.5f, dateY + dayRenderedH + gap),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.85f)), monStr);
        var timeSz = ImGui.CalcTextSize(timeStr);
        dl.AddText(new Vector2(dateCX - timeSz.X * 0.5f, dateY + dayRenderedH + gap + lineH + gap),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.7f)), timeStr);

        var isNow = evt.StartTime <= DateTime.UtcNow && evt.EndTime >= DateTime.UtcNow;
        var nowCol = new Vector4(1f, 0.4f, 0f, 1f);

        var badge = $" {evt.EventType} ";
        var badgeSz = ImGui.CalcTextSize(badge);
        var nowBadge = " NOW ";
        var nowSz = ImGui.CalcTextSize(nowBadge);
        var totalBadgeW = badgeSz.X + 4 + (isNow ? nowSz.X + 8 : 0);

        var badgeX = cardMax.X - totalBadgeW - 4;
        dl.AddRectFilled(
            new Vector2(badgeX - 2, cardMin.Y + 4),
            new Vector2(badgeX + badgeSz.X + 2, cardMin.Y + 4 + badgeSz.Y + 2),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(gold, 0.2f)));
        dl.AddRect(
            new Vector2(badgeX - 2, cardMin.Y + 4),
            new Vector2(badgeX + badgeSz.X + 2, cardMin.Y + 4 + badgeSz.Y + 2),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(gold, 0.5f)), 0f, ImDrawFlags.None, 1f);
        dl.AddText(new Vector2(badgeX, cardMin.Y + 5),
            ImGui.ColorConvertFloat4ToU32(gold), badge);

        if (isNow)
        {
            var nowX = badgeX + badgeSz.X + 6;
            var nowPulse = (MathF.Sin((float)ImGui.GetTime() * 3f) + 1f) / 2f;
            var nowAlpha = 0.7f + 0.3f * nowPulse;
            dl.AddRectFilled(
                new Vector2(nowX - 2, cardMin.Y + 4),
                new Vector2(nowX + nowSz.X + 2, cardMin.Y + 4 + nowSz.Y + 2),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(nowCol, 0.25f)));
            dl.AddRect(
                new Vector2(nowX - 2, cardMin.Y + 4),
                new Vector2(nowX + nowSz.X + 2, cardMin.Y + 4 + nowSz.Y + 2),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(nowCol, 0.6f * nowAlpha)), 0f, ImDrawFlags.None, 1f);
            dl.AddText(new Vector2(nowX, cardMin.Y + 5),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(nowCol, nowAlpha)), nowBadge);
        }

        var contentX = cardMin.X + dateW + 14;
        var maxTitleW = avW - dateW - totalBadgeW - 20;
        var titleFontSz = lineH * 1.2f;
        var linkFontSz  = lineH * 1.05f;

        var contentH = titleFontSz + 3 + lineH + 3 + linkFontSz;
        var contentY = cardMin.Y + (cardH - contentH) * 0.5f;

        var titleClean = StripEmoji(evt.Title);
        var titleText = titleClean;
        var titleScale = titleFontSz / font.FontSize;
        while (titleText.Length > 5 && ImGui.CalcTextSize(titleText).X * titleScale > maxTitleW)
            titleText = titleText[..^4] + "...";
        dl.AddText(font, titleFontSz, new Vector2(contentX, contentY),
            ImGui.ColorConvertFloat4ToU32(hovered ? accent : UIConstants.TextPrimary), titleText);

        if (!string.IsNullOrEmpty(evt.Host))
        {
            dl.AddText(new Vector2(contentX, contentY + titleFontSz + 3),
                ImGui.ColorConvertFloat4ToU32(UIConstants.TextSecondary), evt.Host);
        }

        dl.AddText(font, linkFontSz, new Vector2(contentX, contentY + titleFontSz + 3 + lineH + 3),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, hovered ? 1f : 0.65f)),
            Lang.ViewPartake);

        if (!string.IsNullOrEmpty(venueName))
        {
            var vnSz = ImGui.CalcTextSize(venueName);
            dl.AddText(font, lineH * 0.85f,
                new Vector2(cardMax.X - vnSz.X * 0.85f - 6, cardMax.Y - lineH * 0.85f - 4),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.6f)),
                venueName);
        }
    }

    private static string StripEmoji(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c <= 0x7E || (c >= 0xA0 && c <= 0xFF) || char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c))
                sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    private static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) ||
            (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = url, UseShellExecute = true }); } catch { }
    }
}
