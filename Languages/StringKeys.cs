namespace KerkenezVoice.Languages
{
    public static class StringKeys
    {
        // 1. Navigation & Sidebar
        public const string NavSynthesize = "Nav.Synthesize";
        public const string NavCustomVoices = "Nav.CustomVoices";
        public const string NavAudioFx = "Nav.AudioFx";
        public const string NavLexicon = "Nav.Lexicon";
        public const string NavSettings = "Nav.Settings";
        public const string NavLiveLogs = "Nav.LiveLogs";
        public const string NavTipExpandSidebar = "Nav.TipExpandSidebar";
        public const string NavTipCollapseSidebar = "Nav.TipCollapseSidebar";

        // 2. Main Shell & Window
        public const string AppTitle = "Shell.AppTitle";
        public const string MainShortcutsPromptTitle = "Main.ShortcutsPromptTitle";
        public const string MainShortcutsPromptDesc = "Main.ShortcutsPromptDesc";
        public const string StatusStartingUp = "Status.StartingUp";
        public const string StatusReady = "Status.Ready";
        public const string StatusReadyVoice = "Status.ReadyVoice";
        public const string StatusSynthesizing = "Status.Synthesizing";
        public const string StatusPlaying = "Status.Playing";
        public const string StatusModelsRequired = "Status.ModelsRequired";
        public const string StatusMetrics = "Status.Metrics";

        // 3. Synthesize Tab
        public const string SynthTitle = "Synth.Title";
        public const string SynthInputSource = "Synth.InputSource";
        public const string SynthInputModeText = "Synth.InputModeText";
        public const string SynthInputModeFile = "Synth.InputModeFile";
        public const string SynthDirectTextPlaceholder = "Synth.DirectTextPlaceholder";
        public const string SynthSelectFile = "Synth.SelectFile";
        public const string SynthBrowse = "Synth.Browse";
        public const string SynthSupportedDocs = "Synth.SupportedDocs";
        public const string SynthConfiguration = "Synth.Configuration";
        public const string SynthPreset = "Synth.Preset";
        public const string SynthSavePreset = "Synth.SavePreset";
        public const string SynthRefreshPresets = "Synth.RefreshPresets";
        public const string SynthLanguage = "Synth.Language";
        public const string SynthVoice = "Synth.Voice";
        public const string SynthSpeed = "Synth.Speed";
        public const string SynthPitch = "Synth.Pitch";
        public const string SynthVolume = "Synth.Volume";
        public const string SynthFormat = "Synth.Format";
        public const string SynthThreads = "Synth.Threads";
        public const string SynthCombine = "Synth.Combine";
        public const string SynthSeparate = "Synth.Separate";
        public const string SynthSubtitles = "Synth.Subtitles";
        public const string SynthNormalize = "Synth.Normalize";
        public const string SynthTrim = "Synth.Trim";
        public const string SynthApplyFx = "Synth.ApplyFx";
        public const string SynthFxPreset = "Synth.FxPreset";
        public const string SynthBtnPreview = "Synth.BtnPreview";
        public const string SynthBtnGenerate = "Synth.BtnGenerate";
        public const string SynthBtnCancel = "Synth.BtnCancel";
        public const string SynthBtnOpenFolder = "Synth.BtnOpenFolder";
        public const string SynthStatusComplete = "Synth.StatusComplete";
        public const string SynthStatusCancelled = "Synth.StatusCancelled";
        public const string SynthStatusError = "Synth.StatusError";

        // 4. Custom Voices Tab
        public const string VoiceMixTitle = "VoiceMix.Title";
        public const string VoiceMixVoiceA = "VoiceMix.VoiceA";
        public const string VoiceMixVoiceB = "VoiceMix.VoiceB";
        public const string VoiceMixOperation = "VoiceMix.Operation";
        public const string VoiceMixRatio = "VoiceMix.Ratio";
        public const string VoiceMixBtnPreview = "VoiceMix.BtnPreview";
        public const string VoiceMixNewName = "VoiceMix.NewName";
        public const string VoiceMixBtnCreate = "VoiceMix.BtnCreate";
        public const string VoiceMixListTitle = "VoiceMix.ListTitle";
        public const string VoiceMixBtnDelete = "VoiceMix.BtnDelete";
        public const string VoiceMixCreatedSuccess = "VoiceMix.CreatedSuccess";

        // 5. Audio FX Tab
        public const string FxTitle = "Fx.Title";
        public const string FxPreset = "Fx.Preset";
        public const string FxSavePreset = "Fx.SavePreset";
        public const string FxRefreshPresets = "Fx.RefreshPresets";
        public const string FxApplyMaster = "Fx.ApplyMaster";
        public const string FxDynamicsTitle = "Fx.DynamicsTitle";
        public const string FxComp = "Fx.Comp";
        public const string FxCompThreshold = "Fx.CompThreshold";
        public const string FxCompRatio = "Fx.CompRatio";
        public const string FxLimiter = "Fx.Limiter";
        public const string FxLimiterThreshold = "Fx.LimiterThreshold";
        public const string FxGain = "Fx.Gain";
        public const string FxGainDb = "Fx.GainDb";
        public const string FxEqTitle = "Fx.EqTitle";
        public const string FxBass = "Fx.Bass";
        public const string FxTreble = "Fx.Treble";
        public const string FxHpf = "Fx.Hpf";
        public const string FxLpf = "Fx.Lpf";
        public const string FxSpatialTitle = "Fx.SpatialTitle";
        public const string FxReverb = "Fx.Reverb";
        public const string FxReverbRoom = "Fx.ReverbRoom";
        public const string FxReverbWet = "Fx.ReverbWet";
        public const string FxDelay = "Fx.Delay";
        public const string FxDelayTime = "Fx.DelayTime";
        public const string FxDelayFeedback = "Fx.DelayFeedback";
        public const string FxDelayMix = "Fx.DelayMix";
        public const string FxModTitle = "Fx.ModTitle";
        public const string FxChorus = "Fx.Chorus";
        public const string FxChorusRate = "Fx.ChorusRate";
        public const string FxPhaser = "Fx.Phaser";
        public const string FxPhaserRate = "Fx.PhaserRate";
        public const string FxDistortion = "Fx.Distortion";
        public const string FxDistortionDrive = "Fx.DistortionDrive";
        public const string FxClipping = "Fx.Clipping";
        public const string FxPitchShift = "Fx.PitchShift";
        public const string FxBitcrush = "Fx.Bitcrush";
        public const string FxGsm = "Fx.Gsm";

        // 6. Lexicon Tab
        public const string LexTitle = "Lex.Title";
        public const string LexOriginal = "Lex.Original";
        public const string LexReplacement = "Lex.Replacement";
        public const string LexBtnAdd = "Lex.BtnAdd";
        public const string LexListTitle = "Lex.ListTitle";
        public const string LexNote = "Lex.Note";
        public const string LexBtnDelete = "Lex.BtnDelete";

        // 7. Settings Tab
        public const string SettingsTitle = "Settings.Title";
        public const string SettingsSecLanguage = "Settings.SecLanguage";
        public const string SettingsLanguageDesc = "Settings.LanguageDesc";
        public const string SettingsSecModel = "Settings.SecModel";
        public const string SettingsModelPath = "Settings.ModelPath";
        public const string SettingsBtnDownloadModels = "Settings.BtnDownloadModels";
        public const string SettingsModelsReady = "Settings.ModelsReady";
        public const string SettingsSecVoiceDefaults = "Settings.SecVoiceDefaults";
        public const string SettingsDefaultVoice = "Settings.DefaultVoice";
        public const string SettingsDefaultFormat = "Settings.DefaultFormat";
        public const string SettingsSecThreads = "Settings.SecThreads";
        public const string SettingsThreadsDesc = "Settings.ThreadsDesc";
        public const string SettingsSecOutput = "Settings.SecOutput";
        public const string SettingsOutDir = "Settings.OutDir";
        public const string SettingsBtnBrowseOutDir = "Settings.BtnBrowseOutDir";
        public const string SettingsSecUi = "Settings.SecUi";
        public const string SettingsCollapseSidebar = "Settings.CollapseSidebar";
        public const string SettingsWindowScale = "Settings.WindowScale";
        public const string SettingsScalingHeader = "Settings.ScalingHeader";
        public const string SettingsScalingDesc = "Settings.ScalingDesc";
        public const string SettingsWidthScale = "Settings.WidthScale";
        public const string SettingsHeightScale = "Settings.HeightScale";
        public const string SettingsResizeActive = "Settings.ResizeActive";
        public const string SettingsPresets = "Settings.Presets";
        public const string SettingsPresetDefault = "Settings.PresetDefault";
        public const string SettingsPresetCompact = "Settings.PresetCompact";
        public const string SettingsPresetLarge = "Settings.PresetLarge";
        public const string SettingsPresetMax = "Settings.PresetMax";
        public const string SettingsLaunchDimensions = "Settings.LaunchDimensions";
        public const string SettingsAddShortcuts = "Settings.AddShortcuts";
        public const string SettingsShortcutsSuccess = "Settings.ShortcutsSuccess";
        public const string SettingsShortcutsError = "Settings.ShortcutsError";
        public const string SettingsSecShortcuts = "Settings.SecShortcuts";
        public const string SettingsCreateShortcuts = "Settings.CreateShortcuts";
        public const string SettingsBtnSave = "Settings.BtnSave";
        public const string SettingsBtnReset = "Settings.BtnReset";
        public const string SettingsResetConfirm = "Settings.ResetConfirm";
        public const string SettingsSaved = "Settings.Saved";
        public const string CommonSuccess = "Common.Success";
        public const string CommonWarning = "Common.Warning";
        public const string CommonDefault = "Common.Default";
        public const string CommonBrowse = "Common.Browse";

        // 8. Logs Tab
        public const string LogsTitle = "Logs.Title";
        public const string LogsBtnCopy = "Logs.BtnCopy";
        public const string LogsBtnClear = "Logs.BtnClear";
        public const string LogsCopied = "Logs.Copied";
    }
}
