using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>
    /// Lightweight popup menu (tray menu, item context menu, confirmation prompts) that
    /// matches the popup's look instead of the dated default ToolStrip rendering.
    /// </summary>
    internal sealed class MiniMenu : Form
    {
        private sealed class Entry
        {
            public string Text;
            public string Hint;
            public Action Action;
            public bool Checked;
            public bool Enabled = true;
            public bool Separator;
            public bool Danger;
            public bool Header;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private Palette _palette;
        private int _hover = -1;
        private bool _closing;

        public MiniMenu(Palette palette)
        {
            _palette = palette;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            Font = Fonts.Ui(9.5f);
            BackColor = palette.Bg;
            // Never shown to the user (borderless, tool window), but useful for automation
            // and for accessibility tools that list windows by title.
            Text = "Wincy Menu";
        }

        public Action ClosedAction;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= Native.CS_DROPSHADOW;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        public MiniMenu Add(string text, string hint, Action action)
        {
            Entry e = new Entry();
            e.Text = text;
            e.Hint = hint;
            e.Action = action;
            _entries.Add(e);
            return this;
        }

        public MiniMenu AddCheck(string text, bool isChecked, Action action)
        {
            Entry e = new Entry();
            e.Text = text;
            e.Checked = isChecked;
            e.Action = action;
            _entries.Add(e);
            return this;
        }

        public MiniMenu AddHeader(string text)
        {
            Entry e = new Entry();
            e.Text = text;
            e.Header = true;
            e.Enabled = false;
            _entries.Add(e);
            return this;
        }

        public MiniMenu AddDanger(string text, string hint, Action action)
        {
            Entry e = new Entry();
            e.Text = text;
            e.Hint = hint;
            e.Danger = true;
            e.Action = action;
            _entries.Add(e);
            return this;
        }

        public MiniMenu AddSeparator()
        {
            Entry e = new Entry();
            e.Separator = true;
            _entries.Add(e);
            return this;
        }


        private int RowHeight
        {
            get { return Dpi.S(30); }
        }

        private int PadH
        {
            get { return Dpi.S(12); }
        }

        private int PadV
        {
            get { return Dpi.S(6); }
        }

        private int CheckWidth
        {
            get { return Dpi.S(20); }
        }

        private Size Measure()
        {
            int textWidth = 0;
            int hintWidth = 0;
            using (Graphics g = CreateGraphics())
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry e = _entries[i];
                    if (e.Separator) continue;
                    Font font = e.Header ? Fonts.Bold(8.5f) : Font;
                    textWidth = Math.Max(textWidth, Ui.MeasureWidth(g, font, e.Text));
                    if (!string.IsNullOrEmpty(e.Hint))
                    {
                        hintWidth = Math.Max(hintWidth, Ui.MeasureWidth(g, Fonts.Ui(8.5f), e.Hint) + Dpi.S(28));
                    }
                }
            }
            int width = PadH * 2 + CheckWidth + textWidth + hintWidth + Dpi.S(16);
            width = Math.Max(Dpi.S(190), Math.Min(Dpi.S(360), width));

            int height = PadV * 2;
            for (int i = 0; i < _entries.Count; i++)
            {
                height += _entries[i].Separator ? Dpi.S(9) : RowHeight;
            }
            return new Size(width, height);
        }

        public void ShowAt(Point screenPoint)
        {
            Size size = Measure();
            Rectangle work = Screen.FromPoint(screenPoint).WorkingArea;
            int x = screenPoint.X;
            int y = screenPoint.Y;
            if (x + size.Width > work.Right) x = work.Right - size.Width - Dpi.S(4);
            if (y + size.Height > work.Bottom) y = screenPoint.Y - size.Height;
            if (x < work.Left) x = work.Left;
            if (y < work.Top) y = work.Top;
            Bounds = new Rectangle(x, y, size.Width, size.Height);
            Show();
            Activate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TrySetRoundedCorners(Handle);
            Native.TrySetDarkTitleBar(Handle, _palette.Dark);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _closing = true;
            Action action = ClosedAction;
            ClosedAction = null;
            if (action != null)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error("MiniMenu close action failed", ex);
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Painted in OnPaint to avoid a flash of the default background.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, bounds);
            Ui.StrokeRound(g, _palette.Border, new Rectangle(0, 0, Width - 1, Height - 1), Dpi.S(10), 1f);

            int y = PadV;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Separator)
                {
                    int lineY = y + Dpi.S(4);
                    using (Pen pen = new Pen(_palette.Separator, 1f))
                    {
                        g.DrawLine(pen, PadH, lineY, Width - PadH, lineY);
                    }
                    y += Dpi.S(9);
                    continue;
                }

                Rectangle row = new Rectangle(Dpi.S(6), y, Width - Dpi.S(12), RowHeight);
                if (entry.Enabled && i == _hover) Ui.FillRound(g, _palette.BgHover, row, Dpi.S(6));

                Font font = entry.Header ? Fonts.Bold(8.5f) : Font;
                Color color = entry.Header
                    ? _palette.TextMuted
                    : (entry.Enabled ? (entry.Danger ? _palette.Danger : _palette.Text) : _palette.TextFaint);

                if (entry.Checked)
                {
                    Rectangle checkRect = new Rectangle(row.Left + Dpi.S(6), row.Top + (RowHeight - Dpi.S(14)) / 2,
                        Dpi.S(14), Dpi.S(14));
                    Glyph.Check(g, checkRect, _palette.Accent, 1.8f);
                }

                int textLeft = row.Left + PadH + (entry.Header ? 0 : CheckWidth);
                int hintWidth = string.IsNullOrEmpty(entry.Hint) ? 0 : Ui.MeasureWidth(g, Fonts.Ui(8.5f), entry.Hint) + Dpi.S(12);
                Rectangle textRect = new Rectangle(textLeft, row.Top, Width - textLeft - PadH - hintWidth, RowHeight);
                TextRenderer.DrawText(g, entry.Text, font, textRect, color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

                if (hintWidth > 0)
                {
                    Rectangle hintRect = new Rectangle(Width - PadH - hintWidth, row.Top, hintWidth, RowHeight);
                    TextRenderer.DrawText(g, entry.Hint, Fonts.Ui(8.5f), hintRect, _palette.TextFaint,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                        TextFormatFlags.NoPadding);
                }
                y += RowHeight;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = IndexAt(e.Y);
            if (index != _hover)
            {
                _hover = index;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1)
            {
                _hover = -1;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                Close();
                return;
            }
            int index = IndexAt(e.Y);
            if (index >= 0) ActivateEntry(index);
            base.OnMouseDown(e);
        }

        private void ActivateEntry(int index)
        {
            if (_closing) return;
            Entry entry = _entries[index];
            if (entry == null || !entry.Enabled || entry.Action == null)
            {
                Close();
                return;
            }
            Action action = entry.Action;
            _closing = true;
            Close();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error("Menu action failed", ex);
            }
        }

        private int IndexAt(int y)
        {
            int top = PadV;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                int height = entry.Separator ? Dpi.S(9) : RowHeight;
                if (y >= top && y < top + height) return entry.Separator || !entry.Enabled ? -1 : i;
                top += height;
            }
            return -1;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    Close();
                    e.Handled = true;
                    return;
                case Keys.Down:
                    MoveHover(1);
                    e.Handled = true;
                    return;
                case Keys.Up:
                    MoveHover(-1);
                    e.Handled = true;
                    return;
                case Keys.Enter:
                    if (_hover >= 0) ActivateEntry(_hover);
                    e.Handled = true;
                    return;
            }
            base.OnKeyDown(e);
        }

        private void MoveHover(int direction)
        {
            if (_entries.Count == 0) return;
            int start = _hover < 0 ? (direction > 0 ? -1 : _entries.Count) : _hover;
            int index = start;
            for (int i = 0; i < _entries.Count; i++)
            {
                index += direction;
                if (index < 0) index = _entries.Count - 1;
                if (index >= _entries.Count) index = 0;
                Entry entry = _entries[index];
                if (!entry.Separator && entry.Enabled)
                {
                    _hover = index;
                    Invalidate();
                    return;
                }
            }
        }
    }
}
