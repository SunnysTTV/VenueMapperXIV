using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using VenueMapper.Services;

namespace VenueMapper.UI;

public static class ToastOverlay
{
    private const float FadeInTime = 0.18f;
    private const float FadeOutTime = 0.35f;
    private const float Gap = 8f;
    private const float MinWidth = 230f;
    private const float Margin = 16f;
    private const float TopOffset = 60f;
    private const float ProgressBarHeight = 2.5f;

    private static ToastManager.ToastEntry? pinnedEntry;
    private static Vector2 pinnedBoxMin;

    public static void Draw(ToastManager toasts, ToastCorner corner, bool enabled)
    {
        var active = toasts.Active;
        if (!enabled || active.Count == 0) { pinnedEntry = null; return; }
        if (pinnedEntry != null && !active.Contains(pinnedEntry)) pinnedEntry = null;

        var dl = ImGui.GetForegroundDrawList();
        var vp = ImGui.GetMainViewport();
        var mousePos = ImGui.GetMousePos();
        var deltaTime = ImGui.GetIO().DeltaTime;

        var isTop = corner is ToastCorner.TopLeft or ToastCorner.TopRight;
        var isRight = corner is ToastCorner.TopRight or ToastCorner.BottomRight;

        var edgeX = isRight ? vp.WorkPos.X + vp.WorkSize.X - Margin : vp.WorkPos.X + Margin;
        var cursorY = isTop ? vp.WorkPos.Y + TopOffset : vp.WorkPos.Y + vp.WorkSize.Y - Margin;

        foreach (var entry in active)
        {
            var elapsed = (float)(DateTime.Now - entry.StartedAt).TotalSeconds;
            var duration = (float)entry.Duration;

            float alpha;
            float slide = 0f;

            if (elapsed < FadeInTime)
            {
                var t = elapsed / FadeInTime;
                var eased = 1f - MathF.Pow(1f - t, 3f);
                alpha = eased;
                slide = (1f - eased) * 30f;
            }
            else if (elapsed > duration - FadeOutTime)
            {
                var t = Math.Clamp((elapsed - (duration - FadeOutTime)) / FadeOutTime, 0f, 1f);
                var eased = t * t;
                alpha = 1f - eased;
                slide = eased * 30f;
            }
            else
            {
                alpha = 1f;
            }

            if (alpha <= 0.01f) continue;

            var slideX = isRight ? slide : -slide;
            var size = MeasureToast(entry);

            var boxMin = entry == pinnedEntry
                ? pinnedBoxMin
                : isTop
                    ? new Vector2(isRight ? edgeX + slideX - size.X : edgeX + slideX, cursorY)
                    : new Vector2(isRight ? edgeX + slideX - size.X : edgeX + slideX, cursorY - size.Y);
            var boxMax = boxMin + size;

            var hovered = mousePos.X >= boxMin.X && mousePos.X <= boxMax.X
                       && mousePos.Y >= boxMin.Y && mousePos.Y <= boxMax.Y;
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

            var remainingFrac = duration > 0f ? Math.Clamp(1f - elapsed / duration, 0f, 1f) : 0f;
            RenderToast(dl, entry, boxMin, boxMax, alpha, remainingFrac, elapsed);

            cursorY += isTop ? size.Y + Gap : -(size.Y + Gap);
        }

        var trimCount = toasts.RecentTrimCount;
        if (trimCount > 0)
            DrawTrimBadge(dl, trimCount, edgeX, cursorY, isTop, isRight);
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

        dl.AddRectFilled(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0.85f)), UIConstants.ChipRounding);
        dl.AddRect(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f)), UIConstants.ChipRounding);
        dl.AddText(boxMin + pad, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.85f)), label);
    }

    private static (FontAwesomeIcon Icon, Vector4 Accent, string Label) GetStyle(ToastKind kind) => kind switch
    {
        ToastKind.Success => (FontAwesomeIcon.CheckCircle, UIConstants.ApplyOverride(UIConstants.Success), "SUCCESS"),
        ToastKind.Warning => (FontAwesomeIcon.ExclamationTriangle, UIConstants.ApplyOverride(UIConstants.Warning), "WARNING"),
        ToastKind.Egg => (FontAwesomeIcon.Star, UIConstants.Glow, "EASTER EGG"),
        _ => (FontAwesomeIcon.InfoCircle, UIConstants.Secondary, "NOTIFICATION"),
    };

    private static Vector2 MeasureToast(ToastManager.ToastEntry entry)
    {
        var (icon, _, kindLabel) = GetStyle(entry.Kind);
        var iconFont = UiBuilder.IconFont;
        var headerSz = ImGui.CalcTextSize(kindLabel);
        var msgSz = ImGui.CalcTextSize(entry.Text);

        var iconSize = headerSz.Y * 1.15f;
        var glyphScale = iconSize / iconFont.FontSize;
        ImGui.PushFont(iconFont);
        var glyphRawSz = ImGui.CalcTextSize(icon.ToIconString());
        ImGui.PopFont();
        var glyphSz = glyphRawSz * glyphScale;

        var pad = new Vector2(14, 10);
        var headerGap = 7f;
        var lineGap = 6f;

        var headerRowW = glyphSz.X + headerGap + headerSz.X;
        var contentW = Math.Max(headerRowW, msgSz.X);
        var boxW = Math.Max(MinWidth, contentW + pad.X * 2);

        var headerRowH = Math.Max(glyphSz.Y, headerSz.Y);
        var boxH = pad.Y + headerRowH + lineGap + 1f + lineGap + msgSz.Y + pad.Y + ProgressBarHeight;

        return new Vector2(boxW, boxH);
    }

    private static void RenderToast(ImDrawListPtr dl, ToastManager.ToastEntry entry, Vector2 boxMin, Vector2 boxMax, float alpha, float remainingFrac, float elapsed)
    {
        var (icon, accent, kindLabel) = GetStyle(entry.Kind);

        var iconFont = UiBuilder.IconFont;
        var iconStr = icon.ToIconString();
        var headerSz = ImGui.CalcTextSize(kindLabel);

        var pad = new Vector2(14, 10);
        var headerGap = 7f;
        var lineGap = 6f;

        var iconSize = headerSz.Y * 1.15f;
        var glyphScale = iconSize / iconFont.FontSize;
        ImGui.PushFont(iconFont);
        var glyphRawSz = ImGui.CalcTextSize(iconStr);
        ImGui.PopFont();
        var glyphSz = glyphRawSz * glyphScale;

        var headerRowH = Math.Max(glyphSz.Y, headerSz.Y);

        var bg = UIConstants.WithAlpha(UIConstants.Background, 0.90f * alpha);
        var shadow = new Vector4(0f, 0f, 0f, 0.30f * alpha);

        dl.AddRectFilled(boxMin + new Vector2(0, 3), boxMax + new Vector2(0, 3), ImGui.ColorConvertFloat4ToU32(shadow), UIConstants.ChipRounding);

        const float barWidth = 3f;
        dl.AddRectFilled(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.85f * alpha)), UIConstants.ChipRounding);
        dl.AddRectFilled(new Vector2(boxMin.X + barWidth, boxMin.Y), boxMax,
            ImGui.ColorConvertFloat4ToU32(bg), UIConstants.ChipRounding, ImDrawFlags.RoundCornersRight);

        if (UIConstants.OverrideMode == ColorOverrideMode.Hacker)
        {
            dl.PushClipRect(boxMin, boxMax, true);
            HackerModeOverlay.DrawMatrixRain(dl, boxMin, boxMax, UIConstants.HackerGreen, 0.35f * alpha, 12);
            dl.PopClipRect();
        }
        else if (entry.Kind == ToastKind.Egg)
        {
            dl.PushClipRect(boxMin, boxMax, true);
            DrawSparkle(dl, boxMin, boxMax, accent, alpha, entry.CreatedAt);
            dl.PopClipRect();
        }

        dl.AddRect(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.35f * alpha)), UIConstants.ChipRounding, ImDrawFlags.None, 1f);

        var popScale = 1f;
        if (elapsed < FadeInTime)
        {
            var t = elapsed / FadeInTime;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            popScale = 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
        }
        var pulseScale = 1f;
        if (entry.Kind is ToastKind.Warning or ToastKind.Egg)
        {
            var pulseT = (MathF.Sin((float)ImGui.GetTime() * 4f) + 1f) * 0.5f;
            pulseScale = 1f + pulseT * 0.12f;
        }
        var animIconSize = iconSize * popScale * pulseScale;
        var animGlyphSz = glyphRawSz * (animIconSize / iconFont.FontSize);

        var iconCenter = new Vector2(boxMin.X + pad.X + glyphSz.X / 2f, boxMin.Y + pad.Y + headerRowH / 2f);
        var iconPos = iconCenter - animGlyphSz / 2f;
        dl.AddText(iconFont, animIconSize, iconPos, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, alpha)), iconStr);

        var headerPos = new Vector2(boxMin.X + pad.X + glyphSz.X + headerGap, boxMin.Y + pad.Y + (headerRowH - headerSz.Y) / 2f);
        dl.AddText(headerPos, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), kindLabel);

        var sepY = boxMin.Y + pad.Y + headerRowH + lineGap;
        dl.AddLine(new Vector2(boxMin.X + pad.X, sepY), new Vector2(boxMax.X - pad.X, sepY),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.5f * alpha)), 1f);

        var msgPos = new Vector2(boxMin.X + pad.X, sepY + lineGap);
        dl.AddText(msgPos, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, alpha)), entry.Text);

        var barY0 = boxMax.Y - ProgressBarHeight - 2f;
        var barY1 = boxMax.Y - 2f;
        var barX0 = boxMin.X + 2f;
        var barMaxX = boxMax.X - 2f;
        var barX1 = barX0 + (barMaxX - barX0) * remainingFrac;
        if (barX1 > barX0)
            dl.AddRectFilled(new Vector2(barX0, barY0), new Vector2(barX1, barY1),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.7f * alpha)), 1.5f);
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
            var col = UIConstants.WithAlpha(accent, twinkle * 0.8f * alpha);
            dl.AddCircleFilled(new Vector2(px, py), radius, ImGui.ColorConvertFloat4ToU32(col));
        }
    }
}
