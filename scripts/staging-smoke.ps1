<#
.SYNOPSIS
    NT.QMS staging smoke gate (Road-to-100 external-track wiring).
.DESCRIPTION
    One command to validate a freshly stood-up staging (or any deployed)
    instance: readiness, TLS/HSTS presence, then the fast + deep security
    probes against the remote URL. Exits non-zero on any failure so it can gate
    a deploy pipeline. Point it at the staging origin, not localhost.
.EXAMPLE
    ./scripts/staging-smoke.ps1 -BaseUrl https://staging.ntqms.example `
        -Tenant demo-lab -Email admin@demo-lab.local -Password '...'
#>
param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [string]$Tenant = "demo-lab",
    [string]$Email = "admin@demo-lab.local",
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$PlatformEmail = "platform-admin@localhost",
    [string]$PlatformPassword = "Dev-Only-Platform-Pass-1!"
)

$ErrorActionPreference = "Continue"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$fail = 0

Write-Host "== NT.QMS staging smoke gate -> $BaseUrl ==" -ForegroundColor Cyan

Write-Host "`n[1/4] Readiness"
try {
    $r = Invoke-WebRequest "$BaseUrl/health/ready" -UseBasicParsing -TimeoutSec 20
    if ($r.StatusCode -eq 200) { Write-Host "  PASS  /health/ready is 200" -ForegroundColor Green } else { Write-Host "  FAIL  readiness $($r.StatusCode)" -ForegroundColor Red; $fail++ }
} catch { Write-Host "  FAIL  readiness unreachable: $($_.Exception.Message)" -ForegroundColor Red; $fail++ }

Write-Host "`n[2/4] TLS / HSTS (staging must be https with HSTS)"
if ($BaseUrl -like "https://*") {
    try {
        $h = (Invoke-WebRequest "$BaseUrl/health/live" -UseBasicParsing -TimeoutSec 20).Headers
        if ($h["Strict-Transport-Security"]) { Write-Host "  PASS  HSTS present" -ForegroundColor Green } else { Write-Host "  FAIL  no HSTS on an https origin" -ForegroundColor Red; $fail++ }
    } catch { Write-Host "  FAIL  $($_.Exception.Message)" -ForegroundColor Red; $fail++ }
} else {
    Write-Host "  WARN  BaseUrl is not https - staging MUST be TLS-terminated (ADR-0002)" -ForegroundColor Yellow
}

Write-Host "`n[3/4] Fast security probe"
& "$here/security-probe.ps1" -BaseUrl $BaseUrl -Tenant $Tenant -Email $Email -Password $Password
if ($LASTEXITCODE -ne 0) { $fail++ }

Write-Host "`n[4/4] Deep security probe"
& "$here/security-probe-deep.ps1" -BaseUrl $BaseUrl -PlatformEmail $PlatformEmail -PlatformPassword $PlatformPassword
if ($LASTEXITCODE -ne 0) { $fail++ }

Write-Host ""
if ($fail -eq 0) { Write-Host "STAGING SMOKE: PASS" -ForegroundColor Green; exit 0 }
else { Write-Host "STAGING SMOKE: $fail stage(s) FAILED" -ForegroundColor Red; exit 1 }
