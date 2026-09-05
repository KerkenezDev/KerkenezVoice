using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;
using KerkenezVoice.UI.Controls;
using KerkenezVoice.UI.Tabs;

namespace KerkenezVoice.UI
{
    public class MainForm : Form
    {
        private readonly ConfigService _configService;
        private readonly ModelManagerService _modelManager;
        private readonly AudioPlaybackService _playbackService;
        private readonly AudioProcessingService _audioProcessing;
        private readonly LexiconService _lexiconService;
        private readonly SubtitleExportService _subtitleExport;
        private readonly VoiceMixingService _voiceMixingService;
        private readonly DocumentParserService _docParser;
        private readonly KokoroEngineService _engineService;

        private SidebarNav _sidebar = null!;
        private Panel _contentPanel = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _lblStatus = null!;
        private ToolStripStatusLabel _lblMetrics = null!;

        private GenerateView _generateView = null!;
        private CustomVoicesView _customVoicesView = null!;
        private AudioFxView _audioFxView = null!;
        private LexiconView _lexiconView = null!;
        private SettingsView _settingsView = null!;
        private LogsView _logsView = null!;

        private readonly bool _isFirstLaunch;

        public MainForm(ConfigService? configService = null)
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;

            _isFirstLaunch = ConfigService.IsFirstInstallation;
            _configService = configService ?? new ConfigService();

            // Initialize Services
            _modelManager = new ModelManagerService();
            _playbackService = new AudioPlaybackService();
            _audioProcessing = new AudioProcessingService();
            _lexiconService = new LexiconService();
            _subtitleExport = new SubtitleExportService();
            _voiceMixingService = new VoiceMixingService(_modelManager);
            _docParser = new DocumentParserService();

            _engineService = new KokoroEngineService(
                _modelManager,
                _audioProcessing,
                _lexiconService,
                _subtitleExport,
                _playbackService
            );

            InitializeComponent();
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;

            UpdateStatusStrip(Lang.T(StringKeys.StatusReady), GetMetricsString());

            this.Shown += async (s, e) =>
            {
                await Task.Yield();

                if (_isFirstLaunch && !ShortcutService.ShortcutsExist)
                {
                    try
                    {
                        var res = MessageBox.Show(
                            this,
                            Lang.T(StringKeys.MainShortcutsPromptDesc),
                            Lang.T(StringKeys.MainShortcutsPromptTitle),
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (res == DialogResult.Yes)
                        {
                            ShortcutService.CreateShortcuts();
                        }
                    }
                    catch { }
                }

                // Check model presence
                if (!_modelManager.AreModelsPresent())
                {
                    UpdateStatusStrip(Lang.T(StringKeys.StatusModelsRequired), GetMetricsString());
                    using var dlg = new UI.Dialogs.ModelDownloadDialog(_modelManager);
                    if (dlg.ShowDialog(this) != DialogResult.OK && !_modelManager.AreModelsPresent())
                    {
                        MessageBox.Show(this, "Model weights are required to use Kerkenez Voice. Please download models.", "Models Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                _logsView.AppendLog($"[i] Application: Kerkenez Voice v{UninstallRegistrationService.CurrentVersion}");
                _logsView.AppendLog($"[i] Configuration Directory: {ConfigService.AppDataFolder}");
                _logsView.AppendLog($"[i] Models Directory: {_modelManager.ModelsDirectory}");
                _logsView.AppendLog($"[i] Active Voice: {_configService.Settings.Voice} | Threads: {_configService.Settings.NumThreads}");

                // Initialize Kokoro TTS engine
                UpdateStatusStrip("Initializing Kokoro TTS engine...", GetMetricsString());
                bool initOk = await _engineService.InitializeAsync();
                if (initOk)
                {
                    _generateView.LoadVoices();
                    _customVoicesView.ReloadVoices();
                    UpdateStatusStrip(Lang.T(StringKeys.StatusReady), GetMetricsString());
                    _logsView.AppendLog("[✓] Kokoro TTS engine ready.");
                }
                else
                {
                    UpdateStatusStrip("Kokoro engine initialization failed.", GetMetricsString());
                    _logsView.AppendLog("[!] Kokoro TTS engine failed to initialize.");
                }
            };

            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            ApplyLocalization();
        }

        private void InitializeComponent()
        {
            // Set window icon
            try
            {
                if (File.Exists("app.ico"))
                {
                    this.Icon = new Icon("app.ico");
                }
                else
                {
                    using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("KerkenezVoice.app.ico");
                    if (stream != null)
                    {
                        this.Icon = new Icon(stream);
                    }
                    else
                    {
                        this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                }
            }
            catch { }

            // Dynamic Window Dimensions
            var currentScreen = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
            var workingArea = currentScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

            double widthScale = _configService.Settings.WindowWidthScale > 0.1 && _configService.Settings.WindowWidthScale <= 1.0
                ? _configService.Settings.WindowWidthScale
                : 0.60;
            double heightScale = _configService.Settings.WindowHeightScale > 0.1 && _configService.Settings.WindowHeightScale <= 1.0
                ? _configService.Settings.WindowHeightScale
                : 0.56;

            int targetWidth = _configService.Settings.WindowWidth >= 960
                ? _configService.Settings.WindowWidth
                : (int)Math.Round(workingArea.Width * widthScale);
            int targetHeight = _configService.Settings.WindowHeight >= 600
                ? _configService.Settings.WindowHeight
                : (int)Math.Round(workingArea.Height * heightScale);

            int minWidth = Math.Min(960, workingArea.Width);
            int minHeight = Math.Min(600, workingArea.Height);

            targetWidth = Math.Clamp(targetWidth, minWidth, workingArea.Width);
            targetHeight = Math.Clamp(targetHeight, minHeight, workingArea.Height);

            this.MinimumSize = new Size(minWidth, minHeight);
            this.Size = new Size(targetWidth, targetHeight);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - targetWidth) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - targetHeight) / 2)
            );
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.KeyPreview = true;

            // 1. Bottom Status Strip
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(242, 244, 247),
                Font = new Font("Segoe UI", 8.5F),
                Height = 26
            };

            _lblStatus = new ToolStripStatusLabel
            {
                Text = Lang.T(StringKeys.StatusStartingUp),
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            _lblMetrics = new ToolStripStatusLabel
            {
                Text = GetMetricsString(),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _statusStrip.Items.Add(_lblStatus);
            _statusStrip.Items.Add(_lblMetrics);

            // 2. Logs View
            _logsView = new LogsView();
            var logger = new Progress<string>(msg => _logsView.AppendLog(msg));

            // Wire engine & playback events directly to logs view
            _engineService.OnStatus += (status, isError) =>
            {
                _logsView.AppendLog(isError ? $"[!] {status}" : $"[*] {status}");
            };
            _engineService.OnProgress += (percent, elapsed, file, preview) =>
            {
                if (!string.IsNullOrWhiteSpace(preview))
                    _logsView.AppendLog($"[*] Synthesis: {percent:0}% ({elapsed:mm\\:ss}) - {preview}");
                else if (!string.IsNullOrWhiteSpace(file))
                    _logsView.AppendLog($"[*] Synthesis: {percent:0}% ({elapsed:mm\\:ss}) - {Path.GetFileName(file)}");
            };
            _engineService.OnFinished += () =>
            {
                _logsView.AppendLog("[✓] Synthesis job completed successfully.");
            };
            _playbackService.PlaybackStopped += () =>
            {
                _logsView.AppendLog("[*] Audio playback completed.");
            };

            // 3. Tab Views
            _generateView = new GenerateView(_configService, _modelManager, _engineService, _playbackService, _docParser, logger);
            _generateView.StatusUpdated += (status, metrics) => UpdateStatusStrip(status, metrics);

            _customVoicesView = new CustomVoicesView(_modelManager, _voiceMixingService, _playbackService, _engineService, logger);
            _audioFxView = new AudioFxView(_configService, logger);
            _lexiconView = new LexiconView(_configService);
            _settingsView = new SettingsView(_configService, _modelManager, _engineService, logger);
            _settingsView.SettingsSaved += () =>
            {
                _generateView.LoadVoices();
                _customVoicesView.ReloadVoices();
                UpdateStatusStrip(_lblStatus.Text, GetMetricsString());
            };

            // 4. Content Panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _contentPanel.Controls.Add(_generateView);
            _contentPanel.Controls.Add(_customVoicesView);
            _contentPanel.Controls.Add(_audioFxView);
            _contentPanel.Controls.Add(_lexiconView);
            _contentPanel.Controls.Add(_settingsView);
            _contentPanel.Controls.Add(_logsView);

            // 5. Left Sidebar Navigation
            _sidebar = new SidebarNav();
            _sidebar.IsCollapsed = _configService.Settings.CollapseSidebarByDefault;
            _sidebar.TabChanged += (s, idx) => ShowTab(idx);

            ShowTab(0);

            // Assemble Form
            this.Controls.Add(_contentPanel);
            this.Controls.Add(_sidebar);
            this.Controls.Add(_statusStrip);

            this.KeyDown += OnFormKeyDown;
        }

        private void ShowTab(int index)
        {
            _generateView.Visible = (index == 0);
            _customVoicesView.Visible = (index == 1);
            _audioFxView.Visible = (index == 2);
            _lexiconView.Visible = (index == 3);
            _settingsView.Visible = (index == 4);
            _logsView.Visible = (index == 5);

            if (index == 4)
            {
                _settingsView.LoadSettings();
                _settingsView.BringToFront();
            }
        }

        public void UpdateStatusStrip(string statusText, string metricsText)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateStatusStrip(statusText, metricsText)));
                return;
            }

            if (_lblStatus != null) _lblStatus.Text = statusText;
            if (_lblMetrics != null) _lblMetrics.Text = metricsText;
        }

        private string GetMetricsString()
        {
            return Lang.Format(StringKeys.StatusMetrics, _configService.Settings.Voice, _configService.Settings.NumThreads);
        }

        private void OnSystemPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            // Can be used for dynamic power optimization
        }

        private void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6)
            {
                int tabIdx = e.KeyCode - Keys.D1;
                _sidebar.SelectedIndex = tabIdx;
                e.Handled = true;
            }
        }

        private void ApplyLocalization()
        {
            this.Text = Lang.T(StringKeys.AppTitle);
            UpdateStatusStrip(Lang.T(StringKeys.StatusReady), GetMetricsString());
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
            _playbackService.Dispose();
            base.OnFormClosing(e);
        }
    }
}
