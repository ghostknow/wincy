# End-to-end paste test: copy text, open Wincy's popup, press Enter, then assert that
# the text landed in a throwaway WinForms editor (tests\_pastetarget.exe).
#
# Usage: .\paste-test.ps1 [-Plain]
param(
    [switch]$Plain,
    [switch]$Rich,
    [switch]$NoPaste,
    [switch]$CheckFormats
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

if (-not ('E2E' -as [type])) {
    Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class E2E {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    public delegate bool EnumProc(IntPtr hwnd, IntPtr p);
    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint attach, uint attachTo, bool on);
    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hwnd);
    public static bool FocusWindow(IntPtr hwnd) {
        if (GetForegroundWindow() == hwnd) return true;
        uint ignored;
        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out ignored);
        uint targetThread = GetWindowThreadProcessId(hwnd, out ignored);
        uint me = GetCurrentThreadId();
        bool a = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        bool b = targetThread != 0 && targetThread != me && targetThread != fgThread && AttachThreadInput(me, targetThread, true);
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
        if (b) AttachThreadInput(me, targetThread, false);
        if (a) AttachThreadInput(me, fgThread, false);
        return GetForegroundWindow() == hwnd;
    }
    public static void BeDpiAware() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } }
    public static IntPtr FindByTitle(string title) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            if (!IsWindowVisible(h)) return true;
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, 512);
            if (!string.Equals(sb.ToString(), title, StringComparison.Ordinal)) return true;
            found = h; return false;
        }, IntPtr.Zero);
        return found;
    }
    public static IntPtr FindByPid(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            if (!IsWindowVisible(h)) return true;
            uint owner; GetWindowThreadProcessId(h, out owner);
            if (owner != (uint)pid) return true;
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, 512);
            if (sb.Length == 0) return true;
            found = h; return false;
        }, IntPtr.Zero);
        return found;
    }
}
'@
}

[E2E]::BeDpiAware()

$root = Split-Path -Parent $PSScriptRoot

# Make sure Wincy is running and start from an empty history so the freshly copied
# text is guaranteed to be the item the test selects (pinned items would stay on top).
if (-not (Get-Process Wincy -ErrorAction SilentlyContinue)) {
    Start-Process (Join-Path $root 'dist\Wincy.exe') | Out-Null
    Start-Sleep -Seconds 2
}
Start-Process (Join-Path $root 'dist\Wincy.exe') -ArgumentList '--clear' | Out-Null
Start-Sleep -Milliseconds 700

$source = Join-Path $PSScriptRoot '_paste.txt'
$output = Join-Path $PSScriptRoot '_pastetarget.out'
$expected = [IO.File]::ReadAllText($source, [Text.Encoding]::UTF8).TrimEnd("`r", "`n")

Get-Process _pastetarget -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 250
$target = Start-Process (Join-Path $PSScriptRoot '_pastetarget.exe') -ArgumentList "`"$output`"" -PassThru
Start-Sleep -Milliseconds 1000
$hwnd = [E2E]::FindByPid($target.Id)
if ($hwnd -eq [IntPtr]::Zero) { throw "Could not find the paste target window" }
if (-not [E2E]::FocusWindow($hwnd)) { throw "Could not focus the paste target window" }
Start-Sleep -Milliseconds 400

if ($Rich) {
    $html = "<html><body><!--StartFragment--><b style=""color:red"">PASTE-TEST-42</b> rich fragment<!--EndFragment--></body></html>"
    $fragmentStart = $html.IndexOf("PASTE-TEST-42")
    $fragmentEnd = $html.IndexOf(" rich fragment")
    $header = "Version:0.9`r`nStartHTML:0000000105`r`nEndHTML:0000000300`r`nStartFragment:0000000141`r`nEndFragment:0000000270`r`n"
    $payload = $header + $html
    $data = New-Object System.Windows.Forms.DataObject
    $data.SetData([System.Windows.Forms.DataFormats]::UnicodeText, $expected)
    $data.SetData([System.Windows.Forms.DataFormats]::Html, $payload)
    [System.Windows.Forms.Clipboard]::SetDataObject($data, $true)
}
else {
    [System.Windows.Forms.Clipboard]::SetText($expected)
}
Start-Sleep -Milliseconds 600

Start-Process (Join-Path $root 'dist\Wincy.exe') -ArgumentList '--show' | Out-Null
Start-Sleep -Milliseconds 900
$popup = [E2E]::FindByTitle('Wincy')
if ($popup -eq [IntPtr]::Zero) { throw "Wincy popup did not open" }
[void][E2E]::FocusWindow($popup)
Start-Sleep -Milliseconds 350

if (-not $NoPaste) {
    # The popup deliberately focuses nothing on open, so move the selection first.
[System.Windows.Forms.SendKeys]::SendWait("{DOWN}")
Start-Sleep -Milliseconds 250
if ($Plain) { [System.Windows.Forms.SendKeys]::SendWait("+{ENTER}") }
    else { [System.Windows.Forms.SendKeys]::SendWait("{ENTER}") }
    Start-Sleep -Milliseconds 1200
}

if ($NoPaste) {
    $formats = ([System.Windows.Forms.Clipboard]::GetDataObject()).GetFormats()
    Write-Output ("clipboard formats: " + ($formats -join ', '))
}
$actual = ''
if (Test-Path $output) { $actual = ([IO.File]::ReadAllText($output, [Text.Encoding]::UTF8)).TrimEnd("`r", "`n") }
if ($CheckFormats) {
    $formats = ([System.Windows.Forms.Clipboard]::GetDataObject()).GetFormats()
    $hasHtml = $formats -contains 'HTML Format'
    Write-Output ("clipboard formats: " + ($formats -join ', '))
    Write-Output ("HTML present: " + $hasHtml)
}
Write-Output "foreground now: $([E2E]::GetForegroundWindow()) target: $hwnd"
Write-Output "expected: [$expected]"
Write-Output "actual  : [$actual]"
if ($actual.Trim() -eq $expected.Trim()) { Write-Output 'PASTE OK' } else { Write-Output 'PASTE MISMATCH' }

Get-Process _pastetarget -ErrorAction SilentlyContinue | Stop-Process -Force
