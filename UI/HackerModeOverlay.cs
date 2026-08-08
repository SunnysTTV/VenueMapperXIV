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

    public static void Draw(ref double hackerModeStart, ref double hackerTitleLoopStart, string windowName)
    {
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

            dl.AddRectFilled(winMin, winMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0.02f, 0f, 0.9f)));
            DrawMatrixRain(dl, winMin, winMax, green, 0.16f);
            DrawHackerTitle(dl, winMin, winSz.X, green, ref hackerTitleLoopStart, windowName);

            var glitchOffset = Vector2.Zero;
            if (Rng.NextDouble() < 0.04)
                glitchOffset = new Vector2(Rng.Next(-3, 4), 0);

            var lineY = winMin.Y + ImGui.GetFrameHeight() + 10 + glitchOffset.Y;
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
            DrawMatrixRain(dl, winMin, winMax, green, 0.18f);
            DrawHackerTitle(dl, winMin, winSz.X, green, ref hackerTitleLoopStart, windowName);
            const float scanSpacing = 4f;
            var scanCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(green, 0.035f));
            for (var y = winMin.Y; y < winMax.Y; y += scanSpacing)
                dl.AddLine(new Vector2(winMin.X, y), new Vector2(winMax.X, y), scanCol, 1f);

            dl.AddRect(winMin, winMax, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(green, 0.5f)), 0f, ImDrawFlags.None, 1.5f);
        }

        dl.PopClipRect();
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

    private static void DrawHackerTitle(ImDrawListPtr dl, Vector2 winMin, float winWidth, Vector4 green, ref double hackerTitleLoopStart, string windowName)
    {
        var titleBarHeight = ImGui.GetFrameHeight();
        var displayTitle = windowName.Split("##")[0];

        if (hackerTitleLoopStart < 0) hackerTitleLoopStart = ImGui.GetTime();
        const double loopDuration = 4.0;
        const double typeDuration = 1.6;
        var phase = (ImGui.GetTime() - hackerTitleLoopStart) % loopDuration;
        var typeT = (float)Math.Clamp(phase / typeDuration, 0, 1);
        var take = Math.Clamp((int)(typeT * displayTitle.Length), 0, displayTitle.Length);
        var shown = displayTitle[..take];

        dl.AddRectFilled(winMin, new Vector2(winMin.X + winWidth, winMin.Y + titleBarHeight),
            ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Primary, 0.25f)));

        var textPos = new Vector2(winMin.X + 8, winMin.Y + (titleBarHeight - ImGui.GetTextLineHeight()) * 0.5f);
        dl.AddText(textPos, ImGui.ColorConvertFloat4ToU32(green), shown);
        if (take < displayTitle.Length && (int)(phase * 3) % 2 == 0)
            dl.AddText(new Vector2(textPos.X + ImGui.CalcTextSize(shown).X, textPos.Y),
                ImGui.ColorConvertFloat4ToU32(green), "_");
    }
}
