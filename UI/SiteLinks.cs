using System;
using VenueMapper.Models;

namespace VenueMapper.UI;

public static class SiteLinks
{
    public const string Host = "venuemapper.sunexa.xyz";
    public const string Home = "https://" + Host;
    public const string Owners = Home + "/owners";
    public const string Changelog = Home + "/changelog";
    public const string VenuePath = "/v/";

    public static string? VenuePage(Venue venue)
    {
        if (venue.Closed || string.IsNullOrWhiteSpace(venue.VenueId)) return null;
        return Home + VenuePath + Uri.EscapeDataString(venue.VenueId.Trim().ToLowerInvariant());
    }
}
