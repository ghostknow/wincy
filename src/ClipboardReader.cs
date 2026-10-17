using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Wincy
{
    internal enum CaptureStatus
    {
        Ok,
        Retry,
        Skip
    }

    /// <summary>Reads the Windows clipboard into a <see cref="ClipboardItem"/> (UI thread only).</summary>
    internal static class ClipboardReader
    {
        private const string FormatExcludeMonitor = "ExcludeClipboardContentFromMonitorProcessing";
        private const string FormatViewerIgnore = "Clipboard Viewer Ignore";
        private const string FormatCanInclude = "CanIncludeInClipboardHistory";
        private const string FormatCanUpload = "CanUploadToCloudClipboard";
        private const int MaxHtmlLength = 512 * 1024;
        private const int MaxRtfLength = 512 * 1024;
        private const long MaxImagePixels = 50000000;
        private const int MaxFiles = 500;

        private static readonly int CurrentProcessId = Process.GetCurrentProcess().Id;

        public static CaptureStatus Read(AppSettings settings, out ClipboardItem item, out string reason)
        {
            item = null;
            reason = null;

            IDataObject data = null;
            try
            {
                data = Clipboard.GetDataObject();
            }
            catch (Exception ex)
            {
                reason = "clipboard unavailable: " + ex.Message;
                return CaptureStatus.Retry;
            }
            if (data == null)
            {
                reason = "no clipboard data";
                return CaptureStatus.Retry;
            }

            try
            {
                string[] formats;
                try
                {
                    formats = data.GetFormats(false);
                }
                catch
                {
                    formats = new string[0];
                }

                if (settings.IgnoreSensitive && IsSensitive(data, formats, out reason))
                {
                    return CaptureStatus.Skip;
                }

                if (HasFormat(data, formats, ClipboardFormats.SelfWrite))
                {
                    reason = "clipboard written by Wincy";
                    return CaptureStatus.Skip;
                }

                string processName;
                string displayName;
                string sourcePath;
                uint ownerPid;
                ResolveSource(out processName, out displayName, out sourcePath, out ownerPid);
                if (ownerPid == (uint)CurrentProcessId)
                {
                    reason = "clipboard written by Wincy";
                    return CaptureStatus.Skip;
                }
                if (processName != null && settings.IsAppIgnored(processName))
                {
                    reason = "source app is ignored: " + processName;
                    return CaptureStatus.Skip;
                }
                string sourceApp = displayName ?? processName;

                // Files first: Explorer also publishes a thumbnail bitmap for copied files.
                string[] files = TryGetFiles(data);
                if (files != null && files.Length > 0)
                {
                    item = BuildFiles(files, settings);
                    item.SourceApp = sourceApp;
                    item.SourcePath = sourcePath;
                    item.Hash = "F" + TextUtil.Sha1Hex(string.Join("\u0001", files));
                    return CaptureStatus.Ok;
                }

                if (HasFormat(data, formats, DataFormats.Bitmap) || HasFormat(data, formats, "PNG"))
                {
                    if (!settings.SaveImages)
                    {
                        reason = "images are disabled";
                        return CaptureStatus.Skip;
                    }
                    Image image = TryGetImage(data);
                    if (image != null)
                    {
                        using (image)
                        {
                            if ((long)image.Width * image.Height > MaxImagePixels)
                            {
                                reason = "image too large";
                                return CaptureStatus.Skip;
                            }
                            byte[] png = EncodePng(image);
                            if (png == null || png.Length > settings.MaxImageBytes)
                            {
                                reason = "image too large to store";
                                return CaptureStatus.Skip;
                            }
                            item = new ClipboardItem();
                            item.Kind = ItemKind.Image;
                            item.ImageWidth = image.Width;
                            item.ImageHeight = image.Height;
                            item.SourceApp = sourceApp;
                            item.SourcePath = sourcePath;
                            item.Hash = "I" + TextUtil.Sha1Hex(png);
                            item.Id = NewId();
                            item.Blob = "images/" + item.Id + ".png";
                            Paths.EnsureDirectories();
                            string full = Path.Combine(Paths.Root, item.Blob.Replace('/', Path.DirectorySeparatorChar));
                            File.WriteAllBytes(full, png);
                            return CaptureStatus.Ok;
                        }
                    }
                }

                string text = TryGetText(data, formats);
                if (text == null)
                {
                    reason = "unsupported clipboard format";
                    return CaptureStatus.Skip;
                }
                if (IsBlank(text))
                {
                    reason = "blank text";
                    return CaptureStatus.Skip;
                }
                if (text.Length > settings.MaxTextLength) text = text.Substring(0, settings.MaxTextLength);

                item = new ClipboardItem();
                item.Kind = ItemKind.Text;
                item.Text = text;
                item.SourceApp = sourceApp;
                item.SourcePath = sourcePath;
                item.Hash = "T" + TextUtil.Sha1Hex(text);
                if (HasFormat(data, formats, DataFormats.Html))
                {
                    string html = SafeString(data, DataFormats.Html);
                    if (!string.IsNullOrEmpty(html) && html.Length <= MaxHtmlLength) item.Html = html;
                }
                if (HasFormat(data, formats, DataFormats.Rtf))
                {
                    string rtf = SafeString(data, DataFormats.Rtf);
                    if (!string.IsNullOrEmpty(rtf) && rtf.Length <= MaxRtfLength) item.Rtf = rtf;
                }
                return CaptureStatus.Ok;
            }
            catch (Exception ex)
            {
                reason = "read failed: " + ex.Message;
                return CaptureStatus.Retry;
            }
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static ClipboardItem BuildFiles(string[] files, AppSettings settings)
        {
            ClipboardItem item = new ClipboardItem();
            item.Kind = ItemKind.Files;
            int count = Math.Min(files.Length, MaxFiles);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                item.Files.Add(files[i]);
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(files[i]);
            }
            string text = sb.ToString();
            if (text.Length > settings.MaxTextLength) text = text.Substring(0, settings.MaxTextLength);
            item.Text = text;
            return item;
        }

        private static bool IsSensitive(IDataObject data, string[] formats, out string reason)
        {
            reason = null;
            if (HasFormat(data, formats, FormatExcludeMonitor) || HasFormat(data, formats, FormatViewerIgnore))
            {
                reason = "clipboard content is marked confidential";
                return true;
            }
            int? canInclude = TryGetDword(data, FormatCanInclude);
            if (canInclude.HasValue && canInclude.Value == 0)
            {
                reason = "excluded from clipboard history";
                return true;
            }
            int? canUpload = TryGetDword(data, FormatCanUpload);
            if (canUpload.HasValue && canUpload.Value == 0)
            {
                reason = "excluded from cloud clipboard";
                return true;
            }
            return false;
        }

        private static void ResolveSource(out string processName, out string displayName, out string appPath, out uint pid)
        {
            processName = null;
            displayName = null;
            appPath = null;
            pid = 0;
            try
            {
                IntPtr owner = Native.GetClipboardOwner();
                if (owner == IntPtr.Zero) owner = Native.GetForegroundWindow();
                if (owner == IntPtr.Zero) return;
                uint ownerPid;
                Native.GetWindowThreadProcessId(owner, out ownerPid);
                if (ownerPid == 0 || ownerPid == (uint)CurrentProcessId) return;
                pid = ownerPid;
                using (Process process = Process.GetProcessById((int)ownerPid))
                {
                    processName = process.ProcessName;
                    try
                    {
                        appPath = process.MainModule == null ? null : process.MainModule.FileName;
                    }
                    catch
                    {
                        appPath = null;
                    }
                }
                if (!string.IsNullOrEmpty(appPath))
                {
                    try
                    {
                        FileVersionInfo info = FileVersionInfo.GetVersionInfo(appPath);
                        string description = info.FileDescription;
                        if (!string.IsNullOrEmpty(description))
                        {
                            description = description.Trim();
                            if (description.Length > 0 && description.Length <= 40) displayName = description;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
                processName = null;
                displayName = null;
                appPath = null;
            }
        }

        private static bool IsBlank(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]) && text[i] != '\u00A0' && text[i] != '\u200B') return false;
            }
            return true;
        }

        private static bool HasFormat(IDataObject data, string[] formats, string format)
        {
            if (formats != null)
            {
                for (int i = 0; i < formats.Length; i++)
                {
                    if (string.Equals(formats[i], format, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            try
            {
                return data.GetDataPresent(format, false);
            }
            catch
            {
                return false;
            }
        }

        private static string[] TryGetFiles(IDataObject data)
        {
            try
            {
                if (!data.GetDataPresent(DataFormats.FileDrop, false)) return null;
                object raw = data.GetData(DataFormats.FileDrop, false);
                string[] array = raw as string[];
                if (array != null) return array;
                StringCollection collection = raw as StringCollection;
                if (collection != null)
                {
                    string[] result = new string[collection.Count];
                    collection.CopyTo(result, 0);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read file drop: " + ex.Message);
            }
            return null;
        }

        private static Image TryGetImage(IDataObject data)
        {
            try
            {
                object raw = data.GetData(DataFormats.Bitmap, false);
                Image image = raw as Image;
                if (image != null) return new Bitmap(image);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read bitmap: " + ex.Message);
            }
            try
            {
                if (data.GetDataPresent("PNG", false))
                {
                    object raw = data.GetData("PNG", false);
                    MemoryStream stream = raw as MemoryStream;
                    if (stream != null)
                    {
                        stream.Position = 0;
                        return Image.FromStream(stream, true, true);
                    }
                    byte[] bytes = raw as byte[];
                    if (bytes != null) return Image.FromStream(new MemoryStream(bytes), true, true);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read PNG clipboard format: " + ex.Message);
            }
            return null;
        }

        private static string TryGetText(IDataObject data, string[] formats)
        {
            try
            {
                if (HasFormat(data, formats, DataFormats.UnicodeText))
                {
                    string value = data.GetData(DataFormats.UnicodeText, false) as string;
                    if (value != null) return value;
                }
                if (HasFormat(data, formats, DataFormats.Text))
                {
                    return data.GetData(DataFormats.Text, false) as string;
                }
                if (HasFormat(data, formats, DataFormats.StringFormat))
                {
                    return data.GetData(DataFormats.StringFormat, false) as string;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read text: " + ex.Message);
            }
            return null;
        }

        private static string SafeString(IDataObject data, string format)
        {
            try
            {
                return data.GetData(format, false) as string;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] EncodePng(Image image)
        {
            try
            {
                using (Bitmap copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(copy))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
                    }
                    using (MemoryStream stream = new MemoryStream())
                    {
                        copy.Save(stream, ImageFormat.Png);
                        return stream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PNG encode failed: " + ex.Message);
                return null;
            }
        }

        private static int? TryGetDword(IDataObject data, string format)
        {
            try
            {
                if (!data.GetDataPresent(format, false)) return null;
                object raw = data.GetData(format, false);
                if (raw is int) return (int)raw;
                if (raw is uint) return unchecked((int)(uint)raw);
                byte[] bytes = raw as byte[];
                if (bytes != null && bytes.Length >= 4) return BitConverter.ToInt32(bytes, 0);
                MemoryStream stream = raw as MemoryStream;
                if (stream != null)
                {
                    byte[] buffer = new byte[4];
                    stream.Position = 0;
                    if (stream.Read(buffer, 0, 4) == 4) return BitConverter.ToInt32(buffer, 0);
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
