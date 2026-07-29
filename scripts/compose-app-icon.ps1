#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)][string]$PngPath,
    [string]$OutPngPath = "",
    [string]$IcoPath = "",
    [int]$CornerRadiusPercent = 22
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $PngPath)) {
    throw "PNG not found: $PngPath"
}

if ([string]::IsNullOrWhiteSpace($OutPngPath)) {
    $OutPngPath = $PngPath
}

$src = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $PngPath))
$bmp = $null
try {
    $size = $src.Width
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    $black = [System.Drawing.Color]::FromArgb(255, 13, 15, 20)
    $g.Clear($black)

    $radius = [int][Math]::Round($size * ($CornerRadiusPercent / 100.0))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.SetClip($path)

    $g.DrawImage($src, 0, 0, $size, $size)
    $g.Dispose()
    $src.Dispose()
    $src = $null

    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) {
            $pixel = $bmp.GetPixel($x, $y)
            if ($pixel.A -lt 32) {
                $bmp.SetPixel($x, $y, $black)
                continue
            }

            if ($pixel.R -gt 245 -and $pixel.G -gt 245 -and $pixel.B -gt 245) {
                $bmp.SetPixel($x, $y, $black)
            }
        }
    }

    $resolvedOut = [IO.Path]::GetFullPath($OutPngPath)
    $tempPath = [IO.Path]::Combine([IO.Path]::GetTempPath(), [Guid]::NewGuid().ToString('N') + '.png')
    $bmp.Save($tempPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $bmp = $null

    $outDir = Split-Path -Parent $resolvedOut
    if ($outDir -and -not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    }

    Copy-Item -LiteralPath $tempPath -Destination $resolvedOut -Force
    Remove-Item -LiteralPath $tempPath -Force
    Write-Host "Wrote $resolvedOut"
}
finally {
    if ($null -ne $src) { $src.Dispose() }
    if ($null -ne $bmp) { $bmp.Dispose() }
}

if (-not [string]::IsNullOrWhiteSpace($IcoPath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    & (Join-Path $scriptDir 'png-to-ico.ps1') -PngPath ([IO.Path]::GetFullPath($OutPngPath)) -IcoPath $IcoPath
}
