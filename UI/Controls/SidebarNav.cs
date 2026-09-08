using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using KerkenezVoice.Languages;

namespace KerkenezVoice.UI.Controls
{
    public class SidebarNav : Panel
    {
        public const int ExpandedWidth = 168;
        public const int CollapsedWidth = 60;

        public event EventHandler<int>? TabChanged;
        public event EventHandler<bool>? CollapsedChanged;

        public static string GetTabTitle(int index) => index switch
        {
            0 => Lang.T(StringKeys.NavSynthesize),
            1 => Lang.T(StringKeys.NavEbookVoicer),
            2 => Lang.T(StringKeys.NavCustomVoices),
            3 => Lang.T(StringKeys.NavAudioFx),
            4 => Lang.T(StringKeys.NavLexicon),
            5 => Lang.T(StringKeys.NavSettings),
            6 => Lang.T(StringKeys.NavLiveLogs),
            _ => ""
        };

        private readonly string[] _tabTitles = new[]
        {
            "Synthesize",
            "Ebook Voicer",
            "Custom Voices",
            "Audio FX",
            "Lexicon",
            "Settings",
            "Live Logs"
        };

        private readonly string[] _tabIcons = new[]
        {
            "\uE768", // Play / Speech
            "\uE82D", // Book / Ebook
            "\uE77B", // People / Voices
            "\uE995", // Equalizer / Sliders
            "\uE8D4", // Lexicon / Dictionary
            "\uE713", // Settings gear
            "\uE700"  // Menu / Logs
        };

        private static string? _iconFontFamily;
        private static string GetIconFontFamily()
        {
            if (_iconFontFamily != null) return _iconFontFamily;

            try
            {
                using var installedFonts = new System.Drawing.Text.InstalledFontCollection();
                var set = new System.Collections.Generic.HashSet<string>(installedFonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                if (set.Contains("Segoe Fluent Icons")) return _iconFontFamily = "Segoe Fluent Icons";
                if (set.Contains("Segoe MDL2 Assets")) return _iconFontFamily = "Segoe MDL2 Assets";
            }
            catch { }

            return _iconFontFamily = "Segoe UI Symbol";
        }

        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;
        private bool _isToggleHovered = false;
        private bool _isCollapsed = false;

        private readonly System.Windows.Forms.Timer _animTimer;
        private int _startWidth;
        private int _targetWidth;
        private int _animFrame = 0;
        private const int TotalAnimFrames = 6; // ~90ms fast micro-animation

        private readonly ToolTip _toolTip;
        private string _currentToolTipText = "";

        private readonly Color _bgColor = Color.FromArgb(240, 242, 245);
        private readonly Color _activeBgColor = Color.FromArgb(255, 255, 255);
        private readonly Color _hoverBgColor = Color.FromArgb(230, 233, 238);
        private readonly Color _btnHoverBgColor = Color.FromArgb(220, 224, 230);
        private readonly Color _textColor = Color.FromArgb(50, 54, 62);
        private readonly Color _activeTextColor = Color.FromArgb(0, 102, 204);
        private readonly Color _accentColor = Color.FromArgb(0, 120, 215);
        private readonly Color _borderColor = Color.FromArgb(218, 222, 228);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value && value >= 0 && value < _tabTitles.Length)
                {
                    _selectedIndex = value;
                    Invalidate();
                    TabChanged?.Invoke(this, _selectedIndex);
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    StartAnimation();
                    CollapsedChanged?.Invoke(this, _isCollapsed);
                }
            }
        }

        private float CurrentScale => (this.DeviceDpi > 0 ? this.DeviceDpi : 96f) / 96f;

        public SidebarNav()
        {
            this.DoubleBuffered = true;
            this.Dock = DockStyle.Left;
            this.BackColor = _bgColor;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.Cursor = Cursors.Default;

            _animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animTimer.Tick += OnAnimTimerTick;

            _toolTip = new ToolTip
            {
                InitialDelay = 200,
                ReshowDelay = 100,
                AutoPopDelay = 3000,
                ShowAlways = true
            };

            float scale = CurrentScale;
            this.Width = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);

            LanguageManager.Instance.LanguageChanged += (s, e) => this.Invalidate();
        }

        public void ToggleCollapsed()
        {
            IsCollapsed = !IsCollapsed;
        }

        private void StartAnimation()
        {
            float scale = CurrentScale;
            _targetWidth = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);

            if (!this.IsHandleCreated || !this.Visible)
            {
                this.Width = _targetWidth;
                Invalidate();
                return;
            }

            _startWidth = this.Width;
            _animFrame = 0;
            _animTimer.Stop();
            _animTimer.Start();
        }

        private void OnAnimTimerTick(object? sender, EventArgs e)
        {
            _animFrame++;
            float t = (float)_animFrame / TotalAnimFrames;
            float ease = 1f - (float)Math.Pow(1f - t, 3);
            int currentW = (int)Math.Round(_startWidth + (_targetWidth - _startWidth) * ease);

            if (_animFrame >= TotalAnimFrames || currentW == _targetWidth)
            {
                _animTimer.Stop();
                this.Width = _targetWidth;
            }
            else
            {
                this.Width = currentW;
            }

            Invalidate();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            float scale = CurrentScale;
            this.Width = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);
            Invalidate();
        }

        private Rectangle GetToggleButtonBounds(float scale)
        {
            int sz = (int)(28 * scale);
            int headerH = (int)(56 * scale);
            bool isWide = !_isCollapsed && this.Width >= (int)(110 * scale);
            if (isWide)
            {
                return new Rectangle(this.Width - sz - (int)(10 * scale), (headerH - sz) / 2, sz, sz);
            }
            else
            {
                return new Rectangle((this.Width - sz) / 2, (headerH - sz) / 2, sz, sz);
            }
        }

        public Rectangle GetItemBounds(int index, float scale)
        {
            int headerH = (int)(56 * scale);
            int itemH = (int)(46 * scale);
            int itemY = headerH + index * itemH;
            return new Rectangle((int)(8 * scale), itemY, this.Width - (int)(16 * scale), itemH - (int)(4 * scale));
        }

        private void UpdateToolTip(string text, Point pt)
        {
            if (_currentToolTipText != text)
            {
                _currentToolTipText = text;
                if (string.IsNullOrEmpty(text))
                {
                    _toolTip.Hide(this);
                }
                else
                {
                    _toolTip.Show(text, this, pt.X + 16, pt.Y + 8, 3000);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float scale = CurrentScale;

            bool wasToggle = _isToggleHovered;
            _isToggleHovered = GetToggleButtonBounds(scale).Contains(e.Location);
            if (wasToggle != _isToggleHovered)
            {
                Invalidate();
            }

            if (_isToggleHovered)
            {
                this.Cursor = Cursors.Hand;
                UpdateToolTip(_isCollapsed ? Lang.T(StringKeys.NavTipExpandSidebar) : Lang.T(StringKeys.NavTipCollapseSidebar), e.Location);
                return;
            }

            int matchedIdx = -1;
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                if (GetItemBounds(i, scale).Contains(e.Location))
                {
                    matchedIdx = i;
                    break;
                }
            }

            if (_hoveredIndex != matchedIdx)
            {
                _hoveredIndex = matchedIdx;
                Invalidate();
            }

            if (matchedIdx >= 0)
            {
                this.Cursor = Cursors.Hand;
                if (_isCollapsed)
                {
                    UpdateToolTip(GetTabTitle(matchedIdx), e.Location);
                }
                else
                {
                    UpdateToolTip("", Point.Empty);
                }
            }
            else
            {
                this.Cursor = Cursors.Default;
                UpdateToolTip("", Point.Empty);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _isToggleHovered = false;
            UpdateToolTip("", Point.Empty);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            float scale = CurrentScale;

            if (e.Button == MouseButtons.Left)
            {
                var toggleRect = GetToggleButtonBounds(scale);
                if (toggleRect.Contains(e.Location) || _isToggleHovered)
                {
                    ToggleCollapsed();
                    return;
                }

                for (int i = 0; i < _tabTitles.Length; i++)
                {
                    if (GetItemBounds(i, scale).Contains(e.Location) || _hoveredIndex == i)
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = CurrentScale;
            bool isWide = !_isCollapsed && this.Width >= (int)(110 * scale);

            // Background
            using (var bgBrush = new SolidBrush(_bgColor))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Right border line
            using (var borderPen = new Pen(_borderColor, 1))
            {
                g.DrawLine(borderPen, this.Width - 1, 0, this.Width - 1, this.Height);
            }

            // Top Header: App Branding / Hamburger Toggle
            var toggleRect = GetToggleButtonBounds(scale);

            if (_isToggleHovered)
            {
                using var hoverBrush = new SolidBrush(_btnHoverBgColor);
                FillRoundedRectangle(g, hoverBrush, toggleRect, 4);
            }

            using (var icoFont = new Font(GetIconFontFamily(), 11F, FontStyle.Regular))
            using (var textBrush = new SolidBrush(_isToggleHovered ? _activeTextColor : _textColor))
            {
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("\uE700", icoFont, textBrush, toggleRect, sfCenter);
            }

            // App Title & Subtitle if Expanded
            if (isWide)
            {
                int textMaxWidth = toggleRect.Left - (int)(18 * scale);
                if (textMaxWidth > 20)
                {
                    using var titleFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    using var subFont = new Font("Segoe UI", 8F, FontStyle.Regular);
                    using var titleBrush = new SolidBrush(Color.FromArgb(25, 25, 25));
                    using var subBrush = new SolidBrush(Color.FromArgb(115, 120, 130));

                    var sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    var titleRect = new Rectangle((int)(14 * scale), (int)(10 * scale), textMaxWidth, (int)(20 * scale));
                    var subRect = new Rectangle((int)(14 * scale), (int)(31 * scale), textMaxWidth, (int)(16 * scale));

                    g.DrawString("Kerkenez", titleFont, titleBrush, titleRect, sfTitle);
                    g.DrawString("Voice", subFont, subBrush, subRect, sfTitle);
                }
            }

            // Draw Tabs
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                DrawTabItem(g, i, scale, isWide);
            }
        }

        private void DrawTabItem(Graphics g, int index, float scale, bool isWide)
        {
            var itemRect = GetItemBounds(index, scale);
            bool isSelected = (_selectedIndex == index);
            bool isHovered = (_hoveredIndex == index && !isSelected);

            // Background
            if (isSelected)
            {
                using var activeBrush = new SolidBrush(_activeBgColor);
                using var activeBorderPen = new Pen(_borderColor, 1);
                FillRoundedRectangle(g, activeBrush, itemRect, 5);
                DrawRoundedRectangle(g, activeBorderPen, itemRect, 5);

                // Left Accent Indicator
                using var accentBrush = new SolidBrush(_accentColor);
                g.FillRectangle(accentBrush, new Rectangle(itemRect.Left + 2, itemRect.Top + 6, isWide ? 4 : 3, itemRect.Height - 12));
            }
            else if (isHovered)
            {
                using var hoverBrush = new SolidBrush(_hoverBgColor);
                FillRoundedRectangle(g, hoverBrush, itemRect, 5);
            }

            var textColor = isSelected ? _activeTextColor : (isHovered ? Color.FromArgb(20, 24, 30) : _textColor);
            var fontStyle = isSelected ? FontStyle.Bold : FontStyle.Regular;
            using var itemFont = new Font("Segoe UI", 9.25F, fontStyle);
            using var textBrush = new SolidBrush(textColor);
            using var iconFont = new Font(GetIconFontFamily(), 11F, FontStyle.Regular);

            if (isWide)
            {
                int iconWidth = (int)(24 * scale);
                int iconLeft = itemRect.Left + (int)(12 * scale);
                var iconRect = new Rectangle(iconLeft, itemRect.Top, iconWidth, itemRect.Height);
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_tabIcons[index], iconFont, textBrush, iconRect, sfCenter);

                int textLeft = iconLeft + iconWidth + (int)(8 * scale);
                int textWidth = itemRect.Width - (textLeft - itemRect.Left) - (int)(4 * scale);
                var textRect = new Rectangle(textLeft, itemRect.Top, Math.Max(0, textWidth), itemRect.Height);
                var sfLeft = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(GetTabTitle(index), itemFont, textBrush, textRect, sfLeft);
            }
            else
            {
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_tabIcons[index], iconFont, textBrush, itemRect, sfCenter);
            }
        }

        private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using var path = CreateRoundedRectanglePath(rect, radius);
            g.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using var path = CreateRoundedRectanglePath(rect, radius);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
