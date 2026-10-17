#Requires -Version 5.1
<#
.SYNOPSIS
    Builds Wincy for Windows with the C# compiler that ships with .NET Framework.

.DESCRIPTION
    No SDK, no NuGet, no Node: Windows 10/11 already contain csc.exe and the
    reference assemblies, so the app builds on a clean machine and the output runs
    on any Windows install without a runtime download.

    src\*.cs            -> dist\Wincy.exe (WinExe, themed icon + DPI manifest)
    tests\SelfTest.cs   -> tests\_selftest.exe (headless logic checks)

.PARAMETER Test
    Build and run the headless self tests.

.PARAMETER Run
    Start the freshly built app when the build succeeds.

.PARAMETER Clean
    Remove build output before building.

.EXAMPLE
    .\build.ps1 -Test
    .\build.ps1 -Run
#>
[CmdletBinding()]
param(
    [switch]$Test,
    [switch]$Run,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$distDir = Join-Path $root 'dist'
$testDir = Join-Path $root 'tests'
$assets = Join-Path $root 'assets'

$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $frameworkDir 'csc.exe'))) {
    $frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$csc = Join-Path $frameworkDir 'csc.exe'
if (-not (Test-Path $csc)) {
    throw "Could not find csc.exe in $frameworkDir. Windows 10/11 should have it."
}

if ($Clean) {
    Remove-Item -Recurse -Force $distDir -ErrorAction SilentlyContinue
    Remove-Item -Force (Join-Path $testDir '_selftest.exe') -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

function Invoke-Csc {
    param([string[]]$Arguments, [string]$Label)
    Write-Host "  csc $Label" -ForegroundColor DarkGray
    $output = & $csc @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "Compilation failed ($Label), exit code $LASTEXITCODE"
    }
    $warnings = @($output | Where-Object { $_ -match 'warning' })
    if ($warnings.Count -gt 0) {
        $warnings | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    }
}

# ---------------------------------------------------------------- app
$appSources = @(Get-ChildItem -Path $srcDir -Filter '*.cs' | Sort-Object Name | Select-Object -ExpandProperty FullName)
if ($appSources.Count -eq 0) { throw "No sources found in $srcDir" }

$icon = Join-Path $assets 'wincy.ico'
$manifest = Join-Path $srcDir 'app.manifest'
$exe = Join-Path $distDir 'Wincy.exe'

Write-Host "Building Wincy for Windows" -ForegroundColor Cyan
$appArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug:pdbonly',
    "/out:$exe",
    "/win32icon:$icon",
    ('/win32manifest:' + $manifest),
    '/langversion:5'
) + $appSources
Invoke-Csc -Arguments $appArgs -Label 'Wincy.exe'

$info = Get-Item $exe
Write-Host ("  -> {0} ({1:N0} KB)" -f $exe, ($info.Length / 1KB)) -ForegroundColor Green

# ---------------------------------------------------------------- tests
if ($Test) {
    Write-Host "Building self tests" -ForegroundColor Cyan
    $logicSources = @(
        'Json.cs', 'Paths.cs', 'Log.cs', 'I18n.cs', 'ClipboardItem.cs', 'Search.cs',
        'HotkeySpec.cs', 'AppSettings.cs', 'HistoryStore.cs'
    ) | ForEach-Object { Join-Path $srcDir $_ }
    $testExe = Join-Path $testDir '_selftest.exe'
    $testArgs = @(
        '/nologo',
        '/target:exe',
        '/platform:anycpu',
        '/optimize+',
        "/out:$testExe",
        '/main:Wincy.Tests.SelfTest',
        '/langversion:5'
    ) + $logicSources + @((Join-Path $testDir 'SelfTest.cs'))
    Invoke-Csc -Arguments $testArgs -Label '_selftest.exe'

    Write-Host "Running self tests" -ForegroundColor Cyan
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw "Self tests failed (exit code $LASTEXITCODE)" }

    Write-Host "Building the end-to-end paste target" -ForegroundColor Cyan
    # A leftover test target would lock the output file.
    Get-Process -Name '_pastetarget' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 150
    $targetExe = Join-Path $testDir '_pastetarget.exe'
    Invoke-Csc -Arguments @(
        '/nologo',
        '/target:winexe',
        '/platform:anycpu',
        '/optimize+',
        "/out:$targetExe",
        '/main:Wincy.Tests.PasteTarget',
        '/langversion:5',
        (Join-Path $testDir 'PasteTarget.cs')
    ) -Label '_pastetarget.exe'
}

if ($Run) {
    Write-Host "Starting $exe" -ForegroundColor Cyan
    Start-Process -FilePath $exe | Out-Null
}

Write-Host "Done." -ForegroundColor Green
