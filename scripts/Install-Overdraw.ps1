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
    [switch] $NoStartMenuShortcut
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedInstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$fullThumbprintPath = Join-Path $repoRoot $ThumbprintPath
$exePath = Join-Path $resolvedInstallDirectory "Overdraw.App.exe"

function Test-IsAdministrator {
    return ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
        IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function New-Shortcut([string] $Path, [string] $TargetPath, [string] $Arguments, [string] $WorkingDirectory) {
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $TargetPath
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Description = "Overdraw pen overlay"
    $shortcut.Save()
}

if (-not (Test-IsAdministrator)) {
    throw "Install-Overdraw.ps1 must be run from an elevated PowerShell session."
}

if (-not (Test-Path $fullThumbprintPath)) {
    Write-Host "No repo certificate thumbprint found. Creating and trusting a local UIAccess test certificate."
    & (Join-Path $PSScriptRoot "New-OverdrawTestCertificate.ps1") `
        -Subject $CertificateSubject `
        -ThumbprintPath $ThumbprintPath `
        -TrustInLocalMachineRoot
}

$publishArgs = @{
    Configuration = $Configuration
    Runtime = $Runtime
    InstallDirectory = $resolvedInstallDirectory
    CertificateSubject = $CertificateSubject
    ThumbprintPath = $ThumbprintPath
    SelfContained = $SelfContained.IsPresent
    SingleFile = $SingleFile.IsPresent
}

& (Join-Path $PSScriptRoot "Publish-UiAccessTestBuild.ps1") @publishArgs
& (Join-Path $PSScriptRoot "Test-UiAccessBuild.ps1") -ExePath $exePath -ThumbprintPath $ThumbprintPath

$shortcutArgs = "--pointer-ink-spike --monitor $Monitor"
if ($VerboseLaunch) {
    $shortcutArgs += " --verbose"
}

if (-not $NoStartMenuShortcut) {
    $startMenuPath = Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Overdraw\Overdraw.lnk"
    New-Shortcut -Path $startMenuPath -TargetPath $exePath -Arguments $shortcutArgs -WorkingDirectory $resolvedInstallDirectory
    Write-Host "Start Menu shortcut: $startMenuPath"
}

if (-not $NoDesktopShortcut) {
    $desktopPath = Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Overdraw.lnk"
    New-Shortcut -Path $desktopPath -TargetPath $exePath -Arguments $shortcutArgs -WorkingDirectory $resolvedInstallDirectory
    Write-Host "Desktop shortcut: $desktopPath"
}

Write-Host "Overdraw installed."
Write-Host "Run command:"
Write-Host "  & '$exePath' $shortcutArgs"
