param(
    [string] $SourcePng = "assets\icon\overdraw.png",
    [string] $OutputIco = "assets\icon\overdraw.ico",
    [int[]] $Sizes = @(16, 32, 48, 64, 128, 256)
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot $SourcePng
$outputPath = Join-Path $repoRoot $OutputIco

if (-not (Test-Path $sourcePath)) {
    throw "Source PNG not found at $sourcePath"
}

Add-Type -AssemblyName System.Drawing

function New-ResizedPngBytes([System.Drawing.Image] $SourceImage, [int] $Size) {
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImage($SourceImage, 0, 0, $Size, $Size)
        }
        finally {
            $graphics.Dispose()
        }

        $stream = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
try {
    if ($sourceImage.Width -ne $sourceImage.Height) {
        throw "Source image must be square. Actual dimensions: $($sourceImage.Width)x$($sourceImage.Height)"
    }

    $entries = @()
    foreach ($size in $Sizes) {
        if ($size -le 0 -or $size -gt 256) {
            throw "ICO sizes must be in the range 1..256. Invalid size: $size"
        }

        $entries += [PSCustomObject]@{
            Size = $size
            Bytes = New-ResizedPngBytes -SourceImage $sourceImage -Size $size
        }
    }

    $outputDirectory = Split-Path -Parent $outputPath
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

    $file = [System.IO.File]::Open($outputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = New-Object System.IO.BinaryWriter $file
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$entries.Count)

        $imageOffset = 6 + ($entries.Count * 16)
        foreach ($entry in $entries) {
            $sizeByte = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$entry.Bytes.Length)
            $writer.Write([UInt32]$imageOffset)
            $imageOffset += $entry.Bytes.Length
        }

        foreach ($entry in $entries) {
            $writer.Write([byte[]]$entry.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}
finally {
    $sourceImage.Dispose()
}

Write-Host "Created Windows icon: $outputPath"
foreach ($entry in $entries) {
    Write-Host "  $($entry.Size)x$($entry.Size)"
}
