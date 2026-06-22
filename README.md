# VenueMapper for FFXIV

A Dalamud plugin for discovering and exploring FFXIV venues with interactive 2D map markers and 3D service indicators, live event tracking and integrated venue owner tools. Your all-in-one FFXIV venue companion.

## Features

✨ **Housing Venue Discovery**
- Interactive housing maps for all territories (Mist, Lavender Beds, The Goblet, Shirogane, Empyreum)
- Browse venues by server and datacenter
- View venue details: location, services, social links, and more

🎪 **Venue Services & Details**
- See what services each venue offers (Bar, DJ, Gambling, Entrance, etc.)
- 3D Pictomancy markers showing service locations
- Multi-floor support (Ground, Second Floor, Cellar)
- Customizable venue colors and branding

📅 **Live Events Integration**
- View upcoming events from Partake.gg
- Real-time event listings for specific venues
- Event details: date, time, description, attendee count

🌍 **Multi-Language Support**
- English (EN)
- Deutsch (DE)

**Want to help translate?**
[Join our Crowdin project](https://crowdin.com/project/venuemapper/invite?h=025340303bb4127cbda1a808692fb3e22804757)

⚙️ **Owner Tools**
- Submit venue information via integrated Google Form
- Manage venue details: colors, services, social links
- Auto-fill venue location data

## Installation

1. **Add Repository to Dalamud**
   - Open Dalamud Settings
   - Go to: Experimental → Custom Plugin Repositories
   - Add: `https://raw.githubusercontent.com/SunnysTTV/sunnysrepo/main/pluginmaster.json`

2. **Install Plugin**
   - Open Plugin Installer
   - Search: "VenueMapper"
   - Click "Install"

3. **Enable & Use**
   - Open VenueMapper from Plugin list
   - Browse venues, events, and venue details!

## Commands

/vmapper — Open main VenueMapper window
/vmapper owner — Owner setup (venue submission)

## For Venue Owners

⚠️ **Important: Use `/vmapper owner` in-game first!**

1. In-game, type: `/vmapper owner`
2. Fill in your venue details in the plugin window
3. Submit to Google Form via the plugin (don't open form manually)
4. Wait for admin review
5. Your venue will appear in VenueMapper when added to venues.json

[📋 Venue Submission Form](https://docs.google.com/forms/d/e/1FAIpQLSeXKwEDbHQzjoOFH4o5WLTfd2K7m_KwiKp9kiWAHCxTKcpELg/viewform)

## Data Source

Venue data is managed via `venues.json` hosted on GitHub and auto-updated in the plugin hourly.

- **Repository**: https://github.com/SunnysTTV/VenueMapperXIV
- **Config File**: `Resources/venues.json`
- **Auto-Update**: Every hour via ETag polling

## Credits

**Developer**
- Sunny ([@sunnysofficial](https://sunnysofficial.com))

**Logo**
- Hultay — VenueMapper logo

**Built With**
- [Dalamud](https://github.com/goatcorp/Dalamud)
- [Lumina](https://github.com/NotAdam/Lumina)
- [Pictomancy](https://github.com/sourpuh/ffxiv_pictomancy)
- GraphQL.NET [Partake Events](https://partake.gg)
- ImGui.NET

## Community & Support

🔗 **Links**
- Twitch: https://twitch.tv/sunnysofficial
- Discord: https://discord.gg/agKWEzK5nR
- Support: https://ko-fi.com/sunnysofficial
- Website: https://sunnysofficial.com

## License

MIT License — See LICENSE file for details

```
**VenueMapper v0.5.0**
Last Updated: June 2026
```
