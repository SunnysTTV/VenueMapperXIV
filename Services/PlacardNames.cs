using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using VenueMapper.Models;
using VenueMapper.UI;

namespace VenueMapper.Services;

public sealed class PlacardNames : IDisposable
{
    private const uint FallbackPlacardId = 2002736;
    private const int PlotsPerDivision = 30;
    private static readonly TimeSpan RedrawInterval = TimeSpan.FromSeconds(30);

    private readonly VenueMapperPlugin plugin;
    private readonly INamePlateGui namePlateGui;
    private readonly IFramework framework;
    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly HashSet<uint> placardIds;
    private readonly Dictionary<uint, Dictionary<uint, int>> plotsByTerritory = new();
    private DateTime lastRedraw = DateTime.MinValue;
    private bool active;

    public string DebugInfo { get; private set; } = "-";

    public PlacardNames(VenueMapperPlugin plugin, INamePlateGui namePlateGui, IFramework framework, IDataManager data, IPluginLog log)
    {
        this.plugin = plugin;
        this.namePlateGui = namePlateGui;
        this.framework = framework;
        this.data = data;
        this.log = log;
        placardIds = data.GetExcelSheet<EObjName>(ClientLanguage.English)
            .Where(row => row.Singular.ExtractText().Equals("placard", StringComparison.OrdinalIgnoreCase))
            .Select(row => row.RowId)
            .ToHashSet();
        if (placardIds.Count == 0)
            placardIds.Add(FallbackPlacardId);
        namePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
        framework.Update += OnFrameworkUpdate;
    }

    public void Refresh()
    {
        lastRedraw = DateTime.Now;
        namePlateGui.RequestRedraw();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var tracker = plugin.PositionTracker;
        var wanted = plugin.Configuration.PlacardNames && tracker.IsInHousingWard && !tracker.IsInsideHouse;
        if (wanted != active)
        {
            active = wanted;
            Refresh();
            return;
        }
        if (active && DateTime.Now - lastRedraw > RedrawInterval)
        {
            lastRedraw = DateTime.Now;
            namePlateGui.RequestRedraw();
        }
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!active) return;
        try
        {
            var config = plugin.ConfigManager.Config;
            if (config == null) return;
            var tracker = plugin.PositionTracker;
            if (string.IsNullOrEmpty(tracker.CurrentServerName))
            {
                DebugInfo = "world not known yet";
                return;
            }
            var venues = tracker.GetWardVenues(config);
            if (venues.Count == 0)
            {
                DebugInfo = "no venues in this ward";
                return;
            }

            var plots = PlotsFor(tracker.CurrentTerritoryId);
            var seen = 0;
            var named = 0;
            foreach (var handler in handlers)
            {
                if (handler.NamePlateKind != NamePlateKind.EventObject) continue;
                var obj = handler.GameObject;
                if (obj == null || !placardIds.Contains(obj.BaseId)) continue;
                seen++;
                if (!plots.TryGetValue(LayoutId(obj.Address), out var plot)) continue;

                var venue = venues.FirstOrDefault(v => v.Plot == plot);
                if (venue == null || plugin.Configuration.HiddenVenueIds.Contains(venue.VenueId)) continue;
                handler.Name = new SeString(new TextPayload(Label(venue)));
                named++;
            }
            DebugInfo = $"{seen} placards, {plots.Count} known plots, {named} renamed";
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Placard names: {ex.Message}");
        }
    }

    private static unsafe uint LayoutId(nint address)
    {
        var native = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)address;
        return native != null ? native->LayoutId : 0;
    }

    private Dictionary<uint, int> PlotsFor(uint territoryId)
    {
        if (plotsByTerritory.TryGetValue(territoryId, out var known)) return known;
        var plots = new Dictionary<uint, int>();
        try
        {
            var bg = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.Bg.ExtractText();
            if (!string.IsNullOrEmpty(bg) && bg.Contains('/'))
            {
                var path = $"bg/{bg[..bg.LastIndexOf('/')]}/planmap.lgb";
                var file = data.GetFile<LgbFile>(path);
                if (file != null)
                {
                    plots = BuildPlotMap(file, placardIds);
                    log.Debug($"[VenueMapper] Placards: {plots.Count} placards from {path}");
                }
            }
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Placard layout: {ex.Message}");
        }
        plotsByTerritory[territoryId] = plots;
        return plots;
    }

    public static Dictionary<uint, int> BuildPlotMap(LgbFile file, IReadOnlySet<uint> placardIds)
    {
        var plots = new Dictionary<uint, int>();
        var divisions = file.Layers
            .Select(layer => (layer.Name, Ids: layer.InstanceObjects
                .Where(o => o.Object is LayerCommon.EventInstanceObject e && placardIds.Contains(e.ParentData.BaseId))
                .Select(o => o.InstanceId)
                .OrderBy(id => id)
                .ToList()))
            .Where(layer => layer.Ids.Count > 0)
            .OrderBy(layer => layer.Name, StringComparer.Ordinal)
            .ToList();
        for (var division = 0; division < divisions.Count; division++)
            for (var i = 0; i < divisions[division].Ids.Count; i++)
                plots[divisions[division].Ids[i]] = division * PlotsPerDivision + i + 1;
        return plots;
    }

    private string Label(Venue venue)
    {
        var name = UIConstants.DisplayName(venue.Name);
        var status = plugin.GetVenueStatus(venue).Text;
        return status.Length > 0 ? $"{name} ({status})" : name;
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        try { namePlateGui.RequestRedraw(); } catch { }
    }
}
