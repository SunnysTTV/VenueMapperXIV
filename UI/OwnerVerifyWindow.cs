using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class OwnerVerifyWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;

    private enum Phase { Scanning, Granted, Denied }

    private Phase phase = Phase.Scanning;
    private double phaseStart;
    private Venue? targetVenue;

    private const double ScanDuration = 1.1;
    private const double GrantedHold = 1.1;
    private const double DeniedHold = 3.5;

    private const float Pad = 20f;
    private const float ContentW = 200f;
    private const float Radius = 36f;
    private const float RingBlockHeight = Radius * 2f + 12f;

    public OwnerVerifyWindow(VenueMapperPlugin plugin)
        : base("OwnerVerifyScan##OwnerVerify",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
    }

    public void BeginVerify(Venue venue)
    {
        targetVenue = venue;
        phase = Phase.Scanning;
        phaseStart = ImGui.GetTime();
        IsOpen = true;
    }

    private float ContentHeight()
    {
        var lineH = ImGui.GetTextLineHeight();
        var spacingY = ImGui.GetStyle().ItemSpacing.Y;
        var hintLines = phase == Phase.Denied ? UIConstants.WrapText(Lang.OwnerVerifyDeniedHint, ContentW).Count : 0;
        var hintBlockH = hintLines > 0 ? lineH * hintLines + spacingY * (hintLines - 1) : 0f;
        return lineH + spacingY * 2 + RingBlockHeight + spacingY + lineH + 6f
             + (phase == Phase.Denied ? spacingY + hintBlockH : 0f);
    }

    public override void PreDraw()
    {
        Size = new Vector2(ContentW + Pad * 2f, ContentHeight() + Pad * 2f);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.02f, 0.02f, 0.05f, 0.55f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 24f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Pad, Pad));

        var vp = ImGui.GetMainViewport();
        var pos = vp.Pos + new Vector2(vp.Size.X / 2f, 0f);
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(1);
    }

    public override void Draw()
    {
        var elapsed = (float)(ImGui.GetTime() - phaseStart);

        DrawSpacedHeader(Lang.OwnerVerifyTitle, UIConstants.WithAlpha(UIConstants.Glow, 0.75f), 4f);
        ImGui.Spacing();
        ImGui.Spacing();

        switch (phase)
        {
            case Phase.Scanning:
                DrawScan(elapsed);
                if (elapsed >= ScanDuration)
                    ResolveVerification();
                break;
            case Phase.Granted:
                DrawResult(elapsed, granted: true);
                if (elapsed >= GrantedHold)
                {
                    IsOpen = false;
                    plugin.OwnerUpdateWindow.LoadCurrentVenue();
                    plugin.OwnerUpdateWindow.IsOpen = true;
                }
                break;
            case Phase.Denied:
                DrawResult(elapsed, granted: false);
                if (elapsed >= DeniedHold)
                    IsOpen = false;
                break;
        }
    }

    private void ResolveVerification()
    {
        if (targetVenue == null)
        {
            IsOpen = false;
            return;
        }

        var granted = targetVenue.OwnerIdHashes.Count == 0;
        if (!granted)
        {
            var myHash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
            granted = targetVenue.OwnerIdHashes.Contains(myHash, StringComparer.OrdinalIgnoreCase);
        }

        phase = granted ? Phase.Granted : Phase.Denied;
        phaseStart = ImGui.GetTime();
    }

    private static void DrawScan(float elapsed)
    {
        var color = UIConstants.Glow;
        DrawScanRing(elapsed, color, scanning: true, icon: FontAwesomeIcon.User);

        ImGui.Spacing();
        CenteredText(Lang.OwnerVerifyScanning, color);
    }

    private static Vector2 ShakeOffset(float elapsed)
    {
        const float duration = 0.5f;
        if (elapsed >= duration) return Vector2.Zero;
        const float freq = 30f;
        const float amplitude = 7f;
        var decay = 1f - elapsed / duration;
        var x = MathF.Sin(elapsed * freq) * amplitude * decay * decay;
        return new Vector2(x, 0f);
    }

    private static Vector2 BounceOffset(float elapsed)
    {
        const float duration = 0.5f;
        if (elapsed >= duration) return Vector2.Zero;
        const float freq = 30f;
        const float amplitude = 7f;
        var decay = 1f - elapsed / duration;
        var y = MathF.Sin(elapsed * freq) * amplitude * decay * decay;
        return new Vector2(0f, y);
    }

    private static void DrawResult(float elapsed, bool granted)
    {
        var color = granted ? new Vector4(0.35f, 0.9f, 0.5f, 1f) : new Vector4(0.95f, 0.3f, 0.3f, 1f);
        var icon = granted ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
        var shake = granted ? BounceOffset(elapsed) : ShakeOffset(elapsed);
        DrawScanRing(elapsed, color, scanning: false, icon: icon, shakeOffset: shake);

        ImGui.Spacing();
        CenteredText(granted ? Lang.OwnerVerifyGranted : Lang.OwnerVerifyDenied, color);

        if (!granted)
        {
            ImGui.Spacing();
            CenteredWrappedText(Lang.OwnerVerifyDeniedHint, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.75f), 200f);
        }
    }

    private static float ContentCenterX()
    {
        var winPos = ImGui.GetWindowPos();
        var min = ImGui.GetWindowContentRegionMin();
        var max = ImGui.GetWindowContentRegionMax();
        return winPos.X + (min.X + max.X) / 2f;
    }

    private static float ContentCenterLocalX()
    {
        var min = ImGui.GetWindowContentRegionMin();
        var max = ImGui.GetWindowContentRegionMax();
        return (min.X + max.X) / 2f;
    }

    private static void CenteredWrappedText(string text, Vector4 color, float wrapWidth)
    {
        foreach (var line in UIConstants.WrapText(text, wrapWidth))
        {
            var w = ImGui.CalcTextSize(line).X;
            ImGui.SetCursorPosX(ContentCenterLocalX() - w / 2f);
            ImGui.TextColored(color, line);
        }
    }

    private static readonly Vector2[] GlowOffsets =
    [
        new(-1.4f, 0), new(1.4f, 0), new(0, -1.4f), new(0, 1.4f),
        new(-1f, -1f), new(1f, -1f), new(-1f, 1f), new(1f, 1f),
    ];

    private static void DrawGlowText(ImDrawListPtr dl, Vector2 pos, Vector4 color, string text)
    {
        var haloCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(color, 0.35f));
        foreach (var off in GlowOffsets)
            dl.AddText(pos + off, haloCol, text);
    }

    private static void DrawGlowText(ImDrawListPtr dl, Vector2 pos, Vector4 color, string text, ImFontPtr font, float fontSize)
    {
        var haloCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(color, 0.35f));
        foreach (var off in GlowOffsets)
            dl.AddText(font, fontSize, pos + off, haloCol, text);
    }

    private static void DrawScanRing(float elapsed, Vector4 color, bool scanning, FontAwesomeIcon icon, Vector2 shakeOffset = default)
    {
        const float radius = Radius;
        var cx = ContentCenterX();
        var cy = ImGui.GetCursorScreenPos().Y + radius + 6f;
        var layoutCenter = new Vector2(cx, cy);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, RingBlockHeight));

        var center = layoutCenter + shakeOffset;

        var dl = ImGui.GetWindowDrawList();

        dl.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(color, 0.35f)), 64, 1.5f);

        if (scanning)
        {
            const float arcSpan = 1.7f;
            var start = elapsed * 2.6f;
            dl.PathArcTo(center, radius, start, start + arcSpan, 48);
            dl.PathStroke(ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(color, 0.9f)), ImDrawFlags.None, 3f);
        }
        else
        {
            dl.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(color), 64, 3f);
        }

        var iconFont = UiBuilder.IconFont;
        var iconStr = icon.ToIconString();
        var iconSize = 26f;
        var scale = iconSize / iconFont.FontSize;

        ImGui.PushFont(iconFont);
        Vector2 glyphMin, glyphMax;
        unsafe
        {
            var glyph = iconFont.FindGlyph(iconStr[0]);
            glyphMin = new Vector2(glyph->X0, glyph->Y0) * scale;
            glyphMax = new Vector2(glyph->X1, glyph->Y1) * scale;
        }
        ImGui.PopFont();

        var glyphInkCenter = (glyphMin + glyphMax) / 2f;
        var glyphPos = center - glyphInkCenter;

        DrawGlowText(dl, glyphPos, color, iconStr, iconFont, iconSize);
        dl.AddText(iconFont, iconSize, glyphPos, ImGui.ColorConvertFloat4ToU32(color), iconStr);
    }

    private static void DrawSpacedHeader(string text, Vector4 color, float spacing)
    {
        var dl = ImGui.GetWindowDrawList();
        var widths = new float[text.Length];
        var totalW = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            widths[i] = ImGui.CalcTextSize(text[i].ToString()).X;
            totalW += widths[i];
        }
        totalW += spacing * MathF.Max(0, text.Length - 1);

        var x = ContentCenterX() - totalW / 2f;
        var y = ImGui.GetCursorScreenPos().Y;
        var col = ImGui.ColorConvertFloat4ToU32(color);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i].ToString();
            var pos = new Vector2(x, y);
            DrawGlowText(dl, pos, color, ch);
            dl.AddText(pos, col, ch);
            x += widths[i] + spacing;
        }
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight()));
    }

    private static void CenteredText(string text, Vector4 color)
    {
        var w = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - w) / 2f + ImGui.GetCursorPosX());
        var pos = ImGui.GetCursorScreenPos();
        DrawGlowText(ImGui.GetWindowDrawList(), pos, color, text);
        ImGui.TextColored(color, text);
    }

    public void Dispose() { }
}
