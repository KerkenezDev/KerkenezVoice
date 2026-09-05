using System;
using System.IO;
using System.Text.Json;
using KerkenezVoice.Models;

namespace KerkenezVoice.Services
{
    public class ConfigService
    {
        // Roaming AppData (Configuration, Presets, Lexicon)
        public static readonly string SuiteFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kerkenez");

        public static readonly string AppDataFolder = Path.Combine(SuiteFolder, "voice");
        public static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "config.json");
        public static readonly string PresetsFolder = Path.Combine(AppDataFolder, "presets");
        public static readonly string FxPresetsFolder = Path.Combine(AppDataFolder, "presets", "fx");

        // LocalAppData Programs (Permanent Model Weights, Custom Voice embeddings, Cache)
        public static readonly string LocalProgramsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Kerkenez", "voice");

        public static readonly string ModelsFolder = Path.Combine(LocalProgramsFolder, "models");
        public static readonly string CustomVoicesFolder = Path.Combine(LocalProgramsFolder, "custom_voices");
        public static readonly string CacheFolder = Path.Combine(LocalProgramsFolder, "cache");

        public static readonly string TempFolder = Path.Combine(
            Path.GetTempPath(),
            "Kerkenez", "voice");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static bool IsFirstInstallation { get; } = !File.Exists(ConfigFilePath);

        public AppSettings Settings { get; private set; }

        public event Action? SettingsChanged;

        public ConfigService()
        {
            EnsureAppDataDirectory();
            Settings = LoadConfig();
        }

        private static void EnsureAppDataDirectory()
        {
            try
            {
                // Roaming configuration directories
                if (!Directory.Exists(SuiteFolder)) Directory.CreateDirectory(SuiteFolder);
                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                if (!Directory.Exists(PresetsFolder)) Directory.CreateDirectory(PresetsFolder);
                if (!Directory.Exists(FxPresetsFolder)) Directory.CreateDirectory(FxPresetsFolder);

                // LocalAppData permanent model & data directories
                if (!Directory.Exists(LocalProgramsFolder)) Directory.CreateDirectory(LocalProgramsFolder);
                if (!Directory.Exists(ModelsFolder)) Directory.CreateDirectory(ModelsFolder);
                if (!Directory.Exists(CustomVoicesFolder)) Directory.CreateDirectory(CustomVoicesFolder);
                if (!Directory.Exists(CacheFolder)) Directory.CreateDirectory(CacheFolder);

                // Check for migration from roaming or legacy KokoroTTS
                MigrateModelsAndVoices();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Directory init error: {ex.Message}");
            }
        }

        private static void MigrateModelsAndVoices()
        {
            try
            {
                // 1. If models exist in Roaming %APPDATA%\Kerkenez\voice\models, copy them to %LOCALAPPDATA%\Programs\Kerkenez\voice\models
                string roamingModels = Path.Combine(AppDataFolder, "models");
                if (Directory.Exists(roamingModels))
                {
                    foreach (var file in Directory.GetFiles(roamingModels))
                    {
                        string dest = Path.Combine(ModelsFolder, Path.GetFileName(file));
                        if (!File.Exists(dest))
                        {
                            try { File.Copy(file, dest, false); } catch { }
                        }
                    }
                }

                // 2. If custom voices exist in Roaming, copy to LocalAppData
                string roamingCustomVoices = Path.Combine(AppDataFolder, "custom_voices");
                if (Directory.Exists(roamingCustomVoices))
                {
                    foreach (var file in Directory.GetFiles(roamingCustomVoices))
                    {
                        string dest = Path.Combine(CustomVoicesFolder, Path.GetFileName(file));
                        if (!File.Exists(dest))
                        {
                            try { File.Copy(file, dest, false); } catch { }
                        }
                    }
                }

                // 3. Fallback check from legacy KokoroTTS directory
                string legacyFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "KokoroTTS");

                if (Directory.Exists(legacyFolder))
                {
                    string legacyModels = Path.Combine(legacyFolder, "models");
                    if (Directory.Exists(legacyModels))
                    {
                        foreach (var file in Directory.GetFiles(legacyModels))
                        {
                            string dest = Path.Combine(ModelsFolder, Path.GetFileName(file));
                            if (!File.Exists(dest))
                            {
                                try { File.Copy(file, dest, false); } catch { }
                            }
                        }
                    }

                    string legacyCustomVoices = Path.Combine(legacyFolder, "custom_voices");
                    if (Directory.Exists(legacyCustomVoices))
                    {
                        foreach (var file in Directory.GetFiles(legacyCustomVoices))
                        {
                            string dest = Path.Combine(CustomVoicesFolder, Path.GetFileName(file));
                            if (!File.Exists(dest))
                            {
                                try { File.Copy(file, dest, false); } catch { }
                            }
                        }
                    }

                    string legacyPresets = Path.Combine(legacyFolder, "presets");
                    if (Directory.Exists(legacyPresets))
                    {
                        foreach (var file in Directory.GetFiles(legacyPresets, "*.json"))
                        {
                            string dest = Path.Combine(PresetsFolder, Path.GetFileName(file));
                            if (!File.Exists(dest))
                            {
                                try { File.Copy(file, dest, false); } catch { }
                            }
                        }
                    }

                    string legacyConfig = Path.Combine(legacyFolder, "config.json");
                    if (!File.Exists(ConfigFilePath) && File.Exists(legacyConfig))
                    {
                        try { File.Copy(legacyConfig, ConfigFilePath, true); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Migration error: {ex.Message}");
            }
        }

        public AppSettings LoadConfig()
        {
            try
            {
                EnsureAppDataDirectory();

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Settings = HealAndNormalizeSettings(loaded);
                        return Settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Config read error: {ex.Message}");
            }

            Settings = HealAndNormalizeSettings(new AppSettings());
            SaveConfig(Settings);
            return Settings;
        }

        public bool SaveConfig(AppSettings? settings = null)
        {
            try
            {
                EnsureAppDataDirectory();
                var toSave = settings ?? Settings;
                toSave = HealAndNormalizeSettings(toSave);
                Settings = toSave;

                string json = JsonSerializer.Serialize(toSave, JsonOptions);
                File.WriteAllText(ConfigFilePath, json);
                SettingsChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Save config error: {ex.Message}");
                return false;
            }
        }

        private static AppSettings HealAndNormalizeSettings(AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.AppVersion)) s.AppVersion = "1.0.0";
            if (string.IsNullOrWhiteSpace(s.Language)) s.Language = "en";
            if (string.IsNullOrWhiteSpace(s.Voice)) s.Voice = "af_heart";
            if (string.IsNullOrWhiteSpace(s.Format)) s.Format = "wav";
            if (string.IsNullOrWhiteSpace(s.SplitPattern)) s.SplitPattern = @"\n+";
            if (s.Speed < 0.25 || s.Speed > 4.0) s.Speed = 1.0;
            if (s.Volume < 0.0 || s.Volume > 3.0) s.Volume = 1.0;
            if (s.Pitch < -24.0 || s.Pitch > 24.0) s.Pitch = 0.0;
            if (s.NumThreads < 1) s.NumThreads = 1;
            if (s.NumThreads > 16) s.NumThreads = 16;
            if (s.WindowWidthScale < 0.3 || s.WindowWidthScale > 1.0) s.WindowWidthScale = 0.60;
            if (s.WindowHeightScale < 0.3 || s.WindowHeightScale > 1.0) s.WindowHeightScale = 0.56;
            if (s.Lexicon == null) s.Lexicon = new System.Collections.Generic.Dictionary<string, string>();
            return s;
        }

        public static void CleanTempFolder()
        {
            try
            {
                if (Directory.Exists(TempFolder))
                {
                    Directory.Delete(TempFolder, true);
                }
            }
            catch { }
        }
    }
}
