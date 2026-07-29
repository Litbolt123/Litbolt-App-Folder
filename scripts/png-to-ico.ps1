#Requires -Version 5.1
param(
  [Parameter(Mandatory = $true)][string]$PngPath,
  [Parameter(Mandatory = $true)][string]$IcoPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $PngPath)) {
  throw "PNG not found: $PngPath"
}

$dir = Split-Path -Parent $IcoPath
if ($dir -and -not (Test-Path $dir)) {
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$src = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $PngPath))
try {
  $ms = New-Object System.IO.MemoryStream
  $bw = New-Object System.IO.BinaryWriter $ms

  # ICONDIR
  $bw.Write([uint16]0)           # reserved
  $bw.Write([uint16]1)           # type = icon
  $bw.Write([uint16]$sizes.Count)

  $imageData = New-Object System.Collections.Generic.List[byte[]]
  $offset = 6 + (16 * $sizes.Count)

  foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::FromArgb(255, 13, 15, 20))
    $g.DrawImage($src, 0, 0, $size, $size)
    $g.Dispose()

    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $pngMs.ToArray()
    $pngMs.Dispose()
    $bmp.Dispose()
    $imageData.Add($bytes)

    $w = if ($size -ge 256) { 0 } else { [byte]$size }
    $h = $w
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)           # color count
    $bw.Write([byte]0)           # reserved
    $bw.Write([uint16]1)         # planes
    $bw.Write([uint16]32)        # bit count
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
  }

  foreach ($bytes in $imageData) {
    $bw.Write($bytes)
  }

  $bw.Flush()
  [System.IO.File]::WriteAllBytes($IcoPath, $ms.ToArray())
  $bw.Dispose()
  $ms.Dispose()
}
finally {
  $src.Dispose()
}

$fi = Get-Item -LiteralPath $IcoPath
if ($fi.Length -lt 100) { throw "ICO looks too small ($($fi.Length) bytes)" }
Write-Host ("Wrote {0} ({1:N0} bytes)" -f $IcoPath, $fi.Length)
