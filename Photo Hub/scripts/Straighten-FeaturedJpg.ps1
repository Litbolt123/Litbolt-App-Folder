# Straighten sideways JPGs in Widget Photos\Featured (EXIF orientation baked into pixels).
$ErrorActionPreference = 'Stop'
try { (Get-Process -Id $PID).PriorityClass = 'BelowNormal' } catch {}
$dir = 'C:\Users\Augus\Pictures\Widget Photos\Featured'
if (-not (Test-Path -LiteralPath $dir)) { Write-Host "No Featured folder."; exit 0 }
Add-Type -AssemblyName System.Drawing
$rot = @{2='RotateNoneFlipX';3='Rotate180FlipNone';4='Rotate180FlipX';5='Rotate90FlipX';6='Rotate90FlipNone';7='Rotate270FlipX';8='Rotate270FlipNone'}
$fixed = 0
Get-ChildItem -LiteralPath $dir -Filter *.jpg | ForEach-Object {
  $f = $_.FullName
  $img = [System.Drawing.Image]::FromFile($f)
  $o = 1
  if ($img.PropertyIdList -contains 0x0112) { $o = [BitConverter]::ToUInt16($img.GetPropertyItem(0x0112).Value, 0) }
  if ($o -gt 1) {
    $img.RotateFlip([System.Drawing.RotateFlipType]::($rot[[int]$o]))
    $img.RemovePropertyItem(0x0112)
    $img.Save("$f.tmp.jpg", [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $img.Dispose()
    Move-Item -LiteralPath "$f.tmp.jpg" -Destination $f -Force
    $fixed++
  } else { $img.Dispose() }
}
Write-Host "Straightened $fixed photos in Featured."
