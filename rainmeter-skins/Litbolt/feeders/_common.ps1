function Set-FeederPriority {
    try { $p = Get-Process -Id $PID; $p.PriorityClass = 'BelowNormal' } catch { }
}

function Escape-RmValue([string]$s) {
    if ($null -eq $s) { return '' }
    return ($s -replace '"', '').Replace("`r", ' ').Replace("`n", ' ').Trim()
}

function Write-RainmeterInc {
    param(
        [string]$Path,
        [hashtable]$Vars
    )
    $dir = Split-Path $Path -Parent
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('[Variables]')
    foreach ($key in ($Vars.Keys | Sort-Object)) {
        $lines.Add("$key=$(Escape-RmValue $Vars[$key])")
    }
    [System.IO.File]::WriteAllText($Path, ($lines -join "`r`n") + "`r`n", [System.Text.UTF8Encoding]::new($false))
}

function Get-LitboltRoot {
    $here = Split-Path -Parent $MyInvocation.PSCommandPath
    return Split-Path $here -Parent
}
