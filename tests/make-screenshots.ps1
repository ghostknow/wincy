# Regenerate the documentation screenshots in docs\.
# Requires a running desktop session and a built dist\Wincy.exe.
#
# Usage: .\make-screenshots.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui.ps1')

$root = Split-Path -Parent $PSScriptRoot
$docs = Join-Path $root 'docs'
New-Item -ItemType Directory -Force -Path $docs | Out-Null
$dataDir = Join-Path $env:APPDATA 'Wincy'
$settingsFile = Join-Path $dataDir 'settings.json'
$wincyExe = Join-Path $root 'dist\Wincy.exe'

function Stop-Wincy {
    Get-Process Wincy -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

function Start-Wincy {
    Start-Process $wincyExe | Out-Null
    Start-Sleep -Seconds 2
}

function Send-WincyCommand {
    param([string]$Argument)
    Start-Process $wincyExe -ArgumentList $Argument | Out-Null
}

function Set-Theme {
    param([string]$Theme)
    Stop-Wincy
    if (Test-Path $settingsFile) {
        $raw = Get-Content $settingsFile -Raw -Encoding UTF8
        $raw = [regex]::Replace($raw, '"theme"\s*:\s*"[^"]*"', '"theme": "' + $Theme + '"')
        [IO.File]::WriteAllText($settingsFile, $raw, (New-Object System.Text.UTF8Encoding($false)))
    }
    Start-Wincy
}

Write-Output 'dark theme'
Set-Theme 'dark'
Send-WincyCommand '--clear'
Start-Sleep -Milliseconds 600
& (Join-Path $PSScriptRoot 'seed-history.ps1') | Out-Null
Start-Sleep -Milliseconds 500
Send-WincyCommand '--show'
Wait-UiWindow -Title 'Wincy' | Out-Null
Start-Sleep -Milliseconds 600
Save-UiScreenshot -Title 'Wincy' -Path (Join-Path $docs 'popup-dark.png')

$popup = Get-UiWindow -Title 'Wincy'
$rect = Get-UiWindowRect -Handle $popup
Send-UiKeys -Keys 'wincy' -DelayMs 600
Save-UiScreenshot -Title 'Wincy' -Path (Join-Path $docs 'popup-search.png')
Send-UiKeys -Keys '{ESC}' -DelayMs 400

Send-WincyCommand '--show'
$popup = Wait-UiWindow -Title 'Wincy'
Start-Sleep -Milliseconds 700
$rect = Get-UiWindowRect -Handle $popup
Invoke-UiClickAt -X ([int](($rect.Left + $rect.Right) / 2)) -Y ($rect.Top + 126) -Right
Start-Sleep -Milliseconds 500
Save-UiScreenshot -Title 'Wincy Menu' -Path (Join-Path $docs 'context-menu.png')
Send-UiKeys -Keys '{ESC}' -DelayMs 300
Send-UiKeys -Keys '{ESC}' -DelayMs 300

Send-WincyCommand '--settings'
Start-Sleep -Milliseconds 1300
$wincy = Get-Process Wincy | Select-Object -First 1
Save-UiScreenshot -Title '' -OwnerPid $wincy.Id -Path (Join-Path $docs 'settings-dark.png')
Send-UiKeys -Keys '{ESC}' -DelayMs 400

Send-WincyCommand '--about'
Start-Sleep -Milliseconds 1000
Save-UiScreenshot -Title '' -OwnerPid $wincy.Id -Path (Join-Path $docs 'about-dark.png')
Send-UiKeys -Keys '{ESC}' -DelayMs 400

Write-Output 'light theme'
Set-Theme 'light'
Send-WincyCommand '--show'
Wait-UiWindow -Title 'Wincy' | Out-Null
Start-Sleep -Milliseconds 700
Save-UiScreenshot -Title 'Wincy' -Path (Join-Path $docs 'popup-light.png')
Send-UiKeys -Keys '{ESC}' -DelayMs 300

Send-WincyCommand '--settings'
Start-Sleep -Milliseconds 1300
$wincy = Get-Process Wincy | Select-Object -First 1
Save-UiScreenshot -Title '' -OwnerPid $wincy.Id -Path (Join-Path $docs 'settings-light.png')
Send-UiKeys -Keys '{ESC}' -DelayMs 400

Write-Output 'empty state'
Set-Theme 'dark'
Send-WincyCommand '--clear'
Start-Sleep -Milliseconds 600
Send-WincyCommand '--show'
Wait-UiWindow -Title 'Wincy' | Out-Null
Start-Sleep -Milliseconds 700
Save-UiScreenshot -Title 'Wincy' -Path (Join-Path $docs 'popup-empty.png')
Send-UiKeys -Keys '{ESC}' -DelayMs 300

Write-Output 'done'
