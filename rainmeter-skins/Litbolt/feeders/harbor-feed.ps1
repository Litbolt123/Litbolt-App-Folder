# Writes Harbor\@Resources\data.inc from Harbor rainmeter-status.json (packs folder fallback)
param()
Set-Location $PSScriptRoot
. "$PSScriptRoot\_common.ps1"
Set-FeederPriority

$root = Get-LitboltRoot
$outPath = Join-Path $root 'Harbor\@Resources\data.inc'
$statusPath = Join-Path $env:LOCALAPPDATA 'Harbor\rainmeter-status.json'
$packsDir = Join-Path $env:LOCALAPPDATA 'Harbor\packs'
$exe = Join-Path $env:LOCALAPPDATA 'Programs\Harbor\Harbor.exe'

# Y math matches @Resources\Compact.inc (CBodyY, CRowH, CPad, COpenH=20)
$cBodyY = 38
$cRowH = 22
$cPad = 10
$cOpenH = 20
$vars = @{
    HarborExe    = $exe
    OpenY        = "$cBodyY"
    SkinH        = '80'
    SuggestShow  = '0'
    SuggestId    = ''
    SuggestLabel = ''
    SuggestY     = "$cBodyY"
}
for ($i = 1; $i -le 6; $i++) {
    $vars["Pack${i}Show"] = '0'
    $vars["Pack${i}Id"] = ''
    $vars["Pack${i}Label"] = ''
    $vars["Pack${i}Meta"] = ''
    $vars["Pack${i}Y"] = '0'
    $vars["Pack${i}Color"] = '#ColorText#'
    $vars["Pack${i}Stroke"] = '#ColorBorder#'
}

# Rainmeter treats # as a variable delimiter.
function Get-RmText([string]$s) {
    if ([string]::IsNullOrWhiteSpace($s)) { return '' }
    return ($s -replace '#', '').Trim()
}

function Format-Apps([int]$count) {
    if ($count -lt 0) { $count = 0 }
    if ($count -eq 1) { return '1 app' }
    return "$count apps"
}

function Get-PackAppCount($pack) {
    $apps = $pack.apps
    if ($null -eq $apps) { return 0 }
    return @($apps).Count
}

$rows = @()
$fromStatus = $false
if (Test-Path -LiteralPath $statusPath) {
    try {
        $data = Get-Content -LiteralPath $statusPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $fromStatus = $true
        $last = Get-RmText ([string]$data.lastLaunchedPack)
        foreach ($p in @($data.packs)) {
            if ($null -eq $p) { continue }
            $id = Get-RmText ([string]$p.id)
            if (-not $id) { continue }
            $label = Get-RmText ([string]$p.label)
            if (-not $label) { $label = $id }
            $count = 0
            try { $count = [int]$p.appCount } catch { $count = 0 }
            $rows += [pscustomobject]@{ Id = $id; Label = $label; Count = $count; Last = ($id -eq $last) }
        }
        if ($last) {
            $first = @($rows | Where-Object { $_.Last })
            $rest = @($rows | Where-Object { -not $_.Last })
            $rows = @($first + $rest)
        }
        $sp = $data.suggestedPack
        if ($null -ne $sp) {
            $sid = Get-RmText ([string]$sp.id)
            $slab = Get-RmText ([string]$sp.label)
            if ($sid -and $slab) {
                $vars.SuggestShow = '1'
                $vars.SuggestId = $sid
                $vars.SuggestLabel = $slab
            }
        }
    } catch {
        $fromStatus = $false
        $rows = @()
    }
}

if (-not $fromStatus -and (Test-Path -LiteralPath $packsDir)) {
    $found = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $packsDir -Filter '*.json' -File)) {
        try {
            $p = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {
            continue
        }
        $id = Get-RmText ([string]$p.id)
        if (-not $id) { $id = [System.IO.Path]::GetFileNameWithoutExtension($file.Name) }
        $label = Get-RmText ([string]$p.label)
        if (-not $label) { $label = $id }
        $found += [pscustomobject]@{ Id = $id; Label = $label; Count = (Get-PackAppCount $p); Last = $false }
    }
    $rows = @($found | Sort-Object Label)
}

$shown = @($rows | Select-Object -First 6)
$packStartY = $cBodyY
if ($vars.SuggestShow -eq '1') { $packStartY = $cBodyY + $cRowH }
$y = $packStartY
for ($i = 0; $i -lt $shown.Count; $i++) {
    $n = $i + 1
    $row = $shown[$i]
    $vars["Pack${n}Show"] = '1'
    $vars["Pack${n}Id"] = [string]$row.Id
    $vars["Pack${n}Label"] = [string]$row.Label
    $vars["Pack${n}Meta"] = Format-Apps ([int]$row.Count)
    $vars["Pack${n}Y"] = "$y"
    if ($row.Last) {
        $vars["Pack${n}Color"] = '#ColorGold#'
        $vars["Pack${n}Stroke"] = '#ColorGold#'
    }
    $y += $cRowH
}
$vars.OpenY = "$y"
$vars.SkinH = "$($y + $cOpenH + $cPad)"

Write-RainmeterInc -Path $outPath -Vars $vars
