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
            return v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v0.5.0";
        }
    }

    public static readonly (string Ver, string Date)[] Versions =
    [
        ("v0.5.0",  "Jun 21, 2026"),
        ("v0.4.5",  "Jun 21, 2026"),
    ];

    public static readonly Dictionary<string, string[]> Changelogs = new()
    {
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
