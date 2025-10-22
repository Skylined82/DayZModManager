using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DayZManager
{
    public sealed class SettingsModel
    {
        public string DayZServerPath { get; set; } = "";
        public string WorkshopPath { get; set; } = "";
        public string AddonBuilderPath { get; set; } = "";
        public string ServerConfig { get; set; } = @".\Servers\serverDZ.cfg";
        public string ProfilesDir { get; set; } = @".\Servers\profiles";
        public string MissionPath { get; set; } = @".\Missions\example.mission";
        public string DayZGamePath { get; set; } = "";

        // Launch behavior
        public bool ClientAutoLaunch { get; set; } = true;
        public bool UseDiagClient { get; set; } = false;    // DayZDiag_x64.exe for client
        public bool RunServerInDiag { get; set; } = false;  // DayZDiag_x64.exe (game dir) vs DayZServer_x64.exe (server dir)

        // Selected mods (load order)
        public string[] ExtraMods { get; set; } = Array.Empty<string>();

        // Window state
        public bool StartMaximized { get; set; } = true;
        public int MainX { get; set; } = -1;
        public int MainY { get; set; } = -1;
        public int MainW { get; set; } = 1280;
        public int MainH { get; set; } = 800;

        // Paths & persistence
        [JsonIgnore] public string SettingsPath { get; private set; } = "";
        public string RepoRoot { get; set; } = "";

        private static void EnsureStructure(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "Settings"));
            Directory.CreateDirectory(Path.Combine(root, "Missions"));
            Directory.CreateDirectory(Path.Combine(root, "WorkingMods"));
            Directory.CreateDirectory(Path.Combine(root, "BuiltMods"));
            Directory.CreateDirectory(Path.Combine(root, "Servers"));
            Directory.CreateDirectory(Path.Combine(root, "Servers", "profiles"));

            string cfg = Path.Combine(root, "Servers", "serverDZ.cfg");
            if (!File.Exists(cfg))
            {
                File.WriteAllText(cfg,
@"hostname = ""DayZ Local Dev"";
password = """";
passwordAdmin = """";
enableWhitelist = 0;
maxPlayers = 60;
BattlEye = 0;                    // Diag: off
verifySignatures = 0;            // Diag dev only
allowFilePatching = 1;
forceSameBuild = 1;

disableVoN = 0;
vonCodecQuality = 20;
disable3rdPerson=0;
disableCrosshair=0;
disablePersonalLight = 1;
lightingConfig = 0;

serverTime = ""SystemTime"";
serverTimeAcceleration = 12;
serverTimePersistent = 0;

guaranteedUpdates = 1;
loginQueueConcurrentPlayers = 5;
loginQueueMaxPlayers = 500;

instanceId = 1;
storageAutoFix = 1;
steamQueryPort = 27016;

class Missions
{
    class DayZ
    {
        template = ""empty.alteria"";
    };
};");
            }

            Directory.CreateDirectory(Path.Combine(root, "Missions", "example.mission"));
        }

        private static void EnsureDefaultSettings(string settingsJsonPath)
        {
            if (File.Exists(settingsJsonPath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(settingsJsonPath)!);

            var defaultSettings = new SettingsModel
            {
                DayZServerPath = "",
                DayZGamePath = "",
                WorkshopPath = "",
                AddonBuilderPath = "",
                ServerConfig = @".\Servers\serverDZ.cfg",
                ProfilesDir = @".\Servers\profiles",
                MissionPath = @".\Missions\example.mission",
                ClientAutoLaunch = true,
                UseDiagClient = false,
                RunServerInDiag = false,
                StartMaximized = true,
                MainW = 1280,
                MainH = 800
            };

            var json = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsJsonPath, json);
        }

        public static SettingsModel Load()
        {
            // Settings file beside the EXE: <EXE>\Settings\settings.json
            string appBase = AppContext.BaseDirectory;
            string settingsPath = Path.Combine(appBase, "Settings", "settings.json");
            EnsureDefaultSettings(settingsPath);

            var json = File.ReadAllText(settingsPath);
            var cfg = JsonSerializer.Deserialize<SettingsModel>(json, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new SettingsModel();

            // Determine workspace
            string root = string.IsNullOrWhiteSpace(cfg.RepoRoot) ? appBase : cfg.RepoRoot;
            EnsureStructure(root);

            cfg.SettingsPath = settingsPath;
            cfg.RepoRoot = root;
            return cfg;
        }

        public IEnumerable<string> GetMissingCriticalPaths(bool includeOptional = false)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(DayZGamePath) || !Directory.Exists(DayZGamePath)) missing.Add(nameof(DayZGamePath));
            if (string.IsNullOrWhiteSpace(DayZServerPath) || !Directory.Exists(DayZServerPath)) missing.Add(nameof(DayZServerPath));
            if (string.IsNullOrWhiteSpace(AddonBuilderPath) || !File.Exists(AddonBuilderPath)) missing.Add(nameof(AddonBuilderPath));

            if (includeOptional)
            {
                if (!string.IsNullOrWhiteSpace(WorkshopPath) && !Directory.Exists(WorkshopPath))
                    missing.Add(nameof(WorkshopPath));
            }

            return missing;
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(SettingsPath))
                throw new InvalidOperationException("SettingsPath not set. Call SettingsModel.Load() first.");

            var opt = new JsonSerializerOptions { WriteIndented = true };
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, opt));
        }

        public string Resolve(string pathOrRel)
        {
            if (string.IsNullOrWhiteSpace(pathOrRel)) return "";
            if (Path.IsPathRooted(pathOrRel)) return pathOrRel;
            var baseDir = string.IsNullOrWhiteSpace(RepoRoot) ? AppContext.BaseDirectory : RepoRoot;
            return Path.GetFullPath(Path.Combine(baseDir, pathOrRel));
        }
    }
}
