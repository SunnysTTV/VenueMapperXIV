using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public enum BannerSource { None, Venue, FfxivVenues, GameArt }

public static class VenueBanner
{
    public static (IDalamudTextureWrap? Texture, BannerSource Source) Get(VenueMapperPlugin plugin, Venue venue)
    {
        if (!string.IsNullOrWhiteSpace(venue.Banner))
        {
            var own = plugin.VenueLogos.Get(venue.Banner);
            if (own != null) return (own, BannerSource.Venue);
        }

        var xivId = VenueMapWindow.ExtractXivVenuesId(venue.Links?.FfxivVenues);
        if (xivId != null)
        {
            plugin.XivVenues.RequestSchedule(xivId);
            var xiv = plugin.VenueLogos.Get(plugin.XivVenues.GetBannerUri(xivId));
            if (xiv != null) return (xiv, BannerSource.FfxivVenues);
        }

        var district = plugin.PositionTracker.GetVenueDistrict(venue);
        var art = GameArt(plugin, HousingData.WardTerritory(district));
        return art != null ? (art, BannerSource.GameArt) : (null, BannerSource.None);
    }

    public static IDalamudTextureWrap? GameArt(VenueMapperPlugin plugin, uint territoryId)
    {
        var path = plugin.HousingData.LoadingImagePath(territoryId);
        if (path == null) return null;
        return VenueMapperPlugin.TextureProvider.GetFromGame(path).TryGetWrap(out var wrap, out _) ? wrap : null;
    }

    public static (Vector2 Uv0, Vector2 Uv1) CoverUv(IDalamudTextureWrap texture, Vector2 size)
    {
        var uv0 = Vector2.Zero;
        var uv1 = Vector2.One;
        if (texture.Width <= 0 || texture.Height <= 0 || size.X <= 0 || size.Y <= 0) return (uv0, uv1);

        var texAspect = (float)texture.Width / texture.Height;
        var rectAspect = size.X / size.Y;
        if (texAspect > rectAspect)
        {
            var visible = rectAspect / texAspect;
            uv0.X = (1f - visible) / 2f;
            uv1.X = 1f - uv0.X;
        }
        else
        {
            var visible = texAspect / rectAspect;
            uv0.Y = (1f - visible) / 2f;
            uv1.Y = 1f - uv0.Y;
        }
        return (uv0, uv1);
    }

    public static void DrawCover(ImDrawListPtr dl, IDalamudTextureWrap texture, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFF)
    {
        var (uv0, uv1) = CoverUv(texture, max - min);
        dl.AddImage(texture.Handle, min, max, uv0, uv1, tint);
    }

    public static void DrawFade(ImDrawListPtr dl, Vector2 min, Vector2 max, float startFraction = 0.35f)
    {
        var clear = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0f));
        var solid = ImGui.ColorConvertFloat4ToU32(UIConstants.Background);
        var fadeTop = min.Y + (max.Y - min.Y) * startFraction;
        dl.AddRectFilledMultiColor(new Vector2(min.X, fadeTop), max, clear, clear, solid, solid);
    }

    public static BannerSource Draw(VenueMapperPlugin plugin, Venue venue, ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var accent = venue.Colors?.PrimaryVec ?? UIConstants.Accent;
        var (texture, source) = Get(plugin, venue);

        dl.PushClipRect(min, max, true);
        if (texture != null)
        {
            DrawCover(dl, texture, min, max);
            if (source == BannerSource.GameArt)
                dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.22f)));
        }
        else
        {
            dl.AddRectFilledMultiColor(min, max,
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(accent, 0.45f)),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(venue.Colors?.SecondaryVec ?? accent, 0.3f)),
                ImGui.ColorConvertFloat4ToU32(UIConstants.PanelRaised),
                ImGui.ColorConvertFloat4ToU32(UIConstants.PanelRaised));
        }
        DrawFade(dl, min, max);

        if (source == BannerSource.FfxivVenues)
        {
            var label = "FFXIVVenues";
            var size = ImGui.CalcTextSize(label);
            var pos = new Vector2(max.X - size.X - 8f, min.Y + 6f);
            dl.AddRectFilled(pos - new Vector2(4f, 1f), pos + size + new Vector2(4f, 1f),
                ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0.6f)), 2f);
            dl.AddText(pos, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.7f)), label);
        }
        dl.PopClipRect();
        return source;
    }
}
