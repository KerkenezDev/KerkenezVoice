using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;
using KerkenezVoice.UI.Dialogs;
using KokoroSharp.Core;

namespace KerkenezVoice.UI.Tabs
{
    public class GenerateView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly ModelManagerService _modelManager;
        private readonly KokoroEngineService _engineService;
        private readonly AudioPlaybackService _playbackService;
        private readonly DocumentParserService _docParser;
        private readonly IProgress<string> _logger;

        public event Action<string, string>? StatusUpdated;

        private TextBox _txtDirect = null!;
        private TextBox _txtFilePath = null!;
        private Button _btnBrowseFile = null!;
        private RadioButton _rbText = null!;
        private RadioButton _rbFile = null!;
        private Panel _pnlTextInput = null!;
        private Panel _pnlFileInput = null!;

        private ComboBox _cboPresets = null!;
        private Button _btnSavePreset = null!;
        private Button _btnRefreshPresets = null!;

        private ComboBox _cboVoice = null!;
        private ComboBox _cboLang = null!;
        private ComboBox _cboFormat = null!;
        private TrackBar _tbSpeed = null!;
        private Label _lblSpeed = null!;
        private TrackBar _tbPitch = null!;
        private Label _lblPitch = null!;
        private TrackBar _tbVolume = null!;
        private Label _lblVolume = null!;
        private NumericUpDown _numThreads = null!;

        private CheckBox _chkCombine = null!;
        private CheckBox _chkSeparate = null!;
        private CheckBox _chkSubtitles = null!;
        private CheckBox _chkNormalize = null!;
        private CheckBox _chkTrim = null!;
        private CheckBox _chkApplyFx = null!;

        private ProgressBar _progressBar = null!;
        private Label _lblStatus = null!;
        private Label _lblDetails = null!;
        private Button _btnPreview = null!;
        private Button _btnGenerate = null!;
        private Button _btnCancel = null!;
        private Button _btnOpenFolder = null!;

        private List<KokoroVoice> _voices = new();
        private CancellationTokenSource? _activeCts;
        private string? _lastGeneratedPath;
        private bool _isReloadingPresets;
        private bool _isLoadingVoices;

        public GenerateView(
            ConfigService configService,
            ModelManagerService modelManager,
            KokoroEngineService engineService,
            AudioPlaybackService playbackService,
            DocumentParserService docParser,
            IProgress<string> logger)
        {
            _configService = configService;
            _modelManager = modelManager;
            _engineService = engineService;
            _playbackService = playbackService;
            _docParser = docParser;
            _logger = logger;

            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            WireEngineEvents();
            LoadVoices();
            ReloadPresets();
            LoadConfigValues();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

            // Top Header
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(58 * scale),
                Padding = new Padding((int)(16 * scale), (int)(12 * scale), (int)(16 * scale), (int)(12 * scale)),
                BackColor = Color.White
            };

            topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = Lang.T(StringKeys.SynthTitle),
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, (int)(4 * scale), 0, 0)
            };

            var rightActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            var lblPreset = new Label { Text = Lang.T(StringKeys.SynthPreset), AutoSize = true, Margin = new Padding(0, (int)(6 * scale), (int)(4 * scale), 0), ForeColor = Color.FromArgb(80, 80, 80) };
            _cboPresets = new ComboBox { Width = (int)(140 * scale), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, (int)(2 * scale), (int)(4 * scale), 0) };
            _cboPresets.SelectedIndexChanged += OnPresetSelected;

            _btnSavePreset = new Button { Text = "💾", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding((int)(6 * scale), (int)(3 * scale), (int)(6 * scale), (int)(3 * scale)), Margin = new Padding(0, 0, (int)(4 * scale), 0), FlatStyle = FlatStyle.System };
            _btnSavePreset.Click += OnSavePreset;

            _btnRefreshPresets = new Button { Text = "🔄", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding((int)(6 * scale), (int)(3 * scale), (int)(6 * scale), (int)(3 * scale)), FlatStyle = FlatStyle.System };
            _btnRefreshPresets.Click += (s, e) => ReloadPresets();

            rightActions.Controls.AddRange(new Control[] { lblPreset, _cboPresets, _btnSavePreset, _btnRefreshPresets });
            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(rightActions);

            // Bottom Action Bar
            var bottomActionCard = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(125 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale))
            };

            bottomActionCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, 0, bottomActionCard.Width, 0);
            };

            _lblStatus = new Label
            {
                Text = Lang.T(StringKeys.StatusReady),
                Location = new Point((int)(16 * scale), (int)(10 * scale)),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            _lblDetails = new Label
            {
                Text = "",
                Location = new Point((int)(240 * scale), (int)(10 * scale)),
                AutoSize = true,
                Font = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(120, 120, 120)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point((int)(16 * scale), (int)(32 * scale)),
                Height = (int)(12 * scale),
                Width = (int)(720 * scale),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            var btnRow = new FlowLayoutPanel
            {
                Location = new Point((int)(16 * scale), (int)(52 * scale)),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };

            _btnPreview = new Button
            {
                Text = Lang.T(StringKeys.SynthBtnPreview),
                Height = (int)(36 * scale),
                Width = (int)(150 * scale),
                Margin = new Padding(0, 0, (int)(10 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnPreview.Click += OnPreviewAudio;

            _btnGenerate = new Button
            {
                Text = Lang.T(StringKeys.SynthBtnGenerate),
                Height = (int)(36 * scale),
                Width = (int)(190 * scale),
                Margin = new Padding(0, 0, (int)(10 * scale), 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnGenerate.Click += OnStartGeneration;

            _btnCancel = new Button
            {
                Text = Lang.T(StringKeys.SynthBtnCancel),
                Height = (int)(36 * scale),
                Width = (int)(110 * scale),
                Margin = new Padding(0, 0, (int)(10 * scale), 0),
                FlatStyle = FlatStyle.System,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _btnCancel.Click += OnCancelGeneration;

            _btnOpenFolder = new Button
            {
                Text = Lang.T(StringKeys.SynthBtnOpenFolder),
                Height = (int)(36 * scale),
                Width = (int)(180 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnOpenFolder.Click += OnOpenOutputFolder;

            btnRow.Controls.AddRange(new Control[] { _btnPreview, _btnGenerate, _btnCancel, _btnOpenFolder });

            bottomActionCard.Controls.Add(_lblStatus);
            bottomActionCard.Controls.Add(_lblDetails);
            bottomActionCard.Controls.Add(_progressBar);
            bottomActionCard.Controls.Add(btnRow);

            // Center Scrollable Panel
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding((int)(16 * scale))
            };

            // Card 1: Input Source
            var cardInput = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(190 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale)),
                Margin = new Padding(0, 0, 0, (int)(14 * scale))
            };

            cardInput.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, cardInput.Width - 1, cardInput.Height - 1);
            };

            var radioPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(28 * scale),
                FlowDirection = FlowDirection.LeftToRight
            };

            _rbText = new RadioButton { Text = Lang.T(StringKeys.SynthInputModeText), Checked = true, AutoSize = true, Margin = new Padding(0, 0, (int)(16 * scale), 0), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _rbFile = new RadioButton { Text = Lang.T(StringKeys.SynthInputModeFile), Checked = false, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            _rbText.CheckedChanged += (s, e) =>
            {
                _pnlTextInput.Visible = _rbText.Checked;
                _pnlFileInput.Visible = _rbFile.Checked;
            };

            radioPanel.Controls.Add(_rbText);
            radioPanel.Controls.Add(_rbFile);

            _pnlTextInput = new Panel { Dock = DockStyle.Fill };
            _txtDirect = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = Lang.T(StringKeys.SynthDirectTextPlaceholder)
            };
            _pnlTextInput.Controls.Add(_txtDirect);

            _pnlFileInput = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(0, (int)(10 * scale), 0, 0) };
            var lblSelect = new Label { Text = Lang.T(StringKeys.SynthSelectFile), Dock = DockStyle.Top, Height = (int)(22 * scale), ForeColor = Color.FromArgb(80, 80, 80) };
            var fileRow = new Panel { Dock = DockStyle.Top, Height = (int)(32 * scale) };
            _txtFilePath = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

            var btnBrowseWrap = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnBrowseFile = new Button { Text = Lang.T(StringKeys.SynthBrowse), Width = (int)(90 * scale), Height = (int)(30 * scale), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand, Margin = new Padding((int)(6 * scale), 0, (int)(6 * scale), 0) };
            _btnBrowseFile.Click += OnBrowseFile;

            var btnBatch = new Button
            {
                Text = "⚡ Batch Studio (Folder / Files)...",
                Width = (int)(220 * scale),
                Height = (int)(30 * scale),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            btnBatch.Click += (s, e) => OpenBatchDialog();

            btnBrowseWrap.Controls.Add(_btnBrowseFile);
            btnBrowseWrap.Controls.Add(btnBatch);

            fileRow.Controls.Add(_txtFilePath);
            fileRow.Controls.Add(btnBrowseWrap);

            var lblFileTips = new Label { Text = Lang.T(StringKeys.SynthSupportedDocs), Dock = DockStyle.Top, Height = (int)(24 * scale), ForeColor = Color.FromArgb(120, 120, 120), Font = new Font("Segoe UI", 8.5F) };
            _pnlFileInput.Controls.Add(lblFileTips);
            _pnlFileInput.Controls.Add(fileRow);
            _pnlFileInput.Controls.Add(lblSelect);

            cardInput.Controls.Add(_pnlTextInput);
            cardInput.Controls.Add(_pnlFileInput);
            cardInput.Controls.Add(radioPanel);

            // Card 2: Voice & Synthesis Parameters
            var cardParams = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(270 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale)),
                Margin = new Padding(0, (int)(14 * scale), 0, 0)
            };

            cardParams.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, cardParams.Width - 1, cardParams.Height - 1);
            };

            var lblVoice = new Label { Text = Lang.T(StringKeys.SynthVoice), Location = new Point((int)(16 * scale), (int)(14 * scale)), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            _cboVoice = new ComboBox { Location = new Point((int)(16 * scale), (int)(34 * scale)), Width = (int)(220 * scale), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboVoice.SelectedIndexChanged += (s, e) =>
            {
                if (_isReloadingPresets || _isLoadingVoices) return;
                if (_cboVoice.SelectedItem != null)
                {
                    string selVoice = _cboVoice.SelectedItem.ToString() ?? "af_heart";
                    _configService.Settings.Voice = selVoice;
                    _configService.SaveConfig();
                    _modelManager.UpdateDefaultPresetVoice(selVoice);
                }
            };

            var lblLang = new Label { Text = Lang.T(StringKeys.SynthLanguage), Location = new Point((int)(250 * scale), (int)(14 * scale)), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            _cboLang = new ComboBox { Location = new Point((int)(250 * scale), (int)(34 * scale)), Width = (int)(150 * scale), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboLang.Items.AddRange(new object[] { "American English (a)", "British English (b)", "Spanish (e)", "French (f)", "Italian (i)", "Brazilian Portuguese (p)", "Japanese (j)", "Mandarin Chinese (z)" });
            _cboLang.SelectedIndex = 0;

            var lblFmt = new Label { Text = Lang.T(StringKeys.SynthFormat), Location = new Point((int)(416 * scale), (int)(14 * scale)), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            _cboFormat = new ComboBox { Location = new Point((int)(416 * scale), (int)(34 * scale)), Width = (int)(100 * scale), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboFormat.Items.AddRange(new object[] { "wav", "mp3", "flac", "ogg" });
            _cboFormat.SelectedIndex = 0;

            var lblThr = new Label { Text = "Threads:", Location = new Point((int)(532 * scale), (int)(14 * scale)), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            _numThreads = new NumericUpDown { Location = new Point((int)(532 * scale), (int)(34 * scale)), Width = (int)(75 * scale), Minimum = 1, Maximum = 16, Value = 1 };

            // Sliders
            _lblSpeed = new Label { Location = new Point((int)(16 * scale), (int)(74 * scale)), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            _tbSpeed = new TrackBar { Location = new Point((int)(16 * scale), (int)(94 * scale)), Width = (int)(220 * scale), Minimum = 50, Maximum = 200, Value = 100, TickFrequency = 25 };
            _tbSpeed.ValueChanged += (s, e) =>
            {
                double spd = _tbSpeed.Value / 100.0;
                _configService.Settings.Speed = spd;
                _lblSpeed.Text = Lang.Format(StringKeys.SynthSpeed, spd);
            };

            _lblPitch = new Label { Location = new Point((int)(250 * scale), (int)(74 * scale)), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            _tbPitch = new TrackBar { Location = new Point((int)(250 * scale), (int)(94 * scale)), Width = (int)(220 * scale), Minimum = -12, Maximum = 12, Value = 0, TickFrequency = 2 };
            _tbPitch.ValueChanged += (s, e) =>
            {
                _configService.Settings.Pitch = _tbPitch.Value;
                _lblPitch.Text = Lang.Format(StringKeys.SynthPitch, _tbPitch.Value);
            };

            _lblVolume = new Label { Location = new Point((int)(484 * scale), (int)(74 * scale)), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            _tbVolume = new TrackBar { Location = new Point((int)(484 * scale), (int)(94 * scale)), Width = (int)(220 * scale), Minimum = 0, Maximum = 200, Value = 100, TickFrequency = 25 };
            _tbVolume.ValueChanged += (s, e) =>
            {
                double vol = _tbVolume.Value / 100.0;
                _configService.Settings.Volume = vol;
                _lblVolume.Text = Lang.Format(StringKeys.SynthVolume, vol);
            };

            // Checkboxes
            var flowChk = new FlowLayoutPanel
            {
                Location = new Point((int)(16 * scale), (int)(150 * scale)),
                Size = new Size((int)(700 * scale), (int)(100 * scale)),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            _chkCombine = new CheckBox { Text = Lang.T(StringKeys.SynthCombine), Checked = true, AutoSize = true, Margin = new Padding(0, 0, (int)(16 * scale), (int)(8 * scale)) };
            _chkSeparate = new CheckBox { Text = Lang.T(StringKeys.SynthSeparate), Checked = false, AutoSize = true, Margin = new Padding(0, 0, (int)(16 * scale), (int)(8 * scale)) };
            _chkSubtitles = new CheckBox { Text = Lang.T(StringKeys.SynthSubtitles), Checked = false, AutoSize = true, Margin = new Padding(0, 0, (int)(16 * scale), (int)(8 * scale)) };
            _chkNormalize = new CheckBox { Text = Lang.T(StringKeys.SynthNormalize), Checked = false, AutoSize = true, Margin = new Padding(0, 0, (int)(16 * scale), (int)(8 * scale)) };
            _chkTrim = new CheckBox { Text = Lang.T(StringKeys.SynthTrim), Checked = false, AutoSize = true, Margin = new Padding(0, 0, (int)(16 * scale), (int)(8 * scale)) };
            _chkApplyFx = new CheckBox { Text = Lang.T(StringKeys.SynthApplyFx), Checked = true, AutoSize = true, Margin = new Padding(0, 0, (int)(16 * scale), (int)(8 * scale)) };

            flowChk.Controls.AddRange(new Control[] { _chkCombine, _chkSeparate, _chkSubtitles, _chkNormalize, _chkTrim, _chkApplyFx });

            cardParams.Controls.AddRange(new Control[] {
                lblVoice, _cboVoice,
                lblLang, _cboLang,
                lblFmt, _cboFormat,
                lblThr, _numThreads,
                _lblSpeed, _tbSpeed,
                _lblPitch, _tbPitch,
                _lblVolume, _tbVolume,
                flowChk
            });

            scrollPanel.Controls.Add(cardParams);
            scrollPanel.Controls.Add(cardInput);

            this.Controls.Add(scrollPanel);
            this.Controls.Add(bottomActionCard);
            this.Controls.Add(topPanel);
        }

        private void WireEngineEvents()
        {
            _engineService.OnProgress += (percent, elapsed, file, previewPath) =>
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        double normalizedPercent = percent <= 1.0 ? percent * 100.0 : percent;
                        int pct = (int)Math.Clamp(normalizedPercent, 0, 100);
                        _progressBar.Value = pct;
                        _lblDetails.Text = $"{pct}% | {elapsed.TotalSeconds:0.0}s";
                        if (this.Visible)
                        {
                            StatusUpdated?.Invoke(Lang.Format(StringKeys.StatusSynthesizing, file, normalizedPercent), GetMetricsString());
                        }
                    }));
                }
            };

            _engineService.OnStatus += (status, isError) =>
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _lblStatus.Text = status;
                        _lblStatus.ForeColor = isError ? Color.FromArgb(200, 30, 30) : Color.FromArgb(40, 40, 40);
                    }));
                }
            };

            _engineService.OnFinished += () =>
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _progressBar.Value = 100;
                        _btnGenerate.Enabled = true;
                        _btnPreview.Enabled = true;
                        _btnCancel.Enabled = false;
                        StatusUpdated?.Invoke(Lang.T(StringKeys.StatusReady), GetMetricsString());
                    }));
                }
            };
        }

        public void LoadVoices()
        {
            _isLoadingVoices = true;
            try
            {
                _cboVoice.Items.Clear();
                if (!_modelManager.AreModelsPresent()) return;

                _voices = _modelManager.LoadVoices();
                foreach (var v in _voices)
                {
                    _cboVoice.Items.Add(v.Name);
                }

                string cur = _configService.Settings.Voice;
                int idx = FindVoiceIndex(_cboVoice, cur);
                if (idx >= 0)
                {
                    _cboVoice.SelectedIndex = idx;
                }
                else if (_cboVoice.Items.Count > 0)
                {
                    _cboVoice.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.Report($"Error loading voices into generate view: {ex.Message}");
            }
            finally
            {
                _isLoadingVoices = false;
            }
        }

        public static int FindVoiceIndex(ComboBox combo, string? voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName) || combo.Items.Count == 0) return -1;
            string target = voiceName.Trim();

            // 1. Exact match (case-insensitive)
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i]?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            // 2. Prefix-agnostic match (e.g. "heart" matches "af_heart", or "af_heart" matches "heart")
            string targetBase = (target.Length > 3 && target[2] == '_') ? target.Substring(3) : target;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                string item = combo.Items[i]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(item)) continue;

                string itemBase = (item.Length > 3 && item[2] == '_') ? item.Substring(3) : item;

                if (string.Equals(itemBase, targetBase, StringComparison.OrdinalIgnoreCase))
                    return i;

                if (item.EndsWith("_" + target, StringComparison.OrdinalIgnoreCase) ||
                    target.EndsWith("_" + item, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void ReloadPresets()
        {
            _isReloadingPresets = true;
            try
            {
                string currentSelection = _cboPresets.SelectedItem?.ToString() ?? "Default";
                _cboPresets.Items.Clear();
                _cboPresets.Items.Add("Default");

                if (Directory.Exists(ConfigService.PresetsFolder))
                {
                    foreach (var f in Directory.GetFiles(ConfigService.PresetsFolder, "*.json"))
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase))
                        {
                            _cboPresets.Items.Add(name);
                        }
                    }
                }

                int pIdx = -1;
                for (int i = 0; i < _cboPresets.Items.Count; i++)
                {
                    if (string.Equals(_cboPresets.Items[i]?.ToString(), currentSelection, StringComparison.OrdinalIgnoreCase))
                    {
                        pIdx = i;
                        break;
                    }
                }
                _cboPresets.SelectedIndex = (pIdx >= 0) ? pIdx : 0;
            }
            finally
            {
                _isReloadingPresets = false;
            }
        }

        private void LoadConfigValues()
        {
            var s = _configService.Settings;
            _tbSpeed.Value = (int)Math.Clamp(s.Speed * 100.0, 50, 200);
            _lblSpeed.Text = Lang.Format(StringKeys.SynthSpeed, s.Speed);

            _tbPitch.Value = (int)Math.Clamp(s.Pitch, -12, 12);
            _lblPitch.Text = Lang.Format(StringKeys.SynthPitch, s.Pitch);

            _tbVolume.Value = (int)Math.Clamp(s.Volume * 100.0, 0, 200);
            _lblVolume.Text = Lang.Format(StringKeys.SynthVolume, s.Volume);

            _numThreads.Value = Math.Clamp(s.NumThreads, 1, 16);
            _chkCombine.Checked = s.Combine;
            _chkSeparate.Checked = s.Separate;
            _chkSubtitles.Checked = s.ExportSubtitles;
            _chkNormalize.Checked = s.Normalize;
            _chkTrim.Checked = s.Trim;
            _chkApplyFx.Checked = s.ApplyFx;

            int fIdx = _cboFormat.Items.IndexOf(s.Format.ToLowerInvariant());
            _cboFormat.SelectedIndex = (fIdx >= 0) ? fIdx : 0;
        }

        private void OnBrowseFile(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Document for Speech Synthesis",
                Filter = "Documents (*.txt;*.pdf;*.epub)|*.txt;*.pdf;*.epub|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _txtFilePath.Text = ofd.FileName;
            }
        }

        private void OpenBatchDialog()
        {
            SyncUiToSettings();
            using var dlg = new BatchProcessingDialog(
                _configService,
                _engineService,
                _docParser,
                _modelManager,
                _logger);
            dlg.ShowDialog(this);
        }

        private async void OnStartGeneration(object? sender, EventArgs e)
        {
            if (!_modelManager.AreModelsPresent())
            {
                MessageBox.Show(this, Lang.T(StringKeys.StatusModelsRequired), "Models Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string textToSynthesize = "";
            if (_rbText.Checked)
            {
                textToSynthesize = _txtDirect.Text.Trim();
            }
            else
            {
                string path = _txtFilePath.Text.Trim();
                if (!File.Exists(path))
                {
                    MessageBox.Show(this, "Please select a valid document file.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    textToSynthesize = _docParser.ExtractText(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to extract text from document: {ex.Message}", "Parsing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(textToSynthesize))
            {
                MessageBox.Show(this, "No text provided to synthesize.", "Empty Text", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Sync settings
            SyncUiToSettings();

            _btnGenerate.Enabled = false;
            _btnPreview.Enabled = false;
            _btnCancel.Enabled = true;
            _progressBar.Value = 0;

            _activeCts = new CancellationTokenSource();

            string outDir = _configService.Settings.GetEffectiveOutputDirectory();
            string outName = $"Speech_{DateTime.Now:yyyyMMdd_HHmmss}";
            _configService.Settings.OutDir = outDir;
            _configService.Settings.Filename = outName;

            try
            {
                _logger.Report($"Starting synthesis of {textToSynthesize.Length} characters with voice '{_configService.Settings.Voice}'...");
                await _engineService.StartConversionAsync(textToSynthesize, _configService.Settings);

                _lastGeneratedPath = Path.Combine(outDir, $"{outName}.wav");
                if (!File.Exists(_lastGeneratedPath))
                {
                    var matched = Directory.GetFiles(outDir, $"{outName}*.wav").FirstOrDefault();
                    if (matched != null) _lastGeneratedPath = matched;
                }

                _lblStatus.Text = Lang.T(StringKeys.SynthStatusComplete);
                _lblStatus.ForeColor = Color.FromArgb(20, 140, 50);
                NotificationService.ShowNotification("Synthesis Complete", $"Audio successfully generated in {outDir}");
            }
            catch (OperationCanceledException)
            {
                _lblStatus.Text = Lang.T(StringKeys.SynthStatusCancelled);
                _lblStatus.ForeColor = Color.FromArgb(180, 100, 0);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = Lang.Format(StringKeys.SynthStatusError, ex.Message);
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                _logger.Report($"Synthesis error: {ex.Message}");
            }
            finally
            {
                _btnGenerate.Enabled = true;
                _btnPreview.Enabled = true;
                _btnCancel.Enabled = false;
            }
        }

        private async void OnPreviewAudio(object? sender, EventArgs e)
        {
            if (!_modelManager.AreModelsPresent())
            {
                MessageBox.Show(this, Lang.T(StringKeys.StatusModelsRequired), "Models Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string text = _rbText.Checked ? _txtDirect.Text.Trim() : "This is a real-time speech preview from Kerkenez Voice.";
            if (string.IsNullOrWhiteSpace(text)) text = "Hello from Kerkenez Voice.";

            // Take first sentence / first 100 chars for preview
            if (text.Length > 120) text = text.Substring(0, 120);

            SyncUiToSettings();

            _btnPreview.Enabled = false;
            _lblStatus.Text = Lang.T(StringKeys.StatusPlaying);

            try
            {
                var samples = await _engineService.GeneratePreviewAudioAsync(text, _configService.Settings.Voice, _configService.Settings.Speed, _configService.Settings);
                if (samples != null && samples.Length > 0)
                {
                    _playbackService.PlayAudioData(samples);
                }
            }
            catch (Exception ex)
            {
                _logger.Report($"Preview error: {ex.Message}");
            }
            finally
            {
                _btnPreview.Enabled = true;
            }
        }

        private void OnCancelGeneration(object? sender, EventArgs e)
        {
            _activeCts?.Cancel();
            _engineService.Cancel();
        }

        private void OnOpenOutputFolder(object? sender, EventArgs e)
        {
            string dir = _configService.Settings.GetEffectiveOutputDirectory();
            if (Directory.Exists(dir))
            {
                Process.Start("explorer.exe", dir);
            }
        }

        private void SyncUiToSettings()
        {
            var s = _configService.Settings;
            s.Voice = _cboVoice.SelectedItem?.ToString() ?? s.Voice;
            s.Format = _cboFormat.SelectedItem?.ToString() ?? s.Format;
            s.NumThreads = (int)_numThreads.Value;
            s.Speed = _tbSpeed.Value / 100.0;
            s.Pitch = _tbPitch.Value;
            s.Volume = _tbVolume.Value / 100.0;
            s.Combine = _chkCombine.Checked;
            s.Separate = _chkSeparate.Checked;
            s.ExportSubtitles = _chkSubtitles.Checked;
            s.Normalize = _chkNormalize.Checked;
            s.Trim = _chkTrim.Checked;
            s.ApplyFx = _chkApplyFx.Checked;
            _configService.SaveConfig();
        }

        private void OnPresetSelected(object? sender, EventArgs e)
        {
            string? sel = _cboPresets.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(sel)) return;

            string path = Path.Combine(ConfigService.PresetsFolder, $"{sel}.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var p = JsonSerializer.Deserialize<VoicePreset>(json);
                    if (p != null)
                    {
                        var s = _configService.Settings;

                        // If "Default" preset is selected, respect the user's default voice
                        if (sel.Equals("Default", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Voice = s.Voice;
                        }
                        else if (!_isReloadingPresets && !string.IsNullOrWhiteSpace(p.Voice))
                        {
                            s.Voice = p.Voice;
                        }

                        s.Speed = p.Speed;
                        s.Volume = p.Volume;
                        s.Pitch = p.Pitch;
                        s.Normalize = p.Normalize;
                        s.Trim = p.Trim;
                        s.Format = p.Format;
                        s.ApplyFx = p.ApplyFx;

                        LoadConfigValues();

                        int vIdx = FindVoiceIndex(_cboVoice, p.Voice);
                        if (vIdx >= 0)
                        {
                            _cboVoice.SelectedIndex = vIdx;
                        }
                    }
                }
                catch { }
            }
        }

        private void OnSavePreset(object? sender, EventArgs e)
        {
            using var dlg = new Form
            {
                Text = "Save Voice Preset",
                Size = new Size(320, 150),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label { Text = "Preset Name:", Location = new Point(16, 16), AutoSize = true };
            var txt = new TextBox { Location = new Point(16, 38), Width = 270 };
            var btnOk = new Button { Text = "Save", Location = new Point(126, 75), Width = 75, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.System };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(210, 75), Width = 75, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.System };

            dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            dlg.AcceptButton = btnOk;

            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
            {
                string safeName = Path.GetFileNameWithoutExtension(txt.Text.Trim());
                string path = Path.Combine(ConfigService.PresetsFolder, $"{safeName}.json");
                try
                {
                    SyncUiToSettings();
                    var s = _configService.Settings;
                    var p = new VoicePreset
                    {
                        Voice = s.Voice,
                        Speed = s.Speed,
                        Volume = s.Volume,
                        Pitch = s.Pitch,
                        SplitPattern = s.SplitPattern,
                        Normalize = s.Normalize,
                        Trim = s.Trim,
                        Format = s.Format,
                        ApplyFx = s.ApplyFx
                    };
                    File.WriteAllText(path, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
                    ReloadPresets();
                    _cboPresets.SelectedItem = safeName;
                }
                catch (Exception ex)
                {
                    _logger.Report($"Error saving voice preset: {ex.Message}");
                }
            }
        }

        private string GetMetricsString()
        {
            return Lang.Format(StringKeys.StatusMetrics, _configService.Settings.Voice, _configService.Settings.NumThreads);
        }

        private void ApplyLocalization()
        {
            _rbText.Text = Lang.T(StringKeys.SynthInputModeText);
            _rbFile.Text = Lang.T(StringKeys.SynthInputModeFile);
            _btnBrowseFile.Text = Lang.T(StringKeys.SynthBrowse);
            _btnPreview.Text = Lang.T(StringKeys.SynthBtnPreview);
            _btnGenerate.Text = Lang.T(StringKeys.SynthBtnGenerate);
            _btnCancel.Text = Lang.T(StringKeys.SynthBtnCancel);
            _btnOpenFolder.Text = Lang.T(StringKeys.SynthBtnOpenFolder);
            _chkCombine.Text = Lang.T(StringKeys.SynthCombine);
            _chkSeparate.Text = Lang.T(StringKeys.SynthSeparate);
            _chkSubtitles.Text = Lang.T(StringKeys.SynthSubtitles);
            _chkNormalize.Text = Lang.T(StringKeys.SynthNormalize);
            _chkTrim.Text = Lang.T(StringKeys.SynthTrim);
            _chkApplyFx.Text = Lang.T(StringKeys.SynthApplyFx);
        }
    }
}
