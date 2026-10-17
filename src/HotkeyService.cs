using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Wincy
{
    /// <summary>Owns the single global shortcut registration.</summary>
    internal sealed class HotkeyService : IDisposable
    {
        public const int HotkeyId = 0x4D43; // 'MC'

        private readonly MessageWindow _window;
        private HotkeySpec _current;
        private bool _registered;
        private bool _suspended;

        public event EventHandler Pressed;

        public HotkeyService(MessageWindow window)
        {
            _window = window;
            _window.HotkeyPressed += OnWindowHotkey;
        }

        public HotkeySpec Current
        {
            get { return _current; }
        }

        public bool IsRegistered
        {
            get { return _registered && !_suspended; }
        }

        public bool TryRegister(HotkeySpec spec, out string error)
        {
            error = null;
            Unregister();
            if (spec == null || !spec.IsValid)
            {
                error = "invalid shortcut";
                return false;
            }
            if (!Native.RegisterHotKey(_window.Hwnd, HotkeyId, spec.NativeModifiers | Native.MOD_NOREPEAT, (uint)spec.Key))
            {
                int code = Marshal.GetLastWin32Error();
                error = new Win32Exception(code).Message;
                Log.Warn("RegisterHotKey failed for " + spec.Display + ": " + error);
                return false;
            }
            _current = spec.Clone();
            _registered = true;
            _suspended = false;
            Log.Info("Registered hotkey " + spec.Display);
            return true;
        }

        public void Unregister()
        {
            if (!_registered) return;
            try
            {
                Native.UnregisterHotKey(_window.Hwnd, HotkeyId);
            }
            catch (Exception ex)
            {
                Log.Warn("UnregisterHotKey failed: " + ex.Message);
            }
            _registered = false;
        }

        /// <summary>Temporarily release the shortcut so the settings recorder can capture it.</summary>
        public void Suspend()
        {
            if (!_registered) return;
            Native.UnregisterHotKey(_window.Hwnd, HotkeyId);
            _suspended = true;
        }

        public bool Resume()
        {
            if (!_suspended || _current == null) return false;
            bool ok = Native.RegisterHotKey(_window.Hwnd, HotkeyId,
                _current.NativeModifiers | Native.MOD_NOREPEAT, (uint)_current.Key);
            if (ok) _suspended = false;
            return ok;
        }

        private void OnWindowHotkey(object sender, EventArgs e)
        {
            if (_suspended) return;
            EventHandler handler = Pressed;
            if (handler == null) return;
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error("Hotkey handler failed", ex);
            }
        }

        public void Dispose()
        {
            _window.HotkeyPressed -= OnWindowHotkey;
            Unregister();
        }
    }

    /// <summary>The HKCU\...\Run entry used for "launch at login".</summary>
    internal static class AutoStart
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Wincy";

        public static string CommandLine
        {
            get { return "\"" + Paths.ExecutablePath + "\""; }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(ValueName) as string;
                    if (string.IsNullOrEmpty(value)) return false;
                    return string.Equals(value.Trim(), CommandLine, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read autostart entry: " + ex.Message);
                return false;
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null) return false;
                    if (enabled) key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
                    else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not update autostart entry", ex);
                return false;
            }
        }

        public static bool Sync(bool enabled)
        {
            if (IsEnabled() == enabled) return true;
            return SetEnabled(enabled);
        }


    }
}
