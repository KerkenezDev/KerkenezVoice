using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;

namespace KerkenezVoice.UI.Tabs
{
    public class AudioFxView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly IProgress<string> _logger;

        private Label _lblTitle = null!;
        private CheckBox _chkApplyMaster = null!;
        private ComboBox _cboPresets = null!;
        private Button _btnSavePreset = null!;
        private Button _btnRefreshPresets = null!;

        // Dynamics
        private CheckBox _chkComp = null!;
        private TrackBar _tbCompThresh = null!;
        private Label _lblCompThresh = null!;
        private TrackBar _tbCompRatio = null!;
        private Label _lblCompRatio = null!;
        private CheckBox _chkLimiter = null!;
        private TrackBar _tbLimiterThresh = null!;
        private Label _lblLimiterThresh = null!;
        private CheckBox _chkGain = null!;
        private TrackBar _tbGain = null!;
        private Label _lblGain = null!;

        // EQ & Filters
        private TrackBar _tbBass = null!;
        private Label _lblBass = null!;
        private TrackBar _tbTreble = null!;
        private Label _lblTreble = null!;
        private CheckBox _chkHpf = null!;
        private TrackBar _tbHpf = null!;
        private Label _lblHpf = null!;
        private CheckBox _chkLpf = null!;
        private TrackBar _tbLpf = null!;
        private Label _lblLpf = null!;

        // Spatial & Time
        private CheckBox _chkReverb = null!;
        private TrackBar _tbReverbRoom = null!;
        private Label _lblReverbRoom = null!;
        private TrackBar _tbReverbWet = null!;
        private Label _lblReverbWet = null!;
        private CheckBox _chkDelay = null!;
        private TrackBar _tbDelayTime = null!;
        private Label _lblDelayTime = null!;
        private TrackBar _tbDelayFeedback = null!;
        private Label _lblDelayFeedback = null!;

        // Mod & Distortion
        private CheckBox _chkChorus = null!;
        private TrackBar _tbChorusRate = null!;
        private Label _lblChorusRate = null!;
        private CheckBox _chkPhaser = null!;
        private TrackBar _tbPhaserRate = null!;
        private Label _lblPhaserRate = null!;
        private CheckBox _chkDistortion = null!;
        private TrackBar _tbDistortionDrive = null!;
        private Label _lblDistortionDrive = null!;
        private CheckBox _chkClipping = null!;
        private CheckBox _chkPitchShift = null!;
        private TrackBar _tbPitchShift = null!;
        private Label _lblPitchShift = null!;
        private CheckBox _chkBitcrush = null!;
        private TrackBar _tbBitcrush = null!;
        private Label _lblBitcrush = null!;
        private CheckBox _chkGsm = null!;

        public AudioFxView(ConfigService configService, IProgress<string> logger)
        {
            _configService = configService;
            _logger = logger;

            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            LoadFromSettings();
            ReloadPresets();
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

            _lblTitle = new Label
            {
                Text = Lang.T(StringKeys.FxTitle),
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

            _chkApplyMaster = new CheckBox
            {
                Text = Lang.T(StringKeys.FxApplyMaster),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Margin = new Padding(0, (int)(4 * scale), (int)(16 * scale), 0)
            };
            _chkApplyMaster.CheckedChanged += (s, e) =>
            {
                _configService.Settings.ApplyFx = _chkApplyMaster.Checked;
                _configService.SaveConfig();
            };

            _cboPresets = new ComboBox
            {
                Width = (int)(150 * scale),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, (int)(2 * scale), (int)(6 * scale), 0)
            };
            _cboPresets.SelectedIndexChanged += OnPresetSelected;

            _btnSavePreset = new Button
            {
                Text = "💾 " + Lang.T(StringKeys.FxSavePreset),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(8 * scale), (int)(4 * scale), (int)(8 * scale), (int)(4 * scale)),
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
                FlatStyle = FlatStyle.System
            };
            _btnSavePreset.Click += OnSavePreset;

            _btnRefreshPresets = new Button
            {
                Text = "🔄",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(6 * scale), (int)(4 * scale), (int)(6 * scale), (int)(4 * scale)),
                FlatStyle = FlatStyle.System
            };
            _btnRefreshPresets.Click += (s, e) => ReloadPresets();

            rightActions.Controls.Add(_chkApplyMaster);
            rightActions.Controls.Add(_cboPresets);
            rightActions.Controls.Add(_btnSavePreset);
            rightActions.Controls.Add(_btnRefreshPresets);

            topPanel.Controls.Add(_lblTitle);
            topPanel.Controls.Add(rightActions);

            // Scrollable Content
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding((int)(16 * scale))
            };

            // 1. Dynamics Card
            var cardDyn = CreateCard("1. " + Lang.T(StringKeys.FxDynamicsTitle), (int)(240 * scale), scale);
            _chkComp = new CheckBox { Text = Lang.T(StringKeys.FxComp), Location = new Point((int)(16 * scale), (int)(38 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblCompThresh = new Label { Location = new Point((int)(34 * scale), (int)(62 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbCompThresh = new TrackBar { Location = new Point((int)(180 * scale), (int)(58 * scale)), Width = (int)(280 * scale), Minimum = -60, Maximum = 0, Value = -20, TickFrequency = 10 };
            _lblCompRatio = new Label { Location = new Point((int)(34 * scale), (int)(98 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbCompRatio = new TrackBar { Location = new Point((int)(180 * scale), (int)(94 * scale)), Width = (int)(280 * scale), Minimum = 1, Maximum = 20, Value = 4, TickFrequency = 2 };

            _chkLimiter = new CheckBox { Text = Lang.T(StringKeys.FxLimiter), Location = new Point((int)(16 * scale), (int)(134 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblLimiterThresh = new Label { Location = new Point((int)(34 * scale), (int)(158 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbLimiterThresh = new TrackBar { Location = new Point((int)(180 * scale), (int)(154 * scale)), Width = (int)(280 * scale), Minimum = -12, Maximum = 0, Value = -1, TickFrequency = 2 };

            _chkGain = new CheckBox { Text = Lang.T(StringKeys.FxGain), Location = new Point((int)(16 * scale), (int)(194 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblGain = new Label { Location = new Point((int)(34 * scale), (int)(218 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbGain = new TrackBar { Location = new Point((int)(180 * scale), (int)(214 * scale)), Width = (int)(280 * scale), Minimum = -20, Maximum = 20, Value = 0, TickFrequency = 5 };

            HookDynamicsEvents();
            cardDyn.Controls.AddRange(new Control[] { _chkComp, _lblCompThresh, _tbCompThresh, _lblCompRatio, _tbCompRatio, _chkLimiter, _lblLimiterThresh, _tbLimiterThresh, _chkGain, _lblGain, _tbGain });

            // 2. EQ & Filters Card
            var cardEq = CreateCard("2. " + Lang.T(StringKeys.FxEqTitle), (int)(200 * scale), scale);
            _lblBass = new Label { Location = new Point((int)(16 * scale), (int)(42 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbBass = new TrackBar { Location = new Point((int)(180 * scale), (int)(38 * scale)), Width = (int)(280 * scale), Minimum = -20, Maximum = 20, Value = 0, TickFrequency = 5 };
            _lblTreble = new Label { Location = new Point((int)(16 * scale), (int)(78 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbTreble = new TrackBar { Location = new Point((int)(180 * scale), (int)(74 * scale)), Width = (int)(280 * scale), Minimum = -20, Maximum = 20, Value = 0, TickFrequency = 5 };

            _chkHpf = new CheckBox { Text = "HPF", Location = new Point((int)(16 * scale), (int)(116 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblHpf = new Label { Location = new Point((int)(70 * scale), (int)(118 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbHpf = new TrackBar { Location = new Point((int)(180 * scale), (int)(114 * scale)), Width = (int)(280 * scale), Minimum = 20, Maximum = 1000, Value = 50, TickFrequency = 100 };

            _chkLpf = new CheckBox { Text = "LPF", Location = new Point((int)(16 * scale), (int)(154 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblLpf = new Label { Location = new Point((int)(70 * scale), (int)(156 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbLpf = new TrackBar { Location = new Point((int)(180 * scale), (int)(150 * scale)), Width = (int)(280 * scale), Minimum = 1000, Maximum = 20000, Value = 10000, TickFrequency = 2000 };

            HookEqEvents();
            cardEq.Controls.AddRange(new Control[] { _lblBass, _tbBass, _lblTreble, _tbTreble, _chkHpf, _lblHpf, _tbHpf, _chkLpf, _lblLpf, _tbLpf });

            // 3. Spatial & Time Card
            var cardSpatial = CreateCard("3. " + Lang.T(StringKeys.FxSpatialTitle), (int)(240 * scale), scale);
            _chkReverb = new CheckBox { Text = Lang.T(StringKeys.FxReverb), Location = new Point((int)(16 * scale), (int)(38 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblReverbRoom = new Label { Location = new Point((int)(34 * scale), (int)(62 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbReverbRoom = new TrackBar { Location = new Point((int)(180 * scale), (int)(58 * scale)), Width = (int)(280 * scale), Minimum = 0, Maximum = 100, Value = 50, TickFrequency = 10 };
            _lblReverbWet = new Label { Location = new Point((int)(34 * scale), (int)(98 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbReverbWet = new TrackBar { Location = new Point((int)(180 * scale), (int)(94 * scale)), Width = (int)(280 * scale), Minimum = 0, Maximum = 100, Value = 30, TickFrequency = 10 };

            _chkDelay = new CheckBox { Text = Lang.T(StringKeys.FxDelay), Location = new Point((int)(16 * scale), (int)(134 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblDelayTime = new Label { Location = new Point((int)(34 * scale), (int)(158 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbDelayTime = new TrackBar { Location = new Point((int)(180 * scale), (int)(154 * scale)), Width = (int)(280 * scale), Minimum = 0, Maximum = 200, Value = 50, TickFrequency = 20 };
            _lblDelayFeedback = new Label { Location = new Point((int)(34 * scale), (int)(194 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbDelayFeedback = new TrackBar { Location = new Point((int)(180 * scale), (int)(190 * scale)), Width = (int)(280 * scale), Minimum = 0, Maximum = 95, Value = 0, TickFrequency = 10 };

            HookSpatialEvents();
            cardSpatial.Controls.AddRange(new Control[] { _chkReverb, _lblReverbRoom, _tbReverbRoom, _lblReverbWet, _tbReverbWet, _chkDelay, _lblDelayTime, _tbDelayTime, _lblDelayFeedback, _tbDelayFeedback });

            // 4. Modulation & Distortion Card
            var cardMod = CreateCard("4. " + Lang.T(StringKeys.FxModTitle), (int)(320 * scale), scale);
            _chkChorus = new CheckBox { Text = Lang.T(StringKeys.FxChorus), Location = new Point((int)(16 * scale), (int)(38 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblChorusRate = new Label { Location = new Point((int)(34 * scale), (int)(62 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbChorusRate = new TrackBar { Location = new Point((int)(180 * scale), (int)(58 * scale)), Width = (int)(280 * scale), Minimum = 1, Maximum = 100, Value = 10, TickFrequency = 10 };

            _chkPhaser = new CheckBox { Text = Lang.T(StringKeys.FxPhaser), Location = new Point((int)(16 * scale), (int)(98 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblPhaserRate = new Label { Location = new Point((int)(34 * scale), (int)(122 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbPhaserRate = new TrackBar { Location = new Point((int)(180 * scale), (int)(118 * scale)), Width = (int)(280 * scale), Minimum = 1, Maximum = 100, Value = 10, TickFrequency = 10 };

            _chkDistortion = new CheckBox { Text = Lang.T(StringKeys.FxDistortion), Location = new Point((int)(16 * scale), (int)(158 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblDistortionDrive = new Label { Location = new Point((int)(34 * scale), (int)(182 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbDistortionDrive = new TrackBar { Location = new Point((int)(180 * scale), (int)(178 * scale)), Width = (int)(280 * scale), Minimum = 0, Maximum = 60, Value = 25, TickFrequency = 10 };

            _chkClipping = new CheckBox { Text = Lang.T(StringKeys.FxClipping), Location = new Point((int)(16 * scale), (int)(218 * scale)), AutoSize = true };
            _chkGsm = new CheckBox { Text = Lang.T(StringKeys.FxGsm), Location = new Point((int)(240 * scale), (int)(218 * scale)), AutoSize = true };

            _chkPitchShift = new CheckBox { Text = "Pitch Shift", Location = new Point((int)(16 * scale), (int)(250 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblPitchShift = new Label { Location = new Point((int)(34 * scale), (int)(274 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbPitchShift = new TrackBar { Location = new Point((int)(180 * scale), (int)(270 * scale)), Width = (int)(280 * scale), Minimum = -12, Maximum = 12, Value = 0, TickFrequency = 2 };

            _chkBitcrush = new CheckBox { Text = "Bitcrusher", Location = new Point((int)(16 * scale), (int)(306 * scale)), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblBitcrush = new Label { Location = new Point((int)(34 * scale), (int)(330 * scale)), AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90) };
            _tbBitcrush = new TrackBar { Location = new Point((int)(180 * scale), (int)(326 * scale)), Width = (int)(280 * scale), Minimum = 2, Maximum = 16, Value = 8, TickFrequency = 2 };

            HookModEvents();
            cardMod.Controls.AddRange(new Control[] { _chkChorus, _lblChorusRate, _tbChorusRate, _chkPhaser, _lblPhaserRate, _tbPhaserRate, _chkDistortion, _lblDistortionDrive, _tbDistortionDrive, _chkClipping, _chkGsm, _chkPitchShift, _lblPitchShift, _tbPitchShift, _chkBitcrush, _lblBitcrush, _tbBitcrush });

            // Layout
            scrollPanel.Controls.Add(cardMod);
            scrollPanel.Controls.Add(cardSpatial);
            scrollPanel.Controls.Add(cardEq);
            scrollPanel.Controls.Add(cardDyn);

            this.Controls.Add(scrollPanel);
            this.Controls.Add(topPanel);
        }

        private Panel CreateCard(string title, int height, float scale)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale)),
                Margin = new Padding(0, 0, 0, (int)(16 * scale))
            };

            pnl.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            var lblHeader = new Label
            {
                Text = title,
                Location = new Point((int)(16 * scale), (int)(12 * scale)),
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true
            };
            pnl.Controls.Add(lblHeader);

            return pnl;
        }

        private void HookDynamicsEvents()
        {
            _chkComp.CheckedChanged += (s, e) => { _configService.Settings.CompEnabled = _chkComp.Checked; Save(); };
            _tbCompThresh.ValueChanged += (s, e) => { _configService.Settings.CompThreshold = _tbCompThresh.Value; _lblCompThresh.Text = Lang.Format(StringKeys.FxCompThreshold, _tbCompThresh.Value); Save(); };
            _tbCompRatio.ValueChanged += (s, e) => { _configService.Settings.CompRatio = _tbCompRatio.Value; _lblCompRatio.Text = Lang.Format(StringKeys.FxCompRatio, _tbCompRatio.Value); Save(); };

            _chkLimiter.CheckedChanged += (s, e) => { _configService.Settings.LimiterEnabled = _chkLimiter.Checked; Save(); };
            _tbLimiterThresh.ValueChanged += (s, e) => { _configService.Settings.LimiterThreshold = _tbLimiterThresh.Value; _lblLimiterThresh.Text = Lang.Format(StringKeys.FxLimiterThreshold, _tbLimiterThresh.Value); Save(); };

            _chkGain.CheckedChanged += (s, e) => { _configService.Settings.GainEnabled = _chkGain.Checked; Save(); };
            _tbGain.ValueChanged += (s, e) => { _configService.Settings.GainDb = _tbGain.Value; _lblGain.Text = Lang.Format(StringKeys.FxGainDb, _tbGain.Value); Save(); };
        }

        private void HookEqEvents()
        {
            _tbBass.ValueChanged += (s, e) => { _configService.Settings.EqBass = _tbBass.Value; _lblBass.Text = Lang.Format(StringKeys.FxBass, _tbBass.Value); Save(); };
            _tbTreble.ValueChanged += (s, e) => { _configService.Settings.EqTreble = _tbTreble.Value; _lblTreble.Text = Lang.Format(StringKeys.FxTreble, _tbTreble.Value); Save(); };

            _chkHpf.CheckedChanged += (s, e) => { _configService.Settings.HighpassEnabled = _chkHpf.Checked; Save(); };
            _tbHpf.ValueChanged += (s, e) => { _configService.Settings.HighpassFreq = _tbHpf.Value; _lblHpf.Text = Lang.Format(StringKeys.FxHpf, _tbHpf.Value); Save(); };

            _chkLpf.CheckedChanged += (s, e) => { _configService.Settings.LowpassEnabled = _chkLpf.Checked; Save(); };
            _tbLpf.ValueChanged += (s, e) => { _configService.Settings.LowpassFreq = _tbLpf.Value; _lblLpf.Text = Lang.Format(StringKeys.FxLpf, _tbLpf.Value); Save(); };
        }

        private void HookSpatialEvents()
        {
            _chkReverb.CheckedChanged += (s, e) => { _configService.Settings.ReverbEnabled = _chkReverb.Checked; Save(); };
            _tbReverbRoom.ValueChanged += (s, e) => { _configService.Settings.ReverbRoomSize = _tbReverbRoom.Value / 100.0; _lblReverbRoom.Text = Lang.Format(StringKeys.FxReverbRoom, _tbReverbRoom.Value); Save(); };
            _tbReverbWet.ValueChanged += (s, e) => { _configService.Settings.ReverbWetLevel = _tbReverbWet.Value / 100.0; _lblReverbWet.Text = Lang.Format(StringKeys.FxReverbWet, _tbReverbWet.Value); Save(); };

            _chkDelay.CheckedChanged += (s, e) => { _configService.Settings.DelayEnabled = _chkDelay.Checked; Save(); };
            _tbDelayTime.ValueChanged += (s, e) => { double sec = _tbDelayTime.Value / 100.0; _configService.Settings.DelayTime = sec; _lblDelayTime.Text = Lang.Format(StringKeys.FxDelayTime, sec); Save(); };
            _tbDelayFeedback.ValueChanged += (s, e) => { _configService.Settings.DelayFeedback = _tbDelayFeedback.Value / 100.0; _lblDelayFeedback.Text = Lang.Format(StringKeys.FxDelayFeedback, _tbDelayFeedback.Value); Save(); };
        }

        private void HookModEvents()
        {
            _chkChorus.CheckedChanged += (s, e) => { _configService.Settings.ChorusEnabled = _chkChorus.Checked; Save(); };
            _tbChorusRate.ValueChanged += (s, e) => { double r = _tbChorusRate.Value / 10.0; _configService.Settings.ChorusRate = r; _lblChorusRate.Text = Lang.Format(StringKeys.FxChorusRate, r); Save(); };

            _chkPhaser.CheckedChanged += (s, e) => { _configService.Settings.PhaserEnabled = _chkPhaser.Checked; Save(); };
            _tbPhaserRate.ValueChanged += (s, e) => { double r = _tbPhaserRate.Value / 10.0; _configService.Settings.PhaserRate = r; _lblPhaserRate.Text = Lang.Format(StringKeys.FxPhaserRate, r); Save(); };

            _chkDistortion.CheckedChanged += (s, e) => { _configService.Settings.DistortionEnabled = _chkDistortion.Checked; Save(); };
            _tbDistortionDrive.ValueChanged += (s, e) => { _configService.Settings.DistortionDrive = _tbDistortionDrive.Value; _lblDistortionDrive.Text = Lang.Format(StringKeys.FxDistortionDrive, _tbDistortionDrive.Value); Save(); };

            _chkClipping.CheckedChanged += (s, e) => { _configService.Settings.ClippingEnabled = _chkClipping.Checked; Save(); };
            _chkGsm.CheckedChanged += (s, e) => { _configService.Settings.GsmEnabled = _chkGsm.Checked; Save(); };

            _chkPitchShift.CheckedChanged += (s, e) => { _configService.Settings.PitchShiftEnabled = _chkPitchShift.Checked; Save(); };
            _tbPitchShift.ValueChanged += (s, e) => { _configService.Settings.PitchShiftSemitones = _tbPitchShift.Value; _lblPitchShift.Text = Lang.Format(StringKeys.FxPitchShift, _tbPitchShift.Value); Save(); };

            _chkBitcrush.CheckedChanged += (s, e) => { _configService.Settings.BitcrushEnabled = _chkBitcrush.Checked; Save(); };
            _tbBitcrush.ValueChanged += (s, e) => { _configService.Settings.BitcrushDepth = _tbBitcrush.Value; _lblBitcrush.Text = $"Depth: {_tbBitcrush.Value} bit"; Save(); };
        }

        private void Save()
        {
            _configService.SaveConfig();
        }

        private void LoadFromSettings()
        {
            var s = _configService.Settings;
            _chkApplyMaster.Checked = s.ApplyFx;

            _chkComp.Checked = s.CompEnabled;
            _tbCompThresh.Value = (int)Math.Clamp(s.CompThreshold, -60, 0);
            _lblCompThresh.Text = Lang.Format(StringKeys.FxCompThreshold, _tbCompThresh.Value);
            _tbCompRatio.Value = (int)Math.Clamp(s.CompRatio, 1, 20);
            _lblCompRatio.Text = Lang.Format(StringKeys.FxCompRatio, _tbCompRatio.Value);

            _chkLimiter.Checked = s.LimiterEnabled;
            _tbLimiterThresh.Value = (int)Math.Clamp(s.LimiterThreshold, -12, 0);
            _lblLimiterThresh.Text = Lang.Format(StringKeys.FxLimiterThreshold, _tbLimiterThresh.Value);

            _chkGain.Checked = s.GainEnabled;
            _tbGain.Value = (int)Math.Clamp(s.GainDb, -20, 20);
            _lblGain.Text = Lang.Format(StringKeys.FxGainDb, _tbGain.Value);

            _tbBass.Value = (int)Math.Clamp(s.EqBass, -20, 20);
            _lblBass.Text = Lang.Format(StringKeys.FxBass, _tbBass.Value);
            _tbTreble.Value = (int)Math.Clamp(s.EqTreble, -20, 20);
            _lblTreble.Text = Lang.Format(StringKeys.FxTreble, _tbTreble.Value);

            _chkHpf.Checked = s.HighpassEnabled;
            _tbHpf.Value = (int)Math.Clamp(s.HighpassFreq, 20, 1000);
            _lblHpf.Text = Lang.Format(StringKeys.FxHpf, _tbHpf.Value);

            _chkLpf.Checked = s.LowpassEnabled;
            _tbLpf.Value = (int)Math.Clamp(s.LowpassFreq, 1000, 20000);
            _lblLpf.Text = Lang.Format(StringKeys.FxLpf, _tbLpf.Value);

            _chkReverb.Checked = s.ReverbEnabled;
            _tbReverbRoom.Value = (int)Math.Clamp(s.ReverbRoomSize * 100.0, 0, 100);
            _lblReverbRoom.Text = Lang.Format(StringKeys.FxReverbRoom, _tbReverbRoom.Value);
            _tbReverbWet.Value = (int)Math.Clamp(s.ReverbWetLevel * 100.0, 0, 100);
            _lblReverbWet.Text = Lang.Format(StringKeys.FxReverbWet, _tbReverbWet.Value);

            _chkDelay.Checked = s.DelayEnabled;
            _tbDelayTime.Value = (int)Math.Clamp(s.DelayTime * 100.0, 0, 200);
            _lblDelayTime.Text = Lang.Format(StringKeys.FxDelayTime, s.DelayTime);
            _tbDelayFeedback.Value = (int)Math.Clamp(s.DelayFeedback * 100.0, 0, 95);
            _lblDelayFeedback.Text = Lang.Format(StringKeys.FxDelayFeedback, _tbDelayFeedback.Value);

            _chkChorus.Checked = s.ChorusEnabled;
            _tbChorusRate.Value = (int)Math.Clamp(s.ChorusRate * 10.0, 1, 100);
            _lblChorusRate.Text = Lang.Format(StringKeys.FxChorusRate, s.ChorusRate);

            _chkPhaser.Checked = s.PhaserEnabled;
            _tbPhaserRate.Value = (int)Math.Clamp(s.PhaserRate * 10.0, 1, 100);
            _lblPhaserRate.Text = Lang.Format(StringKeys.FxPhaserRate, s.PhaserRate);

            _chkDistortion.Checked = s.DistortionEnabled;
            _tbDistortionDrive.Value = (int)Math.Clamp(s.DistortionDrive, 0, 60);
            _lblDistortionDrive.Text = Lang.Format(StringKeys.FxDistortionDrive, _tbDistortionDrive.Value);

            _chkClipping.Checked = s.ClippingEnabled;
            _chkGsm.Checked = s.GsmEnabled;

            _chkPitchShift.Checked = s.PitchShiftEnabled;
            _tbPitchShift.Value = (int)Math.Clamp(s.PitchShiftSemitones, -12, 12);
            _lblPitchShift.Text = Lang.Format(StringKeys.FxPitchShift, _tbPitchShift.Value);

            _chkBitcrush.Checked = s.BitcrushEnabled;
            _tbBitcrush.Value = (int)Math.Clamp(s.BitcrushDepth, 2, 16);
            _lblBitcrush.Text = $"Depth: {_tbBitcrush.Value} bit";
        }

        private void ReloadPresets()
        {
            _cboPresets.Items.Clear();
            _cboPresets.Items.Add("Default");

            if (Directory.Exists(ConfigService.FxPresetsFolder))
            {
                foreach (var f in Directory.GetFiles(ConfigService.FxPresetsFolder, "*.json"))
                {
                    _cboPresets.Items.Add(Path.GetFileNameWithoutExtension(f));
                }
            }

            string curr = _configService.Settings.FxPreset;
            int idx = _cboPresets.Items.IndexOf(curr);
            _cboPresets.SelectedIndex = (idx >= 0) ? idx : 0;
        }

        private void OnPresetSelected(object? sender, EventArgs e)
        {
            string? sel = _cboPresets.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(sel)) return;

            _configService.Settings.FxPreset = sel;
            string presetPath = Path.Combine(ConfigService.FxPresetsFolder, $"{sel}.json");
            if (File.Exists(presetPath))
            {
                try
                {
                    string json = File.ReadAllText(presetPath);
                    var p = JsonSerializer.Deserialize<FxPreset>(json);
                    if (p != null)
                    {
                        ApplyPresetToSettings(p);
                        LoadFromSettings();
                        Save();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Report($"Error loading FX preset: {ex.Message}");
                }
            }
        }

        private void ApplyPresetToSettings(FxPreset p)
        {
            var s = _configService.Settings;
            s.ReverbEnabled = p.ReverbEnabled;
            s.ReverbRoomSize = p.ReverbRoomSize;
            s.ReverbWetLevel = p.ReverbWetLevel;
            s.EqBass = p.EqBass;
            s.EqTreble = p.EqTreble;
            s.CompEnabled = p.CompEnabled;
            s.CompThreshold = p.CompThreshold;
            s.CompRatio = p.CompRatio;
            s.LimiterEnabled = p.LimiterEnabled;
            s.LimiterThreshold = p.LimiterThreshold;
            s.GainEnabled = p.GainEnabled;
            s.GainDb = p.GainDb;
            s.DistortionEnabled = p.DistortionEnabled;
            s.DistortionDrive = p.DistortionDrive;
            s.ChorusEnabled = p.ChorusEnabled;
            s.ChorusRate = p.ChorusRate;
            s.PhaserEnabled = p.PhaserEnabled;
            s.PhaserRate = p.PhaserRate;
            s.ClippingEnabled = p.ClippingEnabled;
            s.BitcrushEnabled = p.BitcrushEnabled;
            s.GsmEnabled = p.GsmEnabled;
            s.HighpassEnabled = p.HighpassEnabled;
            s.HighpassFreq = p.HighpassFreq;
            s.LowpassEnabled = p.LowpassEnabled;
            s.LowpassFreq = p.LowpassFreq;
            s.DelayEnabled = p.DelayEnabled;
            s.DelayTime = p.DelayTime;
            s.DelayFeedback = p.DelayFeedback;
            s.PitchShiftEnabled = p.PitchShiftEnabled;
            s.PitchShiftSemitones = p.PitchShiftSemitones;
        }

        private void OnSavePreset(object? sender, EventArgs e)
        {
            using var dlg = new Form
            {
                Text = "Save FX Preset",
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
                string path = Path.Combine(ConfigService.FxPresetsFolder, $"{safeName}.json");
                try
                {
                    var s = _configService.Settings;
                    var p = new FxPreset
                    {
                        ReverbEnabled = s.ReverbEnabled,
                        ReverbRoomSize = s.ReverbRoomSize,
                        ReverbWetLevel = s.ReverbWetLevel,
                        EqBass = s.EqBass,
                        EqTreble = s.EqTreble,
                        CompEnabled = s.CompEnabled,
                        CompThreshold = s.CompThreshold,
                        CompRatio = s.CompRatio,
                        LimiterEnabled = s.LimiterEnabled,
                        LimiterThreshold = s.LimiterThreshold,
                        GainEnabled = s.GainEnabled,
                        GainDb = s.GainDb,
                        DistortionEnabled = s.DistortionEnabled,
                        DistortionDrive = s.DistortionDrive,
                        ChorusEnabled = s.ChorusEnabled,
                        ChorusRate = s.ChorusRate,
                        PhaserEnabled = s.PhaserEnabled,
                        PhaserRate = s.PhaserRate,
                        ClippingEnabled = s.ClippingEnabled,
                        BitcrushEnabled = s.BitcrushEnabled,
                        GsmEnabled = s.GsmEnabled,
                        HighpassEnabled = s.HighpassEnabled,
                        HighpassFreq = s.HighpassFreq,
                        LowpassEnabled = s.LowpassEnabled,
                        LowpassFreq = s.LowpassFreq,
                        DelayEnabled = s.DelayEnabled,
                        DelayTime = s.DelayTime,
                        DelayFeedback = s.DelayFeedback,
                        PitchShiftEnabled = s.PitchShiftEnabled,
                        PitchShiftSemitones = s.PitchShiftSemitones
                    };
                    File.WriteAllText(path, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
                    ReloadPresets();
                    _cboPresets.SelectedItem = safeName;
                }
                catch (Exception ex)
                {
                    _logger.Report($"Error saving FX preset: {ex.Message}");
                }
            }
        }

        private void ApplyLocalization()
        {
            if (_lblTitle != null) _lblTitle.Text = Lang.T(StringKeys.FxTitle);
            if (_chkApplyMaster != null) _chkApplyMaster.Text = Lang.T(StringKeys.FxApplyMaster);
            if (_btnSavePreset != null) _btnSavePreset.Text = "💾 " + Lang.T(StringKeys.FxSavePreset);
        }
    }
}
