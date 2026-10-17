using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Wincy
{
    [Flags]
    internal enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    /// <summary>A single global shortcut: modifiers + non-modifier key.</summary>
    internal sealed class HotkeySpec
    {
        private static Dictionary<string, Keys> _byName;

        public HotkeyModifiers Modifiers = HotkeyModifiers.None;
        public Keys Key = Keys.None;

        public HotkeySpec()
        {
        }

        public HotkeySpec(HotkeyModifiers modifiers, Keys key)
        {
            Modifiers = modifiers;
            Key = key & Keys.KeyCode;
        }

        public HotkeySpec Clone()
        {
            return new HotkeySpec(Modifiers, Key);
        }

        public bool IsValid
        {
            get { return Modifiers != HotkeyModifiers.None && Key != Keys.None && !IsModifierKey(Key); }
        }

        public uint NativeModifiers
        {
            get { return (uint)Modifiers & 0xFu; }
        }

        public string Display
        {
            get { return Format("+"); }
        }

        public string Pretty
        {
            get { return Format(" + "); }
        }

        private string Format(string separator)
        {
            List<string> parts = new List<string>(5);
            if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
            if (Key != Keys.None) parts.Add(KeyGlyph(Key));
            return string.Join(separator, parts.ToArray());
        }

        public string Serialize()
        {
            List<string> parts = new List<string>(5);
            if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
            if (Key != Keys.None) parts.Add(KeyName(Key));
            return string.Join("+", parts.ToArray());
        }

        public override string ToString()
        {
            return Display;
        }

        public static HotkeySpec FromKeyData(Keys keyData)
        {
            HotkeyModifiers mods = HotkeyModifiers.None;
            if ((keyData & Keys.Control) == Keys.Control) mods |= HotkeyModifiers.Control;
            if ((keyData & Keys.Alt) == Keys.Alt) mods |= HotkeyModifiers.Alt;
            if ((keyData & Keys.Shift) == Keys.Shift) mods |= HotkeyModifiers.Shift;
            Keys key = keyData & Keys.KeyCode;
            return new HotkeySpec(mods, key);
        }

        public static HotkeySpec Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;
            HotkeyModifiers mods = HotkeyModifiers.None;
            Keys key = Keys.None;
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length == 0) continue;
                string upper = token.ToUpperInvariant();
                switch (upper)
                {
                    case "CTRL":
                    case "CONTROL":
                        mods |= HotkeyModifiers.Control;
                        continue;
                    case "ALT":
                        mods |= HotkeyModifiers.Alt;
                        continue;
                    case "SHIFT":
                        mods |= HotkeyModifiers.Shift;
                        continue;
                    case "WIN":
                    case "WINDOWS":
                    case "META":
                    case "CMD":
                    case "COMMAND":
                        mods |= HotkeyModifiers.Win;
                        continue;
                }
                Keys parsed;
                if (TryParseKey(token, out parsed))
                {
                    key = parsed;
                }
                else
                {
                    return null;
                }
            }
            HotkeySpec spec = new HotkeySpec(mods, key);
            return spec.IsValid ? spec : null;
        }

        private static bool TryParseKey(string token, out Keys key)
        {
            EnsureKeyMap();
            foreach (KeyValuePair<string, Keys> pair in _byName)
            {
                if (string.Equals(pair.Key, token, StringComparison.OrdinalIgnoreCase))
                {
                    key = pair.Value;
                    return true;
                }
            }
            // Raw enum name ("LWin", "OemTilde", ...).
            foreach (string name in Enum.GetNames(typeof(Keys)))
            {
                if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
                {
                    Keys candidate = (Keys)Enum.Parse(typeof(Keys), name, true);
                    if (candidate != Keys.None && !IsModifierKey(candidate))
                    {
                        key = candidate;
                        return true;
                    }
                }
            }
            key = Keys.None;
            return false;
        }

        public static bool IsModifierKey(Keys key)
        {
            switch (key & Keys.KeyCode)
            {
                case Keys.ControlKey:
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.ShiftKey:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.Menu:
                case Keys.LMenu:
                case Keys.RMenu:
                case Keys.LWin:
                case Keys.RWin:
                case Keys.None:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Stable, culture-free name used for serialization and parsing.</summary>
        public static string KeyName(Keys key)
        {
            key = key & Keys.KeyCode;
            int code = (int)key;
            if (code >= (int)Keys.A && code <= (int)Keys.Z) return ((char)code).ToString();
            if (code >= (int)Keys.D0 && code <= (int)Keys.D9) return ((char)('0' + (code - (int)Keys.D0))).ToString();
            if (code >= (int)Keys.NumPad0 && code <= (int)Keys.NumPad9)
            {
                return "Num" + (code - (int)Keys.NumPad0).ToString(CultureInfo.InvariantCulture);
            }
            if (code >= (int)Keys.F1 && code <= (int)Keys.F24) return "F" + (code - (int)Keys.F1 + 1).ToString(CultureInfo.InvariantCulture);
            switch (key)
            {
                case Keys.Space:
                    return "Space";
                case Keys.Enter:
                    return "Enter";
                case Keys.Tab:
                    return "Tab";
                case Keys.Escape:
                    return "Esc";
                case Keys.Back:
                    return "Backspace";
                case Keys.Delete:
                    return "Delete";
                case Keys.Insert:
                    return "Insert";
                case Keys.Home:
                    return "Home";
                case Keys.End:
                    return "End";
                case Keys.PageUp:
                    return "PageUp";
                case Keys.PageDown:
                    return "PageDown";
                case Keys.Up:
                    return "Up";
                case Keys.Down:
                    return "Down";
                case Keys.Left:
                    return "Left";
                case Keys.Right:
                    return "Right";
                case Keys.Oemtilde:
                    return "`";
                case Keys.OemMinus:
                    return "-";
                case Keys.Oemplus:
                    return "=";
                case Keys.OemOpenBrackets:
                    return "[";
                case Keys.OemCloseBrackets:
                    return "]";
                case Keys.OemPipe:
                    return "\\";
                case Keys.OemSemicolon:
                    return ";";
                case Keys.OemQuotes:
                    return "'";
                case Keys.Oemcomma:
                    return ",";
                case Keys.OemPeriod:
                    return ".";
                case Keys.OemQuestion:
                    return "/";
                case Keys.CapsLock:
                    return "CapsLock";
                case Keys.NumLock:
                    return "NumLock";
                case Keys.Scroll:
                    return "ScrollLock";
                case Keys.PrintScreen:
                    return "PrintScreen";
                case Keys.Pause:
                    return "Pause";
                default:
                    return key.ToString();
            }
        }

        /// <summary>Short label shown in the UI (arrow glyphs, "Esc", ...).</summary>
        public static string KeyGlyph(Keys key)
        {
            switch (key & Keys.KeyCode)
            {
                case Keys.Up:
                    return "↑";
                case Keys.Down:
                    return "↓";
                case Keys.Left:
                    return "←";
                case Keys.Right:
                    return "→";
                case Keys.Enter:
                    return "Enter";
                case Keys.Back:
                    return "⌫";
                case Keys.Delete:
                    return "Del";
                case Keys.Escape:
                    return "Esc";
                default:
                    return KeyName(key);
            }
        }

        private static void EnsureKeyMap()
        {
            if (_byName != null) return;
            Dictionary<string, Keys> map = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 256; i++)
            {
                Keys candidate = (Keys)i;
                if (candidate == Keys.None || IsModifierKey(candidate)) continue;
                // Skip values whose KeyCode differs (combined flags).
                if ((int)(candidate & Keys.KeyCode) != i) continue;
                string name = KeyName(candidate);
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name)) map.Add(name, candidate);
                string glyph = KeyGlyph(candidate);
                if (!string.IsNullOrEmpty(glyph) && !map.ContainsKey(glyph)) map.Add(glyph, candidate);
            }
            _byName = map;
        }
    }
}
