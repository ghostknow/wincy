using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>
    /// Right-hand preview of the focused item: full text (wrapped and scrollable),
    /// a fitted image or the file list, with a small metadata line on top.
    /// </summary>
    internal sealed class PreviewPane : Control
    {
        private const int MaxPreviewChars = 8000;
        private const int MaxFileEntries = 60;

        private ClipboardItem _item;
        private Palette _palette = Palette.MakeLight();
        private int _scroll;
        private List<string> _lines = new List<string>();
        private string _linesKey;

        public PreviewPane()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public ClipboardItem Item
        {
            get { return _item; }
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            Invalidate();
        }

        public void SetItem(ClipboardItem item)
        {
            if (ReferenceEquals(item, _item) && _linesKey != null) return;
            _item = item;
            _scroll = 0;
            _lines.Clear();
            _linesKey = null;
            Invalidate();
        }

        private int Pad
        {
            get { return Dpi.S(16); }
        }

        private int HeaderHeight
        {
            get { return Dpi.S(28); }
        }

        private int LineHeight
        {
            get { return Dpi.S(18); }
        }

        private Rectangle BodyRect
        {
            get
            {
                Rectangle body = new Rectangle(Pad, Pad + HeaderHeight, 0, 0);
                body.Width = Math.Max(Dpi.S(60), ClientSize.Width - Pad * 2 - Dpi.S(10));
                body.Height = Math.Max(Dpi.S(40), ClientSize.Height - body.Top - Dpi.S(12));
                return body;
            }
        }

        // ------------------------------------------------------------------ scrolling

        public void HandleWheel(int delta)
        {
            if (_item == null) return;
            int max = MaxScroll();
            if (max <= 0) return;
            _scroll -= Math.Sign(delta) * LineHeight * 3;
            if (_scroll < 0) _scroll = 0;
            if (_scroll > max) _scroll = max;
            Invalidate();
        }

        public bool CanScroll
        {
            get { return MaxScroll() > 0; }
        }

        private int ContentHeight
        {
            get
            {
                if (_item == null) return 0;
                switch (_item.Kind)
                {
                    case ItemKind.Text:
                        EnsureLines();
                        return _lines.Count * LineHeight;
                    case ItemKind.Files:
                        return Math.Min(_item.Files.Count, MaxFileEntries) * Dpi.S(38);
                    default:
                        return 0;
                }
            }
        }

        private int MaxScroll()
        {
            return Math.Max(0, ContentHeight - BodyRect.Height);
        }

        // ------------------------------------------------------------------ text layout

        private void EnsureLines()
        {
            if (_item == null || _item.Kind != ItemKind.Text || string.IsNullOrEmpty(_item.Text))
            {
                _lines.Clear();
                _linesKey = null;
                return;
            }
            int width = BodyRect.Width;
            string key = _item.Id + "|" + width + "|" + Dpi.Value;
            if (_linesKey == key) return;

            string text = _item.Text;
            bool truncated = text.Length > MaxPreviewChars;
            if (truncated) text = text.Substring(0, MaxPreviewChars);

            List<string> lines = new List<string>();
            string[] paragraphs = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                string paragraph = ExpandTabs(paragraphs[p]);
                if (paragraph.Length == 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }
                int start = 0;
                while (start < paragraph.Length)
                {
                    int count = FitLength(paragraph, start, width);
                    if (count <= 0) count = 1;
                    lines.Add(paragraph.Substring(start, count));
                    start += count;
                    while (start < paragraph.Length && paragraph[start] == ' ') start++;
                    if (lines.Count > 4000) break;
                }
                if (lines.Count > 4000) break;
            }
            if (truncated) lines.Add("…");

            _lines = lines;
            _linesKey = key;
        }

        private static string ExpandTabs(string value)
        {
            if (value.IndexOf('\t') < 0) return value;
            return value.Replace("\t", "    ");
        }

        /// <summary>Largest prefix length of <paramref name="text"/> from <paramref name="start"/> that fits in <paramref name="width"/>.</summary>
        private int FitLength(string text, int start, int width)
        {
            Font font = Fonts.Ui(9.5f);
            int remaining = text.Length - start;
            int low = 0;
            int high = remaining;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                int measured = TextRenderer.MeasureText(text.Substring(start, mid), font,
                    new Size(100000, 1000), TextFormatFlags.NoPadding).Width;
                if (measured <= width) low = mid;
                else high = mid - 1;
            }
            if (low >= remaining) return remaining;

            // Prefer breaking at a space if one is reasonably close to the limit.
            int limit = Math.Max(1, low);
            int space = text.LastIndexOf(' ', start + limit - 1, limit);
            if (space > start + (limit / 2)) return space - start;
            return limit;
        }

        // ------------------------------------------------------------------ painting

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);

            if (_item == null)
            {
                TextRenderer.DrawText(g, I18n.T("preview.empty"), Fonts.Ui(9f),
                    new Rectangle(Pad, Pad, ClientSize.Width - Pad * 2, Dpi.S(40)),
                    _palette.TextFaint,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                return;
            }

            Rectangle body = BodyRect;
            DrawHeader(g);

            switch (_item.Kind)
            {
                case ItemKind.Image:
                    DrawImage(g, body);
                    break;
                case ItemKind.Files:
                    DrawFiles(g, body);
                    break;
                default:
                    DrawText(g, body);
                    break;
            }

            DrawScrollIndicator(g, body);
        }

        private void DrawHeader(Graphics g)
        {
            string header = HeaderText();
            Rectangle rect = new Rectangle(Pad, Pad, ClientSize.Width - Pad * 2, HeaderHeight);
            TextRenderer.DrawText(g, Ui.Ellipsize(g, Fonts.Ui(8.75f), header, rect.Width), Fonts.Ui(8.75f),
                rect, _palette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
            using (Pen pen = new Pen(_palette.Separator, 1f))
            {
                int y = Pad + HeaderHeight - Dpi.S(4);
                g.DrawLine(pen, Pad, y, ClientSize.Width - Pad, y);
            }
        }

        private string HeaderText()
        {
            string kind = _item.KindLabel;
            string age = _item.AgeLabel;
            string extra = null;
            if (_item.Kind == ItemKind.Image && _item.ImageWidth > 0)
            {
                extra = string.Format(CultureInfo.InvariantCulture, "{0} × {1}", _item.ImageWidth, _item.ImageHeight);
                string path = Paths.ImageFullPathFor(_item);
                long size = FileSize(path);
                if (size > 0) extra += "  ·  " + FormatBytes(size);
            }
            else if (_item.Kind == ItemKind.Files)
            {
                if (_item.Files != null) extra = I18n.T("preview.files", _item.Files.Count);
            }
            else if (_item.Text != null)
            {
                extra = _item.Text.Length.ToString("N0", CultureInfo.CurrentCulture) + " ";
                extra += I18n.Language == I18n.Chinese ? "字元" : "chars";
            }

            // The list row already names the source app; keep the preview header compact.
            string line;
            if (_item.Kind == ItemKind.Files && extra != null) line = extra;
            else line = extra == null ? kind : kind + "  ·  " + extra;
            return line + "  ·  " + age;
        }

        private void DrawText(Graphics g, Rectangle body)
        {
            EnsureLines();
            if (_lines.Count == 0) return;
            int max = MaxScroll();
            if (_scroll > max) _scroll = max;
            int first = Math.Max(0, _scroll / LineHeight);
            int last = Math.Min(_lines.Count - 1, (_scroll + body.Height) / LineHeight);

            Font font = Fonts.Ui(9.5f);
            for (int i = first; i <= last; i++)
            {
                int y = body.Top + i * LineHeight - _scroll;
                Rectangle lineRect = new Rectangle(body.Left, y, body.Width, LineHeight);
                TextRenderer.DrawText(g, _lines[i], font, lineRect, _palette.Text,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }

        private void DrawFiles(Graphics g, Rectangle body)
        {
            List<string> files = _item.Files;
            if (files == null || files.Count == 0) return;
            int count = Math.Min(files.Count, MaxFileEntries);
            int rowHeight = Dpi.S(38);
            int y = body.Top - _scroll;
            for (int i = 0; i < count; i++)
            {
                if (y + rowHeight < body.Top || y > body.Bottom)
                {
                    y += rowHeight;
                    continue;
                }
                string full = files[i];
                string name = Path.GetFileName(full);
                if (string.IsNullOrEmpty(name)) name = full;
                string folder = full.Length > name.Length ? full.Substring(0, full.Length - name.Length) : string.Empty;

                Rectangle nameRect = new Rectangle(body.Left, y, body.Width, Dpi.S(20));
                TextRenderer.DrawText(g, Ui.Ellipsize(g, Fonts.Ui(9.5f), name, nameRect.Width), Fonts.Ui(9.5f),
                    nameRect, _palette.Text,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                Rectangle folderRect = new Rectangle(body.Left, y + Dpi.S(19), body.Width, Dpi.S(17));
                TextRenderer.DrawText(g, Ui.Ellipsize(g, Fonts.Ui(8.25f), folder.TrimEnd('\\'), folderRect.Width),
                    Fonts.Ui(8.25f), folderRect, _palette.TextFaint,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                y += rowHeight;
            }
        }

        private void DrawImage(Graphics g, Rectangle body)
        {
            string path = Paths.ImageFullPathFor(_item);
            int width = _item.ImageWidth > 0 ? _item.ImageWidth : body.Width;
            int height = _item.ImageHeight > 0 ? _item.ImageHeight : body.Width;
            float scale = Math.Min(body.Width / (float)width, body.Height / (float)height);
            if (scale > 2f) scale = 2f;
            int drawWidth = Math.Max(1, (int)Math.Round(width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(height * scale));
            Bitmap image = Thumbnails.Get(path, drawWidth, drawHeight, false);

            Rectangle frame = new Rectangle(body.Left, body.Top, drawWidth, drawHeight);
            using (GraphicsPath clip = Ui.Round(frame, Dpi.S(8)))
            {
                Region previous = g.Clip;
                g.SetClip(clip);
                using (SolidBrush fill = new SolidBrush(_palette.BgInput)) g.FillRectangle(fill, frame);
                if (image != null) Ui.DrawIconSized(g, image, frame);
                g.Clip = previous;
            }
            Ui.StrokeRound(g, _palette.Border, new Rectangle(frame.X, frame.Y, frame.Width - 1, frame.Height - 1),
                Dpi.S(8), 1f);
        }

        private void DrawScrollIndicator(Graphics g, Rectangle body)
        {
            int max = MaxScroll();
            if (max <= 0) return;
            int content = ContentHeight;
            if (content <= 0) return;
            int track = body.Height;
            int thumb = Math.Max(Dpi.S(24), (int)(track * (body.Height / (float)content)));
            int usable = Math.Max(1, track - thumb);
            int top = body.Top + (int)(usable * (_scroll / (float)max));
            Rectangle rect = new Rectangle(ClientSize.Width - Dpi.S(7), top, Dpi.S(3), thumb);
            Ui.FillRound(g, Ui.Alpha(_palette.ScrollThumb, 150), rect, Dpi.S(2));
        }

        private static long FileSize(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }
    }
}
