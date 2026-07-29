#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes and compiles Inno Setup installers for Litbolt's App Folder and/or Local Video Hub.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
  powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -App VideoHub
  powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -InstallPrerequisites
#>
param(
    [ValidateSet('AppFolder', 'VideoHub', 'All')]
    [string] $App = 'All',
    [switch] $SkipPublish,
    [switch] $InstallPrerequisites,
    [switch] $BumpPatch
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptsDir = $PSScriptRoot

if ($BumpPatch) {
    & (Join-Path $scriptsDir 'bump-version.ps1') -Part patch
}

if ($InstallPrerequisites) {
    & (Join-Path $scriptsDir 'install-build-prerequisites.ps1')
}

if (-not $SkipPublish) {
    & (Join-Path $scriptsDir 'publish-installer.ps1') -App $App
}

function Find-IsccPath {
    $roots = @(
        $env:INNO_SETUP_ROOT
        "$env:LOCALAPPDATA\Programs\Inno Setup 6"
        "${env:ProgramFiles(x86)}\Inno Setup 6"
        "$env:ProgramFiles\Inno Setup 6"
    ) | Where-Object { $_ -and (Test-Path $_) }
    foreach ($r in $roots) {
        $p = Join-Path $r 'ISCC.exe'
        if (Test-Path $p) { return $p }
    }
    return $null
}

$iscc = Find-IsccPath
if (-not $iscc) {
    throw "Inno Setup 6 not found. Run scripts\install-build-prerequisites.ps1"
}

$appVersion = & (Join-Path $scriptsDir 'get-version.ps1')
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw "Could not read Version from Directory.Build.props."
}

& (Join-Path $scriptsDir 'write-version-inc.ps1') -Version $appVersion | Out-Null

$issMap = @{
    AppFolder = @{
        Iss      = 'LitboltAppFolder.iss'
        Prefix   = 'LitboltAppFolder-Setup-'
        Publish  = 'src\LitboltAppFolder\bin\Publish\win-x64\LitboltAppFolder.exe'
    }
    VideoHub = @{
        Iss      = 'LocalVideoHub.iss'
        Prefix   = 'LocalVideoHub-Setup-'
        Publish  = 'src\LocalVideoHub\bin\Publish\win-x64\LocalVideoHub.exe'
    }
}

$targets = if ($App -eq 'All') { @('AppFolder', 'VideoHub') } else { @($App) }
$issDir = Join-Path $repoRoot 'installer'
$built = @()

Push-Location $issDir
try {
    foreach ($key in $targets) {
        $meta = $issMap[$key]
        $exe = Join-Path $repoRoot $meta.Publish
        if (-not (Test-Path $exe)) {
            throw "Publish output missing: $exe - run without -SkipPublish."
        }

        $setupName = "$($meta.Prefix)$appVersion.exe"
        $outFile = Join-Path $issDir "Output\$setupName"
        Write-Host ""
        Write-Host "Building $setupName"

        & $iscc "/DAppVersion=$appVersion" ".\$($meta.Iss)"
        if ($LASTEXITCODE -ne 0) { throw "ISCC failed for $($meta.Iss) with exit code $LASTEXITCODE" }
        if (-not (Test-Path $outFile)) { throw "Expected installer was not created: $outFile" }

        $built += $outFile
        Write-Host "  OK: $outFile ($([math]::Round((Get-Item $outFile).Length / 1MB, 1)) MB)"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Done. Installer(s):"
foreach ($p in $built) { Write-Host "  $p" }
