using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VenueMapper.UI;

public enum ColorOverrideMode { None, Rgb, Hacker }

public static class UIConstants
{
    public static ColorOverrideMode OverrideMode = ColorOverrideMode.None;
    public static float RgbHue;
    public static bool IsHackerBooting;

    private static readonly Vector4 BasePrimary    = HexToVec4("#FF006E");
    private static readonly Vector4 BaseGlow       = HexToVec4("#00F5FF");
    private static readonly Vector4 BaseSecondary  = HexToVec4("#9D4EDD");
    internal static readonly Vector4 HackerGreen   = new(0.15f, 1f, 0.35f, 1f);

    public static readonly Vector4 Background      = HexToVec4("#0a0e27");
    public static readonly Vector4 TextPrimary     = HexToVec4("#FFFFFF");
    public static readonly Vector4 TextSecondary   = HexToVec4("#B0B0B0");
    public static readonly Vector4 CardBackground  = HexToVec4("#1a1f3a");

    public static Vector4 ApplyOverride(Vector4 original) => OverrideMode switch
    {
        ColorOverrideMode.Rgb    => HsvToRgba(RgbHue, 0.85f, 1f),
        ColorOverrideMode.Hacker => HackerGreen,
        _ => original,
    };

    public static Vector4 Primary   => ApplyOverride(BasePrimary);
    public static Vector4 Secondary => ApplyOverride(BaseSecondary);
    public static Vector4 Glow      => ApplyOverride(BaseGlow);

    public static Vector4 GlowDim         => WithAlpha(Glow, 0.35f);
    public static Vector4 GlowBright      => WithAlpha(Glow, 1.0f);
    public static Vector4 PrimaryHover    => Lighten(Primary, 0.15f);

    public static Vector4 HexToVec4(string hex)
    {
        hex = hex.TrimStart('#');
        var r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        var g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        var b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        var a = hex.Length >= 8 ? System.Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : 1.0f;
        return new Vector4(r, g, b, a);
    }

    public static Vector4 WithAlpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);

    public static List<string> WrapText(string text, float wrapWidth)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' '))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && ImGui.CalcTextSize(candidate).X > wrapWidth)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0)
            lines.Add(current);
        return lines;
    }

    public static Vector4 Lighten(Vector4 color, float amount) => new(
        System.MathF.Min(1f, color.X + amount),
        System.MathF.Min(1f, color.Y + amount),
        System.MathF.Min(1f, color.Z + amount),
        color.W);

    public static Vector4 HsvToRgba(float h, float s, float v)
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

    private static int TagOrder(string tag) => tag switch
    {
        "ADDED"    => 0,
        "IMPROVED" => 1,
        "CHANGED"  => 2,
        "FIXED"    => 3,
        "REMOVED"  => 4,
        _          => 5,
    };

    public static void DrawChangelog(ChangelogSection[] sections)
    {
        var dl    = ImGui.GetWindowDrawList();
        var first = true;
        foreach (var section in sections)
        {
            if (!first) ImGui.Dummy(new Vector2(0, 5));
            first = false;

            var lang   = ChangelogData.CurrentLanguage;
            var sorted = section.Entries
                .OrderBy(e => TagOrder(e.Tag))
                .ToArray();

            if (section.Title != null)
            {
                var sectionTitle = (lang == "DE" && section.TitleDE != null) ? section.TitleDE : section.Title;
                const float sPadX = 7f;
                var titleSz = ImGui.CalcTextSize(sectionTitle);
                var pillW   = titleSz.X + sPadX * 2;
                var pillPos = ImGui.GetCursorScreenPos();

                dl.AddRectFilled(
                    new Vector2(pillPos.X,        pillPos.Y + 1f),
                    new Vector2(pillPos.X + pillW, pillPos.Y + titleSz.Y + 1f),
                    ImGui.ColorConvertFloat4ToU32(WithAlpha(Secondary, 0.20f)), 4f);
                dl.AddText(
                    new Vector2(pillPos.X + sPadX, pillPos.Y),
                    ImGui.ColorConvertFloat4ToU32(WithAlpha(Secondary, 0.95f)), sectionTitle);

                ImGui.Dummy(new Vector2(pillW, titleSz.Y + 4));

                var lineX      = pillPos.X + 3;
                var lineStartY = ImGui.GetCursorScreenPos().Y;

                ImGui.Indent(14f);
                foreach (var e in sorted)
                {
                    DrawChangelogTag(e.Tag);
                    ImGui.TextWrapped((lang == "DE" && e.TextDE != null) ? e.TextDE : e.Text);
                }
                var lineEndY = ImGui.GetCursorScreenPos().Y - 2;
                ImGui.Unindent(14f);

                if (lineEndY > lineStartY)
                    dl.AddLine(
                        new Vector2(lineX, lineStartY),
                        new Vector2(lineX, lineEndY),
                        ImGui.ColorConvertFloat4ToU32(WithAlpha(Secondary, 0.40f)), 2f);
            }
            else
            {
                foreach (var e in sorted)
                {
                    DrawChangelogTag(e.Tag);
                    ImGui.TextWrapped((lang == "DE" && e.TextDE != null) ? e.TextDE : e.Text);
                }
            }
        }
    }

    public static void DrawChangelogTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        var (bg, fg) = tag switch
        {
            "ADDED"    => (new Vector4(0.20f, 0.65f, 0.35f, 0.20f), new Vector4(0.40f, 0.90f, 0.55f, 1f)),
            "FIXED"    => (new Vector4(0.75f, 0.50f, 0.10f, 0.20f), new Vector4(0.98f, 0.75f, 0.20f, 1f)),
            "CHANGED"  => (new Vector4(0.20f, 0.45f, 0.90f, 0.20f), new Vector4(0.45f, 0.70f, 1.00f, 1f)),
            "IMPROVED" => (new Vector4(0.50f, 0.25f, 0.85f, 0.20f), new Vector4(0.70f, 0.50f, 1.00f, 1f)),
            "REMOVED"  => (new Vector4(0.80f, 0.20f, 0.20f, 0.20f), new Vector4(1.00f, 0.45f, 0.45f, 1f)),
            _          => (new Vector4(0.45f, 0.45f, 0.45f, 0.20f), new Vector4(0.70f, 0.70f, 0.70f, 1f)),
        };

        const float padX = 5f;
        const float colW = 82f;

        var textSize = ImGui.CalcTextSize(tag);
        var tagW     = textSize.X + padX * 2;
        var pos      = ImGui.GetCursorScreenPos();
        var dl       = ImGui.GetWindowDrawList();

        var xOff = MathF.Floor((colW - tagW) / 2f);

        dl.AddRectFilled(
            new Vector2(pos.X + xOff,        pos.Y + 1f),
            new Vector2(pos.X + xOff + tagW,  pos.Y + textSize.Y + 1f),
            ImGui.ColorConvertFloat4ToU32(bg), 3f);
        dl.AddText(
            new Vector2(pos.X + xOff + padX, pos.Y),
            ImGui.ColorConvertFloat4ToU32(fg), tag);

        ImGui.Dummy(new Vector2(colW, textSize.Y));
        ImGui.SameLine(0, 6);
    }
}
