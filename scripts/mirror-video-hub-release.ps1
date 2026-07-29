# Upload LocalVideoHub-Setup-<version>.exe to Litbolt123/Local-Video-Hub (standalone updater repo).
param(
    [string]$Version = "",
    [string]$InstallerPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = & (Join-Path $PSScriptRoot "get-version.ps1")
}

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Join-Path $repoRoot "installer\Output\LocalVideoHub-Setup-$Version.exe"
}

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "Installer not found: $InstallerPath. Run build-installer.ps1 first."
}

$tag = "v$Version"
$assetName = [IO.Path]::GetFileName($InstallerPath)
$releaseRepo = "Litbolt123/Local-Video-Hub"

Write-Host "Mirroring $assetName to $releaseRepo release $tag ..."

$existing = gh release view $tag --repo $releaseRepo 2>$null
if ($LASTEXITCODE -ne 0) {
    $body = "Local Video Hub $Version Windows installer (mirrored from Litbolt-App-Folder monorepo)."
    gh release create $tag $InstallerPath --repo $releaseRepo --title "Local Video Hub $Version" --notes $body
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
}
else {
    gh release upload $tag $InstallerPath --repo $releaseRepo --clobber
    if ($LASTEXITCODE -ne 0) { throw "gh release upload failed" }
}

Write-Host "OK: $releaseRepo $tag -> $assetName"
