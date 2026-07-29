<#
.SYNOPSIS
    Stops the NT.QMS dev stack by port owner - never a blanket process kill.

.DESCRIPTION
    Deliberately targets whatever is listening on :5080 and :4200 instead of
    'taskkill /IM dotnet.exe', which also kills unrelated .NET tooling (that blunt
    kill is part of why the dev stack kept disappearing).
#>
[CmdletBinding()]
param(
    # Stop the API only, leaving the SPA running.
    [switch]$ApiOnly
)

function Stop-Port([int]$Port, [string]$Label) {
    $c = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $c) { Write-Host ("  {0,-6} :{1,-5} already down" -f $Label, $Port) -ForegroundColor DarkGray; return }

    $ids = $c | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($procId in $ids) {
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if (-not $p) { continue }
        # The Angular CLI runs under npm.cmd, so kill the tree, not just the leaf.
        try { taskkill /PID $procId /T /F | Out-Null } catch { }
        Write-Host ("  {0,-6} :{1,-5} stopped (pid {2}, {3})" -f $Label, $Port, $procId, $p.ProcessName) -ForegroundColor Yellow
    }
}

Write-Host "Stopping NT.QMS dev stack" -ForegroundColor Cyan
Stop-Port 5080 'API'
if (-not $ApiOnly) { Stop-Port 4200 'SPA' } else { Write-Host "  SPA    :4200  left running (-ApiOnly)" -ForegroundColor DarkGray }
Write-Host "`nBring it back with: scripts\dev-up.ps1"
