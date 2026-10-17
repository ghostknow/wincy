using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>Private clipboard formats shared by the reader and the writer.</summary>
    internal static class ClipboardFormats
    {
        /// <summary>Marker written next to our own data so the watcher ignores it.</summary>
        public const string SelfWrite = "Wincy.SelfWrite";
    }

    /// <summary>Puts a history item back on the Windows clipboard.</summary>
    internal static class ClipboardWriter
    {
        public static bool Put(ClipboardItem item, bool plainText)
        {
            if (item == null) return false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    WriteCore(item, plainText);
                    return true;
                }
                catch (Exception ex)
                {
                    if (attempt == 4) Log.Error("Could not write clipboard", ex);
                    Thread.Sleep(35);
                }
            }
            return false;
        }

        private static void WriteCore(ClipboardItem item, bool plainText)
        {
            switch (item.Kind)
            {
                case ItemKind.Image:
                    WriteImage(item);
                    return;
                case ItemKind.Files:
                    WriteFiles(item);
                    return;
                default:
                    WriteText(item, plainText);
                    return;
            }
        }

        private static DataObject NewDataObject()
        {
            DataObject data = new DataObject();
            data.SetData(ClipboardFormats.SelfWrite, 1);
            return data;
        }

        private static void WriteText(ClipboardItem item, bool plainText)
        {
            string text = item.Text ?? string.Empty;
            if (plainText || (string.IsNullOrEmpty(item.Html) && string.IsNullOrEmpty(item.Rtf)))
            {
                if (string.IsNullOrEmpty(text)) return;
                DataObject plain = NewDataObject();
                plain.SetData(DataFormats.UnicodeText, text);
                Clipboard.SetDataObject(plain, true);
                return;
            }

            DataObject data = NewDataObject();
            data.SetData(DataFormats.UnicodeText, text);
            if (!string.IsNullOrEmpty(item.Html))
            {
                try
                {
                    data.SetData(DataFormats.Html, item.Html);
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not attach HTML flavour: " + ex.Message);
                }
            }
            if (!string.IsNullOrEmpty(item.Rtf))
            {
                try
                {
                    data.SetData(DataFormats.Rtf, item.Rtf);
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not attach RTF flavour: " + ex.Message);
                }
            }
            Clipboard.SetDataObject(data, true);
        }

        private static void WriteImage(ClipboardItem item)
        {
            string path = Path.Combine(Paths.Root, (item.Blob ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                Log.Warn("Image blob missing: " + path);
                return;
            }
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image source = Image.FromStream(stream, false, false))
            using (Bitmap copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(copy))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
                }
                DataObject data = NewDataObject();
                data.SetData(DataFormats.Bitmap, copy);
                try
                {
                    using (MemoryStream png = new MemoryStream())
                    {
                        copy.Save(png, ImageFormat.Png);
                        png.Position = 0;
                        data.SetData("PNG", png);
                        Clipboard.SetDataObject(data, true);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("PNG flavour failed, falling back to bitmap only: " + ex.Message);
                }
                Clipboard.SetDataObject(data, true);
            }
        }

        private static void WriteFiles(ClipboardItem item)
        {
            StringCollection paths = new StringCollection();
            List<string> existing = new List<string>();
            for (int i = 0; i < item.Files.Count; i++)
            {
                string file = item.Files[i];
                if (string.IsNullOrEmpty(file)) continue;
                paths.Add(file);
                existing.Add(file);
            }
            if (paths.Count == 0) return;

            DataObject data = NewDataObject();
            data.SetData(DataFormats.FileDrop, paths);
            string[] array = existing.ToArray();
            try
            {
                data.SetData("FileNameW", array);
                data.SetData("FileName", array);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not attach file name formats: " + ex.Message);
            }
            Clipboard.SetDataObject(data, true);
        }
    }
}
