<#
.SYNOPSIS
  Builds the Windows desktop app and the Android APK in release mode and packs them into ONE zip for a GitHub Release,
  in the layout the faculty rules prescribe.

.DESCRIPTION
  Run it from anywhere:  powershell -ExecutionPolicy Bypass -File tools\package-release.ps1

  - Cleans, then builds:  flutter clean + flutter build windows --release   (apps\ottoman_desktop)
                          flutter clean + flutter build apk --release       (apps\ottoman_mobile)
  - The Visual C++ runtime DLLs (msvcp140.dll, vcruntime140.dll, vcruntime140_1.dll) are copied from the system next
    to the exe when the system has them, so the exe also starts on a PC without the redistributable.
  - Output: <repo>\dist\fit-build-YYYY-MM-DD.zip (dist\ is git-ignored) with exactly this content:
        ottoman_mobile\build\app\outputs\flutter-apk\app-release.apk
        ottoman_desktop\build\windows\x64\runner\Release\...   (the exe folder)
    No .env or other secret goes into this zip.

  The API address is baked into each app at build time (--dart-define=API_BASE_URL). The rules ask for 10.0.2.2 in the
  Android app (the host PC as seen from the Android emulator AVD) and localhost in the Windows app. Nothing here knows
  a fixed LAN IP.

.PARAMETER ApiBaseUrl
  API address baked into the Android app. Default: http://10.0.2.2:5080
  Example for a phone on the same Wi-Fi:  -ApiBaseUrl http://<address of this PC>:5080

.PARAMETER DesktopApiBaseUrl
  API address baked into the Windows app. Default: http://localhost:5080

.PARAMETER OutputDir
  Where the zip goes. Default: <repo>\dist

.PARAMETER PackageOnly
  Do not clean or build; pack the build folders that already exist (both must exist).

.PARAMETER Flutter
  The flutter command. Default: flutter (from PATH).
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://10.0.2.2:5080',
    [string]$DesktopApiBaseUrl = 'http://localhost:5080',
    [string]$OutputDir,
    [switch]$PackageOnly,
    [string]$Flutter = 'flutter'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repo 'dist' }

function Assert-HttpUrl([string]$name, [string]$value) {
    $uri = $null
    if (-not [Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http', 'https')) {
        throw "$name must be an absolute http(s) address such as http://10.0.2.2:5080, got '$value'."
    }
}

function Invoke-Flutter([string]$workingDir, [string[]]$arguments) {
    Push-Location $workingDir
    try {
        Write-Host "> $Flutter $($arguments -join ' ')   (in $workingDir)" -ForegroundColor Cyan
        & $Flutter @arguments
        if ($LASTEXITCODE -ne 0) { throw "flutter $($arguments[0..1] -join ' ') failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }
}

Assert-HttpUrl 'ApiBaseUrl' $ApiBaseUrl
Assert-HttpUrl 'DesktopApiBaseUrl' $DesktopApiBaseUrl
$desktop = Join-Path $repo 'apps\ottoman_desktop'
$mobile = Join-Path $repo 'apps\ottoman_mobile'
$release = Join-Path $desktop 'build\windows\x64\runner\Release'
$apk = Join-Path $mobile 'build\app\outputs\flutter-apk\app-release.apk'

if (-not $PackageOnly) {
    if (-not (Get-Command $Flutter -ErrorAction SilentlyContinue)) {
        throw "Cannot find '$Flutter'. Put Flutter on PATH or pass -Flutter <path to flutter.bat>."
    }
    # Windows
    Invoke-Flutter $desktop @('clean')
    Invoke-Flutter $desktop @('pub', 'get')
    Invoke-Flutter $desktop @('build', 'windows', '--release', "--dart-define=API_BASE_URL=$DesktopApiBaseUrl")
    # Android
    Invoke-Flutter $mobile @('clean')
    Invoke-Flutter $mobile @('pub', 'get')
    Invoke-Flutter $mobile @('build', 'apk', '--release', "--dart-define=API_BASE_URL=$ApiBaseUrl")
}

if (-not (Test-Path (Join-Path $release 'ottoman_desktop.exe'))) { throw "$release\ottoman_desktop.exe is missing; build the Windows app first." }
if (-not (Test-Path $apk)) { throw "$apk is missing; build the Android app first." }

# --------------------------------------------------------------------------------------------- stage and zip
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$stage = Join-Path $OutputDir 'stage'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }

$stageRelease = Join-Path $stage 'ottoman_desktop\build\windows\x64\runner\Release'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $stageRelease) | Out-Null
Copy-Item -Recurse -Force $release $stageRelease

foreach ($dll in 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll') {
    $source = Join-Path $env:SystemRoot "System32\$dll"
    if (Test-Path $source) {
        Copy-Item -Force $source $stageRelease
        Write-Host "  copied $dll"
    }
    else {
        Write-Warning "$dll is not on this PC; the exe needs the Visual C++ redistributable on a PC that lacks it."
    }
}

$stageApkDir = Join-Path $stage 'ottoman_mobile\build\app\outputs\flutter-apk'
New-Item -ItemType Directory -Force -Path $stageApkDir | Out-Null
Copy-Item -Force $apk (Join-Path $stageApkDir 'app-release.apk')

$zip = Join-Path $OutputDir ("fit-build-{0:yyyy-MM-dd}.zip" -f (Get-Date))
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item -Recurse -Force $stage

Write-Host ""
Write-Host "Release zip: $zip" -ForegroundColor Green
Write-Host "  Android APK: ottoman_mobile/build/app/outputs/flutter-apk/app-release.apk (API $ApiBaseUrl)"
Write-Host "  Windows exe: ottoman_desktop/build/windows/x64/runner/Release (API $DesktopApiBaseUrl)"
