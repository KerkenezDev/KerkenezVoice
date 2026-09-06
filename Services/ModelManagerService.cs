using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KerkenezVoice.Models;
using KokoroSharp;
using KokoroSharp.Core;

namespace KerkenezVoice.Services
{
    public class ModelManagerService
    {
        public string BaseDirectory => ConfigService.LocalProgramsFolder;
        public string ModelsDirectory => ConfigService.ModelsFolder;
        public string CustomVoicesDirectory => ConfigService.CustomVoicesFolder;
        public string PresetsDirectory => ConfigService.PresetsFolder;
        public string FxPresetsDirectory => ConfigService.FxPresetsFolder;
        public string CacheDirectory => ConfigService.CacheFolder;
        public string ConfigPath => ConfigService.ConfigFilePath;

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
            Directory.CreateDirectory(BaseDirectory);
            Directory.CreateDirectory(ModelsDirectory);
            Directory.CreateDirectory(CustomVoicesDirectory);
            Directory.CreateDirectory(PresetsDirectory);
            Directory.CreateDirectory(FxPresetsDirectory);
            Directory.CreateDirectory(CacheDirectory);

            EnsureDefaultPresets();
        }

        public bool AreModelsPresent()
        {
            if (File.Exists(ModelFilePath) && File.Exists(VoicesFilePath))
            {
                return true;
            }

            // Fallback 1: check Roaming Kerkenez directory
            string roamingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kerkenez", "voice", "models");
            string roamingModel = Path.Combine(roamingFolder, "kokoro-v1.0.onnx");
            string roamingVoices = Path.Combine(roamingFolder, "voices-v1.0.bin");

            if (File.Exists(roamingModel) && File.Exists(roamingVoices))
            {
                try
                {
                    Directory.CreateDirectory(ModelsDirectory);
                    if (!File.Exists(ModelFilePath)) File.Copy(roamingModel, ModelFilePath, false);
                    if (!File.Exists(VoicesFilePath)) File.Copy(roamingVoices, VoicesFilePath, false);
                    return true;
                }
                catch { }
            }

            // Fallback 2: check legacy KokoroTTS directory
            string legacyFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KokoroTTS", "models");
            string legacyModel = Path.Combine(legacyFolder, "kokoro-v1.0.onnx");
            string legacyVoices = Path.Combine(legacyFolder, "voices-v1.0.bin");

            if (File.Exists(legacyModel) && File.Exists(legacyVoices))
            {
                try
                {
                    Directory.CreateDirectory(ModelsDirectory);
                    if (!File.Exists(ModelFilePath)) File.Copy(legacyModel, ModelFilePath, false);
                    if (!File.Exists(VoicesFilePath)) File.Copy(legacyVoices, VoicesFilePath, false);
                    return true;
                }
                catch { }
            }

            return false;
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
                System.Diagnostics.Debug.WriteLine($"Error loading voices from zip: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error loading custom voices: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error loading custom voice '{voiceName}': {ex.Message}");
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

        public void UpdateDefaultPresetVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName)) return;

            try
            {
                string defaultPresetPath = Path.Combine(PresetsDirectory, "Default.json");
                VoicePreset preset;
                if (File.Exists(defaultPresetPath))
                {
                    string json = File.ReadAllText(defaultPresetPath);
                    preset = JsonSerializer.Deserialize<VoicePreset>(json) ?? new VoicePreset();
                }
                else
                {
                    preset = new VoicePreset();
                }

                preset.Voice = voiceName;
                File.WriteAllText(defaultPresetPath, JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelManagerService] Error updating default preset voice: {ex.Message}");
            }
        }
    }
}
