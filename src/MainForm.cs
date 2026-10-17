using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>The popup: search field, history list and footer hints.</summary>
    internal sealed class MainForm : Form
    {
        private readonly WincyApp _app;
        private readonly SearchBox _search;
        private readonly HistoryList _list;
        private Palette _palette;
        private bool _suppressDeactivate;
        private bool _fromTray;
        private string _query = string.Empty;
        private Rectangle _gearRect;
        private bool _gearHover;
        private bool _gearPressed;
        private bool _layingOut;

        public MainForm(WincyApp app)
        {
            _app = app;
            _palette = app.Palette;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            MinimizeBox = false;
            MaximizeBox = false;
            Text = "Wincy";
            Font = Fonts.Ui(9.75f);
            BackColor = _palette.Bg;

            _search = new SearchBox();
            _search.Font = Fonts.Ui(11.5f);
            _search.BackColor = _palette.BgInput;
            _search.ForeColor = _palette.Text;
            _search.Placeholder = I18n.T("search.placeholder");
            _search.PlaceholderColor = _palette.TextFaint;
            _search.TextChanged += OnSearchTextChanged;
            _search.GotFocus += delegate { Invalidate(); };
            _search.LostFocus += delegate { Invalidate(); };
            Controls.Add(_search);

            _list = new HistoryList();
            _list.Font = Fonts.Ui(9.75f);
            _list.ApplyPalette(_palette);
            _list.ItemActivated += delegate { ActivateSelected(false, false); };
            _list.ItemContextMenu += delegate { ShowItemMenu(); };
            Controls.Add(_list);

            _app.Store.Changed += OnStoreChanged;
        }

        public HistoryList List
        {
            get { return _list; }
        }

        public bool SuppressDeactivate
        {
            get { return _suppressDeactivate; }
            set { _suppressDeactivate = value; }
        }

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

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            _search.BackColor = palette.BgInput;
            _search.ForeColor = palette.Text;
            _search.PlaceholderColor = palette.TextFaint;
            _list.ApplyPalette(palette);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TrySetRoundedCorners(Handle);
            Native.HideFromAltTab(Handle);
        }

        // ------------------------------------------------------------------ layout

        private int Pad
        {
            get { return Dpi.S(10); }
        }

        private int SearchHeight
        {
            get { return Dpi.S(40); }
        }

        private int FooterHeight
        {
            get { return Dpi.S(36); }
        }

        private Rectangle SearchRect
        {
            get { return new Rectangle(Pad, Pad, ClientSize.Width - Pad * 2, SearchHeight); }
        }

        private int HeightForContent()
        {
            int rows = _list.ItemCount;
            if (rows > 8) rows = 8;
            if (rows < 3) rows = 3;
            int height = Pad * 2 + SearchHeight + Dpi.S(6) + rows * _list.RowHeight + FooterHeight;
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int max = Math.Max(Dpi.S(240), work.Height - Dpi.S(80));
            if (height > max) height = max;
            if (height < Dpi.S(240)) height = Dpi.S(240);
            return height;
        }

        private void LayoutUi()
        {
            if (_layingOut) return;
            _layingOut = true;
            try
            {
                ClientSize = new Size(Dpi.S(420), HeightForContent());

                Rectangle pill = SearchRect;
                _search.Font = Fonts.Ui(11.5f);
                int textHeight = _search.Height;
                int textTop = pill.Top + (pill.Height - textHeight) / 2;
                _search.Bounds = new Rectangle(pill.Left + Dpi.S(34), textTop,
                    pill.Width - Dpi.S(46), textHeight);

                int listTop = pill.Bottom + Dpi.S(6);
                _list.Bounds = new Rectangle(0, listTop, ClientSize.Width, ClientSize.Height - listTop - FooterHeight);

                int gearSize = Dpi.S(26);
                _gearRect = new Rectangle(ClientSize.Width - Pad - gearSize,
                    ClientSize.Height - FooterHeight + (FooterHeight - gearSize) / 2, gearSize, gearSize);
            }
            finally
            {
                _layingOut = false;
            }
        }

        private void PositionAt(Point anchor)
        {
            Native.POINT point = new Native.POINT(anchor.X, anchor.Y);
            Native.RECT work = Native.WorkAreaFromPoint(point);
            Rectangle area = new Rectangle(work.Left, work.Top, work.Width, work.Height);
            int margin = Dpi.S(8);
            int width = Width;
            int height = Height;
            int x;
            int y;
            if (_fromTray)
            {
                x = anchor.X - width + Dpi.S(20);
                y = anchor.Y - height - Dpi.S(12);
            }
            else
            {
                x = anchor.X - width / 2;
                y = anchor.Y + Dpi.S(16);
                if (y + height > area.Bottom - margin) y = anchor.Y - height - Dpi.S(16);
            }
            x = Math.Max(area.Left + margin, Math.Min(area.Right - width - margin, x));
            y = Math.Max(area.Top + margin, Math.Min(area.Bottom - height - margin, y));
            if (y < area.Top + margin) y = area.Top + margin;
            Location = new Point(x, y);
        }

        // ------------------------------------------------------------------ show/hide

        public void ShowPopup(Point anchor, bool fromTray)
        {
            _fromTray = fromTray;
            _query = string.Empty;
            _search.Text = string.Empty;
            ApplyFilter();
            LayoutUi();
            PositionAt(anchor);
            if (!Visible) Show();
            TopMost = true;
            Activate();
            // Windows refuses activation for a window raised by another process (a script
            // using --show), so force focus the same way pasting does.
            Paster.FocusWindow(Handle);
            _search.Focus();
            Invalidate(true);
        }

        public void HidePopup()
        {
            if (!Visible) return;
            _suppressDeactivate = true;
            try
            {
                Hide();
            }
            finally
            {
                _suppressDeactivate = false;
            }
            _search.Text = string.Empty;
            _query = string.Empty;
        }

        public void HideForAction()
        {
            _suppressDeactivate = true;
            try
            {
                Hide();
                _search.Text = string.Empty;
                _query = string.Empty;
            }
            finally
            {
                _suppressDeactivate = false;
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (_suppressDeactivate) return;
            if (!Visible) return;
            // Delay so a click on a menu or the settings window does not close the popup.
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (_suppressDeactivate || !Visible) return;
                    if (ActiveForm != null) return;
                    HidePopup();
                }));
            }
            catch (InvalidOperationException)
            {
                // Handle already destroyed during shutdown.
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HidePopup();
                return;
            }
            base.OnFormClosing(e);
        }

        // ------------------------------------------------------------------ data

        private sealed class ScoredItem
        {
            public ClipboardItem Item;
            public int Score;
            public int Index;
        }

        private void OnSearchTextChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        public void ApplyFilter()
        {
            string query = _search.Text == null ? string.Empty : _search.Text.Trim();
            _query = query;
            List<ClipboardItem> all = _app.Store.Snapshot();
            List<ClipboardItem> result;
            if (query.Length == 0)
            {
                result = all;
            }
            else
            {
                List<ScoredItem> hits = new List<ScoredItem>();
                for (int i = 0; i < all.Count; i++)
                {
                    ClipboardItem item = all[i];
                    MatchResult match = Fuzzy.Substring(item.Title, query);
                    int score;
                    if (match.Matched)
                    {
                        score = match.Score;
                    }
                    else
                    {
                        match = Fuzzy.Substring(item.SearchBlob, query);
                        if (!match.Matched) continue;
                        score = match.Score - 800;
                    }
                    if (item.Pinned) score += 6000;
                    ScoredItem scored = new ScoredItem();
                    scored.Item = item;
                    scored.Score = score;
                    scored.Index = i;
                    hits.Add(scored);
                }
                hits.Sort(delegate(ScoredItem a, ScoredItem b)
                {
                    if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
                    return a.Index.CompareTo(b.Index);
                });
                result = new List<ClipboardItem>(hits.Count);
                for (int i = 0; i < hits.Count; i++) result.Add(hits[i].Item);
            }

            string previousId = null;
            ClipboardItem previous = _list.SelectedItem;
            if (previous != null) previousId = previous.Id;

            _list.SetItems(result, query);
            if (previousId != null && query.Length > 0)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i].Id == previousId)
                    {
                        _list.SelectIndexSilent(i);
                        break;
                    }
                }
            }
            else
            {
                _list.SelectFirst();
            }
            Invalidate();
        }

        public void RefreshAfterChange()
        {
            ApplyFilter();
            Invalidate();
        }

        private void OnStoreChanged(object sender, EventArgs e)
        {
            if (!Visible) return;
            try
            {
                BeginInvoke(new MethodInvoker(RefreshAfterChange));
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------------ actions

        private void ActivateSelected(bool plain, bool copyOnly)
        {
            ClipboardItem item = _list.SelectedItem;
            if (item == null) return;
            _app.UseItem(item, plain, copyOnly);
        }

        private void DeleteSelected()
        {
            ClipboardItem item = _list.SelectedItem;
            if (item == null) return;
            _app.Store.Delete(item.Id);
            RefreshAfterChange();
        }

        private void TogglePinSelected()
        {
            ClipboardItem item = _list.SelectedItem;
            if (item == null) return;
            _app.Store.TogglePin(item.Id);
            RefreshAfterChange();
        }

        private void ShowItemMenu()
        {
            ClipboardItem item = _list.SelectedItem;
            if (item == null) return;
            MiniMenu menu = new MiniMenu(_palette);
            menu.Add(I18n.T("item.paste"), "Enter", delegate { _app.UseItem(item, false, false); });
            menu.Add(I18n.T("item.pastePlain"), "Shift+Enter", delegate { _app.UseItem(item, true, false); });
            menu.Add(I18n.T("item.copy"), "Ctrl+Enter", delegate { _app.UseItem(item, false, true); });
            menu.AddSeparator();
            menu.Add(I18n.T(item.Pinned ? "item.unpin" : "item.pin"), "Ctrl+P", delegate
            {
                _app.Store.TogglePin(item.Id);
                RefreshAfterChange();
            });
            menu.AddDanger(I18n.T("item.delete"), "Del", delegate
            {
                _app.Store.Delete(item.Id);
                RefreshAfterChange();
            });
            menu.AddSeparator();
            menu.AddDanger(I18n.T("item.clearAll"), null, ConfirmClearAll);
            ShowMenu(menu, Cursor.Position);
        }

        private void ConfirmClearAll()
        {
            int count = _app.Store.Count;
            MiniMenu menu = new MiniMenu(_palette);
            menu.AddHeader(I18n.T("item.clearConfirm", count));
            menu.AddDanger(I18n.T("item.clearAll"), null, delegate
            {
                _app.Store.Clear();
                RefreshAfterChange();
            });
            menu.Add(I18n.T("item.cancel"), null, delegate { });
            ShowMenu(menu, Cursor.Position);
        }

        public void ShowMenu(MiniMenu menu, Point screenPoint)
        {
            _suppressDeactivate = true;
            menu.ClosedAction = delegate
            {
                _suppressDeactivate = false;
                if (!Visible) return;
                Activate();
                _search.Focus();
            };
            menu.ShowAt(screenPoint);
        }


        // ------------------------------------------------------------------ keyboard

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!Visible) return base.ProcessCmdKey(ref msg, keyData);

            switch (keyData)
            {
                case Keys.Escape:
                    HidePopup();
                    return true;
                case Keys.Up:
                    _list.MoveSelection(-1);
                    return true;
                case Keys.Down:
                    _list.MoveSelection(1);
                    return true;
                case Keys.PageUp:
                    _list.MoveSelection(-6);
                    return true;
                case Keys.PageDown:
                    _list.MoveSelection(6);
                    return true;
                case Keys.Home:
                    if (_search.TextLength == 0)
                    {
                        _list.MoveSelection(-_list.ItemCount);
                        return true;
                    }
                    break;
                case Keys.End:
                    if (_search.TextLength == 0)
                    {
                        _list.MoveSelection(_list.ItemCount);
                        return true;
                    }
                    break;
                case Keys.Enter:
                    ActivateSelected(false, false);
                    return true;
                case Keys.Shift | Keys.Enter:
                    ActivateSelected(true, false);
                    return true;
                case Keys.Control | Keys.Enter:
                    ActivateSelected(false, true);
                    return true;
                case Keys.Delete:
                    if (_search.TextLength == 0)
                    {
                        DeleteSelected();
                        return true;
                    }
                    break;
                case Keys.Control | Keys.P:
                    TogglePinSelected();
                    return true;
                case Keys.Control | Keys.Oemcomma:
                    _app.OpenSettings();
                    return true;
            }

            if ((keyData & Keys.Control) == Keys.Control)
            {
                Keys key = keyData & Keys.KeyCode;
                if (key >= Keys.D1 && key <= Keys.D9)
                {
                    int index = key - Keys.D1;
                    if (index < _list.ItemCount)
                    {
                        _list.SelectedIndex = index;
                        ActivateSelected(false, false);
                        return true;
                    }
                }
                if (key >= Keys.NumPad1 && key <= Keys.NumPad9)
                {
                    int index = key - Keys.NumPad1;
                    if (index < _list.ItemCount)
                    {
                        _list.SelectedIndex = index;
                        ActivateSelected(false, false);
                        return true;
                    }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _list.HandleWheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hover = _gearRect.Contains(e.Location);
            if (hover != _gearHover)
            {
                _gearHover = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate(_gearRect);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_gearHover)
            {
                _gearHover = false;
                Cursor = Cursors.Default;
                Invalidate(_gearRect);
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _gearRect.Contains(e.Location))
            {
                _gearPressed = true;
                Invalidate(_gearRect);
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_gearPressed)
            {
                _gearPressed = false;
                Invalidate(_gearRect);
                if (_gearRect.Contains(e.Location)) _app.OpenSettings();
                return;
            }
            base.OnMouseUp(e);
        }

        // ------------------------------------------------------------------ painting

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Fully painted in OnPaint.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle full = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (SolidBrush brush = new SolidBrush(_palette.Bg)) g.FillRectangle(brush, full);

            Rectangle pill = SearchRect;
            Ui.FillRound(g, _palette.BgInput, pill, Dpi.S(10));
            Color pillBorder = _search.Focused ? Ui.Alpha(_palette.Accent, 170) : _palette.Border;
            Ui.StrokeRound(g, pillBorder, new Rectangle(pill.X, pill.Y, pill.Width - 1, pill.Height - 1), Dpi.S(10), 1f);
            Glyph.Magnifier(g, new Rectangle(pill.Left + Dpi.S(11), pill.Top + (pill.Height - Dpi.S(16)) / 2, Dpi.S(16), Dpi.S(16)),
                _palette.TextMuted, Dpi.Sf(1.5f));

            int footerTop = ClientSize.Height - FooterHeight;
            using (Pen pen = new Pen(_palette.Separator, 1f))
            {
                g.DrawLine(pen, Pad, footerTop, ClientSize.Width - Pad, footerTop);
            }

            string hints = "Enter " + I18n.T("hint.paste") + "    Shift+Enter " + I18n.T("hint.plain") +
                           "    Del " + I18n.T("hint.delete");
            Font small = Fonts.Ui(8.25f);
            int hintWidth = ClientSize.Width - Pad * 2 - Dpi.S(120);
            Rectangle hintRect = new Rectangle(Pad, footerTop, Math.Max(Dpi.S(40), hintWidth), FooterHeight);
            Ui.DrawText(g, Ui.Ellipsize(g, small, hints, hintRect.Width), small, _palette.TextFaint, hintRect, Ui.LeftMiddle);

            if (_gearHover || _gearPressed)
            {
                Ui.FillRound(g, _gearPressed ? _palette.BgInput : _palette.BgHover, _gearRect, Dpi.S(6));
            }
            Rectangle glyphRect = new Rectangle(_gearRect.Left + Dpi.S(6), _gearRect.Top + Dpi.S(6),
                _gearRect.Width - Dpi.S(12), _gearRect.Height - Dpi.S(12));
            Glyph.Sliders(g, glyphRect, _gearHover ? _palette.Text : _palette.TextMuted, Dpi.Sf(1.5f));

            string count = I18n.Count(_list.ItemCount);
            int countWidth = Ui.MeasureWidth(g, small, count);
            Rectangle countRect = new Rectangle(_gearRect.Left - Dpi.S(10) - countWidth, footerTop, countWidth, FooterHeight);
            Ui.DrawText(g, count, small, _palette.TextFaint, countRect, Ui.LeftMiddle);

            Rectangle border = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            Ui.StrokeRound(g, _palette.Border, border, Dpi.S(12), 1f);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutUi();
        }
    }
}
