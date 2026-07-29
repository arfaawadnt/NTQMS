<#
.SYNOPSIS
    Verifies an IIS + Windows-service install of NT.QMS, including that it is
    genuinely configured to come back after a reboot.

.DESCRIPTION
    Checks the two things that actually determine reboot survival (both services
    set to Automatic), then that the stack answers end to end: Kestrel directly on
    loopback, then the same API through the IIS proxy, then the SPA shell.

    Run after Install-NTQMS-IIS.ps1, and again after the first real reboot.
#>
[CmdletBinding()]
param(
    [string]$SiteHostName = 'localhost',
    [int]$KestrelPort = 5000,
    [int]$HttpPort = 80,
    [string]$ServiceName = 'NTQAMS',
    [string]$SiteName = 'NTQMS'
)

$pass = 0; $fail = 0
function Check([string]$Name, [bool]$Condition, [string]$Detail) {
    if ($Condition) { Write-Host ("  PASS  {0}  {1}" -f $Name, $Detail) -ForegroundColor Green; $script:pass++ }
    else            { Write-Host ("  FAIL  {0}  {1}" -f $Name, $Detail) -ForegroundColor Red;   $script:fail++ }
}

Write-Host "`nNT.QMS IIS install verification" -ForegroundColor Cyan

# --- reboot survival is a configuration property, so assert it directly -------
$svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
Check 'api service exists' ($null -ne $svc) $ServiceName
if ($svc) {
    $startMode = (Get-CimInstance Win32_Service -Filter "Name='$ServiceName'").StartMode
    Check 'api service start mode' ($startMode -eq 'Auto') "StartMode=$startMode (must be Auto to survive reboot)"
    Check 'api service running'    ($svc.Status -eq 'Running') "Status=$($svc.Status)"
}
$w3 = Get-CimInstance Win32_Service -Filter "Name='W3SVC'" -ErrorAction SilentlyContinue
Check 'IIS start mode' ($w3 -and $w3.StartMode -eq 'Auto') "W3SVC StartMode=$($w3.StartMode)"

try {
    Import-Module WebAdministration -ErrorAction Stop
    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    Check 'IIS site exists'  ($null -ne $site) $SiteName
    if ($site) {
        Check 'IIS site started' ($site.State -eq 'Started') "State=$($site.State)"
        Check 'IIS site autostart' ([bool]$site.serverAutoStart) "serverAutoStart=$($site.serverAutoStart)"
    }
} catch { Check 'WebAdministration module' $false $_.Exception.Message }

# --- Kestrel directly (bypassing IIS) ----------------------------------------
foreach ($probe in @(
    @{ Name = 'kestrel liveness '; Url = "http://127.0.0.1:$KestrelPort/health/live" },
    @{ Name = 'kestrel readiness'; Url = "http://127.0.0.1:$KestrelPort/health/ready" })) {
    try {
        $r = Invoke-WebRequest -Uri $probe.Url -TimeoutSec 10 -UseBasicParsing
        Check $probe.Name ($r.StatusCode -eq 200) "$($r.StatusCode) $($r.Content)"
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'no answer' }
        $hint = if ($code -eq 503) { '(API up, PostgreSQL unreachable)' } else { '' }
        Check $probe.Name $false "$code $hint"
    }
}

# --- through IIS (proves URL Rewrite + ARR are actually proxying) ------------
$base = "http://${SiteHostName}:$HttpPort"
try {
    $r = Invoke-WebRequest -Uri "$base/health/ready" -TimeoutSec 10 -UseBasicParsing
    Check 'proxy to api      ' ($r.StatusCode -eq 200) "$base/health/ready -> $($r.StatusCode)"
} catch {
    $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'no answer' }
    $hint = switch ($code) { 500 { '(500.19 usually = URL Rewrite/ARR missing)' } 502 { '(502 = Kestrel not answering)' } default { '' } }
    Check 'proxy to api      ' $false "$code $hint"
}

# An unauthenticated API call must be rejected in the documented shape, proving
# it is the real API responding and not a static file or an IIS error page.
try {
    Invoke-WebRequest -Uri "$base/api/nonconformances" -TimeoutSec 10 -UseBasicParsing | Out-Null
    Check 'api authz via proxy' $false 'expected 401 but the call succeeded unauthenticated'
} catch {
    $resp = $_.Exception.Response
    $code = if ($resp) { [int]$resp.StatusCode } else { 0 }
    $ctype = if ($resp) { $resp.ContentType } else { '' }
    Check 'api authz via proxy' ($code -eq 401) "401 expected, got $code ($ctype)"
}

# --- SPA shell ---------------------------------------------------------------
try {
    $r = Invoke-WebRequest -Uri "$base/" -TimeoutSec 10 -UseBasicParsing
    Check 'spa shell         ' ($r.StatusCode -eq 200 -and $r.Content -match '<app-root|<title') "$($r.StatusCode)"
} catch { Check 'spa shell         ' $false $_.Exception.Message }

# --- deep link must fall back to index.html, not 404 -------------------------
try {
    $r = Invoke-WebRequest -Uri "$base/t/demo-lab" -TimeoutSec 10 -UseBasicParsing
    Check 'spa deep link     ' ($r.StatusCode -eq 200) "/t/demo-lab -> $($r.StatusCode) (SPA fallback rule)"
} catch {
    $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'no answer' }
    Check 'spa deep link     ' $false "$code (404 = SPA fallback rewrite rule not applied)"
}

# --- security headers must be present at the edge ----------------------------
try {
    $r = Invoke-WebRequest -Uri "$base/" -TimeoutSec 10 -UseBasicParsing
    foreach ($h in 'Content-Security-Policy','X-Content-Type-Options','X-Frame-Options','Referrer-Policy') {
        Check "header $h" ($r.Headers.Keys -contains $h) ($r.Headers[$h])
    }
} catch { Check 'security headers  ' $false $_.Exception.Message }

# PowerShell 5.1 rejects "(if ...)" as an argument value at runtime, so resolve
# the colour first rather than inlining a conditional.
$summaryColour = 'Red'
if ($fail -eq 0) { $summaryColour = 'Green' }
Write-Host ("`nSUMMARY: {0} passed, {1} failed" -f $pass, $fail) -ForegroundColor $summaryColour
if ($fail -eq 0) {
    Write-Host "Reboot test: restart the host, run this script again WITHOUT starting anything by hand." -ForegroundColor Cyan
    exit 0
}
exit 1
