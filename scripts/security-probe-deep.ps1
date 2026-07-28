<#
.SYNOPSIS
    NT.QMS deep security probe (wave 2) — stateful session & cross-tenant
    attacks that the fast smoke (security-probe.ps1) does not cover.
.DESCRIPTION
    Grey-box adversarial checks against a RUNNING instance:
      [I] Refresh-token reuse detection (ADR-0009): a rotated (stale) refresh
          token, replayed, must be rejected AND revoke the whole family.
      [J] Cross-tenant IDOR/BOLA: tenant B must not read tenant A's record by
          id (RLS fail-closed) — 404, never 200.
      [K] Refresh with no cookie -> 401, not 500.
      [L] CORS is not reflected (ADR-0007 same-origin, no CORS policy).
      [M] Defensive headers ride an AUTHENTICATED 403, not just 200s.
      [N] TRACE method is not enabled (no cross-site tracing).

    The refresh cookie is Secure+SameSite=Strict+path-scoped, so it will not
    round-trip a cookie container over plain-HTTP dev; the token is extracted
    from Set-Cookie and replayed manually to test the SERVER-side rotation
    logic directly. DEV-INSTANCE assessment, not a staging pen test.
#>
param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$PlatformEmail = "platform-admin@localhost",
    [string]$PlatformPassword = "Dev-Only-Platform-Pass-1!"
)

$ErrorActionPreference = "Continue"
$script:pass = 0; $script:fail = 0
function Pass($m) { Write-Host "  PASS  $m" -ForegroundColor Green; $script:pass++ }
function Fail($m) { Write-Host "  FAIL  $m" -ForegroundColor Red; $script:fail++ }

# Raw request that also exposes Set-Cookie and lets us send a manual Cookie header.
function Req($method, $path, $headers, $body, $cookie) {
    $h = @{}; if ($headers) { $h = $headers.Clone() }
    if ($cookie) { $h["Cookie"] = $cookie }
    try {
        $p = @{ Uri="$BaseUrl$path"; Method=$method; UseBasicParsing=$true; TimeoutSec=15; Headers=$h }
        if ($body) { $p.Body = $body; $p.ContentType = "application/json" }
        $r = Invoke-WebRequest @p
        return @{ Status=[int]$r.StatusCode; Headers=$r.Headers; Body=$r.Content; SetCookie=$r.Headers["Set-Cookie"] }
    } catch {
        $resp = $_.Exception.Response
        $status = if ($resp) { [int]$resp.StatusCode.value__ } else { 0 }
        $bd=""; try { $sr=New-Object IO.StreamReader($resp.GetResponseStream()); $bd=$sr.ReadToEnd() } catch {}
        $sc = if ($resp) { $resp.Headers["Set-Cookie"] } else { $null }
        return @{ Status=$status; Headers=($resp.Headers); Body=$bd; SetCookie=$sc }
    }
}
function Rt($setCookie) {
    if (-not $setCookie) { return $null }
    $m = [regex]::Match(($setCookie -join "; "), "qams_rt=([^;]+)")
    if ($m.Success) { return "qams_rt=$($m.Groups[1].Value)" }
    return $null
}
function Login($tenant, $email, $pw) {
    $b = @{ email=$email; password=$pw }; if ($tenant) { $b.tenantIdentifier = $tenant }
    return Req POST "/api/auth/login" $null ($b | ConvertTo-Json)
}
function TokenOf($resp) { if ($resp.Status -eq 200) { return ($resp.Body | ConvertFrom-Json).accessToken } return $null }

Write-Host "NT.QMS deep security probe -> $BaseUrl  ($(Get-Date -Format s))"

# Provision two fresh tenants as platform admin (for the cross-tenant IDOR case).
$plat = TokenOf (Login $null $PlatformEmail $PlatformPassword)
if (-not $plat) { Write-Host "  ABORT  platform admin login failed" -ForegroundColor Red; exit 2 }
$platH = @{ Authorization = "Bearer $plat" }
$suffix = [Guid]::NewGuid().ToString("N").Substring(0,8)
$tenants = @{}
foreach ($n in "a","b") {
    $slug = "pt-$n-$suffix"
    Req POST "/api/tenants" $platH (@{ identifier=$slug; name="PT $n"; adminEmail="admin@$slug.test"; adminDisplayName="Admin $n"; adminPassword="Pentest-Pass-1!" } | ConvertTo-Json) | Out-Null
    $tenants[$n] = @{ Slug=$slug; Token=(TokenOf (Login $slug "admin@$slug.test" "Pentest-Pass-1!")) }
}
$aH = @{ Authorization = "Bearer $($tenants['a'].Token)" }
$bH = @{ Authorization = "Bearer $($tenants['b'].Token)" }

Write-Host "`n[J] Cross-tenant IDOR / BOLA"
$mk = Req POST "/api/nonconformances" $aH (@{ title="tenant-a-secret"; description="x"; severity=3; likelihood=2; sourceType="Internal" } | ConvertTo-Json)
$ncId = if ($mk.Status -in 200,201) { ($mk.Body | ConvertFrom-Json).id } else { $null }
if ($ncId) {
    $steal = Req GET "/api/nonconformances/$ncId" $bH $null
    if ($steal.Status -eq 404) { Pass "tenant B cannot read tenant A's record by id (404, RLS fail-closed)" }
    elseif ($steal.Status -eq 200) { Fail "CROSS-TENANT LEAK: tenant B read tenant A's record (200)" }
    else { Pass "tenant B denied ($($steal.Status))" }
    $own = Req GET "/api/nonconformances/$ncId" $aH $null
    if ($own.Status -eq 200) { Pass "tenant A can read its own record (control passes)" } else { Fail "owner read failed ($($own.Status)) - test invalid" }
} else { Fail "could not create the tenant-A record ($($mk.Status)) - IDOR test inconclusive" }

Write-Host "`n[I] Refresh-token reuse detection (ADR-0009)"
# The refresh cookie is Secure+SameSite=Strict; Invoke-WebRequest silently
# drops a manually-set Cookie header, so the stateful flow uses curl.exe
# (ships with Windows 10+) which transmits it faithfully.
$slugA = $tenants['a'].Slug
function CurlCookie($body) {   # login, return the qams_rt cookie value
    # Body via a file (-d @file): PowerShell 5.1 mangles JSON passed inline to
    # a native exe, so write it out and let curl read it verbatim.
    $tmp = [IO.Path]::GetTempFileName(); $bodyFile = [IO.Path]::GetTempFileName()
    Set-Content -Path $bodyFile -Value $body -Encoding ascii -NoNewline
    & curl.exe -s -D $tmp -o NUL -X POST "$BaseUrl/api/auth/login" -H "Content-Type: application/json" --data "@$bodyFile" | Out-Null
    $hdr = Get-Content $tmp -Raw
    Remove-Item $tmp, $bodyFile -ErrorAction SilentlyContinue
    $m = [regex]::Match($hdr, "qams_rt=([^;\s]+)"); if ($m.Success) { return $m.Groups[1].Value } return $null
}
function CurlRefresh($cookie) {  # POST /refresh with a real Cookie header; return @{Status; NewCookie}
    $tmp = [IO.Path]::GetTempFileName()
    $code = & curl.exe -s -D $tmp -o NUL -w "%{http_code}" -X POST "$BaseUrl/api/auth/refresh" -H "Cookie: qams_rt=$cookie"
    $hdr = Get-Content $tmp -Raw; Remove-Item $tmp -ErrorAction SilentlyContinue
    $m = [regex]::Match($hdr, "qams_rt=([^;\s]+)")
    return @{ Status = [int]$code; NewCookie = if ($m.Success) { $m.Groups[1].Value } else { $null } }
}
$rt0 = CurlCookie (@{ tenantIdentifier=$slugA; email="admin@$slugA.test"; password="Pentest-Pass-1!" } | ConvertTo-Json)
if (-not $rt0) { Fail "login issued no refresh cookie" }
else {
    $rot = CurlRefresh $rt0                                     # rotate: rt0 -> rt1
    if ($rot.Status -eq 200 -and $rot.NewCookie) { Pass "a valid refresh rotates to a new token (200)" } else { Fail "refresh did not rotate ($($rot.Status))" }
    $reuse = CurlRefresh $rt0                                   # replay the STALE token
    if ($reuse.Status -in 401,403) { Pass "replaying the rotated (stale) token is rejected ($($reuse.Status))" } else { Fail "stale refresh token accepted ($($reuse.Status))" }
    if ($rot.NewCookie) {
        $afterReuse = CurlRefresh $rot.NewCookie               # successor must be dead too (family revoked)
        if ($afterReuse.Status -in 401,403) { Pass "reuse revoked the WHOLE family (successor also rejected: $($afterReuse.Status))" }
        else { Fail "family not revoked on reuse - successor still valid ($($afterReuse.Status))" }
    }
}

Write-Host "`n[K] Refresh without a cookie"
$noc = Req POST "/api/auth/refresh" $null $null $null
if ($noc.Status -in 401,400) { Pass "refresh with no cookie is $($noc.Status), not 500" } else { Fail "refresh no-cookie returned $($noc.Status)" }

Write-Host "`n[L] CORS not reflected (ADR-0007 same-origin)"
$cors = Req GET "/health/live" @{ Origin = "https://evil.example" } $null
$acao = $cors.Headers["Access-Control-Allow-Origin"]
if (-not $acao) { Pass "no Access-Control-Allow-Origin reflected for a foreign Origin" } else { Fail "CORS reflected: $acao" }

Write-Host "`n[M] Defensive headers on an authenticated 403"
$denied = Req GET "/api/tenants" $aH $null   # tenant admin hitting the platform-only surface
if ($denied.Status -eq 403) {
    if ($denied.Headers["Content-Security-Policy"] -and $denied.Headers["X-Content-Type-Options"]) { Pass "CSP + nosniff present on the 403 response" }
    else { Fail "defensive headers missing on the 403" }
} else { Write-Host "  INFO  expected 403 on /api/tenants for a tenant admin, got $($denied.Status)" -ForegroundColor Yellow }

Write-Host "`n[N] Cross-site tracing (XST) - TRACE must not echo the request"
# The vuln is TRACE REFLECTING request headers, not merely answering. Send a
# marker header and assert it is not echoed back.
$marker = "X-Probe-Marker-$([Guid]::NewGuid().ToString('N'))"
$tr = & curl.exe -s -X TRACE "$BaseUrl/health/live" -H "${marker}: secret-value"
if ($tr -notmatch $marker -and $tr -notmatch "secret-value") { Pass "TRACE does not echo request headers (no XST)" } else { Fail "TRACE echoed the request header (XST)" }

Write-Host ""
Write-Host "SUMMARY: $($script:pass) passed, $($script:fail) failed" -ForegroundColor Cyan
if ($script:fail -gt 0) { exit 1 } else { exit 0 }
