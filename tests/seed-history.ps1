# Populate the clipboard with a variety of items so screenshots show a realistic
# history. Run with Wincy already running.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Windows.Forms

$root = Split-Path -Parent $PSScriptRoot
$clip = {
    param($text)
    [System.Windows.Forms.Clipboard]::SetText($text)
    Start-Sleep -Milliseconds 450
}

& $clip "重新設計剪貼簿歷史清單，讓搜尋、釘選與貼上更順手。"
& $clip "https://github.com/p0deje/Maccy"
& $clip "SELECT user_id, COUNT(*) AS total FROM events WHERE created_at > now() - interval '7 days' GROUP BY user_id ORDER BY total DESC LIMIT 20;"
& $clip "Meeting notes — Thursday 15:30`r`n- Review the clipboard history layout`r`n- Decide between fuzzy and prefix search`r`n- Ship the Windows build this week"

# A colourful image on the clipboard.
$bitmap = New-Object System.Drawing.Bitmap 960, 540
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$rect = New-Object System.Drawing.Rectangle 0, 0, 960, 540
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 76, 111, 255)), ([System.Drawing.Color]::FromArgb(255, 20, 24, 48)), 35
$g.FillRectangle($brush, $rect)
$font = New-Object System.Drawing.Font "Segoe UI", 42, ([System.Drawing.FontStyle]::Bold)
$g.DrawString("Wincy", $font, [System.Drawing.Brushes]::White, 70, 200)
$font2 = New-Object System.Drawing.Font "Segoe UI", 20
$g.DrawString("clipboard history for Windows", $font2, [System.Drawing.Brushes]::White, 74, 280)
$g.Dispose()
[System.Windows.Forms.Clipboard]::SetImage($bitmap)
Start-Sleep -Milliseconds 600
$bitmap.Dispose()

# Two files on the clipboard.
$tmp = Join-Path $env:ProgramData 'WincyDemo'
try { New-Item -ItemType Directory -Force -Path $tmp | Out-Null } catch { $tmp = Join-Path $env:TEMP 'WincyDemo'; New-Item -ItemType Directory -Force -Path $tmp | Out-Null }
$fileA = Join-Path $tmp 'release-notes.pdf'
$fileB = Join-Path $tmp 'wincy-preview.png'
[IO.File]::WriteAllText($fileA, "dummy pdf", [Text.Encoding]::UTF8)
[IO.File]::WriteAllText($fileB, "dummy png", [Text.Encoding]::UTF8)
$files = New-Object System.Collections.Specialized.StringCollection
$files.Add($fileA) | Out-Null
$files.Add($fileB) | Out-Null
[System.Windows.Forms.Clipboard]::SetFileDropList($files)
Start-Sleep -Milliseconds 600

# A short item that lands on top.
& $clip "Ctrl+Shift+V 開啟 Wincy"
Write-Output "seeded"
