using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace Wincy
{
    internal static class Ui
    {
        public const TextFormatFlags LeftMiddle = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                                  TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                                                  TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

        public const TextFormatFlags Plain = TextFormatFlags.Left | TextFormatFlags.Top |
                                             TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public static GraphicsPath Round(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int r = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            if (r <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int d = r * 2;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath Round(RectangleF bounds, float radius)
        {
            return Round(Rectangle.Round(bounds), (int)Math.Round(radius));
        }

        public static void FillRound(Graphics g, Color color, Rectangle bounds, int radius)
        {
            using (GraphicsPath path = Round(bounds, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void StrokeRound(Graphics g, Color color, Rectangle bounds, int radius, float width)
        {
            using (GraphicsPath path = Round(bounds, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static Color Alpha(Color color, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color.R, color.G, color.B);
        }

        public static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static int MeasureWidth(Graphics g, Font font, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            Size size = TextRenderer.MeasureText(g, text, font, new Size(100000, 1000), TextFormatFlags.NoPadding);
            return size.Width;
        }

        public static string Ellipsize(Graphics g, Font font, string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return string.Empty;
            if (MeasureWidth(g, font, text) <= maxWidth) return text;
            string ellipsis = "…";
            int min = 0;
            int max = text.Length;
            while (min < max)
            {
                int mid = (min + max + 1) / 2;
                string candidate = text.Substring(0, mid) + ellipsis;
                if (MeasureWidth(g, font, candidate) <= maxWidth) min = mid;
                else max = mid - 1;
            }
            if (min <= 0) return string.Empty;
            return text.Substring(0, min) + ellipsis;
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text)) return;
            TextRenderer.DrawText(g, text, font, bounds, color, flags);
        }

        /// <summary>Draw text with search-hit characters in a second color.</summary>
        public static void DrawHighlighted(Graphics g, Rectangle bounds, Font font, string text,
            Color color, Color highlight, int[] positions)
        {
            if (string.IsNullOrEmpty(text) || bounds.Width <= 0) return;
            if (positions == null || positions.Length == 0)
            {
                DrawText(g, Ellipsize(g, font, text, bounds.Width), font, color, bounds, LeftMiddle);
                return;
            }

            bool[] mask = new bool[text.Length];
            int maskCount = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                int p = positions[i];
                if (p >= 0 && p < mask.Length && !mask[p])
                {
                    mask[p] = true;
                    maskCount++;
                }
            }
            if (maskCount == 0)
            {
                DrawText(g, Ellipsize(g, font, text, bounds.Width), font, color, bounds, LeftMiddle);
                return;
            }

            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                                          TextFormatFlags.NoPadding;
            int x = bounds.Left;
            int right = bounds.Right;
            int index = 0;
            while (index < text.Length && x < right)
            {
                bool isMatch = mask[index];
                int start = index;
                while (index < text.Length && mask[index] == isMatch) index++;
                string run = text.Substring(start, index - start);
                int width = MeasureWidth(g, font, run);
                if (x + width > right)
                {
                    string tail = Ellipsize(g, font, text.Substring(start), right - x);
                    if (tail.Length > 0)
                    {
                        TextRenderer.DrawText(g, tail, font, new Rectangle(x, bounds.Top, right - x, bounds.Height),
                            isMatch ? highlight : color, flags);
                    }
                    return;
                }
                TextRenderer.DrawText(g, run, font, new Rectangle(x, bounds.Top, width + 2, bounds.Height),
                    isMatch ? highlight : color, flags);
                x += width;
            }
        }

        public static void DrawIconSized(Graphics g, Image image, Rectangle target)
        {
            if (image == null) return;
            InterpolationMode previous = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, target);
            g.InterpolationMode = previous;
        }

        public static Pen Pen(Color color, float width)
        {
            Pen pen = new Pen(color, width);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }


    }

    /// <summary>Small vector glyphs so the UI needs no icon font or image assets.</summary>
    internal static class Glyph
    {
        public static void Magnifier(Graphics g, Rectangle bounds, Color color, float width)
        {
            float size = Math.Min(bounds.Width, bounds.Height);
            float r = size * 0.30f;
            float cx = bounds.Left + size * 0.44f;
            float cy = bounds.Top + size * 0.44f;
            using (Pen pen = Ui.Pen(color, width))
            {
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                g.DrawLine(pen, cx + r * 0.72f, cy + r * 0.72f, bounds.Left + size * 0.92f, bounds.Top + size * 0.92f);
            }
        }

        public static void Sliders(Graphics g, Rectangle bounds, Color color, float width)
        {
            float left = bounds.Left + bounds.Width * 0.12f;
            float right = bounds.Right - bounds.Width * 0.12f;
            float[] ys = new float[]
            {
                bounds.Top + bounds.Height * 0.24f,
                bounds.Top + bounds.Height * 0.5f,
                bounds.Top + bounds.Height * 0.76f
            };
            float[] knobs = new float[] { 0.66f, 0.34f, 0.58f };
            using (Pen pen = Ui.Pen(color, width))
            using (SolidBrush brush = new SolidBrush(color))
            {
                for (int i = 0; i < ys.Length; i++)
                {
                    g.DrawLine(pen, left, ys[i], right, ys[i]);
                    float x = left + (right - left) * knobs[i];
                    float r = width * 1.5f;
                    g.FillEllipse(brush, x - r, ys[i] - r, r * 2, r * 2);
                }
            }
        }

        public static void Bookmark(Graphics g, Rectangle bounds, Color color)
        {
            float w = bounds.Width * 0.56f;
            float h = bounds.Height * 0.72f;
            float x = bounds.Left + (bounds.Width - w) / 2f;
            float y = bounds.Top + (bounds.Height - h) / 2f;
            PointF[] points = new PointF[]
            {
                new PointF(x, y),
                new PointF(x + w, y),
                new PointF(x + w, y + h),
                new PointF(x + w / 2f, y + h * 0.74f),
                new PointF(x, y + h)
            };
            using (SolidBrush brush = new SolidBrush(color)) g.FillPolygon(brush, points);
        }

        public static void Close(Graphics g, Rectangle bounds, Color color, float width)
        {
            int inset = (int)(bounds.Width * 0.32f);
            using (Pen pen = Ui.Pen(color, width))
            {
                g.DrawLine(pen, bounds.Left + inset, bounds.Top + inset, bounds.Right - inset, bounds.Bottom - inset);
                g.DrawLine(pen, bounds.Right - inset, bounds.Top + inset, bounds.Left + inset, bounds.Bottom - inset);
            }
        }

        public static void Check(Graphics g, Rectangle bounds, Color color, float width)
        {
            using (Pen pen = Ui.Pen(color, width))
            {
                Point[] points = new Point[]
                {
                    new Point(bounds.Left + (int)(bounds.Width * 0.22f), bounds.Top + (int)(bounds.Height * 0.52f)),
                    new Point(bounds.Left + (int)(bounds.Width * 0.42f), bounds.Top + (int)(bounds.Height * 0.72f)),
                    new Point(bounds.Left + (int)(bounds.Width * 0.78f), bounds.Top + (int)(bounds.Height * 0.30f))
                };
                g.DrawLines(pen, points);
            }
        }

        public static void Plus(Graphics g, Rectangle bounds, Color color, float width)
        {
            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f;
            float arm = Math.Min(bounds.Width, bounds.Height) * 0.26f;
            using (Pen pen = Ui.Pen(color, width))
            {
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
            }
        }

        public static void Minus(Graphics g, Rectangle bounds, Color color, float width)
        {
            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f;
            float arm = Math.Min(bounds.Width, bounds.Height) * 0.26f;
            using (Pen pen = Ui.Pen(color, width)) g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
        }

        public static void ChevronDown(Graphics g, Rectangle bounds, Color color, float width)
        {
            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f;
            float arm = Math.Min(bounds.Width, bounds.Height) * 0.2f;
            using (Pen pen = Ui.Pen(color, width))
            {
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(cx - arm, cy - arm * 0.5f),
                    new PointF(cx, cy + arm * 0.5f),
                    new PointF(cx + arm, cy - arm * 0.5f)
                });
            }
        }

        public static void Trash(Graphics g, Rectangle bounds, Color color, float width)
        {
            float left = bounds.Left + bounds.Width * 0.24f;
            float right = bounds.Right - bounds.Width * 0.24f;
            float top = bounds.Top + bounds.Height * 0.3f;
            float bottom = bounds.Bottom - bounds.Height * 0.16f;
            using (Pen pen = Ui.Pen(color, width))
            {
                g.DrawLine(pen, bounds.Left + bounds.Width * 0.16f, bounds.Top + bounds.Height * 0.24f,
                    bounds.Right - bounds.Width * 0.16f, bounds.Top + bounds.Height * 0.24f);
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(left, top), new PointF(left, bottom),
                    new PointF(right, bottom), new PointF(right, top)
                });
                g.DrawLine(pen, bounds.Left + bounds.Width * 0.38f, bounds.Top + bounds.Height * 0.14f,
                    bounds.Right - bounds.Width * 0.38f, bounds.Top + bounds.Height * 0.14f);
            }
        }
    }

    /// <summary>Small LRU of app icons extracted from executable paths.</summary>
    internal static class AppIcons
    {
        private const int MaxEntries = 64;
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> Order = new LinkedList<string>();

        public static Bitmap Get(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return null;
            Bitmap cached;
            if (Cache.TryGetValue(exePath, out cached)) return cached;
            Bitmap bitmap = null;
            try
            {
                if (File.Exists(exePath))
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (icon != null) bitmap = icon.ToBitmap();
                    }
                }
            }
            catch
            {
                bitmap = null;
            }
            Cache[exePath] = bitmap;
            Order.AddLast(exePath);
            while (Order.Count > MaxEntries)
            {
                string oldest = Order.First.Value;
                Order.RemoveFirst();
                Bitmap evicted;
                if (Cache.TryGetValue(oldest, out evicted))
                {
                    Cache.Remove(oldest);
                    if (evicted != null) evicted.Dispose();
                }
            }
            return bitmap;
        }
    }

    /// <summary>Thumbnails for image clipboard entries, scaled to the row box.</summary>
    internal static class Thumbnails
    {
        private const int MaxEntries = 48;
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private static readonly LinkedList<string> Order = new LinkedList<string>();

        public static Bitmap Get(string path, int width, int height)
        {
            if (string.IsNullOrEmpty(path) || width <= 0 || height <= 0) return null;
            string key = path + "|" + width + "x" + height;
            Bitmap cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Bitmap result = null;
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (Image source = Image.FromStream(stream, false, false))
                    {
                        // Cover-fit: fill the row thumbnail and crop the overflow.
                        float scale = Math.Max(width / (float)source.Width, height / (float)source.Height);
                        int w = Math.Max(1, (int)Math.Round(source.Width * scale));
                        int h = Math.Max(1, (int)Math.Round(source.Height * scale));
                        Bitmap target = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(target))
                        {
                            g.Clear(Color.Transparent);
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(source, new Rectangle((width - w) / 2, (height - h) / 2, w, h));
                        }
                        result = target;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Thumbnail failed for " + path + ": " + ex.Message);
                result = null;
            }

            Cache[key] = result;
            Order.AddLast(key);
            while (Order.Count > MaxEntries)
            {
                string oldest = Order.First.Value;
                Order.RemoveFirst();
                Bitmap evicted;
                if (Cache.TryGetValue(oldest, out evicted))
                {
                    Cache.Remove(oldest);
                    if (evicted != null) evicted.Dispose();
                }
            }
            return result;
        }

        public static void Clear()
        {
            foreach (KeyValuePair<string, Bitmap> pair in Cache)
            {
                if (pair.Value != null) pair.Value.Dispose();
            }
            Cache.Clear();
            Order.Clear();
        }
    }
}
