using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wincy
{
    /// <summary>
    /// Tiny string table. The app is bilingual (Traditional Chinese / English) and
    /// picks a default from the OS UI language; the choice is stored in settings.
    /// </summary>
    internal static class I18n
    {
        public const string Auto = "system";
        public const string Chinese = "zh-TW";
        public const string English = "en";

        private static string _language = Chinese;
        private static Dictionary<string, string> _table;

        public static string Language
        {
            get { return _language; }
        }

        public static string ResolveLanguage(string setting)
        {
            if (setting == English || setting == Chinese) return setting;
            CultureInfo ui = CultureInfo.CurrentUICulture;
            string name = ui == null ? "en" : ui.Name;
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                if (name.IndexOf("Hans", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.EndsWith("CN", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("SG", StringComparison.OrdinalIgnoreCase))
                {
                    return Chinese;
                }
                return Chinese;
            }
            if (name.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return English;
            return English;
        }

        public static void SetLanguage(string setting)
        {
            _language = ResolveLanguage(setting);
            _table = _language == Chinese ? Zh : En;
        }

        public static string T(string key)
        {
            return T(key, null);
        }

        public static string T(string key, params object[] args)
        {
            if (_table == null) SetLanguage(Auto);
            string format;
            if (!_table.TryGetValue(key, out format))
            {
                if (!En.TryGetValue(key, out format)) format = key;
            }
            if (args == null || args.Length == 0) return format;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>Plural-free English helper ("1 item" / "3 items").</summary>
        public static string Count(int n)
        {
            if (_language == Chinese) return string.Format(CultureInfo.InvariantCulture, "{0} 個項目", n);
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", n, n == 1 ? "item" : "items");
        }

        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "app.name", "Wincy" },
            { "app.subtitle", "剪貼簿管理員" },

            { "tray.show", "顯示歷史記錄" },
            { "tray.settings", "設定…" },
            { "tray.launch", "開機時啟動" },
            { "tray.clear", "清除歷史記錄" },
            { "tray.about", "關於 Wincy" },
            { "tray.quit", "結束 Wincy" },
            { "tray.tip", "Wincy — {0}" },
            { "tray.ready", "Wincy 正在執行，按 {0} 開啟剪貼簿歷史記錄。" },

            { "search.placeholder", "搜尋剪貼簿…" },
            { "empty.title", "沒有符合的項目" },
            { "empty.titleEmpty", "尚無剪貼簿記錄" },
            { "preview.empty", "選取項目即可預覽內容" },
            { "preview.files", "{0} 個檔案" },
            { "empty.hint", "複製一些文字、圖片或檔案來開始使用" },
            { "empty.hintSearch", "換個關鍵字再試一次" },

            { "hint.paste", "貼上" },
            { "hint.plain", "純文字" },
            { "hint.delete", "刪除" },
            { "hint.pin", "釘選" },
            { "hint.settings", "設定" },

            { "item.paste", "貼上" },
            { "item.pastePlain", "以純文字貼上" },
            { "item.copy", "複製到剪貼簿" },
            { "item.pin", "釘選" },
            { "item.unpin", "取消釘選" },
            { "item.delete", "刪除" },
            { "item.clearAll", "清除全部…" },
            { "item.clearConfirm", "確定清除 {0} 個項目？" },
            { "item.cancel", "取消" },

            { "kind.text", "文字" },
            { "kind.image", "圖片" },
            { "kind.files", "檔案" },

            { "time.now", "剛剛" },
            { "time.min", "{0} 分鐘前" },
            { "time.hour", "{0} 小時前" },
            { "time.yesterday", "昨天" },
            { "time.day", "{0} 天前" },

            { "settings.title", "設定" },
            { "settings.history", "歷史記錄" },
            { "settings.limit", "保留項目數量" },
            { "settings.limitHint", "釘選的項目不會被自動清除" },
            { "settings.shortcut", "快捷鍵" },
            { "settings.openHistory", "開啟歷史記錄" },
            { "settings.shortcutHint", "按一下後直接按下新的組合鍵" },
            { "settings.shortcutRecording", "請按下按鍵組合…" },
            { "settings.behavior", "行為" },
            { "settings.launch", "開機時自動啟動" },
            { "settings.autoPaste", "選取後自動貼上" },
            { "settings.autoPasteHint", "關閉時只會把項目複製到剪貼簿" },
            { "settings.plainByDefault", "預設以純文字貼上" },
            { "settings.ignorePassword", "忽略密碼與敏感欄位" },
            { "settings.ignorePasswordHint", "偵測 Windows 標示為機密的剪貼簿內容" },
            { "settings.saveImages", "儲存圖片內容" },
            { "settings.saveImagesHint", "關閉時只保留文字與檔案" },
            { "settings.appearance", "外觀" },
            { "settings.theme", "主題" },
            { "theme.system", "跟隨系統" },
            { "theme.light", "淺色" },
            { "theme.dark", "深色" },
            { "settings.language", "語言" },
            { "settings.ignoreApps", "忽略的應用程式" },
            { "settings.ignoreAppsHint", "每行一個執行檔名稱，例如 keepass.exe" },
            { "settings.done", "完成" },
            { "settings.openData", "開啟資料夾" },
            { "settings.saved", "已儲存" },
            { "settings.resetHistory", "清除歷史記錄" },

            { "about.title", "關於 Wincy" },
            { "about.tagline", "為 Windows 重寫的剪貼簿管理員" },
            { "about.version", "版本 {0}" },
            { "about.hotkey", "快捷鍵 {0}" },
            { "about.origin", "原始 macOS 專案：github.com/p0deje/Maccy" },
            { "about.close", "關閉" },

            { "msg.hotkeyFailed", "無法註冊快捷鍵 {0}，可能已被其他程式佔用。請在設定中更換。" },
            { "msg.hotkeyFallback", "快捷鍵 {0} 已被佔用，已改用 {1}。" },
            { "msg.cleared", "已清除歷史記錄" },
            { "msg.copied", "已複製到剪貼簿" },

            { "welcome.title", "Wincy 已就緒" },
            { "welcome.body", "按 {0} 開啟剪貼簿歷史記錄。" },

            { "record.hint", "請按下按鍵組合… (Esc 取消)" },
            { "errors.unexpected", "Wincy 發生未預期的錯誤。" }
        };

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "app.name", "Wincy" },
            { "app.subtitle", "Clipboard manager" },

            { "tray.show", "Show history" },
            { "tray.settings", "Settings…" },
            { "tray.launch", "Launch at login" },
            { "tray.clear", "Clear history" },
            { "tray.about", "About Wincy" },
            { "tray.quit", "Quit Wincy" },
            { "tray.tip", "Wincy — {0}" },
            { "tray.ready", "Wincy is running. Press {0} to open your clipboard history." },

            { "search.placeholder", "Search clipboard…" },
            { "empty.title", "No matching items" },
            { "empty.titleEmpty", "No clipboard history yet" },
            { "preview.empty", "Select an item to preview it" },
            { "preview.files", "{0} files" },
            { "empty.hint", "Copy some text, an image or files to get started" },
            { "empty.hintSearch", "Try a different search" },

            { "hint.paste", "paste" },
            { "hint.plain", "plain" },
            { "hint.delete", "delete" },
            { "hint.pin", "pin" },
            { "hint.settings", "settings" },

            { "item.paste", "Paste" },
            { "item.pastePlain", "Paste as plain text" },
            { "item.copy", "Copy to clipboard" },
            { "item.pin", "Pin" },
            { "item.unpin", "Unpin" },
            { "item.delete", "Delete" },
            { "item.clearAll", "Clear all…" },
            { "item.clearConfirm", "Delete all {0} items?" },
            { "item.cancel", "Cancel" },

            { "kind.text", "Text" },
            { "kind.image", "Image" },
            { "kind.files", "Files" },

            { "time.now", "just now" },
            { "time.min", "{0}m ago" },
            { "time.hour", "{0}h ago" },
            { "time.yesterday", "yesterday" },
            { "time.day", "{0}d ago" },

            { "settings.title", "Settings" },
            { "settings.history", "History" },
            { "settings.limit", "Items to keep" },
            { "settings.limitHint", "Pinned items are never removed" },
            { "settings.shortcut", "Shortcut" },
            { "settings.openHistory", "Open history" },
            { "settings.shortcutHint", "Click, then press the new key combination" },
            { "settings.shortcutRecording", "Press a key combination…" },
            { "settings.behavior", "Behaviour" },
            { "settings.launch", "Launch at login" },
            { "settings.autoPaste", "Paste after selecting" },
            { "settings.autoPasteHint", "When off, items are only copied to the clipboard" },
            { "settings.plainByDefault", "Paste as plain text by default" },
            { "settings.ignorePassword", "Ignore passwords and sensitive fields" },
            { "settings.ignorePasswordHint", "Respects clipboard content Windows marks as confidential" },
            { "settings.saveImages", "Store image content" },
            { "settings.saveImagesHint", "When off, only text and files are kept" },
            { "settings.appearance", "Appearance" },
            { "settings.theme", "Theme" },
            { "theme.system", "System" },
            { "theme.light", "Light" },
            { "theme.dark", "Dark" },
            { "settings.language", "Language" },
            { "settings.ignoreApps", "Ignored applications" },
            { "settings.ignoreAppsHint", "One executable name per line, e.g. keepass.exe" },
            { "settings.done", "Done" },
            { "settings.openData", "Open folder" },
            { "settings.saved", "Saved" },
            { "settings.resetHistory", "Clear history" },

            { "about.title", "About Wincy" },
            { "about.tagline", "A clipboard manager rebuilt for Windows" },
            { "about.version", "Version {0}" },
            { "about.hotkey", "Shortcut {0}" },
            { "about.origin", "Original macOS project: github.com/p0deje/Maccy" },
            { "about.close", "Close" },

            { "msg.hotkeyFailed", "Could not register {0}. Another app may already use it — pick another shortcut in Settings." },
            { "msg.hotkeyFallback", "{0} is taken, so {1} is used instead." },
            { "msg.cleared", "History cleared" },
            { "msg.copied", "Copied to clipboard" },

            { "welcome.title", "Wincy is ready" },
            { "welcome.body", "Press {0} to open your clipboard history." },

            { "record.hint", "Press a key combination… (Esc to cancel)" },
            { "errors.unexpected", "Wincy hit an unexpected error." }
        };
    }
}
