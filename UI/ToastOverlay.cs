using System;
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

    public static void Draw(ToastManager toasts, ToastCorner corner, bool enabled)
    {
        var active = toasts.Active;
        if (!enabled || active.Count == 0) return;

        var dl = ImGui.GetForegroundDrawList();
        var vp = ImGui.GetMainViewport();

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

            var boxMin = isTop
                ? new Vector2(isRight ? edgeX + slideX - size.X : edgeX + slideX, cursorY)
                : new Vector2(isRight ? edgeX + slideX - size.X : edgeX + slideX, cursorY - size.Y);
            var boxMax = boxMin + size;

            RenderToast(dl, entry, boxMin, boxMax, alpha);

            cursorY += isTop ? size.Y + Gap : -(size.Y + Gap);
        }
    }

    private static (FontAwesomeIcon Icon, Vector4 Accent, string Label) GetStyle(ToastKind kind) => kind switch
    {
        ToastKind.Success => (FontAwesomeIcon.CheckCircle, UIConstants.ApplyOverride(new Vector4(0.35f, 0.85f, 0.45f, 1f)), "SUCCESS"),
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
        var boxH = pad.Y + headerRowH + lineGap + 1f + lineGap + msgSz.Y + pad.Y;

        return new Vector2(boxW, boxH);
    }

    private static void RenderToast(ImDrawListPtr dl, ToastManager.ToastEntry entry, Vector2 boxMin, Vector2 boxMax, float alpha)
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
        var border = UIConstants.WithAlpha(accent, 0.55f * alpha);
        var shadow = new Vector4(0f, 0f, 0f, 0.30f * alpha);

        dl.AddRectFilled(boxMin + new Vector2(0, 3), boxMax + new Vector2(0, 3), ImGui.ColorConvertFloat4ToU32(shadow), 6f);
        dl.AddRectFilled(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(bg), 6f);

        if (UIConstants.OverrideMode == ColorOverrideMode.Hacker)
        {
            dl.PushClipRect(boxMin, boxMax, true);
            HackerModeOverlay.DrawMatrixRain(dl, boxMin, boxMax, UIConstants.HackerGreen, 0.35f * alpha, 12);
            dl.PopClipRect();
        }

        dl.AddRect(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(border), 6f, ImDrawFlags.None, 1.3f);

        var iconPos = new Vector2(boxMin.X + pad.X, boxMin.Y + pad.Y + (headerRowH - glyphSz.Y) / 2f);
        dl.AddText(iconFont, iconSize, iconPos, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, alpha)), iconStr);

        var headerPos = new Vector2(iconPos.X + glyphSz.X + headerGap, boxMin.Y + pad.Y + (headerRowH - headerSz.Y) / 2f);
        dl.AddText(headerPos, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), kindLabel);

        var sepY = boxMin.Y + pad.Y + headerRowH + lineGap;
        dl.AddLine(new Vector2(boxMin.X + pad.X, sepY), new Vector2(boxMax.X - pad.X, sepY),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.5f * alpha)), 1f);

        var msgPos = new Vector2(boxMin.X + pad.X, sepY + lineGap);
        dl.AddText(msgPos, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, alpha)), entry.Text);
    }
}
