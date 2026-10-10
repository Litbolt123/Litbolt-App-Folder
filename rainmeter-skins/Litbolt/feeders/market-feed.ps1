# Writes Market\@Resources\data.inc — MU, SNDK, BTC-USD (Yahoo chart API)
param()
Set-Location $PSScriptRoot
. "$PSScriptRoot\_common.ps1"
Set-FeederPriority

$root = Get-LitboltRoot
$outPath = Join-Path $root 'Market\@Resources\data.inc'

$green = '96,200,130'
$red = '232,90,90'
$muted = '136,144,160'

$vars = @{
    MuPrice        = '—'
    MuChange       = ''
    MuColor        = $muted
    SndkPrice      = '—'
    SndkChange     = ''
    SndkColor      = $muted
    BtcPrice       = '—'
    BtcChange      = ''
    BtcColor       = $muted
    MarketOk       = '0'
    MarketMessage  = ''
    MarketUpdated  = (Get-Date).ToString('HH:mm')
}

function Get-Quote([string]$symbol) {
    $enc = [uri]::EscapeDataString($symbol)
    $url = "https://query1.finance.yahoo.com/v8/finance/chart/${enc}?interval=1d&range=1d"
    $json = Invoke-RestMethod -Uri $url -TimeoutSec 45
    $meta = $json.chart.result[0].meta
    $price = [double]$meta.regularMarketPrice
    $prev = [double]$meta.chartPreviousClose
    if ($prev -eq 0) { $prev = [double]$meta.previousClose }
    $pct = if ($prev -ne 0) { (($price - $prev) / $prev) * 100.0 } else { 0.0 }
    return @{ Price = $price; Pct = $pct }
}

function Format-Row($q, [bool]$isBtc) {
    $priceStr = if ($isBtc) { '{0:N0}' -f $q.Price } else { '{0:N2}' -f $q.Price }
    $pctStr = '{0:+0.00;-0.00;0.00}%' -f $q.Pct
    $color = if ($q.Pct -gt 0) { $green } elseif ($q.Pct -lt 0) { $red } else { $muted }
    return @{ Price = $priceStr; Change = $pctStr; Color = $color }
}

try {
    $mu = Format-Row (Get-Quote 'MU') $false
    $vars.MuPrice = $mu.Price
    $vars.MuChange = $mu.Change
    $vars.MuColor = $mu.Color

    $sndk = Format-Row (Get-Quote 'SNDK') $false
    $vars.SndkPrice = $sndk.Price
    $vars.SndkChange = $sndk.Change
    $vars.SndkColor = $sndk.Color

    $btc = Format-Row (Get-Quote 'BTC-USD') $true
    $vars.BtcPrice = $btc.Price
    $vars.BtcChange = $btc.Change
    $vars.BtcColor = $btc.Color

    $vars.MarketOk = '1'
} catch {
    $vars.MarketMessage = $_.Exception.Message
}

Write-RainmeterInc -Path $outPath -Vars $vars
