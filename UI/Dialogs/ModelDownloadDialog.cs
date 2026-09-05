using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezVoice.Services;

namespace KerkenezVoice.UI.Dialogs
{
    public class ModelDownloadDialog : Form
    {
        private readonly ModelManagerService _modelManager;
        private ProgressBar _progressBar = null!;
        private Label _lblStatus = null!;
        private Label _lblFile = null!;
        private Button _btnCancel = null!;
        private CancellationTokenSource? _cts;

        public bool IsSuccessful { get; private set; } = false;

        public ModelDownloadDialog(ModelManagerService modelManager)
        {
            _modelManager = modelManager;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Downloading Kokoro TTS Models";
            this.Size = new Size(480, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24)
            };

            var lblHeader = new Label
            {
                Text = "Downloading Required Kokoro-82M Weights",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 20)
            };

            _lblFile = new Label
            {
                Text = "Preparing download...",
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                Location = new Point(24, 50)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(24, 75),
                Size = new Size(415, 18),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            _lblStatus = new Label
            {
                Text = "Connecting to repository...",
                ForeColor = Color.FromArgb(120, 120, 120),
                AutoSize = true,
                Location = new Point(24, 102)
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(344, 125),
                Size = new Size(95, 28),
                FlatStyle = FlatStyle.System
            };
            _btnCancel.Click += (s, e) =>
            {
                _cts?.Cancel();
                this.Close();
            };

            pnl.Controls.Add(lblHeader);
            pnl.Controls.Add(_lblFile);
            pnl.Controls.Add(_progressBar);
            pnl.Controls.Add(_lblStatus);
            pnl.Controls.Add(_btnCancel);

            this.Controls.Add(pnl);

            this.Shown += async (s, e) => await StartDownloadAsync();
        }

        private async Task StartDownloadAsync()
        {
            _cts = new CancellationTokenSource();
            var progress = new Progress<(string file, long downloaded, long total, double percent)>(p =>
            {
                _lblFile.Text = $"Fetching: {p.file}";
                int pct = (int)Math.Clamp(p.percent, 0, 100);
                _progressBar.Value = pct;
                double mbDownloaded = p.downloaded / (1024.0 * 1024.0);
                double mbTotal = p.total / (1024.0 * 1024.0);
                _lblStatus.Text = $"{pct}% ({mbDownloaded:0.0} MB / {mbTotal:0.0} MB)";
            });

            try
            {
                await _modelManager.DownloadModelsAsync(progress, _cts.Token);
                IsSuccessful = _modelManager.AreModelsPresent();
                if (IsSuccessful)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (OperationCanceledException)
            {
                IsSuccessful = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Download failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                IsSuccessful = false;
                this.Close();
            }
        }
    }
}
