# Copy repo rainmeter-skins\Litbolt to %USERPROFILE%\...\Rainmeter\Skins\Litbolt
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$src = Join-Path $repoRoot 'rainmeter-skins\Litbolt'
$dst = Join-Path $env:USERPROFILE 'Rainmeter\Skins\Litbolt'
if (-not (Test-Path $src)) {
    Write-Error "Source not found: $src"
}
New-Item -ItemType Directory -Force -Path $dst | Out-Null
robocopy $src $dst /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit $LASTEXITCODE" }

$mediaProj = Join-Path $repoRoot 'tools\LitboltMediaSession\LitboltMediaSession.csproj'
$mediaOut = Join-Path $dst '@Resources\LitboltMedia'
if (Test-Path $mediaProj) {
    dotnet publish $mediaProj -c Release -o $mediaOut | Out-Host
    Write-Host "Published LitboltMediaSession to $mediaOut"
}

$mediaWatchScript = Join-Path $dst '@Resources\LitboltMedia\ensure-media-watch.ps1'
if (Test-Path $mediaWatchScript) {
    $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $action = New-ScheduledTaskAction -Execute $ps -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$mediaWatchScript`""
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $trigger.Delay = 'PT1M'
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 2)
    Register-ScheduledTask -TaskName 'Litbolt\MediaWatchBootstrap' -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
    Write-Host "Registered logon task Litbolt\MediaWatchBootstrap (starts media watch if Rainmeter is running)."
}

Write-Host "Installed Litbolt skins to $dst"
Write-Host "Skins are not activated. Enable in Rainmeter Manage or Photo Hub Widget tab."
