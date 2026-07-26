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
            return v != null ? (v.Revision > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}" : $"v{v.Major}.{v.Minor}.{v.Build}") : "v0.5.8";
        }
    }

    public static readonly (string Ver, string Date)[] Versions =
    [
        ("v0.5.8",   "Jul 23, 2026"),
        ("v0.5.7.1", "Jul 11, 2026"),
        ("v0.5.7",   "Jul 11, 2026"),
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
        ["v0.5.8"] =
        [
            new("Notifications", "Benachrichtigungen",
            [
                new("ADDED",    "New in-game notification system for status updates and confirmations",
                                 "Neues In-Game-Benachrichtigungssystem für Status-Updates und Bestätigungen"),
                new("ADDED",    "Notifications for entering venues, favorites, hiding venues, teleporting, config sync and more",
                                 "Benachrichtigungen beim Betreten von Venues, Favorisieren, Ausblenden, Teleportieren, Config-Sync u.v.m."),
                new("ADDED",    "Optional event start reminders, off by default",
                                 "Optionale Erinnerungen an Event-Starts, standardmäßig deaktiviert"),
                new("ADDED",    "Notification position, stack size and duration configurable in Settings",
                                 "Position, Stapelgröße und Dauer der Benachrichtigungen in den Einstellungen konfigurierbar"),
                new("ADDED",    "Notifications automatically pause during combat and duties, then resume afterward",
                                 "Benachrichtigungen pausieren automatisch während Kampf und Duty und laufen danach weiter"),
            ]),
            new("Owner Verification", "Owner-Verifizierung",
            [
                new("ADDED",    "\"Update Venue\" button in the About tab lets you re-verify and reload your venue's data for editing, in its own dedicated window separate from Owner Setup",
                                 "\"Update Venue\"-Button im About-Tab lässt dich deine Venue-Daten erneut verifizieren und zum Bearbeiten laden, in einem eigenen Fenster getrennt vom Owner Setup"),
                new("ADDED",    "Owner ID registration - lock editing of your venue to your own registered owner(s), with a themed verification animation",
                                 "Owner-ID-Registrierung - sperrt das Bearbeiten deiner Venue auf registrierte Owner, mit einer thematischen Verifizierungs-Animation"),
                new("ADDED",    "\"Register my Owner ID\" checkbox lets you opt in or out of locking your venue when submitting or updating it",
                                 "\"Register my Owner ID\"-Checkbox lässt dich beim Absenden oder Aktualisieren selbst entscheiden, ob deine Venue gesperrt wird"),
                new("ADDED",    "\"Copy Owner ID JSON\" button in the Debug window for quickly registering as an owner",
                                 "\"Copy Owner ID JSON\"-Button im Debug-Fenster zum schnellen Registrieren als Owner"),
                new("CHANGED",  "Discord name is no longer required when updating an already-registered venue",
                                 "Discord-Name ist beim Aktualisieren einer bereits registrierten Venue nicht mehr erforderlich"),
            ]),
            new("Setup Wizard", "Setup-Wizard",
            [
                new("ADDED",    "Completely redesigned setup wizard with a step progress indicator, icon highlights, and a reordered flow (language selection now comes first and applies instantly)",
                                 "Setup-Wizard komplett überarbeitet mit Fortschrittsanzeige, Icon-Highlights und neuer Reihenfolge (Sprachauswahl kommt jetzt zuerst und wird sofort angewendet)"),
                new("ADDED",    "After a major update, the wizard now explains why it reappeared and can't be skipped until you've clicked through it",
                                 "Nach einem größeren Update erklärt der Wizard jetzt, warum er wieder erscheint, und lässt sich nicht überspringen, bis man ihn durchgeklickt hat"),
                new("ADDED",    "Setup wizard now covers all current settings (notification position/duration, suppress-in-combat, event reminders, auto-pull config, and more), not just a couple of basics",
                                 "Setup-Wizard deckt jetzt alle aktuellen Einstellungen ab (Notification-Position/-Dauer, Suppress-in-Combat, Event Reminders, Auto-Pull-Config u.v.m.), nicht mehr nur ein paar Basics"),
                new("ADDED",    "Setup wizard now points venue owners toward the quick \"Copy Owner ID JSON\" registration path if they don't need to change anything about their venue",
                                 "Setup-Wizard weist Venue-Ownern jetzt auf den schnellen \"Copy Owner ID JSON\"-Registrierungsweg hin, falls sich an ihrer Venue nichts ändern muss"),
            ]),
            new(null, null,
            [
                new("ADDED",    "NSFW tag for venues, shown as a badge in the directory (SFW venues get a badge too)",
                                 "NSFW-Kennzeichnung für Venues, als Badge im Verzeichnis angezeigt (SFW-Venues bekommen ebenfalls ein Badge)"),
                new("ADDED",    "\"Boost currently open venues to the top\" option in Settings, on by default",
                                 "Option \"Gerade geöffnete Venues nach oben schieben\" in den Einstellungen, standardmäßig aktiv"),
                new("IMPROVED", "Debug window location status now distinguishes Inside / Garden / Not in Housing, with garden-aware venue matching",
                                 "Standort-Status im Debug-Fenster unterscheidet jetzt Inside / Garten / Nicht im Housing, mit gartenbewusster Venue-Erkennung"),
                new("ADDED",    "Hide Venue option in the directory, with a shared show/hide toggle between Directory and Events tabs",
                                 "Venue-ausblenden-Option im Verzeichnis, mit gemeinsamem Ein-/Ausblenden-Schalter zwischen Directory- und Events-Tab"),
                new("ADDED",    "A handful of hidden secrets scattered around the plugin - see if you can find them all",
                                 "Ein paar versteckte Geheimnisse im Plugin verteilt - findest du sie alle?"),
                new("ADDED",    "Default tab setting - always open to Map, Directory or Events, or remember the last used tab",
                                 "Standard-Tab-Einstellung - immer Map, Directory oder Events öffnen, oder zuletzt genutzten Tab merken"),
                new("ADDED",    "Reset Window Position and Unhide All Venues buttons in Settings",
                                 "Buttons 'Position zurücksetzen' und 'Alle Venues einblenden' in den Einstellungen"),
                new("FIXED",    "Housing district auto-detection in Owner Setup now correctly handles subdivision/private plots",
                                 "Automatische Bezirks-Erkennung im Owner Setup erkennt jetzt Subdivision-/Private-Plots korrekt"),
                new("FIXED",    "\"Update Venue\" now also works while standing in your garden, not just inside the house",
                                 "\"Update Venue\" funktioniert jetzt auch im Garten, nicht nur innerhalb des Hauses"),
                new("FIXED",    "A couple of potential crashes in the Changelog and Owner Setup windows",
                                 "Ein paar potenzielle Abstürze im Changelog- und Owner-Setup-Fenster behoben"),
                new("FIXED",    "About tab GitHub link pointed to the wrong repository",
                                 "GitHub-Link im About-Tab zeigte auf das falsche Repository"),
                new("FIXED",    "A few small internal fixes and cleanups",
                                 "Ein paar kleine interne Fixes und Aufräumarbeiten"),
                new("REMOVED",  "Custom hotkey to open the window",
                                 "Eigener Hotkey zum Öffnen des Fensters"),
            ]),
        ],
        ["v0.5.7.1"] =
        [
            new(null, null,
            [
                new("FIXED", "Venue map no longer shows when standing in garden or outdoor housing area",
                             "Venue-Karte wird nicht mehr angezeigt wenn man im Garten oder im Außenbereich steht"),
                new("FIXED", "Venue map no longer shows in non-housing zones (e.g. Limsa Lominsa)",
                             "Venue-Karte wird nicht mehr in Nicht-Housing-Zonen angezeigt (z.B. Limsa Lominsa)"),
                new("FIXED", "Empyreum housing district now detected correctly in all house types",
                             "Empyreum wird jetzt in allen Haustypen korrekt erkannt"),
                new("IMPROVED", "Housing detection fully rewritten using HousingManager API - no hardcoded territory IDs",
                                "Housing-Erkennung komplett mit HousingManager API neu geschrieben - keine hardcodierten Territory-IDs mehr"),
            ]),
        ],
        ["v0.5.7"] =
        [
            new(null, null,
            [
                new("IMPROVED", "Venue detection now uses datacenter + ward/plot matching instead of territory IDs - works reliably in all house types",
                                "Venue-Erkennung nutzt jetzt Datacenter + Ward/Plot statt Territory-IDs - funktioniert zuverlässig in allen Haustypen"),
                new("IMPROVED", "Floor names, service labels and schedule status fully localizable via RESX and Crowdin",
                                "Etagennamen, Service-Labels und Zeitstatus vollständig über RESX und Crowdin lokalisierbar"),
                new("FIXED",    "HERE badge and venue map correctly match venues across all datacenters",
                                "HIER-Badge und Venue-Karte erkennen Venues jetzt korrekt über alle Datacenter"),
                new("FIXED",    "Favorite venues now sorted to top of directory",
                                "Favorisierte Venues werden jetzt oben im Verzeichnis angezeigt"),
                new("FIXED",    "Schedule status text now shows correct umlauts in German",
                                "Zeitstatus zeigt jetzt korrekte Umlaute auf Deutsch"),
            ]),
        ],
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
