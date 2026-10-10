# Writes Litbolt Stats\@Resources\data.inc — CurseForge downloads + Etsy placeholder
param()
Set-Location $PSScriptRoot
. "$PSScriptRoot\_common.ps1"
Set-FeederPriority

$root = Get-LitboltRoot
$res = Join-Path $root 'Litbolt Stats\@Resources'
$configPath = Join-Path $res 'stats-config.json'
$historyPath = Join-Path $res 'stats-history.json'
$outPath = Join-Path $res 'data.inc'

$vars = @{
    FlashlightDownloads = '—'
    FlashlightDelta     = '—'
    MapleDownloads      = '—'
    MapleDelta          = '—'
    EtsyLine            = 'Set up Etsy'
    StatsOk             = '0'
    StatsMessage        = ''
    StatsUpdated        = (Get-Date).ToString('yyyy-MM-dd HH:mm')
}

function Get-CurseForgeDownloads($mod, [string]$apiKey) {
    $slug = $mod.Slug
    $gameId = if ($mod.GameId) { [int]$mod.GameId } else { 432 }
    if ($apiKey) {
        $headers = @{ 'x-api-key' = $apiKey; Accept = 'application/json' }
        $search = Invoke-RestMethod -Uri "https://api.curseforge.com/v1/mods/search?gameId=$gameId&slug=$slug" -Headers $headers -TimeoutSec 60
        if ($search.data -and $search.data.Count -gt 0) {
            return [int64]$search.data[0].downloadCount
        }
    }
    $url = "https://www.curseforge.com/minecraft/mc-mods/$slug"
    $html = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60).Content
    if ($html -match '([\d,]+)\s+Downloads') {
        return [int64]($Matches[1] -replace ',', '')
    }
    if ($html -match '"downloadCount"\s*:\s*(\d+)') {
        return [int64]$Matches[1]
    }
    throw "Could not parse download count for $slug"
}

function Format-Delta([int64]$current, [int64]$previous) {
    if ($null -eq $previous) { return 'new' }
    $d = $current - $previous
    if ($d -eq 0) { return '±0' }
    if ($d -gt 0) { return "+$d" }
    return "$d"
}

try {
    if (-not (Test-Path $configPath)) { throw "Missing stats-config.json" }
    $cfg = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $key = "$($cfg.CurseForgeApiKey)".Trim()
    $history = @{}
    if (Test-Path $historyPath) {
        $raw = Get-Content $historyPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($raw) {
            $raw.PSObject.Properties | ForEach-Object { $history[$_.Name] = @{ date = $_.Value.date; count = [int64]$_.Value.count } }
        }
    }
    $today = (Get-Date).ToString('yyyy-MM-dd')
    $mods = @($cfg.Mods)
    if ($mods.Count -ge 1) {
        $n = Get-CurseForgeDownloads $mods[0] $key
        $prev = $null
        $slug0 = [string]$mods[0].Slug
        if ($history.ContainsKey($slug0) -and $history[$slug0].date -ne $today) {
            $prev = [int64]$history[$slug0].count
        }
        $vars.FlashlightDownloads = "{0:N0}" -f $n
        $vars.FlashlightDelta = Format-Delta $n $prev
        $history[$slug0] = @{ date = $today; count = $n }
    }
    if ($mods.Count -ge 2) {
        $n = Get-CurseForgeDownloads $mods[1] $key
        $prev = $null
        $slug1 = [string]$mods[1].Slug
        if ($history.ContainsKey($slug1) -and $history[$slug1].date -ne $today) {
            $prev = [int64]$history[$slug1].count
        }
        $vars.MapleDownloads = "{0:N0}" -f $n
        $vars.MapleDelta = Format-Delta $n $prev
        $history[$slug1] = @{ date = $today; count = $n }
    }
    $history | ConvertTo-Json -Depth 4 | Set-Content $historyPath -Encoding UTF8

    if ($cfg.Etsy -and $cfg.Etsy.Configured -eq $true) {
        $vars.EtsyLine = "$($cfg.Etsy.ShopSalesPlaceholder)"
    } else {
        $vars.EtsyLine = 'Set up Etsy — edit stats-config.json'
    }
    $vars.StatsOk = '1'
} catch {
    $vars.StatsMessage = $_.Exception.Message
}

Write-RainmeterInc -Path $outPath -Vars $vars
