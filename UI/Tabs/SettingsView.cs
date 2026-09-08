using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;
using KerkenezVoice.UI.Dialogs;

namespace KerkenezVoice.UI.Tabs
{
    public class SettingsView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly ModelManagerService _modelManager;
        private readonly KokoroEngineService _engineService;
        private readonly IProgress<string> _logger;

        public event Action? SettingsSaved;

        private bool _isLoadingSettings;
        private FlowLayoutPanel _mainFlow = null!;

        // 1. Model & Engine Controls
        private Label _lblSecModel = null!;
        private Label _lblModelStatusBadge = null!;
        private Label _lblModelPathHeader = null!;
        private Label _lblModelPathDesc = null!;
        private Button _btnVerifyModel = null!;
        private Button _btnDownloadModels = null!;
        private Button _btnOpenModelsFolder = null!;
        private Label _lblThreadsHeader = null!;
        private Label _lblThreadsDesc = null!;
        private NumericUpDown _numThreads = null!;
        private CheckBox _chkCaching = null!;

        // 2. Voice & Synthesis Defaults Controls
        private Label _lblSecVoiceDefaults = null!;
        private Label _lblVoiceDefaultsDesc = null!;
        private Label _lblDefaultVoice = null!;
        private ComboBox _cboDefaultVoice = null!;
        private Label _lblDefaultFormat = null!;
        private ComboBox _cboDefaultFormat = null!;
        private CheckBox _chkCombine = null!;
        private CheckBox _chkSeparate = null!;
        private CheckBox _chkSubtitles = null!;
        private CheckBox _chkNormalize = null!;
        private CheckBox _chkTrim = null!;

        // 3. Output Directory Controls
        private Label _lblSecOutput = null!;
        private Label _lblOutDirHeader = null!;
        private Label _lblOutDirDesc = null!;
        private TextBox _txtOutDir = null!;
        private Button _btnBrowseOutDir = null!;
        private Button _btnResetOutDir = null!;
        private Button _btnOpenOutDir = null!;

        // 4. Language & Region Controls
        private Label _lblSecLanguage = null!;
        private Label _lblLanguageDesc = null!;
        private ComboBox _cboLanguage = null!;

        // 5. Interface & Layout Controls
        private Label _lblSecUi = null!;
        private CheckBox _chkCollapseSidebarByDefault = null!;
        private Label _lblEbookSplitter = null!;
        private NumericUpDown _numEbookSplitter = null!;
        private Label _lblScalingHeader = null!;
        private Label _lblScalingDesc = null!;
        private Label _lblWidth = null!;
        private Label _lblHeight = null!;
        private NumericUpDown _numWindowWidthScale = null!;
        private NumericUpDown _numWindowHeightScale = null!;
        private Button _btnApplyWindowSizeNow = null!;
        private Label _lblLayoutPresets = null!;
        private Button _btnPresetDefault = null!;
        private Button _btnPresetCompact = null!;
        private Button _btnPresetLarge = null!;
        private Button _btnPresetMax = null!;
        private Label _lblScalePreview = null!;
        private Button _btnCreateShortcuts = null!;

        // 6. Storage & Maintenance Controls
        private Label _lblSecStorage = null!;
        private Label _lblStorageDesc = null!;
        private Button _btnOpenAppData = null!;
        private Button _btnClearCache = null!;
        private Button _btnRegisterUninstall = null!;

        // Bottom Action Buttons
        private Button _btnSave = null!;
        private Button _btnReset = null!;

        private class LanguageComboItem
        {
            public ILanguage Language { get; }
            public LanguageComboItem(ILanguage language) => Language = language;
            public override string ToString() => $"{Language.FlagEmoji}  {Language.Name} ({Language.Code})";
        }

        public SettingsView(
            ConfigService configService,
            ModelManagerService modelManager,
            KokoroEngineService engineService,
            IProgress<string> logger)
        {
            _configService = configService;
            _modelManager = modelManager;
            _engineService = engineService;
            _logger = logger;

            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            LoadSettings();
            ApplyLocalization();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(24, 18, 24, 24)
            };

            _mainFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            const int ContentW = 760;

            // ==================== 1. Model & Engine Section ====================
            var pnlModelCard = CreateCardPanel(ContentW);
            _lblSecModel = CreateSectionHeader("🧠  Kokoro-82M TTS Model & Engine");

            _lblModelStatusBadge = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6)
            };

            _lblModelPathHeader = new Label
            {
                Text = "Model Storage Location:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 2, 0, 2)
            };

            _lblModelPathDesc = new Label
            {
                Text = $"Weights Directory: {_modelManager.ModelsDirectory}",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            var rowModelButtons = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            _btnVerifyModel = new Button
            {
                Text = "🔄  Verify & Load Engine",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 8, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnVerifyModel.Click += async (s, e) =>
            {
                _btnVerifyModel.Enabled = false;
                _lblModelStatusBadge.Text = "⏳ Loading Kokoro TTS engine into memory...";
                _lblModelStatusBadge.ForeColor = Color.FromArgb(0, 102, 204);
                bool ok = await _engineService.InitializeAsync();
                UpdateModelStatusBadge();
                _btnVerifyModel.Enabled = true;
            };

            _btnDownloadModels = new Button
            {
                Text = "📥  Download / Update Weights...",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 8, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnDownloadModels.Click += async (s, e) =>
            {
                using var dlg = new ModelDownloadDialog(_modelManager);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await _engineService.InitializeAsync();
                    UpdateModelStatusBadge();
                }
            };

            _btnOpenModelsFolder = new Button
            {
                Text = "📂  Open Models Folder",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 0, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnOpenModelsFolder.Click += (s, e) =>
            {
                if (Directory.Exists(_modelManager.ModelsDirectory))
                {
                    Process.Start(new ProcessStartInfo { FileName = _modelManager.ModelsDirectory, UseShellExecute = true });
                }
            };

            rowModelButtons.Controls.Add(_btnVerifyModel);
            rowModelButtons.Controls.Add(_btnDownloadModels);
            rowModelButtons.Controls.Add(_btnOpenModelsFolder);

            _lblThreadsHeader = new Label
            {
                Text = "Parallel Synthesis Worker Threads:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 4, 0, 2)
            };

            _lblThreadsDesc = new Label
            {
                Text = "Number of parallel worker threads used to process speech chunks simultaneously (speeds up long documents; uses more RAM).",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 6)
            };

            var rowThreads = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            var lblNumThreads = new Label { Text = "Threads:", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
            _numThreads = new NumericUpDown
            {
                Width = 75,
                Minimum = 1,
                Maximum = 16,
                Value = 1,
                Margin = new Padding(0, 0, 16, 0)
            };

            _chkCaching = new CheckBox
            {
                Text = "Enable in-memory audio chunk caching for repeated sentences",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 2, 0, 0)
            };

            rowThreads.Controls.Add(lblNumThreads);
            rowThreads.Controls.Add(_numThreads);
            rowThreads.Controls.Add(_chkCaching);

            pnlModelCard.Controls.Add(_lblSecModel);
            pnlModelCard.Controls.Add(_lblModelStatusBadge);
            pnlModelCard.Controls.Add(_lblModelPathHeader);
            pnlModelCard.Controls.Add(_lblModelPathDesc);
            pnlModelCard.Controls.Add(rowModelButtons);
            pnlModelCard.Controls.Add(_lblThreadsHeader);
            pnlModelCard.Controls.Add(_lblThreadsDesc);
            pnlModelCard.Controls.Add(rowThreads);

            // ==================== 2. Voice & Synthesis Defaults Section ====================
            var pnlVoiceCard = CreateCardPanel(ContentW);
            _lblSecVoiceDefaults = CreateSectionHeader("🎙️  Synthesis & Voice Defaults");

            _lblVoiceDefaultsDesc = new Label
            {
                Text = "Default parameters applied when creating new synthesis sessions or opening documents.",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            var rowVoiceDefaults = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            _lblDefaultVoice = new Label { Text = "Default Voice:", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
            _cboDefaultVoice = new ComboBox
            {
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 20, 0)
            };
            _cboDefaultVoice.SelectedIndexChanged += (s, e) =>
            {
                if (_isLoadingSettings) return;
                if (_cboDefaultVoice.SelectedItem != null)
                {
                    string chosenVoice = _cboDefaultVoice.SelectedItem.ToString() ?? "af_heart";
                    _configService.Settings.Voice = chosenVoice;
                    _configService.SaveConfig();
                    _modelManager.UpdateDefaultPresetVoice(chosenVoice);
                    SettingsSaved?.Invoke();
                }
            };

            _lblDefaultFormat = new Label { Text = "Export Format:", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
            _cboDefaultFormat = new ComboBox
            {
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 0)
            };
            _cboDefaultFormat.Items.AddRange(new object[] { "wav", "mp3", "flac", "ogg" });
            _cboDefaultFormat.SelectedIndex = 0;

            rowVoiceDefaults.Controls.Add(_lblDefaultVoice);
            rowVoiceDefaults.Controls.Add(_cboDefaultVoice);
            rowVoiceDefaults.Controls.Add(_lblDefaultFormat);
            rowVoiceDefaults.Controls.Add(_cboDefaultFormat);

            var flowVoiceCheckboxes = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            _chkCombine = new CheckBox { Text = "Combine segments into single master file", Checked = true, AutoSize = true, Margin = new Padding(0, 0, 16, 6) };
            _chkSeparate = new CheckBox { Text = "Save individual segment files", Checked = false, AutoSize = true, Margin = new Padding(0, 0, 16, 6) };
            _chkSubtitles = new CheckBox { Text = "Export synchronized .srt subtitles", Checked = false, AutoSize = true, Margin = new Padding(0, 0, 16, 6) };
            _chkNormalize = new CheckBox { Text = "Normalize output amplitude (0 dBFS)", Checked = false, AutoSize = true, Margin = new Padding(0, 0, 16, 6) };
            _chkTrim = new CheckBox { Text = "Trim leading and trailing silence", Checked = false, AutoSize = true, Margin = new Padding(0, 0, 16, 6) };

            flowVoiceCheckboxes.Controls.AddRange(new Control[] { _chkCombine, _chkSeparate, _chkSubtitles, _chkNormalize, _chkTrim });

            pnlVoiceCard.Controls.Add(_lblSecVoiceDefaults);
            pnlVoiceCard.Controls.Add(_lblVoiceDefaultsDesc);
            pnlVoiceCard.Controls.Add(rowVoiceDefaults);
            pnlVoiceCard.Controls.Add(flowVoiceCheckboxes);

            // ==================== 3. Output Directory Section ====================
            var pnlOutputCard = CreateCardPanel(ContentW);
            _lblSecOutput = CreateSectionHeader("📁  Default Output Folder");

            _lblOutDirHeader = new Label
            {
                Text = "Output Audio Directory:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 2)
            };

            _lblOutDirDesc = new Label
            {
                Text = "Location where synthesized speech files, chunks, and subtitles will be saved by default.",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            var rowOutDirControls = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 6)
            };

            _txtOutDir = new TextBox
            {
                Width = 340,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 2, 8, 4)
            };

            _btnBrowseOutDir = new Button
            {
                Text = "Browse...",
                Width = 85,
                Height = 26,
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 1, 6, 4)
            };
            _btnBrowseOutDir.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog
                {
                    Description = "Select Default Audio Output Directory",
                    UseDescriptionForTitle = true,
                    SelectedPath = Directory.Exists(_txtOutDir.Text.Trim())
                        ? _txtOutDir.Text.Trim()
                        : _configService.Settings.GetEffectiveOutputDirectory()
                };
                if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    _txtOutDir.Text = fbd.SelectedPath;
                }
            };

            _btnResetOutDir = new Button
            {
                Text = "Default",
                Width = 75,
                Height = 26,
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 1, 6, 4)
            };
            _btnResetOutDir.Click += (s, e) =>
            {
                _txtOutDir.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            };

            _btnOpenOutDir = new Button
            {
                Text = "📂 Open Output Folder",
                AutoSize = true,
                Height = 26,
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 1, 0, 4)
            };
            _btnOpenOutDir.Click += (s, e) =>
            {
                string path = Directory.Exists(_txtOutDir.Text.Trim())
                    ? _txtOutDir.Text.Trim()
                    : _configService.Settings.GetEffectiveOutputDirectory();
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            };

            rowOutDirControls.Controls.Add(_txtOutDir);
            rowOutDirControls.Controls.Add(_btnBrowseOutDir);
            rowOutDirControls.Controls.Add(_btnResetOutDir);
            rowOutDirControls.Controls.Add(_btnOpenOutDir);

            pnlOutputCard.Controls.Add(_lblSecOutput);
            pnlOutputCard.Controls.Add(_lblOutDirHeader);
            pnlOutputCard.Controls.Add(_lblOutDirDesc);
            pnlOutputCard.Controls.Add(rowOutDirControls);

            // ==================== 4. Language & Region Section ====================
            var pnlLangCard = CreateCardPanel(ContentW);
            _lblSecLanguage = CreateSectionHeader(Lang.T(StringKeys.SettingsSecLanguage));

            _lblLanguageDesc = new Label
            {
                Text = Lang.T(StringKeys.SettingsLanguageDesc),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            _cboLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280,
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 0, 4)
            };

            PopulateLanguageDropdown();

            _cboLanguage.SelectedIndexChanged += (s, e) =>
            {
                if (_cboLanguage.SelectedItem is LanguageComboItem item)
                {
                    LanguageManager.Instance.SetLanguage(item.Language.Code);
                }
            };

            pnlLangCard.Controls.Add(_lblSecLanguage);
            pnlLangCard.Controls.Add(_lblLanguageDesc);
            pnlLangCard.Controls.Add(_cboLanguage);

            // ==================== 5. Interface & Layout Section ====================
            var pnlUiCard = CreateCardPanel(ContentW);
            _lblSecUi = CreateSectionHeader("🖥️  Interface & Layout");

            _chkCollapseSidebarByDefault = new CheckBox
            {
                Text = "Start with left sidebar collapsed by default (compact icon rail on launch)",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, 8),
                Font = new Font("Segoe UI", 9F)
            };

            var rowEbookSplitter = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 10)
            };

            _lblEbookSplitter = new Label
            {
                Text = "Ebook Studio Chapter List Width (px):",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 4, 10, 0)
            };

            _numEbookSplitter = new NumericUpDown
            {
                Width = 90,
                Minimum = 250,
                Maximum = 750,
                Increment = 10,
                Value = 380,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 0)
            };

            rowEbookSplitter.Controls.Add(_lblEbookSplitter);
            rowEbookSplitter.Controls.Add(_numEbookSplitter);

            _lblScalingHeader = new Label
            {
                Text = "Default Launch Window Scaling (Relative to Display):",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 4, 0, 2)
            };

            _lblScalingDesc = new Label
            {
                Text = "Target proportion of the active monitor's usable desktop area (working area) on launch (Default: 60% width × 56% height).",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            var rowScaleControls = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _lblWidth = new Label { Text = "Width Scale (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
            _numWindowWidthScale = new NumericUpDown
            {
                Width = 80,
                DecimalPlaces = 1,
                Minimum = 30.0M,
                Maximum = 100.0M,
                Increment = 1.0M,
                Value = 60.0M,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 16, 0)
            };
            _numWindowWidthScale.ValueChanged += (s, e) => UpdateScalePreview();

            _lblHeight = new Label { Text = "Height Scale (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
            _numWindowHeightScale = new NumericUpDown
            {
                Width = 80,
                DecimalPlaces = 1,
                Minimum = 30.0M,
                Maximum = 100.0M,
                Increment = 0.5M,
                Value = 56.0M,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 14, 0)
            };
            _numWindowHeightScale.ValueChanged += (s, e) => UpdateScalePreview();

            _btnApplyWindowSizeNow = new Button
            {
                Text = "⚡ Resize Active Window",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 0, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnApplyWindowSizeNow.Click += OnApplyWindowSizeNowClick;

            rowScaleControls.Controls.Add(_lblWidth);
            rowScaleControls.Controls.Add(_numWindowWidthScale);
            rowScaleControls.Controls.Add(_lblHeight);
            rowScaleControls.Controls.Add(_numWindowHeightScale);
            rowScaleControls.Controls.Add(_btnApplyWindowSizeNow);

            var rowPresets = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _lblLayoutPresets = new Label { Text = "Presets:", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80) };
            _btnPresetDefault = CreatePresetChip("60% × 56% (Default)", 60.0M, 56.0M);
            _btnPresetCompact = CreatePresetChip("50% × 50% (Compact)", 50.0M, 50.0M);
            _btnPresetLarge = CreatePresetChip("75% × 70% (Large)", 75.0M, 70.0M);
            _btnPresetMax = CreatePresetChip("95% × 90% (Near Max)", 95.0M, 90.0M);

            rowPresets.Controls.Add(_lblLayoutPresets);
            rowPresets.Controls.Add(_btnPresetDefault);
            rowPresets.Controls.Add(_btnPresetCompact);
            rowPresets.Controls.Add(_btnPresetLarge);
            rowPresets.Controls.Add(_btnPresetMax);

            _lblScalePreview = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(70, 70, 70),
                Margin = new Padding(0, 2, 0, 2)
            };

            _btnCreateShortcuts = new Button
            {
                Text = "📌  Add Desktop & Start Menu Shortcuts",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(0, 8, 0, 2),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnCreateShortcuts.Click += (s, e) =>
            {
                bool ok = ShortcutService.CreateShortcuts();
                if (ok)
                {
                    MessageBox.Show(Lang.T(StringKeys.SettingsShortcutsSuccess), Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(Lang.T(StringKeys.SettingsShortcutsError), Lang.T(StringKeys.CommonWarning), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            pnlUiCard.Controls.Add(_lblSecUi);
            pnlUiCard.Controls.Add(_chkCollapseSidebarByDefault);
            pnlUiCard.Controls.Add(rowEbookSplitter);
            pnlUiCard.Controls.Add(_lblScalingHeader);
            pnlUiCard.Controls.Add(_lblScalingDesc);
            pnlUiCard.Controls.Add(rowScaleControls);
            pnlUiCard.Controls.Add(rowPresets);
            pnlUiCard.Controls.Add(_lblScalePreview);
            pnlUiCard.Controls.Add(_btnCreateShortcuts);

            // ==================== 6. Storage & Maintenance Section ====================
            var pnlStorageCard = CreateCardPanel(ContentW);
            _lblSecStorage = CreateSectionHeader("🛠️  Storage & Maintenance");

            _lblStorageDesc = new Label
            {
                Text = "Configuration is stored in %APPDATA%\\Kerkenez\\voice. Model weights, custom voices, and synthesis cache are stored in %LOCALAPPDATA%\\Programs\\Kerkenez\\voice.",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            var rowStorageButtons = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 4)
            };

            _btnOpenAppData = new Button
            {
                Text = "📂  Open Config Folder (%APPDATA%)",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 8, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnOpenAppData.Click += (s, e) =>
            {
                if (Directory.Exists(ConfigService.AppDataFolder))
                {
                    Process.Start(new ProcessStartInfo { FileName = ConfigService.AppDataFolder, UseShellExecute = true });
                }
            };

            var btnOpenProgramsFolder = new Button
            {
                Text = "📂  Open Models & Data (%LOCALAPPDATA%)",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 8, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnOpenProgramsFolder.Click += (s, e) =>
            {
                if (Directory.Exists(ConfigService.LocalProgramsFolder))
                {
                    Process.Start(new ProcessStartInfo { FileName = ConfigService.LocalProgramsFolder, UseShellExecute = true });
                }
            };

            _btnClearCache = new Button
            {
                Text = "🗑️  Clear Audio Cache",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 8, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnClearCache.Click += (s, e) =>
            {
                try
                {
                    int deletedCount = 0;
                    string[] cacheFolders = { ConfigService.CacheFolder, Path.Combine(ConfigService.AppDataFolder, "cache") };
                    foreach (var cache in cacheFolders)
                    {
                        if (Directory.Exists(cache))
                        {
                            foreach (var f in Directory.GetFiles(cache))
                            {
                                try { File.Delete(f); deletedCount++; } catch { }
                            }
                        }
                    }
                    MessageBox.Show($"Synthesis audio cache cleared ({deletedCount} files removed).", Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear cache: {ex.Message}", Lang.T(StringKeys.CommonWarning), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            _btnRegisterUninstall = new Button
            {
                Text = "⚙️  Register / Refresh Uninstaller",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 0, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnRegisterUninstall.Click += (s, e) =>
            {
                try
                {
                    UninstallRegistrationService.RegisterOrUpdate();
                    MessageBox.Show("Windows Add/Remove Programs entry was registered successfully.", Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Registration error: {ex.Message}", Lang.T(StringKeys.CommonWarning), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            rowStorageButtons.Controls.Add(_btnOpenAppData);
            rowStorageButtons.Controls.Add(btnOpenProgramsFolder);
            rowStorageButtons.Controls.Add(_btnClearCache);
            rowStorageButtons.Controls.Add(_btnRegisterUninstall);

            pnlStorageCard.Controls.Add(_lblSecStorage);
            pnlStorageCard.Controls.Add(_lblStorageDesc);
            pnlStorageCard.Controls.Add(rowStorageButtons);

            // ==================== 7. Bottom Action Buttons ====================
            var pnlButtons = new FlowLayoutPanel
            {
                Width = ContentW,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 20)
            };

            _btnSave = new Button
            {
                Text = "💾 Save Settings",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 6),
                Margin = new Padding(0, 0, 10, 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.Click += OnSaveSettingsClick;

            _btnReset = new Button
            {
                Text = "↺ Reset Defaults",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 6),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnReset.Click += OnResetDefaultsClick;

            var lblVersionInfo = new Label
            {
                Text = $"v{UninstallRegistrationService.CurrentVersion}",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(128, 128, 128),
                AutoSize = true,
                Margin = new Padding(12, 8, 0, 0)
            };

            pnlButtons.Controls.Add(_btnSave);
            pnlButtons.Controls.Add(_btnReset);
            pnlButtons.Controls.Add(lblVersionInfo);

            // Assemble into main flow
            _mainFlow.Controls.Add(pnlModelCard);
            _mainFlow.Controls.Add(pnlVoiceCard);
            _mainFlow.Controls.Add(pnlOutputCard);
            _mainFlow.Controls.Add(pnlLangCard);
            _mainFlow.Controls.Add(pnlUiCard);
            _mainFlow.Controls.Add(pnlStorageCard);
            _mainFlow.Controls.Add(pnlButtons);

            scrollPanel.Controls.Add(_mainFlow);
            this.Controls.Add(scrollPanel);
        }

        private static FlowLayoutPanel CreateCardPanel(int width)
        {
            var pnl = new FlowLayoutPanel
            {
                Width = width,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 0, 0, 14)
            };

            pnl.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            return pnl;
        }

        private static Label CreateSectionHeader(string text)
        {
            return new Label
            {
                Text = text,
                UseMnemonic = false,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20),
                Margin = new Padding(0, 0, 0, 10)
            };
        }

        private Button CreatePresetChip(string text, decimal widthVal, decimal heightVal)
        {
            var btn = new Button
            {
                Text = text,
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 2, 6, 2),
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.25F)
            };
            btn.Click += (s, e) =>
            {
                _numWindowWidthScale.Value = widthVal;
                _numWindowHeightScale.Value = heightVal;
            };
            return btn;
        }

        private void UpdateScalePreview()
        {
            try
            {
                if (_lblScalePreview == null || _numWindowWidthScale == null || _numWindowHeightScale == null) return;
                var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
                var wa = screen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));
                double wScale = (double)_numWindowWidthScale.Value / 100.0;
                double hScale = (double)_numWindowHeightScale.Value / 100.0;
                int targetW = (int)Math.Round(wa.Width * wScale);
                int targetH = (int)Math.Round(wa.Height * hScale);
                _lblScalePreview.Text = Lang.Format(StringKeys.SettingsLaunchDimensions, targetW, targetH, wa.Width, wa.Height);
            }
            catch { }
        }

        private void OnApplyWindowSizeNowClick(object? sender, EventArgs e)
        {
            try
            {
                var mainForm = this.FindForm();
                if (mainForm != null)
                {
                    var screen = Screen.FromControl(mainForm) ?? Screen.PrimaryScreen;
                    var wa = screen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));
                    double wScale = (double)_numWindowWidthScale.Value / 100.0;
                    double hScale = (double)_numWindowHeightScale.Value / 100.0;
                    int targetW = (int)Math.Round(wa.Width * wScale);
                    int targetH = (int)Math.Round(wa.Height * hScale);
                    int minW = Math.Min(960, wa.Width);
                    int minH = Math.Min(600, wa.Height);
                    targetW = Math.Clamp(targetW, minW, wa.Width);
                    targetH = Math.Clamp(targetH, minH, wa.Height);
                    mainForm.Size = new Size(targetW, targetH);
                    mainForm.Location = new Point(
                        wa.Left + Math.Max(0, (wa.Width - targetW) / 2),
                        wa.Top + Math.Max(0, (wa.Height - targetH) / 2)
                    );
                    _configService.Settings.WindowWidth = targetW;
                    _configService.Settings.WindowHeight = targetH;
                    _configService.Settings.WindowWidthScale = wScale;
                    _configService.Settings.WindowHeightScale = hScale;
                    _configService.SaveConfig();
                }
            }
            catch { }
        }

        private void PopulateLanguageDropdown()
        {
            _cboLanguage.Items.Clear();
            foreach (var lang in LanguageManager.Instance.AvailableLanguages)
            {
                _cboLanguage.Items.Add(new LanguageComboItem(lang));
            }
        }

        public void UpdateModelStatusBadge()
        {
            if (this.IsDisposed || _lblModelStatusBadge == null) return;

            if (_modelManager.AreModelsPresent())
            {
                _lblModelStatusBadge.Text = "✅  Models Ready (kokoro-v1.0.onnx & voices-v1.0.bin present)";
                _lblModelStatusBadge.ForeColor = Color.FromArgb(20, 140, 50);
            }
            else
            {
                _lblModelStatusBadge.Text = "⚠️  Model Weights Missing (Download required to synthesize speech)";
                _lblModelStatusBadge.ForeColor = Color.FromArgb(200, 30, 30);
            }
        }

        public void LoadSettings()
        {
            _isLoadingSettings = true;
            try
            {
                var s = _configService.Settings;

                UpdateModelStatusBadge();

                _numThreads.Value = Math.Clamp(s.NumThreads, 1, 16);
                _chkCaching.Checked = s.Caching;

                // Load Voices
                _cboDefaultVoice.Items.Clear();
                if (_modelManager.AreModelsPresent())
                {
                    try
                    {
                        var voices = _modelManager.LoadVoices();
                        var names = new System.Collections.Generic.HashSet<string>(voices.Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
                        foreach (var n in names.OrderBy(x => x)) _cboDefaultVoice.Items.Add(n);
                    }
                    catch { }
                }
                int vIdx = GenerateView.FindVoiceIndex(_cboDefaultVoice, s.Voice);
                if (vIdx >= 0)
                {
                    _cboDefaultVoice.SelectedIndex = vIdx;
                }
                else if (_cboDefaultVoice.Items.Count > 0)
                {
                    _cboDefaultVoice.SelectedIndex = 0;
                }

                int fIdx = _cboDefaultFormat.Items.IndexOf(s.Format.ToLowerInvariant());
                _cboDefaultFormat.SelectedIndex = (fIdx >= 0) ? fIdx : 0;

                _chkCombine.Checked = s.Combine;
                _chkSeparate.Checked = s.Separate;
                _chkSubtitles.Checked = s.ExportSubtitles;
                _chkNormalize.Checked = s.Normalize;
                _chkTrim.Checked = s.Trim;

                _txtOutDir.Text = s.GetEffectiveOutputDirectory();

                // Language
                string curLang = s.Language ?? "en";
                for (int i = 0; i < _cboLanguage.Items.Count; i++)
                {
                    if (_cboLanguage.Items[i] is LanguageComboItem item && item.Language.Code.Equals(curLang, StringComparison.OrdinalIgnoreCase))
                    {
                        _cboLanguage.SelectedIndex = i;
                        break;
                    }
                }

                // Layout & Scaling
                _chkCollapseSidebarByDefault.Checked = s.CollapseSidebarByDefault;
                _numEbookSplitter.Value = Math.Clamp(s.EbookSplitterDistance > 0 ? s.EbookSplitterDistance : 380, 250, 750);
                decimal wScale = (decimal)(s.WindowWidthScale > 0.1 && s.WindowWidthScale <= 1.0 ? s.WindowWidthScale * 100.0 : 60.0);
                decimal hScale = (decimal)(s.WindowHeightScale > 0.1 && s.WindowHeightScale <= 1.0 ? s.WindowHeightScale * 100.0 : 56.0);
                _numWindowWidthScale.Value = Math.Max(_numWindowWidthScale.Minimum, Math.Min(_numWindowWidthScale.Maximum, wScale));
                _numWindowHeightScale.Value = Math.Max(_numWindowHeightScale.Minimum, Math.Min(_numWindowHeightScale.Maximum, hScale));
                UpdateScalePreview();
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        public void ApplyLocalization()
        {
            if (this.IsDisposed) return;

            if (_lblSecModel != null) _lblSecModel.Text = "🧠  " + Lang.T(StringKeys.SettingsSecModel);
            if (_lblModelPathHeader != null) _lblModelPathHeader.Text = Lang.T(StringKeys.SettingsModelPath);
            if (_btnDownloadModels != null) _btnDownloadModels.Text = "📥  " + Lang.T(StringKeys.SettingsBtnDownloadModels);
            if (_lblThreadsHeader != null) _lblThreadsHeader.Text = Lang.T(StringKeys.SettingsSecThreads);
            if (_lblThreadsDesc != null) _lblThreadsDesc.Text = Lang.T(StringKeys.SettingsThreadsDesc);

            if (_lblSecVoiceDefaults != null) _lblSecVoiceDefaults.Text = "🎙️  " + Lang.T(StringKeys.SettingsSecVoiceDefaults);
            if (_lblDefaultVoice != null) _lblDefaultVoice.Text = Lang.T(StringKeys.SettingsDefaultVoice);
            if (_lblDefaultFormat != null) _lblDefaultFormat.Text = Lang.T(StringKeys.SettingsDefaultFormat);

            if (_lblSecOutput != null) _lblSecOutput.Text = "📁  " + Lang.T(StringKeys.SettingsSecOutput);
            if (_lblOutDirHeader != null) _lblOutDirHeader.Text = Lang.T(StringKeys.SettingsOutDir);
            if (_btnBrowseOutDir != null) _btnBrowseOutDir.Text = Lang.T(StringKeys.CommonBrowse);
            if (_btnResetOutDir != null) _btnResetOutDir.Text = Lang.T(StringKeys.CommonDefault);

            if (_lblSecLanguage != null) _lblSecLanguage.Text = "🌐  " + Lang.T(StringKeys.SettingsSecLanguage);
            if (_lblLanguageDesc != null) _lblLanguageDesc.Text = Lang.T(StringKeys.SettingsLanguageDesc);

            if (_lblSecUi != null) _lblSecUi.Text = Lang.T(StringKeys.SettingsSecUi);
            if (_chkCollapseSidebarByDefault != null) _chkCollapseSidebarByDefault.Text = Lang.T(StringKeys.SettingsCollapseSidebar);
            if (_lblEbookSplitter != null) _lblEbookSplitter.Text = Lang.T(StringKeys.SettingsEbookSplitter);
            if (_lblScalingHeader != null) _lblScalingHeader.Text = Lang.T(StringKeys.SettingsScalingHeader);
            if (_lblScalingDesc != null) _lblScalingDesc.Text = Lang.T(StringKeys.SettingsScalingDesc);
            if (_lblWidth != null) _lblWidth.Text = Lang.T(StringKeys.SettingsWidthScale);
            if (_lblHeight != null) _lblHeight.Text = Lang.T(StringKeys.SettingsHeightScale);
            if (_btnApplyWindowSizeNow != null) _btnApplyWindowSizeNow.Text = "⚡ " + Lang.T(StringKeys.SettingsResizeActive);
            if (_lblLayoutPresets != null) _lblLayoutPresets.Text = Lang.T(StringKeys.SettingsPresets);
            if (_btnPresetDefault != null) _btnPresetDefault.Text = Lang.T(StringKeys.SettingsPresetDefault);
            if (_btnPresetCompact != null) _btnPresetCompact.Text = Lang.T(StringKeys.SettingsPresetCompact);
            if (_btnPresetLarge != null) _btnPresetLarge.Text = Lang.T(StringKeys.SettingsPresetLarge);
            if (_btnPresetMax != null) _btnPresetMax.Text = Lang.T(StringKeys.SettingsPresetMax);
            if (_btnCreateShortcuts != null) _btnCreateShortcuts.Text = "📌  " + Lang.T(StringKeys.SettingsAddShortcuts);

            if (_lblSecStorage != null) _lblSecStorage.Text = "🛠️  " + Lang.T(StringKeys.SettingsSecShortcuts);
            if (_btnSave != null) _btnSave.Text = "💾 " + Lang.T(StringKeys.SettingsBtnSave);
            if (_btnReset != null) _btnReset.Text = Lang.T(StringKeys.SettingsBtnReset);

            UpdateModelStatusBadge();
            UpdateScalePreview();
        }

        private void SaveCurrentValuesToConfig()
        {
            var s = _configService.Settings;

            s.NumThreads = (int)_numThreads.Value;
            s.Caching = _chkCaching.Checked;

            if (_cboDefaultVoice.SelectedItem != null)
            {
                string chosenVoice = _cboDefaultVoice.SelectedItem.ToString() ?? s.Voice;
                s.Voice = chosenVoice;
                _modelManager.UpdateDefaultPresetVoice(chosenVoice);
            }
            if (_cboDefaultFormat.SelectedItem != null)
            {
                s.Format = _cboDefaultFormat.SelectedItem.ToString() ?? "wav";
            }

            s.Combine = _chkCombine.Checked;
            s.Separate = _chkSeparate.Checked;
            s.ExportSubtitles = _chkSubtitles.Checked;
            s.Normalize = _chkNormalize.Checked;
            s.Trim = _chkTrim.Checked;

            s.OutDir = _txtOutDir.Text.Trim();

            s.CollapseSidebarByDefault = _chkCollapseSidebarByDefault.Checked;
            s.EbookSplitterDistance = (int)_numEbookSplitter.Value;
            s.WindowWidthScale = (double)_numWindowWidthScale.Value / 100.0;
            s.WindowHeightScale = (double)_numWindowHeightScale.Value / 100.0;

            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var wa = screen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));
            int targetW = (int)Math.Round(wa.Width * s.WindowWidthScale);
            int targetH = (int)Math.Round(wa.Height * s.WindowHeightScale);
            s.WindowWidth = targetW;
            s.WindowHeight = targetH;

            if (_cboLanguage.SelectedItem is LanguageComboItem selLang)
            {
                s.Language = selLang.Language.Code;
                LanguageManager.Instance.SetLanguage(selLang.Language.Code);
            }

            _configService.SaveConfig();
        }

        private void OnSaveSettingsClick(object? sender, EventArgs e)
        {
            SaveCurrentValuesToConfig();
            MessageBox.Show(Lang.T(StringKeys.SettingsSaved), Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
            _logger.Report("[✓] Configuration saved to config.json.");
            SettingsSaved?.Invoke();
        }

        private void OnResetDefaultsClick(object? sender, EventArgs e)
        {
            if (MessageBox.Show(Lang.T(StringKeys.SettingsResetConfirm), Lang.T(StringKeys.CommonWarning), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var defaults = new AppSettings();
                _configService.SaveConfig(defaults);
                _modelManager.UpdateDefaultPresetVoice(defaults.Voice);
                LoadSettings();
                SettingsSaved?.Invoke();
            }
        }
    }
}
