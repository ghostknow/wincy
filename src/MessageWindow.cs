using System;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>Commands a second launch (or a script) can send to the running instance.</summary>
    internal static class AppCommand
    {
        public const int Show = 1;
        public const int Settings = 2;
        public const int About = 3;
        public const int Clear = 4;
        public const int Quit = 5;
        public const int Toggle = 6;
    }

    internal sealed class CommandEventArgs : EventArgs
    {
        public readonly int Command;

        public CommandEventArgs(int command)
        {
            Command = command;
        }
    }

    /// <summary>
    /// Invisible top-level window that owns the clipboard listener, the global hotkey
    /// and the "another instance started" broadcast. Top-level (not message-only) so it
    /// keeps receiving WM_SETTINGCHANGE broadcasts.
    /// </summary>
    internal sealed class MessageWindow : NativeWindow, IDisposable
    {
        public const string Caption = "Wincy.MessageWindow";
        public const string ShowMessageName = "Wincy.ShowHistory";
        public const string CommandMessageName = "Wincy.Command";

        public event EventHandler ClipboardChanged;
        public event EventHandler HotkeyPressed;
        public event EventHandler ShowRequested;
        public event EventHandler SystemSettingsChanged;
        public event EventHandler<CommandEventArgs> CommandRequested;

        private readonly int _showMessage;
        private readonly int _commandMessage;
        private bool _listening;
        private bool _disposed;

        public MessageWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = Caption;
            cp.X = 0;
            cp.Y = 0;
            cp.Width = 0;
            cp.Height = 0;
            cp.Style = 0;
            cp.ExStyle = 0;
            CreateHandle(cp);
            _showMessage = Native.RegisterWindowMessage(ShowMessageName);
            _commandMessage = Native.RegisterWindowMessage(CommandMessageName);
            _listening = Native.AddClipboardFormatListener(Handle);
            if (!_listening) Log.Warn("AddClipboardFormatListener failed");
        }

        public IntPtr Hwnd
        {
            get { return Handle; }
        }

        public static void BroadcastShow()
        {
            int message = Native.RegisterWindowMessage(ShowMessageName);
            if (message == 0) return;
            Native.PostMessage(new IntPtr(0xFFFF), message, IntPtr.Zero, IntPtr.Zero);
        }

        public static IntPtr FindExisting()
        {
            return Native.FindWindow(null, Caption);
        }

        /// <summary>Ask the running instance to perform a command (see <see cref="AppCommand"/>).</summary>
        public static void SendCommand(int command)
        {
            int message = Native.RegisterWindowMessage(CommandMessageName);
            if (message == 0) return;
            Native.PostMessage(new IntPtr(0xFFFF), message, new IntPtr(command), IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Native.WM_CLIPBOARDUPDATE:
                    Raise(ClipboardChanged);
                    break;
                case Native.WM_HOTKEY:
                    Raise(HotkeyPressed);
                    break;
                case Native.WM_SETTINGCHANGE:
                    Raise(SystemSettingsChanged);
                    break;
                default:
                    if (_showMessage != 0 && m.Msg == _showMessage) Raise(ShowRequested);
                    else if (_commandMessage != 0 && m.Msg == _commandMessage) RaiseCommand(m.WParam.ToInt32());
                    break;
            }
            base.WndProc(ref m);
        }

        private void RaiseCommand(int command)
        {
            EventHandler<CommandEventArgs> handler = CommandRequested;
            if (handler == null) return;
            try
            {
                handler(this, new CommandEventArgs(command));
            }
            catch (Exception ex)
            {
                Log.Error("Command handler failed", ex);
            }
        }

        private void Raise(EventHandler handler)
        {
            if (handler == null) return;
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error("Message window handler failed", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_listening) Native.RemoveClipboardFormatListener(Handle);
            }
            catch
            {
            }
            try
            {
                DestroyHandle();
            }
            catch
            {
            }
        }
    }
}
