param(
    [string] $InstallDirectory = "C:\Program Files\Overdraw",
    [string] $CertificateSubject = "CN=Overdraw UIAccess Test",
    [string] $ThumbprintPath = "artifacts\certificates\overdraw-uiaccess-test.thumbprint.txt",
    [switch] $RemoveCertificates,
    [switch] $RemoveArtifacts,
    [switch] $KeepShortcuts
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$fullThumbprintPath = Join-Path $repoRoot $ThumbprintPath
$resolvedInstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)

function Remove-PathIfPresent([string] $Path) {
    if (Test-Path $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
        Write-Host "Removed: $Path"
    }
}

function Test-IsAdministrator {
    return ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
        IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-TargetCertificateThumbprints {
    if (Test-Path $fullThumbprintPath) {
        return @((Get-Content $fullThumbprintPath -Raw).Trim())
    }

    $certificates = @(
        Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue
        Get-ChildItem Cert:\CurrentUser\Root -ErrorAction SilentlyContinue
        Get-ChildItem Cert:\LocalMachine\Root -ErrorAction SilentlyContinue
    ) | Where-Object { $_ -and $_.Subject -eq $CertificateSubject }

    return @($certificates | Select-Object -ExpandProperty Thumbprint -Unique)
}

if (Test-Path $resolvedInstallDirectory) {
    if ($resolvedInstallDirectory -eq [System.IO.Path]::GetPathRoot($resolvedInstallDirectory)) {
        throw "Refusing to remove filesystem root: $resolvedInstallDirectory"
    }

    if ($resolvedInstallDirectory.StartsWith([Environment]::GetFolderPath("ProgramFiles"), [StringComparison]::OrdinalIgnoreCase) -and -not (Test-IsAdministrator)) {
        throw "Removing $resolvedInstallDirectory requires an elevated PowerShell session."
    }

    Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
    Write-Host "Removed install directory: $resolvedInstallDirectory"
}
else {
    Write-Host "Install directory not present: $resolvedInstallDirectory"
}

if (-not $KeepShortcuts) {
    Remove-PathIfPresent (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Overdraw.lnk")
    Remove-PathIfPresent (Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Overdraw")
}

if ($RemoveCertificates) {
    $thumbprints = Get-TargetCertificateThumbprints
    if ($thumbprints.Count -eq 0) {
        Write-Host "No matching Overdraw test certificates found."
    }

    foreach ($thumbprint in $thumbprints) {
        Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
            Where-Object { $_.Thumbprint -eq $thumbprint } |
            Remove-Item -Force

        Get-ChildItem Cert:\CurrentUser\Root -ErrorAction SilentlyContinue |
            Where-Object { $_.Thumbprint -eq $thumbprint } |
            Remove-Item -Force

        $localMachineRootCertificate = Get-ChildItem Cert:\LocalMachine\Root -ErrorAction SilentlyContinue |
            Where-Object { $_.Thumbprint -eq $thumbprint }

        if ($localMachineRootCertificate) {
            if (-not (Test-IsAdministrator)) {
                throw "Removing Cert:\LocalMachine\Root\$thumbprint requires an elevated PowerShell session."
            }

            $localMachineRootCertificate | Remove-Item -Force
        }

        Write-Host "Removed certificate trust/signing entries for thumbprint: $thumbprint"
    }
}
else {
    Write-Host "Certificate stores were not changed. Pass -RemoveCertificates to remove the Overdraw test certificate entries."
}

if ($RemoveArtifacts) {
    $artifactsPath = Join-Path $repoRoot "artifacts"
    if (Test-Path $artifactsPath) {
        Remove-Item -LiteralPath $artifactsPath -Recurse -Force
        Write-Host "Removed repo artifacts directory: $artifactsPath"
    }
    else {
        Write-Host "Artifacts directory not present: $artifactsPath"
    }
}
