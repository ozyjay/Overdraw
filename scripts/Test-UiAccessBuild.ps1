param(
    [string] $ExePath = "C:\Program Files\Overdraw\Overdraw.App.exe",
    [string] $ThumbprintPath = "artifacts\certificates\overdraw-uiaccess-test.thumbprint.txt"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Executable not found at $ExePath"
}

$signature = Get-AuthenticodeSignature -FilePath $ExePath
Write-Host "Executable: $ExePath"
Write-Host "Signature status: $($signature.Status)"
Write-Host "Signature message: $($signature.StatusMessage)"

if ($signature.SignerCertificate) {
    Write-Host "Signer subject: $($signature.SignerCertificate.Subject)"
    Write-Host "Signer thumbprint: $($signature.SignerCertificate.Thumbprint)"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$fullThumbprintPath = Join-Path $repoRoot $ThumbprintPath
if (Test-Path $fullThumbprintPath) {
    $expectedThumbprint = (Get-Content $fullThumbprintPath -Raw).Trim()
    $matchesExpectedThumbprint = $signature.SignerCertificate -and
        $signature.SignerCertificate.Thumbprint -eq $expectedThumbprint
    Write-Host "Matches repo thumbprint: $matchesExpectedThumbprint"

    if (-not $matchesExpectedThumbprint) {
        throw "The executable signer does not match the repo thumbprint file."
    }
}

if ($signature.SignerCertificate) {
    $rootCertificate = Get-ChildItem Cert:\LocalMachine\Root |
        Where-Object { $_.Thumbprint -eq $signature.SignerCertificate.Thumbprint } |
        Select-Object -First 1
    $trustedInLocalMachineRoot = $null -ne $rootCertificate
    Write-Host "Trusted in LocalMachine Root: $trustedInLocalMachineRoot"

    if (-not $trustedInLocalMachineRoot) {
        throw "The executable signer is not trusted in Cert:\LocalMachine\Root. UIAccess launch can fail with 'A referral was returned from the server'."
    }
}

$programFiles = [Environment]::GetFolderPath("ProgramFiles")
$programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
$windows = [Environment]::GetFolderPath("Windows")
$fullPath = [System.IO.Path]::GetFullPath($ExePath)

$secureLocation =
    $fullPath.StartsWith($programFiles, [StringComparison]::OrdinalIgnoreCase) -or
    ($programFilesX86 -and $fullPath.StartsWith($programFilesX86, [StringComparison]::OrdinalIgnoreCase)) -or
    $fullPath.StartsWith((Join-Path $windows "System32"), [StringComparison]::OrdinalIgnoreCase)

Write-Host "Secure location: $secureLocation"

if ($signature.Status -ne "Valid") {
    throw "The executable signature is not valid. UIAccess launch will fail."
}

if (-not $secureLocation) {
    throw "The executable is not installed in a default secure UIAccess location."
}

Write-Host "UIAccess preflight checks passed."
