# SaySounds4 Plugin

## What is this plugin?
SaySounds4 is a CounterStrikeSharp plugin for CS2 that allows players to trigger fun sound effects by typing specific chat commands. It also supports database-based mute preferences per player and optional admin-only restrictions.

## Requirements
- **CounterStrikeSharp** API installed on your server
- **MySQL/MariaDB** database for storing mute states
- **MultiAddon manager** to load the custom sounds
- Sound files placed in the correct `csgo/sounds/saysounds/` folder in the addon. (For example take a look at this with Source2Viewer: https://steamcommunity.com/sharedfiles/filedetails/?id=3526275068)

## Features
- ✅ Trigger sounds via chat commands (`apam`, `boom`, etc.)
- ✅ Per-player cooldown to prevent spam
- ✅ `!toggless` lets players mute or unmute sounds for themselves
- ✅ `!saysounds` shows an available list of triggers
- ✅ Optional **AdminOnly mode** so only admins can trigger sounds
- ✅ Configurable **AdminGroup** (default `@css/generic`)
- ✅ Configurable cooldown and triggers via `saysounds_config.json`
- ✅ Stores mute preferences in MySQL with auto-create SQL

## What it is NOT
- ❌ This does NOT handle downloading or precaching sounds (you must ensure clients have the files via MultiAddon manager)
- ❌ It is NOT a music player or sound spam plugin; it’s lightweight & controlled
- ❌ Does NOT include advanced admin menus or GUIs

## Database Setup
Run this SQL script to create the necessary table:

```sql
CREATE TABLE IF NOT EXISTS saysounds_preferences (
    steamid VARCHAR(32) NOT NULL PRIMARY KEY,
    muted TINYINT(1) NOT NULL DEFAULT 0,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

## Installation
1. Copy the latest release into your `csgo/addons/counterstrikesharp/plugins/` folder.
2. Start the server once, it will auto-generate `saysounds_config.json`.
3. Edit `saysounds_config.json`:
   - Add your DB credentials
   - Configure triggers & cooldown
   - Set `AdminOnly` or change `AdminGroup` if needed
4. Place your sound files in the correct folder (`csgo/sounds/saysounds/`)
5. Restart the server.

## Default Commands
- `!saysounds` – Show list of available triggers
- `!toggless` – Toggle sound playback on/off for yourself
- Typing a trigger keyword in chat plays the sound if allowed

## Configuration
`saysounds_config.json` example:
```json
{
  "Database": {
    "Host": "localhost",
    "Port": 3306,
    "Name": "dbname",
    "User": "dbuser",
    "Password": "userpass"
  },
  "SoundCooldownSeconds": 10,
  "AdminOnly": false,
  "AdminGroup": "@css/generic",
  "Triggers": {
    "apam": "sounds/saysounds/apam.wav",
    "boom": "sounds/saysounds/boom.wav"
  }
}
```

Enjoy a lightweight, configurable SaySounds plugin!
