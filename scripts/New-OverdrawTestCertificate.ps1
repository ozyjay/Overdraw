param(
    [string] $Subject = "CN=Overdraw UIAccess Test",
    [string] $CertificatePath = "artifacts\certificates\overdraw-uiaccess-test.cer",
    [string] $ThumbprintPath = "artifacts\certificates\overdraw-uiaccess-test.thumbprint.txt",
    [switch] $TrustInCurrentUserRoot,
    [switch] $TrustInLocalMachineRoot
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$fullCertificatePath = Join-Path $repoRoot $CertificatePath
$fullThumbprintPath = Join-Path $repoRoot $ThumbprintPath
$certificateDirectory = Split-Path -Parent $fullCertificatePath
New-Item -ItemType Directory -Force -Path $certificateDirectory | Out-Null

$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature `
    -HashAlgorithm SHA256

Export-Certificate -Cert $certificate -FilePath $fullCertificatePath | Out-Null
Set-Content -Path $fullThumbprintPath -Value $certificate.Thumbprint -NoNewline

if ($TrustInCurrentUserRoot) {
    Import-Certificate -FilePath $fullCertificatePath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
}

if ($TrustInLocalMachineRoot) {
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
        IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

    if (-not $isAdmin) {
        throw "-TrustInLocalMachineRoot requires an elevated PowerShell session."
    }

    Import-Certificate -FilePath $fullCertificatePath -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
}

Write-Host "Created code-signing certificate:"
Write-Host "  Subject:    $($certificate.Subject)"
Write-Host "  Thumbprint: $($certificate.Thumbprint)"
Write-Host "  Public cer: $fullCertificatePath"
Write-Host "  Thumbprint file: $fullThumbprintPath"

if (-not $TrustInCurrentUserRoot -and -not $TrustInLocalMachineRoot) {
    Write-Host ""
    Write-Host "The certificate has not been trusted yet."
    Write-Host "For UIAccess testing, rerun from an elevated shell with -TrustInLocalMachineRoot or import the .cer into LocalMachine\Root."
}

if ($TrustInLocalMachineRoot) {
    Write-Host "Trusted in: Cert:\LocalMachine\Root"
}

if ($TrustInCurrentUserRoot) {
    Write-Host "Trusted in: Cert:\CurrentUser\Root"
}
