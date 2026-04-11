param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $InstallDirectory = "C:\Program Files\Overdraw",
    [string] $CertificateSubject = "CN=Overdraw UIAccess Test",
    [string] $CertificateThumbprint,
    [string] $ThumbprintPath = "artifacts\certificates\overdraw-uiaccess-test.thumbprint.txt",
    [switch] $SkipSign,
    [switch] $SkipInstall
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\Overdraw.App\Overdraw.App.csproj"
$publishDirectory = Join-Path $repoRoot "artifacts\publish\uiaccess"
$fullThumbprintPath = Join-Path $repoRoot $ThumbprintPath

if (-not $CertificateThumbprint -and (Test-Path $fullThumbprintPath)) {
    $CertificateThumbprint = (Get-Content $fullThumbprintPath -Raw).Trim()
}

Remove-Item -Recurse -Force $publishDirectory -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    -p:UiAccess=true `
    -p:PublishSingleFile=false `
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
    New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null
    Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $InstallDirectory -Recurse -Force
}

$runDirectory = if ($SkipInstall) { $publishDirectory } else { $InstallDirectory }
Write-Host "UIAccess test build is ready:"
Write-Host "  Published: $publishDirectory"
Write-Host "  Run from:  $runDirectory"
Write-Host ""
Write-Host "Test command:"
Write-Host "  & '$runDirectory\Overdraw.App.exe' --pointer-ink-spike --monitor 1 --verbose"
