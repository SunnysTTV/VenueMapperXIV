using System.Collections.Generic;
using System.Reflection;

namespace VenueMapper.UI;

public static class ChangelogData
{
    public static string PluginVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v0.5.5.1";
        }
    }

    public static readonly (string Ver, string Date)[] Versions =
    [
        ("v0.5.5.1", "Jun 25, 2026"),
        ("v0.5.5",  "Jun 25, 2026"),
        ("v0.5.4.5", "Jun 22, 2026"),
        ("v0.5.4",  "Jun 22, 2026"),
        ("v0.5.3",  "Jun 22, 2026"),
        ("v0.5.2",  "Jun 21, 2026"),
        ("v0.5.1",  "Jun 21, 2026"),
        ("v0.5.0",  "Jun 21, 2026"),
        ("v0.4.5",  "Jun 21, 2026"),
    ];

    public static readonly Dictionary<string, string[]> Changelogs = new()
    {
        ["v0.5.5.1"] = [
            "Fixed schedule time calculation (UTC timezone)",
            "Fixed changelog scroll style stack",
            "Partake fallback now triggers fetch from directory",
        ],
        ["v0.5.5"] = [
            "XIVVenues schedule integration (OPEN NOW / Opens in Xh)",
            "Partake event schedule as fallback in directory",
            "Stage service marker (MicrophoneAlt icon)",
            "RESX-based language selector",
            "Framework error handling with throttled logging",
            "JSON export fully escaped",
            "URL validation on all external links",
            "Thread-safe config and API collections",
            "Dynamic User-Agent version",
            "Reduced Partake API fetch to 2 events",
        ],
        ["v0.5.4.5"] = [
            "Scrollable venue directory and events tab",
            "Server filter dropdown (appears when DC selected)",
            "Compact venue cards with shortened addresses",
            "Favorite star icon (FontAwesome) right-aligned in card",
            "Animated Visit button with shimmer and color shift",
            "New service types: vip, bath/spa, event",
            "Debug coords now match venues.json format",
            "Auto floor detection in Owner Setup on Use my pos",
            "Fixed Services tab jumping on add/delete",
        ],
        ["v0.5.4"] = [
            "Multi-select datacenter filter in directory",
            "Events limited to 1 per venue",
            "Fixed event leak on plugin reload",
            "Fixed encoding issues in zoom badge",
            "All service marker colors are now fixed per type",
            "Dead code cleanup",
        ],
        ["v0.5.3"] = [
            "Active events from Partake API (activeEvents query)",
            "Pulsing NOW badge for currently running events",
            "Venue name shown on event cards",
            "Events merged across all venues into single list",
            "HERE badge now respects ward/plot matching",
            "Fixed config cache file lock issue",
        ],
        ["v0.5.2"] = [
            "Auto-detection of housing ward/plot via HousingManager",
            "Multiple venues can share the same territory ID",
            "Ward/Plot display in debug window",
            "Fixed corrupted Unicode characters in UI",
            "Fixed texture debug display encoding",
        ],
        ["v0.5.1"] = [
            "Removed placeholder text from Owner Setup fields",
            "Copy JSON button with visual confirmation",
            "Fixed icon.png path for Dalamud API compatibility",
            "Fixed manifest and project file cleanup",
        ],
        ["v0.5.0"] = ["Initial Release"],
        ["v0.4.5"] = [
            "Interactive housing maps with 2D zoom/pan and floor switching",
            "3D Pictomancy world-space markers at service locations",
            "Venue directory with favorites, color sweep, and social links",
            "Live event tracking from Partake.gg with server time",
            "Lifestream IPC teleport integration (GoToHousingAddress)",
            "First-time setup wizard with language and feature selection",
            "Owner submission tools with Google Forms auto-fill and Discord export",
            "Datacenter/Server hierarchy with 80+ FFXIV servers",
            "RESX-based localization system (EN/DE)",
            "Auto GitHub config polling with ETag-based conditional requests",
        ],
    };
}
