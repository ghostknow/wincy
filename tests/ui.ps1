# Shared UI automation helpers for the verification scripts (Windows PowerShell 5.1).
#
#   . .\tests\ui.ps1
#   $hwnd = Get-UiWindow -Title "Wincy"
#   Invoke-UiRightClick -X 500 -Y 400
#   Save-UiScreenshot -Title "Wincy" -Path shots\popup.png
#
# Note: FindWindow/FindWindowEx are unreliable in this environment, so every lookup
# goes through EnumWindows.

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ('Ui' -as [type])) {
    Add-Type -ReferencedAssemblies @('System.Drawing') @'
using System;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class Ui {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    public delegate bool EnumProc(IntPtr hwnd, IntPtr param);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool on);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);

    public static void BeDpiAware() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } }

    public static IntPtr Find(string title, int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            if (!IsWindowVisible(h)) return true;
            uint owner; GetWindowThreadProcessId(h, out owner);
            if (pid != 0 && owner != (uint)pid) return true;
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, 512);
            if (!string.IsNullOrEmpty(title) && !string.Equals(sb.ToString(), title, StringComparison.Ordinal)) return true;
            found = h; return false;
        }, IntPtr.Zero);
        return found;
    }

    public static string TitleOf(IntPtr h) {
        StringBuilder sb = new StringBuilder(512);
        GetWindowText(h, sb, 512);
        return sb.ToString();
    }

    public static Bitmap Render(IntPtr h) {
        RECT r; GetWindowRect(h, out r);
        int w = Math.Max(1, r.Right - r.Left), hh = Math.Max(1, r.Bottom - r.Top);
        Bitmap bmp = new Bitmap(w, hh, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
            IntPtr dc = g.GetHdc();
            bool ok = PrintWindow(h, dc, 2);
            g.ReleaseHdc(dc);
            if (!ok) g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, hh));
        }
        return bmp;
    }

    public static bool Focus(IntPtr hwnd) {
        if (GetForegroundWindow() == hwnd) return true;
        uint ignored;
        uint fg = GetWindowThreadProcessId(GetForegroundWindow(), out ignored);
        uint target = GetWindowThreadProcessId(hwnd, out ignored);
        uint me = GetCurrentThreadId();
        bool a = fg != 0 && fg != me && AttachThreadInput(me, fg, true);
        bool b = target != 0 && target != me && target != fg && AttachThreadInput(me, target, true);
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
        if (b) AttachThreadInput(me, target, false);
        if (a) AttachThreadInput(me, fg, false);
        return GetForegroundWindow() == hwnd;
    }
}
'@
}

[Ui]::BeDpiAware()

function Get-UiWindow {
    param([string]$Title = "", [int]$OwnerPid = 0)
    return [Ui]::Find($Title, $OwnerPid)
}

function Wait-UiWindow {
    param([string]$Title = "", [int]$OwnerPid = 0, [int]$TimeoutMs = 4000)
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    do {
        $hwnd = [Ui]::Find($Title, $OwnerPid)
        if ($hwnd -ne [IntPtr]::Zero) { return $hwnd }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    return [IntPtr]::Zero
}

function Get-UiWindowRect {
    param([IntPtr]$Handle)
    $rect = New-Object Ui+RECT
    [void][Ui]::GetWindowRect($Handle, [ref]$rect)
    return $rect
}

function Save-UiFullScreen {
    param([Parameter(Mandatory = $true)][string]$Path)
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen(0, 0, 0, 0, $bitmap.Size)
    $graphics.Dispose()
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $size = "$($bitmap.Width)x$($bitmap.Height)"
    $bitmap.Dispose()
    Write-Output "saved $Path ($size)"
}

function Save-UiScreenshot {
    param([string]$Title = "Wincy", [int]$OwnerPid = 0, [Parameter(Mandatory = $true)][string]$Path)
    $hwnd = [Ui]::Find($Title, $OwnerPid)
    if ($hwnd -eq [IntPtr]::Zero) { throw "Window not found: '$Title'" }
    $bitmap = [Ui]::Render($hwnd)
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $size = "$($bitmap.Width)x$($bitmap.Height)"
    $bitmap.Dispose()
    Write-Output "saved $Path ($size)"
}

function Invoke-UiClickAt {
    param([int]$X, [int]$Y, [switch]$Right)
    [void][Ui]::SetCursorPos($X, $Y)
    Start-Sleep -Milliseconds 120
    if ($Right) {
        [Ui]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Ui]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
    }
    else {
        [Ui]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Ui]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    }
    Start-Sleep -Milliseconds 200
}

function Send-UiKeys {
    param([string]$Keys, [int]$DelayMs = 150)
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds $DelayMs
}
