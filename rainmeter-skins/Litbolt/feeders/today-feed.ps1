# Writes Today\@Resources\data.inc - calendar (iCal) + countdowns.txt
param()
Set-Location $PSScriptRoot
. "$PSScriptRoot\_common.ps1"
Set-FeederPriority

$root = Get-LitboltRoot
$res = Join-Path $root 'Today\@Resources'
$configPath = Join-Path $res 'calendar-config.json'
$countdownPath = Join-Path $res 'countdowns.txt'
$outPath = Join-Path $res 'data.inc'

$vars = @{
    TodayDate     = (Get-Date).ToString('dddd, MMMM d, yyyy')
    Event1        = ''
    Event2        = ''
    Event3        = ''
    Countdown1    = ''
    Countdown2    = ''
    Countdown3    = ''
    DataOk        = '0'
    DataMessage   = ''
}

function Get-CountdownLabel([datetime]$target, [string]$name) {
    $now = Get-Date
    if ($target -lt $now) { return "$name - passed" }
    $span = $target - $now
    if ($span.TotalDays -ge 1) {
        $days = [int][math]::Floor($span.TotalDays)
        return "$name - in $days day$(if ($days -ne 1) { 's' })"
    }
    if ($span.TotalHours -ge 1) {
        $hrs = [int][math]::Floor($span.TotalHours)
        return "$name - in $hrs hour$(if ($hrs -ne 1) { 's' })"
    }
    $mins = [int][math]::Max(1, [math]::Floor($span.TotalMinutes))
    return "$name - in $mins min"
}

# Countdowns
$cdLines = @()
if (Test-Path $countdownPath) {
    foreach ($line in Get-Content $countdownPath -Encoding UTF8) {
        $t = $line.Trim()
        if (-not $t -or $t.StartsWith('#')) { continue }
        $parts = $t -split '\|', 2
        if ($parts.Count -lt 2) { continue }
        $name = $parts[0].Trim()
        $when = $parts[1].Trim()
        try {
            $dt = [datetime]::Parse($when, [System.Globalization.CultureInfo]::InvariantCulture)
            $cdLines += Get-CountdownLabel $dt $name
        } catch {
            $cdLines += "$name - bad date"
        }
    }
}
if ($cdLines.Count -gt 0) { $vars.Countdown1 = $cdLines[0] }
if ($cdLines.Count -gt 1) { $vars.Countdown2 = $cdLines[1] }
if ($cdLines.Count -gt 2) { $vars.Countdown3 = $cdLines[2] }

# Calendar
try {
    if (-not (Test-Path $configPath)) { throw "Missing calendar-config.json" }
    $cfg = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $url = "$($cfg.ICalUrl)".Trim()
    if ($url -match 'PASTE_YOUR') { throw "Set ICalUrl in calendar-config.json (Google Calendar → Settings → your calendar → Secret address in iCal format)." }
    $ics = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60).Content
    $events = @()
    $blocks = [regex]::Split($ics, 'BEGIN:VEVENT')
    foreach ($block in $blocks) {
        if ($block -notmatch 'SUMMARY:') { continue }
        $sum = if ($block -match '(?m)^SUMMARY:(.+)$') { $Matches[1].Trim() } else { 'Event' }
        $startRaw = if ($block -match '(?m)^DTSTART[^:]*:(.+)$') { $Matches[1].Trim() } else { $null }
        if (-not $startRaw) { continue }
        $start = $null
        if ($startRaw -match '^\d{8}T\d{6}') {
            $start = [datetime]::ParseExact($startRaw.Substring(0, 15), 'yyyyMMddTHHmmss', $null)
        } elseif ($startRaw -match '^\d{8}$') {
            $start = [datetime]::ParseExact($startRaw.Substring(0, 8), 'yyyyMMdd', $null)
        }
        if ($start -and $start -ge (Get-Date).Date) {
            $events += [pscustomobject]@{ Start = $start; Summary = $sum }
        }
    }
    $events = $events | Sort-Object Start | Select-Object -First 3
    $i = 0
    foreach ($ev in $events) {
        $i++
        $label = "{0:ddd h:mm tt} - {1}" -f $ev.Start, $ev.Summary
        $vars["Event$i"] = $label
    }
    $vars.DataOk = '1'
} catch {
    $vars.DataMessage = $_.Exception.Message
}

Write-RainmeterInc -Path $outPath -Vars $vars
