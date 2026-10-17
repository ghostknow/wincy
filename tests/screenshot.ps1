# Capture a window (default: rendered via PrintWindow, so it works even if covered)
# or the whole screen.
#
# Usage:
#   .\screenshot.ps1 -Title "Wincy" -Path shots\popup.png
#   .\screenshot.ps1 -FullScreen -Path shots\screen.png
param(
    [string]$Title = "Wincy",
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$OwnerPid = 0,
    [switch]$FullScreen,
    [switch]$CopyFromScreen
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ('Shot' -as [type])) {
    Add-Type -ReferencedAssemblies @('System.Drawing') @'
using System;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class Shot {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    public delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    public static void BeDpiAware() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } }

    public static IntPtr Find(string title, int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hwnd, IntPtr param) {
            if (!IsWindowVisible(hwnd)) return true;
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, 512);
            if (!string.IsNullOrEmpty(title) && !string.Equals(sb.ToString(), title, StringComparison.Ordinal)) return true;
            if (pid != 0) {
                uint owner;
                GetWindowThreadProcessId(hwnd, out owner);
                if (owner != (uint)pid) return true;
            }
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static Bitmap Render(IntPtr hwnd) {
        RECT rect;
        GetWindowRect(hwnd, out rect);
        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);
        Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap)) {
            IntPtr hdc = g.GetHdc();
            bool ok = PrintWindow(hwnd, hdc, 2);
            g.ReleaseHdc(hdc);
            if (!ok) {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            }
        }
        return bitmap;
    }
}
'@
}

[Shot]::BeDpiAware()

$bitmap = $null
if ($FullScreen) {
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen(0, 0, 0, 0, $bitmap.Size)
    $graphics.Dispose()
}
else {
    $hwnd = [Shot]::Find($Title, $OwnerPid)
    if ($hwnd -eq [IntPtr]::Zero) { throw "Window not found: $Title" }
    if ($CopyFromScreen) {
        $rect = New-Object Shot+RECT
        [void][Shot]::GetWindowRect($hwnd, [ref]$rect)
        $bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        $graphics.Dispose()
    }
    else {
        $bitmap = [Shot]::Render($hwnd)
    }
}

$dir = Split-Path -Parent $Path
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
$size = "$($bitmap.Width)x$($bitmap.Height)"
$bitmap.Dispose()
Write-Output "saved $Path ($size)"
