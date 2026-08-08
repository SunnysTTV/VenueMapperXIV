using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Pictomancy;
using VenueMapper.Models;
using VenueMapper.UI;

namespace VenueMapper.Services;

public class MarkerAccessibilityOptions
{
    public float SizeScale = 1.0f;
    public bool ColorOverrideEnabled;
    public Vector3 OverrideColor = new(1f, 0f, 1f);
    public bool StrongPulse;
}

public class PictomancyMarkerManager : IDisposable
{
    private readonly IPluginLog log;
    private PctContext? ctx;

    public bool Enabled { get; set; } = true;
    public bool Available => ctx != null;

    public PictomancyMarkerManager(IDalamudPluginInterface pi, IPluginLog log)
    {
        this.log = log;
        try
        {
            ctx = PctService.Initialize(pi);
            log.Information("[VenueMapper] Pictomancy initialized");
        }
        catch (Exception ex)
        {
            log.Warning($"[VenueMapper] Pictomancy not available: {ex.Message}");
            ctx = null;
        }
    }

    public void DrawMarkers(Floor? floor, VenueColors? colors, Dictionary<string, bool>? filters = null, MarkerAccessibilityOptions? access = null)
    {
        if (!Enabled || ctx == null || floor == null) return;
        access ??= new MarkerAccessibilityOptions();

        try
        {
            using var drawList = PctService.Draw(hints: new PctDrawHints
            {
                DefaultParams = new PctDxParams
                {
                    OccludedAlpha = 0.3f,
                    OcclusionTolerance = 0.5f,
                },
            });
            if (drawList == null) return;

            var time = (float)DateTime.Now.TimeOfDay.TotalSeconds;
            var scale = access.SizeScale;
            var pulseSpeed = access.StrongPulse ? 4f : 2f;
            var pulseAmp = access.StrongPulse ? 0.4f : 0.25f;
            var blackU32 = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f));
            var whiteU32 = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f));

            foreach (var svc in floor.Services)
            {
                if (filters != null && filters.TryGetValue(svc.Type, out var visible) && !visible)
                    continue;

                var worldPos = new Vector3(svc.X, svc.Z, svc.Y);
                var col = access.ColorOverrideEnabled
                    ? new Vector4(access.OverrideColor.X, access.OverrideColor.Y, access.OverrideColor.Z, 1f)
                    : GetServiceColor(svc.Type, colors);
                var colU32 = ImGui.ColorConvertFloat4ToU32(col);

                drawList.AddCircle(worldPos, 0.6f * scale, colU32, 24, 2f * scale);

                var pulse = (MathF.Sin(time * pulseSpeed) + 1f) / 2f;
                var pulseCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(col, 0.25f + pulseAmp * pulse));
                drawList.AddCircle(worldPos, (0.8f + 0.3f * pulse) * scale, pulseCol, 24, 1.5f * scale);

                var fillCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(col, 0.15f));
                drawList.AddCircleFilled(worldPos, 0.5f * scale, fillCol);

                var beamBot = worldPos;
                var beamTop = new Vector3(worldPos.X, worldPos.Y + 2.5f, worldPos.Z);
                var beamCol = ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(col, 0.12f + 0.08f * pulse));
                drawList.AddLine(beamBot, beamTop, beamCol, (uint)MathF.Max(1, 3 * scale));
                drawList.AddLine(beamBot, beamTop, colU32, (uint)MathF.Max(1, 1 * scale));

                drawList.AddDot(worldPos, 6f * scale, colU32, colU32);

                var labelPos = new Vector3(worldPos.X, worldPos.Y + 2.0f, worldPos.Z);
                var bgCol = access.ColorOverrideEnabled
                    ? colU32
                    : ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Background, 0.7f));
                drawList.AddDot(labelPos, 40f * scale, bgCol, bgCol);

                var textCol = access.ColorOverrideEnabled
                    ? (Luminance(col) > 0.5f ? blackU32 : whiteU32)
                    : colU32;
                drawList.AddText(labelPos, textCol, svc.Label);
            }
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Pictomancy render: {ex.Message}");
        }
    }

    private static float Luminance(Vector4 col) => 0.299f * col.X + 0.587f * col.Y + 0.114f * col.Z;

    private static Vector4 GetServiceColor(string type, VenueColors? colors)
    {
        return type switch
        {
            "entrance"      => new Vector4(0.2f, 1f, 0.5f, 1f),
            "bar"           => new Vector4(1f, 0.4f, 0.2f, 1f),
            "dj_booth"      => new Vector4(0f, 0.9f, 1f, 1f),
            "gambling"      => new Vector4(1f, 0.84f, 0f, 1f),
            "upstairs"      => new Vector4(0.7f, 0.7f, 0.7f, 1f),
            "downstairs"    => new Vector4(0.7f, 0.7f, 0.7f, 1f),
            "vip"           => new Vector4(1f, 0.84f, 0f, 1f),
            "bath" or "spa" => new Vector4(0.4f, 0.8f, 1f, 1f),
            "event"         => new Vector4(1f, 0.5f, 0.8f, 1f),
            "stage"         => new Vector4(0.9f, 0.3f, 1f, 1f),
            _               => new Vector4(0.7f, 0.7f, 0.7f, 1f),
        };
    }

    public void Dispose()
    {
        ctx?.Dispose();
        ctx = null;
    }
}
