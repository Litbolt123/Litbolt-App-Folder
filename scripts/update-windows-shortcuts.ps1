param(
  [Parameter(Mandatory = $true)][string]$SourceDir,
  [Parameter(Mandatory = $true)][string]$AppName,
  [Parameter(Mandatory = $true)][string]$ExeName,
  [Parameter(Mandatory = $true)][string]$ProgramsFolder,
  [switch]$SkipProcessStop,
  [switch]$SkipDesktop
)

$ErrorActionPreference = "Stop"
$dest = Join-Path $env:LOCALAPPDATA "Programs\$ProgramsFolder"
$exeNameOnly = [IO.Path]::GetFileNameWithoutExtension($ExeName)

if (-not $SkipProcessStop) {
  Get-Process -Name $exeNameOnly -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Write-Host "Syncing $AppName"
Write-Host "  From: $SourceDir"
Write-Host "  To:   $dest"

$robocopyArgs = @(
    $SourceDir,
    $dest,
    '/MIR',
    '/NFL', '/NDL', '/NJH', '/NJS', '/NC', '/NS', '/NP'
)
$rc = & robocopy @robocopyArgs
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}

$iconPath = Join-Path $dest "app.ico"
if (-not (Test-Path $iconPath)) {
  $fromSource = Join-Path $SourceDir "app.ico"
  if (Test-Path $fromSource) {
    Copy-Item $fromSource $iconPath -Force
  }
}

$exe = Join-Path $dest $ExeName
$shell = New-Object -ComObject WScript.Shell

function Set-Shortcut([string]$path) {
  $sc = $shell.CreateShortcut($path)
  $sc.TargetPath = $exe
  $sc.WorkingDirectory = $dest
  $sc.Description = $AppName
  if (Test-Path $iconPath) {
    $sc.IconLocation = "$iconPath,0"
  }
  $sc.Save()
  Write-Host "  Shortcut: $path"
}

$startDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
New-Item -ItemType Directory -Force -Path $startDir | Out-Null
Set-Shortcut (Join-Path $startDir "$AppName.lnk")
if (-not $SkipDesktop) {
  Set-Shortcut (Join-Path ([Environment]::GetFolderPath("Desktop")) "$AppName.lnk")
} else {
  $desktopLnk = Join-Path ([Environment]::GetFolderPath("Desktop")) "$AppName.lnk"
  if (Test-Path $desktopLnk) {
    Remove-Item -LiteralPath $desktopLnk -Force
    Write-Host "  Removed desktop shortcut: $desktopLnk"
  }
}

Write-Host "Done. Shortcuts now launch: $exe"
