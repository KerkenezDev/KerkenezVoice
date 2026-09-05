using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using KerkenezVoice.Languages;

namespace KerkenezVoice.UI.Tabs
{
    public class LogsView : UserControl
    {
        private RichTextBox _rtbLog = null!;
        private Label _lblTitle = null!;
        private Button _btnCopy = null!;
        private Button _btnClear = null!;
        private readonly StringBuilder _logBuffer = new StringBuilder();

        public LogsView()
        {
            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            AppendLog("[*] Kerkenez Voice Live Activity Console initialized.");
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

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
                Text = Lang.T(StringKeys.LogsTitle),
                UseMnemonic = false,
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

            _btnCopy = new Button
            {
                Text = "📋 " + Lang.T(StringKeys.LogsBtnCopy),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(12 * scale), (int)(5 * scale), (int)(12 * scale), (int)(5 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnCopy.Click += OnCopyClick;

            _btnClear = new Button
            {
                Text = "🧹 " + Lang.T(StringKeys.LogsBtnClear),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(12 * scale), (int)(5 * scale), (int)(12 * scale), (int)(5 * scale)),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnClear.Click += (s, e) =>
            {
                _logBuffer.Clear();
                _rtbLog.Clear();
            };

            rightActions.Controls.Add(_btnCopy);
            rightActions.Controls.Add(_btnClear);

            topPanel.Controls.Add(_lblTitle);
            topPanel.Controls.Add(rightActions);

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(16 * scale))
            };

            var logCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(1)
            };

            logCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(60, 60, 60), 1);
                e.Graphics.DrawRectangle(p, 0, 0, logCard.Width - 1, logCard.Height - 1);
            };

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9.5F, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };

            logCard.Controls.Add(_rtbLog);
            contentPanel.Controls.Add(logCard);

            this.Controls.Add(contentPanel);
            this.Controls.Add(topPanel);
        }

        public void AppendLog(string message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendLog(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formatted = message.StartsWith("\r\n") 
                ? $"\r\n[{timestamp}] {message.TrimStart('\r', '\n')}\r\n" 
                : $"[{timestamp}] {message}\r\n";

            _logBuffer.Append(formatted);

            Color logColor = Color.FromArgb(220, 220, 220);
            if (message.Contains("[✓]") || message.Contains("ready", StringComparison.OrdinalIgnoreCase) || message.Contains("success", StringComparison.OrdinalIgnoreCase))
            {
                logColor = Color.FromArgb(100, 220, 100);
            }
            else if (message.Contains("[!]") || message.Contains("error", StringComparison.OrdinalIgnoreCase) || message.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                logColor = Color.FromArgb(255, 120, 120);
            }
            else if (message.Contains("[*]") || message.Contains("synthesis:", StringComparison.OrdinalIgnoreCase))
            {
                logColor = Color.FromArgb(100, 180, 255);
            }
            else if (message.Contains("warn", StringComparison.OrdinalIgnoreCase))
            {
                logColor = Color.FromArgb(240, 210, 100);
            }

            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionLength = 0;
            _rtbLog.SelectionColor = logColor;
            _rtbLog.AppendText(formatted);
            _rtbLog.ScrollToCaret();
        }

        private void OnCopyClick(object? sender, EventArgs e)
        {
            if (_rtbLog.TextLength > 0)
            {
                Clipboard.SetText(_rtbLog.Text);
                MessageBox.Show(this, Lang.T(StringKeys.LogsCopied), "Kerkenez Voice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void ApplyLocalization()
        {
            if (this.IsDisposed) return;
            if (_lblTitle != null) _lblTitle.Text = Lang.T(StringKeys.LogsTitle);
            if (_btnCopy != null) _btnCopy.Text = "📋 " + Lang.T(StringKeys.LogsBtnCopy);
            if (_btnClear != null) _btnClear.Text = "🧹 " + Lang.T(StringKeys.LogsBtnClear);
        }
    }
}
