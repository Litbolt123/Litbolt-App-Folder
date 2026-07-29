#Requires -Version 5.1
<#
.SYNOPSIS
  Self-contained win-x64 publish for installer builds (includes LibVLC native libs).

.PARAMETER App
  AppFolder, VideoHub, or All (default).
#>
param(
    [ValidateSet('AppFolder', 'VideoHub', 'All')]
    [string] $App = 'All'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$shortcutScript = Join-Path $PSScriptRoot 'update-windows-shortcuts.ps1'

$definitions = @{
    AppFolder = @{
        Project       = 'src\LitboltAppFolder\LitboltAppFolder.csproj'
        PublishDir    = 'src\LitboltAppFolder\bin\Publish\win-x64'
        ExeName       = 'LitboltAppFolder.exe'
        AppName       = "Litbolt's App Folder"
        ProgramsFolder = 'LitboltAppFolder'
    }
    VideoHub = @{
        Project       = 'src\LocalVideoHub\LocalVideoHub.csproj'
        PublishDir    = 'src\LocalVideoHub\bin\Publish\win-x64'
        ExeName       = 'LocalVideoHub.exe'
        AppName       = 'Local Video Hub'
        ProgramsFolder = 'LocalVideoHub'
    }
}

$targets = if ($App -eq 'All') { @('AppFolder', 'VideoHub') } else { @($App) }

foreach ($key in $targets) {
    $def = $definitions[$key]
    $project = Join-Path $repoRoot $def.Project
    $publishDir = Join-Path $repoRoot $def.PublishDir

    if (Test-Path $publishDir) {
        Write-Host "Cleaning $publishDir"
        Remove-Item $publishDir -Recurse -Force
    }

    Write-Host "Publishing $key -> $publishDir"
    & dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishReadyToRun=true `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $key with exit code $LASTEXITCODE" }

    if ($env:GITHUB_ACTIONS -ne 'true') {
        & $shortcutScript `
            -SourceDir $publishDir `
            -AppName $def.AppName `
            -ExeName $def.ExeName `
            -ProgramsFolder $def.ProgramsFolder `
            -SkipProcessStop
    }
}

$ver = & (Join-Path $PSScriptRoot 'get-version.ps1')
Write-Host "Published $App (Version $ver)"
