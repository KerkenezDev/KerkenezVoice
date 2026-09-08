using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KokoroGUI.Models;
using KokoroSharp;
using KokoroSharp.Core;

namespace KokoroGUI.Services
{
    public class ModelManagerService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KokoroTTS"
        );

        public string BaseDirectory => AppDataFolder;
        public string ModelsDirectory => Path.Combine(AppDataFolder, "models");
        public string CustomVoicesDirectory => Path.Combine(AppDataFolder, "custom_voices");
        public string PresetsDirectory => Path.Combine(AppDataFolder, "presets");
        public string FxPresetsDirectory => Path.Combine(AppDataFolder, "presets", "fx");
        public string CacheDirectory => Path.Combine(AppDataFolder, "cache");
        public string ConfigPath => Path.Combine(AppDataFolder, "config.json");

        public string ModelFilePath => Path.Combine(ModelsDirectory, "kokoro-v1.0.onnx");
        public string VoicesFilePath => Path.Combine(ModelsDirectory, "voices-v1.0.bin");

        private const string OnnxUrlGithub = "https://github.com/nazdridoy/kokoro-tts/releases/download/v0.1.0/kokoro-v1.0.onnx";
        private const string OnnxUrlHuggingFace = "https://huggingface.co/hexgrad/Kokoro-82M/resolve/main/kokoro-v1.0.onnx";
        private const string VoicesUrlGithub = "https://github.com/nazdridoy/kokoro-tts/releases/download/v0.1.0/voices-v1.0.bin";
        private const string VoicesUrlHuggingFace = "https://huggingface.co/hexgrad/Kokoro-82M/resolve/main/voices/voices-v1.0.bin";

        public ModelManagerService()
        {
            EnsureDirectories();
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(AppDataFolder);
            Directory.CreateDirectory(ModelsDirectory);
            Directory.CreateDirectory(CustomVoicesDirectory);
            Directory.CreateDirectory(PresetsDirectory);
            Directory.CreateDirectory(FxPresetsDirectory);
            Directory.CreateDirectory(CacheDirectory);

            EnsureDefaultPresets();
        }

        public bool AreModelsPresent()
        {
            return File.Exists(ModelFilePath) && File.Exists(VoicesFilePath);
        }

        public async Task DownloadModelsAsync(
            IProgress<(string file, long downloaded, long total, double percent)>? progress,
            CancellationToken cancellationToken = default)
        {
            EnsureDirectories();

            if (!File.Exists(ModelFilePath))
            {
                await DownloadFileWithFallbackAsync(
                    OnnxUrlGithub,
                    OnnxUrlHuggingFace,
                    ModelFilePath,
                    "kokoro-v1.0.onnx",
                    progress,
                    cancellationToken
                );
            }

            if (!File.Exists(VoicesFilePath))
            {
                await DownloadFileWithFallbackAsync(
                    VoicesUrlGithub,
                    VoicesUrlHuggingFace,
                    VoicesFilePath,
                    "voices-v1.0.bin",
                    progress,
                    cancellationToken
                );
            }
        }

        private async Task DownloadFileWithFallbackAsync(
            string primaryUrl,
            string fallbackUrl,
            string destinationPath,
            string displayName,
            IProgress<(string file, long downloaded, long total, double percent)>? progress,
            CancellationToken cancellationToken)
        {
            string tempPath = destinationPath + ".tmp";
            try
            {
                try
                {
                    await DownloadFileAsync(primaryUrl, tempPath, displayName, progress, cancellationToken);
                }
                catch (Exception)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    await DownloadFileAsync(fallbackUrl, tempPath, displayName, progress, cancellationToken);
                }

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempPath, destinationPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        private async Task DownloadFileAsync(
            string url,
            string destinationPath,
            string displayName,
            IProgress<(string file, long downloaded, long total, double percent)>? progress,
            CancellationToken cancellationToken)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromHours(1) };
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalRead += bytesRead;

                double percent = totalBytes > 0 ? (double)totalRead / totalBytes * 100.0 : 0.0;
                progress?.Report((displayName, totalRead, totalBytes, percent));
            }
        }

        public List<KokoroVoice> LoadVoices()
        {
            var voicesList = new List<KokoroVoice>();
            if (!File.Exists(VoicesFilePath))
            {
                return voicesList;
            }

            try
            {
                using var zip = ZipFile.OpenRead(VoicesFilePath);
                foreach (var entry in zip.Entries)
                {
                    if (entry.Name.EndsWith(".npy", StringComparison.OrdinalIgnoreCase))
                    {
                        string voiceName = Path.GetFileNameWithoutExtension(entry.Name);
                        using var stream = entry.Open();
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        byte[] bytes = ms.ToArray();

                        if (bytes.Length > 10)
                        {
                            ushort headerLen = BitConverter.ToUInt16(bytes, 8);
                            int dataOffset = 10 + headerLen;
                            int floatCount = (bytes.Length - dataOffset) / 4;

                            if (floatCount >= 130560) // 510 * 1 * 256
                            {
                                float[,,] features = new float[510, 1, 256];
                                Buffer.BlockCopy(bytes, dataOffset, features, 0, 130560 * sizeof(float));

                                var (lang, gender, baseName) = ParseVoiceDetails(voiceName);
                                var voice = new KokoroVoice { Features = features };
                                voice.Rename(baseName, lang, gender);
                                voicesList.Add(voice);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading voices from zip: {ex.Message}");
            }

            // Load custom voices
            try
            {
                if (Directory.Exists(CustomVoicesDirectory))
                {
                    foreach (var file in Directory.GetFiles(CustomVoicesDirectory, "*.bin"))
                    {
                        string customName = Path.GetFileNameWithoutExtension(file);
                        var voice = LoadCustomVoice(customName, KokoroLanguage.AmericanEnglish);
                        if (voice != null)
                        {
                            voicesList.Add(voice);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading custom voices: {ex.Message}");
            }

            return voicesList;
        }

        public KokoroVoice? LoadCustomVoice(string voiceName, KokoroLanguage lang = KokoroLanguage.AmericanEnglish)
        {
            if (string.IsNullOrWhiteSpace(voiceName)) return null;

            try
            {
                string safeName = Path.GetFileNameWithoutExtension(voiceName).Trim();
                string candidatePath = Path.Combine(CustomVoicesDirectory, $"{safeName}.bin");

                // If passed with prefix (e.g. "af_40Adam60Echo") and direct file doesn't exist, try stripped name
                if (!File.Exists(candidatePath) && safeName.Length >= 3 && safeName[2] == '_')
                {
                    string strippedName = safeName.Substring(3);
                    string strippedPath = Path.Combine(CustomVoicesDirectory, $"{strippedName}.bin");
                    if (File.Exists(strippedPath))
                    {
                        safeName = strippedName;
                        candidatePath = strippedPath;
                    }
                }

                if (!File.Exists(candidatePath)) return null;

                byte[] raw = File.ReadAllBytes(candidatePath);
                if (raw.Length >= 130560 * 4)
                {
                    float[,,] features = new float[510, 1, 256];
                    Buffer.BlockCopy(raw, 0, features, 0, 130560 * sizeof(float));
                    var voice = new KokoroVoice { Features = features };
                    voice.Rename(safeName, lang, KokoroGender.Female);
                    return voice;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading custom voice '{voiceName}': {ex.Message}");
            }

            return null;
        }

        public static (KokoroLanguage language, KokoroGender gender, string baseName) ParseVoiceDetails(string fullName)
        {
            string baseName = fullName;
            char langChar = 'a';
            char genderChar = 'f';

            if (fullName.Length >= 3 && fullName[2] == '_')
            {
                langChar = fullName[0];
                genderChar = fullName[1];
                baseName = fullName.Substring(3);
            }

            KokoroLanguage lang = langChar switch
            {
                'b' => KokoroLanguage.BritishEnglish,
                'e' => KokoroLanguage.Spanish,
                'f' => KokoroLanguage.French,
                'i' => KokoroLanguage.Italian,
                'p' => KokoroLanguage.BrazilianPortuguese,
                'j' => KokoroLanguage.Japanese,
                'z' => KokoroLanguage.MandarinChinese,
                _ => KokoroLanguage.AmericanEnglish
            };

            KokoroGender gender = (genderChar == 'm') ? KokoroGender.Male : KokoroGender.Female;
            return (lang, gender, baseName);
        }

        public AppSettings LoadSettings()
        {
            var defaults = new AppSettings();

            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
                catch { }
            }

            if (File.Exists("config.json"))
            {
                try
                {
                    string json = File.ReadAllText("config.json");
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
                catch { }
            }

            return defaults;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                EnsureDirectories();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void EnsureDefaultPresets()
        {
            string defaultPresetPath = Path.Combine(PresetsDirectory, "Default.json");
            if (!File.Exists(defaultPresetPath))
            {
                var defaultPreset = new VoicePreset
                {
                    Voice = "af_heart",
                    Speed = 1.0,
                    Volume = 1.0,
                    Pitch = 0.0,
                    SplitPattern = @"\n+",
                    Normalize = false,
                    Trim = false
                };
                try
                {
                    File.WriteAllText(defaultPresetPath, JsonSerializer.Serialize(defaultPreset, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { }
            }
        }
    }
}
