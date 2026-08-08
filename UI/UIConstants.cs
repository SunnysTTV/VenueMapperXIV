using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace VenueMapper.UI;

public enum ColorOverrideMode { None, Rgb, Hacker }

public static class UIConstants
{
    public static ColorOverrideMode OverrideMode = ColorOverrideMode.None;
    public static float RgbHue;
    public static bool IsHackerBooting;

    private static readonly Vector4 BasePrimary    = HexToVec4("#4A7EBB");
    private static readonly Vector4 BaseGlow       = HexToVec4("#00F5FF");
    private static readonly Vector4 BaseSecondary  = HexToVec4("#9D4EDD");
    internal static readonly Vector4 HackerGreen   = new(0.15f, 1f, 0.35f, 1f);

    public static readonly Vector4 Background      = HexToVec4("#0a0e27");
    public static readonly Vector4 TextPrimary     = HexToVec4("#FFFFFF");
    public static readonly Vector4 TextSecondary   = HexToVec4("#B0B0B0");
    public static readonly Vector4 CardBackground  = HexToVec4("#1a1f3a");

    public static readonly Vector4 Success    = HexToVec4("#4DDE73");
    public static readonly Vector4 Danger     = HexToVec4("#FF4D4D");
    public static readonly Vector4 Warning    = HexToVec4("#FFAA33");
    public static readonly Vector4 GoldAccent = HexToVec4("#FFD700");

    public const float CardRounding = 10f;
    public const float ChipRounding = 6f;

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

    private static readonly Dictionary<string, float> HoverAnim = new();
    private static readonly Dictionary<string, float> ClickPulse = new();

    private static float StepHover(string id, bool hovered)
    {
        HoverAnim.TryGetValue(id, out var v);
        var target = hovered ? 1f : 0f;
        v += (target - v) * System.MathF.Min(1f, ImGui.GetIO().DeltaTime * 10f);
        if (v < 0.003f) HoverAnim.Remove(id); else HoverAnim[id] = v;
        return v;
    }

    private static float StepPulse(string id)
    {
        ClickPulse.TryGetValue(id, out var v);
        v = System.MathF.Max(0f, v - ImGui.GetIO().DeltaTime * 3.5f);
        if (v > 0f) ClickPulse[id] = v; else ClickPulse.Remove(id);
        return v;
    }

    public static void DrawHoverPulseOverlay(string id, bool hovered, bool clicked, Vector4 accent)
        => DrawHoverPulseOverlayOnList(ImGui.GetWindowDrawList(), id, hovered, clicked, accent,
            ImGui.GetItemRectMin(), ImGui.GetItemRectMax());

    public static void DrawHoverPulseOverlayOnList(ImDrawListPtr dl, string id, bool hovered, bool clicked, Vector4 accent, Vector2 min, Vector2 max)
    {
        if (clicked) ClickPulse[id] = 1f;
        var hoverT = StepHover(id, hovered);
        var pulseT = StepPulse(id);
        if (hoverT < 0.003f && pulseT < 0.003f) return;

        var rounding = ImGui.GetStyle().FrameRounding;

        if (hoverT > 0.003f)
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(WithAlpha(accent, hoverT * 0.12f)), rounding);

        if (pulseT > 0.003f)
        {
            var pad = pulseT * 4f;
            dl.AddRect(min - new Vector2(pad, pad), max + new Vector2(pad, pad),
                ImGui.ColorConvertFloat4ToU32(WithAlpha(accent, pulseT * 0.5f)), rounding + pad, ImDrawFlags.None, 1.5f);
        }
    }

    private static void PushComboStyles()
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg,        Lighten(CardBackground, 0.30f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Lighten(CardBackground, 0.38f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,  Lighten(CardBackground, 0.46f));
        ImGui.PushStyleColor(ImGuiCol.Text,           TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.PopupBg,        Lighten(CardBackground, 0.16f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,  WithAlpha(Primary, 0.30f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,   WithAlpha(Primary, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.Border,         GlowDim);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 7));
    }

    private static void PopComboStyles()
    {
        ImGui.PopStyleVar(6);
        ImGui.PopStyleColor(8);
    }

    private static readonly Vector2[] ShadowOffsets = { new(1, 2), new(2, 4), new(3, 6) };
    private static readonly float[] ShadowAlphas = { 0.22f, 0.14f, 0.07f };

    private static void DrawComboShadow(ImDrawListPtr dl, Vector2 pos, Vector2 size, float rounding)
    {
        for (var i = 0; i < ShadowOffsets.Length; i++)
        {
            var min = pos + ShadowOffsets[i];
            var max = pos + size + ShadowOffsets[i];
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, ShadowAlphas[i])), rounding);
        }
    }

    /// <summary>Draws a themed combo box styled as a raised card with a soft drop shadow. <paramref name="drawItems"/> is only invoked while the dropdown is open.</summary>
    public static void StyledCombo(string id, string preview, float width, Action drawItems)
    {
        PushComboStyles();

        var parentDl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, ImGui.GetFrameHeight());
        DrawComboShadow(parentDl, pos, size, 6f);

        ImGui.SetNextItemWidth(width);
        var wasOpen = ImGui.IsPopupOpen(id);
        var opened = ImGui.BeginCombo(id, preview);

        DrawHoverPulseOverlayOnList(parentDl, id, ImGui.IsItemHovered(), opened && !wasOpen, Primary, pos, pos + size);

        if (opened)
        {
            drawItems();
            ImGui.EndCombo();
        }

        PopComboStyles();
    }

    /// <summary>Array-backed variant of <see cref="StyledCombo(string,string,float,Action)"/> for simple index-selection dropdowns.</summary>
    public static bool StyledCombo(string id, string[] options, ref int index, float width)
    {
        var current = index;
        var preview = current >= 0 && current < options.Length ? options[current] : "";
        var newIndex = current;
        StyledCombo(id, preview, width, () =>
        {
            for (var i = 0; i < options.Length; i++)
            {
                if (ImGui.Selectable(options[i], i == current))
                    newIndex = i;
            }
        });
        if (newIndex == current) return false;
        index = newIndex;
        return true;
    }

    /// <summary>Thin, rounded, glow-tinted scrollbar to replace the harsh default gray one. Push before a scrollable region, pop after.</summary>
    public static void PushScrollbarStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,          WithAlpha(Background, 0f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,        WithAlpha(Glow, 0.25f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, WithAlpha(Glow, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive,  WithAlpha(Glow, 0.65f));
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 8f);
    }

    public static void PopScrollbarStyle()
    {
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
    }

    /// <summary>Width of a Toggle() pill for the current frame height - mirrors Toggle()'s own sizing calc so callers can estimate layout width without duplicating the formula.</summary>
    public static float ToggleWidth() => ImGui.GetFrameHeight() * 0.78f * 1.8f;

    /// <summary>Continues the next item on the current line only if <paramref name="neededWidth"/> actually fits in the remaining space - otherwise leaves the cursor alone so the next item wraps to a new line naturally. Use to make a second toggle/control sit beside the first "if there's room", adapting to whatever width the window actually has instead of a hardcoded guess.</summary>
    /// <summary>Right-aligns the next item (of the given width) to the current line's content region right edge - use so two controls in different rows still line up in the same column.</summary>
    public static void RightAlign(float width)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > width)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - width);
    }

    /// <summary>Right-aligns the next item to a specific absolute screen-space X (from <see cref="RightEdgeX"/>), rather than the current row's own remaining space - use when two controls on different rows must land at the *exact same* pixel column, since their individual "remaining space" can differ slightly depending on what precedes them on their own row.</summary>
    public static void RightAlignAbs(float width, float targetRightScreenX)
    {
        var y = ImGui.GetCursorScreenPos().Y;
        ImGui.SetCursorScreenPos(new Vector2(targetRightScreenX - width, y));
    }

    /// <summary>Captures the current line's right edge in absolute screen-space X, for later use with <see cref="RightAlignAbs"/>.</summary>
    public static float RightEdgeX() => ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;

    public static void FlowNext(float neededWidth, float spacing = 20f)
    {
        // Extra safety margin on top of the caller's own estimate - CalcTextSize/toggle-width math
        // is close but not pixel-perfect against the real rendered layout, and a borderline "yes it
        // fits" that's wrong by a few pixels clips the item at the window edge instead of wrapping.
        const float safety = 16f;
        if (ImGui.GetContentRegionAvail().X >= neededWidth + spacing + safety)
            ImGui.SameLine(0, spacing);
    }

    private static readonly Dictionary<string, float> ToggleAnim = new();
    private static readonly Dictionary<string, float> TogglePulse = new();

    public static Vector4 Vector4Lerp(Vector4 a, Vector4 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Z + (b.Z - a.Z) * t,
        a.W + (b.W - a.W) * t);

    public static bool Toggle(string id, ref bool value, Vector4? onColor = null)
    {
        var col = onColor ?? Primary;
        var frameH = ImGui.GetFrameHeight();
        var height = frameH * 0.78f;
        var width = height * 1.8f;
        var radius = height * 0.5f;
        var dt = ImGui.GetIO().DeltaTime;

        var origin = ImGui.GetCursorScreenPos();
        var p = origin + new Vector2(0, (frameH - height) * 0.5f);

        ImGui.InvisibleButton(id, new Vector2(width, frameH));
        var hovered = ImGui.IsItemHovered();
        var changed = false;
        if (ImGui.IsItemClicked())
        {
            value = !value;
            changed = true;
            TogglePulse[id] = 1f;
        }

        if (!ToggleAnim.TryGetValue(id, out var t))
            t = value ? 1f : 0f;
        var target = value ? 1f : 0f;
        t += (target - t) * System.MathF.Min(1f, dt * 14f);
        ToggleAnim[id] = t;

        TogglePulse.TryGetValue(id, out var pulse);
        pulse = System.MathF.Max(0f, pulse - dt * 3.5f);
        if (pulse > 0f) TogglePulse[id] = pulse; else TogglePulse.Remove(id);

        var dl = ImGui.GetWindowDrawList();
        var trackOff = WithAlpha(TextSecondary, 0.22f);
        var trackCol = Vector4Lerp(trackOff, col, t);
        if (hovered) trackCol = Lighten(trackCol, 0.08f);

        if (hovered || pulse > 0f)
        {
            var glowAlpha = (hovered ? 0.18f : 0f) + pulse * 0.25f;
            var glowPad = 3f + pulse * 2f;
            dl.AddRectFilled(p - new Vector2(glowPad, glowPad), p + new Vector2(width + glowPad, height + glowPad),
                ImGui.ColorConvertFloat4ToU32(WithAlpha(col, glowAlpha)), radius + glowPad);
        }

        dl.AddRectFilled(p, p + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(trackCol), radius);

        var knobX = p.X + radius + t * (width - height);
        var knobCenter = new Vector2(knobX, p.Y + radius);
        var knobRadius = (radius - 2.5f) * (1f + pulse * 0.2f);
        dl.AddCircleFilled(knobCenter, knobRadius, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)));

        return changed;
    }

    private static readonly Vector2[] GlowRimOffsets = { new(1, 1), new(2, 2) };
    private static readonly float[] GlowRimAlphas = { 0.18f, 0.09f };

    /// <summary>Draws a card-style background (filled panel + crisp border + soft outward glow rim) onto an arbitrary rect. Draw-list only — does not affect the ImGui cursor.</summary>
    public static void DrawCardBackground(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding = -1f, float glowAlpha = 0.35f)
    {
        var r = rounding < 0 ? CardRounding : rounding;

        for (var i = GlowRimOffsets.Length - 1; i >= 0; i--)
        {
            var mn = min - GlowRimOffsets[i];
            var mx = max + GlowRimOffsets[i];
            dl.AddRect(mn, mx, ImGui.ColorConvertFloat4ToU32(WithAlpha(Glow, GlowRimAlphas[i])), r + GlowRimOffsets[i].X, ImDrawFlags.None, 1f);
        }

        dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(CardBackground), r);
        dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(WithAlpha(Glow, glowAlpha)), r, ImDrawFlags.None, 1.2f);
    }

    /// <summary>Draws a card body with a left accent stripe that follows the card's own rounded corners (rather than
    /// either poking a square corner past the curve, or stopping short and leaving a gap before it). Draws the accent
    /// as a full rounded backdrop, then punches the body color back over everything except the left <paramref name="barWidth"/>
    /// strip - since both layers share the same rounding, the accent naturally tapers into the same curve as the card silhouette.
    /// <paramref name="body"/> is forced fully opaque: the punch-back layer sits on top of the accent backdrop, not the
    /// window background, so any alpha in it would blend with the accent color instead of replacing it (tints the whole card).</summary>
    public static void DrawCardWithAccentBar(ImDrawListPtr dl, Vector2 cardMin, Vector2 cardMax, Vector4 body, Vector4 accent, float rounding, float barWidth = 3f)
    {
        dl.AddRectFilled(cardMin, cardMax, ImGui.ColorConvertFloat4ToU32(accent), rounding);
        dl.AddRectFilled(new Vector2(cardMin.X + barWidth, cardMin.Y), cardMax,
            ImGui.ColorConvertFloat4ToU32(WithAlpha(body, 1f)), rounding, ImDrawFlags.RoundCornersRight);
    }

    /// <summary>Themed replacement for the default gray ImGui popup chrome - push before BeginPopup/BeginPopupContextItem, pop after EndPopup.</summary>
    public static void PushMenuStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Lighten(CardBackground, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.Border, GlowDim);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, WithAlpha(Primary, 0.30f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, WithAlpha(Primary, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.Text, TextPrimary);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
    }

    public static void PopMenuStyle()
    {
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(5);
    }

    /// <summary>A small rounded-square icon button (e.g. favorite/hide/eye-toggle/delete/link). Includes hover-glow and click-pulse feedback.</summary>
    public static bool IconChip(string id, FontAwesomeIcon icon, Vector2 size, Vector4? tint = null, bool active = false)
    {
        var col = tint ?? Primary;
        ImGui.PushStyleColor(ImGuiCol.Button,        WithAlpha(col, active ? 0.30f : 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,  WithAlpha(col, active ? 0.40f : 0.22f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,   WithAlpha(col, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.Text,           col);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ChipRounding);

        var iconFont = UiBuilder.IconFont;
        ImGui.PushFont(iconFont);
        var clicked = ImGui.Button($"{icon.ToIconString()}##{id}", size);
        ImGui.PopFont();

        DrawHoverPulseOverlay(id, ImGui.IsItemHovered(), clicked, col);

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        return clicked;
    }

    private static ImDrawListPtr sectionDl;
    private static Vector2 sectionMin;
    private static float sectionRightX;
    private static float sectionPad;

    /// <summary>Starts a card-panel section: subsequent widgets are laid out inside padded, indented content that ends up drawn on top of a card background + glow rim added by <see cref="EndSection"/>.</summary>
    public static void BeginSection(float padding = 10f)
    {
        sectionDl = ImGui.GetWindowDrawList();
        sectionPad = padding;
        var cursor = ImGui.GetCursorScreenPos();
        sectionMin = cursor;
        sectionRightX = cursor.X + ImGui.GetContentRegionAvail().X;

        sectionDl.ChannelsSplit(2);
        sectionDl.ChannelsSetCurrent(1);
        ImGui.Dummy(new Vector2(0, padding));
        ImGui.Indent(padding);
    }

    public static void EndSection(float glowAlpha = 0.30f)
    {
        ImGui.Unindent(sectionPad);
        ImGui.Dummy(new Vector2(0, sectionPad));
        var bottomY = ImGui.GetCursorScreenPos().Y;

        sectionDl.ChannelsSetCurrent(0);
        DrawCardBackground(sectionDl, sectionMin, new Vector2(sectionRightX, bottomY), -1f, glowAlpha);
        sectionDl.ChannelsMerge();

        ImGui.Spacing();
    }

    /// <summary>Codifies the shared window chrome (dark bg, glow-tinted border, square corners) used across the plugin's windows.</summary>
    public static void PushWindowChrome(Vector4? accent = null, float borderSize = 1.5f)
    {
        var acc = accent ?? Glow;
        ImGui.PushStyleColor(ImGuiCol.WindowBg,      Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,       Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, CardBackground);
        ImGui.PushStyleColor(ImGuiCol.Border,        WithAlpha(acc, 0.55f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, borderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
    }

    public static void PopWindowChrome()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(4);
    }

    /// <summary>Shared ghost-style accent button (tinted background, colored border/text, hover-glow + click-pulse feedback).</summary>
    public static bool AccentButton(string label, Vector4? accent = null, float width = 0, bool disabled = false)
    {
        var col = accent ?? Primary;
        if (disabled) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, WithAlpha(col, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, WithAlpha(col, 0.35f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, WithAlpha(col, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.Border, WithAlpha(col, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.Text, col);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ChipRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(14, ImGui.GetStyle().FramePadding.Y));
        var clicked = ImGui.Button(label, new Vector2(width, ImGui.GetFrameHeight()));
        if (!disabled)
            DrawHoverPulseOverlay(label, ImGui.IsItemHovered(), clicked, col);
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(5);
        if (disabled) ImGui.EndDisabled();
        return clicked;
    }

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
