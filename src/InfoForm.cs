using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>Compact "About" window.</summary>
    internal sealed class InfoForm : Form
    {
        private readonly WincyApp _app;
        private readonly Palette _palette;
        private readonly Rectangle _linkRect;
        private readonly TextButton _close;
        private bool _linkHover;

        public InfoForm(WincyApp app)
        {
            _app = app;
            _palette = app.Palette;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = I18n.T("about.title");
            BackColor = _palette.Bg;
            Font = Fonts.Ui(9.5f);

            _close = new TextButton();
            _close.Label = I18n.T("about.close");
            _close.Primary = true;
            _close.Font = Fonts.Ui(9f);
            _close.Click += delegate { Close(); };
            _close.ApplyPalette(_palette);
            Controls.Add(_close);

            ClientSize = new Size(Dpi.S(420), Dpi.S(296));
            _linkRect = new Rectangle(Dpi.S(28), Dpi.S(232), Dpi.S(260), Dpi.S(22));
            _close.FitToLabel();
            _close.Bounds = new Rectangle(ClientSize.Width - Dpi.S(28) - Math.Max(Dpi.S(80), _close.Width),
                ClientSize.Height - Dpi.S(28) - Dpi.S(32), Math.Max(Dpi.S(80), _close.Width), Dpi.S(32));

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TrySetRoundedCorners(Handle);
            Native.TrySetDarkTitleBar(Handle, _palette.Dark);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
            {
                Close();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hover = _linkRect.Contains(e.Location);
            if (hover != _linkHover)
            {
                _linkHover = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate(_linkRect);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _linkRect.Contains(e.Location))
            {
                try
                {
                    Process.Start("https://github.com/p0deje/Maccy");
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not open the original Maccy repository: " + ex.Message);
                }
                return;
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);

            Icon icon = _app.AppIcon;
            if (icon != null)
            {
                using (Bitmap bitmap = icon.ToBitmap())
                {
                    Ui.DrawIconSized(g, bitmap, new Rectangle(Dpi.S(28), Dpi.S(28), Dpi.S(64), Dpi.S(64)));
                }
            }

            TextRenderer.DrawText(g, I18n.T("app.name"), Fonts.Bold(19f), new Rectangle(Dpi.S(108), Dpi.S(28), Dpi.S(280), Dpi.S(36)),
                _palette.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            string subtitle = I18n.T("app.subtitle") + "  ·  " + I18n.T("about.version", Version);
            TextRenderer.DrawText(g, subtitle, Fonts.Ui(9f), new Rectangle(Dpi.S(109), Dpi.S(62), Dpi.S(280), Dpi.S(22)),
                _palette.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, I18n.T("about.tagline"), Fonts.Ui(9.5f),
                new Rectangle(Dpi.S(28), Dpi.S(116), ClientSize.Width - Dpi.S(56), Dpi.S(24)),
                _palette.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            string hotkeyText = I18n.T("about.hotkey", _app.Settings.Hotkey == null ? "—" : _app.Settings.Hotkey.Pretty);
            TextRenderer.DrawText(g, hotkeyText, Fonts.Ui(9f),
                new Rectangle(Dpi.S(28), Dpi.S(146), ClientSize.Width - Dpi.S(56), Dpi.S(22)),
                _palette.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, Paths.Root, Fonts.Ui(8.25f),
                new Rectangle(Dpi.S(28), Dpi.S(170), ClientSize.Width - Dpi.S(56), Dpi.S(22)),
                _palette.TextFaint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, "github.com/p0deje/Maccy", Fonts.Ui(8.75f), _linkRect,
                _linkHover ? Ui.Mix(_palette.Accent, Color.White, 0.25) : _palette.Accent,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            using (Pen pen = new Pen(_palette.Separator, 1f))
            {
                g.DrawLine(pen, Dpi.S(28), Dpi.S(210), ClientSize.Width - Dpi.S(28), Dpi.S(210));
            }

            Ui.StrokeRound(g, _palette.Border, new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1),
                Dpi.S(12), 1f);
        }

        private static string Version
        {
            get
            {
                try
                {
                    System.Version version = Assembly.GetExecutingAssembly().GetName().Version;
                    if (version == null) return "1.0.0";
                    return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
                }
                catch
                {
                    return "1.0.0";
                }
            }
        }
    }
}
