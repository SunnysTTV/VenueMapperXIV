using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using VenueMapper.Models;
using VenueMapper.UI;

namespace VenueMapper.Services;

public sealed unsafe class GameMapMarkers : IDisposable
{
    private const uint VenueIcon = UI.VenueWorldMarkers.VenueIcon;
    private const float OverlayIconSize = 32f;
    private const float PinTolerance = 12f;
    private static readonly TimeSpan RebuildInterval = TimeSpan.FromSeconds(1);

    private readonly VenueMapperPlugin plugin;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IPluginLog log;
    private readonly List<Pin> pins = new();
    private readonly Dictionary<string, nint> nameBuffers = new();
    private DateTime builtAt = DateTime.MinValue;
    private bool nativeActive;
    private bool bigMapActive;
    private string lastOverlayInfo = "";

    private readonly record struct Pin(Vector3 World, uint MapId, string Name, Vector4 Color, bool Open, uint Icon);

    public GameMapMarkers(VenueMapperPlugin plugin, IFramework framework, IGameGui gameGui, IAddonLifecycle addonLifecycle, IPluginLog log)
    {
        this.plugin = plugin;
        this.framework = framework;
        this.gameGui = gameGui;
        this.addonLifecycle = addonLifecycle;
        this.log = log;
        framework.Update += OnFrameworkUpdate;
        addonLifecycle.RegisterListener(AddonEvent.PostDraw, "AreaMap", OnAreaMapDraw);
        addonLifecycle.RegisterListener(AddonEvent.PreHide, "AreaMap", OnAreaMapClose);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "AreaMap", OnAreaMapClose);
    }

    public void Refresh()
    {
        builtAt = DateTime.MinValue;
        if (!UsesNative)
            ClearNative();
    }

    private bool UsesNative => plugin.Configuration.GameMapNames is GameMapMode.Native or GameMapMode.Both;
    private bool UsesOverlay => plugin.Configuration.GameMapNames is GameMapMode.Overlay or GameMapMode.Both;

    private void Rebuild()
    {
        if (DateTime.Now - builtAt < RebuildInterval) return;
        builtAt = DateTime.Now;
        if (pins.Count > 0)
        {
            lastPins.Clear();
            lastPins.AddRange(pins);
        }
        pins.Clear();

        var config = plugin.ConfigManager.Config;
        var tracker = plugin.PositionTracker;
        if (config == null || tracker.IsInsideHouse) return;

        var district = tracker.CurrentDistrict;
        foreach (var venue in tracker.GetWardVenues(config))
        {
            if (plugin.Configuration.HiddenVenueIds.Contains(venue.VenueId)) continue;
            if (!plugin.HousingData.TryGetPlotMapLink(district, venue.Plot, out _, out var mapId, out var pos)) continue;
            var color = venue.Colors?.PrimaryVec ?? UIConstants.TextPrimary;
            pins.Add(new Pin(pos, mapId, UIConstants.DisplayName(venue.Name), color, plugin.GetVenueStatus(venue).Open, VenueIcon));
        }
    }

    private const uint HouseIconFirst = 60751;
    private const uint HouseIconLast = 60799;
    private int houseIcons;
    private int replacedMap;
    private int replacedMini;
    private string matchMode = "-";
    private string houseSample = "-";

    private static bool IsHouseIcon(uint icon) => icon is >= HouseIconFirst and <= HouseIconLast;

    private const float MarkerUnits = 16f;
    private const float HouseMatchDistance = 4f;

    private static Vector2 MarkerWorld(short x, short y) => new Vector2(x, y) / MarkerUnits;

    private static bool SamePlace(short x, short y, Vector3 world, float tolerance)
        => Vector2.Distance(MarkerWorld(x, y), new Vector2(world.X, world.Z)) < tolerance;

    private Pin? MatchPin(short x, short y, uint mapId)
    {
        foreach (var pin in pins)
        {
            if (pin.MapId != mapId) continue;
            if (SamePlace(x, y, pin.World, HouseMatchDistance)) { matchMode = "world"; return pin; }
        }
        return null;
    }

    private void ReplaceHouseIcons(AgentMap* agent)
    {
        houseIcons = 0;
        replacedMap = 0;
        replacedMini = 0;

        var mapCount = Math.Min((int)agent->MapMarkerCount, agent->MapMarkers.Length);
        for (var i = 0; i < mapCount; i++)
        {
            ref var marker = ref agent->MapMarkers[i].MapMarker;
            if (!IsHouseIcon(marker.IconId)) continue;
            houseIcons++;
            if (houseIcons == 1) houseSample = $"icon {marker.IconId} at {MarkerWorld(marker.X, marker.Y)}";
            if (MatchPin(marker.X, marker.Y, agent->SelectedMapId) == null) continue;
            marker.IconId = VenueIcon;
            replacedMap++;
        }

        var miniCount = Math.Min((int)agent->MiniMapMarkerCount, agent->MiniMapMarkers.Length);
        for (var i = 0; i < miniCount; i++)
        {
            ref var marker = ref agent->MiniMapMarkers[i].MapMarker;
            if (!IsHouseIcon(marker.IconId)) continue;
            houseIcons++;
            if (MatchPin(marker.X, marker.Y, agent->CurrentMapId) == null) continue;
            marker.IconId = VenueIcon;
            replacedMini++;
        }
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        try
        {
            if (plugin.Configuration.GameMapNames == GameMapMode.Off)
            {
                if (nativeActive) ClearNative();
                return;
            }

            Rebuild();
            var agent = AgentMap.Instance();
            if (agent == null) return;
            if (pins.Count == 0)
            {
                if (nativeActive) ClearNative();
                return;
            }

            if (!UsesNative)
            {
                if (nativeActive) ClearNative();
                ReplaceHouseIcons(agent);
                return;
            }

            var bigMap = plugin.Configuration.GameMapNames == GameMapMode.Native;
            if (!bigMap && bigMapActive) RemoveOwnMapMarkers(agent);
            bigMapActive = bigMap;
            ReplaceHouseIcons(agent);
            foreach (var pin in pins)
            {
                if (bigMap && pin.MapId == agent->SelectedMapId && !HasMapMarker(agent, pin))
                    agent->AddMapMarker(pin.World, pin.Icon, 0, (byte*)NameBuffer(pin.Name));
                if (pin.MapId == agent->CurrentMapId && !HasMiniMapMarker(agent, pin))
                    agent->AddMiniMapMarker(pin.World, pin.Icon);
            }
            nativeActive = true;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Game map markers: {ex.Message}");
        }
    }

    private nint NameBuffer(string name)
    {
        if (nameBuffers.TryGetValue(name, out var ptr)) return ptr;
        var bytes = Encoding.UTF8.GetBytes(name);
        ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        ((byte*)ptr)[bytes.Length] = 0;
        nameBuffers[name] = ptr;
        return ptr;
    }

    private static bool IsOwn(in FFXIVClientStructs.FFXIV.Client.UI.Agent.MapMarkerBase marker, Pin pin)
        => marker.IconId == pin.Icon && SamePlace(marker.X, marker.Y, pin.World, 0.5f);

    private static bool HasMapMarker(AgentMap* agent, Pin pin)
    {
        var count = Math.Min((int)agent->MapMarkerCount, agent->MapMarkers.Length);
        for (var i = 0; i < count; i++)
            if (agent->MapMarkers[i].MapMarker.Subtext.HasValue && IsOwn(agent->MapMarkers[i].MapMarker, pin)) return true;
        return false;
    }

    private static bool HasMiniMapMarker(AgentMap* agent, Pin pin)
    {
        var count = Math.Min((int)agent->MiniMapMarkerCount, agent->MiniMapMarkers.Length);
        for (var i = 0; i < count; i++)
            if (IsOwn(agent->MiniMapMarkers[i].MapMarker, pin)) return true;
        return false;
    }

    private bool IsOwnBuffer(byte* text)
    {
        foreach (var ptr in nameBuffers.Values)
            if ((byte*)ptr == text) return true;
        return false;
    }

    private void RemoveOwnMapMarkers(AgentMap* agent)
    {
        var count = Math.Min((int)agent->MapMarkerCount, agent->MapMarkers.Length);
        var write = 0;
        for (var read = 0; read < count; read++)
        {
            var marker = agent->MapMarkers[read];
            if (marker.MapMarker.Subtext.HasValue && IsOwnBuffer(marker.MapMarker.Subtext.Value)) continue;
            marker.MapMarker.Index = (byte)write;
            agent->MapMarkers[write++] = marker;
        }
        agent->MapMarkerCount = (byte)write;
    }

    private void RemoveOwnMiniMapMarkers(AgentMap* agent)
    {
        var count = Math.Min((int)agent->MiniMapMarkerCount, agent->MiniMapMarkers.Length);
        var write = 0;
        for (var read = 0; read < count; read++)
        {
            var marker = agent->MiniMapMarkers[read];
            if (marker.MapMarker.IconId == VenueIcon && !marker.MapMarker.Subtext.HasValue && IsPinPlace(marker.MapMarker)) continue;
            marker.MapMarker.Index = (byte)write;
            agent->MiniMapMarkers[write++] = marker;
        }
        agent->MiniMapMarkerCount = (byte)write;
    }

    private bool IsPinPlace(in FFXIVClientStructs.FFXIV.Client.UI.Agent.MapMarkerBase marker)
    {
        foreach (var pin in lastPins)
            if (SamePlace(marker.X, marker.Y, pin.World, 0.5f)) return true;
        return false;
    }

    private readonly List<Pin> lastPins = new();

    private void ClearNative()
    {
        if (!nativeActive) return;
        nativeActive = false;
        bigMapActive = false;
        var agent = AgentMap.Instance();
        if (agent == null) return;
        RemoveOwnMapMarkers(agent);
        RemoveOwnMiniMapMarkers(agent);
        lastPins.Clear();
    }
    private const long OverlayMaxAgeMs = 250;

    private readonly record struct OverlayItem(Vector2 Screen, string Name, Vector4 Color, uint Icon);
    private sealed record OverlayFrame(List<OverlayItem> Items, Vector2 ClipMin, Vector2 ClipMax, float UiScale, long At);

    private OverlayFrame? overlay;

    private void OnAreaMapDraw(AddonEvent type, AddonArgs args)
    {
        if (!UsesOverlay)
        {
            overlay = null;
            return;
        }
        try
        {
            overlay = BuildOverlay((AddonAreaMap*)args.Addon.Address);
        }
        catch (Exception ex)
        {
            overlay = null;
            lastOverlayInfo = $"error {ex.Message}";
            log.Debug($"[VenueMapper] Game map overlay: {ex.Message}");
        }
    }

    private void OnAreaMapClose(AddonEvent type, AddonArgs args)
    {
        overlay = null;
        lastOverlayInfo = "map closed";
    }

    private OverlayFrame? BuildOverlay(AddonAreaMap* addon)
    {
        Rebuild();
        if (pins.Count == 0) { lastOverlayInfo = "no venues in this ward"; return null; }
        if (addon == null || !addon->IsVisible || addon->ComponentMap == null) { lastOverlayInfo = "map hidden"; return null; }

        var agent = AgentMap.Instance();
        if (agent == null) return null;
        var mapInfo = plugin.MapLoader.GetMapInfoByMapId(agent->SelectedMapId);
        if (mapInfo == null) { lastOverlayInfo = $"no map info for {agent->SelectedMapId}"; return null; }

        var component = addon->ComponentMap;
        var owner = component->OwnerNode;
        var image = component->BaseMapImage;
        if (owner == null || image == null) return null;

        var uiScale = addon->GetScale();
        var (imagePos, imageScale) = NodeTransform(&image->AtkResNode);
        var imageSize = new Vector2(image->AtkResNode.Width, image->AtkResNode.Height) * imageScale;
        var (ownerPos, ownerScale) = NodeTransform(&owner->AtkResNode);
        var clipMax = ownerPos + new Vector2(owner->AtkResNode.Width, owner->AtkResNode.Height) * ownerScale;
        if (imageSize.X <= 0 || imageSize.Y <= 0) return null;
        var factor = mapInfo.SizeFactor / 100f;

        var partOrigin = Vector2.Zero;
        var partSize = new Vector2(2048f);
        var textureSize = new Vector2(2048f);
        if (image->PartsList != null && image->PartId < image->PartsList->PartCount)
        {
            var part = image->PartsList->Parts[image->PartId];
            if (part.Width > 0 && part.Height > 0)
            {
                partOrigin = new Vector2(part.U, part.V);
                partSize = new Vector2(part.Width, part.Height);
            }
            if (part.UldAsset != null && part.UldAsset->AtkTexture.IsTextureReady())
            {
                var w = part.UldAsset->AtkTexture.GetTextureWidth();
                var h = part.UldAsset->AtkTexture.GetTextureHeight();
                if (w > 0 && h > 0) textureSize = new Vector2(w, h);
            }
        }
        var pixelScale = imageSize / partSize;

        Vector2 Pixel(Vector3 world)
            => new Vector2((world.X + mapInfo.OffsetX) * factor + 1024f, (world.Z + mapInfo.OffsetY) * factor + 1024f) / 2048f * textureSize;

        Func<Vector3, Vector2> project = world => imagePos + (Pixel(world) - partOrigin) * pixelScale;
        var mode = $"map image (part {partOrigin.X:0},{partOrigin.Y:0} {partSize.X:0}x{partSize.Y:0}, texture {textureSize.X:0})";
        var pin = addon->AreaMap.PlayerPin;
        var tracker = plugin.PositionTracker;
        if (pin != null && pin->AtkResNode.IsVisible() && agent->SelectedMapId == agent->CurrentMapId)
        {
            var (pinPos, pinScale) = NodeTransform(&pin->AtkResNode);
            var pivot = pinPos + new Vector2(pin->AtkResNode.OriginX, pin->AtkResNode.OriginY) * pinScale;
            var player = new Vector3(tracker.PlayerX, tracker.PlayerY, tracker.PlayerZ);
            var error = Vector2.Distance(project(player), pivot);
            if (error > PinTolerance)
            {
                var playerPixel = Pixel(player);
                project = world => pivot + (Pixel(world) - playerPixel) * pixelScale;
                mode += $", anchored on the player pin ({error:0} px off)";
            }
            else
            {
                mode += $", player pin {error:0} px off";
            }
        }

        var items = new List<OverlayItem>(pins.Count);
        foreach (var p in pins)
        {
            if (p.MapId != agent->SelectedMapId) continue;
            var projected = project(p.World);
            items.Add(new OverlayItem(new Vector2(MathF.Round(projected.X), MathF.Round(projected.Y)), p.Name, p.Color, p.Icon));
        }
        lastOverlayInfo = $"{items.Count} drawn, mode {mode}";
        return new OverlayFrame(items, ownerPos, clipMax, uiScale, Environment.TickCount64);
    }

    public void DrawOverlay()
    {
        var frame = overlay;
        if (!UsesOverlay || frame == null || frame.Items.Count == 0) return;
        if (Environment.TickCount64 - frame.At > OverlayMaxAgeMs) return;

        var dl = ImGui.GetBackgroundDrawList();
        dl.PushClipRect(frame.ClipMin, frame.ClipMax, false);
        var font = ImGui.GetFont();
        var fontSize = MathF.Round(ImGui.GetFontSize() * Math.Clamp(frame.UiScale, 0.75f, 2f));
        var iconHalf = OverlayIconSize / 2f * frame.UiScale;
        var outline = MathF.Max(1f, frame.UiScale);
        var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.9f));
        foreach (var item in frame.Items)
        {
            var screen = item.Screen;
            if (VenueMapperPlugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.Icon)).TryGetWrap(out var icon, out _))
                dl.AddImage(icon.Handle, screen - new Vector2(iconHalf), screen + new Vector2(iconHalf));

            var textPos = new Vector2(MathF.Round(screen.X + iconHalf * 0.85f + 3f), MathF.Round(screen.Y - fontSize / 2f));
            foreach (var o in OutlineOffsets)
                dl.AddText(font, fontSize, textPos + o * outline, shadow, item.Name);
            dl.AddText(font, fontSize, textPos, ImGui.ColorConvertFloat4ToU32(UIConstants.Readable(item.Color)), item.Name);
        }
        dl.PopClipRect();
    }

    private static readonly Vector2[] OutlineOffsets = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(1, 1)];

    private static (Vector2 Pos, Vector2 Scale) NodeTransform(FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode* node)
    {
        var pos = new Vector2(node->X, node->Y) - new Vector2(node->OriginX * (node->ScaleX - 1f), node->OriginY * (node->ScaleY - 1f));
        var scale = new Vector2(node->ScaleX, node->ScaleY);
        for (var parent = node->ParentNode; parent != null; parent = parent->ParentNode)
        {
            pos *= new Vector2(parent->ScaleX, parent->ScaleY);
            pos += new Vector2(parent->X, parent->Y);
            pos -= new Vector2(parent->OriginX * (parent->ScaleX - 1f), parent->OriginY * (parent->ScaleY - 1f));
            scale *= new Vector2(parent->ScaleX, parent->ScaleY);
        }
        return (pos, scale);
    }

    public string HouseIconInfo => $"house icons {houseIcons}, replaced map {replacedMap} minimap {replacedMini} ({matchMode}), first {houseSample}";

    public string DebugInfo()
    {
        var addonPtr = gameGui.GetAddonByName("AreaMap");
        if (addonPtr.IsNull) return $"map closed · {lastOverlayInfo}";
        var addon = (AddonAreaMap*)addonPtr.Address;
        if (addon->ComponentMap == null) return "no map component";
        var c = addon->ComponentMap;
        var agent = AgentMap.Instance();
        return $"map {(agent != null ? agent->SelectedMapId : 0)} scale {c->MapScale:F3} offset ({c->MapOffsetX:F0},{c->MapOffsetY:F0}) "
               + $"size ({c->MapWidth:F0}x{c->MapHeight:F0}) ui {addon->GetScale():F2} pins {pins.Count} · {lastOverlayInfo}";
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        addonLifecycle.UnregisterListener(OnAreaMapDraw, OnAreaMapClose);
        try { ClearNative(); } catch { }
        foreach (var ptr in nameBuffers.Values)
            Marshal.FreeHGlobal(ptr);
        nameBuffers.Clear();
    }
}
