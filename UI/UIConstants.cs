using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace VenueMapper.UI;

public enum ColorOverrideMode { None, Rgb, Hacker }

public static class UIConstants
{
    public static ColorOverrideMode OverrideMode = ColorOverrideMode.None;
    public static float RgbHue;
    public static bool IsHackerBooting;

    private static readonly Vector4 BasePrimary    = HexToVec4("#E2C27E");
    private static readonly Vector4 BaseGlow       = HexToVec4("#56D6EE");
    private static readonly Vector4 BaseSecondary  = HexToVec4("#B08D57");
    private static readonly Vector4 BaseTrim       = HexToVec4("#7D6640");
    internal static readonly Vector4 HackerGreen   = new(0.15f, 1f, 0.35f, 1f);

    public static readonly Vector4 Background      = HexToVec4("#0D0F14");
    public static readonly Vector4 PanelRaised     = HexToVec4("#13161D");
    public static readonly Vector4 CardBackground  = HexToVec4("#191D26");
    public static readonly Vector4 TextPrimary     = HexToVec4("#EFE6D2");
    public static readonly Vector4 TextSecondary   = HexToVec4("#A9A08D");
    public static readonly Vector4 TextMuted       = HexToVec4("#6F685B");
    public static readonly Vector4 Line            = HexToVec4("#EFE6D214");
    public static readonly Vector4 LineStrong      = HexToVec4("#EFE6D224");

    public static readonly Vector4 Success    = HexToVec4("#5FE0A2");
    public static readonly Vector4 Danger     = HexToVec4("#FF6F6F");
    public static readonly Vector4 Warning    = HexToVec4("#F2B35B");
    public static readonly Vector4 GoldAccent = HexToVec4("#E2C27E");
    public static readonly Vector4 Rose       = HexToVec4("#FF6F9C");

    public const float CardRounding = 4f;
    public const float ChipRounding = 4f;

    public static Vector4 ApplyOverride(Vector4 original) => OverrideMode switch
    {
        ColorOverrideMode.Rgb    => HsvToRgba(RgbHue, 0.85f, 1f),
        ColorOverrideMode.Hacker => HackerGreen,
        _ => original,
    };

    public static Vector4 Heading   => ApplyOverride(BasePrimary);
    public static Vector4 Accent    => ApplyOverride(BaseGlow);
    public static Vector4 Secondary => ApplyOverride(BaseSecondary);
    public static Vector4 Glow      => ApplyOverride(BaseGlow);
    public static Vector4 Trim      => ApplyOverride(BaseTrim);

    public static Vector4 GlowDim         => WithAlpha(Trim, 0.7f);
    public static Vector4 GlowBright      => WithAlpha(Accent, 1.0f);

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

    private sealed class StyleScope(IDisposable colors, IDisposable styles) : IDisposable
    {
        public void Dispose()
        {
            styles.Dispose();
            colors.Dispose();
        }
    }

    public static IDisposable Scope(IDisposable colors, IDisposable styles) => new StyleScope(colors, styles);

    private static IDisposable ComboStyle() => new StyleScope(
        ImRaii.PushColor(ImGuiCol.FrameBg,        Lighten(CardBackground, 0.30f))
              .Push(ImGuiCol.FrameBgHovered, Lighten(CardBackground, 0.38f))
              .Push(ImGuiCol.FrameBgActive,  Lighten(CardBackground, 0.46f))
              .Push(ImGuiCol.Text,           TextPrimary)
              .Push(ImGuiCol.PopupBg,        Lighten(CardBackground, 0.16f))
              .Push(ImGuiCol.HeaderHovered,  WithAlpha(Accent, 0.30f))
              .Push(ImGuiCol.HeaderActive,   WithAlpha(Accent, 0.45f))
              .Push(ImGuiCol.Border,         GlowDim),
        ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f)
              .Push(ImGuiStyleVar.FrameRounding, 6f)
              .Push(ImGuiStyleVar.PopupRounding, 8f)
              .Push(ImGuiStyleVar.PopupBorderSize, 0f)
              .Push(ImGuiStyleVar.WindowPadding, new Vector2(8, 8))
              .Push(ImGuiStyleVar.ItemSpacing, new Vector2(6, 7)));

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

    public static void StyledCombo(string id, string preview, float width, Action drawItems)
    {
        using var style = ComboStyle();

        var parentDl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, ImGui.GetFrameHeight());
        DrawComboShadow(parentDl, pos, size, 6f);

        ImGui.SetNextItemWidth(width);
        var wasOpen = ImGui.IsPopupOpen(id);
        using var combo = ImRaii.Combo(id, preview);
        bool opened = combo;

        DrawHoverPulseOverlayOnList(parentDl, id, ImGui.IsItemHovered(), opened && !wasOpen, Accent, pos, pos + size);

        if (opened)
            drawItems();
    }

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

    public static readonly Vector4 Hover = HexToVec4("#1F2430");

    private static readonly Dictionary<string, string> DisplayNames = new();

    public static unsafe string DisplayName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (DisplayNames.TryGetValue(name, out var cached)) return cached;

        var font = ImGui.GetIO().FontDefault;
        if (font.IsNull) font = ImGui.GetFont();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var rune in name.EnumerateRunes())
        {
            if (rune.Value > 0xFFFF) continue;
            var c = (char)rune.Value;
            if (c is '‍' or '︎' or '️' or '⃣') continue;
            if (!char.IsWhiteSpace(c) && !font.IsNull && font.FindGlyphNoFallback(c) == null) continue;
            sb.Append(c);
        }

        var result = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();
        if (result.Length == 0) result = name;
        if (!font.IsNull)
            DisplayNames[name] = result;
        return result;
    }

    private static float LinearChannel(float c) => c <= 0.03928f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    public static float RelativeLuminance(Vector4 c)
        => 0.2126f * LinearChannel(c.X) + 0.7152f * LinearChannel(c.Y) + 0.0722f * LinearChannel(c.Z);

    public static float Contrast(Vector4 a, Vector4 b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (MathF.Max(la, lb) + 0.05f) / (MathF.Min(la, lb) + 0.05f);
    }

    public static bool IsDark(Vector4 c) => Contrast(c, Background) < 3f;

    public static Vector4 OnColor(Vector4 background)
    {
        var white = new Vector4(1f, 1f, 1f, 1f);
        return Contrast(background, white) >= Contrast(background, Background) ? white : Background;
    }

    public static Vector4 Readable(Vector4 color)
    {
        if (!IsDark(color)) return color;
        var white = new Vector4(1f, 1f, 1f, color.W);
        for (var t = 0.1f; t < 1f; t += 0.1f)
        {
            var mixed = Vector4Lerp(color, white, t);
            if (Contrast(mixed, Background) >= 4f) return mixed;
        }
        return WithAlpha(TextPrimary, color.W);
    }

    public static void AddTextReadable(ImDrawListPtr dl, Vector2 pos, Vector4 color, string text)
        => dl.AddText(pos, ImGui.ColorConvertFloat4ToU32(Readable(color)), text);

    public static void TextReadable(Vector4 color, string text)
    {
        var pos = ImGui.GetCursorScreenPos();
        var size = ImGui.CalcTextSize(text);
        ImGui.Dummy(size);
        AddTextReadable(ImGui.GetWindowDrawList(), pos, color, text);
    }

    public static Vector4 Mix(Vector4 a, Vector4 b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, 1f);

    public static IDisposable PushTheme()
    {
        var accent = Accent;
        return new StyleScope(
            ImRaii.PushColor(ImGuiCol.Text, TextPrimary)
                  .Push(ImGuiCol.TextDisabled, TextMuted)
                  .Push(ImGuiCol.WindowBg, Background)
                  .Push(ImGuiCol.ChildBg, Vector4.Zero)
                  .Push(ImGuiCol.PopupBg, WithAlpha(CardBackground, 0.98f))
                  .Push(ImGuiCol.Border, LineStrong)
                  .Push(ImGuiCol.BorderShadow, Vector4.Zero)
                  .Push(ImGuiCol.FrameBg, CardBackground)
                  .Push(ImGuiCol.FrameBgHovered, Hover)
                  .Push(ImGuiCol.FrameBgActive, Mix(CardBackground, accent, 0.18f))
                  .Push(ImGuiCol.TitleBg, Background)
                  .Push(ImGuiCol.TitleBgActive, PanelRaised)
                  .Push(ImGuiCol.TitleBgCollapsed, Background)
                  .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
                  .Push(ImGuiCol.ScrollbarGrab, WithAlpha(TextMuted, 0.55f))
                  .Push(ImGuiCol.ScrollbarGrabHovered, WithAlpha(accent, 0.6f))
                  .Push(ImGuiCol.ScrollbarGrabActive, WithAlpha(accent, 0.85f))
                  .Push(ImGuiCol.CheckMark, accent)
                  .Push(ImGuiCol.SliderGrab, accent)
                  .Push(ImGuiCol.SliderGrabActive, Lighten(accent, 0.15f))
                  .Push(ImGuiCol.Button, CardBackground)
                  .Push(ImGuiCol.ButtonHovered, Hover)
                  .Push(ImGuiCol.ButtonActive, Mix(CardBackground, accent, 0.3f))
                  .Push(ImGuiCol.Header, WithAlpha(accent, 0.16f))
                  .Push(ImGuiCol.HeaderHovered, WithAlpha(accent, 0.12f))
                  .Push(ImGuiCol.HeaderActive, WithAlpha(accent, 0.24f))
                  .Push(ImGuiCol.Separator, LineStrong)
                  .Push(ImGuiCol.SeparatorHovered, WithAlpha(accent, 0.6f))
                  .Push(ImGuiCol.SeparatorActive, accent)
                  .Push(ImGuiCol.ResizeGrip, Vector4.Zero)
                  .Push(ImGuiCol.ResizeGripHovered, WithAlpha(accent, 0.4f))
                  .Push(ImGuiCol.ResizeGripActive, WithAlpha(accent, 0.7f))
                  .Push(ImGuiCol.Tab, CardBackground)
                  .Push(ImGuiCol.TabHovered, Mix(CardBackground, accent, 0.25f))
                  .Push(ImGuiCol.TabActive, Mix(CardBackground, accent, 0.18f))
                  .Push(ImGuiCol.TabUnfocused, CardBackground)
                  .Push(ImGuiCol.TabUnfocusedActive, Mix(CardBackground, accent, 0.12f))
                  .Push(ImGuiCol.TableHeaderBg, PanelRaised)
                  .Push(ImGuiCol.TableBorderStrong, LineStrong)
                  .Push(ImGuiCol.TableBorderLight, Line)
                  .Push(ImGuiCol.TableRowBgAlt, WithAlpha(TextPrimary, 0.02f))
                  .Push(ImGuiCol.TextSelectedBg, WithAlpha(accent, 0.3f))
                  .Push(ImGuiCol.DragDropTarget, accent)
                  .Push(ImGuiCol.NavHighlight, accent)
                  .Push(ImGuiCol.PlotHistogram, accent),
            ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 3f)
                  .Push(ImGuiStyleVar.GrabRounding, 3f)
                  .Push(ImGuiStyleVar.PopupRounding, 4f)
                  .Push(ImGuiStyleVar.TabRounding, 3f)
                  .Push(ImGuiStyleVar.GrabMinSize, 10f));
    }

    public static IDisposable ScrollbarStyle() => new StyleScope(
        ImRaii.PushColor(ImGuiCol.ScrollbarBg,          WithAlpha(Background, 0f))
              .Push(ImGuiCol.ScrollbarGrab,        WithAlpha(Trim, 0.55f))
              .Push(ImGuiCol.ScrollbarGrabHovered, WithAlpha(Accent, 0.55f))
              .Push(ImGuiCol.ScrollbarGrabActive,  WithAlpha(Accent, 0.8f)),
        ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 6f)
              .Push(ImGuiStyleVar.ScrollbarRounding, 0f));

    public static float ToggleWidth() => ImGui.GetFrameHeight() * 0.78f * 1.8f;

    public static void FlowNext(float neededWidth, float spacing = 20f)
    {

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
        var col = onColor ?? Accent;
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

    public static void DrawCardBackground(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding = -1f, float glowAlpha = 0.35f)
    {
        var cut = rounding < 0 ? 8f : rounding;
        Chamfer(dl, min, max, cut);
        dl.PathFillConvex(ImGui.ColorConvertFloat4ToU32(PanelRaised));
        Chamfer(dl, min, max, cut);
        dl.PathStroke(ImGui.ColorConvertFloat4ToU32(WithAlpha(Trim, Math.Clamp(glowAlpha * 2f, 0f, 1f))), ImDrawFlags.Closed, 1f);
    }

    public static void Chamfer(ImDrawListPtr dl, Vector2 min, Vector2 max, float cut)
    {
        dl.PathLineTo(new Vector2(min.X + cut, min.Y));
        dl.PathLineTo(new Vector2(max.X, min.Y));
        dl.PathLineTo(new Vector2(max.X, max.Y - cut));
        dl.PathLineTo(new Vector2(max.X - cut, max.Y));
        dl.PathLineTo(new Vector2(min.X, max.Y));
        dl.PathLineTo(new Vector2(min.X, min.Y + cut));
    }

    public static void DrawHudFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float cut, float alpha, bool rgb = false, bool grip = true)
    {
        dl.PushClipRect(min, max, false);
        Chamfer(dl, min, max, cut);
        dl.PathFillConvex(ImGui.ColorConvertFloat4ToU32(WithAlpha(Background, alpha)));
        var inset = new Vector2(0.5f, 0.5f);
        Chamfer(dl, min + inset, max - inset, cut);
        dl.PathStroke(ImGui.ColorConvertFloat4ToU32(WithAlpha(rgb ? Glow : Trim, alpha)), ImDrawFlags.Closed, rgb ? 3f : 1f);

        if (!rgb)
            DrawFrameCorner(dl, min, cut, alpha);

        if (grip)
        {
            var g = ImGui.ColorConvertFloat4ToU32(WithAlpha(Heading, 0.6f * alpha));
            dl.AddLine(new Vector2(max.X - 3f, max.Y - cut - 5f), new Vector2(max.X - cut - 5f, max.Y - 3f), g, 1f);
            dl.AddLine(new Vector2(max.X - 3f, max.Y - cut - 10f), new Vector2(max.X - cut - 10f, max.Y - 3f), g, 1f);
        }
        dl.PopClipRect();
    }

    public static void DrawFrameCorner(ImDrawListPtr dl, Vector2 min, float cut, float alpha)
    {
        var gold = ImGui.ColorConvertFloat4ToU32(WithAlpha(Heading, alpha));
        var clear = ImGui.ColorConvertFloat4ToU32(WithAlpha(Heading, 0f));
        dl.AddLine(new Vector2(min.X + 0.5f, min.Y + cut), new Vector2(min.X + cut, min.Y + 0.5f), gold, 1f);
        dl.AddRectFilledMultiColor(new Vector2(min.X + cut, min.Y), new Vector2(min.X + cut + 180f, min.Y + 1f),
            gold, clear, clear, gold);
        dl.AddRectFilledMultiColor(new Vector2(min.X, min.Y + cut), new Vector2(min.X + 1f, min.Y + cut + 140f),
            gold, gold, clear, clear);
    }

    public static void DrawGem(ImDrawListPtr dl, Vector2 center, float radius, Vector4 color)
    {
        dl.AddQuadFilled(
            center + new Vector2(0, -radius), center + new Vector2(radius, 0),
            center + new Vector2(0, radius), center + new Vector2(-radius, 0),
            ImGui.ColorConvertFloat4ToU32(color));
    }

    public static void DrawTrimRule(ImDrawListPtr dl, float x0, float x1, float y)
    {
        var mid = (x0 + x1) / 2f;
        var clear = ImGui.ColorConvertFloat4ToU32(WithAlpha(Trim, 0f));
        var gold = ImGui.ColorConvertFloat4ToU32(Heading);
        dl.AddRectFilledMultiColor(new Vector2(x0, y), new Vector2(mid, y + 1), clear, gold, gold, clear);
        dl.AddRectFilledMultiColor(new Vector2(mid, y), new Vector2(x1, y + 1), gold, clear, clear, gold);
        var c = new Vector2(mid, y + 0.5f);
        DrawGem(dl, c, 4f, Background);
        dl.AddQuad(c + new Vector2(0, -4), c + new Vector2(4, 0), c + new Vector2(0, 4), c + new Vector2(-4, 0), gold, 1f);
    }

    public static void DrawCardWithAccentBar(ImDrawListPtr dl, Vector2 cardMin, Vector2 cardMax, Vector4 body, Vector4 accent, float rounding, float barWidth = 3f)
    {
        dl.AddRectFilled(cardMin, cardMax, ImGui.ColorConvertFloat4ToU32(accent), rounding);
        dl.AddRectFilled(new Vector2(cardMin.X + barWidth, cardMin.Y), cardMax,
            ImGui.ColorConvertFloat4ToU32(WithAlpha(body, 1f)), rounding, ImDrawFlags.RoundCornersRight);
    }

    public static IDisposable MenuStyle() => new StyleScope(
        ImRaii.PushColor(ImGuiCol.PopupBg, Lighten(CardBackground, 0.10f))
              .Push(ImGuiCol.Border, GlowDim)
              .Push(ImGuiCol.HeaderHovered, WithAlpha(Accent, 0.30f))
              .Push(ImGuiCol.HeaderActive, WithAlpha(Accent, 0.45f))
              .Push(ImGuiCol.Text, TextPrimary),
        ImRaii.PushStyle(ImGuiStyleVar.PopupRounding, 8f)
              .Push(ImGuiStyleVar.PopupBorderSize, 1f)
              .Push(ImGuiStyleVar.WindowPadding, new Vector2(6, 6))
              .Push(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4)));

    public static bool IconChip(string id, FontAwesomeIcon icon, Vector2 size, Vector4? tint = null, bool active = false)
    {
        var col = tint ?? Accent;
        using var colors = ImRaii.PushColor(ImGuiCol.Button, WithAlpha(col, active ? 0.30f : 0.12f))
                                 .Push(ImGuiCol.ButtonHovered, WithAlpha(col, active ? 0.40f : 0.22f))
                                 .Push(ImGuiCol.ButtonActive, WithAlpha(col, 0.50f))
                                 .Push(ImGuiCol.Text, col);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, ChipRounding);

        bool clicked;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            clicked = ImGui.Button($"{icon.ToIconString()}##{id}", size);

        DrawHoverPulseOverlay(id, ImGui.IsItemHovered(), clicked, col);
        return clicked;
    }

    private sealed class SectionScope(float glowAlpha) : IDisposable
    {
        public void Dispose() => EndSection(glowAlpha);
    }

    public static IDisposable Section(float padding = 10f, float glowAlpha = 0.30f)
    {
        BeginSection(padding);
        return new SectionScope(glowAlpha);
    }

    private static ImDrawListPtr sectionDl;
    private static Vector2 sectionMin;
    private static float sectionRightX;
    private static float sectionPad;

    private static void BeginSection(float padding)
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

    private static void EndSection(float glowAlpha)
    {
        ImGui.Unindent(sectionPad);
        ImGui.Dummy(new Vector2(0, sectionPad));
        var bottomY = ImGui.GetCursorScreenPos().Y;

        sectionDl.ChannelsSetCurrent(0);
        DrawCardBackground(sectionDl, sectionMin, new Vector2(sectionRightX, bottomY), -1f, glowAlpha);
        sectionDl.ChannelsMerge();

        ImGui.Spacing();
    }

    public static void PushWindowChrome(Vector4? accent = null, float borderSize = 1f)
    {
        var acc = accent ?? Trim;
        ImGui.PushStyleColor(ImGuiCol.WindowBg,      Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,       Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.Border,        WithAlpha(acc, 0.9f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, borderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 2f);
    }

    public static void PopWindowChrome()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(4);
    }

    public static bool AccentButton(string label, Vector4? accent = null, float width = 0, bool disabled = false,
        float height = 0, string? tooltip = null)
    {
        var col = accent ?? Accent;
        bool clicked;
        using (ImRaii.Disabled(disabled))
        {
            var padY = ImGui.GetStyle().FramePadding.Y;
            using var colors = ImRaii.PushColor(ImGuiCol.Button, WithAlpha(col, 0.2f))
                                     .Push(ImGuiCol.ButtonHovered, WithAlpha(col, 0.35f))
                                     .Push(ImGuiCol.ButtonActive, WithAlpha(col, 0.5f))
                                     .Push(ImGuiCol.Border, WithAlpha(col, 0.6f))
                                     .Push(ImGuiCol.Text, col);
            using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f)
                                    .Push(ImGuiStyleVar.FrameRounding, ChipRounding)
                                    .Push(ImGuiStyleVar.FramePadding, new Vector2(14, padY));
            clicked = ImGui.Button(label, new Vector2(width, height > 0 ? height : ImGui.GetFrameHeight()));
            if (!disabled)
                DrawHoverPulseOverlay(label, ImGui.IsItemHovered(), clicked, col);
        }
        if (tooltip != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tooltip);
        return clicked;
    }

    public static string FormatServerTime(DateTime utc)
    {
        var format = utc.Minute == 0 ? "h tt" : "h:mm tt";
        return utc.ToString(format, System.Globalization.CultureInfo.InvariantCulture) + " ST";
    }

    public static void OpenUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) ||
            (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)) return;
        try { Dalamud.Utility.Util.OpenLink(u.AbsoluteUri); } catch { }
    }

    public static string FitText(string text, float maxWidth, Func<string, float>? measure = null)
    {
        measure ??= s => ImGui.CalcTextSize(s).X;
        if (string.IsNullOrEmpty(text) || measure(text) <= maxWidth) return text;
        const string ellipsis = "...";
        var length = text.Length;
        while (length > 0 && measure(text[..length].TrimEnd() + ellipsis) > maxWidth)
            length--;
        return length > 0 ? text[..length].TrimEnd() + ellipsis : ellipsis;
    }

    public static unsafe void DrawIconCentered(ImDrawListPtr dl, ImFontPtr font, float size, Vector2 center, string glyph, uint color)
    {
        if (string.IsNullOrEmpty(glyph)) return;
        var g = font.FindGlyph(glyph[0]);
        var ink = g == null
            ? new Vector2(size / 2f)
            : new Vector2(g->X0 + g->X1, g->Y0 + g->Y1) * 0.5f * (size / font.FontSize);
        dl.AddText(font, size, center - ink, color, glyph);
    }

    public static void DrawIconCentered(ImDrawListPtr dl, float size, Vector2 center, FontAwesomeIcon icon, uint color)
        => DrawIconCentered(dl, UiBuilder.IconFont, size, center, icon.ToIconString(), color);

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
}
