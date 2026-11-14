using System;
using System.Collections.Concurrent;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace VenueMapper.Services;

public sealed class PartyFinderAds : IDisposable
{
    private const int MinListingSeconds = 60;
    private const int MaxListingSeconds = 3600;

    private readonly VenueMapperPlugin plugin;
    private readonly IPartyFinderGui partyFinder;
    private readonly IPluginLog log;
    private readonly ConcurrentDictionary<string, (DateTime SeenAt, DateTime Until, ulong ListingId)> ads = new();

    public PartyFinderAds(VenueMapperPlugin plugin, IPartyFinderGui partyFinder, IPluginLog log)
    {
        this.plugin = plugin;
        this.partyFinder = partyFinder;
        this.log = log;
        partyFinder.ReceiveListing += OnListing;
    }

    public DateTime? SeenAt(string venueId)
    {
        if (!plugin.Configuration.PartyFinderAds) return null;
        if (!ads.TryGetValue(venueId, out var ad)) return null;
        if (DateTime.UtcNow <= ad.Until) return ad.SeenAt;
        ads.TryRemove(venueId, out _);
        return null;
    }

    public unsafe bool OpenListing(string venueId)
    {
        if (SeenAt(venueId) == null || !ads.TryGetValue(venueId, out var ad) || ad.ListingId == 0) return false;
        try
        {
            var agent = AgentLookingForGroup.Instance();
            return agent != null && agent->OpenListing(ad.ListingId);
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Open Party Finder listing: {ex.Message}");
            return false;
        }
    }

    private void OnListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
    {
        if (!plugin.Configuration.PartyFinderAds) return;
        try
        {
            var text = listing.Description.TextValue;
            if (string.IsNullOrWhiteSpace(text)) return;
            var world = listing.World.ValueNullable?.Name.ExtractText();
            var now = DateTime.UtcNow;
            var until = now + TimeSpan.FromSeconds(Math.Clamp((int)listing.SecondsRemaining, MinListingSeconds, MaxListingSeconds));
            foreach (var hit in plugin.ChatLinks.FindIn(text, world))
                ads.AddOrUpdate(hit.Venue.VenueId, (now, until, listing.Id), (_, old) => (now, old.Until > until ? old.Until : until, listing.Id));
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Party Finder listing: {ex.Message}");
        }
    }

    public void Dispose()
    {
        partyFinder.ReceiveListing -= OnListing;
        ads.Clear();
    }
}
