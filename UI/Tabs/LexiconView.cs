using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Models;
using KerkenezVoice.Services;

namespace KerkenezVoice.UI.Tabs
{
    public class LexiconView : UserControl
    {
        private readonly ConfigService _configService;
        private TextBox _txtOriginal = null!;
        private TextBox _txtReplacement = null!;
        private Button _btnAdd = null!;
        private ListView _lvRules = null!;
        private Button _btnDelete = null!;
        private Label _lblTitle = null!;
        private Label _lblNote = null!;
        private Label _lblOriginal = null!;
        private Label _lblReplacement = null!;

        public LexiconView(ConfigService configService)
        {
            _configService = configService;
            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            LoadRules();
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
                Text = Lang.T(StringKeys.LexTitle),
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

            _btnDelete = new Button
            {
                Text = "🗑️ " + Lang.T(StringKeys.LexBtnDelete),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(12 * scale), (int)(5 * scale), (int)(12 * scale), (int)(5 * scale)),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnDelete.Click += OnDeleteSelectedRule;

            rightActions.Controls.Add(_btnDelete);
            topPanel.Controls.Add(_lblTitle);
            topPanel.Controls.Add(rightActions);

            // 2. Content Container
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(16 * scale))
            };

            // Card 1: Add rule form
            var addCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(80 * scale),
                BackColor = Color.White,
                Padding = new Padding((int)(14 * scale)),
                Margin = new Padding(0, 0, 0, (int)(12 * scale))
            };

            addCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, addCard.Width - 1, addCard.Height - 1);
            };

            _lblOriginal = new Label
            {
                Text = Lang.T(StringKeys.LexOriginal),
                AutoSize = true,
                Location = new Point((int)(12 * scale), (int)(12 * scale)),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _txtOriginal = new TextBox
            {
                Location = new Point((int)(12 * scale), (int)(34 * scale)),
                Width = (int)(220 * scale)
            };

            _lblReplacement = new Label
            {
                Text = Lang.T(StringKeys.LexReplacement),
                AutoSize = true,
                Location = new Point((int)(246 * scale), (int)(12 * scale)),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _txtReplacement = new TextBox
            {
                Location = new Point((int)(246 * scale), (int)(34 * scale)),
                Width = (int)(220 * scale)
            };

            _btnAdd = new Button
            {
                Text = "➕ " + Lang.T(StringKeys.LexBtnAdd),
                Location = new Point((int)(480 * scale), (int)(32 * scale)),
                Height = (int)(27 * scale),
                Width = (int)(110 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnAdd.Click += OnAddRule;

            addCard.Controls.Add(_lblOriginal);
            addCard.Controls.Add(_txtOriginal);
            addCard.Controls.Add(_lblReplacement);
            addCard.Controls.Add(_txtReplacement);
            addCard.Controls.Add(_btnAdd);

            // Card 2: Rules List
            var listCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(1)
            };

            listCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, listCard.Width - 1, listCard.Height - 1);
            };

            _lvRules = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BorderStyle = BorderStyle.None,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5F)
            };

            _lvRules.Columns.Add("Original Text", (int)(280 * scale));
            _lvRules.Columns.Add("Pronounce As", (int)(340 * scale));

            _lvRules.SelectedIndexChanged += (s, e) =>
            {
                _btnDelete.Enabled = _lvRules.SelectedItems.Count > 0;
            };

            listCard.Controls.Add(_lvRules);

            // Bottom Note
            var botPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(36 * scale),
                Padding = new Padding((int)(8 * scale), (int)(8 * scale), 0, 0)
            };

            _lblNote = new Label
            {
                Text = Lang.T(StringKeys.LexNote),
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                Dock = DockStyle.Fill
            };

            botPanel.Controls.Add(_lblNote);

            container.Controls.Add(listCard);
            container.Controls.Add(botPanel);
            container.Controls.Add(addCard);

            this.Controls.Add(container);
            this.Controls.Add(topPanel);
        }

        private void LoadRules()
        {
            _lvRules.Items.Clear();
            var dict = _configService.Settings.Lexicon;
            if (dict == null) return;

            foreach (var kv in dict)
            {
                var item = new ListViewItem(kv.Key);
                item.SubItems.Add(kv.Value);
                _lvRules.Items.Add(item);
            }
        }

        private void OnAddRule(object? sender, EventArgs e)
        {
            string orig = _txtOriginal.Text.Trim();
            string repl = _txtReplacement.Text.Trim();

            if (string.IsNullOrWhiteSpace(orig)) return;

            _configService.Settings.Lexicon[orig] = repl;
            _configService.SaveConfig();

            _txtOriginal.Clear();
            _txtReplacement.Clear();
            LoadRules();
        }

        private void OnDeleteSelectedRule(object? sender, EventArgs e)
        {
            if (_lvRules.SelectedItems.Count == 0) return;

            string orig = _lvRules.SelectedItems[0].Text;
            if (_configService.Settings.Lexicon.ContainsKey(orig))
            {
                _configService.Settings.Lexicon.Remove(orig);
                _configService.SaveConfig();
                LoadRules();
            }
        }

        private void ApplyLocalization()
        {
            if (_lblTitle != null) _lblTitle.Text = Lang.T(StringKeys.LexTitle);
            if (_lblOriginal != null) _lblOriginal.Text = Lang.T(StringKeys.LexOriginal);
            if (_lblReplacement != null) _lblReplacement.Text = Lang.T(StringKeys.LexReplacement);
            if (_btnAdd != null) _btnAdd.Text = "➕ " + Lang.T(StringKeys.LexBtnAdd);
            if (_btnDelete != null) _btnDelete.Text = "🗑️ " + Lang.T(StringKeys.LexBtnDelete);
            if (_lblNote != null) _lblNote.Text = Lang.T(StringKeys.LexNote);
        }
    }
}
