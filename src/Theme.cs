using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Wincy
{
    internal static class Dpi
    {
        public static int Value = 96;

        public static float Factor
        {
            get { return Value / 96f; }
        }

        public static int S(int pixels)
        {
            return (int)Math.Round(pixels * Value / 96.0);
        }

        public static float Sf(float pixels)
        {
            return (float)(pixels * Value / 96.0);
        }


    }

    internal sealed class Palette
    {
        public bool Dark;
        public Color Bg;
        public Color BgHover;
        public Color BgInput;
        public Color Border;
        public Color Text;
        public Color TextMuted;
        public Color TextFaint;
        public Color Accent;
        public Color AccentSoft;
        public Color OnAccent;
        public Color OnAccentMuted;
        public Color Separator;
        public Color Danger;
        public Color ScrollThumb;
        public Color Shadow;

        public static Palette MakeLight()
        {
            Palette p = new Palette();
            p.Dark = false;
            p.Bg = Color.FromArgb(255, 255, 255, 255);
            p.BgHover = Color.FromArgb(255, 243, 244, 248);
            p.BgInput = Color.FromArgb(255, 242, 243, 247);
            p.Border = Color.FromArgb(255, 226, 227, 233);
            p.Text = Color.FromArgb(255, 22, 23, 27);
            p.TextMuted = Color.FromArgb(255, 118, 120, 130);
            p.TextFaint = Color.FromArgb(255, 165, 166, 175);
            p.Accent = Color.FromArgb(255, 60, 86, 240);
            p.AccentSoft = Color.FromArgb(255, 234, 238, 255);
            p.OnAccent = Color.FromArgb(255, 255, 255, 255);
            p.OnAccentMuted = Color.FromArgb(210, 255, 255, 255);
            p.Separator = Color.FromArgb(255, 237, 238, 243);
            p.Danger = Color.FromArgb(255, 222, 60, 66);
            p.ScrollThumb = Color.FromArgb(255, 198, 200, 210);
            p.Shadow = Color.FromArgb(40, 0, 0, 0);
            return p;
        }

        public static Palette MakeDark()
        {
            Palette p = new Palette();
            p.Dark = true;
            p.Bg = Color.FromArgb(255, 28, 29, 33);
            p.BgHover = Color.FromArgb(255, 39, 40, 46);
            p.BgInput = Color.FromArgb(255, 39, 40, 46);
            p.Border = Color.FromArgb(255, 52, 53, 60);
            p.Text = Color.FromArgb(255, 237, 238, 242);
            p.TextMuted = Color.FromArgb(255, 152, 154, 164);
            p.TextFaint = Color.FromArgb(255, 112, 114, 124);
            p.Accent = Color.FromArgb(255, 108, 134, 255);
            p.AccentSoft = Color.FromArgb(255, 44, 52, 88);
            p.OnAccent = Color.FromArgb(255, 255, 255, 255);
            p.OnAccentMuted = Color.FromArgb(200, 255, 255, 255);
            p.Separator = Color.FromArgb(255, 43, 44, 50);
            p.Danger = Color.FromArgb(255, 255, 107, 107);
            p.ScrollThumb = Color.FromArgb(255, 74, 76, 86);
            p.Shadow = Color.FromArgb(70, 0, 0, 0);
            return p;
        }

        public static bool SystemPrefersDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int) return (int)value == 0;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static Palette Resolve(string theme)
        {
            string mode = AppSettings.NormalizeTheme(theme);
            bool dark = mode == "dark" || (mode == "system" && SystemPrefersDark());
            return dark ? MakeDark() : MakeLight();
        }
    }

    internal static class Fonts
    {
        private static readonly Dictionary<string, Font> Cache = new Dictionary<string, Font>(StringComparer.Ordinal);
        private static string _family;
        private static string _monoFamily;

        public static string Family
        {
            get
            {
                if (_family == null) Initialize();
                return _family;
            }
        }

        public static void Initialize()
        {
            if (_family != null) return;
            List<string> installed = new List<string>();
            try
            {
                using (InstalledFontCollection collection = new InstalledFontCollection())
                {
                    for (int i = 0; i < collection.Families.Length; i++) installed.Add(collection.Families[i].Name);
                }
            }
            catch
            {
            }

            string preferred;
            string culture = CultureInfo.CurrentUICulture.Name;
            if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                preferred = "Microsoft JhengHei UI";
                if (!installed.Contains(preferred)) preferred = "Microsoft YaHei UI";
                if (!installed.Contains(preferred)) preferred = "Segoe UI";
            }
            else if (culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            {
                preferred = "Yu Gothic UI";
            }
            else if (culture.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
            {
                preferred = "Malgun Gothic";
            }
            else
            {
                preferred = "Segoe UI Variable Text";
                if (!installed.Contains(preferred)) preferred = "Segoe UI";
            }

            if (I18n.Language == I18n.Chinese && installed.Contains("Microsoft JhengHei UI"))
            {
                preferred = "Microsoft JhengHei UI";
            }

            if (installed.Count > 0 && !installed.Contains(preferred)) preferred = "Segoe UI";
            _family = preferred;
            _monoFamily = installed.Contains("Cascadia Mono") ? "Cascadia Mono" : "Consolas";
        }

        public static Font Ui(float points)
        {
            return Get(Family, points, FontStyle.Regular);
        }

        public static Font Bold(float points)
        {
            return Get(Family, points, FontStyle.Bold);
        }

        public static Font Mono(float points)
        {
            if (_monoFamily == null) Initialize();
            return Get(_monoFamily, points, FontStyle.Regular);
        }

        private static Font Get(string family, float points, FontStyle style)
        {
            string key = family + "|" + points.ToString("0.##", CultureInfo.InvariantCulture) + "|" + (int)style;
            Font font;
            if (Cache.TryGetValue(key, out font)) return font;
            try
            {
                font = new Font(family, points, style, GraphicsUnit.Point);
            }
            catch
            {
                font = new Font("Segoe UI", points, style, GraphicsUnit.Point);
            }
            Cache[key] = font;
            return font;
        }


    }
}
