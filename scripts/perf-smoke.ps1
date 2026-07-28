<#
.SYNOPSIS
    NT.QMS baseline performance smoke (EA remediation Phase 6, TEST-003).
.DESCRIPTION
    Measures p50/p95 latency for the three canonical request classes against a
    RUNNING instance: readiness probe, login (credential path incl. hashing),
    and an authenticated paged list. Exits non-zero when the list p95 exceeds
    the threshold — a coarse regression tripwire, not a load test. Run it
    against a production-like host before sign-off and record the numbers.
.EXAMPLE
    ./scripts/perf-smoke.ps1 -BaseUrl http://localhost:5080 `
        -Tenant demo-lab -Email admin@demo-lab.local -Password 'Demo-Admin-Pass-2!'
#>
param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$Tenant = "demo-lab",
    [string]$Email = "admin@demo-lab.local",
    [Parameter(Mandatory = $true)][string]$Password,
    [int]$Requests = 50,
    [int]$P95ThresholdMs = 800
)

$ErrorActionPreference = "Stop"

function Measure-Endpoint([string]$Name, [scriptblock]$Call, [int]$Count) {
    $samples = @()
    for ($i = 0; $i -lt $Count; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        & $Call | Out-Null
        $sw.Stop()
        $samples += $sw.Elapsed.TotalMilliseconds
    }
    $sorted = $samples | Sort-Object
    $p50 = $sorted[[int][Math]::Floor(0.50 * ($sorted.Count - 1))]
    $p95 = $sorted[[int][Math]::Floor(0.95 * ($sorted.Count - 1))]
    # Write-Host, NOT Write-Output: the report line must not leak into the
    # function's return pipeline (the caller consumes the p95 number).
    Write-Host ("{0,-28} n={1,4}  p50={2,7:0.0}ms  p95={3,7:0.0}ms" -f $Name, $Count, $p50, $p95)
    return $p95
}

Write-Output "NT.QMS perf smoke against $BaseUrl ($Requests requests per class)"

$null = Measure-Endpoint "GET /health/ready" {
    Invoke-WebRequest "$BaseUrl/health/ready" -UseBasicParsing -TimeoutSec 15
} $Requests

# Login is deliberately expensive (password hashing) and rate-limited — a
# handful of samples is the honest measurement.
$loginBody = @{ tenantIdentifier = $Tenant; email = $Email; password = $Password } | ConvertTo-Json
$null = Measure-Endpoint "POST /api/auth/login" {
    Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
} ([Math]::Min(5, $Requests))

$token = (Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json").accessToken
$headers = @{ Authorization = "Bearer $token" }
$listP95 = Measure-Endpoint "GET /api/nonconformances" {
    Invoke-RestMethod "$BaseUrl/api/nonconformances?page=1&pageSize=50" -Headers $headers
} $Requests

if ($listP95 -gt $P95ThresholdMs) {
    Write-Error "FAIL: list p95 ${listP95}ms exceeds threshold ${P95ThresholdMs}ms"
    exit 1
}
Write-Output "PASS: list p95 within ${P95ThresholdMs}ms threshold"
