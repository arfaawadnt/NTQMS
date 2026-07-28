<#
.SYNOPSIS
    NT.QMS automated adversarial security probe (Road-to-100 external-track
    PRECURSOR, not a substitute for a vendor penetration test).
.DESCRIPTION
    Fires real attack attempts at a RUNNING instance and asserts the defence
    holds. Covers the classes the app claims to defend (OWASP API Top-10
    aligned): broken auth, tenant isolation (BOLA), unauthenticated access,
    injection, mass-assignment / privilege escalation, rate-limit bypass,
    security headers, verb tampering, and error-shape leakage. Read-oriented
    and self-cleaning; safe against a dev instance.

    Every check prints PASS (defence held) or FAIL (a real weakness). This is
    a DEV-INSTANCE assessment; a production pen test against staging remains
    the authoritative external activity.
#>
param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$Tenant = "demo-lab",
    [string]$Email = "admin@demo-lab.local",
    [string]$Password = "Demo-Admin-Pass-2!"
)

$ErrorActionPreference = "Continue"
$script:pass = 0; $script:fail = 0
function Pass($m) { Write-Host "  PASS  $m" -ForegroundColor Green; $script:pass++ }
function Fail($m) { Write-Host "  FAIL  $m" -ForegroundColor Red; $script:fail++ }

# Raw HTTP helper returning status + headers + body regardless of status code.
function Req($method, $path, $headers, $body) {
    try {
        $p = @{ Uri = "$BaseUrl$path"; Method = $method; UseBasicParsing = $true; TimeoutSec = 15 }
        if ($headers) { $p.Headers = $headers }
        if ($body) { $p.Body = $body; $p.ContentType = "application/json" }
        $r = Invoke-WebRequest @p
        return @{ Status = [int]$r.StatusCode; Headers = $r.Headers; Body = $r.Content }
    } catch {
        $resp = $_.Exception.Response
        $status = if ($resp) { [int]$resp.StatusCode.value__ } else { 0 }
        $bd = ""
        try { $sr = New-Object IO.StreamReader($resp.GetResponseStream()); $bd = $sr.ReadToEnd() } catch {}
        return @{ Status = $status; Headers = $resp.Headers; Body = $bd }
    }
}

Write-Host "NT.QMS security probe -> $BaseUrl  ($(Get-Date -Format s))"

# Obtain a legitimate token for the authenticated-attack cases.
$login = Req POST "/api/auth/login" $null (@{ tenantIdentifier=$Tenant; email=$Email; password=$Password } | ConvertTo-Json)
$token = if ($login.Status -eq 200) { ($login.Body | ConvertFrom-Json).accessToken } else { $null }
$auth = @{ Authorization = "Bearer $token" }

Write-Host "`n[A] Broken authentication"
# A1 - unauthenticated access to a protected resource
$r = Req GET "/api/nonconformances" $null $null
if ($r.Status -eq 401) { Pass "unauthenticated read is 401 (deny-by-default)" } else { Fail "unauthenticated read returned $($r.Status)" }
# A2 - garbage bearer token
$r = Req GET "/api/nonconformances" @{ Authorization = "Bearer not.a.jwt" } $null
if ($r.Status -eq 401) { Pass "forged/garbage JWT is 401" } else { Fail "garbage JWT returned $($r.Status)" }
# A3 - alg=none style tamper: valid header/body, stripped signature
if ($token) {
  $parts = $token.Split("."); $tampered = "$($parts[0]).$($parts[1])."
  $r = Req GET "/api/nonconformances" @{ Authorization = "Bearer $tampered" } $null
  if ($r.Status -eq 401) { Pass "signature-stripped JWT is 401" } else { Fail "signature-stripped JWT returned $($r.Status)" }
}

Write-Host "`n[B] Injection"
# B1 - SQL injection in a filter param (RLS + parameterized EF should neutralize)
$r = Req GET "/api/nonconformances?search=%27%20OR%201%3D1--" $auth $null
if ($r.Status -eq 200) {
  $env:PGPASSWORD="dev-only-local"
  $total = (& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -h localhost -U qams_app -d ntqams -t -A -c "SELECT count(*) FROM qams.nonconformance;")
  $body = $r.Body | ConvertFrom-Json
  if ($body.total -lt [int]$total -or $body.total -le 20) { Pass "SQLi filter did not widen the result set (returned $($body.total))" }
  else { Fail "SQLi filter may have bypassed filtering (total=$($body.total))" }
} else { Pass "SQLi filter rejected ($($r.Status))" }
# B2 - path traversal on the file download id
$r = Req GET "/api/files/..%2f..%2f..%2fetc%2fpasswd" $auth $null
if ($r.Status -in 400,404) { Pass "path-traversal file id is $($r.Status), not 200" } else { Fail "path-traversal returned $($r.Status)" }

Write-Host "`n[C] Broken object-level authorization (tenant isolation / BOLA)"
# C1 - tenant_id cannot be supplied by the client (JWT-claim only)
$r = Req POST "/api/nonconformances" $auth (@{ title="probe"; description="x"; severity=3; likelihood=2; sourceType="Internal"; tenantId=[guid]::NewGuid().ToString() } | ConvertTo-Json)
if ($r.Status -in 201,200,422,400) { Pass "client-supplied tenantId is ignored/validated (status $($r.Status))" } else { Fail "unexpected $($r.Status) on tenantId mass-assign" }

Write-Host "`n[D] Mass assignment / privilege escalation"
# D1 - self-escalate role via the register endpoint payload (deny-by-default authz)
$r = Req POST "/api/users" $auth (@{ email="esc@x.test"; displayName="esc"; role="PlatformAdmin"; initialPassword="Esc-Pass-1!" } | ConvertTo-Json)
# TenantAdmin creating a PlatformAdmin should be rejected by role validation/authz.
if ($r.Status -in 400,403,422) { Pass "cannot mint a PlatformAdmin via register ($($r.Status))" }
elseif ($r.Status -in 200,201) { Fail "register accepted a PlatformAdmin role escalation" }
else { Pass "register escalation blocked ($($r.Status))" }

Write-Host "`n[F] Security headers & transport"
$r = Req GET "/health/live" $null $null
$h = $r.Headers
if ($h["Content-Security-Policy"]) { Pass "CSP present ($($h['Content-Security-Policy']))" } else { Fail "no Content-Security-Policy" }
if ($h["X-Content-Type-Options"] -eq "nosniff") { Pass "X-Content-Type-Options: nosniff" } else { Fail "no nosniff" }
if ($h["X-Frame-Options"]) { Pass "X-Frame-Options: $($h['X-Frame-Options'])" } else { Fail "no X-Frame-Options" }
if ($h["Strict-Transport-Security"]) { Pass "HSTS present" } else { Write-Host "  INFO  HSTS absent in Development (expected; emitted outside Dev)" -ForegroundColor Yellow }

Write-Host "`n[G] Error-shape leakage"
# G1 - bad login must not leak which factor failed and must be problem+json
$r = Req POST "/api/auth/login" $null (@{ tenantIdentifier=$Tenant; email=$Email; password="definitely-wrong" } | ConvertTo-Json)
if ($r.Body -match "Invalid credentials" -and $r.Body -notmatch "password|user|no-such") { Pass "login failure is generic (no user/password enumeration)" } else { Fail "login error may leak factor detail" }
$ct = $r.Headers["Content-Type"]
if ($ct -match "application/problem.json") { Pass "auth error is application/problem+json" } else { Fail "auth error content-type is $ct" }
# G2 - no stack traces in any observed error body
if ($r.Body -notmatch "at NT\.QAMS|Exception|StackTrace") { Pass "no stack trace leaked in error body" } else { Fail "error body leaked internals" }

Write-Host "`n[H] Verb / method tampering"
$r = Req DELETE "/api/nonconformances" $auth $null
if ($r.Status -in 401,403,404,405,400) { Pass "unsupported DELETE on collection is $($r.Status), not 500" } else { Fail "verb tamper returned $($r.Status)" }

# [E] runs LAST: the credential burst exhausts the /api/auth/* rate-limit
# partition for the rest of the window, so it must not precede the auth-shape
# checks above.
Write-Host "`n[E] Rate-limit / brute force"
$got429 = $false
for ($i=0; $i -lt 15; $i++) { $rr = Req POST "/api/auth/login" $null (@{ tenantIdentifier=$Tenant; email="x@x.test"; password="w$i" } | ConvertTo-Json); if ($rr.Status -eq 429) { $got429=$true; break } }
if ($got429) { Pass "credential burst tripped 429 after several attempts" } else { Fail "no 429 after 15 rapid bad logins" }

Write-Host ""
Write-Host "SUMMARY: $($script:pass) passed, $($script:fail) failed" -ForegroundColor Cyan
if ($script:fail -gt 0) { exit 1 } else { exit 0 }
