using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>Settings window: history size, shortcut, behaviour toggles, theme, ignore list.</summary>
    internal sealed class SettingsForm : Form
    {
        private enum CaptionStyle
        {
            Title,
            Section,
            Row,
            Hint
        }

        private sealed class Caption
        {
            public Rectangle Bounds;
            public string Text;
            public CaptionStyle Style;
        }

        private readonly WincyApp _app;
        private readonly List<Caption> _captions = new List<Caption>();
        private Palette _palette;
        private int _contentBottom;
        private int _footerTop;
        private Rectangle _listBoxRect;
        private string _status;
        private bool _statusIsError;
        private DateTime _statusUntil = DateTime.MinValue;

        private Stepper _limit;
        private HotkeyBox _hotkey;
        private ToggleSwitch _autoPaste;
        private ToggleSwitch _plainByDefault;
        private ToggleSwitch _ignoreSensitive;
        private ToggleSwitch _launch;
        private ToggleSwitch _saveImages;
        private Segmented _theme;
        private TextBox _ignoreApps;
        private TextButton _done;
        private TextButton _openFolder;
        private TextButton _clear;
        private IconButton _closeButton;
        private bool _loading = true;

        public SettingsForm(WincyApp app)
        {
            _app = app;
            _palette = app.Palette;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            DoubleBuffered = true;
            Text = I18n.T("settings.title");
            Font = Fonts.Ui(9.5f);
            BackColor = _palette.Bg;

            BuildControls();
            LayoutSettings();
            CenterOnScreen();
            _loading = false;
        }

        private int PadX
        {
            get { return Dpi.S(26); }
        }

        private int Width2
        {
            get { return Dpi.S(520); }
        }

        private int HeaderHeight
        {
            get { return Dpi.S(54); }
        }

        private int FooterHeight
        {
            get { return Dpi.S(66); }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= Native.CS_DROPSHADOW;
                return cp;
            }
        }

        // ------------------------------------------------------------------ build

        private void BuildControls()
        {
            AppSettings settings = _app.Settings;

            _limit = new Stepper();
            _limit.Minimum = AppSettings.MinHistoryLimit;
            _limit.Maximum = AppSettings.MaxHistoryLimit;
            _limit.Step = 25;
            _limit.SetValueSilent(settings.HistoryLimit);
            _limit.ValueChanged += delegate
            {
                if (_loading) return;
                _app.Settings.HistoryLimit = _limit.Value;
                _app.Store.Limit = _limit.Value;
                _app.SaveSettings();
            };
            Controls.Add(_limit);

            _saveImages = new ToggleSwitch();
            _saveImages.Checked = settings.SaveImages;
            _saveImages.CheckedChanged += delegate
            {
                if (_loading) return;
                _app.Settings.SaveImages = _saveImages.Checked;
                _app.SaveSettings();
            };
            Controls.Add(_saveImages);

            _hotkey = new HotkeyBox();
            _hotkey.Value = settings.Hotkey;
            _hotkey.RecordingStarted += delegate { _app.SuspendHotkey(); };
            _hotkey.RecordingFinished += delegate { _app.ResumeHotkey(); };
            _hotkey.ValueChanged += delegate
            {
                if (_loading) return;
                string error;
                if (_app.TryChangeHotkey(_hotkey.Value, out error))
                {
                    SetStatus(I18n.T("settings.saved"), false);
                }
                else
                {
                    _hotkey.Value = _app.Settings.Hotkey;
                    SetStatus(error, true);
                }
            };
            Controls.Add(_hotkey);

            _autoPaste = MakeToggle(settings.AutoPaste, delegate(bool value)
            {
                _app.Settings.AutoPaste = value;
                _app.SaveSettings();
            });
            _plainByDefault = MakeToggle(settings.PastePlainByDefault, delegate(bool value)
            {
                _app.Settings.PastePlainByDefault = value;
                _app.SaveSettings();
            });
            _ignoreSensitive = MakeToggle(settings.IgnoreSensitive, delegate(bool value)
            {
                _app.Settings.IgnoreSensitive = value;
                _app.SaveSettings();
            });
            _launch = MakeToggle(settings.LaunchAtLogin, delegate(bool value)
            {
                _app.Settings.LaunchAtLogin = value;
                _app.SaveSettings();
                _app.ApplyAutoStart();
            });

            _theme = new Segmented(
                new string[] { I18n.T("theme.system"), I18n.T("theme.light"), I18n.T("theme.dark") },
                new string[] { "system", "light", "dark" });
            _theme.SelectSilent(settings.Theme);
            _theme.SelectionChanged += delegate
            {
                if (_loading) return;
                _app.Settings.Theme = _theme.SelectedValue;
                _app.SaveSettings();
                _app.RefreshTheme();
            };
            Controls.Add(_theme);

            _ignoreApps = new TextBox();
            _ignoreApps.Multiline = true;
            _ignoreApps.BorderStyle = BorderStyle.None;
            _ignoreApps.ScrollBars = ScrollBars.Vertical;
            _ignoreApps.Font = Fonts.Mono(9f);
            _ignoreApps.BackColor = _palette.BgInput;
            _ignoreApps.ForeColor = _palette.Text;
            _ignoreApps.WordWrap = false;
            _ignoreApps.HandleCreated += delegate { Native.TrySetDarkControlTheme(_ignoreApps.Handle, _palette.Dark); };
            _ignoreApps.AcceptsReturn = true;
            _ignoreApps.Text = AppSettings.ToMultiline(settings.IgnoreApps);
            _ignoreApps.TextChanged += delegate
            {
                if (_loading) return;
                List<string> apps = AppSettings.ParseAppList(_ignoreApps.Text);
                if (string.Equals(AppSettings.ToMultiline(apps), AppSettings.ToMultiline(_app.Settings.IgnoreApps),
                    StringComparison.Ordinal)) return;
                _app.Settings.IgnoreApps = apps;
                _app.SaveSettings();
            };
            Controls.Add(_ignoreApps);

            _openFolder = MakeTextButton(I18n.T("settings.openData"), false, false, delegate
            {
                try
                {
                    Process.Start("explorer.exe", "\"" + Paths.Root + "\"");
                }
                catch (Exception ex)
                {
                    Log.Error("Could not open data folder", ex);
                }
            });
            _openFolder.Font = Fonts.Ui(9f);

            _clear = MakeTextButton(I18n.T("settings.resetHistory"), false, true, ConfirmClear);
            _done = MakeTextButton(I18n.T("settings.done"), true, false, Close);
            _done.Font = Fonts.Ui(9.5f);

            _closeButton = new IconButton();
            _closeButton.Kind = GlyphKind.Close;
            _closeButton.Click += delegate { Close(); };
            _closeButton.Size = new Size(Dpi.S(28), Dpi.S(28));
            Controls.Add(_closeButton);

            ApplyPaletteToControls();
        }

        private ToggleSwitch MakeToggle(bool value, Action<bool> changed)
        {
            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Checked = value;
            toggle.CheckedChanged += delegate
            {
                if (_loading) return;
                changed(toggle.Checked);
            };
            Controls.Add(toggle);
            return toggle;
        }

        private TextButton MakeTextButton(string label, bool primary, bool danger, Action action)
        {
            TextButton button = new TextButton();
            button.Label = label;
            button.Primary = primary;
            button.Danger = danger;
            button.Click += delegate
            {
                if (action != null) action();
            };
            Controls.Add(button);
            return button;
        }

        private void ApplyPaletteToControls()
        {
            _limit.ApplyPalette(_palette);
            _saveImages.ApplyPalette(_palette);
            _hotkey.ApplyPalette(_palette);
            _autoPaste.ApplyPalette(_palette);
            _plainByDefault.ApplyPalette(_palette);
            _ignoreSensitive.ApplyPalette(_palette);
            _launch.ApplyPalette(_palette);
            _theme.ApplyPalette(_palette);
            _done.ApplyPalette(_palette);
            _openFolder.ApplyPalette(_palette);
            _clear.ApplyPalette(_palette);
            _closeButton.ApplyPalette(_palette);
            _ignoreApps.BackColor = _palette.BgInput;
            _ignoreApps.ForeColor = _palette.Text;
            Native.TrySetDarkControlTheme(_ignoreApps.Handle, _palette.Dark);
        }

        // ------------------------------------------------------------------ layout

        private int _y;

        private void Caption_section(string key)
        {
            Caption c = new Caption();
            c.Bounds = new Rectangle(PadX, _y, Width2 - PadX * 2, Dpi.S(20));
            c.Text = I18n.T(key).ToUpperInvariant();
            c.Style = CaptionStyle.Section;
            _captions.Add(c);
            _y += Dpi.S(26);
        }

        private void Caption_row(string text, string hint)
        {
            Caption c = new Caption();
            c.Bounds = new Rectangle(PadX, _y + Dpi.S(8), Width2 - PadX * 2 - Dpi.S(240), Dpi.S(22));
            c.Text = text;
            c.Style = CaptionStyle.Row;
            _captions.Add(c);
            if (!string.IsNullOrEmpty(hint))
            {
                Caption h = new Caption();
                h.Bounds = new Rectangle(PadX, _y + Dpi.S(28), Width2 - PadX * 2 - Dpi.S(120), Dpi.S(20));
                h.Text = hint;
                h.Style = CaptionStyle.Hint;
                _captions.Add(h);
            }
        }

        private void LayoutRow(Control control, int controlWidth, string label, string hint)
        {
            Caption_row(label, hint);
            int rowHeight = string.IsNullOrEmpty(hint) ? Dpi.S(38) : Dpi.S(54);
            int top = _y + (Dpi.S(38) - control.Height) / 2;
            control.Bounds = new Rectangle(Width2 - PadX - controlWidth, top, controlWidth, control.Height);
            _y += rowHeight;
        }

        private void LayoutSettings()
        {
            _captions.Clear();
            ClientSize = new Size(Width2, Dpi.S(760));
            _y = HeaderHeight + Dpi.S(4);

            Caption title = new Caption();
            title.Bounds = new Rectangle(PadX, Dpi.S(18), Width2 - Dpi.S(120), Dpi.S(30));
            title.Text = I18n.T("settings.title");
            title.Style = CaptionStyle.Title;
            _captions.Add(title);
            _closeButton.Bounds = new Rectangle(Width2 - PadX - Dpi.S(28), Dpi.S(18), Dpi.S(28), Dpi.S(28));

            Caption_section("settings.history");
            LayoutRow(_limit, Dpi.S(150), I18n.T("settings.limit"), I18n.T("settings.limitHint"));
            LayoutRow(_saveImages, Dpi.S(46), I18n.T("settings.saveImages"), I18n.T("settings.saveImagesHint"));
            _y += Dpi.S(6);

            Caption_section("settings.shortcut");
            LayoutRow(_hotkey, Dpi.S(190), I18n.T("settings.openHistory"), I18n.T("settings.shortcutHint"));
            _y += Dpi.S(6);

            Caption_section("settings.behavior");
            LayoutRow(_autoPaste, Dpi.S(46), I18n.T("settings.autoPaste"), I18n.T("settings.autoPasteHint"));
            LayoutRow(_plainByDefault, Dpi.S(46), I18n.T("settings.plainByDefault"), null);
            LayoutRow(_ignoreSensitive, Dpi.S(46), I18n.T("settings.ignorePassword"), I18n.T("settings.ignorePasswordHint"));
            LayoutRow(_launch, Dpi.S(46), I18n.T("settings.launch"), null);
            _y += Dpi.S(6);

            Caption_section("settings.appearance");
            LayoutRow(_theme, Dpi.S(216), I18n.T("settings.theme"), null);
            _y += Dpi.S(6);

            Caption_section("settings.ignoreApps");
            Caption ignoreHint = new Caption();
            ignoreHint.Bounds = new Rectangle(PadX, _y - Dpi.S(2), Width2 - PadX * 2, Dpi.S(20));
            ignoreHint.Text = I18n.T("settings.ignoreAppsHint");
            ignoreHint.Style = CaptionStyle.Hint;
            _captions.Add(ignoreHint);
            _y += Dpi.S(22);

            _listBoxRect = new Rectangle(PadX, _y, Width2 - PadX * 2, Dpi.S(92));
            _ignoreApps.Bounds = new Rectangle(_listBoxRect.Left + Dpi.S(10), _listBoxRect.Top + Dpi.S(8),
                _listBoxRect.Width - Dpi.S(20), _listBoxRect.Height - Dpi.S(16));
            _y += _listBoxRect.Height + Dpi.S(18);

            _contentBottom = _y;
            _footerTop = _contentBottom + Dpi.S(10);

            // Small screens: give up the ignore-list height first so the footer stays visible.
            int maxHeight = Math.Max(Dpi.S(420), Screen.FromPoint(Cursor.Position).WorkingArea.Height - Dpi.S(24));
            int totalHeight = _footerTop + FooterHeight;
            if (totalHeight > maxHeight)
            {
                int newBoxHeight = Math.Max(Dpi.S(48), _listBoxRect.Height - (totalHeight - maxHeight));
                int shift = _listBoxRect.Height - newBoxHeight;
                _listBoxRect = new Rectangle(_listBoxRect.X, _listBoxRect.Y, _listBoxRect.Width, newBoxHeight);
                _ignoreApps.Bounds = new Rectangle(_listBoxRect.Left + Dpi.S(10), _listBoxRect.Top + Dpi.S(8),
                    _listBoxRect.Width - Dpi.S(20), Math.Max(Dpi.S(24), newBoxHeight - Dpi.S(16)));
                _footerTop -= shift;
            }

            _openFolder.FitToLabel();
            _clear.FitToLabel();
            _done.FitToLabel();
            int buttonY = _footerTop + Dpi.S(18);
            _openFolder.Bounds = new Rectangle(PadX, buttonY, _openFolder.Width, Dpi.S(32));
            _clear.Bounds = new Rectangle(_openFolder.Right + Dpi.S(6), buttonY, _clear.Width, Dpi.S(32));
            _done.Bounds = new Rectangle(Width2 - PadX - Math.Max(Dpi.S(88), _done.Width), buttonY,
                Math.Max(Dpi.S(88), _done.Width), Dpi.S(32));

            ClientSize = new Size(Width2, _footerTop + FooterHeight);
            Invalidate();
        }

        private void CenterOnScreen()
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x = area.Left + (area.Width - Width) / 2;
            int y = area.Top + (area.Height - Height) / 2;
            Location = new Point(x, y);
        }

        // ------------------------------------------------------------------ behaviour

        private void SetStatus(string message, bool isError)
        {
            _status = message;
            _statusIsError = isError;
            _statusUntil = DateTime.UtcNow.AddSeconds(6);
            Invalidate();
        }

        private void ConfirmClear()
        {
            MiniMenu menu = new MiniMenu(_palette);
            menu.AddHeader(I18n.T("item.clearConfirm", _app.Store.Count));
            menu.AddDanger(I18n.T("item.clearAll"), null, delegate
            {
                _app.Store.Clear();
                SetStatus(I18n.T("msg.cleared"), false);
            });
            menu.Add(I18n.T("item.cancel"), null, delegate { });
            menu.ShowAt(new Point(_clear.PointToScreen(Point.Empty).X, _clear.PointToScreen(Point.Empty).Y - Dpi.S(8)));
            Activate();
        }

        public void RefreshPalette()
        {
            _palette = _app.Palette;
            BackColor = _palette.Bg;
            ApplyPaletteToControls();
            Invalidate(true);
        }

        /// <summary>Push the current settings into the controls without firing change handlers.</summary>
        public void ReloadValues()
        {
            bool wasLoading = _loading;
            _loading = true;
            try
            {
                _limit.SetValueSilent(_app.Settings.HistoryLimit);
                _saveImages.Checked = _app.Settings.SaveImages;
                _hotkey.Value = _app.Settings.Hotkey;
                _autoPaste.Checked = _app.Settings.AutoPaste;
                _plainByDefault.Checked = _app.Settings.PastePlainByDefault;
                _ignoreSensitive.Checked = _app.Settings.IgnoreSensitive;
                _launch.Checked = _app.Settings.LaunchAtLogin;
                _theme.SelectSilent(_app.Settings.Theme);
            }
            finally
            {
                _loading = wasLoading;
            }
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TrySetRoundedCorners(Handle);
            Native.TrySetDarkTitleBar(Handle, _palette.Dark);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == Native.WM_NCHITTEST)
            {
                int x = unchecked((short)(long)m.LParam);
                int y = unchecked((short)((long)m.LParam >> 16));
                Point client = PointToClient(new Point(x, y));
                if (client.Y < HeaderHeight && !_closeButton.Bounds.Contains(client))
                {
                    m.Result = (IntPtr)Native.HTCAPTION;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle full = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, full);

            for (int i = 0; i < _captions.Count; i++)
            {
                Caption c = _captions[i];
                switch (c.Style)
                {
                    case CaptionStyle.Title:
                        TextRenderer.DrawText(g, c.Text, Fonts.Bold(15f), c.Bounds, _palette.Text,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        break;
                    case CaptionStyle.Section:
                        TextRenderer.DrawText(g, c.Text, Fonts.Bold(8f), c.Bounds, _palette.TextFaint,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        break;
                    case CaptionStyle.Row:
                        TextRenderer.DrawText(g, c.Text, Fonts.Ui(9.75f), c.Bounds, _palette.Text,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                        break;
                    default:
                        TextRenderer.DrawText(g, c.Text, Fonts.Ui(8.25f), c.Bounds, _palette.TextMuted,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                        break;
                }
            }

            Ui.FillRound(g, _palette.BgInput, _listBoxRect, Dpi.S(8));
            Ui.StrokeRound(g, _palette.Border, new Rectangle(_listBoxRect.X, _listBoxRect.Y, _listBoxRect.Width - 1, _listBoxRect.Height - 1),
                Dpi.S(8), 1f);

            using (Pen pen = new Pen(_palette.Separator, 1f))
            {
                g.DrawLine(pen, PadX, _footerTop, ClientSize.Width - PadX, _footerTop);
            }

            if (_status != null && DateTime.UtcNow < _statusUntil)
            {
                TextRenderer.DrawText(g, _status, Fonts.Ui(8.25f),
                    new Rectangle(PadX, _footerTop - Dpi.S(26), ClientSize.Width - PadX * 2, Dpi.S(20)),
                    _statusIsError ? _palette.Danger : _palette.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }

            Rectangle border = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            Ui.StrokeRound(g, _palette.Border, border, Dpi.S(12), 1f);
        }
    }
}
