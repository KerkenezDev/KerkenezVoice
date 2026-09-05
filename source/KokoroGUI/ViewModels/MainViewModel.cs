using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using KokoroGUI.Models;
using KokoroGUI.Services;
using KokoroSharp;
using KokoroSharp.Core;

namespace KokoroGUI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ModelManagerService _modelManager;
        private readonly KokoroEngineService _engineService;
        private readonly VoiceMixingService _voiceMixingService;
        private readonly DocumentParserService _docParser;
        private readonly AudioPlaybackService _playbackService;

        public AppSettings Settings { get; }

        // --- Collections ---
        public ObservableCollection<LanguageInfo> Languages { get; } = new();
        public ObservableCollection<string> CurrentVoices { get; } = new();
        public ObservableCollection<string> OutputFormats { get; } = new() { "wav", "flac", "mp3", "ogg" };
        public ObservableCollection<string> SplitPatterns { get; } = new()
        {
            "Natural (Newlines)",
            "Paragraphs (Double Newline)",
            "Sentences (.!?)"
        };
        public ObservableCollection<string> Presets { get; } = new();
        public ObservableCollection<string> FxPresets { get; } = new();
        public ObservableCollection<string> MixingOperations { get; } = new() { "mix", "add", "subtract", "multiply", "divide" };
        public ObservableCollection<string> MixVoicesA { get; } = new();
        public ObservableCollection<string> MixVoicesB { get; } = new();
        public ObservableCollection<string> CustomVoices { get; } = new();
        public ObservableCollection<LexiconRule> LexiconRules { get; } = new();

        private static readonly Dictionary<string, List<string>> VoiceDb = new()
        {
            ["a"] = new() { "af_heart", "af_alloy", "af_aoede", "af_bella", "af_jessica", "af_kore", "af_nicole", "af_nova", "af_river", "af_sarah", "af_sky", "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael", "am_onyx", "am_puck", "am_santa" },
            ["b"] = new() { "bf_alice", "bf_emma", "bf_isabella", "bf_lily", "bm_daniel", "bm_fable", "bm_george", "bm_lewis" },
            ["e"] = new() { "ef_dora", "em_alex", "em_santa" },
            ["f"] = new() { "ff_siwis" },
            ["i"] = new() { "if_sara", "im_nicola" },
            ["p"] = new() { "pf_dora", "pm_alex" },
            ["j"] = new() { "jf_alpha", "jf_gongitsune", "jf_nezumi", "jf_tebukuro" },
            ["z"] = new() { "zf_xiaobei", "zf_xiaoni", "zf_xiaoxiao", "zm_yunjian" }
        };

        // --- Input State ---
        private int _selectedInputTab = 0; // 0 = Direct Text, 1 = Load File
        private string _directText = string.Empty;
        private string _inputFilePath = string.Empty;

        public int SelectedInputTab
        {
            get => _selectedInputTab;
            set => SetProperty(ref _selectedInputTab, value);
        }

        public string DirectText
        {
            get => _directText;
            set => SetProperty(ref _directText, value);
        }

        public string InputFilePath
        {
            get => _inputFilePath;
            set => SetProperty(ref _inputFilePath, value);
        }

        // --- Configuration State ---
        private LanguageInfo? _selectedLanguage;
        public LanguageInfo? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value) && value != null)
                {
                    Settings.LangCode = value.Code;
                    UpdateVoiceList();
                    AutoSave();
                }
            }
        }

        public string SelectedVoice
        {
            get => Settings.Voice;
            set
            {
                if (Settings.Voice != value)
                {
                    Settings.Voice = value;
                    OnPropertyChanged();
                    AutoSave();
                }
            }
        }

        public string OutputDirectory
        {
            get => Settings.OutDir;
            set
            {
                if (Settings.OutDir != value)
                {
                    Settings.OutDir = value;
                    OnPropertyChanged();
                    AutoSave();
                }
            }
        }

        public string BaseFilename
        {
            get => Settings.Filename;
            set
            {
                if (Settings.Filename != value)
                {
                    Settings.Filename = value;
                    OnPropertyChanged();
                    AutoSave();
                }
            }
        }

        public string SelectedFormat
        {
            get => Settings.Format;
            set
            {
                if (Settings.Format != value)
                {
                    Settings.Format = value;
                    OnPropertyChanged();
                    AutoSave();
                }
            }
        }

        public double Speed
        {
            get => Settings.Speed;
            set
            {
                if (Math.Abs(Settings.Speed - value) > 0.001)
                {
                    Settings.Speed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SpeedLabel));
                    AutoSave();
                }
            }
        }
        public string SpeedLabel => $"Speed: {Speed:F1}x";

        private string _selectedSplitPatternKey = "Natural (Newlines)";
        public string SelectedSplitPatternKey
        {
            get => _selectedSplitPatternKey;
            set
            {
                if (SetProperty(ref _selectedSplitPatternKey, value))
                {
                    Settings.SplitPattern = value switch
                    {
                        "Paragraphs (Double Newline)" => @"\n\n+",
                        "Sentences (.!?)" => @"(?<!\w\.\w.)(?<![A-Z][a-z]\.)(?<=\.|\?|\!)\s",
                        _ => @"\n+"
                    };
                    AutoSave();
                }
            }
        }

        // --- Audio Control State ---
        public double Volume
        {
            get => Settings.Volume;
            set
            {
                if (Math.Abs(Settings.Volume - value) > 0.001)
                {
                    Settings.Volume = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VolumeLabel));
                    AutoSave();
                }
            }
        }
        public string VolumeLabel => $"Volume: {(int)(Volume * 100)}%";

        public double Pitch
        {
            get => Settings.Pitch;
            set
            {
                if (Math.Abs(Settings.Pitch - value) > 0.001)
                {
                    Settings.Pitch = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PitchLabel));
                    AutoSave();
                }
            }
        }
        public string PitchLabel => $"Pitch: {(int)Pitch} st";

        private string? _selectedPreset;
        public string? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetProperty(ref _selectedPreset, value) && !string.IsNullOrEmpty(value) && value != "Select Preset...")
                {
                    LoadPreset(value);
                }
            }
        }

        private string? _selectedFxPreset;
        public string? SelectedFxPreset
        {
            get => _selectedFxPreset;
            set
            {
                if (SetProperty(ref _selectedFxPreset, value) && !string.IsNullOrEmpty(value) && value != "Select FX Preset...")
                {
                    LoadFxPreset(value);
                }
            }
        }

        public bool ApplyFx
        {
            get => Settings.ApplyFx;
            set { if (Settings.ApplyFx != value) { Settings.ApplyFx = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool NormalizeAudio
        {
            get => Settings.Normalize;
            set { if (Settings.Normalize != value) { Settings.Normalize = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool TrimSilence
        {
            get => Settings.Trim;
            set { if (Settings.Trim != value) { Settings.Trim = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool KeepSegments
        {
            get => Settings.Separate;
            set { if (Settings.Separate != value) { Settings.Separate = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool CombineOutput
        {
            get => Settings.Combine;
            set { if (Settings.Combine != value) { Settings.Combine = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool ExportSubtitles
        {
            get => Settings.ExportSubtitles;
            set { if (Settings.ExportSubtitles != value) { Settings.ExportSubtitles = value; OnPropertyChanged(); AutoSave(); } }
        }

        public int NumThreads
        {
            get => Settings.NumThreads;
            set
            {
                int val = Math.Max(1, Math.Min(16, value));
                if (Settings.NumThreads != val)
                {
                    Settings.NumThreads = val;
                    OnPropertyChanged();
                    AutoSave();
                }
            }
        }

        // --- FX Parameters ---
        public bool ReverbEnabled
        {
            get => Settings.ReverbEnabled;
            set { if (Settings.ReverbEnabled != value) { Settings.ReverbEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double ReverbRoomSize
        {
            get => Settings.ReverbRoomSize;
            set { if (Math.Abs(Settings.ReverbRoomSize - value) > 0.001) { Settings.ReverbRoomSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReverbRoomLabel)); AutoSave(); } }
        }
        public string ReverbRoomLabel => $"Size: {ReverbRoomSize:F2}";
        public double ReverbWetLevel
        {
            get => Settings.ReverbWetLevel;
            set { if (Math.Abs(Settings.ReverbWetLevel - value) > 0.001) { Settings.ReverbWetLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReverbWetLabel)); AutoSave(); } }
        }
        public string ReverbWetLabel => $"Wet: {ReverbWetLevel:F2}";
        public double ReverbDamping
        {
            get => Settings.ReverbDamping;
            set { if (Math.Abs(Settings.ReverbDamping - value) > 0.001) { Settings.ReverbDamping = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double ReverbWidth
        {
            get => Settings.ReverbWidth;
            set { if (Math.Abs(Settings.ReverbWidth - value) > 0.001) { Settings.ReverbWidth = value; OnPropertyChanged(); AutoSave(); } }
        }

        public double EqBass
        {
            get => Settings.EqBass;
            set { if (Math.Abs(Settings.EqBass - value) > 0.001) { Settings.EqBass = value; OnPropertyChanged(); OnPropertyChanged(nameof(EqBassLabel)); AutoSave(); } }
        }
        public string EqBassLabel => $"Bass: {EqBass:F1} dB";

        public double EqTreble
        {
            get => Settings.EqTreble;
            set { if (Math.Abs(Settings.EqTreble - value) > 0.001) { Settings.EqTreble = value; OnPropertyChanged(); OnPropertyChanged(nameof(EqTrebleLabel)); AutoSave(); } }
        }
        public string EqTrebleLabel => $"Treble: {EqTreble:F1} dB";

        public bool CompEnabled
        {
            get => Settings.CompEnabled;
            set { if (Settings.CompEnabled != value) { Settings.CompEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double CompThreshold
        {
            get => Settings.CompThreshold;
            set { if (Math.Abs(Settings.CompThreshold - value) > 0.001) { Settings.CompThreshold = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompThresholdLabel)); AutoSave(); } }
        }
        public string CompThresholdLabel => $"Thresh: {CompThreshold:F1} dB";
        public double CompRatio
        {
            get => Settings.CompRatio;
            set { if (Math.Abs(Settings.CompRatio - value) > 0.001) { Settings.CompRatio = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompRatioLabel)); AutoSave(); } }
        }
        public string CompRatioLabel => $"Ratio: {CompRatio:F1}:1";

        public bool LimiterEnabled
        {
            get => Settings.LimiterEnabled;
            set { if (Settings.LimiterEnabled != value) { Settings.LimiterEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double LimiterThreshold
        {
            get => Settings.LimiterThreshold;
            set { if (Math.Abs(Settings.LimiterThreshold - value) > 0.001) { Settings.LimiterThreshold = value; OnPropertyChanged(); OnPropertyChanged(nameof(LimiterThresholdLabel)); AutoSave(); } }
        }
        public string LimiterThresholdLabel => $"Thresh: {LimiterThreshold:F1} dB";

        public bool GainEnabled
        {
            get => Settings.GainEnabled;
            set { if (Settings.GainEnabled != value) { Settings.GainEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double GainDb
        {
            get => Settings.GainDb;
            set { if (Math.Abs(Settings.GainDb - value) > 0.001) { Settings.GainDb = value; OnPropertyChanged(); OnPropertyChanged(nameof(GainLabel)); AutoSave(); } }
        }
        public string GainLabel => $"Gain: {GainDb:F1} dB";

        public bool HighpassEnabled
        {
            get => Settings.HighpassEnabled;
            set { if (Settings.HighpassEnabled != value) { Settings.HighpassEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double HighpassFreq
        {
            get => Settings.HighpassFreq;
            set { if (Math.Abs(Settings.HighpassFreq - value) > 0.001) { Settings.HighpassFreq = value; OnPropertyChanged(); OnPropertyChanged(nameof(HighpassLabel)); AutoSave(); } }
        }
        public string HighpassLabel => $"Freq: {(int)HighpassFreq} Hz";

        public bool LowpassEnabled
        {
            get => Settings.LowpassEnabled;
            set { if (Settings.LowpassEnabled != value) { Settings.LowpassEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double LowpassFreq
        {
            get => Settings.LowpassFreq;
            set { if (Math.Abs(Settings.LowpassFreq - value) > 0.001) { Settings.LowpassFreq = value; OnPropertyChanged(); OnPropertyChanged(nameof(LowpassLabel)); AutoSave(); } }
        }
        public string LowpassLabel => $"Freq: {(int)LowpassFreq} Hz";

        public bool DelayEnabled
        {
            get => Settings.DelayEnabled;
            set { if (Settings.DelayEnabled != value) { Settings.DelayEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double DelayTime
        {
            get => Settings.DelayTime;
            set { if (Math.Abs(Settings.DelayTime - value) > 0.001) { Settings.DelayTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(DelayTimeLabel)); AutoSave(); } }
        }
        public string DelayTimeLabel => $"Time: {DelayTime:F2} s";
        public double DelayFeedback
        {
            get => Settings.DelayFeedback;
            set { if (Math.Abs(Settings.DelayFeedback - value) > 0.001) { Settings.DelayFeedback = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double DelayMix
        {
            get => Settings.DelayMix;
            set { if (Math.Abs(Settings.DelayMix - value) > 0.001) { Settings.DelayMix = value; OnPropertyChanged(); OnPropertyChanged(nameof(DelayMixLabel)); AutoSave(); } }
        }
        public string DelayMixLabel => $"Mix: {DelayMix:F2}";

        public bool ChorusEnabled
        {
            get => Settings.ChorusEnabled;
            set { if (Settings.ChorusEnabled != value) { Settings.ChorusEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double ChorusRate
        {
            get => Settings.ChorusRate;
            set { if (Math.Abs(Settings.ChorusRate - value) > 0.001) { Settings.ChorusRate = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChorusRateLabel)); AutoSave(); } }
        }
        public string ChorusRateLabel => $"Rate: {ChorusRate:F1} Hz";
        public double ChorusDepth
        {
            get => Settings.ChorusDepth;
            set { if (Math.Abs(Settings.ChorusDepth - value) > 0.001) { Settings.ChorusDepth = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double ChorusMix
        {
            get => Settings.ChorusMix;
            set { if (Math.Abs(Settings.ChorusMix - value) > 0.001) { Settings.ChorusMix = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool DistortionEnabled
        {
            get => Settings.DistortionEnabled;
            set { if (Settings.DistortionEnabled != value) { Settings.DistortionEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double DistortionDrive
        {
            get => Settings.DistortionDrive;
            set { if (Math.Abs(Settings.DistortionDrive - value) > 0.001) { Settings.DistortionDrive = value; OnPropertyChanged(); OnPropertyChanged(nameof(DistortionDriveLabel)); AutoSave(); } }
        }
        public string DistortionDriveLabel => $"Drive: {DistortionDrive:F1} dB";

        public bool PhaserEnabled
        {
            get => Settings.PhaserEnabled;
            set { if (Settings.PhaserEnabled != value) { Settings.PhaserEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double PhaserRate
        {
            get => Settings.PhaserRate;
            set { if (Math.Abs(Settings.PhaserRate - value) > 0.001) { Settings.PhaserRate = value; OnPropertyChanged(); OnPropertyChanged(nameof(PhaserRateLabel)); AutoSave(); } }
        }
        public string PhaserRateLabel => $"Rate: {PhaserRate:F1} Hz";

        public double PhaserDepth
        {
            get => Settings.PhaserDepth;
            set { if (Math.Abs(Settings.PhaserDepth - value) > 0.001) { Settings.PhaserDepth = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double PhaserMix
        {
            get => Settings.PhaserMix;
            set { if (Math.Abs(Settings.PhaserMix - value) > 0.001) { Settings.PhaserMix = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool ClippingEnabled
        {
            get => Settings.ClippingEnabled;
            set { if (Settings.ClippingEnabled != value) { Settings.ClippingEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double ClippingThresh
        {
            get => Settings.ClippingThresh;
            set { if (Math.Abs(Settings.ClippingThresh - value) > 0.001) { Settings.ClippingThresh = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClippingThreshLabel)); AutoSave(); } }
        }
        public string ClippingThreshLabel => $"Thresh: {ClippingThresh:F1} dB";

        public bool BitcrushEnabled
        {
            get => Settings.BitcrushEnabled;
            set { if (Settings.BitcrushEnabled != value) { Settings.BitcrushEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double BitcrushDepth
        {
            get => Settings.BitcrushDepth;
            set { if (Math.Abs(Settings.BitcrushDepth - value) > 0.001) { Settings.BitcrushDepth = value; OnPropertyChanged(); OnPropertyChanged(nameof(BitcrushDepthLabel)); AutoSave(); } }
        }
        public string BitcrushDepthLabel => $"Depth: {BitcrushDepth:F1}";

        public bool GsmEnabled
        {
            get => Settings.GsmEnabled;
            set { if (Settings.GsmEnabled != value) { Settings.GsmEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }

        public bool PitchShiftEnabled
        {
            get => Settings.PitchShiftEnabled;
            set { if (Settings.PitchShiftEnabled != value) { Settings.PitchShiftEnabled = value; OnPropertyChanged(); AutoSave(); } }
        }
        public double PitchShiftSemitones
        {
            get => Settings.PitchShiftSemitones;
            set { if (Math.Abs(Settings.PitchShiftSemitones - value) > 0.001) { Settings.PitchShiftSemitones = value; OnPropertyChanged(); OnPropertyChanged(nameof(PitchShiftLabel)); AutoSave(); } }
        }
        public string PitchShiftLabel => $"Shift: {PitchShiftSemitones:F1} st";

        // --- Mixing Tab State ---
        private LanguageInfo? _mixLangA;
        public LanguageInfo? MixLangA
        {
            get => _mixLangA;
            set
            {
                if (SetProperty(ref _mixLangA, value) && value != null)
                {
                    UpdateMixVoicesA(value.Code);
                }
            }
        }

        private string? _selectedMixVoiceA;
        public string? SelectedMixVoiceA
        {
            get => _selectedMixVoiceA;
            set => SetProperty(ref _selectedMixVoiceA, value);
        }

        private LanguageInfo? _mixLangB;
        public LanguageInfo? MixLangB
        {
            get => _mixLangB;
            set
            {
                if (SetProperty(ref _mixLangB, value) && value != null)
                {
                    UpdateMixVoicesB(value.Code);
                }
            }
        }

        private string? _selectedMixVoiceB;
        public string? SelectedMixVoiceB
        {
            get => _selectedMixVoiceB;
            set => SetProperty(ref _selectedMixVoiceB, value);
        }

        private string _selectedMixingOperation = "mix";
        public string SelectedMixingOperation
        {
            get => _selectedMixingOperation;
            set
            {
                if (SetProperty(ref _selectedMixingOperation, value))
                {
                    OnPropertyChanged(nameof(MixRatioLabel));
                }
            }
        }

        private double _mixRatio = 0.5;
        public double MixRatio
        {
            get => _mixRatio;
            set
            {
                if (SetProperty(ref _mixRatio, value))
                {
                    OnPropertyChanged(nameof(MixRatioLabel));
                }
            }
        }

        public string MixRatioLabel
        {
            get
            {
                int p = (int)(MixRatio * 100);
                if (SelectedMixingOperation == "mix")
                    return $"Mix: {100 - p}% A / {p}% B";
                if (SelectedMixingOperation == "divide")
                    return $"Op: Divide | Influence: {p}%\n(Results are more likely to be unstable and VERY LOUD)";
                return $"Op: {char.ToUpper(SelectedMixingOperation[0])}{SelectedMixingOperation[1..]} | Influence: {p}%";
            }
        }

        private LanguageInfo? _previewMixLanguage;
        public LanguageInfo? PreviewMixLanguage
        {
            get => _previewMixLanguage;
            set => SetProperty(ref _previewMixLanguage, value);
        }

        private string _newVoiceName = string.Empty;
        public string NewVoiceName
        {
            get => _newVoiceName;
            set => SetProperty(ref _newVoiceName, value);
        }

        private string _mixStatus = string.Empty;
        public string MixStatus
        {
            get => _mixStatus;
            set => SetProperty(ref _mixStatus, value);
        }

        // --- Lexicon Tab State ---
        private string _newLexiconOrig = string.Empty;
        public string NewLexiconOrig
        {
            get => _newLexiconOrig;
            set => SetProperty(ref _newLexiconOrig, value);
        }

        private string _newLexiconReplace = string.Empty;
        public string NewLexiconReplace
        {
            get => _newLexiconReplace;
            set => SetProperty(ref _newLexiconReplace, value);
        }

        // --- Bottom Actions State ---
        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isStatusError = false;
        public bool IsStatusError
        {
            get => _isStatusError;
            set => SetProperty(ref _isStatusError, value);
        }

        private string _detailText = "...";
        public string DetailText
        {
            get => _detailText;
            set => SetProperty(ref _detailText, value);
        }

        private double _progressValue = 0.0;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private string _infoText = "Time: 00:00 / ETA: --:-- | 0%";
        public string InfoText
        {
            get => _infoText;
            set => SetProperty(ref _infoText, value);
        }

        private bool _isRunning = false;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    OnPropertyChanged(nameof(IsNotRunning));
                    StartConversionCommand.RaiseCanExecuteChanged();
                    PreviewAudioCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public bool IsNotRunning => !IsRunning;

        public string StartButtonText => Settings.JitEnabled ? "Start Real-time JIT" : "Start Generation";

        // --- Commands ---
        public RelayCommand StartConversionCommand { get; }
        public RelayCommand PreviewAudioCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand BrowseInputFileCommand { get; }
        public RelayCommand BrowseOutputDirCommand { get; }
        public RelayCommand IncrementThreadsCommand { get; }
        public RelayCommand DecrementThreadsCommand { get; }
        public RelayCommand SavePresetCommand { get; }
        public RelayCommand RefreshPresetsCommand { get; }
        public RelayCommand SaveFxPresetCommand { get; }
        public RelayCommand RefreshFxPresetsCommand { get; }
        public RelayCommand PreviewMixCommand { get; }
        public RelayCommand CreateMixVoiceCommand { get; }
        public RelayCommand<string> DeleteCustomVoiceCommand { get; }
        public RelayCommand AddLexiconRuleCommand { get; }
        public RelayCommand<LexiconRule> DeleteLexiconRuleCommand { get; }

        public event Func<string, string, string?>? RequestTextInputDialog;

        public MainViewModel(
            ModelManagerService modelManager,
            KokoroEngineService engineService,
            VoiceMixingService voiceMixingService,
            DocumentParserService docParser,
            AudioPlaybackService playbackService)
        {
            _modelManager = modelManager;
            _engineService = engineService;
            _voiceMixingService = voiceMixingService;
            _docParser = docParser;
            _playbackService = playbackService;

            Settings = _modelManager.LoadSettings();

            // Populate Languages
            Languages.Add(new LanguageInfo { Name = "American English", Code = "a" });
            Languages.Add(new LanguageInfo { Name = "British English", Code = "b" });
            Languages.Add(new LanguageInfo { Name = "Spanish", Code = "e" });
            Languages.Add(new LanguageInfo { Name = "French", Code = "f" });
            Languages.Add(new LanguageInfo { Name = "Italian", Code = "i" });
            Languages.Add(new LanguageInfo { Name = "Portuguese", Code = "p" });
            Languages.Add(new LanguageInfo { Name = "Japanese", Code = "j" });
            Languages.Add(new LanguageInfo { Name = "Chinese", Code = "z" });

            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == Settings.LangCode) ?? Languages[0];
            MixLangA = Languages[0];
            MixLangB = Languages[0];
            PreviewMixLanguage = Languages[0];

            // Lexicon
            foreach (var kv in Settings.Lexicon)
            {
                LexiconRules.Add(new LexiconRule(kv.Key, kv.Value));
            }

            // Engine Callbacks
            _engineService.OnProgress += (percent, elapsed, eta, detail) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ProgressValue = percent / 100.0;
                    string elapsedStr = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
                    InfoText = $"Time: {elapsedStr} / ETA: {eta} | {(int)percent}%";
                    DetailText = detail;
                });
            };

            _engineService.OnStatus += (msg, isError) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusText = msg.Split('\n')[0];
                    IsStatusError = isError;
                });
            };

            _engineService.OnFinished += () =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsRunning = false;
                });
            };

            // Setup Commands
            StartConversionCommand = new RelayCommand(ExecuteStartConversion, () => IsNotRunning);
            PreviewAudioCommand = new RelayCommand(ExecutePreviewAudio, () => IsNotRunning);
            CancelCommand = new RelayCommand(ExecuteCancel, () => IsRunning);
            BrowseInputFileCommand = new RelayCommand(ExecuteBrowseInputFile);
            BrowseOutputDirCommand = new RelayCommand(ExecuteBrowseOutputDir);
            IncrementThreadsCommand = new RelayCommand(() => NumThreads++);
            DecrementThreadsCommand = new RelayCommand(() => NumThreads--);
            SavePresetCommand = new RelayCommand(ExecuteSavePreset);
            RefreshPresetsCommand = new RelayCommand(RefreshPresets);
            SaveFxPresetCommand = new RelayCommand(ExecuteSaveFxPreset);
            RefreshFxPresetsCommand = new RelayCommand(RefreshFxPresets);
            PreviewMixCommand = new RelayCommand(ExecutePreviewMix);
            CreateMixVoiceCommand = new RelayCommand(ExecuteCreateMixVoice);
            DeleteCustomVoiceCommand = new RelayCommand<string>(ExecuteDeleteCustomVoice);
            AddLexiconRuleCommand = new RelayCommand(ExecuteAddLexiconRule);
            DeleteLexiconRuleCommand = new RelayCommand<LexiconRule>(ExecuteDeleteLexiconRule);

            _playbackService.PlaybackStopped += () =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (StatusText == "Playing preview...")
                    {
                        StatusText = "Ready";
                    }
                    if (MixStatus == "Playing preview...")
                    {
                        MixStatus = "Ready";
                    }
                });
            };

            RefreshPresets();
            RefreshFxPresets();
            RefreshCustomVoices();
        }

        public async Task InitializeEngineAsync()
        {
            await _engineService.InitializeAsync();
            UpdateVoiceList();
            RefreshCustomVoices();
        }

        private void UpdateVoiceList()
        {
            string code = SelectedLanguage?.Code ?? "a";
            var standard = VoiceDb.ContainsKey(code) ? VoiceDb[code] : VoiceDb["a"];
            var custom = GetCustomVoiceNames();

            CurrentVoices.Clear();
            foreach (var v in standard.Concat(custom).OrderBy(v => v))
            {
                CurrentVoices.Add(v);
            }

            if (!CurrentVoices.Contains(SelectedVoice))
            {
                SelectedVoice = CurrentVoices.FirstOrDefault() ?? "af_heart";
            }
        }

        private void UpdateMixVoicesA(string code)
        {
            var list = (VoiceDb.ContainsKey(code) ? VoiceDb[code] : VoiceDb["a"]).Concat(GetCustomVoiceNames()).OrderBy(v => v).ToList();
            MixVoicesA.Clear();
            foreach (var v in list) MixVoicesA.Add(v);
            if (string.IsNullOrEmpty(SelectedMixVoiceA) || !MixVoicesA.Contains(SelectedMixVoiceA))
            {
                SelectedMixVoiceA = MixVoicesA.FirstOrDefault();
            }
        }

        private void UpdateMixVoicesB(string code)
        {
            var list = (VoiceDb.ContainsKey(code) ? VoiceDb[code] : VoiceDb["a"]).Concat(GetCustomVoiceNames()).OrderBy(v => v).ToList();
            MixVoicesB.Clear();
            foreach (var v in list) MixVoicesB.Add(v);
            if (string.IsNullOrEmpty(SelectedMixVoiceB) || !MixVoicesB.Contains(SelectedMixVoiceB))
            {
                SelectedMixVoiceB = MixVoicesB.Skip(1).FirstOrDefault() ?? MixVoicesB.FirstOrDefault();
            }
        }

        public List<string> GetCustomVoiceNames()
        {
            if (Directory.Exists(_modelManager.CustomVoicesDirectory))
            {
                return Directory.GetFiles(_modelManager.CustomVoicesDirectory, "*.bin")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => n!)
                    .ToList();
            }
            return new List<string>();
        }

        public void RefreshCustomVoices()
        {
            CustomVoices.Clear();
            foreach (var cv in GetCustomVoiceNames().OrderBy(v => v))
            {
                CustomVoices.Add(cv);
            }
            UpdateVoiceList();
            if (MixLangA != null) UpdateMixVoicesA(MixLangA.Code);
            if (MixLangB != null) UpdateMixVoicesB(MixLangB.Code);
        }

        public void RefreshPresets()
        {
            Presets.Clear();
            Presets.Add("Select Preset...");
            if (Directory.Exists(_modelManager.PresetsDirectory))
            {
                foreach (var file in Directory.GetFiles(_modelManager.PresetsDirectory, "*.json"))
                {
                    Presets.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            SelectedPreset = "Select Preset...";
        }

        public void RefreshFxPresets()
        {
            FxPresets.Clear();
            FxPresets.Add("Select FX Preset...");
            if (Directory.Exists(_modelManager.FxPresetsDirectory))
            {
                foreach (var file in Directory.GetFiles(_modelManager.FxPresetsDirectory, "*.json"))
                {
                    FxPresets.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            SelectedFxPreset = "Select FX Preset...";
        }

        private void LoadPreset(string name)
        {
            var preset = _engineService.LoadPreset(name);
            if (preset == null) return;

            if (!string.IsNullOrEmpty(preset.Voice)) SelectedVoice = preset.Voice;
            Speed = preset.Speed;
            Volume = preset.Volume;
            Pitch = preset.Pitch;
            NormalizeAudio = preset.Normalize;
            TrimSilence = preset.Trim;
            if (!string.IsNullOrEmpty(preset.Format)) SelectedFormat = preset.Format;
            ApplyFx = preset.ApplyFx;

            if (!string.IsNullOrEmpty(preset.FxPreset) && preset.FxPreset != "Select FX Preset...")
            {
                LoadFxPreset(preset.FxPreset);
                SelectedFxPreset = preset.FxPreset;
            }
        }

        private void LoadFxPreset(string name)
        {
            var fx = _engineService.LoadFxPreset(name);
            if (fx == null) return;

            ReverbEnabled = fx.ReverbEnabled;
            ReverbRoomSize = fx.ReverbRoomSize;
            ReverbWetLevel = fx.ReverbWetLevel;
            ReverbDamping = fx.ReverbDamping;
            ReverbWidth = fx.ReverbWidth;

            EqBass = fx.EqBass;
            EqTreble = fx.EqTreble;

            CompEnabled = fx.CompEnabled;
            CompThreshold = fx.CompThreshold;
            CompRatio = fx.CompRatio;

            DistortionEnabled = fx.DistortionEnabled;
            DistortionDrive = fx.DistortionDrive;

            ChorusEnabled = fx.ChorusEnabled;
            ChorusRate = fx.ChorusRate;
            ChorusDepth = fx.ChorusDepth;
            ChorusMix = fx.ChorusMix;

            PhaserEnabled = fx.PhaserEnabled;
            PhaserRate = fx.PhaserRate;
            PhaserDepth = fx.PhaserDepth;
            PhaserMix = fx.PhaserMix;

            ClippingEnabled = fx.ClippingEnabled;
            ClippingThresh = fx.ClippingThresh;

            BitcrushEnabled = fx.BitcrushEnabled;
            BitcrushDepth = fx.BitcrushDepth;

            GsmEnabled = fx.GsmEnabled;

            HighpassEnabled = fx.HighpassEnabled;
            HighpassFreq = fx.HighpassFreq;

            LowpassEnabled = fx.LowpassEnabled;
            LowpassFreq = fx.LowpassFreq;

            DelayEnabled = fx.DelayEnabled;
            DelayTime = fx.DelayTime;
            DelayFeedback = fx.DelayFeedback;
            DelayMix = fx.DelayMix;

            PitchShiftEnabled = fx.PitchShiftEnabled;
            PitchShiftSemitones = fx.PitchShiftSemitones;

            LimiterEnabled = fx.LimiterEnabled;
            LimiterThreshold = fx.LimiterThreshold;

            GainEnabled = fx.GainEnabled;
            GainDb = fx.GainDb;
        }

        private void ExecuteSavePreset()
        {
            string? name = RequestTextInputDialog?.Invoke("Enter preset name:", "Save Preset");
            if (string.IsNullOrWhiteSpace(name)) return;

            name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            var preset = new VoicePreset
            {
                Voice = SelectedVoice,
                Speed = Speed,
                Volume = Volume,
                Pitch = Pitch,
                SplitPattern = Settings.SplitPattern,
                Normalize = NormalizeAudio,
                Trim = TrimSilence,
                Format = SelectedFormat,
                ApplyFx = ApplyFx,
                FxPreset = SelectedFxPreset
            };

            string path = Path.Combine(_modelManager.PresetsDirectory, $"{name}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true }));
            RefreshPresets();
            SelectedPreset = name;
        }

        private void ExecuteSaveFxPreset()
        {
            string? name = RequestTextInputDialog?.Invoke("Enter FX preset name:", "Save FX Preset");
            if (string.IsNullOrWhiteSpace(name)) return;

            name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            var fx = new FxPreset
            {
                ReverbEnabled = ReverbEnabled,
                ReverbRoomSize = ReverbRoomSize,
                ReverbWetLevel = ReverbWetLevel,
                ReverbDamping = ReverbDamping,
                ReverbWidth = ReverbWidth,
                EqBass = EqBass,
                EqTreble = EqTreble,
                CompEnabled = CompEnabled,
                CompThreshold = CompThreshold,
                CompRatio = CompRatio,
                DistortionEnabled = DistortionEnabled,
                DistortionDrive = DistortionDrive,
                ChorusEnabled = ChorusEnabled,
                ChorusRate = ChorusRate,
                ChorusDepth = ChorusDepth,
                ChorusMix = ChorusMix,
                PhaserEnabled = PhaserEnabled,
                PhaserRate = PhaserRate,
                PhaserDepth = PhaserDepth,
                PhaserMix = PhaserMix,
                ClippingEnabled = ClippingEnabled,
                ClippingThresh = ClippingThresh,
                BitcrushEnabled = BitcrushEnabled,
                BitcrushDepth = BitcrushDepth,
                GsmEnabled = GsmEnabled,
                HighpassEnabled = HighpassEnabled,
                HighpassFreq = HighpassFreq,
                LowpassEnabled = LowpassEnabled,
                LowpassFreq = LowpassFreq,
                DelayEnabled = DelayEnabled,
                DelayTime = DelayTime,
                DelayFeedback = DelayFeedback,
                DelayMix = DelayMix,
                PitchShiftEnabled = PitchShiftEnabled,
                PitchShiftSemitones = PitchShiftSemitones,
                LimiterEnabled = LimiterEnabled,
                LimiterThreshold = LimiterThreshold,
                GainEnabled = GainEnabled,
                GainDb = GainDb
            };

            string path = Path.Combine(_modelManager.FxPresetsDirectory, $"{name}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(fx, new JsonSerializerOptions { WriteIndented = true }));
            RefreshFxPresets();
            SelectedFxPreset = name;
        }

        private void ExecuteBrowseInputFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Supported Documents (*.txt;*.pdf;*.epub)|*.txt;*.pdf;*.epub|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                InputFilePath = dialog.FileName;
            }
        }

        private void ExecuteBrowseOutputDir()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                OutputDirectory = dialog.FolderName;
            }
        }

        private async void ExecutePreviewAudio()
        {
            if (!_engineService.IsInitialized)
            {
                StatusText = "Engine initializing... please wait.";
                return;
            }

            string text = GetInputText();
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "This is a sample audio preview using the Kokoro TTS engine. It demonstrates voice quality and speed settings.";
            }

            StatusText = "Generating preview...";
            IsStatusError = false;

            float[]? samples = await _engineService.GeneratePreviewAudioAsync(text, SelectedVoice, Speed, Settings);

            if (samples != null && samples.Length > 0)
            {
                StatusText = "Playing preview...";
                _playbackService.PlayAudioData(samples);
            }
            else
            {
                StatusText = "Preview failed.";
                IsStatusError = true;
            }
        }

        private async void ExecutePreviewMix()
        {
            if (string.IsNullOrEmpty(SelectedMixVoiceA) || string.IsNullOrEmpty(SelectedMixVoiceB)) return;

            var vA = _engineService.ResolveVoice(SelectedMixVoiceA, MixLangA?.Code);
            var vB = _engineService.ResolveVoice(SelectedMixVoiceB, MixLangB?.Code);

            if (vA == null || vB == null)
            {
                MixStatus = "Error: Voice not found.";
                return;
            }

            MixStatus = "Generating preview...";
            var (success, msg, tensor) = _voiceMixingService.MixVoices(vA, vB, MixRatio, "", SelectedMixingOperation);
            if (!success || tensor == null)
            {
                MixStatus = $"Mix failed: {msg}";
                return;
            }

            string previewText = "This is a preview of your custom mixed voice.";
            string langCode = PreviewMixLanguage?.Code ?? "a";
            if (langCode == "f") previewText = "Ceci est un aperçu de votre voix personnalisée.";
            else if (langCode == "e") previewText = "Esta es una vista previa de su voz personalizada.";
            else if (langCode == "j") previewText = "これはカスタム合成音声のプレビューです。";
            else if (langCode == "z") previewText = "这是您的自定义混合语音预览。";
            else if (langCode == "i") previewText = "Questa è un'anteprima della tua voce personalizzata.";
            else if (langCode == "p") previewText = "Esta é uma prévia da sua voz personalizada.";

            var previewConfig = new AppSettings
            {
                Volume = 1.0,
                Pitch = 0.0,
                Speed = 1.0,
                ApplyFx = false,
                Normalize = false,
                Trim = false
            };

            float[]? samples = await _engineService.GeneratePreviewAudioAsync(
                previewText,
                SelectedMixVoiceA ?? "af_heart",
                1.0,
                previewConfig,
                tensor,
                langCode
            );

            if (samples != null && samples.Length > 0)
            {
                MixStatus = "Playing preview...";
                _playbackService.PlayAudioData(samples);
            }
            else
            {
                MixStatus = "Preview generation failed.";
            }
        }

        private void ExecuteCreateMixVoice()
        {
            if (string.IsNullOrWhiteSpace(NewVoiceName))
            {
                MessageBox.Show("Please enter a name for the new voice.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(SelectedMixVoiceA) || string.IsNullOrEmpty(SelectedMixVoiceB)) return;

            var vA = _engineService.ResolveVoice(SelectedMixVoiceA, MixLangA?.Code);
            var vB = _engineService.ResolveVoice(SelectedMixVoiceB, MixLangB?.Code);

            if (vA == null || vB == null) return;

            var (success, msg, _) = _voiceMixingService.MixVoices(vA, vB, MixRatio, NewVoiceName.Trim(), SelectedMixingOperation);
            if (success)
            {
                _engineService.ReloadVoices();
                MixStatus = $"Saved: {NewVoiceName.Trim()}";
                RefreshCustomVoices();
                NewVoiceName = string.Empty;
            }
            else
            {
                MixStatus = $"Error: {msg}";
            }
        }

        private void ExecuteDeleteCustomVoice(string? voiceName)
        {
            if (string.IsNullOrEmpty(voiceName)) return;
            if (MessageBox.Show($"Delete custom voice '{voiceName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (_voiceMixingService.DeleteCustomVoice(voiceName))
                {
                    _engineService.ReloadVoices();
                    RefreshCustomVoices();
                }
            }
        }

        private void ExecuteAddLexiconRule()
        {
            string orig = NewLexiconOrig.Trim();
            string rep = NewLexiconReplace.Trim();

            if (string.IsNullOrEmpty(orig))
            {
                MessageBox.Show("Original text cannot be empty.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Settings.Lexicon[orig] = rep;
            LexiconRules.Add(new LexiconRule(orig, rep));
            NewLexiconOrig = string.Empty;
            NewLexiconReplace = string.Empty;
            AutoSave();
        }

        private void ExecuteDeleteLexiconRule(LexiconRule? rule)
        {
            if (rule == null) return;
            Settings.Lexicon.Remove(rule.Original);
            LexiconRules.Remove(rule);
            AutoSave();
        }

        private async void ExecuteStartConversion()
        {
            string text = GetInputText();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("No text to process.", "Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsRunning = true;
            ProgressValue = 0.0;

            if (Settings.JitEnabled)
            {
                await _engineService.StartJitConversionAsync(text, Settings);
            }
            else
            {
                await _engineService.StartConversionAsync(text, Settings);
            }
        }

        private void ExecuteCancel()
        {
            _engineService.Cancel();
            StatusText = "Cancelling... waiting for workers...";
        }

        private string GetInputText()
        {
            if (SelectedInputTab == 0)
            {
                return DirectText;
            }
            else
            {
                if (File.Exists(InputFilePath))
                {
                    try
                    {
                        return _docParser.ExtractText(InputFilePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Read failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                return string.Empty;
            }
        }

        public void AutoSave()
        {
            _modelManager.SaveSettings(Settings);
        }
    }
}
