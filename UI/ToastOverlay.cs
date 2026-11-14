using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility.Raii;
using VenueMapper.Services;

namespace VenueMapper.UI;

public static class ToastOverlay
{
    private const float FadeInTime = 0.18f;
    private const float FadeOutTime = 0.35f;
    private const float Gap = 8f;
    private const float MinWidth = 240f;
    private const float MaxWidth = 340f;
    private const float Margin = 16f;
    private const float TopOffset = 60f;
    private const float Cut = 8f;
    private const float IconSize = 28f;
    private static readonly Vector2 Pad = new(12f, 9f);

    private const float BannerFadeIn = 0.35f;
    private const float BannerFadeOut = 0.6f;

    private static ToastManager.ToastEntry? pinnedEntry;
    private static Vector2 pinnedBoxMin;

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    public static void Draw(ToastManager toasts, ToastCorner corner, bool enabled, IFontHandle? bannerFont, float reserved = 0f)
    {
        if (!enabled) { pinnedEntry = null; return; }

        var dl = ImGui.GetForegroundDrawList();
        var banner = toasts.ActiveBanner;
        if (banner != null)
            DrawBanner(dl, banner, bannerFont);

        var active = toasts.Active;
        if (active.Count == 0) { pinnedEntry = null; return; }
        if (pinnedEntry != null && !active.Contains(pinnedEntry)) pinnedEntry = null;

        var vp = ImGui.GetMainViewport();
        var mousePos = ImGui.GetMousePos();
        var deltaTime = ImGui.GetIO().DeltaTime;

        var isTop = corner is ToastCorner.TopLeft or ToastCorner.TopRight;
        var isRight = corner is ToastCorner.TopRight or ToastCorner.BottomRight;

        var edgeX = isRight ? vp.WorkPos.X + vp.WorkSize.X - Margin : vp.WorkPos.X + Margin;
        var cursorY = isTop ? vp.WorkPos.Y + TopOffset + reserved : vp.WorkPos.Y + vp.WorkSize.Y - Margin - reserved;

        foreach (var entry in active)
        {
            var elapsed = (float)(DateTime.Now - entry.StartedAt).TotalSeconds;
            var duration = (float)entry.Duration;

            float alpha;
            float slide = 0f;
            if (elapsed < FadeInTime)
            {
                var eased = 1f - MathF.Pow(1f - elapsed / FadeInTime, 3f);
                alpha = eased;
                slide = (1f - eased) * 30f;
            }
            else if (elapsed > duration - FadeOutTime)
            {
                var t = Math.Clamp((elapsed - (duration - FadeOutTime)) / FadeOutTime, 0f, 1f);
                alpha = 1f - t * t;
                slide = t * t * 30f;
            }
            else
            {
                alpha = 1f;
            }
            if (alpha <= 0.01f) continue;

            var slideX = isRight ? slide : -slide;
            var layout = MeasureCard(entry);
            var size = layout.Size;

            var boxMin = entry == pinnedEntry
                ? pinnedBoxMin
                : isTop
                    ? new Vector2(isRight ? edgeX + slideX - size.X : edgeX + slideX, cursorY)
                    : new Vector2(isRight ? edgeX + slideX - size.X : edgeX + slideX, cursorY - size.Y);
            var boxMax = boxMin + size;

            var hovered = mousePos.X >= boxMin.X && mousePos.X <= boxMax.X && mousePos.Y >= boxMin.Y && mousePos.Y <= boxMax.Y;
            if (hovered)
            {
                entry.StartedAt += TimeSpan.FromSeconds(deltaTime);
                pinnedEntry = entry;
                pinnedBoxMin = boxMin;
            }
            else if (entry == pinnedEntry)
            {
                pinnedEntry = null;
            }

            var remaining = duration > 0f ? Math.Clamp(1f - elapsed / duration, 0f, 1f) : 0f;
            RenderCard(dl, entry, layout, boxMin, boxMax, alpha, remaining, elapsed);
            cursorY += isTop ? size.Y + Gap : -(size.Y + Gap);
        }

        var trimCount = toasts.RecentTrimCount;
        if (trimCount > 0)
            DrawTrimBadge(dl, trimCount, edgeX, cursorY, isTop, isRight);
    }

    private static (FontAwesomeIcon Icon, Vector4 Color) KindStyle(ToastKind kind) => kind switch
    {
        ToastKind.Success => (FontAwesomeIcon.Check, UIConstants.ApplyOverride(UIConstants.Success)),
        ToastKind.Warning => (FontAwesomeIcon.ExclamationTriangle, UIConstants.ApplyOverride(UIConstants.Warning)),
        ToastKind.Egg     => (FontAwesomeIcon.Star, UIConstants.ApplyOverride(UIConstants.Rose)),
        _                 => (FontAwesomeIcon.InfoCircle, UIConstants.Accent),
    };

    private static Vector4 EntryColor(ToastManager.ToastEntry entry)
        => entry.Color is { } c && UIConstants.OverrideMode == ColorOverrideMode.None ? UIConstants.Readable(c) : KindStyle(entry.Kind).Color;

    private sealed record CardLayout(Vector2 Size, List<string> Title, List<string> Detail);

    private static CardLayout MeasureCard(ToastManager.ToastEntry entry)
    {
        var textW = MaxWidth - Pad.X * 2f - IconSize - 10f;
        var title = UIConstants.WrapText(entry.Text, textW);
        var detail = entry.Detail != null ? UIConstants.WrapText(entry.Detail, textW) : new List<string>();
        var lineH = ImGui.GetTextLineHeight();
        var widest = title.Concat(detail).Select(l => ImGui.CalcTextSize(l).X).DefaultIfEmpty(0f).Max();
        var width = MathF.Max(MinWidth, Pad.X * 2f + IconSize + 10f + widest);
        var textH = title.Count * lineH + (detail.Count > 0 ? 2f + detail.Count * lineH : 0f);
        var height = Pad.Y * 2f + MathF.Max(IconSize, textH) + 3f;
        return new CardLayout(new Vector2(width, height), title, detail);
    }

    private static void RenderCard(ImDrawListPtr dl, ToastManager.ToastEntry entry, CardLayout layout, Vector2 min, Vector2 max,
        float alpha, float remaining, float elapsed)
    {
        var color = EntryColor(entry);
        var color2 = entry.Color2 is { } c2 && UIConstants.OverrideMode == ColorOverrideMode.None ? UIConstants.Readable(c2) : color;
        var icon = KindStyle(entry.Kind).Icon;

        UIConstants.Chamfer(dl, min + new Vector2(0f, 3f), max + new Vector2(0f, 3f), Cut);
        dl.PathFillConvex(U32(new Vector4(0f, 0f, 0f, 0.3f * alpha)));
        UIConstants.Chamfer(dl, min, max, Cut);
        dl.PathFillConvex(U32(UIConstants.WithAlpha(UIConstants.Background, 0.94f * alpha)));

        if (UIConstants.OverrideMode == ColorOverrideMode.Hacker)
        {
            dl.PushClipRect(min, max, true);
            HackerModeOverlay.DrawMatrixRain(dl, min, max, UIConstants.HackerGreen, 0.35f * alpha, 12);
            dl.PopClipRect();
        }
        else if (entry.Kind == ToastKind.Egg)
        {
            dl.PushClipRect(min, max, true);
            DrawSparkle(dl, min, max, color, alpha, entry.CreatedAt);
            dl.PopClipRect();
        }

        dl.AddRectFilledMultiColor(new Vector2(min.X, min.Y + Cut), new Vector2(min.X + 3f, max.Y),
            U32(UIConstants.WithAlpha(color, alpha)), U32(UIConstants.WithAlpha(color, alpha)),
            U32(UIConstants.WithAlpha(color2, alpha)), U32(UIConstants.WithAlpha(color2, alpha)));

        UIConstants.Chamfer(dl, min + new Vector2(0.5f), max - new Vector2(0.5f), Cut);
        dl.PathStroke(U32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.14f * alpha)), ImDrawFlags.Closed, 1f);
        var accent = U32(UIConstants.WithAlpha(color, alpha));
        var clear = U32(UIConstants.WithAlpha(color, 0f));
        dl.AddLine(new Vector2(min.X + 0.5f, min.Y + Cut), new Vector2(min.X + Cut, min.Y + 0.5f), accent, 1.2f);
        dl.AddRectFilledMultiColor(new Vector2(min.X + Cut, min.Y), new Vector2(min.X + Cut + 90f, min.Y + 1f), accent, clear, clear, accent);

        var pop = 1f;
        if (elapsed < FadeInTime)
        {
            var t = elapsed / FadeInTime;
            const float c1 = 1.70158f;
            pop = 1f + (c1 + 1f) * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
        }
        if (entry.Kind is ToastKind.Warning or ToastKind.Egg)
            pop *= 1f + (MathF.Sin((float)ImGui.GetTime() * 4f) + 1f) * 0.04f;

        var iconCenter = new Vector2(min.X + Pad.X + 4f + IconSize / 2f, min.Y + Pad.Y + IconSize / 2f);
        dl.AddCircleFilled(iconCenter, IconSize / 2f * pop, U32(UIConstants.WithAlpha(color, 0.18f * alpha)), 24);
        UIConstants.DrawIconCentered(dl, UiBuilder.IconFont.FontSize * 0.85f * pop, iconCenter, icon, U32(UIConstants.WithAlpha(color, alpha)));

        var lineH = ImGui.GetTextLineHeight();
        var textX = min.X + Pad.X + 4f + IconSize + 10f;
        var textH = layout.Title.Count * lineH + (layout.Detail.Count > 0 ? 2f + layout.Detail.Count * lineH : 0f);
        var y = min.Y + Pad.Y + MathF.Max(0f, (IconSize - textH) / 2f);
        foreach (var line in layout.Title)
        {
            dl.AddText(new Vector2(textX, y), U32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), line);
            y += lineH;
        }
        if (layout.Detail.Count > 0)
        {
            y += 2f;
            foreach (var line in layout.Detail)
            {
                dl.AddText(new Vector2(textX, y), U32(UIConstants.WithAlpha(UIConstants.TextSecondary, alpha)), line);
                y += lineH;
            }
        }

        var barStart = min.X + 3f;
        var barEnd = max.X - Cut;
        var barX = barStart + (barEnd - barStart) * remaining;
        if (barX > barStart)
            dl.AddRectFilled(new Vector2(barStart, max.Y - 2.5f), new Vector2(barX, max.Y - 0.5f), U32(UIConstants.WithAlpha(color, 0.75f * alpha)));
    }

    private static void DrawBanner(ImDrawListPtr dl, ToastManager.ToastEntry entry, IFontHandle? font)
    {
        var elapsed = (float)(DateTime.Now - entry.StartedAt).TotalSeconds;
        var duration = (float)entry.Duration;
        float alpha;
        if (elapsed < BannerFadeIn) alpha = elapsed / BannerFadeIn;
        else if (elapsed > duration - BannerFadeOut) alpha = Math.Clamp((duration - elapsed) / BannerFadeOut, 0f, 1f);
        else alpha = 1f;
        if (alpha <= 0.01f) return;
        alpha = alpha * alpha * (3f - 2f * alpha);

        var vp = ImGui.GetMainViewport();
        var centerX = vp.WorkPos.X + vp.WorkSize.X / 2f;
        var top = vp.WorkPos.Y + vp.WorkSize.Y * 0.15f + (1f - alpha) * 8f;
        var color = EntryColor(entry);
        var color2 = entry.Color2 is { } c2 && UIConstants.OverrideMode == ColorOverrideMode.None ? UIConstants.Readable(c2) : color;

        Vector2 titleSize;
        ImFontPtr titleFont;
        float titleFontSize;
        using (font?.Push())
        {
            titleFont = ImGui.GetFont();
            titleFontSize = ImGui.GetFontSize();
            titleSize = ImGui.CalcTextSize(entry.Text);
        }
        var detailSize = entry.Detail != null ? ImGui.CalcTextSize(entry.Detail) : Vector2.Zero;
        var contentW = MathF.Max(titleSize.X, detailSize.X);
        var bandW = MathF.Min(contentW + 220f, vp.WorkSize.X - 40f);
        var padY = 10f;
        var bandH = padY * 2f + titleSize.Y + (entry.Detail != null ? 4f + detailSize.Y : 0f);
        var bandMin = new Vector2(centerX - bandW / 2f, top);
        var bandMax = bandMin + new Vector2(bandW, bandH);
        var mid = centerX;

        var dark = U32(UIConstants.WithAlpha(UIConstants.Background, 0.86f * alpha));
        var clear = U32(UIConstants.WithAlpha(UIConstants.Background, 0f));
        dl.AddRectFilledMultiColor(bandMin, new Vector2(mid, bandMax.Y), clear, dark, dark, clear);
        dl.AddRectFilledMultiColor(new Vector2(mid, bandMin.Y), bandMax, dark, clear, clear, dark);

        var lineInset = bandW * 0.1f;
        var gold = UIConstants.Heading;
        foreach (var y in new[] { bandMin.Y, bandMax.Y })
        {
            var solid = U32(UIConstants.WithAlpha(gold, 0.9f * alpha));
            var fade = U32(UIConstants.WithAlpha(gold, 0f));
            dl.AddRectFilledMultiColor(new Vector2(bandMin.X + lineInset, y), new Vector2(mid, y + 1f), fade, solid, solid, fade);
            dl.AddRectFilledMultiColor(new Vector2(mid, y), new Vector2(bandMax.X - lineInset, y + 1f), solid, fade, fade, solid);
        }

        var titlePos = new Vector2(centerX - titleSize.X / 2f, bandMin.Y + padY);
        dl.AddText(titleFont, titleFontSize, titlePos + new Vector2(0f, 1.5f), U32(new Vector4(0f, 0f, 0f, 0.85f * alpha)), entry.Text);
        dl.AddText(titleFont, titleFontSize, titlePos, U32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), entry.Text);

        var gemY = titlePos.Y + titleSize.Y / 2f;
        var gemRadius = MathF.Max(8f, titleSize.Y * 0.32f);
        var gemOffset = gemRadius + 14f;
        foreach (var (center, gemColor) in new[] { (new Vector2(titlePos.X - gemOffset, gemY), color), (new Vector2(titlePos.X + titleSize.X + gemOffset, gemY), color2) })
        {
            UIConstants.DrawGem(dl, center, gemRadius + 4f, UIConstants.WithAlpha(gemColor, 0.22f * alpha));
            UIConstants.DrawGem(dl, center, gemRadius, UIConstants.WithAlpha(gemColor, alpha));
            UIConstants.DrawGem(dl, center - new Vector2(0f, gemRadius * 0.35f), gemRadius * 0.35f, UIConstants.WithAlpha(UIConstants.TextPrimary, 0.45f * alpha));
        }

        if (entry.Detail != null)
            dl.AddText(new Vector2(centerX - detailSize.X / 2f, titlePos.Y + titleSize.Y + 4f),
                U32(UIConstants.WithAlpha(UIConstants.TextSecondary, alpha)), entry.Detail);
    }

    private static void DrawTrimBadge(ImDrawListPtr dl, int count, float edgeX, float cursorY, bool isTop, bool isRight)
    {
        var label = $"+{count} more";
        var textSz = ImGui.CalcTextSize(label);
        var pad = new Vector2(10, 5);
        var boxSz = textSz + pad * 2;
        var boxMin = isTop
            ? new Vector2(isRight ? edgeX - boxSz.X : edgeX, cursorY)
            : new Vector2(isRight ? edgeX - boxSz.X : edgeX, cursorY - boxSz.Y);
        var boxMax = boxMin + boxSz;

        UIConstants.Chamfer(dl, boxMin, boxMax, 5f);
        dl.PathFillConvex(U32(UIConstants.WithAlpha(UIConstants.Background, 0.88f)));
        UIConstants.Chamfer(dl, boxMin, boxMax, 5f);
        dl.PathStroke(U32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.35f)), ImDrawFlags.Closed, 1f);
        dl.AddText(boxMin + pad, U32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.85f)), label);
    }

    private static void DrawSparkle(ImDrawListPtr dl, Vector2 boxMin, Vector2 boxMax, Vector4 accent, float alpha, DateTime seedSource)
    {
        const int count = 7;
        var seed = seedSource.Ticks;
        var rng = new Random(unchecked((int)(seed ^ (seed >> 32))));
        var time = (float)ImGui.GetTime();
        var size = boxMax - boxMin;

        for (var i = 0; i < count; i++)
        {
            var px = boxMin.X + (float)rng.NextDouble() * size.X;
            var py = boxMin.Y + (float)rng.NextDouble() * size.Y;
            var phase = (float)rng.NextDouble() * MathF.PI * 2f;
            var speed = 1.5f + (float)rng.NextDouble();
            var twinkle = (MathF.Sin(time * speed + phase) + 1f) * 0.5f;
            var radius = 1f + twinkle * 1.5f;
            dl.AddCircleFilled(new Vector2(px, py), radius, U32(UIConstants.WithAlpha(accent, twinkle * 0.8f * alpha)));
        }
    }
}
