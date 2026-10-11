# Registers hidden BelowNormal feeder tasks for Litbolt Rainmeter widgets (run as current user).
#Requires -RunAsAdministrator
param(
    [switch]$Unregister
)

$feeders = Join-Path $PSScriptRoot ''
$ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Register-FeedTask {
    param(
        [string]$Name,
        [string]$Script,
        [int]$Minutes
    )
    $action = New-ScheduledTaskAction -Execute $ps -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$Script`""
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes $Minutes) -RepetitionDuration ([TimeSpan]::MaxValue)
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
    Register-ScheduledTask -TaskName $Name -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
    Write-Host "Registered $Name every $Minutes min"
}

function Unregister-FeedTask([string]$Name) {
    Unregister-ScheduledTask -TaskName $Name -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Removed $Name"
}

function Register-LogonTask {
    param(
        [string]$Name,
        [string]$Script
    )
    $action = New-ScheduledTaskAction -Execute $ps -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$Script`""
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $trigger.Delay = 'PT1M'
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 2)
    Register-ScheduledTask -TaskName $Name -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
    Write-Host "Registered $Name at logon (+1 min delay)"
}

$tasks = @(
    @{ Name = 'Litbolt\TodayFeed'; Script = Join-Path $feeders 'today-feed.ps1'; Minutes = 15 }
    @{ Name = 'Litbolt\StatsFeed'; Script = Join-Path $feeders 'stats-feed.ps1'; Minutes = 180 }
    @{ Name = 'Litbolt\MarketFeed'; Script = Join-Path $feeders 'market-feed-gated.ps1'; Minutes = 5 }
)

$litboltRoot = Split-Path $feeders -Parent
$mediaWatchScript = Join-Path $litboltRoot '@Resources\LitboltMedia\ensure-media-watch.ps1'

if ($Unregister) {
    foreach ($t in $tasks) { Unregister-FeedTask $t.Name }
    Unregister-FeedTask 'Litbolt\MediaWatchBootstrap'
    exit 0
}

foreach ($t in $tasks) {
    Register-FeedTask -Name $t.Name -Script $t.Script -Minutes $t.Minutes
}

if (Test-Path $mediaWatchScript) {
    Register-LogonTask -Name 'Litbolt\MediaWatchBootstrap' -Script $mediaWatchScript
}

Write-Host ""
Write-Host "MarketFeed uses market-feed-gated.ps1 (5 min 09:00-16:59 local, else 30 min)."
Write-Host "Run feeders once now, e.g.:"
Write-Host "  & '$($tasks[0].Script)'"
