# Start LitboltMediaSession watch if Rainmeter is running and watch is not already alive.
$ErrorActionPreference = 'SilentlyContinue'
$mediaDir = $PSScriptRoot
$exe = Join-Path $mediaDir 'LitboltMediaSession.exe'
if (-not (Test-Path $exe)) { exit 0 }
if (-not (Get-Process -Name Rainmeter -ErrorAction SilentlyContinue)) { exit 0 }
& $exe watch | Out-Null
exit 0
