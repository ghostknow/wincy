# Behaviour checks against a running Wincy: capture, delete, pin, sensitive data.
# These drive the real UI (clipboard + popup) rather than calling into the app.
#
# Usage: .\behavior-test.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui.ps1')

$root = Split-Path -Parent $PSScriptRoot
$dataDir = Join-Path $env:APPDATA 'Wincy'
$historyFile = Join-Path $dataDir 'history.json'
$wincy = Get-Process Wincy -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $wincy) {
    Start-Process (Join-Path $root 'dist\Wincy.exe') | Out-Null
    Start-Sleep -Seconds 2
    $wincy = Get-Process Wincy | Select-Object -First 1
}

$passed = 0
$failed = 0
function Assert-True {
    param([bool]$Condition, [string]$Label)
    if ($Condition) { $script:passed++; Write-Output "  ok   $Label" }
    else { $script:failed++; Write-Output "  FAIL $Label" }
}
function Get-Items {
    if (-not (Test-Path $historyFile)) { return @() }
    $json = Get-Content $historyFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $json.items) { return @() }
    return @($json.items)
}
function Set-Clip {
    param([string]$Text)
    [System.Windows.Forms.Clipboard]::SetText($Text)
    Start-Sleep -Milliseconds 550
}

Write-Output '--- setup ---'
Start-Process (Join-Path $root 'dist\Wincy.exe') -ArgumentList '--clear' | Out-Null
Start-Sleep -Milliseconds 700
Assert-True ((Get-Items).Count -eq 0) 'history starts empty'

Write-Output '--- capture ---'
Set-Clip 'alpha-one'
Set-Clip 'beta-two'
Set-Clip 'gamma-three'
$items = Get-Items
Assert-True ($items.Count -eq 3) 'three captures stored'
Assert-True ($items[0].text -eq 'gamma-three') 'newest capture is first'
Assert-True ($items[0].sourceApp -eq 'Windows PowerShell') 'source app recorded'

Write-Output '--- duplicate handling ---'
Set-Clip 'alpha-one'
$items = Get-Items
Assert-True ($items.Count -eq 3) 'duplicate is not stored twice'
Assert-True ($items[0].text -eq 'alpha-one') 'duplicate bubbles to the top'

Write-Output '--- delete ---'
$beforeDelete = Get-Items
$expectedRemoved = $beforeDelete[1].text
Start-Process (Join-Path $root 'dist\Wincy.exe') -ArgumentList '--show' | Out-Null
$popup = Wait-UiWindow -Title 'Wincy'
Assert-True ($popup -ne [IntPtr]::Zero) 'popup opened'
# Focus can lag the first paint; make the keyboard steps deterministic.
[void][Ui]::Focus($popup)
Start-Sleep -Milliseconds 350
Send-UiKeys -Keys '{DOWN}' -DelayMs 250
Send-UiKeys -Keys '{DELETE}' -DelayMs 400
$items = Get-Items
Assert-True ($items.Count -eq 2) 'delete removed one item'
Assert-True (-not ($items | Where-Object { $_.text -eq $expectedRemoved })) "removed the selected item ($expectedRemoved)"

Write-Output '--- pin ---'
Send-UiKeys -Keys '{DOWN}' -DelayMs 200
Send-UiKeys -Keys '^p' -DelayMs 400
$items = Get-Items
$pinned = @($items | Where-Object { $_.pinned })
Assert-True ($pinned.Count -eq 1) 'one item pinned'
Assert-True ($items[0].pinned) 'pinned item moved to the top'

Write-Output '--- sensitive content ---'
Send-UiKeys -Keys '{ESC}' -DelayMs 300
$before = (Get-Items).Count
$data = New-Object System.Windows.Forms.DataObject
$data.SetData([System.Windows.Forms.DataFormats]::UnicodeText, 'hunter2-not-recorded')
$data.SetData('CanIncludeInClipboardHistory', 0)
[System.Windows.Forms.Clipboard]::SetDataObject($data, $true)
Start-Sleep -Milliseconds 600
Assert-True ((Get-Items).Count -eq $before) 'content marked "no history" is ignored'

$data2 = New-Object System.Windows.Forms.DataObject
$data2.SetData([System.Windows.Forms.DataFormats]::UnicodeText, 'confidential-not-recorded')
$data2.SetData('ExcludeClipboardContentFromMonitorProcessing', 1)
[System.Windows.Forms.Clipboard]::SetDataObject($data2, $true)
Start-Sleep -Milliseconds 600
Assert-True ((Get-Items).Count -eq $before) 'content marked confidential is ignored'

Write-Output '--- own writes are not re-captured ---'
$log = Join-Path $dataDir 'wincy.log'
$before = (Get-Items).Count
Start-Process (Join-Path $root 'dist\Wincy.exe') -ArgumentList '--show' | Out-Null
Start-Sleep -Milliseconds 800
Send-UiKeys -Keys '{ENTER}' -DelayMs 800
$items = Get-Items
Assert-True ($items.Count -eq $before) 'pasting does not add a duplicate item'

Write-Output ''
if ($failed -eq 0) { Write-Output "PASS - $passed checks" } else { Write-Output "FAIL - $passed passed, $failed failed" }
exit $failed
