# Runs market-feed.ps1 every 5 min during 09:00–16:59 local; otherwise at most every 30 min.
. "$PSScriptRoot\_common.ps1"
Set-FeederPriority

$stamp = Join-Path $PSScriptRoot '.market-last-run'
$now = Get-Date
$hour = $now.Hour
$inSession = ($hour -ge 9 -and $hour -lt 17)

$shouldRun = $inSession
if (-not $shouldRun -and (Test-Path $stamp)) {
    $last = [datetime]::Parse((Get-Content $stamp -Raw).Trim())
    if (($now - $last).TotalMinutes -ge 30) { $shouldRun = $true }
}
elseif (-not $shouldRun) {
    $shouldRun = $true
}

if ($shouldRun) {
    & "$PSScriptRoot\market-feed.ps1"
    $now.ToString('o') | Set-Content $stamp -Encoding UTF8
}
