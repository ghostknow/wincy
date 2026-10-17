using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Wincy
{
    internal enum ItemKind
    {
        Text = 0,
        Image = 1,
        Files = 2
    }

    /// <summary>One clipboard capture.</summary>
    internal sealed class ClipboardItem
    {
        public string Id;
        public ItemKind Kind = ItemKind.Text;
        public string Text;
        public string Html;
        public string Rtf;
        /// <summary>Relative path (under the data folder) of the stored PNG, e.g. "images/ab12.png".</summary>
        public string Blob;
        public int ImageWidth;
        public int ImageHeight;
        public List<string> Files;
        public string SourceApp;
        public string SourcePath;
        public DateTime CreatedUtc = DateTime.UtcNow;
        public bool Pinned;
        public string Hash;

        private string _title;
        private string _searchBlob;

        public ClipboardItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Files = new List<string>();
        }

        public bool IsText
        {
            get { return Kind == ItemKind.Text; }
        }

        public bool IsImage
        {
            get { return Kind == ItemKind.Image; }
        }

        public bool IsFiles
        {
            get { return Kind == ItemKind.Files; }
        }

        /// <summary>Single line summary shown in the list.</summary>
        public string Title
        {
            get
            {
                if (_title != null) return _title;
                switch (Kind)
                {
                    case ItemKind.Image:
                        if (ImageWidth > 0 && ImageHeight > 0)
                        {
                            _title = string.Format(CultureInfo.InvariantCulture, "{0}  {1} × {2}",
                                I18n.T("kind.image"), ImageWidth, ImageHeight);
                        }
                        else
                        {
                            _title = I18n.T("kind.image");
                        }
                        break;
                    case ItemKind.Files:
                        _title = FilesTitle();
                        break;
                    default:
                        _title = TextUtil.SingleLine(Text, 600);
                        break;
                }
                if (_title == null) _title = string.Empty;
                return _title;
            }
        }

        public string KindLabel
        {
            get
            {
                switch (Kind)
                {
                    case ItemKind.Image:
                        return I18n.T("kind.image");
                    case ItemKind.Files:
                        return I18n.T("kind.files");
                    default:
                        return I18n.T("kind.text");
                }
            }
        }

        public string SourceLabel
        {
            get
            {
                if (string.IsNullOrEmpty(SourceApp)) return I18n.T("app.name");
                return SourceApp;
            }
        }

        public string AgeLabel
        {
            get { return Humanize.Ago(CreatedUtc); }
        }

        /// <summary>Everything a search may match, lower-cased lazily.</summary>
        public string SearchBlob
        {
            get
            {
                if (_searchBlob != null) return _searchBlob;
                StringBuilder sb = new StringBuilder();
                if (Text != null) sb.Append(Text);
                if (Files != null)
                {
                    for (int i = 0; i < Files.Count; i++)
                    {
                        sb.Append('\n').Append(Files[i]);
                    }
                }
                if (!string.IsNullOrEmpty(SourceApp)) sb.Append('\n').Append(SourceApp);
                _searchBlob = sb.ToString().ToLowerInvariant();
                return _searchBlob;
            }
        }

        private string FilesTitle()
        {
            if (Files == null || Files.Count == 0) return I18n.T("kind.files");
            string first = System.IO.Path.GetFileName(Files[0]);
            if (string.IsNullOrEmpty(first)) first = Files[0];
            if (Files.Count == 1) return first;
            return I18n.Language == I18n.Chinese
                ? string.Format(CultureInfo.InvariantCulture, "{0} 及另外 {1} 個檔案", first, Files.Count - 1)
                : string.Format(CultureInfo.InvariantCulture, "{0} and {1} more", first, Files.Count - 1);
        }


        public JsonValue ToJson()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("id", Id);
            o.Set("kind", KindToString(Kind));
            o.Set("created", CreatedUtc.ToString("o", CultureInfo.InvariantCulture));
            o.Set("pinned", FromBool(Pinned));
            if (!string.IsNullOrEmpty(Hash)) o.Set("hash", Hash);
            if (!string.IsNullOrEmpty(Text)) o.Set("text", Text);
            if (!string.IsNullOrEmpty(Html)) o.Set("html", Html);
            if (!string.IsNullOrEmpty(Rtf)) o.Set("rtf", Rtf);
            if (!string.IsNullOrEmpty(Blob)) o.Set("blob", Blob);
            if (ImageWidth > 0) o.Set("width", ImageWidth);
            if (ImageHeight > 0) o.Set("height", ImageHeight);
            if (!string.IsNullOrEmpty(SourceApp)) o.Set("sourceApp", SourceApp);
            if (!string.IsNullOrEmpty(SourcePath)) o.Set("sourcePath", SourcePath);
            if (Files != null && Files.Count > 0)
            {
                JsonValue arr = JsonValue.NewArray();
                for (int i = 0; i < Files.Count; i++) arr.Add(JsonValue.FromString(Files[i]));
                o.Set("files", arr);
            }
            return o;
        }

        private static JsonValue FromBool(bool value)
        {
            return JsonValue.FromBool(value);
        }

        public static ClipboardItem FromJson(JsonValue o)
        {
            if (o == null || o.Kind != JsonKind.Object) return null;
            ClipboardItem item = new ClipboardItem();
            string id = o.GetString("id", null);
            if (!string.IsNullOrEmpty(id)) item.Id = id;
            item.Kind = StringToKind(o.GetString("kind", "text"));
            item.Text = o.GetString("text", null);
            item.Html = o.GetString("html", null);
            item.Rtf = o.GetString("rtf", null);
            item.Blob = o.GetString("blob", null);
            item.ImageWidth = o.GetInt("width", 0);
            item.ImageHeight = o.GetInt("height", 0);
            item.SourceApp = o.GetString("sourceApp", null);
            item.SourcePath = o.GetString("sourcePath", null);
            item.Pinned = o.GetBool("pinned", false);
            item.Hash = o.GetString("hash", null);
            string created = o.GetString("created", null);
            DateTime parsed;
            if (!string.IsNullOrEmpty(created) &&
                DateTime.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            {
                item.CreatedUtc = parsed.ToUniversalTime();
            }
            item.Files = o.GetStringList("files");
            if (item.Kind == ItemKind.Text && string.IsNullOrEmpty(item.Text)) return null;
            if (item.Kind == ItemKind.Image && string.IsNullOrEmpty(item.Blob)) return null;
            return item;
        }

        private static string KindToString(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Image:
                    return "image";
                case ItemKind.Files:
                    return "files";
                default:
                    return "text";
            }
        }

        private static ItemKind StringToKind(string value)
        {
            if (string.Equals(value, "image", StringComparison.OrdinalIgnoreCase)) return ItemKind.Image;
            if (string.Equals(value, "files", StringComparison.OrdinalIgnoreCase)) return ItemKind.Files;
            return ItemKind.Text;
        }
    }

    internal static class TextUtil
    {
        /// <summary>Flatten to one line, collapsing whitespace, and elide at <paramref name="max"/> chars.</summary>
        public static string SingleLine(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder sb = new StringBuilder(Math.Min(text.Length, max + 8));
            bool pendingSpace = false;
            bool sawContent = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == '\u00A0')
                {
                    if (sawContent) pendingSpace = true;
                    continue;
                }
                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }
                if (sb.Length >= max)
                {
                    sb.Append('…');
                    break;
                }
                sb.Append(c);
                sawContent = true;
            }
            return sb.ToString().Trim();
        }

        public static string Sha1Hex(string value)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static string Sha1Hex(byte[] data)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(data ?? new byte[0]);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static string Truncate(string value, int max)
        {
            if (value == null) return null;
            if (value.Length <= max) return value;
            return value.Substring(0, max);
        }


    }
}
