param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $InstallDirectory = "C:\Program Files\Overdraw",
    [string] $Monitor = "primary",
    [string] $CertificateSubject = "CN=Overdraw UIAccess Test",
    [string] $ThumbprintPath = "artifacts\certificates\overdraw-uiaccess-test.thumbprint.txt",
    [switch] $SelfContained,
    [switch] $SingleFile,
    [switch] $VerboseLaunch,
    [switch] $NoDesktopShortcut,
    [switch] $NoStartMenuShortcut,
    [switch] $Launch
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "Overdraw.sln"
$exePath = Join-Path ([System.IO.Path]::GetFullPath($InstallDirectory)) "Overdraw.App.exe"

function Test-IsAdministrator {
    return ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
        IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    throw "Setup-Overdraw.ps1 must be run from an elevated PowerShell session. Open PowerShell as Administrator and rerun this script."
}

if (-not (Test-Path $solutionPath)) {
    throw "Solution not found at $solutionPath"
}

Write-Host "Overdraw setup"
Write-Host "Repo: $repoRoot"
Write-Host "Configuration: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host "Install directory: $InstallDirectory"
Write-Host "Monitor: $Monitor"
Write-Host ""

Push-Location $repoRoot
try {
    Write-Host "Step 1/3: Building solution..."
    dotnet build $solutionPath --configuration $Configuration

    Write-Host ""
    Write-Host "Step 2/3: Installing signed UIAccess build..."
    $installArgs = @{
        Configuration = $Configuration
        Runtime = $Runtime
        InstallDirectory = $InstallDirectory
        Monitor = $Monitor
        CertificateSubject = $CertificateSubject
        ThumbprintPath = $ThumbprintPath
    }

    if ($SelfContained) {
        $installArgs.SelfContained = $true
    }

    if ($SingleFile) {
        $installArgs.SingleFile = $true
    }

    if ($VerboseLaunch) {
        $installArgs.VerboseLaunch = $true
    }

    if ($NoDesktopShortcut) {
        $installArgs.NoDesktopShortcut = $true
    }

    if ($NoStartMenuShortcut) {
        $installArgs.NoStartMenuShortcut = $true
    }

    & (Join-Path $PSScriptRoot "Install-Overdraw.ps1") @installArgs

    Write-Host ""
    Write-Host "Step 3/3: Setup complete."
    $runArgs = "--pointer-ink-spike --monitor $Monitor"
    if ($VerboseLaunch) {
        $runArgs += " --verbose"
    }

    Write-Host "Run Overdraw with:"
    Write-Host "  & '$exePath' $runArgs"

    if ($Launch) {
        Write-Host ""
        Write-Host "Launching Overdraw..."
        $launchArgs = @("--pointer-ink-spike", "--monitor", $Monitor)
        if ($VerboseLaunch) {
            $launchArgs += "--verbose"
        }

        & $exePath @launchArgs
    }
}
finally {
    Pop-Location
}
