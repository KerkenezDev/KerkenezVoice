using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;
using KokoroSharp.Core;
using NAudio.Wave;

namespace KerkenezVoice.UI.Tabs
{
    public class EbookVoicerView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly ModelManagerService _modelManager;
        private readonly KokoroEngineService _engineService;
        private readonly AudioPlaybackService _playbackService;
        private readonly EbookParserService _ebookParser;
        private readonly IProgress<string> _logger;

        private EbookMetadata? _currentBook;
        private EbookChapter? _selectedChapter;
        private EbookCleaningOptions _cleaningOptions = new();
        private CancellationTokenSource? _activeCts;
        private string? _lastOutputDirectory;

        // Progress & Live ETA Tracking
        private readonly Stopwatch _overallStopwatch = new();
        private int _totalSelectedWords = 0;
        private int _completedWordsPrior = 0;
        private int _currentChapterWords = 0;
        private int _currentChapterIndex = 0;
        private int _totalSelectedCount = 0;
        private string _currentChapterTitle = "";

        // Top Header
        private Panel _pnlHeader = null!;
        private Button _btnOpenEbook = null!;
        private PictureBox _pbCover = null!;
        private Label _lblBookTitle = null!;
        private Label _lblBookMeta = null!;
        private Label _lblTotalWords = null!;
        private Label _lblTotalDuration = null!;

        // Center SplitContainer
        private SplitContainer _splitMain = null!;

        // Left Panel: Chapter Navigator
        private ListView _lvChapters = null!;
        private Button _btnSelectAll = null!;
        private Button _btnSelectNone = null!;
        private Button _btnInvertSelection = null!;
        private Label _lblSelectionSummary = null!;

        // Right Panel: Text Editor & Cleaning Tools
        private Label _lblChapterEditorTitle = null!;
        private Label _lblChapterWordCount = null!;
        private CheckBox _chkFixHyphenation = null!;
        private CheckBox _chkStripPageNumbers = null!;
        private CheckBox _chkRemoveCitations = null!;
        private CheckBox _chkRemoveUrls = null!;
        private CheckBox _chkCleanLigatures = null!;
        private CheckBox _chkRemoveFootnotes = null!;
        private Button _btnRecleanChapter = null!;
        private Button _btnRecleanAll = null!;
        private Button _btnResetOriginal = null!;
        private TextBox _txtChapterContent = null!;

        // Bottom Studio Bar
        private ComboBox _cboVoice = null!;
        private TrackBar _tbSpeed = null!;
        private Label _lblSpeed = null!;
        private TrackBar _tbPitch = null!;
        private Label _lblPitch = null!;
        private NumericUpDown _numThreads = null!;

        private CheckBox _chkExportIndividual = null!;
        private CheckBox _chkExportCombined = null!;
        private CheckBox _chkGenerateSubtitles = null!;

        private ProgressBar _pbChapter = null!;
        private Label _lblChapterProgress = null!;
        private ProgressBar _pbOverall = null!;
        private Label _lblOverallProgress = null!;

        private Label _lblStatus = null!;
        private Button _btnVoiceChapters = null!;
        private Button _btnCancel = null!;
        private Button _btnOpenFolder = null!;

        private float CurrentScale => (this.DeviceDpi > 0 ? this.DeviceDpi : 96f) / 96f;

        public EbookVoicerView(
            ConfigService configService,
            ModelManagerService modelManager,
            KokoroEngineService engineService,
            AudioPlaybackService playbackService,
            EbookParserService ebookParser,
            IProgress<string> logger)
        {
            _configService = configService;
            _modelManager = modelManager;
            _engineService = engineService;
            _playbackService = playbackService;
            _ebookParser = ebookParser;
            _logger = logger;

            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            WireEngineEvents();
            LoadVoices();
            LoadConfigDefaults();
            ApplyLocalization();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = CurrentScale;

            // 1. Top Book Header Banner
            _pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(95 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale), (int)(10 * scale), (int)(16 * scale), (int)(10 * scale))
            };
            _pnlHeader.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, _pnlHeader.Height - 1, _pnlHeader.Width, _pnlHeader.Height - 1);
            };

            _btnOpenEbook = new Button
            {
                Text = "📂 Open Ebook...",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size((int)(135 * scale), (int)(38 * scale)),
                Location = new Point((int)(16 * scale), (int)(26 * scale)),
                Cursor = Cursors.Hand
            };
            _btnOpenEbook.FlatAppearance.BorderSize = 0;
            _btnOpenEbook.Click += OnOpenEbookClick;

            _pbCover = new PictureBox
            {
                Size = new Size((int)(54 * scale), (int)(72 * scale)),
                Location = new Point((int)(165 * scale), (int)(10 * scale)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(240, 242, 245)
            };

            _lblBookTitle = new Label
            {
                Text = "No ebook loaded",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Location = new Point((int)(230 * scale), (int)(14 * scale)),
                AutoSize = true
            };

            _lblBookMeta = new Label
            {
                Text = "Open an EPUB, PDF, or TXT file to inspect chapters, clean noise, and voice audiobooks.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point((int)(230 * scale), (int)(42 * scale)),
                AutoSize = true
            };

            var pnlHeaderStats = new Panel
            {
                Dock = DockStyle.Right,
                Width = (int)(250 * scale),
                BackColor = Color.Transparent
            };

            _lblTotalWords = new Label
            {
                Text = "Total Words: 0",
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 45, 55),
                Dock = DockStyle.Top,
                Height = (int)(28 * scale),
                TextAlign = ContentAlignment.MiddleRight
            };

            _lblTotalDuration = new Label
            {
                Text = "Est. Audio: 0h 0m",
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Dock = DockStyle.Top,
                Height = (int)(28 * scale),
                TextAlign = ContentAlignment.MiddleRight
            };

            pnlHeaderStats.Controls.Add(_lblTotalDuration);
            pnlHeaderStats.Controls.Add(_lblTotalWords);

            _pnlHeader.Controls.Add(pnlHeaderStats);
            _pnlHeader.Controls.Add(_lblBookTitle);
            _pnlHeader.Controls.Add(_lblBookMeta);
            _pnlHeader.Controls.Add(_pbCover);
            _pnlHeader.Controls.Add(_btnOpenEbook);

            // 2. Bottom Audio Studio Controls Bar
            var pnlBottom = CreateBottomStudioPanel(scale);

            // 3. Center SplitContainer (Chapter Navigator & Text Editor)
            int defaultDistance = _configService.Settings.EbookSplitterDistance > 0
                ? _configService.Settings.EbookSplitterDistance
                : (int)(380 * scale);

            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = Math.Clamp(defaultDistance, 250, 800),
                SplitterWidth = 6,
                BackColor = Color.FromArgb(235, 238, 242)
            };
            _splitMain.SplitterMoved += (s, e) =>
            {
                _configService.Settings.EbookSplitterDistance = _splitMain.SplitterDistance;
                _configService.SaveConfig();
            };

            // Setup Left Chapter Navigator
            SetupChapterNavigator(_splitMain.Panel1, scale);

            // Setup Right Text Preview & Editor
            SetupTextEditor(_splitMain.Panel2, scale);

            // Add all to view
            this.Controls.Add(_splitMain);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(_pnlHeader);
        }

        private void SetupChapterNavigator(Panel parent, float scale)
        {
            parent.BackColor = Color.White;
            parent.Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(10 * scale), (int)(10 * scale));

            var pnlTopNav = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(36 * scale),
                BackColor = Color.White
            };

            var lblChaptersHeader = new Label
            {
                Text = "Chapters & Sections",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(0, (int)(6 * scale), 0, 0)
            };

            var flowNavButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            _btnSelectAll = new Button
            {
                Text = "All",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                Size = new Size((int)(46 * scale), (int)(26 * scale)),
                Margin = new Padding((int)(2 * scale), (int)(2 * scale), (int)(2 * scale), 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 212);
            _btnSelectAll.Click += (s, e) => SetAllChaptersSelected(true);

            _btnSelectNone = new Button
            {
                Text = "None",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                Size = new Size((int)(52 * scale), (int)(26 * scale)),
                Margin = new Padding((int)(2 * scale), (int)(2 * scale), (int)(2 * scale), 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnSelectNone.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 212);
            _btnSelectNone.Click += (s, e) => SetAllChaptersSelected(false);

            _btnInvertSelection = new Button
            {
                Text = "Invert",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                Size = new Size((int)(56 * scale), (int)(26 * scale)),
                Margin = new Padding((int)(2 * scale), (int)(2 * scale), 0, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnInvertSelection.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 212);
            _btnInvertSelection.Click += (s, e) => InvertChaptersSelected();

            flowNavButtons.Controls.Add(_btnSelectAll);
            flowNavButtons.Controls.Add(_btnSelectNone);
            flowNavButtons.Controls.Add(_btnInvertSelection);

            pnlTopNav.Controls.Add(flowNavButtons);
            pnlTopNav.Controls.Add(lblChaptersHeader);

            _lblSelectionSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = (int)(26 * scale),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                Text = "0 of 0 chapters selected",
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lvChapters = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HideSelection = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                BackColor = Color.FromArgb(252, 253, 255)
            };

            _lvChapters.Columns.Add("#", (int)(42 * scale));
            _lvChapters.Columns.Add("Chapter Title", (int)(155 * scale));
            _lvChapters.Columns.Add("Words", (int)(55 * scale), HorizontalAlignment.Right);
            _lvChapters.Columns.Add("Duration", (int)(62 * scale), HorizontalAlignment.Right);
            _lvChapters.Columns.Add("Status", (int)(80 * scale), HorizontalAlignment.Left);

            _lvChapters.ItemChecked += OnChapterItemChecked;
            _lvChapters.SelectedIndexChanged += OnChapterSelectedIndexChanged;

            parent.Controls.Add(_lvChapters);
            parent.Controls.Add(_lblSelectionSummary);
            parent.Controls.Add(pnlTopNav);
        }

        private void SetupTextEditor(Panel parent, float scale)
        {
            parent.BackColor = Color.White;
            parent.Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(10 * scale));

            // Editor Header & Cleaning Options
            var pnlTopEditor = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(105 * scale),
                BackColor = Color.White
            };

            // Sub-row 1: Title and Word Count
            var pnlTitleRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(28 * scale),
                BackColor = Color.Transparent
            };

            _lblChapterEditorTitle = new Label
            {
                Text = "Chapter Content Preview & Editor",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Left,
                AutoSize = true
            };

            _lblChapterWordCount = new Label
            {
                Text = "Words: 0",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true
            };

            pnlTitleRow.Controls.Add(_lblChapterWordCount);
            pnlTitleRow.Controls.Add(_lblChapterEditorTitle);

            // Sub-row 2: Checkboxes
            var flowCheckboxes = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(32 * scale),
                BackColor = Color.Transparent,
                WrapContents = false,
                AutoScroll = false
            };

            _chkFixHyphenation = CreateCleaningCheckBox("Fix Hyphens", _cleaningOptions.FixHyphenation, scale);
            _chkStripPageNumbers = CreateCleaningCheckBox("Strip Page #", _cleaningOptions.StripPageNumbersAndHeaders, scale);
            _chkRemoveCitations = CreateCleaningCheckBox("Remove Citations", _cleaningOptions.RemoveCitations, scale);
            _chkRemoveUrls = CreateCleaningCheckBox("Remove URLs", _cleaningOptions.RemoveUrls, scale);
            _chkCleanLigatures = CreateCleaningCheckBox("Clean Ligatures", _cleaningOptions.NormalizeUnicodeAndLigatures, scale);
            _chkRemoveFootnotes = CreateCleaningCheckBox("Strip Footnotes", _cleaningOptions.RemoveFootnotes, scale);

            flowCheckboxes.Controls.Add(_chkFixHyphenation);
            flowCheckboxes.Controls.Add(_chkStripPageNumbers);
            flowCheckboxes.Controls.Add(_chkRemoveCitations);
            flowCheckboxes.Controls.Add(_chkRemoveUrls);
            flowCheckboxes.Controls.Add(_chkCleanLigatures);
            flowCheckboxes.Controls.Add(_chkRemoveFootnotes);

            // Sub-row 3: Clean Buttons Action Bar
            var flowCleanButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(36 * scale),
                BackColor = Color.Transparent,
                WrapContents = false
            };

            _btnRecleanChapter = new Button
            {
                Text = "↺ Clean Chapter",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                Size = new Size((int)(115 * scale), (int)(28 * scale)),
                BackColor = Color.FromArgb(235, 240, 250),
                ForeColor = Color.FromArgb(0, 102, 204),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, (int)(2 * scale), (int)(8 * scale), 0)
            };
            _btnRecleanChapter.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 240);
            _btnRecleanChapter.Click += (s, e) => RecleanCurrentChapter();

            _btnRecleanAll = new Button
            {
                Text = "↺ Clean All Chapters",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                Size = new Size((int)(135 * scale), (int)(28 * scale)),
                BackColor = Color.FromArgb(245, 247, 250),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, (int)(2 * scale), (int)(8 * scale), 0)
            };
            _btnRecleanAll.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 222);
            _btnRecleanAll.Click += (s, e) => RecleanAllChapters();

            _btnResetOriginal = new Button
            {
                Text = "Reset Original",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                Size = new Size((int)(95 * scale), (int)(28 * scale)),
                BackColor = Color.FromArgb(250, 245, 245),
                ForeColor = Color.FromArgb(160, 50, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, (int)(2 * scale), 0, 0)
            };
            _btnResetOriginal.FlatAppearance.BorderColor = Color.FromArgb(230, 200, 200);
            _btnResetOriginal.Click += (s, e) => ResetCurrentChapterToOriginal();

            flowCleanButtons.Controls.Add(_btnRecleanChapter);
            flowCleanButtons.Controls.Add(_btnRecleanAll);
            flowCleanButtons.Controls.Add(_btnResetOriginal);

            pnlTopEditor.Controls.Add(flowCleanButtons);
            pnlTopEditor.Controls.Add(flowCheckboxes);
            pnlTopEditor.Controls.Add(pnlTitleRow);

            // Multiline Textbox for Cleaned Text
            _txtChapterContent = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                BackColor = Color.FromArgb(255, 255, 255),
                ForeColor = Color.FromArgb(33, 37, 41)
            };
            _txtChapterContent.TextChanged += OnTextContentChanged;

            parent.Controls.Add(_txtChapterContent);
            parent.Controls.Add(pnlTopEditor);
        }

        private CheckBox CreateCleaningCheckBox(string text, bool isChecked, float scale)
        {
            var chk = new CheckBox
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.FromArgb(50, 55, 65),
                Margin = new Padding((int)(2 * scale), (int)(3 * scale), (int)(8 * scale), (int)(3 * scale)),
                Cursor = Cursors.Hand
            };
            chk.CheckedChanged += (s, e) => SyncCleaningOptionsFromUi();
            return chk;
        }

        private Panel CreateBottomStudioPanel(float scale)
        {
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(180 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale), (int)(8 * scale), (int)(16 * scale), (int)(8 * scale))
            };
            pnlBottom.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, 0, pnlBottom.Width, 0);
            };

            // Row 1: Voice, Speed, Pitch, Threads
            var pnlRow1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(38 * scale),
                BackColor = Color.Transparent,
                WrapContents = false
            };

            var lblVoice = new Label { Text = "Voice:", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, (int)(6 * scale), (int)(4 * scale), 0) };
            _cboVoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(130 * scale), Margin = new Padding(0, (int)(2 * scale), (int)(14 * scale), 0) };
            _cboVoice.SelectedIndexChanged += (s, e) => RecalculateAllEstimates();

            var lblSpeedH = new Label { Text = "Speed:", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, (int)(6 * scale), (int)(4 * scale), 0) };
            _tbSpeed = new TrackBar { Minimum = 5, Maximum = 20, Value = 10, TickFrequency = 5, Width = (int)(95 * scale), Margin = new Padding(0, 0, 0, 0) };
            _lblSpeed = new Label { Text = "1.0x", Width = (int)(38 * scale), Font = new Font("Segoe UI", 8.5F, FontStyle.Regular), Margin = new Padding((int)(2 * scale), (int)(6 * scale), (int)(10 * scale), 0) };
            _tbSpeed.Scroll += (s, e) =>
            {
                double speed = _tbSpeed.Value / 10.0;
                _lblSpeed.Text = $"{speed:0.0}x";
                RecalculateAllEstimates();
            };

            var lblPitchH = new Label { Text = "Pitch:", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, (int)(6 * scale), (int)(4 * scale), 0) };
            _tbPitch = new TrackBar { Minimum = 5, Maximum = 15, Value = 10, TickFrequency = 5, Width = (int)(85 * scale), Margin = new Padding(0, 0, 0, 0) };
            _lblPitch = new Label { Text = "1.0x", Width = (int)(38 * scale), Font = new Font("Segoe UI", 8.5F, FontStyle.Regular), Margin = new Padding((int)(2 * scale), (int)(6 * scale), (int)(10 * scale), 0) };
            _tbPitch.Scroll += (s, e) =>
            {
                double pitch = _tbPitch.Value / 10.0;
                _lblPitch.Text = $"{pitch:0.0}x";
            };

            var lblThreadsH = new Label { Text = "Threads:", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, (int)(6 * scale), (int)(4 * scale), 0) };
            _numThreads = new NumericUpDown { Minimum = 1, Maximum = 32, Value = Environment.ProcessorCount, Width = (int)(52 * scale), Margin = new Padding(0, (int)(2 * scale), (int)(14 * scale), 0) };

            pnlRow1.Controls.Add(lblVoice);
            pnlRow1.Controls.Add(_cboVoice);
            pnlRow1.Controls.Add(lblSpeedH);
            pnlRow1.Controls.Add(_tbSpeed);
            pnlRow1.Controls.Add(_lblSpeed);
            pnlRow1.Controls.Add(lblPitchH);
            pnlRow1.Controls.Add(_tbPitch);
            pnlRow1.Controls.Add(_lblPitch);
            pnlRow1.Controls.Add(lblThreadsH);
            pnlRow1.Controls.Add(_numThreads);

            // Row 2: Export options checkboxes
            var pnlRow2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(28 * scale),
                BackColor = Color.Transparent,
                WrapContents = false
            };

            _chkExportIndividual = new CheckBox { Text = "Export Individual Chapter WAVs", Checked = true, AutoSize = true, Margin = new Padding(0, (int)(2 * scale), (int)(16 * scale), 0), Font = new Font("Segoe UI", 8.5F) };
            _chkExportCombined = new CheckBox { Text = "Merge into Full Book WAV", Checked = true, AutoSize = true, Margin = new Padding(0, (int)(2 * scale), (int)(16 * scale), 0), Font = new Font("Segoe UI", 8.5F) };
            _chkGenerateSubtitles = new CheckBox { Text = "Generate Subtitles (.srt)", Checked = true, AutoSize = true, Margin = new Padding(0, (int)(2 * scale), 0, 0), Font = new Font("Segoe UI", 8.5F) };

            pnlRow2.Controls.Add(_chkExportIndividual);
            pnlRow2.Controls.Add(_chkExportCombined);
            pnlRow2.Controls.Add(_chkGenerateSubtitles);

            // Row 3: Dual Progress Bars with Live ETA
            var pnlRow3 = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(52 * scale),
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            pnlRow3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlRow3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            _lblChapterProgress = new Label
            {
                Text = "Current Chapter: Ready (ETA: --:--)",
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 40, 50),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            _lblOverallProgress = new Label
            {
                Text = "Overall Book: 0 of 0 chapters (0%) — ETA: --:--",
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };

            _pbChapter = new ProgressBar { Dock = DockStyle.Fill, Height = (int)(14 * scale), Value = 0 };
            _pbOverall = new ProgressBar { Dock = DockStyle.Fill, Height = (int)(14 * scale), Value = 0 };

            pnlRow3.Controls.Add(_lblChapterProgress, 0, 0);
            pnlRow3.Controls.Add(_lblOverallProgress, 1, 0);
            pnlRow3.Controls.Add(_pbChapter, 0, 1);
            pnlRow3.Controls.Add(_pbOverall, 1, 1);

            // Row 4: Action Buttons & Live Status
            var pnlRow4 = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(42 * scale),
                BackColor = Color.Transparent
            };

            _lblStatus = new Label
            {
                Text = "Ready to synthesize.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 75, 85),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var pnlActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = (int)(450 * scale),
                Height = (int)(42 * scale),
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent
            };

            _btnOpenFolder = new Button
            {
                Text = "📂 Open Output",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Size = new Size((int)(115 * scale), (int)(34 * scale)),
                BackColor = Color.FromArgb(240, 243, 248),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding((int)(6 * scale), 0, 0, 0)
            };
            _btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 215);
            _btnOpenFolder.Click += (s, e) => OpenOutputFolder();

            _btnCancel = new Button
            {
                Text = "⏹ Cancel",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Size = new Size((int)(85 * scale), (int)(34 * scale)),
                BackColor = Color.FromArgb(250, 240, 240),
                ForeColor = Color.FromArgb(180, 50, 50),
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                Cursor = Cursors.Hand,
                Margin = new Padding((int)(6 * scale), 0, 0, 0)
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(230, 190, 190);
            _btnCancel.Click += (s, e) => CancelSynthesis();

            _btnVoiceChapters = new Button
            {
                Text = "🎙️ Voice Selected Chapters",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size((int)(200 * scale), (int)(34 * scale)),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding((int)(6 * scale), 0, 0, 0)
            };
            _btnVoiceChapters.FlatAppearance.BorderSize = 0;
            _btnVoiceChapters.Click += OnVoiceSelectedChaptersClick;

            pnlActions.Controls.Add(_btnVoiceChapters);
            pnlActions.Controls.Add(_btnCancel);
            pnlActions.Controls.Add(_btnOpenFolder);

            pnlRow4.Controls.Add(_lblStatus);
            pnlRow4.Controls.Add(pnlActions);

            pnlBottom.Controls.Add(pnlRow4);
            pnlBottom.Controls.Add(pnlRow3);
            pnlBottom.Controls.Add(pnlRow2);
            pnlBottom.Controls.Add(pnlRow1);

            return pnlBottom;
        }

        #region Loading & Book Interaction

        private async void OnOpenEbookClick(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Ebook or Document to Voice",
                Filter = "All Supported Ebooks (*.epub;*.pdf;*.txt)|*.epub;*.pdf;*.txt|EPUB Books (*.epub)|*.epub|PDF Documents (*.pdf)|*.pdf|Plain Text (*.txt)|*.txt|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            string filePath = ofd.FileName;
            _btnOpenEbook.Enabled = false;
            _lblStatus.Text = Lang.T(StringKeys.EbookStatusParsing);
            _lblStatus.ForeColor = Color.FromArgb(0, 102, 204);

            try
            {
                SyncCleaningOptionsFromUi();

                EbookMetadata book = await Task.Run(() => _ebookParser.ParseEbook(filePath, _cleaningOptions));
                _currentBook = book;

                LoadBookIntoUi(book);

                _lblStatus.Text = Lang.T(StringKeys.EbookStatusReady);
                _lblStatus.ForeColor = Color.FromArgb(20, 140, 50);
                _logger.Report($"Parsed ebook '{book.Title}' ({book.Format}): {book.Chapters.Count} chapters, {book.TotalWordCount:N0} words.");
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Failed to parse ebook: {ex.Message}";
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                MessageBox.Show(this, $"Failed to load ebook:\n{ex.Message}", "Ebook Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _logger.Report($"[!] Ebook parse error: {ex.Message}");
            }
            finally
            {
                _btnOpenEbook.Enabled = true;
            }
        }

        private void LoadBookIntoUi(EbookMetadata book)
        {
            _lblBookTitle.Text = book.Title;
            _lblBookMeta.Text = $"by {book.Author}  •  {book.Format.ToString().ToUpperInvariant()}  •  {book.Chapters.Count} Chapters";

            // Cover Image
            if (book.CoverImage != null && book.CoverImage.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(book.CoverImage);
                    _pbCover.Image = Image.FromStream(ms);
                }
                catch
                {
                    _pbCover.Image = null;
                }
            }
            else
            {
                _pbCover.Image = null;
            }

            // Populate Chapters ListView
            _lvChapters.BeginUpdate();
            _lvChapters.Items.Clear();

            double speed = _tbSpeed.Value / 10.0;

            foreach (var ch in book.Chapters)
            {
                ch.RecalculateStats(speed);
                var lvi = new ListViewItem(ch.Index.ToString())
                {
                    Checked = ch.IsSelected,
                    Tag = ch
                };
                lvi.SubItems.Add(ch.Title);
                lvi.SubItems.Add($"{ch.WordCount:N0}");
                lvi.SubItems.Add(FormatTimeSpan(ch.EstimatedDuration));
                lvi.SubItems.Add(ch.StatusMessage);

                _lvChapters.Items.Add(lvi);
            }

            _lvChapters.EndUpdate();

            // Select the first chapter
            if (_lvChapters.Items.Count > 0)
            {
                _lvChapters.Items[0].Selected = true;
            }

            UpdateSelectionSummary();
        }

        private void OnChapterSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_lvChapters.SelectedItems.Count == 0)
                return;

            var lvi = _lvChapters.SelectedItems[0];
            if (lvi.Tag is EbookChapter chapter)
            {
                // Save any manual edits from currently active chapter
                SaveCurrentEditorText();

                _selectedChapter = chapter;
                _lblChapterEditorTitle.Text = $"Chapter {chapter.Index}: {chapter.Title}";
                _lblChapterWordCount.Text = $"Words: {chapter.WordCount:N0}  (~{FormatTimeSpan(chapter.EstimatedDuration)})";

                _txtChapterContent.Text = ToUiText(chapter.CleanedText);
            }
        }

        private void OnChapterItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            if (e.Item.Tag is EbookChapter chapter)
            {
                chapter.IsSelected = e.Item.Checked;
                UpdateSelectionSummary();
            }
        }

        private void OnTextContentChanged(object? sender, EventArgs e)
        {
            if (_selectedChapter != null)
            {
                _selectedChapter.CleanedText = FromUiText(_txtChapterContent.Text);
                _selectedChapter.RecalculateStats(_tbSpeed.Value / 10.0);
                _lblChapterWordCount.Text = $"Words: {_selectedChapter.WordCount:N0}  (~{FormatTimeSpan(_selectedChapter.EstimatedDuration)})";

                // Update corresponding listview item
                foreach (ListViewItem item in _lvChapters.Items)
                {
                    if (item.Tag == _selectedChapter)
                    {
                        item.SubItems[2].Text = $"{_selectedChapter.WordCount:N0}";
                        item.SubItems[3].Text = FormatTimeSpan(_selectedChapter.EstimatedDuration);
                        break;
                    }
                }

                UpdateSelectionSummary();
            }
        }

        private void SaveCurrentEditorText()
        {
            if (_selectedChapter != null)
            {
                _selectedChapter.CleanedText = FromUiText(_txtChapterContent.Text);
                _selectedChapter.RecalculateStats(_tbSpeed.Value / 10.0);
            }
        }

        private void SetAllChaptersSelected(bool selected)
        {
            _lvChapters.BeginUpdate();
            foreach (ListViewItem item in _lvChapters.Items)
            {
                item.Checked = selected;
                if (item.Tag is EbookChapter ch)
                    ch.IsSelected = selected;
            }
            _lvChapters.EndUpdate();
            UpdateSelectionSummary();
        }

        private void InvertChaptersSelected()
        {
            _lvChapters.BeginUpdate();
            foreach (ListViewItem item in _lvChapters.Items)
            {
                item.Checked = !item.Checked;
                if (item.Tag is EbookChapter ch)
                    ch.IsSelected = item.Checked;
            }
            _lvChapters.EndUpdate();
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            if (_currentBook == null)
            {
                _lblSelectionSummary.Text = "0 of 0 chapters selected";
                _lblTotalWords.Text = "Total Words: 0";
                _lblTotalDuration.Text = "Est. Audio: 0h 0m";
                return;
            }

            int selectedCount = _currentBook.Chapters.Count(c => c.IsSelected);
            int totalCount = _currentBook.Chapters.Count;
            int selectedWords = _currentBook.Chapters.Where(c => c.IsSelected).Sum(c => c.WordCount);
            double totalSeconds = _currentBook.Chapters.Where(c => c.IsSelected).Sum(c => c.EstimatedDuration.TotalSeconds);
            var duration = TimeSpan.FromSeconds(totalSeconds);

            _lblSelectionSummary.Text = $"{selectedCount} of {totalCount} chapters selected ({selectedWords:N0} words • ~{FormatTimeSpan(duration)})";
            _lblTotalWords.Text = $"Total Words: {_currentBook.TotalWordCount:N0}";
            _lblTotalDuration.Text = $"Est. Audio: {FormatTimeSpan(_currentBook.TotalDuration)}";
        }

        private void RecalculateAllEstimates()
        {
            if (_currentBook == null) return;
            double speed = _tbSpeed.Value / 10.0;

            _lvChapters.BeginUpdate();
            foreach (ListViewItem item in _lvChapters.Items)
            {
                if (item.Tag is EbookChapter ch)
                {
                    ch.RecalculateStats(speed);
                    item.SubItems[2].Text = $"{ch.WordCount:N0}";
                    item.SubItems[3].Text = FormatTimeSpan(ch.EstimatedDuration);
                }
            }
            _lvChapters.EndUpdate();

            if (_selectedChapter != null)
            {
                _lblChapterWordCount.Text = $"Words: {_selectedChapter.WordCount:N0}  (~{FormatTimeSpan(_selectedChapter.EstimatedDuration)})";
            }

            UpdateSelectionSummary();
        }

        #endregion

        #region Cleaning Actions

        private void SyncCleaningOptionsFromUi()
        {
            _cleaningOptions.FixHyphenation = _chkFixHyphenation.Checked;
            _cleaningOptions.StripPageNumbersAndHeaders = _chkStripPageNumbers.Checked;
            _cleaningOptions.RemoveCitations = _chkRemoveCitations.Checked;
            _cleaningOptions.RemoveUrls = _chkRemoveUrls.Checked;
            _cleaningOptions.NormalizeUnicodeAndLigatures = _chkCleanLigatures.Checked;
            _cleaningOptions.RemoveFootnotes = _chkRemoveFootnotes.Checked;
        }

        private void RecleanCurrentChapter()
        {
            if (_selectedChapter == null) return;
            SyncCleaningOptionsFromUi();

            _selectedChapter.CleanedText = EbookParserService.CleanText(_selectedChapter.RawText, _cleaningOptions);
            _selectedChapter.RecalculateStats(_tbSpeed.Value / 10.0);

            _txtChapterContent.Text = ToUiText(_selectedChapter.CleanedText);
            _lblChapterWordCount.Text = $"Words: {_selectedChapter.WordCount:N0}  (~{FormatTimeSpan(_selectedChapter.EstimatedDuration)})";

            foreach (ListViewItem item in _lvChapters.Items)
            {
                if (item.Tag == _selectedChapter)
                {
                    item.SubItems[2].Text = $"{_selectedChapter.WordCount:N0}";
                    item.SubItems[3].Text = FormatTimeSpan(_selectedChapter.EstimatedDuration);
                    break;
                }
            }

            UpdateSelectionSummary();
        }

        private void RecleanAllChapters()
        {
            if (_currentBook == null || _currentBook.Chapters.Count == 0) return;
            SyncCleaningOptionsFromUi();

            _lvChapters.BeginUpdate();
            foreach (var ch in _currentBook.Chapters)
            {
                ch.CleanedText = EbookParserService.CleanText(ch.RawText, _cleaningOptions);
                ch.RecalculateStats(_tbSpeed.Value / 10.0);
            }

            foreach (ListViewItem item in _lvChapters.Items)
            {
                if (item.Tag is EbookChapter ch)
                {
                    item.SubItems[2].Text = $"{ch.WordCount:N0}";
                    item.SubItems[3].Text = FormatTimeSpan(ch.EstimatedDuration);
                }
            }
            _lvChapters.EndUpdate();

            if (_selectedChapter != null)
            {
                _txtChapterContent.Text = ToUiText(_selectedChapter.CleanedText);
                _lblChapterWordCount.Text = $"Words: {_selectedChapter.WordCount:N0}  (~{FormatTimeSpan(_selectedChapter.EstimatedDuration)})";
            }

            UpdateSelectionSummary();
            _lblStatus.Text = "All chapters re-cleaned with updated options.";
            _lblStatus.ForeColor = Color.FromArgb(0, 102, 204);
        }

        private void ResetCurrentChapterToOriginal()
        {
            if (_selectedChapter == null) return;
            _selectedChapter.CleanedText = _selectedChapter.RawText;
            _selectedChapter.RecalculateStats(_tbSpeed.Value / 10.0);
            _txtChapterContent.Text = ToUiText(_selectedChapter.CleanedText);
            _lblChapterWordCount.Text = $"Words: {_selectedChapter.WordCount:N0}  (~{FormatTimeSpan(_selectedChapter.EstimatedDuration)})";
            UpdateSelectionSummary();
        }

        #endregion

        #region Audio Synthesis Pipeline

        private async void OnVoiceSelectedChaptersClick(object? sender, EventArgs e)
        {
            if (!_modelManager.AreModelsPresent())
            {
                MessageBox.Show(this, Lang.T(StringKeys.StatusModelsRequired), "Models Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_currentBook == null || _currentBook.Chapters.Count == 0)
            {
                MessageBox.Show(this, "Please open an ebook file first.", "No Book Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveCurrentEditorText();

            var selectedChapters = _currentBook.Chapters.Where(c => c.IsSelected && !string.IsNullOrWhiteSpace(c.CleanedText)).ToList();
            if (selectedChapters.Count == 0)
            {
                MessageBox.Show(this, "Please select at least one chapter with readable content.", "No Chapters Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Prepare Audio Output Directory
            string safeBookTitle = SanitizeFileName(string.IsNullOrWhiteSpace(_currentBook.Title) ? "Audiobook" : _currentBook.Title);
            string baseOutDir = _configService.Settings.GetEffectiveOutputDirectory();
            string bookOutDir = Path.Combine(baseOutDir, "Audiobooks", safeBookTitle);
            Directory.CreateDirectory(bookOutDir);
            _lastOutputDirectory = bookOutDir;

            // UI State
            _btnVoiceChapters.Enabled = false;
            _btnCancel.Enabled = true;
            _btnOpenEbook.Enabled = false;
            _pbOverall.Value = 0;
            _pbChapter.Value = 0;

            _activeCts = new CancellationTokenSource();
            var ct = _activeCts.Token;

            string selectedVoice = _cboVoice.SelectedItem?.ToString() ?? _configService.Settings.Voice;
            double speed = _tbSpeed.Value / 10.0;
            double pitch = _tbPitch.Value / 10.0;
            int threads = (int)_numThreads.Value;
            bool exportIndividual = _chkExportIndividual.Checked;
            bool exportCombined = _chkExportCombined.Checked;
            bool exportSubtitles = _chkGenerateSubtitles.Checked;

            _totalSelectedWords = selectedChapters.Sum(c => c.WordCount);
            _completedWordsPrior = 0;
            _totalSelectedCount = selectedChapters.Count;
            _overallStopwatch.Restart();

            var generatedChapterWavs = new List<string>();

            _logger.Report($"[📚] Starting Audiobook synthesis: {selectedChapters.Count} chapters into '{bookOutDir}'");

            try
            {
                for (int i = 0; i < selectedChapters.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    var chapter = selectedChapters[i];
                    _currentChapterIndex = i + 1;
                    _currentChapterTitle = chapter.Title;
                    _currentChapterWords = chapter.WordCount;

                    chapter.Status = EbookChapterStatus.Synthesizing;
                    chapter.StatusMessage = "Synthesizing...";
                    UpdateChapterStatusInListView(chapter);

                    int chapterNum = i + 1;
                    int overallInitialPct = _totalSelectedWords > 0
                        ? (int)((double)_completedWordsPrior / _totalSelectedWords * 100)
                        : (int)((double)i / selectedChapters.Count * 100);

                    _lblOverallProgress.Text = $"Overall Book: {i}/{selectedChapters.Count} complete ({overallInitialPct}%) — ETA: calculating... (Elapsed: {FormatTimeSpan(_overallStopwatch.Elapsed)})";
                    _pbOverall.Value = overallInitialPct;

                    _lblChapterProgress.Text = $"Chapter {chapterNum}/{selectedChapters.Count}: '{chapter.Title}' — 0% (ETA: calculating...)";
                    _pbChapter.Value = 0;

                    _lblStatus.Text = string.Format(Lang.T(StringKeys.EbookStatusSynthesizing), chapterNum, selectedChapters.Count, chapter.Title);
                    _lblStatus.ForeColor = Color.FromArgb(0, 102, 204);

                    // Setup Chapter Config
                    string safeChapterTitle = SanitizeFileName(chapter.Title);
                    string chapterBaseName = $"{chapter.Index:D2} - {safeChapterTitle}";

                    var chapterConfig = CloneConfig(_configService.Settings);
                    chapterConfig.Voice = selectedVoice;
                    chapterConfig.Speed = speed;
                    chapterConfig.Pitch = pitch;
                    chapterConfig.NumThreads = threads;
                    chapterConfig.OutDir = bookOutDir;
                    chapterConfig.Filename = chapterBaseName;
                    chapterConfig.Combine = true; // Output single unified wav per chapter
                    chapterConfig.Separate = false;
                    chapterConfig.ExportSubtitles = exportSubtitles;

                    // Synthesize chapter text
                    await _engineService.StartConversionAsync(chapter.CleanedText, chapterConfig);

                    if (ct.IsCancellationRequested) break;

                    // Locate output chapter file
                    string expectedWav = Path.Combine(bookOutDir, $"{chapterBaseName}.wav");
                    if (!File.Exists(expectedWav))
                    {
                        var matching = Directory.GetFiles(bookOutDir, $"{chapterBaseName}*.wav")
                            .OrderByDescending(f => File.GetCreationTime(f))
                            .FirstOrDefault();

                        if (matching != null)
                        {
                            try
                            {
                                if (File.Exists(expectedWav)) File.Delete(expectedWav);
                                File.Move(matching, expectedWav);
                            }
                            catch
                            {
                                expectedWav = matching;
                            }
                        }
                    }

                    if (File.Exists(expectedWav))
                    {
                        chapter.OutputPath = expectedWav;
                        generatedChapterWavs.Add(expectedWav);
                    }

                    chapter.Status = EbookChapterStatus.Complete;
                    chapter.StatusMessage = "✓ Complete";
                    UpdateChapterStatusInListView(chapter);

                    _completedWordsPrior += chapter.WordCount;
                    _pbChapter.Value = 100;
                    _logger.Report($"[✓] Chapter {chapterNum}/{selectedChapters.Count} complete: {chapter.Title}");
                }

                if (ct.IsCancellationRequested)
                {
                    _lblStatus.Text = Lang.T(StringKeys.EbookStatusCancelled);
                    _lblStatus.ForeColor = Color.FromArgb(180, 100, 0);
                    _logger.Report("[!] Audiobook synthesis cancelled by user.");
                    return;
                }

                // If Combine into Single Audiobook is checked:
                if (exportCombined && generatedChapterWavs.Count > 1)
                {
                    _lblStatus.Text = "Combining chapters into unified audiobook file...";
                    _lblStatus.ForeColor = Color.FromArgb(0, 102, 204);
                    _logger.Report($"[*] Concatenating {generatedChapterWavs.Count} chapter audio files into '{safeBookTitle}.wav'...");

                    string combinedBookWav = Path.Combine(bookOutDir, $"{safeBookTitle}_FullAudiobook.wav");
                    await Task.Run(() => ConcatenateWavFiles(generatedChapterWavs, combinedBookWav));

                    _logger.Report($"[✓] Combined audiobook created: {combinedBookWav}");
                }

                // If user didn't want individual files, clean up individual chapter WAVs
                if (!exportIndividual && exportCombined && generatedChapterWavs.Count > 1)
                {
                    foreach (var f in generatedChapterWavs)
                    {
                        try { if (File.Exists(f)) File.Delete(f); } catch { }
                    }
                }

                _overallStopwatch.Stop();
                _pbOverall.Value = 100;
                _pbChapter.Value = 100;
                _lblOverallProgress.Text = $"Overall Book: {selectedChapters.Count}/{selectedChapters.Count} complete (100%) — Total Time: {FormatTimeSpan(_overallStopwatch.Elapsed)}";
                _lblChapterProgress.Text = $"Chapter {selectedChapters.Count}/{selectedChapters.Count}: Complete";
                _lblStatus.Text = Lang.T(StringKeys.EbookStatusComplete);
                _lblStatus.ForeColor = Color.FromArgb(20, 140, 50);

                NotificationService.ShowNotification("Audiobook Ready", $"'{_currentBook.Title}' voiced successfully!");
            }
            catch (OperationCanceledException)
            {
                _overallStopwatch.Stop();
                _lblStatus.Text = Lang.T(StringKeys.EbookStatusCancelled);
                _lblStatus.ForeColor = Color.FromArgb(180, 100, 0);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Synthesis error: {ex.Message}";
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                _logger.Report($"[!] Synthesis error: {ex.Message}");
            }
            finally
            {
                _btnVoiceChapters.Enabled = true;
                _btnCancel.Enabled = false;
                _btnOpenEbook.Enabled = true;
            }
        }

        private void CancelSynthesis()
        {
            _activeCts?.Cancel();
            _engineService.Cancel();
            _lblStatus.Text = "Cancelling synthesis...";
            _lblStatus.ForeColor = Color.FromArgb(180, 100, 0);
        }

        private void OpenOutputFolder()
        {
            string? folder = _lastOutputDirectory;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = Path.Combine(_configService.Settings.GetEffectiveOutputDirectory(), "Audiobooks");
                Directory.CreateDirectory(folder);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open folder: {ex.Message}", "Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateChapterStatusInListView(EbookChapter chapter)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateChapterStatusInListView(chapter)));
                return;
            }

            foreach (ListViewItem item in _lvChapters.Items)
            {
                if (item.Tag == chapter)
                {
                    item.SubItems[4].Text = chapter.StatusMessage;
                    break;
                }
            }
        }

        private static void ConcatenateWavFiles(IEnumerable<string> sourceFiles, string destinationFile)
        {
            byte[] buffer = new byte[64 * 1024];
            WaveFileWriter? waveFileWriter = null;

            try
            {
                foreach (string sourceFile in sourceFiles)
                {
                    if (!File.Exists(sourceFile)) continue;

                    using var reader = new WaveFileReader(sourceFile);
                    if (waveFileWriter == null)
                    {
                        waveFileWriter = new WaveFileWriter(destinationFile, reader.WaveFormat);
                    }
                    else if (!reader.WaveFormat.Equals(waveFileWriter.WaveFormat))
                    {
                        throw new InvalidOperationException("Cannot concatenate WAV files with differing audio formats.");
                    }

                    int bytesRead;
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        waveFileWriter.Write(buffer, 0, bytesRead);
                    }
                }
            }
            finally
            {
                waveFileWriter?.Dispose();
            }
        }

        #endregion

        #region Engine Events & Localization

        private void WireEngineEvents()
        {
            _engineService.OnProgress += (percent, elapsed, eta, preview) =>
            {
                if (!this.IsHandleCreated || this.IsDisposed) return;

                this.BeginInvoke(new Action(() =>
                {
                    int val = (int)Math.Clamp(percent, 0, 100);
                    _pbChapter.Value = val;

                    // 1. Calculate and display Chapter ETA
                    string chapterEtaStr = !string.IsNullOrWhiteSpace(eta) && eta != "--:--" && eta != "00:00"
                        ? eta
                        : CalculateEta(elapsed, percent);

                    string previewSnippet = !string.IsNullOrWhiteSpace(preview) ? $"  {preview}" : "";
                    _lblChapterProgress.Text = $"Chapter {_currentChapterIndex}/{_totalSelectedCount}: '{_currentChapterTitle}' — {val}% (ETA: ~{chapterEtaStr}){previewSnippet}";

                    // 2. Calculate and display Overall Book ETA
                    double chapterFraction = Math.Clamp(percent / 100.0, 0.0, 1.0);
                    double currentChapterWordsDone = _currentChapterWords * chapterFraction;
                    double totalWordsDone = _completedWordsPrior + currentChapterWordsDone;

                    double overallFraction = _totalSelectedWords > 0
                        ? totalWordsDone / _totalSelectedWords
                        : (_totalSelectedCount > 0 ? (_currentChapterIndex - 1 + chapterFraction) / _totalSelectedCount : 0.0);

                    int overallVal = (int)Math.Clamp(overallFraction * 100.0, 0, 100);
                    _pbOverall.Value = overallVal;

                    double overallElapsedSec = _overallStopwatch.Elapsed.TotalSeconds;
                    string overallEtaStr = "--:--";
                    if (overallElapsedSec > 2.0 && totalWordsDone > 30)
                    {
                        double wordsPerSec = totalWordsDone / overallElapsedSec;
                        double wordsRemaining = Math.Max(0, _totalSelectedWords - totalWordsDone);
                        double remSeconds = wordsPerSec > 0 ? wordsRemaining / wordsPerSec : 0.0;
                        overallEtaStr = FormatTimeSpan(TimeSpan.FromSeconds(remSeconds));
                    }

                    _lblOverallProgress.Text = $"Overall Book: {_currentChapterIndex - 1}/{_totalSelectedCount} complete ({overallVal}%) — ETA: ~{overallEtaStr} (Elapsed: {FormatTimeSpan(_overallStopwatch.Elapsed)})";
                }));
            };
        }

        private static string CalculateEta(TimeSpan elapsed, double percent)
        {
            if (percent > 1.0 && elapsed.TotalSeconds > 1.0)
            {
                double totalEstSec = (elapsed.TotalSeconds / percent) * 100.0;
                double remSec = Math.Max(0, totalEstSec - elapsed.TotalSeconds);
                return FormatTimeSpan(TimeSpan.FromSeconds(remSec));
            }
            return "--:--";
        }

        public void LoadVoices()
        {
            try
            {
                _cboVoice.Items.Clear();
                if (!_modelManager.AreModelsPresent()) return;

                var voices = _modelManager.LoadVoices();
                foreach (var v in voices)
                {
                    _cboVoice.Items.Add(v.Name);
                }

                string cur = _configService.Settings.Voice;
                int idx = GenerateView.FindVoiceIndex(_cboVoice, cur);
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
                _logger.Report($"[!] Error loading voices in Ebook Voicer: {ex.Message}");
            }
        }

        private void LoadConfigDefaults()
        {
            var s = _configService.Settings;

            int speedVal = (int)Math.Round(s.Speed * 10);
            _tbSpeed.Value = Math.Clamp(speedVal, 5, 20);
            _lblSpeed.Text = $"{s.Speed:0.0}x";

            int pitchVal = (int)Math.Round(s.Pitch * 10);
            _tbPitch.Value = Math.Clamp(pitchVal, 5, 15);
            _lblPitch.Text = $"{s.Pitch:0.0}x";

            _numThreads.Value = Math.Clamp(s.NumThreads, 1, 32);
        }

        private void ApplyLocalization()
        {
            _btnOpenEbook.Text = Lang.T(StringKeys.EbookBtnOpen);
            _btnVoiceChapters.Text = Lang.T(StringKeys.EbookVoiceSelected);
            _btnCancel.Text = Lang.T(StringKeys.EbookBtnCancel);
            _btnOpenFolder.Text = Lang.T(StringKeys.EbookBtnOpenFolder);

            _chkExportIndividual.Text = Lang.T(StringKeys.EbookOptExportIndividual);
            _chkExportCombined.Text = Lang.T(StringKeys.EbookOptExportCombined);
            _chkGenerateSubtitles.Text = Lang.T(StringKeys.EbookOptGenerateSubtitles);

            _chkFixHyphenation.Text = Lang.T(StringKeys.EbookOptHyphenation);
            _chkStripPageNumbers.Text = Lang.T(StringKeys.EbookOptPageNumbers);
            _chkRemoveCitations.Text = Lang.T(StringKeys.EbookOptCitations);
            _chkRemoveUrls.Text = Lang.T(StringKeys.EbookOptUrls);
            _chkCleanLigatures.Text = Lang.T(StringKeys.EbookOptLigatures);
            _chkRemoveFootnotes.Text = Lang.T(StringKeys.EbookOptFootnotes);

            bool isTr = LanguageManager.Instance.CurrentLanguage.Code.Equals("tr", StringComparison.OrdinalIgnoreCase);
            _btnRecleanChapter.Text = isTr ? "↺ Bölümü Temizle" : "↺ Clean Chapter";
            _btnRecleanAll.Text = isTr ? "↺ Tümünü Temizle" : "↺ Clean All Chapters";
            _btnResetOriginal.Text = isTr ? "Sıfırla" : "Reset Original";

            _btnSelectAll.Text = isTr ? "Tümü" : "All";
            _btnSelectNone.Text = isTr ? "Hiçi" : "None";
            _btnInvertSelection.Text = isTr ? "Ters" : "Invert";

            if (_currentBook == null)
            {
                _lblBookTitle.Text = Lang.T(StringKeys.EbookNoBookLoaded);
                _lblBookMeta.Text = Lang.T(StringKeys.EbookSelectBookPrompt);
                _lblStatus.Text = Lang.T(StringKeys.EbookStatusReady);
            }
        }

        #endregion

        #region Helpers

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1.0)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            }
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        }

        private static string SanitizeFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            string safe = Regex.Replace(name, invalidRegStr, "_");
            return safe.Length > 80 ? safe.Substring(0, 80).Trim() : safe.Trim();
        }

        private static string ToUiText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }

        private static string FromUiText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static AppSettings CloneConfig(AppSettings src)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(src);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }

        #endregion
    }
}
