using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>
    /// Application controller: owns the tray icon, clipboard listener, hotkey, history
    /// store and the windows, and wires them together.
    /// </summary>
    internal sealed class WincyApp : ApplicationContext, IDisposable
    {
        public AppSettings Settings;
        public HistoryStore Store;
        public Palette Palette;
        public MainForm Popup;
        public SettingsForm SettingsForm;
        public InfoForm InfoForm;
        public Icon AppIcon;

        private readonly MessageWindow _messages;
        private readonly HotkeyService _hotkeys;
        private readonly ForegroundTracker _foreground;
        private readonly Timer _retryTimer;
        private readonly Timer _pasteTimer;
        private readonly Timer _themeTimer;
        private readonly Timer _debounceTimer;
        private NotifyIcon _tray;
        private int _retryAttempts;
        private uint _lastSequence;
        private IntPtr _pasteTarget;
        private bool _quitting;
        private bool _disposed;

        public WincyApp()
        {
            Paths.EnsureDirectories();
            Settings = AppSettings.Load();
            I18n.SetLanguage(Settings.Language);
            Fonts.Initialize();
            Palette = Palette.Resolve(Settings.Theme);

            Store = new HistoryStore(Settings.HistoryLimit);
            Store.Load();

            AppIcon = LoadIcon();

            _messages = new MessageWindow();
            _messages.ClipboardChanged += OnClipboardChanged;
            _messages.ShowRequested += OnShowRequested;
            _messages.SystemSettingsChanged += OnSystemSettingsChanged;
            _messages.CommandRequested += OnCommandRequested;

            _hotkeys = new HotkeyService(_messages);
            _hotkeys.Pressed += OnHotkeyPressed;

            _foreground = new ForegroundTracker();
            Popup = new MainForm(this);

            CreateTray();
            RegisterHotkeyWithFallback();
            AutoStart.Sync(Settings.LaunchAtLogin);

            _retryTimer = new Timer();
            _retryTimer.Interval = 45;
            _retryTimer.Tick += OnRetryTick;

            _pasteTimer = new Timer();
            _pasteTimer.Interval = 90;
            _pasteTimer.Tick += OnPasteTick;

            _themeTimer = new Timer();
            _themeTimer.Interval = 250;
            _themeTimer.Tick += OnThemeTick;

            _debounceTimer = new Timer();
            _debounceTimer.Interval = 70;
            _debounceTimer.Tick += OnDebounceTick;

            if (Settings.ShowWelcome)
            {
                Settings.ShowWelcome = false;
                SaveSettings();
                ShowBalloon(I18n.T("welcome.title"),
                    I18n.T("welcome.body", Settings.Hotkey == null ? "—" : Settings.Hotkey.Display));
            }

            Log.Info("Wincy started; history " + Store.Describe());
        }

        // ------------------------------------------------------------------ accessors

        private void CreateTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = AppIcon ?? SystemIcons.Application;
            _tray.Visible = true;
            _tray.MouseUp += OnTrayMouseUp;
            _tray.DoubleClick += OnTrayDoubleClick;
            _tray.BalloonTipClicked += delegate { ShowPopup(true); };
            UpdateTrayText();
        }

        private static Icon LoadIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Paths.ExecutablePath);
                if (icon != null) return icon;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not load the application icon: " + ex.Message);
            }
            return SystemIcons.Application;
        }

        private void UpdateTrayText()
        {
            if (_tray == null) return;
            string hotkey = Settings.Hotkey == null ? string.Empty : Settings.Hotkey.Display;
            string text = I18n.T("tray.tip", hotkey);
            if (text.Length > 63) text = text.Substring(0, 60) + "…";
            _tray.Text = text;
        }

        // ------------------------------------------------------------------ popup

        private void OnHotkeyPressed(object sender, EventArgs e)
        {
            TogglePopup(false);
        }

        private void OnShowRequested(object sender, EventArgs e)
        {
            ShowPopup(false);
        }

        private void OnCommandRequested(object sender, CommandEventArgs e)
        {
            HandleCommand(e.Command);
        }

        public void HandleCommand(int command)
        {
            switch (command)
            {
                case AppCommand.Show:
                    ShowPopup(false);
                    break;
                case AppCommand.Toggle:
                    TogglePopup(false);
                    break;
                case AppCommand.Settings:
                    OpenSettings();
                    break;
                case AppCommand.About:
                    OpenAbout();
                    break;
                case AppCommand.Clear:
                    Store.Clear();
                    break;
                case AppCommand.Quit:
                    Quit();
                    break;
            }
        }

        private void OnTrayMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) TogglePopup(true);
            else if (e.Button == MouseButtons.Right) ShowTrayMenu();
        }

        private void OnTrayDoubleClick(object sender, EventArgs e)
        {
            ShowPopup(true);
        }

        public void TogglePopup(bool fromTray)
        {
            if (Popup.Visible)
            {
                Popup.HidePopup();
                return;
            }
            ShowPopup(fromTray);
        }

        public void ShowPopup(bool fromTray)
        {
            if (_quitting) return;
            _foreground.Refresh();
            Popup.ShowPopup(Cursor.Position, fromTray);
        }

        private void ShowTrayMenu()
        {
            Point point = Cursor.Position;
            MiniMenu menu = new MiniMenu(Palette);
            menu.Add(I18n.T("tray.show"), Settings.Hotkey == null ? null : Settings.Hotkey.Display,
                delegate { ShowPopup(true); });
            menu.AddSeparator();
            menu.AddCheck(I18n.T("tray.launch"), AutoStart.IsEnabled(), delegate
            {
                bool next = !AutoStart.IsEnabled();
                Settings.LaunchAtLogin = next;
                SaveSettings();
                ApplyAutoStart();
                if (SettingsForm != null && !SettingsForm.IsDisposed) SettingsForm.ReloadValues();
            });
            menu.Add(I18n.T("tray.settings"), "Ctrl+,", OpenSettings);
            menu.AddSeparator();
            menu.AddDanger(I18n.T("tray.clear"), null, delegate { ConfirmClearHistory(point); });
            menu.Add(I18n.T("tray.about"), null, OpenAbout);
            menu.AddSeparator();
            menu.Add(I18n.T("tray.quit"), null, Quit);
            menu.ShowAt(point);
        }

        private void ConfirmClearHistory(Point anchor)
        {
            MiniMenu menu = new MiniMenu(Palette);
            menu.AddHeader(I18n.T("item.clearConfirm", Store.Count));
            menu.AddDanger(I18n.T("item.clearAll"), null, delegate
            {
                Store.Clear();
                ShowBalloon(I18n.T("app.name"), I18n.T("msg.cleared"));
            });
            menu.Add(I18n.T("item.cancel"), null, delegate { });
            menu.ShowAt(anchor);
        }

        // ------------------------------------------------------------------ history

        private void OnClipboardChanged(object sender, EventArgs e)
        {
            uint sequence = Native.GetClipboardSequenceNumber();
            if (sequence == _lastSequence) return;
            _lastSequence = sequence;
            _retryAttempts = 0;
            // One copy can raise several notifications (empty + set); settle first.
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void OnDebounceTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            TryCapture();
        }

        private void OnRetryTick(object sender, EventArgs e)
        {
            _retryTimer.Stop();
            TryCapture();
        }

        private void TryCapture()
        {
            ClipboardItem item = null;
            string reason = null;
            CaptureStatus status;
            try
            {
                status = ClipboardReader.Read(Settings, out item, out reason);
            }
            catch (Exception ex)
            {
                Log.Error("Clipboard read threw", ex);
                status = CaptureStatus.Retry;
                reason = ex.Message;
            }

            if (status == CaptureStatus.Retry)
            {
                if (_retryAttempts++ < 10)
                {
                    _retryTimer.Start();
                }
                else
                {
                    Log.Warn("Gave up reading the clipboard: " + reason);
                }
                return;
            }
            if (status == CaptureStatus.Skip || item == null)
            {
                Log.Info("Clipboard skipped: " + reason);
                return;
            }

            ClipboardItem stored = Store.Add(item);
            if (stored != null && stored.Id != item.Id && item.Kind == ItemKind.Image)
            {
                // Deduplicated: the fresh blob we just wrote is not referenced any more.
                string blob = Paths.ImageFullPathFor(item);
                try
                {
                    if (blob != null && File.Exists(blob)) File.Delete(blob);
                }
                catch
                {
                }
            }
            Log.Info("Captured " + item.Kind + " from " + (item.SourceApp ?? "unknown"));
        }

        public void UseItem(ClipboardItem item, bool plainRequested, bool copyOnly)
        {
            if (item == null) return;
            bool plain = plainRequested || Settings.PastePlainByDefault;
            IntPtr target = _foreground.LastExternal;

            Log.Info("UseItem kind=" + item.Kind + " plain=" + plain + " copyOnly=" + copyOnly +
                     " autoPaste=" + Settings.AutoPaste + " target=" + target);

            Popup.HideForAction();
            if (!ClipboardWriter.Put(item, plain))
            {
                ShowBalloon(I18n.T("app.name"), "Could not write to the clipboard");
                return;
            }
            Store.MoveToTop(item.Id);

            if (copyOnly || !Settings.AutoPaste) return;
            if (target == IntPtr.Zero)
            {
                Log.Warn("No target window to paste into");
                return;
            }
            _pasteTarget = target;
            _pasteTimer.Start();
        }

        private void OnPasteTick(object sender, EventArgs e)
        {
            _pasteTimer.Stop();
            IntPtr target = _pasteTarget;
            _pasteTarget = IntPtr.Zero;
            Paster.SendPaste(target);
        }

        // ------------------------------------------------------------------ settings

        public void SaveSettings()
        {
            Settings.Save();
        }

        public bool TryChangeHotkey(HotkeySpec spec, out string error)
        {
            error = null;
            HotkeySpec previous = Settings.Hotkey;
            if (!_hotkeys.TryRegister(spec, out error))
            {
                string ignored;
                if (previous != null) _hotkeys.TryRegister(previous, out ignored);
                return false;
            }
            Settings.Hotkey = spec;
            SaveSettings();
            UpdateTrayText();
            return true;
        }

        public void SuspendHotkey()
        {
            _hotkeys.Suspend();
        }

        public void ResumeHotkey()
        {
            if (!_hotkeys.Resume())
            {
                string error;
                if (Settings.Hotkey != null && !_hotkeys.TryRegister(Settings.Hotkey, out error))
                {
                    Log.Warn("Could not restore the hotkey: " + error);
                }
            }
        }

        private void RegisterHotkeyWithFallback()
        {
            string error;
            if (_hotkeys.TryRegister(Settings.Hotkey, out error)) return;

            Log.Warn("Primary hotkey unavailable: " + error);
            string previousDisplay = Settings.Hotkey == null ? "—" : Settings.Hotkey.Display;
            HotkeySpec fallback = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.V);
            string fallbackError;
            if (_hotkeys.TryRegister(fallback, out fallbackError))
            {
                Settings.Hotkey = fallback;
                SaveSettings();
                UpdateTrayText();
                ShowBalloon(I18n.T("app.name"),
                    I18n.T("msg.hotkeyFallback", previousDisplay, fallback.Display));
            }
            else
            {
                ShowBalloon(I18n.T("app.name"), I18n.T("msg.hotkeyFailed", previousDisplay));
            }
        }

        public void ApplyAutoStart()
        {
            AutoStart.SetEnabled(Settings.LaunchAtLogin);
        }

        public void RefreshTheme()
        {
            Palette next = Palette.Resolve(Settings.Theme);
            if (next.Dark == Palette.Dark) return;
            Palette = next;
            Popup.ApplyPalette(next);
            if (SettingsForm != null && !SettingsForm.IsDisposed) SettingsForm.RefreshPalette();
            if (InfoForm != null && !InfoForm.IsDisposed) InfoForm.Close();
        }

        private void OnSystemSettingsChanged(object sender, EventArgs e)
        {
            if (Settings.Theme != "system") return;
            _themeTimer.Stop();
            _themeTimer.Start();
        }

        private void OnThemeTick(object sender, EventArgs e)
        {
            _themeTimer.Stop();
            RefreshTheme();
        }

        public void OpenSettings()
        {
            Popup.HideForAction();
            if (SettingsForm != null && !SettingsForm.IsDisposed)
            {
                SettingsForm.Activate();
                return;
            }
            SettingsForm = new SettingsForm(this);
            SettingsForm.FormClosed += delegate { SettingsForm = null; };
            SettingsForm.Show();
            SettingsForm.Activate();
        }

        public void OpenAbout()
        {
            if (InfoForm != null && !InfoForm.IsDisposed)
            {
                InfoForm.Activate();
                return;
            }
            InfoForm = new InfoForm(this);
            InfoForm.FormClosed += delegate { InfoForm = null; };
            InfoForm.Show();
            InfoForm.Activate();
        }

        public void ShowBalloon(string title, string text)
        {
            if (_tray == null) return;
            try
            {
                _tray.ShowBalloonTip(5000, title, text, ToolTipIcon.None);
            }
            catch (Exception ex)
            {
                Log.Warn("Balloon tip failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ shutdown

        public void Quit()
        {
            if (_quitting) return;
            _quitting = true;
            try
            {
                Store.Save();
                Settings.Save();
            }
            catch (Exception ex)
            {
                Log.Error("Could not save state on quit", ex);
            }
            Dispose();
            ExitThread();
        }

        public new void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Log.Info("Wincy stopping");

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.MouseUp -= OnTrayMouseUp;
                _tray.DoubleClick -= OnTrayDoubleClick;
                _tray.Dispose();
                _tray = null;
            }

            if (_hotkeys != null) _hotkeys.Dispose();
            if (_messages != null) _messages.Dispose();
            if (_foreground != null) _foreground.Dispose();
            if (_retryTimer != null) _retryTimer.Dispose();
            if (_pasteTimer != null) _pasteTimer.Dispose();
            if (_themeTimer != null) _themeTimer.Dispose();
            if (_debounceTimer != null) _debounceTimer.Dispose();
            if (SettingsForm != null && !SettingsForm.IsDisposed) SettingsForm.Dispose();
            if (InfoForm != null && !InfoForm.IsDisposed) InfoForm.Dispose();
            if (Popup != null && !Popup.IsDisposed) Popup.Dispose();
            Thumbnails.Clear();
            base.Dispose();
        }
    }
}
