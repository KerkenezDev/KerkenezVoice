namespace KerkenezVoice.Languages
{
    public class EnglishLanguage : BaseLanguage
    {
        public override string Code => "en";
        public override string Name => "English";
        public override string EnglishName => "English";
        public override string FlagEmoji => "🇬🇧";

        protected override void InitTranslations()
        {
            // Navigation
            Set(StringKeys.NavSynthesize, "Synthesize");
            Set(StringKeys.NavEbookVoicer, "Ebook Voicer");
            Set(StringKeys.NavCustomVoices, "Custom Voices");
            Set(StringKeys.NavAudioFx, "Audio FX");
            Set(StringKeys.NavLexicon, "Lexicon");
            Set(StringKeys.NavSettings, "Settings");
            Set(StringKeys.NavLiveLogs, "Live Logs");
            Set(StringKeys.NavTipExpandSidebar, "Expand sidebar");
            Set(StringKeys.NavTipCollapseSidebar, "Collapse sidebar");

            // Shell & Window
            Set(StringKeys.AppTitle, "Kerkenez Voice");
            Set(StringKeys.MainShortcutsPromptTitle, "Create Desktop Shortcuts");
            Set(StringKeys.MainShortcutsPromptDesc, "Would you like to create Start Menu and Desktop shortcuts for Kerkenez Voice?");
            Set(StringKeys.StatusStartingUp, "Starting up Kerkenez Voice...");
            Set(StringKeys.StatusReady, "Ready");
            Set(StringKeys.StatusReadyVoice, "Ready | Active Voice: {0}");
            Set(StringKeys.StatusSynthesizing, "Synthesizing speech: {0} ({1:0.0}%)");
            Set(StringKeys.StatusPlaying, "Playing preview audio...");
            Set(StringKeys.StatusModelsRequired, "Model files missing. Please download models.");
            Set(StringKeys.StatusMetrics, "Voice: {0} | Threads: {1} | 24kHz");

            // Synthesize
            Set(StringKeys.SynthTitle, "Speech Synthesis");
            Set(StringKeys.SynthInputSource, "Input Source");
            Set(StringKeys.SynthInputModeText, "Direct Text");
            Set(StringKeys.SynthInputModeFile, "Load Document");
            Set(StringKeys.SynthDirectTextPlaceholder, "Enter text here to generate high quality speech...");
            Set(StringKeys.SynthSelectFile, "Document File Path:");
            Set(StringKeys.SynthBrowse, "Browse...");
            Set(StringKeys.SynthSupportedDocs, "Supported document formats: .txt, .pdf, .epub");
            Set(StringKeys.SynthConfiguration, "Voice & Synthesis Options");
            Set(StringKeys.SynthPreset, "Preset:");
            Set(StringKeys.SynthSavePreset, "Save Preset");
            Set(StringKeys.SynthRefreshPresets, "Refresh");
            Set(StringKeys.SynthLanguage, "Language:");
            Set(StringKeys.SynthVoice, "Voice:");
            Set(StringKeys.SynthSpeed, "Speed: {0:0.00}x");
            Set(StringKeys.SynthPitch, "Pitch: {0:0} st");
            Set(StringKeys.SynthVolume, "Volume: {0:0.00}x");
            Set(StringKeys.SynthFormat, "Format:");
            Set(StringKeys.SynthThreads, "Parallel Threads: {0}");
            Set(StringKeys.SynthCombine, "Combine Output Segments");
            Set(StringKeys.SynthSeparate, "Save Individual Segments");
            Set(StringKeys.SynthSubtitles, "Export Subtitles (.srt)");
            Set(StringKeys.SynthNormalize, "Normalize Audio");
            Set(StringKeys.SynthTrim, "Trim Silence");
            Set(StringKeys.SynthApplyFx, "Apply FX Pipeline");
            Set(StringKeys.SynthFxPreset, "FX Preset:");
            Set(StringKeys.SynthBtnPreview, "🔊 Preview Audio");
            Set(StringKeys.SynthBtnGenerate, "🎙️ Generate Speech");
            Set(StringKeys.SynthBtnCancel, "⏹️ Cancel");
            Set(StringKeys.SynthBtnOpenFolder, "📁 Open Output Folder");
            Set(StringKeys.SynthStatusComplete, "Synthesis completed successfully!");
            Set(StringKeys.SynthStatusCancelled, "Synthesis cancelled by user.");
            Set(StringKeys.SynthStatusError, "Synthesis error: {0}");

            // Custom Voices
            Set(StringKeys.VoiceMixTitle, "Voice Mixing Studio");
            Set(StringKeys.VoiceMixVoiceA, "Voice A:");
            Set(StringKeys.VoiceMixVoiceB, "Voice B:");
            Set(StringKeys.VoiceMixOperation, "Operation:");
            Set(StringKeys.VoiceMixRatio, "Blend Ratio (A: {0:0}%, B: {1:0}%):");
            Set(StringKeys.VoiceMixBtnPreview, "🔊 Preview Blend");
            Set(StringKeys.VoiceMixNewName, "New Voice Name:");
            Set(StringKeys.VoiceMixBtnCreate, "Create & Save Custom Voice");
            Set(StringKeys.VoiceMixListTitle, "Saved Custom Voices");
            Set(StringKeys.VoiceMixBtnDelete, "Delete Voice");
            Set(StringKeys.VoiceMixCreatedSuccess, "Custom voice '{0}' saved successfully!");

            // Audio FX
            Set(StringKeys.FxTitle, "Studio Audio FX Pipeline (Pure C# DSP)");
            Set(StringKeys.FxPreset, "FX Preset:");
            Set(StringKeys.FxSavePreset, "Save Preset");
            Set(StringKeys.FxRefreshPresets, "Refresh");
            Set(StringKeys.FxApplyMaster, "Enable Audio FX Master Switch");
            Set(StringKeys.FxDynamicsTitle, "Dynamics Processing");
            Set(StringKeys.FxComp, "Dynamic Compressor");
            Set(StringKeys.FxCompThreshold, "Threshold: {0:0} dB");
            Set(StringKeys.FxCompRatio, "Ratio: {0:0}:1");
            Set(StringKeys.FxLimiter, "Peak Limiter");
            Set(StringKeys.FxLimiterThreshold, "Threshold: {0:0} dB");
            Set(StringKeys.FxGain, "Gain Stage");
            Set(StringKeys.FxGainDb, "Boost: {0:+0.0;-0.0;0.0} dB");
            Set(StringKeys.FxEqTitle, "Equalization & Filters");
            Set(StringKeys.FxBass, "Bass EQ: {0:+0;-0;0} dB");
            Set(StringKeys.FxTreble, "Treble EQ: {0:+0;-0;0} dB");
            Set(StringKeys.FxHpf, "High-Pass Filter: {0:0} Hz");
            Set(StringKeys.FxLpf, "Low-Pass Filter: {0:0} Hz");
            Set(StringKeys.FxSpatialTitle, "Spatial & Time Effects");
            Set(StringKeys.FxReverb, "Schroeder Reverb");
            Set(StringKeys.FxReverbRoom, "Room Size: {0:0}%");
            Set(StringKeys.FxReverbWet, "Wet Level: {0:0}%");
            Set(StringKeys.FxDelay, "Stereo Delay");
            Set(StringKeys.FxDelayTime, "Delay Time: {0:0.00}s");
            Set(StringKeys.FxDelayFeedback, "Feedback: {0:0}%");
            Set(StringKeys.FxDelayMix, "Dry/Wet Mix: {0:0}%");
            Set(StringKeys.FxModTitle, "Modulation, Pitch & Texture");
            Set(StringKeys.FxChorus, "Multi-voice Chorus");
            Set(StringKeys.FxChorusRate, "Rate: {0:0.0} Hz");
            Set(StringKeys.FxPhaser, "Phase Shifter");
            Set(StringKeys.FxPhaserRate, "Rate: {0:0.0} Hz");
            Set(StringKeys.FxDistortion, "Analog Distortion");
            Set(StringKeys.FxDistortionDrive, "Drive: {0:0}%");
            Set(StringKeys.FxClipping, "Soft Saturation / Clipping");
            Set(StringKeys.FxPitchShift, "Pitch Shift: {0:+0;-0;0} semitones");
            Set(StringKeys.FxBitcrush, "Digital Bitcrusher");
            Set(StringKeys.FxGsm, "GSM Telephony Codec Emulation");

            // Lexicon
            Set(StringKeys.LexTitle, "Pronunciation Lexicon Rules");
            Set(StringKeys.LexOriginal, "Word / Acronym:");
            Set(StringKeys.LexReplacement, "Pronounce As:");
            Set(StringKeys.LexBtnAdd, "Add Rule");
            Set(StringKeys.LexListTitle, "Active Pronunciation Rules");
            Set(StringKeys.LexNote, "Note: Replacements are applied case-insensitively to the text before speech synthesis.");
            Set(StringKeys.LexBtnDelete, "Delete");

            // Settings
            Set(StringKeys.SettingsTitle, "Application Settings");
            Set(StringKeys.SettingsSecLanguage, "Language & Regional Preferences");
            Set(StringKeys.SettingsLanguageDesc, "Select the user interface display language.");
            Set(StringKeys.SettingsSecModel, "AI Model & Weights Management");
            Set(StringKeys.SettingsModelPath, "Model Folder: %LOCALAPPDATA%\\Programs\\Kerkenez\\voice\\models");
            Set(StringKeys.SettingsBtnDownloadModels, "Download Models (Kokoro-82M)");
            Set(StringKeys.SettingsModelsReady, "Model weights are present and verified.");
            Set(StringKeys.SettingsSecVoiceDefaults, "Voice Defaults");
            Set(StringKeys.SettingsDefaultVoice, "Default Voice:");
            Set(StringKeys.SettingsDefaultFormat, "Default Export Format:");
            Set(StringKeys.SettingsSecThreads, "Parallel Processing");
            Set(StringKeys.SettingsThreadsDesc, "Parallel threads for chunk synthesis (Higher threads speed up long text, uses more RAM).");
            Set(StringKeys.SettingsSecOutput, "Default Audio Output Directory");
            Set(StringKeys.SettingsOutDir, "Target Directory:");
            Set(StringKeys.SettingsBtnBrowseOutDir, "Browse...");
            Set(StringKeys.SettingsSecUi, "🖥️  Interface & Layout");
            Set(StringKeys.SettingsCollapseSidebar, "Start with left sidebar collapsed by default (compact icon rail on launch)");
            Set(StringKeys.SettingsEbookSplitter, "Ebook Studio Chapter List Width (px):");
            Set(StringKeys.SettingsWindowScale, "Window Size Scaling (% of screen area):");
            Set(StringKeys.SettingsScalingHeader, "Default Launch Window Scaling (Relative to Display):");
            Set(StringKeys.SettingsScalingDesc, "Target proportion of the active monitor's usable desktop area (working area) on launch (Default: 60% width × 56% height).");
            Set(StringKeys.SettingsWidthScale, "Width Scale (%):");
            Set(StringKeys.SettingsHeightScale, "Height Scale (%):");
            Set(StringKeys.SettingsResizeActive, "Resize Active Window");
            Set(StringKeys.SettingsPresets, "Presets:");
            Set(StringKeys.SettingsPresetDefault, "60% × 56% (Default)");
            Set(StringKeys.SettingsPresetCompact, "50% × 50% (Compact)");
            Set(StringKeys.SettingsPresetLarge, "75% × 70% (Large)");
            Set(StringKeys.SettingsPresetMax, "95% × 90% (Near Max)");
            Set(StringKeys.SettingsLaunchDimensions, "Calculated launch size: {0} × {1} px on current display ({2} × {3})");
            Set(StringKeys.SettingsAddShortcuts, "Add Desktop & Start Menu Shortcuts");
            Set(StringKeys.SettingsShortcutsSuccess, "Desktop and Start Menu shortcuts were created successfully.");
            Set(StringKeys.SettingsShortcutsError, "Failed to create shortcuts. Check file permissions.");
            Set(StringKeys.SettingsSecShortcuts, "System Integration & Maintenance");
            Set(StringKeys.SettingsCreateShortcuts, "Create Desktop & Start Menu Shortcuts");
            Set(StringKeys.SettingsBtnSave, "💾 Save Settings");
            Set(StringKeys.SettingsBtnReset, "↺ Reset Defaults");
            Set(StringKeys.SettingsResetConfirm, "Are you sure you want to reset all settings to defaults?");
            Set(StringKeys.SettingsSaved, "Settings saved successfully!");
            Set(StringKeys.CommonSuccess, "Success");
            Set(StringKeys.CommonWarning, "Warning");
            Set(StringKeys.CommonDefault, "Default");
            Set(StringKeys.CommonBrowse, "Browse...");

            // Logs
            Set(StringKeys.LogsTitle, "Live Engine & Synthesis Activity Logs");
            Set(StringKeys.LogsBtnCopy, "Copy Logs");
            Set(StringKeys.LogsBtnClear, "Clear Logs");
            Set(StringKeys.LogsCopied, "Logs copied to clipboard!");

            // Ebook Voicer Studio
            Set(StringKeys.EbookTitle, "Ebook Voicer & Audiobook Studio");
            Set(StringKeys.EbookSubtitle, "Convert EPUB, PDF, and TXT ebooks into natural audiobooks with artifact cleaning and chapter control");
            Set(StringKeys.EbookBtnOpen, "📂 Open Ebook...");
            Set(StringKeys.EbookNoBookLoaded, "No ebook loaded");
            Set(StringKeys.EbookSelectBookPrompt, "Open an EPUB, PDF, or TXT file to inspect chapters, clean artifacts, and synthesize speech.");
            Set(StringKeys.EbookAuthorUnknown, "Unknown Author");
            Set(StringKeys.EbookChapters, "Chapters & Sections");
            Set(StringKeys.EbookSelectAll, "Select All");
            Set(StringKeys.EbookSelectNone, "Select None");
            Set(StringKeys.EbookCleanOptions, "Artifact Cleaning Pipeline");
            Set(StringKeys.EbookOptHyphenation, "Fix Line-break Hyphens");
            Set(StringKeys.EbookOptPageNumbers, "Strip Page # & Headers");
            Set(StringKeys.EbookOptCitations, "Remove Citations [1]");
            Set(StringKeys.EbookOptUrls, "Remove Web URLs");
            Set(StringKeys.EbookOptLigatures, "Clean Ligatures & Dashes");
            Set(StringKeys.EbookOptFootnotes, "Remove Footnote Markers");
            Set(StringKeys.EbookBtnReapplyCleaning, "↺ Re-clean Text");
            Set(StringKeys.EbookBtnResetOriginal, "Reset to Original");
            Set(StringKeys.EbookTextEditorTitle, "Chapter Content Preview & Editor");
            Set(StringKeys.EbookVoiceSelected, "🎙️ Voice Selected Chapters");
            Set(StringKeys.EbookBtnCancel, "⏹ Cancel");
            Set(StringKeys.EbookBtnOpenFolder, "📂 Open Output Folder");
            Set(StringKeys.EbookOptExportIndividual, "Export individual chapter audio files");
            Set(StringKeys.EbookOptExportCombined, "Combine into single audiobook file");
            Set(StringKeys.EbookOptGenerateSubtitles, "Generate subtitles (.srt)");
            Set(StringKeys.EbookOverallProgress, "Overall Audiobook Progress");
            Set(StringKeys.EbookCurrentChapter, "Current Chapter");
            Set(StringKeys.EbookStatusReady, "Ready to synthesize.");
            Set(StringKeys.EbookStatusParsing, "Parsing and cleaning ebook...");
            Set(StringKeys.EbookStatusSynthesizing, "Synthesizing chapter {0} of {1}: '{2}'...");
            Set(StringKeys.EbookStatusComplete, "Audiobook synthesis completed successfully!");
            Set(StringKeys.EbookStatusCancelled, "Audiobook synthesis was cancelled.");
        }
    }
}
