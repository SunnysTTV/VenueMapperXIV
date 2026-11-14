using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VenueMapper.UI;

public static class HudIcons
{
    public static void Aetheryte(ImDrawListPtr dl, Vector2 center, float size, uint col, float thickness = 1.4f)
    {
        var top = center + new Vector2(0, -0.48f * size);
        var right = center + new Vector2(0.16f * size, -0.12f * size);
        var bottom = center + new Vector2(0, 0.22f * size);
        var left = center + new Vector2(-0.16f * size, -0.12f * size);
        dl.AddQuad(top, right, bottom, left, col, thickness);
        Ellipse(dl, center + new Vector2(0, 0.34f * size), 0.38f * size, 0.1f * size, col, thickness);
    }

    public static void Crystal(ImDrawListPtr dl, Vector2 center, float size, uint col, float thickness = 1.4f)
    {
        var top = center + new Vector2(0, -0.5f * size);
        var right = center + new Vector2(0.26f * size, -0.12f * size);
        var bottom = center + new Vector2(0, 0.5f * size);
        var left = center + new Vector2(-0.26f * size, -0.12f * size);
        dl.AddQuad(top, right, bottom, left, col, thickness);
        dl.AddLine(left, right, col, thickness);
        dl.AddLine(top, bottom, col, thickness);
    }

    private static void Ellipse(ImDrawListPtr dl, Vector2 c, float rx, float ry, uint col, float thickness)
    {
        const int segments = 20;
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * MathF.PI * 2f;
            dl.PathLineTo(c + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry));
        }
        dl.PathStroke(col, ImDrawFlags.Closed, thickness);
    }
}
