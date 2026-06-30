using System.Collections.Generic;
using System.Reflection;

namespace VenueMapper.UI;

public record ChangelogEntry(string Tag, string Text, string? TextDE = null);
public record ChangelogSection(string? Title, string? TitleDE, ChangelogEntry[] Entries);

public static class ChangelogData
{
    public static string CurrentLanguage { get; set; } = "EN";

    public static string PluginVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null ? (v.Revision > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}" : $"v{v.Major}.{v.Minor}.{v.Build}") : "v0.5.6";
        }
    }

    public static readonly (string Ver, string Date)[] Versions =
    [
        ("v0.5.6",   "Jun 30, 2026"),
        ("v0.5.5.2", "Jun 25, 2026"),
        ("v0.5.5",   "Jun 25, 2026"),
        ("v0.5.4.5", "Jun 22, 2026"),
        ("v0.5.4",   "Jun 22, 2026"),
        ("v0.5.3",   "Jun 22, 2026"),
        ("v0.5.2",   "Jun 21, 2026"),
        ("v0.5.1",   "Jun 21, 2026"),
        ("v0.5.0",   "Jun 21, 2026"),
        ("v0.4.5",   "Jun 21, 2026"),
    ];

    public static readonly Dictionary<string, ChangelogSection[]> Changelogs = new()
    {
        ["v0.5.6"] =
        [
            new("Owner Setup", "Owner Setup",
            [
                new("ADDED",    "House size field (L / M / S)",
                                "Hausgröße-Feld (L / M / S)"),
                new("ADDED",    "'Detect from Current Position' button fills DC, server, district, ward, plot and house size in one click",
                                "Schaltfläche 'Position erkennen' befüllt DC, Server, Bezirk, Ward, Plot und Hausgröße in einem Klick"),
                new("IMPROVED", "House size auto-detected from district and plot number",
                                "Hausgröße wird automatisch aus Bezirk und Plot-Nummer erkannt"),
                new("IMPROVED", "Owner Setup fully translated to German",
                                "Owner Setup vollständig auf Deutsch übersetzt"),
                new("FIXED",    "Housing district now detected correctly both inside and outside houses",
                                "Wohnbezirk wird jetzt korrekt erkannt – sowohl innerhalb als auch außerhalb von Häusern"),
                new("CHANGED",  "Window height increased to fit all fields",
                                "Fensterhöhe vergrößert, um alle Felder anzuzeigen"),
            ]),
            new(null, null,
            [
                new("ADDED",   "Colored status tags in changelog (ADDED / IMPROVED / CHANGED / FIXED / REMOVED)",
                               "Farbige Status-Tags im Changelog (ADDED / IMPROVED / CHANGED / FIXED / REMOVED)"),
                new("IMPROVED", "German translations completed for changelog, directory filter, and About tab",
                                "Deutsche Übersetzungen für Changelog, Verzeichnis-Filter und About-Tab vervollständigt"),
                new("CHANGED", "Main window minimum size increased",
                               "Mindestgröße des Hauptfensters vergrößert"),
                new("CHANGED", "Discord button in About tab labeled as 'Support Discord'",
                               "Discord-Button im About-Tab heißt jetzt 'Support Discord'"),
                new("FIXED",   "3D markers no longer visible on the character selection screen after logging out",
                               "3D-Marker werden nach dem Ausloggen nicht mehr auf dem Charakterauswahlbildschirm angezeigt"),
            ]),
        ],
        ["v0.5.5.2"] =
        [
            new(null, null,
            [
                new("FIXED", "Schedule time calculation (UTC timezone mismatch)",
                             "Zeitberechnung für Veranstaltungen (UTC-Zeitzonen-Fehler)"),
                new("FIXED", "Changelog scroll style stack error",
                             "Changelog-Scroll-Stilfehler behoben"),
                new("FIXED", "Partake fallback now triggers fetch from directory",
                             "Partake-Fallback lädt jetzt korrekt aus dem Verzeichnis"),
            ]),
        ],
        ["v0.5.5"] =
        [
            new(null, null,
            [
                new("ADDED",    "XIVVenues schedule integration (OPEN NOW / Opens in Xh badge)",
                                "XIVVenues Zeitplan-Integration (JETZT OFFEN / Öffnet in Xh Badge)"),
                new("ADDED",    "Partake event schedule as fallback in directory",
                                "Partake-Veranstaltungsplan als Fallback im Verzeichnis"),
                new("ADDED",    "Stage service marker",
                                "Bühnen-Dienstmarkierung"),
                new("ADDED",    "Language selector (EN / DE)",
                                "Sprachauswahl (EN / DE)"),
                new("IMPROVED", "Framework error handling with throttled logging",
                                "Framework-Fehlerbehandlung mit gedrosseltem Logging"),
                new("IMPROVED", "JSON export fully escaped",
                                "JSON-Export vollständig escaped"),
                new("IMPROVED", "URL validation on all external links",
                                "URL-Validierung für alle externen Links"),
                new("IMPROVED", "Thread-safe config and API collections",
                                "Thread-sichere Konfig- und API-Sammlungen"),
                new("CHANGED",  "Dynamic User-Agent version header",
                                "Dynamischer User-Agent-Versions-Header"),
                new("CHANGED",  "Reduced Partake API fetch to 2 events per venue",
                                "Partake-API-Abfrage auf 2 Events pro Venue reduziert"),
            ]),
        ],
        ["v0.5.4.5"] =
        [
            new(null, null,
            [
                new("ADDED",   "Scrollable venue directory and events tab",
                               "Scrollbares Venue-Verzeichnis und Veranstaltungs-Tab"),
                new("ADDED",   "Server filter dropdown (appears when datacenter is selected)",
                               "Server-Filter-Dropdown (erscheint bei DC-Auswahl)"),
                new("ADDED",   "Favorite star icon right-aligned in venue cards",
                               "Favoriten-Stern rechtsbündig in Venue-Karten"),
                new("ADDED",   "New service types: VIP, Bath/Spa, Event",
                               "Neue Diensttypen: VIP, Bad/Spa, Event"),
                new("ADDED",   "Auto floor detection in Owner Setup on Use my pos",
                               "Automatische Etagenerkennung im Owner Setup bei 'Meine Position'"),
                new("CHANGED", "Compact venue cards with shortened addresses",
                               "Kompakte Venue-Karten mit gekürzten Adressen"),
                new("CHANGED", "Animated Visit button with shimmer and color shift",
                               "Animierter Besuchen-Button mit Schimmer und Farbwechsel"),
                new("FIXED",   "Services tab jumping on add/delete",
                               "Services-Tab sprang beim Hinzufügen/Löschen"),
            ]),
        ],
        ["v0.5.4"] =
        [
            new(null, null,
            [
                new("ADDED",   "Multi-select datacenter filter in directory",
                               "Mehrfachauswahl-DC-Filter im Verzeichnis"),
                new("CHANGED", "Events limited to 1 per venue",
                               "Events auf 1 pro Venue begrenzt"),
                new("CHANGED", "All service marker colors are now fixed per type",
                               "Alle Dienstmarkierungsfarben sind jetzt pro Typ festgelegt"),
                new("FIXED",   "Event leak on plugin reload",
                               "Event-Leak bei Plugin-Neuladung"),
                new("FIXED",   "Encoding issues in zoom badge",
                               "Kodierungsprobleme im Zoom-Badge"),
            ]),
        ],
        ["v0.5.3"] =
        [
            new(null, null,
            [
                new("ADDED",   "Active events from Partake API",
                               "Aktive Events von der Partake-API"),
                new("ADDED",   "Pulsing NOW badge for currently running events",
                               "Pulsierender JETZT-Badge für laufende Events"),
                new("ADDED",   "Venue name shown on event cards",
                               "Venue-Name auf Event-Karten angezeigt"),
                new("CHANGED", "Events merged across all venues into single sorted list",
                               "Events aller Venues in eine sortierte Liste zusammengeführt"),
                new("FIXED",   "HERE badge now respects ward/plot matching",
                               "HIER-Badge berücksichtigt jetzt Ward/Plot-Übereinstimmung"),
                new("FIXED",   "Config cache file lock issue (atomic write)",
                               "Konfig-Cache-Dateisperrproblem behoben (atomares Schreiben)"),
            ]),
        ],
        ["v0.5.2"] =
        [
            new(null, null,
            [
                new("ADDED", "Auto-detection of housing ward/plot via HousingManager",
                             "Automatische Erkennung von Ward/Plot per HousingManager"),
                new("ADDED", "Multiple venues can now share the same territory ID",
                             "Mehrere Venues können jetzt dieselbe Gebiets-ID teilen"),
                new("FIXED", "Corrupted Unicode characters in UI",
                             "Beschädigte Unicode-Zeichen in der Oberfläche"),
                new("FIXED", "Texture debug display encoding",
                             "Textur-Debug-Anzeige-Kodierung"),
            ]),
        ],
        ["v0.5.1"] =
        [
            new(null, null,
            [
                new("REMOVED", "Placeholder text from Owner Setup input fields",
                               "Platzhaltertext aus den Owner Setup-Eingabefeldern"),
                new("ADDED",   "Copy JSON button shows visual 'COPIED!' confirmation",
                               "JSON-Kopieren-Button zeigt visuelle 'KOPIERT!'-Bestätigung"),
                new("FIXED",   "icon.png path for Dalamud API compatibility",
                               "icon.png-Pfad für Dalamud-API-Kompatibilität"),
                new("FIXED",   "Manifest and project file cleanup",
                               "Manifest- und Projektdatei-Bereinigung"),
            ]),
        ],
        ["v0.5.0"] =
        [
            new(null, null, [new("ADDED", "Initial Release", "Erstveröffentlichung")]),
        ],
        ["v0.4.5"] =
        [
            new(null, null,
            [
                new("ADDED", "Interactive housing maps with 2D zoom/pan and floor switching",
                             "Interaktive Wohnungskarten mit 2D-Zoom/Pan und Etagenwechsel"),
                new("ADDED", "3D Pictomancy world-space markers at service locations",
                             "3D-Pictomancy-Weltmarkierungen an Dienstorten"),
                new("ADDED", "Venue directory with favorites, color sweep, and social links",
                             "Venue-Verzeichnis mit Favoriten, Farbwechsel und Social-Links"),
                new("ADDED", "Live event tracking from Partake.gg with server time",
                             "Live-Event-Tracking von Partake.gg mit Serverzeit"),
                new("ADDED", "Lifestream IPC teleport integration",
                             "Lifestream-IPC-Teleport-Integration"),
                new("ADDED", "First-time setup wizard with language and feature selection",
                             "Ersteinrichtungsassistent mit Sprach- und Funktionsauswahl"),
                new("ADDED", "Owner submission tools with JSON export",
                             "Owner-Einreichungstools mit JSON-Export"),
                new("ADDED", "Datacenter/Server hierarchy with 80+ FFXIV servers",
                             "Datencenter/Server-Hierarchie mit 80+ FFXIV-Servern"),
                new("ADDED", "Auto GitHub config polling with ETag-based caching",
                             "Automatisches GitHub-Konfig-Polling mit ETag-basiertem Caching"),
            ]),
        ],
    };
}
