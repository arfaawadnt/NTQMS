<#
.SYNOPSIS
    Reports whether the NT.QMS dev stack is up, and what owns each port.

.DESCRIPTION
    First thing to run when "the app is not working": it distinguishes the three
    cases that look identical in the browser - server down (ERR_CONNECTION_REFUSED),
    server up but database unreachable (readiness 503), and server up and healthy
    (so the problem is elsewhere, e.g. credentials).
#>
[CmdletBinding()]
param()

function Show-Port([int]$Port, [string]$Label) {
    $c = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue | Where-Object { $_.State -eq 'Listen' } | Select-Object -First 1
    if (-not $c) {
        Write-Host ("  {0,-6} :{1,-5} DOWN" -f $Label, $Port) -ForegroundColor Red
        return $false
    }
    $procId = $c.OwningProcess
    $p      = Get-Process -Id $procId -ErrorAction SilentlyContinue
    $parent = (Get-CimInstance Win32_Process -Filter "ProcessId=$procId" -ErrorAction SilentlyContinue)
    $pname  = if ($parent) { (Get-CimInstance Win32_Process -Filter "ProcessId=$($parent.ParentProcessId)" -ErrorAction SilentlyContinue).Name } else { 'unknown' }
    Write-Host ("  {0,-6} :{1,-5} UP   pid {2} ({3}) parent {4}" -f $Label, $Port, $procId, $p.ProcessName, $pname) -ForegroundColor Green
    return $true
}

Write-Host "NT.QMS dev stack status" -ForegroundColor Cyan
$apiUp = Show-Port 5080 'API'
$feUp  = Show-Port 4200 'SPA'

if ($apiUp) {
    foreach ($probe in @(@{ Path = '/health/live'; Name = 'liveness ' }, @{ Path = '/health/ready'; Name = 'readiness' })) {
        try {
            $r = Invoke-WebRequest -Uri ("http://localhost:5080" + $probe.Path) -TimeoutSec 6 -UseBasicParsing
            Write-Host ("  {0}      {1} {2}" -f $probe.Name, $r.StatusCode, $r.Content) -ForegroundColor Green
        } catch {
            $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'no answer' }
            $hint = if ($code -eq 503) { '  <- API is up but PostgreSQL is not reachable' } else { '' }
            Write-Host ("  {0}      {1}{2}" -f $probe.Name, $code, $hint) -ForegroundColor Yellow
        }
    }
}

# PostgreSQL is the dependency most likely to be the real cause.
$pg = Get-Service -Name 'postgresql*' -ErrorAction SilentlyContinue
# Note: PowerShell 5.1 rejects "(if ...)" as an argument value at RUNTIME (it
# parses, then fails with "the term 'if' is not recognized"), so the colour is
# resolved into a variable first. Same reason ternaries are avoided throughout.
if ($pg) {
    foreach ($s in $pg) {
        $pgColour = 'Red'
        if ($s.Status -eq 'Running') { $pgColour = 'Green' }
        Write-Host ("  PG     {0,-11} {1}" -f $s.Name, $s.Status) -ForegroundColor $pgColour
    }
}

Write-Host ""
if ($apiUp -and $feUp)      { Write-Host "Both up. Open http://localhost:4200/t/demo-lab" -ForegroundColor Cyan }
elseif (-not $apiUp -and -not $feUp) { Write-Host "Both down. Run: scripts\dev-up.ps1" -ForegroundColor Yellow }
else                        { Write-Host "Partially up. Run: scripts\dev-up.ps1 (it starts only what is missing)" -ForegroundColor Yellow }

$logDir = Join-Path $env:TEMP 'ntqms-dev'
if (Test-Path $logDir) { Write-Host "Logs: $logDir" -ForegroundColor DarkGray }
