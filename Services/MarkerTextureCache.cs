using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using VenueMapper.UI;

namespace VenueMapper.Services;

public sealed class MarkerTextureCache : IDisposable
{
    private const int MaxEntries = 160;
    public const float Supersample = 2f;
    public const float FontPx = 22f * Supersample;
    private const float K = Supersample;
    public const float BadgeTexture = 64f * K;

    public static readonly ushort[] GlyphRanges = [0x0020, 0x024F, 0x0370, 0x03FF, 0x0400, 0x04FF, 0x2000, 0x206F, 0x20A0, 0x20CF, 0x2100, 0x214F, 0x2190, 0x21FF, 0x2600, 0x26FF, 0];

    private readonly ITextureProvider textureProvider;
    private readonly IPluginLog log;
    private readonly IFontHandle textFont;
    private readonly IFontHandle iconFont;
    private readonly Dictionary<string, IDalamudTextureWrap?> cache = new();

    public MarkerTextureCache(ITextureProvider textureProvider, IPluginLog log, IFontHandle textFont, IFontHandle iconFont)
    {
        this.textureProvider = textureProvider;
        this.log = log;
        this.textFont = textFont;
        this.iconFont = iconFont;
    }

    public int Count => cache.Count;

    public static Vector2 DisplaySize(IDalamudTextureWrap wrap, float scale) => new Vector2(wrap.Width, wrap.Height) / K * scale;

    private bool FontsReady => textFont.Available && iconFont.Available;

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    private static (ImFontPtr Font, float Size) Resolve(IFontHandle handle)
    {
        using (handle.Push())
            return (ImGui.GetFont(), ImGui.GetFontSize());
    }

    private static Vector2 Measure(ImFontPtr font, float size, string text)
    {
        using (ImRaii.PushFont(font))
            return ImGui.CalcTextSize(text) * (size / ImGui.GetFontSize());
    }

    private static void DrawIcon(ImDrawListPtr dl, ImFontPtr font, float size, Vector2 center, FontAwesomeIcon icon, Vector4 color)
    {
        UIConstants.DrawIconCentered(dl, font, size, center, icon.ToIconString(), U32(color));
    }

    public IDalamudTextureWrap? Badge(FontAwesomeIcon icon, Vector4 color, bool strong, bool solid = false)
    {
        var key = $"b{U32(color):X8}|{(int)icon}|{strong}|{solid}";
        if (TryGet(key, out var existing)) return existing;
        if (!FontsReady) return null;

        var (glyphFont, glyphSize) = Resolve(iconFont);
        return Composite(key, new Vector2(BadgeTexture, BadgeTexture), dl =>
        {
            var center = new Vector2(BadgeTexture / 2f);
            var radius = BadgeTexture / 2f - 3f * K;
            if (solid)
            {
                var ink = UIConstants.OnColor(color);
                dl.AddCircleFilled(center, radius + 1f * K, U32(UIConstants.WithAlpha(ink, 0.9f)), 64);
                dl.AddCircleFilled(center, radius - 1.5f * K, U32(color), 64);
                DrawIcon(dl, glyphFont, glyphSize * 1.15f, center, icon, ink);
                return;
            }
            if (strong)
                dl.AddCircleFilled(center, radius + 2f * K, U32(UIConstants.WithAlpha(color, 0.35f)), 64);
            dl.AddCircleFilled(center, radius, U32(UIConstants.WithAlpha(UIConstants.Background, 0.9f)), 64);
            dl.AddCircle(center, radius - 2f * K, U32(color), 64, (strong ? 5f : 3.5f) * K);
            if (UIConstants.IsDark(color))
                dl.AddCircle(center, radius - 0.5f * K, U32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.5f)), 64, 1.2f * K);
            DrawIcon(dl, glyphFont, glyphSize * 1.15f, center, icon, UIConstants.Readable(color));
        });
    }

    public IDalamudTextureWrap? Pill(FontAwesomeIcon icon, string text, Vector4 color, bool strong, bool solid = false)
    {
        var key = $"l{U32(color):X8}|{(int)icon}|{strong}|{solid}|{text}";
        if (TryGet(key, out var existing)) return existing;
        if (string.IsNullOrEmpty(text) || !FontsReady) return null;

        var (font, fontSize) = Resolve(textFont);
        var (glyphFont, glyphSize) = Resolve(iconFont);
        var textSize = Measure(font, fontSize, text);
        var pad = 6f * K;
        var circle = MathF.Ceiling(textSize.Y + 8f * K);
        var height = circle + pad;
        var width = MathF.Ceiling(pad / 2f + circle + 8f * K + textSize.X + 14f * K);

        return Composite(key, new Vector2(width, height), dl =>
        {
            var max = new Vector2(width, height);
            var r = height / 2f;
            var circleCenter = new Vector2(pad / 2f + circle / 2f + 1f * K, height / 2f);
            var textPos = new Vector2(pad / 2f + circle + 8f * K, (height - textSize.Y) / 2f);
            if (solid)
            {
                var ink = UIConstants.OnColor(color);
                dl.AddRectFilled(Vector2.Zero, max, U32(UIConstants.WithAlpha(ink, 0.9f)), r);
                dl.AddRectFilled(new Vector2(2f * K), max - new Vector2(2f * K), U32(color), r - 2f * K);
                DrawIcon(dl, glyphFont, glyphSize, circleCenter, icon, ink);
                dl.AddText(font, fontSize, textPos, U32(ink), text);
                return;
            }
            if (strong)
                dl.AddRectFilled(Vector2.Zero, max, U32(UIConstants.WithAlpha(color, 0.3f)), r);
            var inset = strong ? 2f * K : 0f;
            dl.AddRectFilled(new Vector2(inset), max - new Vector2(inset), U32(UIConstants.WithAlpha(UIConstants.Background, 0.9f)), r - inset);
            var edge = UIConstants.IsDark(color) ? UIConstants.WithAlpha(UIConstants.TextPrimary, 0.5f) : UIConstants.WithAlpha(color, 0.85f);
            dl.AddRect(new Vector2(inset + 1f * K), max - new Vector2(inset + 1f * K), U32(edge), r - inset, ImDrawFlags.None, 2f * K);

            dl.AddCircleFilled(circleCenter, circle / 2f - 1f * K, U32(UIConstants.WithAlpha(color, 0.22f)), 48);
            DrawIcon(dl, glyphFont, glyphSize, circleCenter, icon, UIConstants.Readable(color));
            dl.AddText(font, fontSize, textPos, U32(UIConstants.TextPrimary), text);
        });
    }

    public IDalamudTextureWrap? VenuePin(string name, string? detail, Vector4 detailColor, Vector4 accent, Vector4 secondary, bool open, bool favorite,
        string initials, float alpha)
    {
        var key = $"v{U32(accent):X8}|{U32(secondary):X8}|{U32(detailColor):X8}|{open}|{favorite}|{initials}|{alpha:0.00}|{name}|{detail}";
        if (TryGet(key, out var existing)) return existing;
        if (!FontsReady) return null;

        var (font, fontSize) = Resolve(textFont);
        var (glyphFont, glyphSize) = Resolve(iconFont);
        var small = fontSize * 0.82f;
        var nameSize = Measure(font, fontSize, name);
        var detailSize = detail != null ? Measure(font, small, detail) : Vector2.Zero;
        var padX = 10f * K;
        var padY = 4f * K;
        var bar = 3f * K;
        var cardW = MathF.Ceiling(MathF.Max(nameSize.X, detailSize.X) + padX * 2f + bar);
        var cardH = MathF.Ceiling(nameSize.Y + (detail != null ? detailSize.Y : 0f) + padY * 2f);
        var radius = 25f * K;
        var glow = 6f * K;
        var pointer = 9f * K;
        var gap = 6f * K;
        var width = MathF.Ceiling(MathF.Max(cardW, (radius + glow) * 2f) + 4f * K);
        var height = MathF.Ceiling(cardH + gap + (radius + glow) * 2f + pointer);

        return Composite(key, new Vector2(width, height), dl =>
        {
            var cardMin = new Vector2(MathF.Round((width - cardW) / 2f), 0f);
            var cardMax = cardMin + new Vector2(cardW, cardH);
            dl.AddRectFilled(cardMin, cardMax, U32(UIConstants.WithAlpha(UIConstants.Background, 0.88f * alpha)), 4f * K);
            dl.AddRect(cardMin, cardMax, U32(UIConstants.WithAlpha(accent, 0.5f * alpha)), 4f * K, ImDrawFlags.None, 1f * K);
            dl.AddRectFilledMultiColor(cardMin + new Vector2(1f * K, 2f * K), new Vector2(cardMin.X + bar + 1f * K, cardMax.Y - 2f * K),
                U32(UIConstants.WithAlpha(accent, alpha)), U32(UIConstants.WithAlpha(accent, alpha)),
                U32(UIConstants.WithAlpha(secondary, alpha)), U32(UIConstants.WithAlpha(secondary, alpha)));
            var textX = cardMin.X + bar + padX;
            var textW = cardW - bar - padX * 2f;
            dl.AddText(font, fontSize, new Vector2(textX + (textW - nameSize.X) / 2f, cardMin.Y + padY), U32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), name);
            if (detail != null)
                dl.AddText(font, small, new Vector2(textX + (textW - detailSize.X) / 2f, cardMin.Y + padY + nameSize.Y), U32(UIConstants.WithAlpha(detailColor, alpha)), detail);

            var center = new Vector2(width / 2f, cardH + gap + glow + radius);
            var tip = new Vector2(width / 2f, height - 1f);
            dl.AddCircleFilled(center, radius + glow, U32(UIConstants.WithAlpha(accent, (open ? 0.28f : 0.16f) * alpha)), 64);
            dl.AddTriangleFilled(center + new Vector2(-7f * K, radius - 3f * K), center + new Vector2(7f * K, radius - 3f * K), tip,
                U32(UIConstants.WithAlpha(accent, 0.95f * alpha)));
            dl.AddCircleFilled(center, radius, U32(UIConstants.WithAlpha(UIConstants.Background, 0.92f * alpha)), 64);
            dl.AddCircle(center, radius - 1.5f * K, U32(UIConstants.WithAlpha(accent, alpha)), 64, 3f * K);
            dl.AddCircle(center, radius - 5f * K, U32(UIConstants.WithAlpha(secondary, 0.55f * alpha)), 64, 1.2f * K);
            if (initials.Length > 0)
            {
                var letters = Measure(font, fontSize, initials);
                var fit = MathF.Min(radius * 1.05f / MathF.Max(letters.Y, 1f), radius * 1.3f / MathF.Max(letters.X, 1f));
                var letterSize = fontSize * fit;
                var drawn = letters * fit;
                var at = new Vector2(MathF.Round(center.X - drawn.X / 2f), MathF.Round(center.Y - drawn.Y / 2f));
                dl.AddText(font, letterSize, at, U32(UIConstants.WithAlpha(accent, alpha)), initials);
            }
            if (open)
            {
                var dot = center + new Vector2(radius * 0.72f, radius * 0.72f);
                dl.AddCircleFilled(dot, 5.5f * K, U32(UIConstants.WithAlpha(UIConstants.Background, alpha)), 24);
                dl.AddCircleFilled(dot, 4f * K, U32(UIConstants.WithAlpha(UIConstants.Success, alpha)), 24);
            }
            if (favorite)
                DrawIcon(dl, glyphFont, glyphSize * 0.8f, center + new Vector2(radius * 0.74f, -radius * 0.74f), FontAwesomeIcon.Star,
                    UIConstants.WithAlpha(UIConstants.GoldAccent, alpha));
        });
    }

    private IDalamudTextureWrap? Composite(string key, Vector2 size, Action<ImDrawListPtr> draw)
    {
        if (TryGet(key, out var wrap)) return wrap;
        Evict();

        try
        {
            using var dd = BufferBackedImDrawData.Create();
            draw(dd.ListPtr);
            dd.Data.DisplayPos = Vector2.Zero;
            dd.Data.DisplaySize = size;
            dd.Data.Valid = 1;
            dd.UpdateDrawDataStatistics();

            var texture = textureProvider.CreateDrawListTexture("VenueMapper marker texture");
            texture.Size = size;
            texture.Draw(dd.DataPtr);
            wrap = texture;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Marker texture failed: {ex.Message}");
            wrap = null;
        }

        cache[key] = wrap;
        lastUsed[key] = ImGui.GetFrameCount();
        return wrap;
    }

    private const int RetireFrames = 4;
    private readonly Dictionary<string, int> lastUsed = new();
    private readonly List<(IDalamudTextureWrap Wrap, int Frame)> retired = new();

    private bool TryGet(string key, out IDalamudTextureWrap? wrap)
    {
        if (!cache.TryGetValue(key, out wrap)) return false;
        lastUsed[key] = ImGui.GetFrameCount();
        return true;
    }

    private void Evict()
    {
        if (cache.Count < MaxEntries) return;
        var frame = ImGui.GetFrameCount();
        var stale = lastUsed.Where(kv => frame - kv.Value > RetireFrames)
            .OrderBy(kv => kv.Value)
            .Take(Math.Max(1, cache.Count - MaxEntries * 3 / 4))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in stale)
            Retire(key);
    }

    private void Retire(string key)
    {
        if (cache.Remove(key, out var wrap) && wrap != null)
            retired.Add((wrap, ImGui.GetFrameCount()));
        lastUsed.Remove(key);
    }

    public void Tick()
    {
        if (retired.Count == 0) return;
        var frame = ImGui.GetFrameCount();
        retired.RemoveAll(r =>
        {
            if (frame - r.Frame <= RetireFrames) return false;
            r.Wrap.Dispose();
            return true;
        });
    }

    public void Clear()
    {
        foreach (var key in cache.Keys.ToList())
            Retire(key);
    }

    public void Dispose()
    {
        foreach (var wrap in cache.Values)
            wrap?.Dispose();
        cache.Clear();
        lastUsed.Clear();
        foreach (var (wrap, _) in retired)
            wrap.Dispose();
        retired.Clear();
    }
}
