using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;

namespace KerkenezVoice.UI.Dialogs
{
    public enum BatchItemStatus
    {
        Pending,
        Processing,
        Complete,
        Failed,
        Skipped
    }

    public class BatchQueueItem
    {
        public int Index { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ResolvedOutputName { get; set; } = string.Empty;
        public BatchItemStatus Status { get; set; } = BatchItemStatus.Pending;
        public string StatusDetails { get; set; } = "Queued";
        public ListViewItem? ListItem { get; set; }
    }

    public class BatchProcessingDialog : Form
    {
        private readonly ConfigService _configService;
        private readonly KokoroEngineService _engineService;
        private readonly DocumentParserService _docParser;
        private readonly ModelManagerService _modelManager;
        private readonly IProgress<string> _logger;

        private ListView _lvQueue = null!;
        private ComboBox _cboNamingPreset = null!;
        private TextBox _txtCustomTemplate = null!;
        private Label _lblTemplateHint = null!;
        private TextBox _txtOutputDir = null!;
        private Button _btnBrowseOutputDir = null!;

        private ProgressBar _pbOverall = null!;
        private ProgressBar _pbCurrent = null!;
        private Label _lblOverallStatus = null!;
        private Label _lblCurrentStatus = null!;

        private Button _btnAddFiles = null!;
        private Button _btnAddFolder = null!;
        private Button _btnRemoveSelected = null!;
        private Button _btnClearAll = null!;
        private Button _btnStart = null!;
        private Button _btnCancel = null!;
        private Button _btnOpenFolder = null!;
        private Button _btnClose = null!;

        private readonly List<BatchQueueItem> _queue = new();
        private CancellationTokenSource? _activeCts;
        private bool _isRunning = false;

        public BatchProcessingDialog(
            ConfigService configService,
            KokoroEngineService engineService,
            DocumentParserService docParser,
            ModelManagerService modelManager,
            IProgress<string> logger)
        {
            _configService = configService;
            _engineService = engineService;
            _docParser = docParser;
            _modelManager = modelManager;
            _logger = logger;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            float scale = this.DeviceDpi / 96f;

            this.Text = "Batch Speech Synthesis & Renaming Studio";
            this.Size = new Size((int)(920 * scale), (int)(680 * scale));
            this.MinimumSize = new Size((int)(780 * scale), (int)(540 * scale));
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 1. Top Header Card
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(62 * scale),
                Padding = new Padding((int)(18 * scale), (int)(10 * scale), (int)(18 * scale), (int)(10 * scale)),
                BackColor = Color.White
            };
            pnlHeader.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "📁  Batch Document Synthesis & Custom Renaming",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Top,
                Height = (int)(24 * scale)
            };

            var lblSubtitle = new Label
            {
                Text = "Queue multiple documents or entire folders for high-speed speech synthesis with custom output naming templates.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(108, 117, 125),
                Dock = DockStyle.Bottom,
                Height = (int)(18 * scale)
            };
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(lblTitle);

            // 2. Settings & Naming Pattern Card
            var pnlNaming = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(108 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale), (int)(10 * scale), (int)(16 * scale), (int)(10 * scale))
            };
            pnlNaming.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, pnlNaming.Height - 1, pnlNaming.Width, pnlNaming.Height - 1);
            };

            var row1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(32 * scale),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var lblPreset = new Label
            {
                Text = "Naming Template:",
                AutoSize = true,
                Margin = new Padding(0, (int)(4 * scale), (int)(8 * scale), 0),
                ForeColor = Color.FromArgb(50, 50, 50),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _cboNamingPreset = new ComboBox
            {
                Width = (int)(240 * scale),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, (int)(10 * scale), 0)
            };
            _cboNamingPreset.Items.AddRange(new object[]
            {
                "[Original] {Filename}",
                "[Numbered] {Index:00}_{Filename}",
                "[Numbered Prefix] Audio_{Index:00}",
                "[With Voice] {Filename}_{Voice}",
                "[Voice + Number] {Voice}_{Index:00}",
                "[Custom Template]..."
            });
            _cboNamingPreset.SelectedIndex = 0;
            _cboNamingPreset.SelectedIndexChanged += (s, e) => OnNamingPresetChanged();

            _txtCustomTemplate = new TextBox
            {
                Width = (int)(200 * scale),
                Text = "{Filename}",
                Margin = new Padding(0, 0, (int)(10 * scale), 0),
                Visible = false
            };
            _txtCustomTemplate.TextChanged += (s, e) => UpdateResolvedOutputNames();

            _lblTemplateHint = new Label
            {
                Text = "Tags: {Filename}, {Index:00}, {Voice}, {Date}",
                AutoSize = true,
                Margin = new Padding(0, (int)(4 * scale), 0, 0),
                ForeColor = Color.FromArgb(130, 130, 130),
                Font = new Font("Segoe UI", 8.25F)
            };

            row1.Controls.AddRange(new Control[] { lblPreset, _cboNamingPreset, _txtCustomTemplate, _lblTemplateHint });

            var row2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(36 * scale),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var lblOutDir = new Label
            {
                Text = "Output Folder:",
                AutoSize = true,
                Margin = new Padding(0, (int)(4 * scale), (int)(22 * scale), 0),
                ForeColor = Color.FromArgb(50, 50, 50),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _txtOutputDir = new TextBox
            {
                Width = (int)(450 * scale),
                Text = _configService.Settings.GetEffectiveOutputDirectory(),
                Margin = new Padding(0, 0, (int)(8 * scale), 0)
            };

            _btnBrowseOutputDir = new Button
            {
                Text = "Browse...",
                Width = (int)(85 * scale),
                Height = (int)(26 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnBrowseOutputDir.Click += (s, e) => OnBrowseOutputDir();

            row2.Controls.AddRange(new Control[] { lblOutDir, _txtOutputDir, _btnBrowseOutputDir });

            pnlNaming.Controls.Add(row2);
            pnlNaming.Controls.Add(row1);

            // 3. Queue Action Toolbar
            var pnlToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(42 * scale),
                Padding = new Padding((int)(16 * scale), (int)(6 * scale), (int)(16 * scale), 0),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _btnAddFiles = new Button { Text = "➕ Add Files...", Width = (int)(110 * scale), Height = (int)(30 * scale), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand, Margin = new Padding(0, 0, (int)(8 * scale), 0) };
            _btnAddFiles.Click += OnAddFilesClick;

            _btnAddFolder = new Button { Text = "📁 Add Folder...", Width = (int)(120 * scale), Height = (int)(30 * scale), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand, Margin = new Padding(0, 0, (int)(8 * scale), 0) };
            _btnAddFolder.Click += OnAddFolderClick;

            _btnRemoveSelected = new Button { Text = "🗑️ Remove", Width = (int)(95 * scale), Height = (int)(30 * scale), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand, Margin = new Padding(0, 0, (int)(8 * scale), 0) };
            _btnRemoveSelected.Click += OnRemoveSelectedClick;

            _btnClearAll = new Button { Text = "🧹 Clear Queue", Width = (int)(110 * scale), Height = (int)(30 * scale), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand, Margin = new Padding(0, 0, (int)(8 * scale), 0) };
            _btnClearAll.Click += (s, e) => ClearQueue();

            pnlToolbar.Controls.AddRange(new Control[] { _btnAddFiles, _btnAddFolder, _btnRemoveSelected, _btnClearAll });

            // 4. Center ListView Queue
            _lvQueue = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F)
            };

            _lvQueue.Columns.Add("#", (int)(40 * scale), HorizontalAlignment.Right);
            _lvQueue.Columns.Add("Source File", (int)(230 * scale));
            _lvQueue.Columns.Add("Source Folder", (int)(180 * scale));
            _lvQueue.Columns.Add("Output Audio Name", (int)(230 * scale));
            _lvQueue.Columns.Add("Size", (int)(75 * scale), HorizontalAlignment.Right);
            _lvQueue.Columns.Add("Status", (int)(130 * scale));

            var pnlCenter = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(16 * scale), 0, (int)(16 * scale), (int)(10 * scale))
            };
            pnlCenter.Controls.Add(_lvQueue);

            // 5. Bottom Status and Progress Bar Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(150 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(18 * scale), (int)(12 * scale), (int)(18 * scale), (int)(12 * scale))
            };
            pnlBottom.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, 0, pnlBottom.Width, 0);
            };

            _lblOverallStatus = new Label
            {
                Text = "Queue empty. Click 'Add Files' or 'Add Folder' to begin.",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point((int)(18 * scale), (int)(10 * scale)),
                AutoSize = true
            };

            _pbOverall = new ProgressBar
            {
                Location = new Point((int)(18 * scale), (int)(32 * scale)),
                Size = new Size((int)(865 * scale), (int)(12 * scale)),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            _lblCurrentStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point((int)(18 * scale), (int)(50 * scale)),
                AutoSize = true
            };

            _pbCurrent = new ProgressBar
            {
                Location = new Point((int)(18 * scale), (int)(72 * scale)),
                Size = new Size((int)(865 * scale), (int)(10 * scale)),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            var btnRow = new FlowLayoutPanel
            {
                Location = new Point((int)(18 * scale), (int)(94 * scale)),
                Width = (int)(865 * scale),
                Height = (int)(42 * scale),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _btnStart = new Button
            {
                Text = "▶ Start Batch Processing",
                Width = (int)(200 * scale),
                Height = (int)(36 * scale),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, (int)(12 * scale), 0)
            };
            _btnStart.Click += OnStartBatchClick;

            _btnCancel = new Button
            {
                Text = "⏹ Cancel",
                Width = (int)(100 * scale),
                Height = (int)(36 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Enabled = false,
                Margin = new Padding(0, 0, (int)(12 * scale), 0)
            };
            _btnCancel.Click += OnCancelBatchClick;

            _btnOpenFolder = new Button
            {
                Text = "📂 Open Output Folder",
                Width = (int)(170 * scale),
                Height = (int)(36 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, (int)(12 * scale), 0)
            };
            _btnOpenFolder.Click += (s, e) =>
            {
                string dir = _txtOutputDir.Text.Trim();
                if (Directory.Exists(dir)) Process.Start("explorer.exe", dir);
            };

            _btnClose = new Button
            {
                Text = "Close",
                Width = (int)(100 * scale),
                Height = (int)(36 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnClose.Click += (s, e) => this.Close();

            btnRow.Controls.AddRange(new Control[] { _btnStart, _btnCancel, _btnOpenFolder, _btnClose });

            pnlBottom.Controls.AddRange(new Control[] {
                _lblOverallStatus, _pbOverall,
                _lblCurrentStatus, _pbCurrent,
                btnRow
            });

            // Assemble main dialog layout
            this.Controls.Add(pnlCenter);
            this.Controls.Add(pnlToolbar);
            this.Controls.Add(pnlNaming);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottom);

            this.Resize += (s, e) =>
            {
                int w = this.ClientSize.Width - (int)(36 * scale);
                _pbOverall.Width = w;
                _pbCurrent.Width = w;
                btnRow.Width = w;
            };
        }

        private void OnNamingPresetChanged()
        {
            int sel = _cboNamingPreset.SelectedIndex;
            if (sel == 5) // Custom Template
            {
                _txtCustomTemplate.Visible = true;
                _txtCustomTemplate.Focus();
            }
            else
            {
                _txtCustomTemplate.Visible = false;
            }
            UpdateResolvedOutputNames();
        }

        private string GetCurrentNamingPattern()
        {
            int sel = _cboNamingPreset.SelectedIndex;
            return sel switch
            {
                0 => "{Filename}",
                1 => "{Index:00}_{Filename}",
                2 => "Audio_{Index:00}",
                3 => "{Filename}_{Voice}",
                4 => "{Voice}_{Index:00}",
                5 => string.IsNullOrWhiteSpace(_txtCustomTemplate.Text) ? "{Filename}" : _txtCustomTemplate.Text.Trim(),
                _ => "{Filename}"
            };
        }

        private void UpdateResolvedOutputNames()
        {
            string pattern = GetCurrentNamingPattern();
            string ext = "." + _configService.Settings.Format.ToLowerInvariant();
            string voice = _configService.Settings.Voice;
            string dateStr = DateTime.Now.ToString("yyyyMMdd");

            for (int i = 0; i < _queue.Count; i++)
            {
                var item = _queue[i];
                item.Index = i + 1;

                string resolved = pattern
                    .Replace("{Filename}", Path.GetFileNameWithoutExtension(item.FileName), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Index:000}", item.Index.ToString("D3"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Index:00}", item.Index.ToString("D2"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Index}", item.Index.ToString(), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Voice}", voice, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Date}", dateStr, StringComparison.OrdinalIgnoreCase);

                // Sanitize filename characters
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    resolved = resolved.Replace(c, '_');
                }

                item.ResolvedOutputName = resolved + ext;

                if (item.ListItem != null)
                {
                    item.ListItem.SubItems[0].Text = item.Index.ToString();
                    item.ListItem.SubItems[3].Text = item.ResolvedOutputName;
                }
            }
        }

        private void OnAddFilesClick(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Documents for Batch Speech Synthesis",
                Filter = "Supported Documents (*.txt;*.pdf;*.epub)|*.txt;*.pdf;*.epub|Text Files (*.txt)|*.txt|PDF Files (*.pdf)|*.pdf|EPUB Files (*.epub)|*.epub|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                AddFilesToQueue(ofd.FileNames);
            }
        }

        private void OnAddFolderClick(object? sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Select Folder Containing Documents to Synthesize",
                UseDescriptionForTitle = true
            };

            if (fbd.ShowDialog(this) == DialogResult.OK && Directory.Exists(fbd.SelectedPath))
            {
                var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt", ".pdf", ".epub" };
                try
                {
                    var files = Directory.GetFiles(fbd.SelectedPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => validExts.Contains(Path.GetExtension(f)))
                        .OrderBy(f => f)
                        .ToArray();

                    if (files.Length == 0)
                    {
                        MessageBox.Show(this, "No .txt, .pdf, or .epub documents found in the selected folder.", "No Documents Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    AddFilesToQueue(files);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Error reading directory: {ex.Message}", "Folder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void AddFilesToQueue(string[] filePaths)
        {
            _lvQueue.BeginUpdate();
            int addedCount = 0;

            foreach (var path in filePaths)
            {
                if (!File.Exists(path)) continue;

                // Avoid duplicate full paths
                if (_queue.Any(q => string.Equals(q.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var fi = new FileInfo(path);
                var item = new BatchQueueItem
                {
                    Index = _queue.Count + 1,
                    SourcePath = path,
                    FileName = fi.Name,
                    Folder = fi.DirectoryName ?? string.Empty,
                    FileSizeBytes = fi.Length,
                    Status = BatchItemStatus.Pending,
                    StatusDetails = "Queued"
                };

                var lvi = new ListViewItem(item.Index.ToString());
                lvi.SubItems.Add(item.FileName);
                lvi.SubItems.Add(item.Folder);
                lvi.SubItems.Add(""); // Will be filled by UpdateResolvedOutputNames
                lvi.SubItems.Add(FormatFileSize(item.FileSizeBytes));
                lvi.SubItems.Add(item.StatusDetails);
                lvi.Tag = item;
                item.ListItem = lvi;

                _queue.Add(item);
                _lvQueue.Items.Add(lvi);
                addedCount++;
            }

            UpdateResolvedOutputNames();
            _lvQueue.EndUpdate();

            UpdateQueueStatusText();
        }

        private void OnRemoveSelectedClick(object? sender, EventArgs e)
        {
            if (_isRunning) return;

            var selected = _lvQueue.SelectedItems.Cast<ListViewItem>().ToList();
            if (selected.Count == 0) return;

            _lvQueue.BeginUpdate();
            foreach (var lvi in selected)
            {
                if (lvi.Tag is BatchQueueItem item)
                {
                    _queue.Remove(item);
                }
                _lvQueue.Items.Remove(lvi);
            }
            UpdateResolvedOutputNames();
            _lvQueue.EndUpdate();
            UpdateQueueStatusText();
        }

        private void ClearQueue()
        {
            if (_isRunning) return;
            _queue.Clear();
            _lvQueue.Items.Clear();
            UpdateQueueStatusText();
            _pbOverall.Value = 0;
            _pbCurrent.Value = 0;
            _lblCurrentStatus.Text = "";
        }

        private void UpdateQueueStatusText()
        {
            if (_queue.Count == 0)
            {
                _lblOverallStatus.Text = "Queue empty. Click 'Add Files' or 'Add Folder' to begin.";
                _lblOverallStatus.ForeColor = Color.FromArgb(100, 100, 100);
            }
            else
            {
                long totalBytes = _queue.Sum(q => q.FileSizeBytes);
                _lblOverallStatus.Text = $"Ready: {_queue.Count} file(s) in queue ({FormatFileSize(totalBytes)}). Ready to synthesize.";
                _lblOverallStatus.ForeColor = Color.FromArgb(30, 120, 40);
            }
        }

        private void OnBrowseOutputDir()
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Select Output Folder for Synthesized Batch Audio",
                SelectedPath = _txtOutputDir.Text.Trim()
            };

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                _txtOutputDir.Text = fbd.SelectedPath;
            }
        }

        private async void OnStartBatchClick(object? sender, EventArgs e)
        {
            if (_queue.Count == 0)
            {
                MessageBox.Show(this, "The queue is empty. Please add files or a folder to synthesize.", "Queue Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outDir = _txtOutputDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                outDir = _configService.Settings.GetEffectiveOutputDirectory();
                _txtOutputDir.Text = outDir;
            }

            try
            {
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to create output directory: {ex.Message}", "Directory Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!_modelManager.AreModelsPresent())
            {
                MessageBox.Show(this, "Kokoro TTS models are missing. Please download them first.", "Models Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isRunning = true;
            _activeCts = new CancellationTokenSource();
            var ct = _activeCts.Token;

            SetUiRunningState(true);

            int totalFiles = _queue.Count;
            int completedFiles = 0;
            var batchStartTime = DateTime.Now;

            // Wire progress from engine
            Action<double, TimeSpan, string, string> progressHandler = (percent, elapsed, eta, snippet) =>
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _pbCurrent.Value = (int)Math.Clamp(percent, 0, 100);
                        _lblCurrentStatus.Text = $"{snippet} ({percent:0}% | ETA: {eta})";
                    }));
                }
            };

            _engineService.OnProgress += progressHandler;

            try
            {
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    var item = _queue[i];

                    // Update UI status
                    item.Status = BatchItemStatus.Processing;
                    item.StatusDetails = "Extracting text...";
                    if (item.ListItem != null)
                    {
                        item.ListItem.SubItems[5].Text = item.StatusDetails;
                        item.ListItem.ForeColor = Color.FromArgb(0, 102, 204);
                        item.ListItem.EnsureVisible();
                    }

                    _lblOverallStatus.Text = $"Processing file {i + 1} of {totalFiles}: '{item.FileName}'";
                    _pbCurrent.Value = 0;

                    string text;
                    try
                    {
                        text = _docParser.ExtractText(item.SourcePath).Trim();
                    }
                    catch (Exception ex)
                    {
                        item.Status = BatchItemStatus.Failed;
                        item.StatusDetails = $"Extraction Error: {ex.Message}";
                        if (item.ListItem != null)
                        {
                            item.ListItem.SubItems[5].Text = item.StatusDetails;
                            item.ListItem.ForeColor = Color.FromArgb(200, 30, 30);
                        }
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        item.Status = BatchItemStatus.Skipped;
                        item.StatusDetails = "Empty text";
                        if (item.ListItem != null)
                        {
                            item.ListItem.SubItems[5].Text = item.StatusDetails;
                            item.ListItem.ForeColor = Color.FromArgb(150, 150, 150);
                        }
                        continue;
                    }

                    // Configure synthesis
                    var fileConfig = _configService.Settings.Clone();
                    fileConfig.OutDir = outDir;
                    fileConfig.Filename = Path.GetFileNameWithoutExtension(item.ResolvedOutputName);

                    item.StatusDetails = $"Synthesizing ({text.Length} chars)...";
                    if (item.ListItem != null) item.ListItem.SubItems[5].Text = item.StatusDetails;

                    _logger.Report($"[Batch] ({i + 1}/{totalFiles}) Synthesizing '{item.FileName}' -> '{item.ResolvedOutputName}'...");

                    try
                    {
                        await _engineService.StartConversionAsync(text, fileConfig);

                        if (ct.IsCancellationRequested) break;

                        item.Status = BatchItemStatus.Complete;
                        item.StatusDetails = "✓ Complete";
                        if (item.ListItem != null)
                        {
                            item.ListItem.SubItems[5].Text = item.StatusDetails;
                            item.ListItem.ForeColor = Color.FromArgb(20, 140, 50);
                        }
                        completedFiles++;
                    }
                    catch (Exception ex)
                    {
                        item.Status = BatchItemStatus.Failed;
                        item.StatusDetails = $"Synth Error: {ex.Message}";
                        if (item.ListItem != null)
                        {
                            item.ListItem.SubItems[5].Text = item.StatusDetails;
                            item.ListItem.ForeColor = Color.FromArgb(200, 30, 30);
                        }
                    }

                    int overallPct = (int)((i + 1) * 100.0 / totalFiles);
                    _pbOverall.Value = Math.Clamp(overallPct, 0, 100);
                }

                var batchElapsed = DateTime.Now - batchStartTime;

                if (ct.IsCancellationRequested)
                {
                    _lblOverallStatus.Text = $"Batch Cancelled. Completed {completedFiles} of {totalFiles} files in {batchElapsed:mm\\:ss}.";
                    _lblOverallStatus.ForeColor = Color.FromArgb(180, 100, 0);
                }
                else
                {
                    _lblOverallStatus.Text = $"✓ Batch Complete! Successfully processed {completedFiles} of {totalFiles} files in {batchElapsed:mm\\:ss}.";
                    _lblOverallStatus.ForeColor = Color.FromArgb(20, 140, 50);
                    _pbOverall.Value = 100;
                    _pbCurrent.Value = 100;
                    _lblCurrentStatus.Text = "All batch files completed.";

                    NotificationService.ShowNotification("Batch Complete", $"Processed {completedFiles} files into {outDir}");
                }
            }
            finally
            {
                _engineService.OnProgress -= progressHandler;
                _isRunning = false;
                SetUiRunningState(false);
            }
        }

        private void OnCancelBatchClick(object? sender, EventArgs e)
        {
            _activeCts?.Cancel();
            _engineService.Cancel();
            _lblOverallStatus.Text = "Cancelling batch processing...";
        }

        private void SetUiRunningState(bool running)
        {
            _btnStart.Enabled = !running;
            _btnCancel.Enabled = running;
            _btnAddFiles.Enabled = !running;
            _btnAddFolder.Enabled = !running;
            _btnRemoveSelected.Enabled = !running;
            _btnClearAll.Enabled = !running;
            _cboNamingPreset.Enabled = !running;
            _txtCustomTemplate.Enabled = !running;
            _btnBrowseOutputDir.Enabled = !running;
            _txtOutputDir.Enabled = !running;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        }
    }
}
