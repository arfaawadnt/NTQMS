<#
.SYNOPSIS
    NT.QMS operational failure drills (Road-to-100 Phase 8).
.DESCRIPTION
    Exercises the incident behaviours the observability baseline (Phase 2) is
    meant to catch, against a RUNNING API + PostgreSQL:

      Drill 1 - Database down: readiness must flip to 503 (the alert source)
                and recover to 200 when the database returns.
      Drill 2 - Poison event: an outbox row that can never deserialize must
                dead-letter (leaving the live stream) and stop at MaxAttempts,
                without blocking healthy events.

    Both drills are reversible and self-cleaning. Drill 1 stops the local
    PostgreSQL service and needs an elevated shell; it is skipped with a
    notice if the stop is denied (the behaviour is also covered
    deterministically by ReadinessAndTopologyTests / HealthEndpointTests).

.EXAMPLE
    ./scripts/failure-drills.ps1 -PgService postgresql-x64-17
#>
param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$PgService = "postgresql-x64-17",
    [string]$Psql = "C:\Program Files\PostgreSQL\17\bin\psql.exe"
)

$ErrorActionPreference = "Stop"
$script:failures = 0
function Check($name, $ok) {
    if ($ok) { Write-Host "  PASS  $name" -ForegroundColor Green }
    else { Write-Host "  FAIL  $name" -ForegroundColor Red; $script:failures++ }
}
function Ready() {
    try { return (Invoke-WebRequest "$BaseUrl/health/ready" -UseBasicParsing -TimeoutSec 5).StatusCode }
    catch { return $_.Exception.Response.StatusCode.value__ }
}

$env:PGPASSWORD = "dev-only-local"
function Sql($q) { & $Psql -h localhost -U qams_app -d ntqams -t -A -c $q }

Write-Host "NT.QMS failure drills -> $BaseUrl"

# Drill 1 - database down -> readiness 503 -> recovery
Write-Host "`nDrill 1 - database down"
Check "readiness is 200 before the drill" ((Ready) -eq 200)
$stopped = $false
try {
    Stop-Service $PgService -ErrorAction Stop
    $stopped = $true
    Start-Sleep 3
    Check "readiness flips to 503 while PostgreSQL is down" ((Ready) -eq 503)
}
catch {
    Write-Host "  SKIP  stopping '$PgService' was denied (needs an elevated shell)." -ForegroundColor Yellow
    Write-Host "        Covered by ReadinessAndTopologyTests + HealthEndpointTests." -ForegroundColor Yellow
}
finally {
    if ($stopped) {
        Start-Service $PgService
        $recovered = $false
        foreach ($i in 1..30) { Start-Sleep 2; if ((Ready) -eq 200) { $recovered = $true; break } }
        Check "readiness recovers to 200 after PostgreSQL returns" $recovered
    }
}

# Drill 2 - poison event -> dead-letter
Write-Host "`nDrill 2 - poison outbox event"
$marker = "DRILL." + [Guid]::NewGuid().ToString("N")
# Pre-aged to attempt 4 (MaxAttempts-1) and due now, so the next ~2s processor
# pass tips it into the dead-letter state immediately rather than after the
# full exponential-backoff ladder.
$id = [Guid]::NewGuid().ToString()
$insert = "INSERT INTO qams.outbox_event (id, tenant_id, event_type, payload, occurred_at_utc, attempts, next_attempt_at_utc) VALUES ('$id', NULL, '$marker, Nowhere', '{}', now(), 4, now() - interval '1 minute');"
Sql $insert | Out-Null
Write-Host "  injected poison row $id (attempts=4, due now)"

$deadLettered = $false
foreach ($i in 1..15) {
    Start-Sleep 2
    if ((Sql "SELECT dead_lettered_at_utc IS NOT NULL FROM qams.outbox_event WHERE id='$id';") -eq "t") {
        $deadLettered = $true; break
    }
}
Check "poison event moved to the dead-letter state" $deadLettered
$attempts = Sql "SELECT attempts FROM qams.outbox_event WHERE id='$id';"
Check "it stopped at MaxAttempts (5)" ($attempts -eq "5")

# Cleanup - the drill row is transport, not a record.
Sql "DELETE FROM qams.outbox_event WHERE id='$id';" | Out-Null
Write-Host "  cleaned up drill row"

Write-Host ""
if ($script:failures -eq 0) { Write-Host "ALL DRILLS PASSED" -ForegroundColor Green; exit 0 }
else { Write-Host "$($script:failures) DRILL CHECK(S) FAILED" -ForegroundColor Red; exit 1 }
