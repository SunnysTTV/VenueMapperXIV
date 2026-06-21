using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

using AddressBookEntry = (string Name, int World, int City, int Ward,
    int PropertyType, int Plot, int Apartment,
    bool ApartmentSubdivision, bool AliasEnabled, string Alias);

namespace VenueMapper.Services;

public class LifestreamService : IDisposable
{

    private readonly IPluginLog log;

    private readonly ICallGateSubscriber<bool>                                                                  isBusyGate;
    private readonly ICallGateSubscriber<string, string, string, string, bool, bool, AddressBookEntry>          buildEntryGate;
    private readonly ICallGateSubscriber<AddressBookEntry, object>                                              goToHousingGate;
    private readonly ICallGateSubscriber<string, bool>                                                          aetheryteTeleportGate;
    private readonly ICallGateSubscriber<uint, byte, bool>                                                      teleportGate;

    private static readonly Dictionary<string, uint> DistrictAetheryteIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mist"]          = 56,
        ["lavender beds"] = 57,
        ["the goblet"]    = 58,
        ["goblet"]        = 58,
        ["shirogane"]     = 127,
        ["empyreum"]      = 182,
    };

    private bool?    _cachedLoaded;
    private DateTime _lastLoadCheck = DateTime.MinValue;

    public LifestreamService(IDalamudPluginInterface pi, IPluginLog log)
    {
        this.log = log;

        log.Information("[VenueMapper/LS] Registering IPC subscribers...");
        isBusyGate      = pi.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        buildEntryGate  = pi.GetIpcSubscriber<string, string, string, string, bool, bool, AddressBookEntry>("Lifestream.BuildAddressBookEntry");
        goToHousingGate      = pi.GetIpcSubscriber<AddressBookEntry, object>("Lifestream.GoToHousingAddress");
        aetheryteTeleportGate = pi.GetIpcSubscriber<string, bool>("Lifestream.AetheryteTeleport");
        teleportGate         = pi.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
        log.Information("[VenueMapper/LS] IPC subscribers created.");
    }

    public bool IsLoaded
    {
        get
        {
            var now = DateTime.Now;
            if (_cachedLoaded.HasValue && (now - _lastLoadCheck).TotalSeconds < 10)
                return _cachedLoaded.Value;

            _lastLoadCheck = now;
            try
            {
                isBusyGate.InvokeFunc();
                _cachedLoaded = true;
            }
            catch (IpcNotReadyError)
            {
                _cachedLoaded = false;
            }
            catch
            {
                _cachedLoaded = true;
            }
            return _cachedLoaded.Value;
        }
    }

    public bool NavigateTo(string address)
    {
        log.Information($"[VenueMapper/LS] NavigateTo: '{address}'");

        if (!TryParseAddress(address, out var worldName, out var cityName, out var ward, out var plot))
        {
            log.Warning($"[VenueMapper/LS] Address parse FAILED: '{address}'");
            return false;
        }

        log.Information($"[VenueMapper/LS] Parsed â†’ world='{worldName}', city='{cityName}', ward={ward}, plot={plot}");

        try
        {
            log.Information($"[VenueMapper/LS] BuildAddressBookEntry('{worldName}', '{cityName}', '{ward}', '{plot}', false, false)");
            var entry = buildEntryGate.InvokeFunc(worldName, cityName, ward.ToString(), plot.ToString(), false, false);
            log.Information($"[VenueMapper/LS] Entry: world={entry.World}, city={entry.City}, ward={entry.Ward}, plot={entry.Plot}");
            log.Information("[VenueMapper/LS] Calling GoToHousingAddress...");
            goToHousingGate.InvokeAction(entry);
            log.Information("[VenueMapper/LS] GoToHousingAddress OK.");
            return true;
        }
        catch (IpcNotReadyError)
        {
            log.Warning("[VenueMapper/LS] GoToHousingAddress not available (older Lifestream) - falling back to Teleport.");
        }
        catch (Exception ex)
        {
            log.Error($"[VenueMapper/LS] GoToHousingAddress failed: {ex.GetType().Name}: {ex.Message}");
        }

        return FallbackTeleport(cityName);
    }

    private bool FallbackTeleport(string cityName)
    {
        try
        {
            log.Information($"[VenueMapper/LS] AetheryteTeleport('{cityName}')");
            var ok = aetheryteTeleportGate.InvokeFunc(cityName);
            log.Information($"[VenueMapper/LS] AetheryteTeleport returned: {ok}");
            if (ok) return true;
        }
        catch (Exception ex)
        {
            log.Warning($"[VenueMapper/LS] AetheryteTeleport failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (!DistrictAetheryteIds.TryGetValue(cityName, out var aetheryteId))
        {
            log.Error($"[VenueMapper/LS] No aetheryte ID known for '{cityName}'.");
            return false;
        }

        log.Information($"[VenueMapper/LS] Teleport({aetheryteId}, 0) for '{cityName}'");
        try
        {
            var ok = teleportGate.InvokeFunc(aetheryteId, 0);
            log.Information($"[VenueMapper/LS] Teleport returned: {ok}");
            return ok;
        }
        catch (Exception ex)
        {
            log.Error($"[VenueMapper/LS] Teleport failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }


    private static readonly string[] Districts =
        ["The Goblet", "Lavender Beds", "Mist", "Shirogane", "Empyreum"];

    private bool TryParseAddress(string address, out string worldName, out string cityName, out int ward, out int plot)
    {
        worldName = string.Empty;
        cityName  = string.Empty;
        ward      = 0;
        plot      = 0;

        var parts = address.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        string? foundCity  = null;
        string? foundWorld = null;
        int wardNum = 0, plotNum = 0;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var combined = $"{parts[i]} {parts[i + 1]}";
            foreach (var d in Districts)
                if (combined.Equals(d, StringComparison.OrdinalIgnoreCase))
                    foundCity = d;
        }

        foreach (var part in parts)
        {
            if (part.StartsWith("Ward ", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(part[5..].Trim(), out var w))
                wardNum = w;
            else if (part.StartsWith("Plot ", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(part[5..].Trim(), out var p))
                plotNum = p;
            else if (foundCity == null)
            {
                foreach (var d in Districts)
                    if (part.Equals(d, StringComparison.OrdinalIgnoreCase))
                        foundCity = d;
            }
        }

        if (foundCity == null || wardNum == 0 || plotNum == 0)
            return false;

        foreach (var part in parts)
        {
            if (!IsDatacenter(part) &&
                !part.Equals(foundCity, StringComparison.OrdinalIgnoreCase) &&
                !part.StartsWith("Ward ", StringComparison.OrdinalIgnoreCase) &&
                !part.StartsWith("Plot ", StringComparison.OrdinalIgnoreCase) &&
                !foundCity.StartsWith(part, StringComparison.OrdinalIgnoreCase))
            {
                foundWorld = part;
                break;
            }
        }

        worldName = foundWorld ?? string.Empty;
        cityName  = foundCity;
        ward      = wardNum;
        plot      = plotNum;
        return true;
    }

    private static bool IsDatacenter(string s) => s is
        "Light" or "Crystal" or "Aether" or "Primal" or "Chaos" or "Materia" or
        "Elemental" or "Gaia" or "Mana" or "Meteor" or "Dynamis" or "Shadow";

    public void Dispose() { }
}
