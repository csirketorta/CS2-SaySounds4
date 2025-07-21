using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;

public class SaySounds3 : BasePlugin
{
    public override string ModuleName => "SaySounds4";
    public override string ModuleVersion => "4.0";
    public override string ModuleAuthor => "csirk";

    private string _connectionString = string.Empty;
    private readonly Dictionary<string, bool> _mutedPlayers = new();
    private readonly Dictionary<string, DateTime> _lastSoundTrigger = new();
    private TimeSpan SoundCooldown = TimeSpan.FromSeconds(10);

    private Dictionary<string, string> TriggerSounds = new();
    private bool _adminOnly = false;
    private string _adminGroup = "@css/generic";

    private class Config
    {
        public DatabaseConfig Database { get; set; } = new();
        public int SoundCooldownSeconds { get; set; } = 10;
        public bool AdminOnly { get; set; } = false;
        public Dictionary<string, string> Triggers { get; set; } = new();
        public string AdminGroup { get; set; } = "@css/generic";
    }

    private class DatabaseConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 3306;
        public string Name { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
    }

    private void LoadConfig()
    {
        string configPath = Path.Combine(ModuleDirectory, "saysounds_config.json");

        if (!File.Exists(configPath))
        {
            Logger.LogWarning("[SaySounds4] No config found, creating default one...");

            var defaultConfig = new Config
            {
                Database = new DatabaseConfig
                {
                    Host = "localhost",
                    Port = 3306,
                    Name = "dbname",
                    User = "dbuser",
                    Password = "userpass"
                },
                SoundCooldownSeconds = 10,
                AdminOnly = false,
                AdminGroup = "@css/generic",
                Triggers = new Dictionary<string, string>
                {
                    { "apam",      "sounds/saysounds/apam.wav" },
                    { "baszki",    "sounds/saysounds/baszki.wav" },
                    { "boom",      "sounds/saysounds/boom.wav" },
                    { "brutalis",  "sounds/saysounds/brutalis.wav" },
                    { "csunyan",   "sounds/saysounds/csunyan.wav" },
                    { "eridj",     "sounds/saysounds/eridj.wav" },
                    { "fain",      "sounds/saysounds/fain.wav" },
                    { "fulke",     "sounds/saysounds/fulke.wav" },
                    { "gyenge",    "sounds/saysounds/gyenge.wav" },
                    { "gyikok",    "sounds/saysounds/gyikok.wav" },
                    { "hali",      "sounds/saysounds/hali.wav" },
                    { "hallgass",  "sounds/saysounds/hallgass.wav" },
                    { "harc",      "sounds/saysounds/harc.wav" },
                    { "haver",     "sounds/saysounds/haver.wav" },
                    { "hehe",      "sounds/saysounds/hehe.wav" },
                    { "hopp",      "sounds/saysounds/hopp.wav" },
                    { "joska",     "sounds/saysounds/joska.wav" },
                    { "kuss",      "sounds/saysounds/kuss.wav" },
                    { "kutyak",    "sounds/saysounds/kutyak.wav" },
                    { "natha",     "sounds/saysounds/natha.wav" },
                    { "ne",        "sounds/saysounds/ne.wav" },
                    { "nyoma",     "sounds/saysounds/nyoma.wav" },
                    { "olni",      "sounds/saysounds/olni.wav" },
                    { "olom",      "sounds/saysounds/olom.wav" },
                    { "pofad",     "sounds/saysounds/pofad.wav" },
                    { "pofajat",   "sounds/saysounds/pofajat.wav" },
                    { "szeva",     "sounds/saysounds/szeva.wav" },
                    { "torveny",   "sounds/saysounds/torveny.wav" },
                    { "utso",      "sounds/saysounds/utso.wav" },
                    { "zabalsz",   "sounds/saysounds/zabalsz.wav" }
                }
            };

            string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        string configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<Config>(configJson);

        if (config != null)
        {
            TriggerSounds = config.Triggers ?? new();
            SoundCooldown = TimeSpan.FromSeconds(config.SoundCooldownSeconds);
            _adminOnly = config.AdminOnly;
            _adminGroup = string.IsNullOrEmpty(config.AdminGroup) ? "@css/generic" : config.AdminGroup;

            var db = config.Database;
            _connectionString =
                $"Server={db.Host};Port={db.Port};" +
                $"Database={db.Name};" +
                $"User ID={db.User};" +
                $"Password={db.Password};" +
                "AllowPublicKeyRetrieval=True;SslMode=none;";
        }

        Logger.LogInformation($"[SaySounds4] Loaded config with {TriggerSounds.Count} triggers, cooldown {SoundCooldown.TotalSeconds}s, AdminOnly={_adminOnly}, AdminGroup={_adminGroup}");
    }

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);
        LoadConfig();

        RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        Logger.LogInformation("[SaySounds4] Loaded + chat/connect hooks registered!");
    }

    private void ShowSaySoundsList(CCSPlayerController player)
    {
        var triggers = new List<string>(TriggerSounds.Keys);
        triggers.Sort();

        const int perLine = 8;
        int total = triggers.Count;
        int index = 0;

        player.PrintToChat("[SaySounds4] Available sounds:");

        while (index < total)
        {
            int count = Math.Min(perLine, total - index);
            var lineItems = triggers.GetRange(index, count);
            string line = string.Join(", ", lineItems);
            player.PrintToChat(line);

            index += perLine;
        }
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
    {
        var player = ev.Userid;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        string steamId = player.SteamID.ToString();
        LoadMuteStateFromDB(steamId);

        return HookResult.Continue;
    }

    private async void LoadMuteStateFromDB(string steamId)
    {
        if (string.IsNullOrEmpty(_connectionString)) return;

        try
        {
            using var conn = new MySqlConnector.MySqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = "SELECT muted FROM saysounds_preferences WHERE steamid = @steamid LIMIT 1";
            using var cmd = new MySqlConnector.MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@steamid", steamId);

            var result = await cmd.ExecuteScalarAsync();
            bool muted = result != null && Convert.ToBoolean(result);

            _mutedPlayers[steamId] = muted;
            Logger.LogInformation($"[SaySounds4] Loaded mute state for {steamId}: {muted}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SaySounds4] DB load error for {steamId}: {ex.Message}");
        }
    }

    private bool IsAdmin(CCSPlayerController player)
    {
        return AdminManager.PlayerHasPermissions(player, _adminGroup);
    }

    private HookResult OnPlayerChat(EventPlayerChat ev, GameEventInfo info)
    {
        if (ev.Text == null || ev.Userid == null)
            return HookResult.Continue;

        string msg = ev.Text.Trim().ToLower();
        var triggeringPlayer = Utilities.GetPlayerFromUserid(ev.Userid);
        if (triggeringPlayer == null || !triggeringPlayer.IsValid)
            return HookResult.Continue;

        string steamId = triggeringPlayer.SteamID.ToString();

        if (msg == "!toggless" || msg == "/togless")
        {
            bool currentMuted = _mutedPlayers.TryGetValue(steamId, out var m) && m;
            bool newMutedState = !currentMuted;

            _mutedPlayers[steamId] = newMutedState;
            SaveMuteStateToDB(steamId, newMutedState);

            triggeringPlayer.PrintToChat(newMutedState
                ? "[SaySounds4] You muted all sounds played by SaySounds."
                : "[SaySounds4] You enabled all sounds played by SaySounds.");

            return HookResult.Continue;
        }

        if (msg == "!saysounds")
        {
            ShowSaySoundsList(triggeringPlayer);
            return HookResult.Continue;
        }

        if (!TriggerSounds.TryGetValue(msg, out string soundPath))
            return HookResult.Continue;

        if (_adminOnly && !IsAdmin(triggeringPlayer))
        {
            triggeringPlayer.PrintToChat("[SaySounds4] Only admins can trigger sounds.");
            return HookResult.Continue;
        }

        Logger.LogInformation($"[SaySounds4] Player {triggeringPlayer.PlayerName} triggered sound command '{msg}'");

        DateTime now = DateTime.UtcNow;
        if (_lastSoundTrigger.TryGetValue(steamId, out var lastTriggerTime))
        {
            var timeSinceLast = now - lastTriggerTime;
            if (timeSinceLast < SoundCooldown)
            {
                double secondsLeft = Math.Ceiling((SoundCooldown - timeSinceLast).TotalSeconds);
                string secondWord = secondsLeft == 1 ? "second" : "seconds";
                triggeringPlayer.PrintToChat($"[SaySounds4] Please wait {secondsLeft:0} more {secondWord} before you play a sound again.");
                return HookResult.Continue;
            }
        }

        _lastSoundTrigger[steamId] = now;

        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            string sid = p.SteamID.ToString();

            if (_mutedPlayers.TryGetValue(sid, out bool muted) && muted)
                continue;

            p.ExecuteClientCommand($"play {soundPath}");
        }
        string fileName = System.IO.Path.GetFileName(soundPath);
        Logger.LogInformation($"[SaySounds4] Sound '{fileName}' played for all players (triggered by {triggeringPlayer.PlayerName})");

        return HookResult.Continue;
    }

    private async void SaveMuteStateToDB(string steamId, bool muted)
    {
        if (string.IsNullOrEmpty(_connectionString)) return;

        try
        {
            using var conn = new MySqlConnector.MySqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
            INSERT INTO saysounds_preferences (steamid, muted)
            VALUES (@steamid, @muted)
            ON DUPLICATE KEY UPDATE muted = @muted;";

            using var cmd = new MySqlConnector.MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@steamid", steamId);
            cmd.Parameters.AddWithValue("@muted", muted ? 1 : 0);

            await cmd.ExecuteNonQueryAsync();
            Logger.LogInformation($"[SaySounds4] Saved mute state for {steamId}: {muted}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SaySounds4] DB save error for {steamId}: {ex.Message}");
        }
    }
}