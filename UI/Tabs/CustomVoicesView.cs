using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;
using KokoroSharp;
using KokoroSharp.Core;

namespace KerkenezVoice.UI.Tabs
{
    public class CustomVoicesView : UserControl
    {
        private readonly ModelManagerService _modelManager;
        private readonly VoiceMixingService _voiceMixingService;
        private readonly AudioPlaybackService _playbackService;
        private readonly KokoroEngineService _engineService;
        private readonly IProgress<string> _logger;

        private Label _lblTitle = null!;
        private ComboBox _cboVoiceA = null!;
        private ComboBox _cboVoiceB = null!;
        private ComboBox _cboOperation = null!;
        private TrackBar _tbRatio = null!;
        private Label _lblRatioValue = null!;
        private Button _btnPreview = null!;
        private TextBox _txtNewName = null!;
        private Button _btnCreate = null!;
        private ListBox _lbCustomVoices = null!;
        private Button _btnDelete = null!;
        private Button _btnPlaySaved = null!;
        private Label _lblStatus = null!;

        private List<KokoroVoice> _voices = new();

        public CustomVoicesView(
            ModelManagerService modelManager,
            VoiceMixingService voiceMixingService,
            AudioPlaybackService playbackService,
            KokoroEngineService engineService,
            IProgress<string> logger)
        {
            _modelManager = modelManager;
            _voiceMixingService = voiceMixingService;
            _playbackService = playbackService;
            _engineService = engineService;
            _logger = logger;

            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            ReloadVoices();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

            // 1. Top Header
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
                Text = Lang.T(StringKeys.VoiceMixTitle),
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, (int)(4 * scale), 0, 0)
            };

            topPanel.Controls.Add(_lblTitle);

            // 2. Main Scrollable Content
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding((int)(16 * scale))
            };

            // Card 1: Mixer Controls
            var cardMix = new Panel
            {
                Location = new Point((int)(16 * scale), (int)(16 * scale)),
                Width = (int)(480 * scale),
                Height = (int)(460 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale))
            };

            cardMix.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, cardMix.Width - 1, cardMix.Height - 1);
            };

            var lblMixTitle = new Label
            {
                Text = "Blend Voice Lab",
                Location = new Point((int)(16 * scale), (int)(14 * scale)),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true
            };

            var lblVoiceA = new Label { Text = "Base Voice A:", Location = new Point((int)(16 * scale), (int)(48 * scale)), AutoSize = true };
            _cboVoiceA = new ComboBox { Location = new Point((int)(16 * scale), (int)(68 * scale)), Width = (int)(210 * scale), DropDownStyle = ComboBoxStyle.DropDownList };

            var lblVoiceB = new Label { Text = "Base Voice B:", Location = new Point((int)(245 * scale), (int)(48 * scale)), AutoSize = true };
            _cboVoiceB = new ComboBox { Location = new Point((int)(245 * scale), (int)(68 * scale)), Width = (int)(210 * scale), DropDownStyle = ComboBoxStyle.DropDownList };

            var lblOp = new Label { Text = "Math Blending Operator:", Location = new Point((int)(16 * scale), (int)(108 * scale)), AutoSize = true };
            _cboOperation = new ComboBox { Location = new Point((int)(16 * scale), (int)(128 * scale)), Width = (int)(440 * scale), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboOperation.Items.AddRange(new object[] {
                "mix (Linear norm-preserving blend - Recommended)",
                "add (Arithmetic feature addition)",
                "subtract (Voice feature subtraction)",
                "multiply (Tensor modulation)",
                "divide (Inverse ratio modulation)"
            });
            _cboOperation.SelectedIndex = 0;

            var lblRatio = new Label { Text = "Blend Ratio (Weight toward Voice B):", Location = new Point((int)(16 * scale), (int)(170 * scale)), AutoSize = true };
            _lblRatioValue = new Label { Text = "50% Voice B / 50% Voice A", Location = new Point((int)(240 * scale), (int)(170 * scale)), AutoSize = true, ForeColor = Color.FromArgb(0, 102, 204), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };

            _tbRatio = new TrackBar { Location = new Point((int)(16 * scale), (int)(192 * scale)), Width = (int)(440 * scale), Minimum = 0, Maximum = 100, Value = 50, TickFrequency = 10 };
            _tbRatio.ValueChanged += (s, e) =>
            {
                int val = _tbRatio.Value;
                _lblRatioValue.Text = $"{val}% Voice B / {100 - val}% Voice A";
            };

            _btnPreview = new Button
            {
                Text = "▶ " + Lang.T(StringKeys.VoiceMixBtnPreview),
                Location = new Point((int)(16 * scale), (int)(245 * scale)),
                Width = (int)(440 * scale),
                Height = (int)(36 * scale),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPreview.Click += OnPreviewBlend;

            var sep = new Panel { Location = new Point((int)(16 * scale), (int)(296 * scale)), Width = (int)(440 * scale), Height = 1, BackColor = Color.FromArgb(225, 228, 232) };

            var lblNewName = new Label { Text = "New Custom Voice Name:", Location = new Point((int)(16 * scale), (int)(310 * scale)), AutoSize = true };
            _txtNewName = new TextBox { Location = new Point((int)(16 * scale), (int)(330 * scale)), Width = (int)(300 * scale) };

            _btnCreate = new Button
            {
                Text = "💾 " + Lang.T(StringKeys.VoiceMixBtnCreate),
                Location = new Point((int)(326 * scale), (int)(328 * scale)),
                Width = (int)(130 * scale),
                Height = (int)(28 * scale),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnCreate.Click += OnCreateCustomVoice;

            _lblStatus = new Label
            {
                Location = new Point((int)(16 * scale), (int)(370 * scale)),
                Width = (int)(440 * scale),
                Height = (int)(50 * scale),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            cardMix.Controls.AddRange(new Control[] {
                lblMixTitle,
                lblVoiceA, _cboVoiceA,
                lblVoiceB, _cboVoiceB,
                lblOp, _cboOperation,
                lblRatio, _lblRatioValue, _tbRatio,
                _btnPreview,
                sep,
                lblNewName, _txtNewName, _btnCreate,
                _lblStatus
            });

            // Card 2: Saved Custom Voices List
            var cardList = new Panel
            {
                Location = new Point((int)(512 * scale), (int)(16 * scale)),
                Width = (int)(340 * scale),
                Height = (int)(460 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale))
            };

            cardList.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, cardList.Width - 1, cardList.Height - 1);
            };

            var lblListTitle = new Label
            {
                Text = Lang.T(StringKeys.VoiceMixListTitle),
                Dock = DockStyle.Top,
                Height = (int)(26 * scale),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            _lbCustomVoices = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle
            };
            _lbCustomVoices.SelectedIndexChanged += (s, e) =>
            {
                bool hasSel = _lbCustomVoices.SelectedIndex >= 0;
                _btnDelete.Enabled = hasSel;
                _btnPlaySaved.Enabled = hasSel;
            };
            _lbCustomVoices.DoubleClick += OnPlaySavedCustomVoice;

            var bottomListActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(42 * scale),
                Padding = new Padding(0, (int)(8 * scale), 0, 0)
            };

            _btnPlaySaved = new Button
            {
                Text = "▶ Play Voice",
                Dock = DockStyle.Left,
                Width = (int)(140 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnPlaySaved.Click += OnPlaySavedCustomVoice;

            _btnDelete = new Button
            {
                Text = "🗑️ " + Lang.T(StringKeys.VoiceMixBtnDelete),
                Dock = DockStyle.Right,
                Width = (int)(140 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnDelete.Click += OnDeleteCustomVoice;

            bottomListActions.Controls.Add(_btnPlaySaved);
            bottomListActions.Controls.Add(_btnDelete);

            cardList.Controls.Add(_lbCustomVoices);
            cardList.Controls.Add(bottomListActions);
            cardList.Controls.Add(lblListTitle);

            scrollPanel.Controls.Add(cardList);
            scrollPanel.Controls.Add(cardMix);

            this.Controls.Add(scrollPanel);
            this.Controls.Add(topPanel);
        }

        public void ReloadVoices()
        {
            if (!_modelManager.AreModelsPresent()) return;

            try
            {
                _voices = _modelManager.LoadVoices();

                _cboVoiceA.Items.Clear();
                _cboVoiceB.Items.Clear();

                foreach (var v in _voices)
                {
                    _cboVoiceA.Items.Add(v.Name);
                    _cboVoiceB.Items.Add(v.Name);
                }

                if (_cboVoiceA.Items.Count > 0) _cboVoiceA.SelectedIndex = 0;
                if (_cboVoiceB.Items.Count > 1) _cboVoiceB.SelectedIndex = 1;
                else if (_cboVoiceB.Items.Count > 0) _cboVoiceB.SelectedIndex = 0;

                ReloadCustomVoicesList();
            }
            catch (Exception ex)
            {
                _logger.Report($"Failed to load voices in mixing studio: {ex.Message}");
            }
        }

        private void ReloadCustomVoicesList()
        {
            _lbCustomVoices.Items.Clear();
            if (Directory.Exists(_modelManager.CustomVoicesDirectory))
            {
                foreach (var f in Directory.GetFiles(_modelManager.CustomVoicesDirectory, "*.bin"))
                {
                    _lbCustomVoices.Items.Add(Path.GetFileNameWithoutExtension(f));
                }
            }
            _btnDelete.Enabled = false;
            _btnPlaySaved.Enabled = false;
        }

        private async void OnPreviewBlend(object? sender, EventArgs e)
        {
            if (_cboVoiceA.SelectedIndex < 0 || _cboVoiceB.SelectedIndex < 0) return;

            string nameA = _cboVoiceA.SelectedItem?.ToString() ?? "";
            string nameB = _cboVoiceB.SelectedItem?.ToString() ?? "";

            var vA = _voices.FirstOrDefault(v => v.Name == nameA);
            var vB = _voices.FirstOrDefault(v => v.Name == nameB);
            if (vA == null || vB == null) return;

            double ratio = _tbRatio.Value / 100.0;
            string op = "mix";
            int opIdx = _cboOperation.SelectedIndex;
            if (opIdx == 1) op = "add";
            else if (opIdx == 2) op = "subtract";
            else if (opIdx == 3) op = "multiply";
            else if (opIdx == 4) op = "divide";

            _btnPreview.Enabled = false;
            _lblStatus.ForeColor = Color.FromArgb(0, 102, 204);
            _lblStatus.Text = "Blending voices and generating audio preview...";

            try
            {
                var (success, msg, tensor) = _voiceMixingService.MixVoices(vA, vB, ratio, "", op);
                if (!success || tensor == null)
                {
                    _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                    _lblStatus.Text = $"Blending error: {msg}";
                    return;
                }

                var previewConfig = new AppSettings
                {
                    Volume = 1.0,
                    Pitch = 0.0,
                    Speed = 1.0,
                    ApplyFx = false,
                    Normalize = false,
                    Trim = false
                };

                string previewText = "This is a real-time speech preview of your custom blended voice.";
                var samples = await _engineService.GeneratePreviewAudioAsync(
                    previewText,
                    nameA,
                    1.0,
                    previewConfig,
                    tensor,
                    "a"
                );

                if (samples != null && samples.Length > 0)
                {
                    _lblStatus.ForeColor = Color.FromArgb(20, 140, 50);
                    _lblStatus.Text = "Playing blended voice preview...";
                    _playbackService.PlayAudioData(samples);
                    _logger.Report($"[✓] Custom blend preview generated ({op}, ratio {ratio:P0}): {nameA} + {nameB}.");
                }
                else
                {
                    _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                    _lblStatus.Text = "Failed to synthesize preview audio.";
                }
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                _lblStatus.Text = $"Preview error: {ex.Message}";
                _logger.Report($"[!] Blend preview error: {ex.Message}");
            }
            finally
            {
                _btnPreview.Enabled = true;
            }
        }

        private async void OnPlaySavedCustomVoice(object? sender, EventArgs e)
        {
            string? sel = _lbCustomVoices.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(sel)) return;

            _btnPlaySaved.Enabled = false;
            _lblStatus.ForeColor = Color.FromArgb(0, 102, 204);
            _lblStatus.Text = $"Synthesizing preview for '{sel}'...";

            try
            {
                var previewConfig = new AppSettings
                {
                    Volume = 1.0,
                    Pitch = 0.0,
                    Speed = 1.0,
                    ApplyFx = false,
                    Normalize = false,
                    Trim = false
                };

                string previewText = $"This is a preview of the custom voice {sel}.";
                var samples = await _engineService.GeneratePreviewAudioAsync(
                    previewText,
                    sel,
                    1.0,
                    previewConfig
                );

                if (samples != null && samples.Length > 0)
                {
                    _lblStatus.ForeColor = Color.FromArgb(20, 140, 50);
                    _lblStatus.Text = $"Playing preview of '{sel}'...";
                    _playbackService.PlayAudioData(samples);
                    _logger.Report($"[✓] Previewing saved custom voice '{sel}'.");
                }
                else
                {
                    _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                    _lblStatus.Text = $"Failed to preview custom voice '{sel}'.";
                }
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                _lblStatus.Text = $"Error previewing voice: {ex.Message}";
                _logger.Report($"[!] Error previewing custom voice: {ex.Message}");
            }
            finally
            {
                _btnPlaySaved.Enabled = true;
            }
        }

        private void OnCreateCustomVoice(object? sender, EventArgs e)
        {
            string newName = _txtNewName.Text.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show(this, "Please enter a valid voice name.", "Name Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string nameA = _cboVoiceA.SelectedItem?.ToString() ?? "";
            string nameB = _cboVoiceB.SelectedItem?.ToString() ?? "";

            var vA = _voices.FirstOrDefault(v => v.Name == nameA);
            var vB = _voices.FirstOrDefault(v => v.Name == nameB);
            if (vA == null || vB == null) return;

            double ratio = _tbRatio.Value / 100.0;
            string op = "mix";
            int opIdx = _cboOperation.SelectedIndex;
            if (opIdx == 1) op = "add";
            else if (opIdx == 2) op = "subtract";
            else if (opIdx == 3) op = "multiply";
            else if (opIdx == 4) op = "divide";

            var (success, msg, _) = _voiceMixingService.MixVoices(vA, vB, ratio, newName, op);
            if (success)
            {
                _lblStatus.ForeColor = Color.FromArgb(20, 140, 50);
                _lblStatus.Text = Lang.Format(StringKeys.VoiceMixCreatedSuccess, newName);
                _logger.Report($"[✓] Created custom voice '{newName}' in {_modelManager.CustomVoicesDirectory}.");
                _txtNewName.Clear();
                ReloadCustomVoicesList();
                ReloadVoices();
            }
            else
            {
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                _lblStatus.Text = $"Error creating voice: {msg}";
            }
        }

        private void OnDeleteCustomVoice(object? sender, EventArgs e)
        {
            string? sel = _lbCustomVoices.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(sel)) return;

            var res = MessageBox.Show(this, $"Are you sure you want to delete custom voice '{sel}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                _voiceMixingService.DeleteCustomVoice(sel);
                _logger.Report($"[!] Deleted custom voice '{sel}'.");
                ReloadCustomVoicesList();
                ReloadVoices();
            }
        }

        private void ApplyLocalization()
        {
            if (_lblTitle != null) _lblTitle.Text = Lang.T(StringKeys.VoiceMixTitle);
            if (_btnPreview != null) _btnPreview.Text = "▶ " + Lang.T(StringKeys.VoiceMixBtnPreview);
            if (_btnCreate != null) _btnCreate.Text = "💾 " + Lang.T(StringKeys.VoiceMixBtnCreate);
            if (_btnDelete != null) _btnDelete.Text = "🗑️ " + Lang.T(StringKeys.VoiceMixBtnDelete);
        }
    }
}
