using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>
    /// Owner-drawn history list: app icon/thumbnail, one-line preview, source and age,
    /// search-hit highlighting, hover/selection states and a slim custom scrollbar.
    /// </summary>
    internal sealed class HistoryList : Control
    {
        private readonly List<ClipboardItem> _items = new List<ClipboardItem>();
        private string _query = string.Empty;
        private Palette _palette = Palette.MakeLight();
        private int _selected;
        private int _hover = -1;
        private int _scroll;
        private bool _draggingThumb;
        private bool _pressed;
        private int _dragOffset;
        private int _pressY;
        private bool _movedDuringPress;

        public event EventHandler SelectionChanged;
        public event EventHandler ItemActivated;
        public event EventHandler ItemContextMenu;
        public event EventHandler PointerLeft;

        public HistoryList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public int RowHeight
        {
            get { return Dpi.S(56); }
        }

        private int PadH
        {
            get { return Dpi.S(8); }
        }

        private int ScrollBarWidth
        {
            get { return Dpi.S(4); }
        }

        public IList<ClipboardItem> Items
        {
            get { return _items; }
        }

        public int ItemCount
        {
            get { return _items.Count; }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int index = _items.Count == 0 ? -1 : Math.Max(0, Math.Min(_items.Count - 1, value));
                if (index == _selected) return;
                _selected = index;
                EnsureVisible(index);
                Invalidate();
                RaiseSelectionChanged();
            }
        }

        public ClipboardItem SelectedItem
        {
            get
            {
                if (_selected < 0 || _selected >= _items.Count) return null;
                return _items[_selected];
            }
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            Invalidate();
        }

        public void SetItems(List<ClipboardItem> items, string query)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            _query = query ?? string.Empty;
            _hover = -1;
            if (_items.Count == 0) _selected = -1;
            else if (_selected >= _items.Count) _selected = _items.Count - 1;
            ClampScroll();
            Invalidate();
        }

        public void MoveSelection(int delta)
        {
            if (_items.Count == 0) return;
            int next;
            if (_selected < 0) next = delta >= 0 ? 0 : _items.Count - 1;
            else next = _selected + delta;
            if (next < 0) next = 0;
            if (next > _items.Count - 1) next = _items.Count - 1;
            SelectedIndex = next;
        }

        /// <summary>Drops the focus so the list (and the preview) start out empty.</summary>
        public void ClearSelection()
        {
            if (_selected == -1) return;
            _selected = -1;
            Invalidate();
            RaiseSelectionChanged();
        }

        /// <summary>Row under a client-space point, or -1 for padding, the scrollbar or empty space.</summary>
        public int IndexAtPoint(Point point)
        {
            if (point.X < 0 || point.X >= ClientSize.Width) return -1;
            if (IsOverScrollbar(point)) return -1;
            return IndexAtY(point.Y);
        }

        private bool IsOverScrollbar(Point point)
        {
            return NeedsScrollbar && point.X >= ClientSize.Width - Dpi.S(12);
        }

        private void RaiseSelectionChanged()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void SelectFirst()
        {
            if (_items.Count == 0)
            {
                _selected = -1;
                return;
            }
            _selected = 0;
            _scroll = 0;
            Invalidate();
        }

        public void SelectIndexSilent(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _selected = index;
            EnsureVisible(index);
            Invalidate();
        }

        public void EnsureVisible(int index)
        {
            if (index < 0) return;
            int top = index * RowHeight;
            int bottom = top + RowHeight;
            int view = ClientSize.Height;
            if (top < _scroll) _scroll = top;
            else if (bottom > _scroll + view) _scroll = bottom - view;
            ClampScroll();
        }

        private int ContentHeight
        {
            get { return _items.Count * RowHeight; }
        }

        private bool NeedsScrollbar
        {
            get { return ContentHeight > ClientSize.Height; }
        }

        private int MaxScroll
        {
            get { return Math.Max(0, ContentHeight - ClientSize.Height); }
        }

        private void ClampScroll()
        {
            if (_scroll < 0) _scroll = 0;
            int max = MaxScroll;
            if (_scroll > max) _scroll = max;
        }

        public void HandleWheel(int delta)
        {
            if (!NeedsScrollbar) return;
            _scroll -= Math.Sign(delta) * RowHeight * 3;
            ClampScroll();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Background is painted in OnPaint so scrolling never flashes.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);

            if (_items.Count == 0)
            {
                PaintEmptyState(g);
                return;
            }

            int first = Math.Max(0, _scroll / RowHeight);
            int last = Math.Min(_items.Count - 1, (_scroll + ClientSize.Height) / RowHeight + 1);

            for (int i = first; i <= last; i++)
            {
                int y = i * RowHeight - _scroll;
                PaintRow(g, i, _items[i], y);
            }

            PaintScrollbar(g);
        }

        private void PaintEmptyState(Graphics g)
        {
            int width = ClientSize.Width;
            int centerY = ClientSize.Height / 2 - Dpi.S(24);
            Rectangle glyphRect = new Rectangle(width / 2 - Dpi.S(11), centerY - Dpi.S(30), Dpi.S(22), Dpi.S(22));
            Glyph.Magnifier(g, glyphRect, _palette.TextFaint, Dpi.Sf(1.6f));

            string title = I18n.T(string.IsNullOrEmpty(_query) ? "empty.titleEmpty" : "empty.title");
            string hint = I18n.T(string.IsNullOrEmpty(_query) ? "empty.hint" : "empty.hintSearch");
            TextRenderer.DrawText(g, title, Fonts.Ui(9.75f),
                new Rectangle(Dpi.S(16), centerY + Dpi.S(6), width - Dpi.S(32), Dpi.S(22)),
                _palette.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, hint, Fonts.Ui(8.5f),
                new Rectangle(Dpi.S(24), centerY + Dpi.S(32), width - Dpi.S(48), Dpi.S(40)),
                _palette.TextFaint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
        }

        private void PaintRow(Graphics g, int index, ClipboardItem item, int y)
        {
            bool selected = index == _selected;
            bool hovered = index == _hover && !selected;
            Rectangle row = new Rectangle(PadH, y, ClientSize.Width - PadH * 2, RowHeight - Dpi.S(2));

            if (selected) Ui.FillRound(g, _palette.Accent, row, Dpi.S(8));
            else if (hovered) Ui.FillRound(g, _palette.BgHover, row, Dpi.S(8));

            Color titleColor = selected ? _palette.OnAccent : _palette.Text;
            Color mutedColor = selected ? _palette.OnAccentMuted : _palette.TextMuted;
            Color matchColor = selected ? _palette.OnAccent : _palette.Accent;

            // Age label on the right.
            string age = item.AgeLabel;
            Font small = Fonts.Ui(8.25f);
            int ageWidth = Ui.MeasureWidth(g, small, age);
            Rectangle ageRect = new Rectangle(row.Right - Dpi.S(12) - ageWidth, row.Top, ageWidth, RowHeight);
            TextRenderer.DrawText(g, age, small, ageRect, mutedColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            int rightEdge = ageRect.Left - Dpi.S(8);
            if (item.Pinned)
            {
                Rectangle pinRect = new Rectangle(rightEdge - Dpi.S(10), row.Top + (RowHeight - Dpi.S(12)) / 2,
                    Dpi.S(9), Dpi.S(12));
                Glyph.Bookmark(g, pinRect, selected ? _palette.OnAccent : _palette.Accent);
                rightEdge -= Dpi.S(16);
            }

            int iconLeft = row.Left + Dpi.S(10);
            int iconSize = Dpi.S(20);
            int textLeft;
            if (item.Kind == ItemKind.Image)
            {
                int thumbWidth = Dpi.S(46);
                int thumbHeight = Dpi.S(34);
                Rectangle thumb = new Rectangle(iconLeft, row.Top + (RowHeight - thumbHeight) / 2 - Dpi.S(1), thumbWidth, thumbHeight);
                Bitmap image = Thumbnails.Get(Paths.ImageFullPathFor(item), thumbWidth - Dpi.S(4), thumbHeight - Dpi.S(4));
                if (image != null)
                {
                    using (GraphicsPath clip = Ui.Round(thumb, Dpi.S(5)))
                    {
                        Region previous = g.Clip;
                        g.SetClip(clip);
                        using (SolidBrush fill = new SolidBrush(selected ? Ui.Alpha(Color.Black, 60) : _palette.BgInput))
                        {
                            g.FillRectangle(fill, thumb);
                        }
                        Ui.DrawIconSized(g, image, new Rectangle(thumb.Left + Dpi.S(2), thumb.Top + Dpi.S(2),
                            thumbWidth - Dpi.S(4), thumbHeight - Dpi.S(4)));
                        g.Clip = previous;
                    }
                }
                else
                {
                    Ui.FillRound(g, selected ? Ui.Alpha(Color.White, 40) : _palette.BgInput, thumb, Dpi.S(5));
                }
                textLeft = thumb.Right + Dpi.S(12);
            }
            else
            {
                Rectangle iconRect = new Rectangle(iconLeft, row.Top + (RowHeight - iconSize) / 2 - Dpi.S(1), iconSize, iconSize);
                Bitmap icon = AppIcons.Get(item.SourcePath);
                if (icon != null) Ui.DrawIconSized(g, icon, iconRect);
                else
                {
                    Ui.FillRound(g, selected ? Ui.Alpha(Color.White, 45) : _palette.BgInput, iconRect, Dpi.S(5));
                    string initial = string.IsNullOrEmpty(item.SourceApp) ? "M" : item.SourceApp.Substring(0, 1).ToUpperInvariant();
                    TextRenderer.DrawText(g, initial, Fonts.Bold(8f), iconRect, mutedColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                        TextFormatFlags.NoPadding);
                }
                textLeft = iconRect.Right + Dpi.S(12);
            }

            int textRight = rightEdge;
            int textWidth = Math.Max(Dpi.S(20), textRight - textLeft);
            int titleHeight = Dpi.S(19);
            Rectangle titleRect = new Rectangle(textLeft, row.Top + Dpi.S(8), textWidth, titleHeight);

            MatchResult match = string.IsNullOrEmpty(_query) ? null : Fuzzy.Substring(item.Title, _query);
            if (match != null && match.Matched)
            {
                Ui.DrawHighlighted(g, titleRect, Fonts.Ui(9.75f), item.Title, titleColor, matchColor, match.Positions);
            }
            else
            {
                Ui.DrawText(g, Ui.Ellipsize(g, Fonts.Ui(9.75f), item.Title, textWidth), Fonts.Ui(9.75f),
                    titleColor, titleRect, Ui.LeftMiddle);
            }

            string subtitle = item.SourceLabel;
            if (item.Kind != ItemKind.Text) subtitle = subtitle + "  ·  " + item.KindLabel;
            Rectangle subtitleRect = new Rectangle(textLeft, row.Top + Dpi.S(27), textWidth, Dpi.S(18));
            Ui.DrawText(g, Ui.Ellipsize(g, small, subtitle, textWidth), small, mutedColor, subtitleRect, Ui.LeftMiddle);
        }

        private void PaintScrollbar(Graphics g)
        {
            if (!NeedsScrollbar) return;
            int view = ClientSize.Height;
            int content = ContentHeight;
            int trackHeight = view - Dpi.S(8);
            if (trackHeight <= 0) return;
            int thumbHeight = Math.Max(Dpi.S(28), (int)(trackHeight * (view / (float)content)));
            int maxScroll = MaxScroll;
            int thumbY = Dpi.S(4) + (maxScroll <= 0 ? 0 : (int)((trackHeight - thumbHeight) * (_scroll / (float)maxScroll)));
            int x = ClientSize.Width - ScrollBarWidth - Dpi.S(4);
            Rectangle thumb = new Rectangle(x, thumbY, ScrollBarWidth, thumbHeight);
            // Overlay scrollbar: faint while idle, solid while the pointer is in the list.
            int alpha = (_hover >= 0 || _draggingThumb) ? 210 : 110;
            Ui.FillRound(g, Ui.Alpha(_palette.ScrollThumb, alpha), thumb, ScrollBarWidth / 2);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_draggingThumb)
            {
                int view = ClientSize.Height;
                int trackHeight = view - Dpi.S(8);
                int thumbHeight = Math.Max(Dpi.S(28), (int)(trackHeight * (view / (float)Math.Max(1, ContentHeight))));
                int usable = Math.Max(1, trackHeight - thumbHeight);
                int maxScroll = MaxScroll;
                int y = e.Y - Dpi.S(4) - _dragOffset;
                _scroll = (int)Math.Round(Math.Max(0, Math.Min(1, y / (float)usable)) * maxScroll);
                ClampScroll();
                Invalidate();
                return;
            }

            if (_pressed && Math.Abs(e.Y - _pressY) > Dpi.S(4)) _movedDuringPress = true;

            int index = IndexAtPoint(e.Location);
            if (index != _hover)
            {
                _hover = index;
                Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            // Follow the pointer: the row under the mouse becomes the focused item, so the
            // preview always matches what the user is pointing at.
            if (index >= 0 && index != _selected)
            {
                _selected = index;
                Invalidate();
                RaiseSelectionChanged();
            }
            else if (index < 0 && _selected >= 0 && !_pressed && !IsOverScrollbar(e.Location))
            {
                // The pointer is over padding or empty space: nothing should stay focused.
                _selected = -1;
                Invalidate();
                RaiseSelectionChanged();
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
            Cursor = Cursors.Default;
            EventHandler handler = PointerLeft;
            if (handler != null) handler(this, EventArgs.Empty);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Right)
            {
                int index = IndexAtY(e.Y);
                if (index >= 0)
                {
                    _selected = index;
                    Invalidate();
                    EventHandler handler = ItemContextMenu;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            if (NeedsScrollbar && e.X >= ClientSize.Width - ScrollBarWidth - Dpi.S(10))
            {
                _draggingThumb = true;
                _dragOffset = e.Y - Dpi.S(4) - ThumbTop();
                return;
            }

            int hit = IndexAtY(e.Y);
            if (hit >= 0)
            {
                _pressed = true;
                _movedDuringPress = false;
                _pressY = e.Y;
                _selected = hit;
                Invalidate();
                EventHandler handler = SelectionChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private int ThumbTop()
        {
            int view = ClientSize.Height;
            int trackHeight = Math.Max(1, view - Dpi.S(8));
            int content = Math.Max(1, ContentHeight);
            int thumbHeight = Math.Max(Dpi.S(28), (int)(trackHeight * (view / (float)content)));
            int usable = Math.Max(1, trackHeight - thumbHeight);
            int maxScroll = Math.Max(1, MaxScroll);
            return (int)(usable * (_scroll / (float)maxScroll));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_draggingThumb)
            {
                _draggingThumb = false;
                return;
            }
            if (e.Button == MouseButtons.Left && _pressed)
            {
                _pressed = false;
                int hit = IndexAtY(e.Y);
                if (hit >= 0 && !_movedDuringPress && hit == _selected)
                {
                    EventHandler handler = ItemActivated;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
            base.OnMouseUp(e);
        }

        public int IndexAtY(int y)
        {
            int index = (y + _scroll) / RowHeight;
            if (index < 0 || index >= _items.Count) return -1;
            return index;
        }
    }
}
