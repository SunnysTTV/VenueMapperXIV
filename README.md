# VenueMapper

A Dalamud plugin for discovering and exploring FFXIV housing venues. Browse venues, view interactive maps, track live events, and submit your own venue.

![Dalamud](https://img.shields.io/badge/Dalamud-Plugin-blue) ![API Level](https://img.shields.io/badge/API%20Level-15-green) ![License](https://img.shields.io/badge/license-MIT-lightgrey)

## Features

- **Interactive Housing Maps** — 2D map with zoom, pan, and floor switching using in-game textures
- **3D World Markers** — Pictomancy-powered markers at service locations (bar, DJ, gambling, etc.)
- **Venue Directory** — Browse all venues with favorites, search, datacenter and server filters
- **Live Events** — Upcoming and active events from Partake.gg with NOW badge for running events
- **Teleport Integration** — One-click teleport to any venue via Lifestream IPC
- **Owner Tools** — Submit your venue with auto-captured coordinates and Google Form integration
- **Multi-Language** — English and Deutsch (RESX-based, Crowdin-ready)
- **Auto-Update** — Venue config syncs from GitHub hourly via ETag polling

## Installation

1. Open Dalamud Settings (`/xlsettings`)
2. Go to **Experimental** tab
3. Add this custom repository URL:
   ```
   https://raw.githubusercontent.com/SunnysTTV/sunnysrepo/main/pluginmaster.json
   ```
4. Save, then search for **Venue Mapper** in the Plugin Installer

## Translate

VenueMapper uses RESX-based localization and is ready for community translations. Currently supported: **English** and **German**. Want to help translate? Contribute on [Crowdin](https://crowdin.com/project/venuemapper) or join our [Discord](https://discord.com/invite/agKWEzK5nR)!

## For Venue Owners

Want your venue in VenueMapper?

1. Open the Owner Setup via `/vmapper owner`
2. Fill in your venue details (name, location, colors)
3. Add services and use **"Use my pos"** to auto-capture coordinates
4. Export via Google Form or copy JSON

Your venue will appear in the directory after review.

## How It Works

- **Maps** are loaded from FFXIV's game data via Lumina — no external images needed
- **Venue data** is stored in `venues.json` on GitHub and auto-synced to all users
- **Ward/Plot detection** uses `HousingManager` for precise venue matching
- **Events** are fetched from the Partake.gg GraphQL API

## Built With

- [Dalamud](https://github.com/goatcorp/Dalamud) — Plugin framework
- [Lumina](https://github.com/NotAdam/Lumina) — FFXIV data access
- [Pictomancy](https://github.com/sourpuh/Pictomancy) — 3D world-space rendering
- [Lifestream](https://github.com/NightmareXIV/Lifestream) — Teleport IPC
- [Partake.gg](https://partake.gg) — Event data API

## Links

- [Discord](https://discord.com/invite/agKWEzK5nR)
- [Support on Ko-Fi](https://ko-fi.com/sunnysofficial)
- [Report Issues](https://github.com/SunnysTTV/VenueMapperXIV/issues)

## License

MIT
