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
            return v != null ? (v.Revision > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}" : $"v{v.Major}.{v.Minor}.{v.Build}") : "v0.5.8.1";
        }
    }

    public static readonly HashSet<string> ForcedSetupVersions = new()
    {
        "v0.5.8",
    };

    public static readonly HashSet<string> HighlightVersions = new()
    {
        "v0.5.9",
    };

    public static readonly (string Ver, string Date)[] Versions =
    [
        ("v0.5.9",   "Aug 7, 2026"),
        ("v0.5.8.1", "Jul 26, 2026"),
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
        ["v0.5.9"] =
        [
            new("Notifications", "Benachrichtigungen",
            [
                new("ADDED",    "New Warning notification style (amber, with a warning icon) for real failures like a failed teleport or config pull, instead of the same neutral look as an ordinary status update",
                                 "Neuer Warning-Benachrichtigungsstil (bernsteinfarben, mit Warn-Icon) für echte Fehlschläge wie einen fehlgeschlagenen Teleport oder Config-Pull, statt dem gleichen neutralen Look wie eine normale Status-Meldung"),
                new("ADDED",    "Notification History window - a running log of recent notifications grouped by day, with filters per kind, styled after the Changelog window and opened via the new bell icon next to it in the title bar",
                                 "Notification-History-Fenster - ein laufendes, nach Tag gruppiertes Protokoll der letzten Benachrichtigungen mit Filtern pro Art, im Look des Changelog-Fensters, erreichbar über das neue Glocken-Icon daneben in der Titelleiste"),
                new("ADDED",    "Hovering a notification now pauses its countdown, so it doesn't disappear while you're still reading it",
                                 "Eine Benachrichtigung mit der Maus überfahren pausiert jetzt ihren Countdown, damit sie beim Lesen nicht verschwindet"),
                new("ADDED",    "A thin countdown bar on each notification shows how much time is left before it dismisses",
                                 "Ein dünner Countdown-Balken an jeder Benachrichtigung zeigt, wie viel Zeit bis zum Verschwinden bleibt"),
                new("ADDED",    "Changing the notification position in Settings shows an instant confirmation at the new spot",
                                 "Ändern der Benachrichtigungs-Position in den Einstellungen zeigt sofort eine Bestätigung an der neuen Stelle"),
                new("IMPROVED", "Notification cards restyled with an accent bar matching the rest of the plugin's card look, plus a small pop-in animation and an ongoing pulse for Warning and Easter Egg notifications",
                                 "Benachrichtigungskarten mit Akzentbalken im Look der restlichen Plugin-Karten neu gestaltet, dazu eine kleine Pop-in-Animation und ein anhaltendes Pulsieren bei Warning- und Easter-Egg-Benachrichtigungen"),
            ]),
            new("Quick Popup", "Quick-Popup",
            [
                new("ADDED", "New quick popup that can appear when you enter a venue, showing its name, district and open/closed status at a glance",
                             "Neues Quick-Popup, das beim Betreten einer Venue erscheinen kann und Name, Bezirk und Öffnungsstatus auf einen Blick zeigt"),
                new("ADDED", "Quick toggles for 3D Markers, Strong Pulse and Marker Color Override right inside the popup, fully synced with Settings",
                             "Schnell-Schalter für 3D-Marker, Strong Pulse und Marker-Farbüberschreibung direkt im Popup, vollständig synchron mit den Einstellungen"),
                new("ADDED", "Copy Address and Directory buttons for quick access without opening the full window",
                             "Copy-Address- und Directory-Buttons für schnellen Zugriff, ohne das ganze Fenster zu öffnen"),
                new("ADDED", "\"Show quick popup on venue enter\" setting, off by default, reacts instantly even if you toggle it while already standing inside a venue",
                             "Einstellung \"Show quick popup on venue enter\", standardmäßig aus, reagiert sofort auch wenn man sie einschaltet während man schon in einer Venue steht"),
                new("ADDED", "\"/vmapper quick\" command to open or close the quick popup manually while inside a venue",
                             "\"/vmapper quick\"-Befehl, um das Quick-Popup manuell zu öffnen oder zu schließen, solange man in einer Venue steht"),
            ]),
            new("Venue Details", "Venue-Details",
            [
                new("ADDED", "New Venue Details window - click a venue's name or the info icon on the Map tab to see its full info in one place",
                             "Neues Venue-Details-Fenster - klicke auf den Namen oder das Info-Icon im Map-Tab, um alle Infos auf einen Blick zu sehen"),
                new("ADDED", "Visit and Copy Lifestream Command buttons for quick teleporting or sharing the exact house location",
                             "Visit- und Copy-Lifestream-Command-Buttons zum schnellen Teleportieren oder Teilen der genauen Hausadresse"),
                new("ADDED", "Venue avatar shown at the top, pulled from Partake or FFXIVVenues with automatic center-cropping so it's never stretched",
                             "Venue-Avatar oben im Fenster, geladen von Partake oder FFXIVVenues mit automatischem Center-Crop, damit nichts mehr verzerrt wird"),
                new("ADDED", "Next Event card with a clear NOW badge for live events, and a fallback message when no event data is available",
                             "Next-Event-Karte mit deutlichem NOW-Badge für laufende Events und einer Fallback-Nachricht, wenn keine Event-Daten verfügbar sind"),
            ]),
            new(null, null,
            [
                new("ADDED",    "Crown filter in the Directory to show only venues you're a registered owner of",
                                 "Kronen-Filter im Verzeichnis, um nur Venues anzuzeigen, bei denen du als Owner registriert bist"),
                new("ADDED",    "Separate \"Auto-open for my own venue\" setting (shown only if you own a registered venue) to opt your own venue out of auto-open while keeping it on for others",
                                 "Eigene Einstellung \"Auto-open für meine Venue\" (nur sichtbar, wenn du eine registrierte Venue besitzt), um Auto-Open für deine eigene Venue separat auszuschalten"),
                new("ADDED",    "\"Send Feedback\" button in the About tab, linking directly to the feedback form",
                                 "\"Send Feedback\"-Button im About-Tab, verlinkt direkt zum Feedback-Formular"),
                new("IMPROVED", "Directory and Events cards now have properly rounded corners and a cleaner accent-bar highlight",
                                 "Directory- und Events-Karten haben jetzt korrekt abgerundete Ecken und einen saubereren Akzentbalken"),
                new("IMPROVED", "Navigation tabs simplified and more stable, no more layout glitches on hover",
                                 "Navigations-Tabs vereinfacht und stabiler, keine Layout-Glitches beim Hovern mehr"),
                new("IMPROVED", "Scrollbars are now consistently styled everywhere, including the Changelog window and a few places that still had the default gray one",
                                 "Scrollbars sind jetzt überall einheitlich gestylt, auch im Changelog-Fenster und an ein paar Stellen, die noch die graue Standard-Scrollbar hatten"),
                new("IMPROVED", "Right-click context menu styling cleaned up",
                                 "Styling des Rechtsklick-Kontextmenüs überarbeitet"),
                new("IMPROVED", "About tab support buttons reorganized into a clearer 2x2 grid",
                                 "Support-Buttons im About-Tab in einem übersichtlicheren 2x2-Raster angeordnet"),
                new("IMPROVED", "Event schedule lookups now use a single efficient bulk request instead of many individual ones, reducing load on the venue data API",
                                 "Event-Zeitplan-Abfragen nutzen jetzt eine einzelne effiziente Sammel-Anfrage statt vieler Einzelanfragen, was die Venue-Daten-API entlastet"),
                new("FIXED",    "Copy Lifestream button now copies the correct teleport command instead of a plain address",
                                 "Copy-Lifestream-Button kopiert jetzt den korrekten Teleport-Befehl statt einer reinen Adresse"),
                new("FIXED",    "Marker color override setting wasn't applied to the marker's background, only its label",
                                 "Die Marker-Farbüberschreibung wurde nur auf das Label angewendet, nicht auf den Marker-Hintergrund"),
                new("FIXED",    "Hardened several windows against a rare crash and UI-rendering-corruption issue",
                                 "Mehrere Fenster gegen einen seltenen Absturz und eine UI-Rendering-Korruption abgesichert"),
                new("FIXED",    "Main window could lose its saved position under certain conditions",
                                 "Hauptfenster konnte unter bestimmten Umständen seine gespeicherte Position verlieren"),
                new("FIXED",    "Some venues' open/closed status could be wrong if they have more than one weekly schedule entry",
                                 "Der Öffnungsstatus mancher Venues konnte falsch sein, wenn sie mehr als einen wöchentlichen Termin haben"),
                new("FIXED",    "A venue's Partake logo could show the wrong image (or fall back to none) if the venue currently has no listed events",
                                 "Das Partake-Logo einer Venue konnte falsch angezeigt werden (oder ganz fehlen), wenn die Venue gerade keine gelisteten Events hat"),
                new("FIXED",    "A failed venue schedule fetch (e.g. a rate limit) could retry instantly in a tight loop instead of waiting for the normal cooldown",
                                 "Ein fehlgeschlagener Venue-Zeitplan-Abruf (z.B. Rate-Limit) konnte sofort in einer Dauerschleife neu versuchen, statt die normale Abklingzeit abzuwarten"),
                new("IMPROVED", "Venue schedule data now survives a plugin reload or game restart instead of needing to refetch",
                                 "Venue-Zeitplan-Daten überstehen jetzt einen Plugin-Reload oder Neustart, statt neu geladen werden zu müssen"),
                new("IMPROVED", "Several Settings labels shortened for a more compact layout, and the auto-open/quick-popup toggles now sit in a fixed two-column grid instead of a layout that could overflow",
                                 "Mehrere Beschriftungen in den Einstellungen für ein kompakteres Layout gekürzt, und die Auto-Open-/Quick-Popup-Schalter stehen jetzt in einem festen Zwei-Spalten-Raster statt einem Layout, das überlaufen konnte"),
                new("IMPROVED", "Minor Settings layout polish (RGB slider label placement, Misc section header)",
                                 "Kleinere Layout-Verbesserungen in den Einstellungen (Position der RGB-Slider-Beschriftung, Misc-Sektionsheader)"),
                new("ADDED",    "\"Hide 3D markers in my venue\" setting next to 3D World Markers - visible to everyone, but only enabled if you have a registered Owner ID",
                                 "Einstellung \"3D-Marker in meiner Venue ausblenden\" neben den 3D-Weltmarkern - für alle sichtbar, aber nur aktivierbar mit registrierter Owner-ID"),
                new("CHANGED",  "The \"Own venue\" auto-open setting is now always visible in Settings (greyed out without a registered Owner ID) instead of being hidden entirely",
                                 "Die \"Own venue\"-Auto-Open-Einstellung ist jetzt immer in den Einstellungen sichtbar (ausgegraut ohne registrierte Owner-ID), statt komplett ausgeblendet zu werden"),
                new("ADDED",    "A short \"What's New\" popup for major updates like this one, summarizing new features and settings without repeating the entire setup wizard",
                                 "Ein kurzes \"What's New\"-Popup für größere Updates wie dieses, das neue Features und Einstellungen zusammenfasst, ohne den kompletten Setup-Assistenten zu wiederholen"),
                new("FIXED",    "A venue's FFXIVVenues banner and open/closed schedule status could fail to load if its FFXIVVenues link used the alternate hash-style URL format instead of the standard /venue/ path format",
                                 "Das FFXIVVenues-Banner und der Öffnungsstatus einer Venue konnten nicht geladen werden, wenn ihr FFXIVVenues-Link das alternative Hash-Format statt des Standard-/venue/-Pfad-Formats verwendete"),
            ]),
        ],
        ["v0.5.8.1"] =
        [
            new(null, null,
            [
                new("FIXED", "A critical issue", "Ein kritisches Problem"),
            ]),
        ],
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
