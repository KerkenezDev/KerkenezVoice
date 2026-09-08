using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KokoroGUI.Models;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using NAudio.Wave;

namespace KokoroGUI.Services
{
    public class GeneratedChunk
    {
        public string Path { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public double Duration { get; set; }
        public int SegmentIndex { get; set; }
        public float[] AudioData { get; set; } = Array.Empty<float>();
    }

    public class KokoroEngineService : IDisposable
    {
        private readonly ModelManagerService _modelManager;
        private readonly AudioProcessingService _audioProcessing;
        private readonly LexiconService _lexiconService;
        private readonly SubtitleExportService _subtitleExport;
        private readonly AudioPlaybackService _playbackService;
        private readonly ConcurrentDictionary<string, float[]> _memoryCache = new();

        private readonly ConcurrentBag<KokoroModel> _modelPool = new();
        private readonly SemaphoreSlim _poolSignal = new(0);
        private int _activeModelCount = 0;
        private readonly object _initLock = new();
        private CancellationTokenSource? _cts;

        public event Action<double, TimeSpan, string, string>? OnProgress;
        public event Action<string, bool>? OnStatus;
        public event Action? OnFinished;

        public bool IsInitialized => _activeModelCount > 0;

        public KokoroEngineService(
            ModelManagerService modelManager,
            AudioProcessingService audioProcessing,
            LexiconService lexiconService,
            SubtitleExportService subtitleExport,
            AudioPlaybackService playbackService)
        {
            _modelManager = modelManager;
            _audioProcessing = audioProcessing;
            _lexiconService = lexiconService;
            _subtitleExport = subtitleExport;
            _playbackService = playbackService;
        }

        public bool EnsureInitialized()
        {
            if (_activeModelCount > 0) return true;
            lock (_initLock)
            {
                if (_activeModelCount > 0) return true;
                try
                {
                    if (!_modelManager.AreModelsPresent())
                    {
                        OnStatus?.Invoke("Models not found.", false);
                        return false;
                    }

                    OnStatus?.Invoke("Initializing Kokoro TTS engine...", false);

                    var voices = _modelManager.LoadVoices();
                    KokoroVoiceManager.Voices.Clear();
                    KokoroVoiceManager.Voices.AddRange(voices);

                    var initialModel = new KokoroModel(_modelManager.ModelFilePath);
                    _modelPool.Add(initialModel);
                    _activeModelCount = 1;

                    OnStatus?.Invoke("Engine Initialized.", false);
                    return true;
                }
                catch (Exception ex)
                {
                    OnStatus?.Invoke($"Engine Init Failed: {ex.Message}", true);
                    return false;
                }
            }
        }

        public async Task<bool> InitializeAsync()
        {
            return await Task.Run(() => EnsureInitialized());
        }

        public async Task<KokoroModel> RentModelAsync(int maxPoolSize, CancellationToken ct = default)
        {
            if (_modelPool.TryTake(out var existingModel))
            {
                return existingModel;
            }

            KokoroModel? createdModel = null;
            lock (_initLock)
            {
                if (_modelPool.TryTake(out existingModel))
                {
                    return existingModel;
                }

                if (_activeModelCount < maxPoolSize)
                {
                    createdModel = new KokoroModel(_modelManager.ModelFilePath);
                    _activeModelCount++;
                }
            }

            if (createdModel != null)
            {
                return createdModel;
            }

            while (!ct.IsCancellationRequested)
            {
                await _poolSignal.WaitAsync(ct);
                if (_modelPool.TryTake(out var model))
                {
                    return model;
                }
            }

            ct.ThrowIfCancellationRequested();
            throw new OperationCanceledException(ct);
        }

        public void ReturnModel(KokoroModel model)
        {
            _modelPool.Add(model);
            _poolSignal.Release();
        }

        public void Dispose()
        {
            Cancel();
            lock (_initLock)
            {
                while (_modelPool.TryTake(out var model))
                {
                    try { model.Dispose(); } catch { }
                }
                _activeModelCount = 0;
            }
        }

        public void ReloadVoices()
        {
            var voices = _modelManager.LoadVoices();
            lock (_initLock)
            {
                KokoroVoiceManager.Voices.Clear();
                KokoroVoiceManager.Voices.AddRange(voices);
            }
        }

        public static KokoroLanguage GetKokoroLanguage(string? langCode) => langCode?.ToLowerInvariant() switch
        {
            "b" => KokoroLanguage.BritishEnglish,
            "e" => KokoroLanguage.Spanish,
            "f" => KokoroLanguage.French,
            "i" => KokoroLanguage.Italian,
            "p" => KokoroLanguage.BrazilianPortuguese,
            "j" => KokoroLanguage.Japanese,
            "z" => KokoroLanguage.MandarinChinese,
            _ => KokoroLanguage.AmericanEnglish
        };

        public KokoroVoice? ResolveVoice(string? voiceName, string? langCode = null)
        {
            KokoroLanguage targetLang = GetKokoroLanguage(langCode);

            lock (_initLock)
            {
                if (string.IsNullOrWhiteSpace(voiceName))
                {
                    return KokoroVoiceManager.Voices.FirstOrDefault();
                }

                string trimmed = voiceName.Trim();

                // 1. Exact match by Name
                var found = KokoroVoiceManager.Voices.FirstOrDefault(v => string.Equals(v.Name, trimmed, StringComparison.OrdinalIgnoreCase));

                // 2. Prefix-agnostic match
                if (found == null)
                {
                    found = KokoroVoiceManager.Voices.FirstOrDefault(v =>
                        (v.Name.Length > 3 && v.Name[2] == '_' && string.Equals(v.Name.Substring(3), trimmed, StringComparison.OrdinalIgnoreCase)) ||
                        (trimmed.Length > 3 && trimmed[2] == '_' && string.Equals(v.Name, trimmed.Substring(3), StringComparison.OrdinalIgnoreCase)) ||
                        v.Name.EndsWith("_" + trimmed, StringComparison.OrdinalIgnoreCase) ||
                        trimmed.EndsWith("_" + v.Name, StringComparison.OrdinalIgnoreCase));
                }

                // 3. Try loading directly from custom voices directory
                if (found == null)
                {
                    var customVoice = _modelManager.LoadCustomVoice(trimmed, targetLang);
                    if (customVoice != null)
                    {
                        if (!KokoroVoiceManager.Voices.Any(v => v.Name == customVoice.Name))
                        {
                            KokoroVoiceManager.Voices.Add(customVoice);
                        }
                        return customVoice;
                    }
                }

                if (found != null)
                {
                    // If it's a custom voice and the target language differs from current voice language, adapt language
                    bool isCustom = Directory.Exists(_modelManager.CustomVoicesDirectory) &&
                        (File.Exists(Path.Combine(_modelManager.CustomVoicesDirectory, $"{trimmed}.bin")) ||
                         (trimmed.Length > 3 && File.Exists(Path.Combine(_modelManager.CustomVoicesDirectory, $"{trimmed.Substring(3)}.bin"))));

                    if (isCustom && !string.IsNullOrEmpty(langCode) && found.Language != targetLang)
                    {
                        var adapted = new KokoroVoice { Features = found.Features };
                        string baseName = (found.Name.Length > 3 && found.Name[2] == '_') ? found.Name.Substring(3) : found.Name;
                        adapted.Rename(baseName, targetLang, found.Gender);
                        return adapted;
                    }

                    return found;
                }

                return KokoroVoiceManager.Voices.FirstOrDefault();
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
            _playbackService.Stop();
        }

        public List<MultispeakerSegment> ParseMultispeakerText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<MultispeakerSegment>();
            }

            // Match speaker tag only at start of line (or beginning of string):
            // e.g. [Narrator]: Hello or [Bella:EchoReverb]: Hello
            var pattern = new Regex(@"(?m)^[ \t]*\[([^\]\r\n]{1,100})\]:[ \t]*", RegexOptions.Compiled);
            var matches = pattern.Matches(text);

            if (matches.Count == 0)
            {
                return new List<MultispeakerSegment>
                {
                    new MultispeakerSegment { Text = text.Trim() }
                };
            }

            var segments = new List<MultispeakerSegment>();

            // If there is text before the first speaker match, retain it as the initial untagged segment
            if (matches[0].Index > 0)
            {
                string prefixText = text.Substring(0, matches[0].Index).Trim();
                if (!string.IsNullOrEmpty(prefixText))
                {
                    segments.Add(new MultispeakerSegment
                    {
                        SpeakerName = null,
                        FxName = null,
                        Text = prefixText
                    });
                }
            }

            for (int i = 0; i < matches.Count; i++)
            {
                string rawName = matches[i].Groups[1].Value;
                string speakerName = rawName;
                string? fxName = null;

                if (rawName.Contains(':'))
                {
                    var parts = rawName.Split(':', 2);
                    speakerName = parts[0].Trim();
                    fxName = parts[1].Trim();
                }

                int start = matches[i].Index + matches[i].Length;
                int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;

                string segmentText = text.Substring(start, end - start).Trim();
                if (!string.IsNullOrEmpty(segmentText))
                {
                    segments.Add(new MultispeakerSegment
                    {
                        SpeakerName = speakerName,
                        FxName = fxName,
                        Text = segmentText
                    });
                }
            }

            return segments;
        }

        public List<string> SmartSplit(string text, int chunkSize = 3000)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var chunks = new List<string>();
            var currentChunk = new List<string>();
            int currentLen = 0;

            // Split on double newlines (paragraphs)
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para)) continue;

                if (para.Length > chunkSize)
                {
                    // Split paragraph by single newline
                    var lines = para.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.Length > chunkSize)
                        {
                            // Split long line by sentences (. ! ? followed by space)
                            var sentencePattern = new Regex(@"(?<=[.!?])\s+", RegexOptions.Compiled);
                            var sentences = sentencePattern.Split(line);
                            foreach (var s in sentences)
                            {
                                if (string.IsNullOrWhiteSpace(s)) continue;

                                if (currentLen + s.Length > chunkSize && currentChunk.Count > 0)
                                {
                                    chunks.Add(string.Join(" ", currentChunk));
                                    currentChunk.Clear();
                                    currentLen = 0;
                                }
                                currentChunk.Add(s);
                                currentLen += s.Length + 1;
                            }
                        }
                        else
                        {
                            if (currentLen + line.Length > chunkSize && currentChunk.Count > 0)
                            {
                                chunks.Add(string.Join("\n", currentChunk));
                                currentChunk.Clear();
                                currentLen = 0;
                            }
                            currentChunk.Add(line);
                            currentLen += line.Length + 1;
                        }
                    }
                }
                else
                {
                    if (currentLen + para.Length > chunkSize && currentChunk.Count > 0)
                    {
                        chunks.Add(string.Join("\n\n", currentChunk));
                        currentChunk.Clear();
                        currentLen = 0;
                    }
                    currentChunk.Add(para);
                    currentLen += para.Length + 2;
                }
            }

            if (currentChunk.Count > 0)
            {
                chunks.Add(string.Join("\n\n", currentChunk));
            }

            return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }

        public async Task<float[]?> GeneratePreviewAudioAsync(
            string text,
            string voiceName,
            double speed,
            AppSettings config,
            float[,,]? customVoiceTensor = null,
            string? langCode = null)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    if (_activeModelCount == 0 && !EnsureInitialized()) return null;

                    var msSegments = ParseMultispeakerText(text);
                    if (msSegments.Count > 3)
                        msSegments = msSegments.Take(3).ToList();

                    var allPieces = new List<float[]>();

                    foreach (var seg in msSegments)
                    {
                        string segText = _lexiconService.ApplyLexicon(seg.Text, config.Lexicon);
                        if (segText.Length > 1000)
                            segText = segText.Substring(0, 1000);

                        var targetVoiceName = voiceName;
                        var targetSpeed = speed;
                        var targetConfig = CloneConfig(config);

                        if (!string.IsNullOrEmpty(seg.SpeakerName))
                        {
                            var preset = LoadPreset(seg.SpeakerName);
                            if (preset != null)
                            {
                                targetVoiceName = preset.Voice ?? targetVoiceName;
                                targetSpeed = preset.Speed;
                                targetConfig.Volume = preset.Volume;
                                targetConfig.Pitch = preset.Pitch;
                                targetConfig.Normalize = preset.Normalize;
                                targetConfig.Trim = preset.Trim;
                            }
                            else if (ResolveVoice(seg.SpeakerName, config.LangCode) != null)
                            {
                                targetVoiceName = seg.SpeakerName;
                            }
                        }

                        if (!string.IsNullOrEmpty(seg.FxName))
                        {
                            var fxPreset = LoadFxPreset(seg.FxName);
                            if (fxPreset != null)
                            {
                                ApplyFxPresetToConfig(targetConfig, fxPreset);
                                targetConfig.ApplyFx = true;
                            }
                        }

                        KokoroVoice? voice = null;
                        if (customVoiceTensor != null && string.IsNullOrEmpty(seg.SpeakerName))
                        {
                            KokoroLanguage targetLang = GetKokoroLanguage(langCode ?? config.LangCode);
                            voice = new KokoroVoice { Features = customVoiceTensor };
                            voice.Rename("_preview_temp", targetLang, KokoroGender.Female);
                        }
                        else
                        {
                            voice = ResolveVoice(targetVoiceName, config.LangCode);
                        }

                        if (voice == null) return null;

                        double effSpeed = targetSpeed;
                        if (Math.Abs(targetConfig.Pitch) > 0.01)
                        {
                            double factor = Math.Pow(2.0, targetConfig.Pitch / 12.0);
                            effSpeed = targetSpeed / factor;
                        }

                        var pipelineConfig = new KokoroSharp.Processing.KokoroTTSPipelineConfig
                        {
                            Speed = (float)effSpeed
                        };

                        var model = await RentModelAsync(1);
                        float[] rawAudio;
                        try
                        {
                            rawAudio = SynthesizeTextToSamples(segText, voice, pipelineConfig, model);
                        }
                        finally
                        {
                            ReturnModel(model);
                        }

                        if (rawAudio.Length > 0)
                        {
                            var processed = _audioProcessing.ProcessAudio(rawAudio, targetConfig);
                            allPieces.Add(processed);
                        }
                    }

                    if (allPieces.Count == 0) return null;

                    int totalSamples = allPieces.Sum(p => p.Length);
                    float[] combined = new float[totalSamples];
                    int offset = 0;
                    foreach (var piece in allPieces)
                    {
                        Array.Copy(piece, 0, combined, offset, piece.Length);
                        offset += piece.Length;
                    }

                    return combined;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Preview generation error: {ex.Message}");
                    return null;
                }
            });
        }

        public async Task StartConversionAsync(string text, AppSettings config)
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            await Task.Run(async () =>
            {
                try
                {
                    if (_activeModelCount == 0 && !EnsureInitialized())
                    {
                        OnStatus?.Invoke("Engine not initialized.", true);
                        OnFinished?.Invoke();
                        return;
                    }

                    OnStatus?.Invoke("Preparing text...", false);
                    Directory.CreateDirectory(config.OutDir);

                    var msSegments = ParseMultispeakerText(text);
                    var taskChunks = new List<(int index, string chunkText, AppSettings chunkConfig)>();

                    foreach (var seg in msSegments)
                    {
                        string segmentText = _lexiconService.ApplyLexicon(seg.Text, config.Lexicon);
                        var segConfig = CloneConfig(config);

                        if (!string.IsNullOrEmpty(seg.SpeakerName))
                        {
                            var preset = LoadPreset(seg.SpeakerName);
                            if (preset != null)
                            {
                                segConfig.Voice = preset.Voice ?? segConfig.Voice;
                                segConfig.Speed = preset.Speed;
                                segConfig.Volume = preset.Volume;
                                segConfig.Pitch = preset.Pitch;
                                segConfig.Normalize = preset.Normalize;
                                segConfig.Trim = preset.Trim;
                            }
                            else if (ResolveVoice(seg.SpeakerName, config.LangCode) != null)
                            {
                                segConfig.Voice = seg.SpeakerName;
                            }
                        }

                        if (!string.IsNullOrEmpty(seg.FxName))
                        {
                            var fxPreset = LoadFxPreset(seg.FxName);
                            if (fxPreset != null)
                            {
                                ApplyFxPresetToConfig(segConfig, fxPreset);
                                segConfig.ApplyFx = true;
                            }
                        }

                        int targetChunkSize = config.NumThreads > 1
                            ? Math.Max(300, Math.Min(1500, segmentText.Length / Math.Max(1, config.NumThreads)))
                            : 3000;
                        var segChunks = SmartSplit(segmentText, targetChunkSize);
                        foreach (var chunk in segChunks)
                        {
                            taskChunks.Add((taskChunks.Count, chunk, segConfig));
                        }
                    }

                    if (taskChunks.Count == 0)
                    {
                        OnStatus?.Invoke("No text to process.", false);
                        OnFinished?.Invoke();
                        return;
                    }

                    int totalChunks = taskChunks.Count;
                    int totalChars = taskChunks.Sum(c => c.chunkText.Length);
                    int processedChars = 0;
                    var startTime = DateTime.Now;
                    double phaseWeight = config.Combine ? 0.9 : 1.0;

                    OnStatus?.Invoke($"Queued {totalChunks} blocks. Starting {config.NumThreads} workers...", false);

                    var generatedChunks = new GeneratedChunk?[totalChunks];
                    var semaphore = new SemaphoreSlim(Math.Max(1, config.NumThreads));

                    var tasks = taskChunks.Select(async item =>
                    {
                        await semaphore.WaitAsync(ct);
                        try
                        {
                            if (ct.IsCancellationRequested) return;

                            var chunk = await ProcessChunkAsync(item.index, item.chunkText, item.chunkConfig, (chars, snippet) =>
                            {
                                Interlocked.Add(ref processedChars, chars);
                                var elapsed = DateTime.Now - startTime;
                                double genFraction = Math.Min(1.0, (double)processedChars / totalChars);
                                double totalFraction = genFraction * phaseWeight;

                                string eta = "--:--";
                                if (totalFraction > 0.01)
                                {
                                    double totalEstSec = elapsed.TotalSeconds / totalFraction;
                                    double remSec = Math.Max(0, totalEstSec - elapsed.TotalSeconds);
                                    var remTime = TimeSpan.FromSeconds(remSec);
                                    eta = $"{(int)remTime.TotalMinutes:D2}:{remTime.Seconds:D2}";
                                }

                                string cleanSnip = snippet.Replace("\n", " ").Trim();
                                if (cleanSnip.Length > 40) cleanSnip = cleanSnip.Substring(0, 37) + "...";

                                OnProgress?.Invoke(totalFraction * 100.0, elapsed, eta, $"Processing: {cleanSnip}");
                            }, ct);

                            generatedChunks[item.index] = chunk;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);

                    if (ct.IsCancellationRequested)
                    {
                        OnStatus?.Invoke("Conversion Cancelled.", false);
                        OnFinished?.Invoke();
                        return;
                    }

                    var validChunks = generatedChunks
                        .Where(c => c != null && c.AudioData != null && c.AudioData.Length > 0)
                        .Select(c => c!)
                        .OrderBy(c => c.SegmentIndex)
                        .ToList();

                    if (validChunks.Count < totalChunks)
                    {
                        OnStatus?.Invoke($"Warning: Completed {validChunks.Count}/{totalChunks} segments. Processing outputs...", false);
                    }
                    else
                    {
                        OnStatus?.Invoke($"Generated {validChunks.Count} segments. Processing outputs...", false);
                    }

                    string timeId = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string baseFilename = config.Filename;

                    // Export Subtitles
                    if (config.ExportSubtitles && validChunks.Count > 0)
                    {
                        string srtPath = Path.Combine(config.OutDir, $"{baseFilename}_{timeId}.srt");
                        var subSegments = validChunks.Select(c => new SubtitleSegment { Text = c.Text, Duration = c.Duration }).ToList();
                        _subtitleExport.GenerateSrt(subSegments, srtPath);
                    }

                    if (validChunks.Count == 1)
                    {
                        // Single chunk: save exactly one audio file (no duplicate part/combined)
                        string singlePath = Path.Combine(config.OutDir, $"{baseFilename}_{timeId}.wav");
                        SaveWavFile(singlePath, validChunks[0].AudioData, 24000);
                        validChunks[0].Path = singlePath;
                        OnStatus?.Invoke($"Done! Saved: {singlePath}", false);
                    }
                    else if (validChunks.Count > 1)
                    {
                        // Multiple chunks
                        if (config.Separate)
                        {
                            for (int i = 0; i < validChunks.Count; i++)
                            {
                                string partPath = Path.Combine(config.OutDir, $"{baseFilename}_{timeId}_part{i + 1}.wav");
                                SaveWavFile(partPath, validChunks[i].AudioData, 24000);
                                validChunks[i].Path = partPath;
                            }
                        }

                        if (config.Combine)
                        {
                            OnStatus?.Invoke("Merging audio files...", false);
                            string combinePath = Path.Combine(config.OutDir, $"{baseFilename}_{timeId}_combined.wav");

                            var allAudio = new List<float>();
                            for (int i = 0; i < validChunks.Count; i++)
                            {
                                if (i > 0 && config.Trim)
                                {
                                    // Add natural 0.25s breathing pause between trimmed chunks
                                    allAudio.AddRange(new float[(int)(0.25f * 24000)]);
                                }
                                allAudio.AddRange(validChunks[i].AudioData);
                            }

                            SaveWavFile(combinePath, allAudio.ToArray(), 24000);
                            OnStatus?.Invoke($"Done! Saved: {combinePath}", false);
                        }
                        else
                        {
                            OnStatus?.Invoke("Conversion Complete!", false);
                        }
                    }

                    var totalElapsed = DateTime.Now - startTime;
                    OnProgress?.Invoke(100.0, totalElapsed, "00:00", "Completed");
                }
                catch (Exception ex)
                {
                    OnStatus?.Invoke($"Critical Error: {ex.Message}", true);
                }
                finally
                {
                    OnFinished?.Invoke();
                }
            });
        }

        public async Task StartJitConversionAsync(string text, AppSettings config)
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            await Task.Run(async () =>
            {
                try
                {
                    if (_activeModelCount == 0 && !EnsureInitialized())
                    {
                        OnStatus?.Invoke("Engine not initialized.", true);
                        OnFinished?.Invoke();
                        return;
                    }

                    OnStatus?.Invoke("JIT: Preparing...", false);
                    Directory.CreateDirectory(config.OutDir);

                    var msSegments = ParseMultispeakerText(text);
                    var allTextSegments = new List<(string chunkText, AppSettings chunkConfig)>();

                    foreach (var seg in msSegments)
                    {
                        string segmentText = _lexiconService.ApplyLexicon(seg.Text, config.Lexicon);
                        var segConfig = CloneConfig(config);
                        segConfig.Format = "wav";

                        if (!string.IsNullOrEmpty(seg.SpeakerName))
                        {
                            var preset = LoadPreset(seg.SpeakerName);
                            if (preset != null)
                            {
                                segConfig.Voice = preset.Voice ?? segConfig.Voice;
                                segConfig.Speed = preset.Speed;
                                segConfig.Volume = preset.Volume;
                                segConfig.Pitch = preset.Pitch;
                                segConfig.Normalize = preset.Normalize;
                                segConfig.Trim = preset.Trim;
                            }
                            else if (ResolveVoice(seg.SpeakerName, config.LangCode) != null)
                            {
                                segConfig.Voice = seg.SpeakerName;
                            }
                        }

                        if (!string.IsNullOrEmpty(seg.FxName))
                        {
                            var fxPreset = LoadFxPreset(seg.FxName);
                            if (fxPreset != null)
                            {
                                ApplyFxPresetToConfig(segConfig, fxPreset);
                                segConfig.ApplyFx = true;
                            }
                        }

                        var chunks = SmartSplit(segmentText, 500);
                        foreach (var c in chunks)
                        {
                            allTextSegments.Add((c, segConfig));
                        }
                    }

                    if (allTextSegments.Count == 0)
                    {
                        OnStatus?.Invoke("No text for JIT.", false);
                        OnFinished?.Invoke();
                        return;
                    }

                    var audioQueue = new BlockingCollection<GeneratedChunk?>();
                    var playedSegments = new List<GeneratedChunk>();
                    var generatedButUnplayed = new List<GeneratedChunk>();
                    int totalSegments = allTextSegments.Count;

                    // Generation task
                    var genTask = Task.Run(async () =>
                    {
                        try
                        {
                            for (int i = 0; i < totalSegments; i++)
                            {
                                if (ct.IsCancellationRequested) break;

                                while (audioQueue.Count > 10 && !ct.IsCancellationRequested)
                                {
                                    await Task.Delay(500, ct);
                                }

                                if (ct.IsCancellationRequested) break;

                                OnStatus?.Invoke($"JIT: Generating chunk {i + 1}/{totalSegments}...", false);

                                var (chunkText, chunkConfig) = allTextSegments[i];
                                var chunk = await ProcessChunkAsync(i, chunkText, chunkConfig, null, ct);

                                if (chunk != null)
                                {
                                    audioQueue.Add(chunk);
                                    lock (generatedButUnplayed)
                                    {
                                        generatedButUnplayed.Add(chunk);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"JIT Gen Error: {ex.Message}");
                        }
                        finally
                        {
                            audioQueue.Add(null);
                        }
                    }, ct);

                    // Playback task
                    var playTask = Task.Run(() =>
                    {
                        var startTime = DateTime.Now;
                        int idx = 0;

                        while (!ct.IsCancellationRequested)
                        {
                            GeneratedChunk? item;
                            try
                            {
                                item = audioQueue.Take(ct);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            if (item == null) break;

                            idx++;
                            OnStatus?.Invoke($"JIT: Playing chunk {idx}...", false);

                            string cleanSnip = item.Text.Replace("\n", " ").Trim();
                            if (cleanSnip.Length > 40) cleanSnip = cleanSnip.Substring(0, 37) + "...";

                            var elapsed = DateTime.Now - startTime;
                            double percent = (double)idx / totalSegments * 100.0;
                            OnProgress?.Invoke(percent, elapsed, "--:--", $"Playing: {cleanSnip}");

                            _playbackService.PlayAudioData(item.AudioData, blocking: true);

                            lock (generatedButUnplayed)
                            {
                                playedSegments.Add(item);
                                generatedButUnplayed.Remove(item);
                            }
                        }
                    }, ct);

                    await Task.WhenAll(genTask, playTask);

                    if (ct.IsCancellationRequested)
                    {
                        OnStatus?.Invoke("JIT Stopped. Saving state...", false);
                    }
                    else
                    {
                        OnStatus?.Invoke("JIT Finished.", false);
                    }

                    string timeId = DateTime.Now.ToString("yyyyMMddHHmmss");
                    var allWork = playedSegments.Concat(generatedButUnplayed).ToList();
                    if (allWork.Count > 0)
                    {
                        string combinedPath = Path.Combine(config.OutDir, $"{config.Filename}_{timeId}_jit_output.wav");
                        var allSamples = new List<float>();
                        foreach (var w in allWork)
                        {
                            allSamples.AddRange(w.AudioData);
                        }
                        SaveWavFile(combinedPath, allSamples.ToArray(), 24000);
                        OnStatus?.Invoke($"JIT Output saved: {combinedPath}", false);
                    }
                }
                catch (Exception ex)
                {
                    OnStatus?.Invoke($"JIT Error: {ex.Message}", true);
                }
                finally
                {
                    OnFinished?.Invoke();
                }
            });
        }

        private async Task<GeneratedChunk?> ProcessChunkAsync(
            int index,
            string text,
            AppSettings config,
            Action<int, string>? progressCallback,
            CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return null;

            double effSpeed = config.Speed;
            if (Math.Abs(config.Pitch) > 0.01)
            {
                double factor = Math.Pow(2.0, config.Pitch / 12.0);
                effSpeed = config.Speed / factor;
            }

            string cacheKey = string.Empty;
            if (config.Caching)
            {
                cacheKey = $"{text}|{config.Voice}|{effSpeed}|{config.LangCode}";
                if (_memoryCache.TryGetValue(cacheKey, out var cachedRaw))
                {
                    progressCallback?.Invoke(text.Length, text);
                    return ProcessAndSave(index, text, cachedRaw, config);
                }
            }

            var voice = ResolveVoice(config.Voice, config.LangCode);
            if (voice == null) return null;

            var pipelineConfig = new KokoroSharp.Processing.KokoroTTSPipelineConfig
            {
                Speed = (float)effSpeed
            };

            int maxPoolSize = Math.Max(1, config.NumThreads);
            var model = await RentModelAsync(maxPoolSize, ct);
            float[] rawAudio;
            try
            {
                rawAudio = SynthesizeTextToSamples(text, voice, pipelineConfig, model, ct);
            }
            finally
            {
                ReturnModel(model);
            }

            progressCallback?.Invoke(text.Length, text);

            if (config.Caching && !string.IsNullOrEmpty(cacheKey) && rawAudio.Length > 0)
            {
                _memoryCache[cacheKey] = rawAudio;
            }

            return ProcessAndSave(index, text, rawAudio, config);
        }

        private GeneratedChunk ProcessAndSave(int index, string text, float[] rawAudio, AppSettings config)
        {
            var processed = _audioProcessing.ProcessAudio(rawAudio, config);
            return new GeneratedChunk
            {
                Path = string.Empty,
                Text = text,
                Duration = (double)processed.Length / 24000.0,
                SegmentIndex = index,
                AudioData = processed
            };
        }

        private float[] SynthesizeTextToSamples(
            string text,
            KokoroVoice voice,
            KokoroSharp.Processing.KokoroTTSPipelineConfig config,
            KokoroModel model,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();
            if (ct.IsCancellationRequested) return Array.Empty<float>();

            try
            {
                string langCode = voice.GetLangCode();
                bool preprocess = config?.PreprocessText ?? true;
                int[] tokens = Tokenizer.Tokenize(text.Trim(), langCode, preprocess);
                if (tokens.Length == 0) return Array.Empty<float>();

                var segmentationStrategy = new DefaultSegmentationConfig();
                List<int[]> segments = config?.SegmentationFunc != null
                    ? config.SegmentationFunc(tokens)
                    : SegmentationSystem.SplitToSegments(tokens, segmentationStrategy);

                if (segments == null || segments.Count == 0) return Array.Empty<float>();

                var allSamples = new List<float>();
                var pauseStrategy = config?.SecondsOfPauseBetweenProperSegments ?? new PauseAfterSegmentStrategy();

                for (int i = 0; i < segments.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int[] segTokens = segments[i];
                    if (segTokens == null || segTokens.Length == 0) continue;

                    float[] segAudio = model.Infer(segTokens, voice.Features, config?.Speed ?? 1.0f, out _);
                    if (segAudio != null && segAudio.Length > 0)
                    {
                        allSamples.AddRange(segAudio);
                    }

                    // Insert natural pause between segments if ending in punctuation
                    if (i < segments.Count - 1 && segTokens.Length > 0 && Tokenizer.PunctuationTokens.Contains(segTokens[^1]))
                    {
                        char punct = Tokenizer.TokenToChar[segTokens[^1]];
                        float pauseSec = pauseStrategy[punct];
                        if (pauseSec > 0.01f)
                        {
                            int pauseSamples = (int)(pauseSec * 24000);
                            allSamples.AddRange(new float[pauseSamples]);
                        }
                    }
                }

                return allSamples.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Synthesis error: {ex.Message}");
                return Array.Empty<float>();
            }
        }

        public static void SaveWavFile(string filePath, float[] samples, int sampleRate = 24000)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var format = new WaveFormat(sampleRate, 16, 1);
            using var writer = new WaveFileWriter(filePath, format);

            byte[] buffer = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)Math.Clamp((int)(samples[i] * 32767.0f), short.MinValue, short.MaxValue);
                buffer[i * 2] = (byte)(val & 0xFF);
                buffer[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            writer.Write(buffer, 0, buffer.Length);
        }

        public static float[] LoadWavFile(string filePath)
        {
            using var reader = new AudioFileReader(filePath);
            var samples = new List<float>();
            float[] buffer = new float[4096];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++) samples.Add(buffer[i]);
            }
            return samples.ToArray();
        }

        public VoicePreset? LoadPreset(string name)
        {
            string safeName = Path.GetFileNameWithoutExtension(name);
            string path = Path.Combine(_modelManager.PresetsDirectory, $"{safeName}.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return System.Text.Json.JsonSerializer.Deserialize<VoicePreset>(json);
                }
                catch { }
            }
            return null;
        }

        public FxPreset? LoadFxPreset(string name)
        {
            string safeName = Path.GetFileNameWithoutExtension(name);
            string path = Path.Combine(_modelManager.FxPresetsDirectory, $"{safeName}.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return System.Text.Json.JsonSerializer.Deserialize<FxPreset>(json);
                }
                catch { }
            }
            return null;
        }

        private static AppSettings CloneConfig(AppSettings src)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(src);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }

        private static void ApplyFxPresetToConfig(AppSettings config, FxPreset fx)
        {
            config.ReverbEnabled = fx.ReverbEnabled;
            config.ReverbRoomSize = fx.ReverbRoomSize;
            config.ReverbWetLevel = fx.ReverbWetLevel;
            config.ReverbDamping = fx.ReverbDamping;
            config.ReverbDryLevel = fx.ReverbDryLevel;
            config.ReverbWidth = fx.ReverbWidth;

            config.EqBass = fx.EqBass;
            config.EqTreble = fx.EqTreble;

            config.CompEnabled = fx.CompEnabled;
            config.CompThreshold = fx.CompThreshold;
            config.CompRatio = fx.CompRatio;
            config.CompAttack = fx.CompAttack;
            config.CompRelease = fx.CompRelease;

            config.DistortionEnabled = fx.DistortionEnabled;
            config.DistortionDrive = fx.DistortionDrive;

            config.ChorusEnabled = fx.ChorusEnabled;
            config.ChorusRate = fx.ChorusRate;
            config.ChorusDepth = fx.ChorusDepth;
            config.ChorusMix = fx.ChorusMix;

            config.PhaserEnabled = fx.PhaserEnabled;
            config.PhaserRate = fx.PhaserRate;
            config.PhaserDepth = fx.PhaserDepth;
            config.PhaserMix = fx.PhaserMix;

            config.ClippingEnabled = fx.ClippingEnabled;
            config.ClippingThresh = fx.ClippingThresh;

            config.BitcrushEnabled = fx.BitcrushEnabled;
            config.BitcrushDepth = fx.BitcrushDepth;

            config.GsmEnabled = fx.GsmEnabled;

            config.HighpassEnabled = fx.HighpassEnabled;
            config.HighpassFreq = fx.HighpassFreq;

            config.LowpassEnabled = fx.LowpassEnabled;
            config.LowpassFreq = fx.LowpassFreq;

            config.DelayEnabled = fx.DelayEnabled;
            config.DelayTime = fx.DelayTime;
            config.DelayFeedback = fx.DelayFeedback;
            config.DelayMix = fx.DelayMix;

            config.PitchShiftEnabled = fx.PitchShiftEnabled;
            config.PitchShiftSemitones = fx.PitchShiftSemitones;

            config.LimiterEnabled = fx.LimiterEnabled;
            config.LimiterThreshold = fx.LimiterThreshold;
            config.LimiterRelease = fx.LimiterRelease;

            config.GainEnabled = fx.GainEnabled;
            config.GainDb = fx.GainDb;
        }
    }
}
