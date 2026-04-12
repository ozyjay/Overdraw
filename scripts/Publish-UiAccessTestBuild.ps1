param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $InstallDirectory = "C:\Program Files\Overdraw",
    [string] $CertificateSubject = "CN=Overdraw UIAccess Test",
    [string] $CertificateThumbprint,
    [string] $ThumbprintPath = "artifacts\certificates\overdraw-uiaccess-test.thumbprint.txt",
    [switch] $SelfContained,
    [switch] $SingleFile,
    [switch] $SkipSign,
    [switch] $SkipInstall,
    [switch] $NoCleanInstall
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\Overdraw.App\Overdraw.App.csproj"
$publishDirectory = Join-Path $repoRoot "artifacts\publish\uiaccess"
$fullThumbprintPath = Join-Path $repoRoot $ThumbprintPath
$resolvedInstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$selfContainedValue = $SelfContained.IsPresent.ToString().ToLowerInvariant()
$singleFileValue = $SingleFile.IsPresent.ToString().ToLowerInvariant()

function Test-IsAdministrator {
    return ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
        IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-RequiresAdministratorForInstall([string] $Path) {
    $programFiles = [Environment]::GetFolderPath("ProgramFiles")
    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $windows = [Environment]::GetFolderPath("Windows")

    return $Path.StartsWith($programFiles, [StringComparison]::OrdinalIgnoreCase) -or
        ($programFilesX86 -and $Path.StartsWith($programFilesX86, [StringComparison]::OrdinalIgnoreCase)) -or
        $Path.StartsWith($windows, [StringComparison]::OrdinalIgnoreCase)
}

if (-not $CertificateThumbprint -and (Test-Path $fullThumbprintPath)) {
    $CertificateThumbprint = (Get-Content $fullThumbprintPath -Raw).Trim()
}

if (-not $SkipInstall -and (Test-RequiresAdministratorForInstall $resolvedInstallDirectory) -and -not (Test-IsAdministrator)) {
    throw "Installing to $resolvedInstallDirectory requires an elevated PowerShell session. Rerun as Administrator or pass -SkipInstall."
}

Remove-Item -Recurse -Force $publishDirectory -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $selfContainedValue `
    -p:UiAccess=true `
    -p:PublishSingleFile=$singleFileValue `
    -o $publishDirectory

$exePath = Join-Path $publishDirectory "Overdraw.App.exe"
if (-not (Test-Path $exePath)) {
    throw "Published executable not found at $exePath"
}

if (-not $SkipSign) {
    if ($CertificateThumbprint) {
        $certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
            Where-Object { $_.Thumbprint -eq $CertificateThumbprint } |
            Select-Object -First 1
    }
    else {
        $certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
            Where-Object { $_.Subject -eq $CertificateSubject } |
            Sort-Object NotAfter -Descending |
            Select-Object -First 1
    }

    if ($null -eq $certificate) {
        throw "No code-signing certificate found. Run scripts\New-OverdrawTestCertificate.ps1 first."
    }

    $signature = Set-AuthenticodeSignature -FilePath $exePath -Certificate $certificate -HashAlgorithm SHA256
    if ($signature.Status -ne "Valid") {
        throw "Signing failed: $($signature.Status) $($signature.StatusMessage)"
    }

    Write-Host "Signed with certificate thumbprint: $($certificate.Thumbprint)"
}

if (-not $SkipInstall) {
    if (Test-Path $resolvedInstallDirectory) {
        if (-not $NoCleanInstall) {
            Get-ChildItem -LiteralPath $resolvedInstallDirectory -Force |
                Remove-Item -Recurse -Force
        }
    }
    else {
        New-Item -ItemType Directory -Force -Path $resolvedInstallDirectory | Out-Null
    }

    Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $resolvedInstallDirectory -Recurse -Force
}

$runDirectory = if ($SkipInstall) { $publishDirectory } else { $resolvedInstallDirectory }
Write-Host "UIAccess test build is ready:"
Write-Host "  Published: $publishDirectory"
Write-Host "  Run from:  $runDirectory"
Write-Host "  Self-contained: $($SelfContained.IsPresent)"
Write-Host "  Single file:    $($SingleFile.IsPresent)"
Write-Host ""
Write-Host "Test command:"
Write-Host "  & '$runDirectory\Overdraw.App.exe' --pointer-ink-spike --monitor 1 --verbose"
