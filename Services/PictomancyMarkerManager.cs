using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Pictomancy;
using VenueMapper.Models;
using VenueMapper.UI;

namespace VenueMapper.Services;

public enum MarkerNamesMode { Always, Near, Off }

public class MarkerAccessibilityOptions
{
    public float SizeScale = 1.0f;
    public bool ColorOverrideEnabled;
    public Vector3 OverrideColor = new(1f, 0f, 1f);
    public bool StrongPulse;
    public MarkerNamesMode Names = MarkerNamesMode.Near;
    public bool ThroughWalls = true;
    public bool VenueColors;
    public float SignRange = 100f;
    public GuideAnimation GuideAnimation = GuideAnimation.Arrow;
    public GuideColorMode GuideColor = GuideColorMode.Venue;
    public Vector4 GuideSingleColor = new(0.337f, 0.839f, 0.933f, 1f);
}

public readonly record struct MarkerItem(Service Service, Vector4? VenueColor);

public readonly record struct VenueSign(string Id, Vector3 Position, string Name, string? Line2, bool Open, bool Favorite, Vector4 Accent, Vector4 Secondary);

public class PictomancyMarkerManager : IDisposable
{
    private readonly IPluginLog log;
    private readonly MarkerTextureCache textures;
    private PctContext? ctx;

    public bool Enabled { get; set; } = true;
    public bool Available => ctx != null;
    public int CachedTextures => textures.Count;

    public const float FadeNear = 25f;
    public const float FadeFar = 45f;
    private const float BadgeSize = 40f;
    private const float NearDistance = 20f;
    private const int MaxSignNameLength = 32;
    private static readonly PctDxParams ShapeParams = new() { OccludedAlpha = 0.3f, OcclusionTolerance = 0.5f };
    private static readonly PctDxParams HiddenShapeParams = new() { OccludedAlpha = 0f, OcclusionTolerance = 0.5f };
    private static readonly PctDxParams GlowParams = new() { OccludedAlpha = 0f, OcclusionTolerance = 0.5f, ProjectionHeight = 1f };

    public PictomancyMarkerManager(IDalamudPluginInterface pi, ITextureProvider textureProvider, IPluginLog log,
        Dalamud.Interface.ManagedFontAtlas.IFontHandle textFont, Dalamud.Interface.ManagedFontAtlas.IFontHandle iconFont)
    {
        this.log = log;
        textures = new MarkerTextureCache(textureProvider, log, textFont, iconFont);
        try
        {
            ctx = PctService.Initialize(pi, new PctOptions { EnableKtkOutput = true });
            log.Information("[VenueMapper] Pictomancy initialized");
        }
        catch (Exception ex)
        {
            log.Warning($"[VenueMapper] Pictomancy not available: {ex.Message}");
            ctx = null;
        }
    }

    public static float DistanceFade(Service svc, Vector2? origin)
    {
        if (origin is not { } o) return 1f;
        var dx = svc.X - o.X;
        var dz = svc.Y - o.Y;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        return 1f - Math.Clamp((distance - FadeNear) / (FadeFar - FadeNear), 0f, 1f);
    }

    public static Vector4 TypeColor(string type) => type switch
    {
        "entrance"             => new Vector4(0.62f, 0.84f, 0.70f, 1f),
        "bar"                  => new Vector4(0.91f, 0.63f, 0.48f, 1f),
        "dj_booth"             => new Vector4(0.56f, 0.78f, 0.84f, 1f),
        "gambling" or "vip"    => new Vector4(0.89f, 0.76f, 0.49f, 1f),
        "stage" or "event"     => new Vector4(0.79f, 0.65f, 0.88f, 1f),
        "bath" or "spa"        => new Vector4(0.61f, 0.79f, 0.91f, 1f),
        "stall"                => new Vector4(0.91f, 0.77f, 0.54f, 1f),
        "upstairs" or "downstairs" => new Vector4(0.66f, 0.63f, 0.55f, 1f),
        _                      => new Vector4(0.72f, 0.69f, 0.63f, 1f),
    };

    public void Draw(IReadOnlyList<MarkerItem>? items, Dictionary<string, bool>? filters, MarkerAccessibilityOptions access,
        Vector3 viewer, Vector2? fadeOrigin = null, IReadOnlyList<VenueSign>? signs = null, GuideRoute? route = null)
    {
        var hasItems = Enabled && items is { Count: > 0 };
        var hasSigns = Enabled && signs is { Count: > 0 };
        if (ctx == null) return;
        textures.Tick();
        if (!hasItems && !hasSigns && route == null)
        {
            Flush();
            return;
        }

        try
        {
            using var drawList = PctService.Draw(hints: new PctDrawHints
            {
                AutoDraw = BehindGameUi ? AutoDraw.NativeOverlay : AutoDraw.ImGuiOverlay,
                DefaultParams = access.ThroughWalls ? ShapeParams : HiddenShapeParams,
            });
            if (drawList == null)
            {
                Flush();
                return;
            }
            flushFrames = FlushFrameCount;

            var time = (float)(DateTime.Now.TimeOfDay.TotalSeconds % 3600.0);

            if (hasItems)
            {
                foreach (var item in items!)
                {
                    var svc = item.Service;
                    if (filters != null && filters.TryGetValue(svc.Type, out var visible) && !visible)
                        continue;
                    var fade = DistanceFade(svc, fadeOrigin);
                    if (fade <= 0.02f) continue;
                    DrawServiceMarker(drawList, item, access, viewer, fade, time);
                }
            }

            if (hasSigns)
            {
                foreach (var sign in signs!)
                    DrawSign(drawList, sign, access, viewer, time);
            }
            if (route != null)
                DrawRoute(drawList, route, access, viewer, time);
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Pictomancy render: {ex.Message}");
        }
    }

    private const int FlushFrameCount = 4;
    private int flushFrames;

    public void Flush()
    {
        if (ctx == null || flushFrames <= 0) return;
        try
        {
            using var drawList = PctService.Draw(hints: new PctDrawHints
            {
                AutoDraw = BehindGameUi ? AutoDraw.NativeOverlay : AutoDraw.ImGuiOverlay,
                DefaultParams = HiddenShapeParams,
                DrawWhenFaded = true,
                DrawInCutscene = true,
            });
            if (drawList != null) flushFrames--;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Pictomancy flush: {ex.Message}");
        }
    }

    private static uint C(Vector4 color, float alpha)
        => ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(color, Math.Clamp(alpha, 0f, 1f)));

    private static float Phase(string text)
    {
        var hash = 17u;
        foreach (var ch in text)
            hash = hash * 31u + ch;
        return (hash % 1000u) / 1000f;
    }

    private static Vector4 MarkerColor(Vector4 natural, MarkerAccessibilityOptions access)
        => access.ColorOverrideEnabled ? new Vector4(access.OverrideColor, 1f) : natural;

    private void DrawServiceMarker(PctDrawList dl, MarkerItem item, MarkerAccessibilityOptions access, Vector3 viewer, float fade, float time)
    {
        var svc = item.Service;
        var s = access.SizeScale;
        var pos = new Vector3(svc.X, svc.Z, svc.Y);
        var natural = access.VenueColors && item.VenueColor is { } venueColor ? venueColor : TypeColor(svc.Type);
        var col = MarkerColor(natural, access);
        var phase = Phase(svc.Label ?? svc.Type);

        dl.AddCircleFilled(pos, 0.75f * s, C(col, 0.34f * fade), C(col, 0f), 40, GlowParams);
        if (access.StrongPulse)
        {
            var ping = (time / 1.4f + phase) % 1f;
            dl.AddCircle(pos, (0.5f + 0.9f * ping) * s, C(col, (1f - ping) * 0.85f * fade), 48, 2.5f * s);
        }

        if (fade < 0.4f) return;

        var distance = Vector3.Distance(viewer, pos);
        var anchor = pos + new Vector3(0f, 1.5f, 0f);
        var spriteParams = new PctDxParams
        {
            OccludedAlpha = access.ThroughWalls ? 0.35f : 0f,
            OcclusionTolerance = 0.6f,
            FadeStart = 40f,
            FadeStop = 60f,
        };

        var icon = UI.ServiceIcons.GetIcon(svc.Type);
        var showName = !string.IsNullOrEmpty(svc.Label)
                       && (access.Names == MarkerNamesMode.Always || (access.Names == MarkerNamesMode.Near && distance <= NearDistance));
        var pill = showName ? textures.Pill(icon, svc.Label!, col, access.StrongPulse, access.ColorOverrideEnabled) : null;
        if (pill != null)
        {
            dl.AddSprite(pill, anchor, MarkerTextureCache.DisplaySize(pill, s), Vector2.Zero, spriteParams);
            return;
        }

        var badgePx = BadgeSize * s;
        var badge = textures.Badge(icon, col, access.StrongPulse, access.ColorOverrideEnabled);
        if (badge != null)
            dl.AddSprite(badge, anchor, new Vector2(badgePx, badgePx), Vector2.Zero, spriteParams);
        else
            dl.AddDot(anchor, badgePx / 2f, C(col, fade));
    }

    private void DrawSign(PctDrawList dl, VenueSign sign, MarkerAccessibilityOptions access, Vector3 viewer, float time)
    {
        var flat = new Vector2(sign.Position.X - viewer.X, sign.Position.Z - viewer.Z).Length();
        if (flat > access.SignRange) return;

        var col = MarkerColor(sign.Accent, access);
        var clean = UIConstants.DisplayName(sign.Name);
        var name = clean.Length > MaxSignNameLength ? clean[..(MaxSignNameLength - 3)].TrimEnd() + "..." : clean;
        var line2Color = sign.Open ? UIConstants.Success : UIConstants.TextSecondary;
        var col2 = access.ColorOverrideEnabled ? col : sign.Secondary;

        var fadeStart = access.SignRange * 0.8f;
        var fadeStop = access.SignRange * 1.25f;
        DrawVenuePin(dl, sign, access, viewer, col, col2, line2Color, name, fadeStart, fadeStop);
    }

    public static float PinScale(float distance) => Math.Clamp(0.8f + distance / 400f, 0.8f, 1.05f);

    public static int RoundedDistance(float distance)
        => (int)(distance < 50f ? MathF.Max(5f, MathF.Round(distance / 5f) * 5f) : MathF.Round(distance / 10f) * 10f);

    private void DrawVenuePin(PctDrawList dl, VenueSign sign, MarkerAccessibilityOptions access, Vector3 viewer, Vector4 col, Vector4 col2,
        Vector4 line2Color, string name, float fadeStart, float fadeStop)
    {
        var distance = Vector3.Distance(viewer, sign.Position);
        var rounded = RoundedDistance(distance);
        var detail = sign.Line2 != null ? $"{sign.Line2} · {rounded} y" : $"{rounded} y";
        var initials = UI.PassportView.Initials(name);
        var alpha = sign.Open || sign.Favorite ? 1f : 0.8f;
        var pin = textures.VenuePin(name, detail, line2Color, UIConstants.Readable(col), UIConstants.Readable(col2), sign.Open, sign.Favorite, initials, alpha);
        if (pin == null) return;
        var scale = PinScale(distance) * access.SizeScale;
        var size = MarkerTextureCache.DisplaySize(pin, scale);
        var pinParams = new PctDxParams
        {
            OccludedAlpha = 1f,
            OcclusionTolerance = 2f,
            FadeStart = fadeStart,
            FadeStop = fadeStop,
        };
        dl.AddSprite(pin, sign.Position, size, new Vector2(0f, -size.Y / 2f), pinParams);
    }

    private static readonly PctDxParams RouteParams = new() { OccludedAlpha = 0.35f, OcclusionTolerance = 1.5f };
    private static readonly Vector3 Up = new(0f, 1f, 0f);

    private static Vector4 Cycle(IReadOnlyList<Vector4> palette, float time, float offset = 0f)
    {
        if (palette.Count == 1) return palette[0];
        var t = time / 2.5f + offset;
        var index = (int)MathF.Floor(t);
        var f = t - index;
        f = f * f * (3f - 2f * f);
        var a = palette[((index % palette.Count) + palette.Count) % palette.Count];
        var b = palette[(((index + 1) % palette.Count) + palette.Count) % palette.Count];
        return Vector4.Lerp(a, b, f);
    }

    private IReadOnlyList<Vector4>? routePalette;

    private Vector4 ParticleColor(Vector4 fallback, int id, float time)
        => routePalette is { Count: > 1 } p ? Cycle(p, time, id * 0.37f) : fallback;

    private void DrawRoute(PctDrawList dl, GuideRoute route, MarkerAccessibilityOptions access, Vector3 viewer, float time)
    {
        routePalette = access.ColorOverrideEnabled || access.GuideColor != GuideColorMode.Venue || route.Palette.Count == 0 ? null : route.Palette;
        var col = access.ColorOverrideEnabled ? new Vector4(access.OverrideColor, 1f)
            : routePalette != null ? Cycle(routePalette, time)
            : access.GuideSingleColor;
        var lift = new Vector3(0f, 0.08f, 0f);
        var goal = route.Goal + lift;

        if (route.Points.Count == 0)
        {
            DrawGoalRing(dl, goal, col, time);
            var toGoal = new Vector3(goal.X - viewer.X, 0f, goal.Z - viewer.Z);
            if (toGoal.LengthSquared() < 0.01f) return;
            var heading = Vector3.Normalize(toGoal);
            var across = new Vector3(-heading.Z, 0f, heading.X);
            var tipAtFeet = viewer + lift + heading * (1.3f + 0.15f * MathF.Sin(time * 3f));
            Chevron(dl, tipAtFeet, heading, across, 0.35f, 0.32f, C(col, 0.95f), 3f * access.SizeScale);
            return;
        }

        var points = new List<Vector3>(route.Points.Count + 1) { viewer + lift };
        for (var i = 0; i < route.Points.Count; i++)
            points.Add(route.Complete && i == route.Points.Count - 1 ? goal : route.Points[i] + lift);
        var lengths = new float[points.Count];
        for (var i = 1; i < points.Count; i++)
            lengths[i] = lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        var total = lengths[^1];

        switch (access.GuideAnimation)
        {
            case GuideAnimation.Sparkles:
                BaseLine(dl, points, col, 0.05f, 0.22f);
                DrawSparkles(dl, points, lengths, total, col, time);
                break;
            case GuideAnimation.Pulse:
                BaseLine(dl, points, col, 0.06f, 0.3f);
                DrawPulse(dl, points, lengths, total, col, time);
                break;
            case GuideAnimation.Dots:
                DrawDots(dl, points, lengths, total, col, time);
                break;
            default:
                BaseLine(dl, points, col, 0.1f, 0.55f);
                DrawArrows(dl, points, lengths, total, col, time, access.SizeScale);
                break;
        }

        DrawGoalRing(dl, goal, col, time);

        if (route.WaypointIcon is not { } icon || string.IsNullOrEmpty(route.WaypointText)) return;
        var pill = textures.Pill(icon, route.WaypointText, col, true, access.ColorOverrideEnabled);
        if (pill == null) return;
        var spriteParams = new PctDxParams { OccludedAlpha = 0.6f, OcclusionTolerance = 1f };
        dl.AddSprite(pill, route.Goal + new Vector3(0f, 2.4f, 0f), MarkerTextureCache.DisplaySize(pill, access.SizeScale), Vector2.Zero, spriteParams);
    }

    private static void BaseLine(PctDrawList dl, List<Vector3> points, Vector4 col, float glow, float core)
    {
        foreach (var p in points)
            dl.PathLineTo(p);
        dl.PathStroke(C(col, glow), PctStrokeFlags.None, 8f, RouteParams);
        foreach (var p in points)
            dl.PathLineTo(p);
        dl.PathStroke(C(col, core), PctStrokeFlags.None, 2f, RouteParams);
    }

    private static (Vector3 Pos, Vector3 Dir) PointAt(List<Vector3> points, float[] lengths, float d)
    {
        var seg = 1;
        while (seg < points.Count - 1 && lengths[seg] < d) seg++;
        var segLength = MathF.Max(0.01f, lengths[seg] - lengths[seg - 1]);
        var pos = Vector3.Lerp(points[seg - 1], points[seg], Math.Clamp((d - lengths[seg - 1]) / segLength, 0f, 1f));
        var dir = points[seg] - points[seg - 1];
        dir.Y = 0f;
        return (pos, dir.LengthSquared() > 0.0001f ? Vector3.Normalize(dir) : Vector3.UnitZ);
    }

    private static IEnumerable<(float Distance, float FromGoal, int Id)> FlowToGoal(float total, float spacing, float speed, float time)
    {
        var travelled = time * speed;
        var offset = travelled % spacing;
        var cycle = (int)(travelled / spacing);
        var k = 0;
        for (var fromGoal = spacing - offset; fromGoal < total; fromGoal += spacing, k++)
            yield return (total - fromGoal, fromGoal, cycle + k);
    }

    private static float EndFade(float distance, float fromGoal, float near, float far)
        => Math.Clamp(MathF.Min((distance - near) / 0.8f, (fromGoal - far) / 0.6f), 0f, 1f);

    private static float Hash(int n, int salt)
    {
        var v = MathF.Sin(n * 12.9898f + salt * 78.233f) * 43758.5453f;
        return v - MathF.Floor(v);
    }

    private static void Chevron(PctDrawList dl, Vector3 tip, Vector3 dir, Vector3 side, float length, float width, uint color, float thickness)
    {
        var back = tip - dir * length;
        dl.PathLineTo(back + side * width);
        dl.PathLineTo(tip);
        dl.PathLineTo(back - side * width);
        dl.PathStroke(color, PctStrokeFlags.None, thickness, RouteParams);
    }

    private static void DrawArrows(PctDrawList dl, List<Vector3> points, float[] lengths, float total, Vector4 col, float time, float scale)
    {
        foreach (var (d, fromGoal, _) in FlowToGoal(total, 5.5f, 1.6f, time))
        {
            var fade = EndFade(d, fromGoal, 0.5f, 0.4f);
            if (fade <= 0f) continue;
            var (tip, dir) = PointAt(points, lengths, d);
            Chevron(dl, tip, dir, new Vector3(-dir.Z, 0f, dir.X), 0.3f, 0.28f, C(col, 0.95f * fade), 3f * scale);
        }
    }

    private void DrawSparkles(PctDrawList dl, List<Vector3> points, float[] lengths, float total, Vector4 col, float time)
    {
        var white = new Vector4(1f, 1f, 1f, 1f);
        foreach (var (d, fromGoal, id) in FlowToGoal(total, 0.8f, 1.1f, time))
        {
            var fade = EndFade(d, fromGoal, 0.3f, 0.2f);
            if (fade <= 0f) continue;
            var (pos, dir) = PointAt(points, lengths, d);
            var side = new Vector3(-dir.Z, 0f, dir.X);
            var twinkle = MathF.Max(0f, MathF.Sin(time * 5f + Hash(id, 3) * MathF.Tau));
            var center = pos + side * ((Hash(id, 1) - 0.5f) * 0.55f) + Up * (0.1f + Hash(id, 2) * 0.55f);
            var r = 0.05f + 0.09f * twinkle;
            var color = C(UIConstants.Mix(ParticleColor(col, id, time), white, 0.5f * twinkle), (0.3f + 0.7f * twinkle) * fade);
            dl.PathLineTo(center - side * r);
            dl.PathLineTo(center + side * r);
            dl.PathStroke(color, PctStrokeFlags.None, 2f, RouteParams);
            dl.PathLineTo(center - Up * r * 1.3f);
            dl.PathLineTo(center + Up * r * 1.3f);
            dl.PathStroke(color, PctStrokeFlags.None, 2f, RouteParams);
        }
    }

    private static void DrawPulse(PctDrawList dl, List<Vector3> points, float[] lengths, float total, Vector4 col, float time)
    {
        const float tail = 2.2f;
        const int pieces = 8;
        foreach (var (head, fromGoal, _) in FlowToGoal(total, 7f, 3f, time))
        {
            var fade = EndFade(head, fromGoal, 0.2f, 0f);
            for (var i = 0; i < pieces; i++)
            {
                var a = head - tail * (i + 1) / pieces;
                var b = head - tail * i / pieces;
                if (b <= 0f) break;
                var strength = 1f - i / (float)pieces;
                dl.PathLineTo(PointAt(points, lengths, MathF.Max(0f, a)).Pos);
                dl.PathLineTo(PointAt(points, lengths, b).Pos);
                dl.PathStroke(C(col, 0.95f * strength * MathF.Max(fade, 0.2f)), PctStrokeFlags.None, 2f + 5f * strength, RouteParams);
            }
        }
    }

    private void DrawDots(PctDrawList dl, List<Vector3> points, float[] lengths, float total, Vector4 col, float time)
    {
        foreach (var (d, fromGoal, id) in FlowToGoal(total, 0.7f, 1f, time))
        {
            var fade = EndFade(d, fromGoal, 0.3f, 0.2f);
            if (fade <= 0f) continue;
            var pos = PointAt(points, lengths, d).Pos;
            var dot = ParticleColor(col, id, time);
            dl.AddCircleFilled(pos, 0.08f, C(dot, 0.95f * fade), C(dot, 0.5f * fade), 12, RouteParams);
        }
    }

    public bool BehindGameUi { get; set; } = true;

    private static void DrawGoalRing(PctDrawList dl, Vector3 goal, Vector4 col, float time)
    {
        var pulse = time * 0.8f % 1f;
        dl.PathArcTo(goal, 0.55f, 0f, MathF.Tau, 36);
        dl.PathStroke(C(col, 0.85f), PctStrokeFlags.Closed, 2.5f, RouteParams);
        dl.PathArcTo(goal, 0.55f + pulse * 0.8f, 0f, MathF.Tau, 40);
        dl.PathStroke(C(col, (1f - pulse) * 0.55f), PctStrokeFlags.Closed, 2f, RouteParams);
    }


    public void Dispose()
    {
        textures.Dispose();
        ctx?.Dispose();
        ctx = null;
    }
}
