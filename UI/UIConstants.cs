using System.Numerics;

namespace VenueMapper.UI;

public static class UIConstants
{
    public static readonly Vector4 Background      = HexToVec4("#0a0e27");
    public static readonly Vector4 Primary         = HexToVec4("#FF006E"); // Magenta
    public static readonly Vector4 Glow            = HexToVec4("#00F5FF"); // Cyan
    public static readonly Vector4 Secondary       = HexToVec4("#9D4EDD"); // Purple
    public static readonly Vector4 TextPrimary     = HexToVec4("#FFFFFF");
    public static readonly Vector4 TextSecondary   = HexToVec4("#B0B0B0");
    public static readonly Vector4 CardBackground  = HexToVec4("#1a1f3a");

    public static readonly Vector4 GlowDim         = WithAlpha(Glow, 0.35f);
    public static readonly Vector4 GlowBright      = WithAlpha(Glow, 1.0f);
    public static readonly Vector4 PrimaryDim      = WithAlpha(Primary, 0.6f);
    public static readonly Vector4 PrimaryHover    = Lighten(Primary, 0.15f);

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

    public static Vector4 Lighten(Vector4 color, float amount) => new(
        System.MathF.Min(1f, color.X + amount),
        System.MathF.Min(1f, color.Y + amount),
        System.MathF.Min(1f, color.Z + amount),
        color.W);
}
