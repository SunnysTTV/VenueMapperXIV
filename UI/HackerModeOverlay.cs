using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VenueMapper.UI;

public static class HackerModeOverlay
{
    private static readonly Random Rng = new();

    private static readonly string[] HackerLogLines =
    [
        "> initializing venue_grid.sys...",
        "> bypassing housing firewall... OK",
        "> decrypting plot coordinates...",
        "> spoofing datacenter handshake...",
        "> mounting bgcommon/hou volume...",
        "> enumerating ward aetherytes...",
        "> cross-referencing partake.gg feed...",
        "> patching lifestream navigation table...",
        "> calibrating pictomancy overlay...",
        "> injecting rgb_overload.dll... OK",
        "> disabling mainframe countermeasures...",
        "> access granted: root@venuemapper",
    ];

    public static void Draw(ref double hackerModeStart, ref double hackerTitleLoopStart, string windowName, float titleBarHeight = 0f,
        float frameCut = 0f, float titleInsetX = 8f, float titleTop = 0f)
    {
        var titleH = titleBarHeight > 0f ? titleBarHeight : ImGui.GetFrameHeight();
        if (UIConstants.OverrideMode != ColorOverrideMode.Hacker)
        {
            hackerModeStart = -1;
            hackerTitleLoopStart = -1;
            return;
        }
        if (hackerModeStart < 0) hackerModeStart = ImGui.GetTime();

        var dl = ImGui.GetWindowDrawList();
        var winMin = ImGui.GetWindowPos();
        var winSz  = ImGui.GetWindowSize();
        var winMax = winMin + winSz;
        var green = UIConstants.HackerGreen;

        dl.PushClipRect(winMin, winMax, true);

        if (UIConstants.IsHackerBooting)
        {
            var elapsed = ImGui.GetTime() - hackerModeStart;

            FillFrame(dl, winMin, winMax, frameCut, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0.02f, 0f, 0.9f)));
            DrawMatrixRain(dl, winMin + new Vector2(frameCut * 0.5f, 2f), winMax - new Vector2(frameCut * 0.5f, 2f), green, 0.16f);
            DrawHackerTitle(dl, winMin, winSz.X, green, ref hackerTitleLoopStart, windowName, titleH, titleInsetX, titleTop);

            var glitchOffset = Vector2.Zero;
            if (Rng.NextDouble() < 0.04)
                glitchOffset = new Vector2(Rng.Next(-3, 4), 0);

            var lineY = winMin.Y + titleTop + titleH + 10 + glitchOffset.Y;
            const double charsPerSecond = 70.0;
            var budget = elapsed * charsPerSecond;

            foreach (var line in HackerLogLines)
            {
                var take = (int)Math.Clamp(budget, 0, line.Length);
                budget -= line.Length + 2;
                var shown = line[..Math.Max(0, take)];
                if (shown.Length > 0)
                    dl.AddText(new Vector2(winMin.X + 14 + glitchOffset.X, lineY), ImGui.ColorConvertFloat4ToU32(green), shown);
                lineY += 18;
                if (take < line.Length) break;
            }

            if ((int)(elapsed * 2) % 2 == 0)
                dl.AddText(new Vector2(winMin.X + 14, lineY), ImGui.ColorConvertFloat4ToU32(green), "_");
        }
        else
        {
            DrawMatrixRain(dl, winMin + new Vector2(frameCut * 0.5f, 2f), winMax - new Vector2(frameCut * 0.5f, 2f), green, 0.18f);
            DrawHackerTitle(dl, winMin, winSz.X, green, ref hackerTitleLoopStart, windowName, titleH, titleInsetX, titleTop);
            const float scanSpacing = 4f;
            var scanCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(green, 0.035f));
            for (var y = winMin.Y; y < winMax.Y; y += scanSpacing)
                dl.AddLine(new Vector2(winMin.X, y), new Vector2(winMax.X, y), scanCol, 1f);

            var border = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(green, 0.5f));
            if (frameCut > 0f)
            {
                UIConstants.Chamfer(dl, winMin + new Vector2(0.75f), winMax - new Vector2(0.75f), frameCut);
                dl.PathStroke(border, ImDrawFlags.Closed, 1.5f);
            }
            else
            {
                dl.AddRect(winMin, winMax, border, 0f, ImDrawFlags.None, 1.5f);
            }
        }

        dl.PopClipRect();
    }

    private static void FillFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float cut, uint color)
    {
        if (cut <= 0f)
        {
            dl.AddRectFilled(min, max, color);
            return;
        }
        UIConstants.Chamfer(dl, min, max, cut);
        dl.PathFillConvex(color);
    }

    internal static void DrawMatrixRain(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax, Vector4 green, float baseAlpha, int colCount = 20)
    {
        const int trailLength = 6;
        var colW = (winMax.X - winMin.X) / colCount;
        var lineH = ImGui.GetTextLineHeight();
        var height = winMax.Y - winMin.Y + trailLength * lineH;

        for (var c = 0; c < colCount; c++)
        {
            var speed = 30 + (c % 5) * 15;
            var headY = (float)((ImGui.GetTime() * speed + c * 53) % height) - trailLength * lineH;
            var seedRng = new Random(c * 7919 + (int)(ImGui.GetTime() * 2));
            for (var i = 0; i < trailLength; i++)
            {
                var y = winMin.Y + headY - i * lineH;
                if (y < winMin.Y || y > winMax.Y) continue;
                var alpha = baseAlpha * (1f - (float)i / trailLength);
                var glyph = ((char)('0' + seedRng.Next(10))).ToString();
                dl.AddText(new Vector2(winMin.X + c * colW, y), ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(green, alpha)), glyph);
            }
        }
    }

    private static void DrawHackerTitle(ImDrawListPtr dl, Vector2 winMin, float winWidth, Vector4 green, ref double hackerTitleLoopStart, string windowName,
        float titleBarHeight, float insetX, float top)
    {
        var displayTitle = windowName.Split("##")[0];

        if (hackerTitleLoopStart < 0) hackerTitleLoopStart = ImGui.GetTime();
        const double loopDuration = 4.0;
        const double typeDuration = 1.6;
        var phase = (ImGui.GetTime() - hackerTitleLoopStart) % loopDuration;
        var typeT = (float)Math.Clamp(phase / typeDuration, 0, 1);
        var take = Math.Clamp((int)(typeT * displayTitle.Length), 0, displayTitle.Length);
        var shown = displayTitle[..take];

        var bandMin = new Vector2(winMin.X + MathF.Max(0f, insetX - 6f), winMin.Y + top);
        dl.AddRectFilled(bandMin, new Vector2(winMin.X + winWidth - 2f, bandMin.Y + titleBarHeight),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Accent, 0.18f)));

        var textPos = new Vector2(winMin.X + insetX, bandMin.Y + (titleBarHeight - ImGui.GetTextLineHeight()) * 0.5f);
        dl.AddText(textPos, ImGui.ColorConvertFloat4ToU32(green), shown);
        if (take < displayTitle.Length && (int)(phase * 3) % 2 == 0)
            dl.AddText(new Vector2(textPos.X + ImGui.CalcTextSize(shown).X, textPos.Y),
                ImGui.ColorConvertFloat4ToU32(green), "_");
    }
}
