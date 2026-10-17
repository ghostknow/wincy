using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>Sends Ctrl+V to whichever window had focus before Wincy opened.</summary>
    internal static class Paster
    {
        public static bool SendPaste(IntPtr target)
        {
            if (target == IntPtr.Zero || !Native.IsWindow(target))
            {
                Log.Warn("No paste target window available");
                return false;
            }

            IntPtr before = Native.GetForegroundWindow();
            ForceForeground(target);
            Thread.Sleep(45);
            Log.Info("Paste: before=" + before + " target=" + target + " after=" + Native.GetForegroundWindow());

            // A modifier may still be held from the hotkey (Ctrl+Shift+V would become
            // Ctrl+Shift+V again), so drop the non-Ctrl modifiers first.
            SendVirtualKey(Native.VK_SHIFT, true);
            SendVirtualKey(Native.VK_LSHIFT, true);
            SendVirtualKey(Native.VK_RSHIFT, true);
            SendVirtualKey(Native.VK_MENU, true);
            SendVirtualKey(Native.VK_LMENU, true);
            SendVirtualKey(Native.VK_RMENU, true);
            SendVirtualKey(Native.VK_LWIN, true);
            SendVirtualKey(Native.VK_RWIN, true);

            SendVirtualKey(Native.VK_CONTROL, false);
            Thread.Sleep(8);
            SendVirtualKey(Native.VK_V, false);
            Thread.Sleep(12);
            SendVirtualKey(Native.VK_V, true);
            SendVirtualKey(Native.VK_CONTROL, true);
            return true;
        }

        public static bool FocusWindow(IntPtr target)
        {
            if (target == IntPtr.Zero || !Native.IsWindow(target)) return false;
            ForceForeground(target);
            return Native.GetForegroundWindow() == target;
        }

        private static void ForceForeground(IntPtr target)
        {
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == target)
            {
                Native.BringWindowToTop(target);
                return;
            }

            uint ignored;
            uint foregroundThread = Native.GetWindowThreadProcessId(foreground, out ignored);
            uint targetThread = Native.GetWindowThreadProcessId(target, out ignored);
            uint currentThread = Native.GetCurrentThreadId();

            bool attachedForeground = false;
            bool attachedTarget = false;
            try
            {
                if (foregroundThread != 0 && foregroundThread != currentThread)
                {
                    attachedForeground = Native.AttachThreadInput(currentThread, foregroundThread, true);
                }
                if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
                {
                    attachedTarget = Native.AttachThreadInput(currentThread, targetThread, true);
                }
                Native.SetForegroundWindow(target);
                Native.BringWindowToTop(target);
            }
            catch (Exception ex)
            {
                Log.Warn("SetForegroundWindow failed: " + ex.Message);
            }
            finally
            {
                if (attachedTarget) Native.AttachThreadInput(currentThread, targetThread, false);
                if (attachedForeground) Native.AttachThreadInput(currentThread, foregroundThread, false);
            }

            if (Native.GetForegroundWindow() == target) return;

            // Last resort for the Windows foreground lock.
            SendVirtualKey(Native.VK_MENU, false);
            SendVirtualKey(Native.VK_MENU, true);
            Native.SetForegroundWindow(target);
        }

        private static void SendVirtualKey(ushort virtualKey, bool keyUp)
        {
            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_KEYBOARD;
            input.u.ki.wVk = virtualKey;
            input.u.ki.wScan = 0;
            input.u.ki.dwFlags = keyUp ? Native.KEYEVENTF_KEYUP : 0;
            input.u.ki.time = 0;
            input.u.ki.dwExtraInfo = IntPtr.Zero;
            Native.INPUT[] inputs = new Native.INPUT[] { input };
            Native.SendInput(1, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
            Thread.Sleep(6);
        }
    }

    /// <summary>
    /// Remembers the last window that was not Wincy, the tray or the desktop: that is
    /// the window a pasted item goes back to.
    /// </summary>
    internal sealed class ForegroundTracker : IDisposable
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly uint _ownProcessId;
        private IntPtr _last;

        public ForegroundTracker()
        {
            _ownProcessId = (uint)Process.GetCurrentProcess().Id;
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 300;
            _timer.Tick += OnTick;
            Refresh();
            _timer.Start();
        }

        public IntPtr LastExternal
        {
            get { return _last; }
        }

        public void Remember(IntPtr hwnd)
        {
            if (IsUsable(hwnd)) _last = hwnd;
        }

        public void Refresh()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (IsUsable(hwnd)) _last = hwnd;
        }

        private void OnTick(object sender, EventArgs e)
        {
            Refresh();
        }

        private bool IsUsable(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return false;
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0 || pid == _ownProcessId) return false;
            string cls = Native.ClassNameOf(hwnd);
            if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls == "NotifyIconOverflowWindow" ||
                cls == "Progman" || cls == "WorkerW" || cls == "Windows.UI.Core.CoreWindow") return false;
            if (hwnd == Native.GetShellWindow() || hwnd == Native.GetDesktopWindow()) return false;
            return true;
        }

        public void Dispose()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer.Dispose();
        }
    }
}
