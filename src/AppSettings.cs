using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Wincy
{
    internal sealed class AppSettings
    {
        public const int MinHistoryLimit = 10;
        public const int MaxHistoryLimit = 5000;

        public int HistoryLimit = 200;
        public int MaxTextLength = 100000;
        public HotkeySpec Hotkey;
        public bool LaunchAtLogin;
        public bool AutoPaste = true;
        public bool PastePlainByDefault;
        public bool IgnoreSensitive = true;
        public bool SaveImages = true;
        public int MaxImageBytes = 12000000;
        public string Theme = "system";
        public string Language = I18n.Auto;
        public bool ShowWelcome = true;
        public List<string> IgnoreApps = new List<string>();

        public AppSettings()
        {
            Hotkey = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.V);
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                string text = Paths.ReadAllText(Paths.SettingsFile);
                if (string.IsNullOrEmpty(text)) return settings;
                JsonValue root = Json.Parse(text);
                settings.HistoryLimit = Clamp(root.GetInt("historyLimit", settings.HistoryLimit), MinHistoryLimit, MaxHistoryLimit);
                settings.MaxTextLength = Clamp(root.GetInt("maxTextLength", settings.MaxTextLength), 1000, 4000000);
                settings.MaxImageBytes = Clamp(root.GetInt("maxImageBytes", settings.MaxImageBytes), 65536, 100 * 1024 * 1024);
                settings.LaunchAtLogin = root.GetBool("launchAtLogin", settings.LaunchAtLogin);
                settings.AutoPaste = root.GetBool("autoPaste", settings.AutoPaste);
                settings.PastePlainByDefault = root.GetBool("pastePlainByDefault", settings.PastePlainByDefault);
                settings.IgnoreSensitive = root.GetBool("ignoreSensitive", settings.IgnoreSensitive);
                settings.SaveImages = root.GetBool("saveImages", settings.SaveImages);
                settings.ShowWelcome = root.GetBool("showWelcome", settings.ShowWelcome);
                settings.Theme = NormalizeTheme(root.GetString("theme", settings.Theme));
                settings.Language = I18n.ResolveLanguage(root.GetString("language", settings.Language));
                HotkeySpec hotkey = HotkeySpec.Parse(root.GetString("hotkey", null));
                if (hotkey != null && hotkey.IsValid) settings.Hotkey = hotkey;
                List<string> ignored = root.GetStringList("ignoreApps");
                settings.IgnoreApps = NormalizeAppList(ignored);
            }
            catch (Exception ex)
            {
                Log.Error("Could not read settings.json, using defaults", ex);
                TryBackupCorrupt(Paths.SettingsFile);
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                JsonValue root = JsonValue.NewObject();
                root.Set("historyLimit", HistoryLimit);
                root.Set("maxTextLength", MaxTextLength);
                root.Set("maxImageBytes", MaxImageBytes);
                root.Set("hotkey", Hotkey == null ? "Ctrl+Shift+V" : Hotkey.Serialize());
                root.Set("launchAtLogin", LaunchAtLogin);
                root.Set("autoPaste", AutoPaste);
                root.Set("pastePlainByDefault", PastePlainByDefault);
                root.Set("ignoreSensitive", IgnoreSensitive);
                root.Set("saveImages", SaveImages);
                root.Set("theme", Theme);
                root.Set("language", Language);
                root.Set("showWelcome", ShowWelcome);
                JsonValue apps = JsonValue.NewArray();
                for (int i = 0; i < IgnoreApps.Count; i++) apps.Add(JsonValue.FromString(IgnoreApps[i]));
                root.Set("ignoreApps", apps);
                Paths.WriteAllTextAtomic(Paths.SettingsFile, Json.Write(root, true) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Log.Error("Could not write settings.json", ex);
            }
        }

        public bool IsAppIgnored(string processName)
        {
            if (string.IsNullOrEmpty(processName) || IgnoreApps.Count == 0) return false;
            string normalized = NormalizeAppName(processName);
            for (int i = 0; i < IgnoreApps.Count; i++)
            {
                if (string.Equals(IgnoreApps[i], normalized, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string NormalizeAppName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string name = value.Trim().Trim('"');
            int slash = name.LastIndexOfAny(new char[] { '\\', '/' });
            if (slash >= 0 && slash < name.Length - 1) name = name.Substring(slash + 1);
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return name.ToLowerInvariant();
        }

        private static List<string> NormalizeAppList(List<string> values)
        {
            List<string> result = new List<string>();
            if (values == null) return result;
            for (int i = 0; i < values.Count; i++)
            {
                string name = NormalizeAppName(values[i]);
                if (name.Length == 0) continue;
                if (!result.Contains(name)) result.Add(name);
            }
            return result;
        }

        public static List<string> ParseAppList(string multiline)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(multiline)) return result;
            string[] lines = multiline.Split(new string[] { "\r\n", "\n", "\r", ",", ";" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string name = NormalizeAppName(lines[i]);
                if (name.Length == 0) continue;
                if (!result.Contains(name)) result.Add(name);
            }
            return result;
        }

        public static string ToMultiline(List<string> apps)
        {
            if (apps == null || apps.Count == 0) return string.Empty;
            return string.Join(Environment.NewLine, apps.ToArray());
        }

        public static string NormalizeTheme(string theme)
        {
            if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase)) return "light";
            if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)) return "dark";
            return "system";
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void TryBackupCorrupt(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                File.Copy(path, backup, true);
                Log.Warn("Backed up unreadable file to " + backup);
            }
            catch
            {
            }
        }
    }
}
