<#
.SYNOPSIS
  Builds the Windows desktop app and the Android APK in release mode and zips both for handing in.

.DESCRIPTION
  Run it from anywhere:  powershell -ExecutionPolicy Bypass -File tools\package-release.ps1

  - Windows:  flutter build windows --release   (apps\ottoman_desktop)
  - Android:  flutter build apk --release       (apps\ottoman_mobile)
  - The Visual C++ runtime DLLs (msvcp140.dll, vcruntime140.dll, vcruntime140_1.dll) are copied from the system next
    to the exe when the system has them, so the exe also starts on a PC without the redistributable.
  - Output goes to <repo>\dist: ottoman_desktop_windows.zip and ottoman_mobile_android.zip (the APK and a short note).
  dist\ is git-ignored.

  The API address is baked into each app at build time (--dart-define=API_BASE_URL). The default for the phone,
  http://10.0.2.2:5080, is the host PC as seen from the Android emulator. For a real phone pass the PC's address in
  the local network yourself; nothing here knows a fixed IP.

.PARAMETER ApiBaseUrl
  API address baked into the Android app. Default: http://10.0.2.2:5080 (Android emulator -> host PC).
  Example for a phone on the same Wi-Fi:  -ApiBaseUrl http://<address of this PC>:5080

.PARAMETER DesktopApiBaseUrl
  API address baked into the Windows app. Default: http://localhost:5080 (the API runs on the same PC).

.PARAMETER OutputDir
  Where the zips go. Default: <repo>\dist

.PARAMETER SkipWindows
  Do not build the Windows app.

.PARAMETER SkipAndroid
  Do not build the Android app.

.PARAMETER Flutter
  The flutter command. Default: flutter (from PATH).
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://10.0.2.2:5080',
    [string]$DesktopApiBaseUrl = 'http://localhost:5080',
    [string]$OutputDir,
    [switch]$SkipWindows,
    [switch]$SkipAndroid,
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
if (-not (Get-Command $Flutter -ErrorAction SilentlyContinue)) {
    throw "Cannot find '$Flutter'. Put Flutter on PATH or pass -Flutter <path to flutter.bat>."
}
if ($SkipWindows -and $SkipAndroid) { throw 'Nothing to do: both -SkipWindows and -SkipAndroid are set.' }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ----------------------------------------------------------------------------------------------- Windows
if (-not $SkipWindows) {
    $desktop = Join-Path $repo 'apps\ottoman_desktop'
    Invoke-Flutter $desktop @('build', 'windows', '--release', "--dart-define=API_BASE_URL=$DesktopApiBaseUrl")

    $release = Join-Path $desktop 'build\windows\x64\runner\Release'
    if (-not (Test-Path (Join-Path $release 'ottoman_desktop.exe'))) {
        throw "The build finished but $release\ottoman_desktop.exe is missing."
    }

    $stage = Join-Path $OutputDir 'ottoman_desktop_windows'
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    Copy-Item -Recurse -Force $release $stage

    foreach ($dll in 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll') {
        $source = Join-Path $env:SystemRoot "System32\$dll"
        if (Test-Path $source) {
            Copy-Item -Force $source $stage
            Write-Host "  copied $dll"
        }
        else {
            Write-Warning "$dll is not on this PC; the exe needs the Visual C++ redistributable on a PC that lacks it."
        }
    }

    $zip = Join-Path $OutputDir 'ottoman_desktop_windows.zip'
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host "Windows app: $zip" -ForegroundColor Green
}

# ----------------------------------------------------------------------------------------------- Android
if (-not $SkipAndroid) {
    $mobile = Join-Path $repo 'apps\ottoman_mobile'
    Invoke-Flutter $mobile @('build', 'apk', '--release', "--dart-define=API_BASE_URL=$ApiBaseUrl")

    $apk = Join-Path $mobile 'build\app\outputs\flutter-apk\app-release.apk'
    if (-not (Test-Path $apk)) { throw "The build finished but $apk is missing." }

    $stage = Join-Path $OutputDir 'ottoman_mobile_android'
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item -Force $apk (Join-Path $stage 'ottoman_mobile.apk')
    @(
        "Ottoman mobile app (Android release build).",
        "API address baked into this build: $ApiBaseUrl",
        "Install: adb install ottoman_mobile.apk (or copy the file to the phone and open it).",
        "The address cannot be changed without building again: tools\package-release.ps1 -ApiBaseUrl <address>."
    ) | Set-Content -Encoding UTF8 (Join-Path $stage 'README.txt')

    $zip = Join-Path $OutputDir 'ottoman_mobile_android.zip'
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host "Android app: $zip (API $ApiBaseUrl)" -ForegroundColor Green
}
