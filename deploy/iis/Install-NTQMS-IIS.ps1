<#
.SYNOPSIS
    Installs NT.QMS on this Windows host so it survives reboots: Kestrel as an
    auto-start Windows service, fronted by IIS.

.DESCRIPTION
    This AUTOMATES the topology already specified in deploy\DEPLOY.md (sections
    1-4) and ADR-0001/0002/0007 - it does not invent a new one:

        browser --HTTPS--> IIS site (static SPA + URL Rewrite/ARR proxy)
                              |  /api/* reverse-proxied over loopback
                              v
                           Kestrel (Windows service "NTQAMS", start=auto)
                              |
                              v
                        PostgreSQL 17 (role qams_app, least privilege)

    Reboot survival comes from two independent mechanisms: the Windows service is
    start=auto, and IIS (W3SVC) is itself an auto-start service. Neither depends
    on a logged-in user or an open shell - which is what the dev-up.ps1 scripts
    cannot give you.

    MUST BE RUN FROM AN ELEVATED POWERSHELL. It is idempotent: re-running it
    refreshes the published files and configuration without duplicating objects.

.NOTES
    Prerequisites that this script CANNOT install for you (each is a Microsoft
    download + installer, and installing software is your decision, not the
    script's):
      * IIS role/features           - the script CAN enable these (see -EnableIisFeatures)
      * URL Rewrite 2.1             - https://www.iis.net/downloads/microsoft/url-rewrite
      * Application Request Routing - https://www.iis.net/downloads/microsoft/application-request-routing
      * .NET 9 Runtime (or Hosting Bundle) on the host

    HARD BLOCKER - read before running: in Production the application REFUSES to
    start when its database role owns the tables or has SUPERUSER/BYPASSRLS
    (finding TENANT-004; verified by OQ-DEP-01). A development database where
    qams_app owns the schema WILL be rejected. Run deploy\db-init.sql,
    deploy\migrations.sql (as qams_owner) and deploy\harden-runtime-role.sql
    first. This is the guard working as designed, not a bug.

.EXAMPLE
    # From an ELEVATED PowerShell:
    .\Install-NTQMS-IIS.ps1 -SiteHostName qms.lab.local -TargetRoot C:\apps\ntqams
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Where the published API and SPA are installed.
    [string]$TargetRoot = 'C:\apps\ntqams',
    # IIS site name and host header.
    [string]$SiteName = 'NTQMS',
    [string]$SiteHostName = 'localhost',
    # Loopback port Kestrel listens on (never exposed to the network directly).
    [int]$KestrelPort = 5000,
    # HTTP/HTTPS ports for the IIS site.
    [int]$HttpPort = 80,
    [int]$HttpsPort = 443,
    # Thumbprint of an existing certificate in LocalMachine\My for the HTTPS binding.
    # Omit to configure HTTP only (acceptable only for an isolated internal trial).
    [string]$CertificateThumbprint,
    # Enable the required IIS Windows features if they are missing.
    [switch]$EnableIisFeatures,
    # Skip the build/publish step and deploy whatever is already in $TargetRoot.
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # deploy\iis -> repo root
$serviceName = 'NTQAMS'
$apiDir = Join-Path $TargetRoot 'api'
$webDir = Join-Path $TargetRoot 'web'

function Fail([string]$Message) { Write-Host "  FAIL  $Message" -ForegroundColor Red; throw $Message }
function Ok([string]$Message)   { Write-Host "  ok    $Message" -ForegroundColor Green }
function Warn([string]$Message) { Write-Host "  warn  $Message" -ForegroundColor Yellow }

# ---------------------------------------------------------------- 0 preflight
Write-Host "`n[0/6] Preflight" -ForegroundColor Cyan

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Fail 'Not elevated. Re-run this script from an ELEVATED PowerShell (Run as administrator).'
}
Ok 'running elevated'

if ($EnableIisFeatures) {
    $features = @('IIS-WebServerRole','IIS-WebServer','IIS-StaticContent','IIS-DefaultDocument',
                  'IIS-HttpErrors','IIS-HttpLogging','IIS-RequestFiltering','IIS-Security',
                  'IIS-WebServerManagementTools','IIS-ManagementConsole')
    foreach ($f in $features) {
        $state = (Get-WindowsOptionalFeature -Online -FeatureName $f -ErrorAction SilentlyContinue).State
        if ($state -ne 'Enabled') {
            if ($PSCmdlet.ShouldProcess($f, 'Enable IIS feature')) {
                Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart | Out-Null
                Ok "enabled IIS feature $f"
            }
        }
    }
}

if (-not (Get-Service W3SVC -ErrorAction SilentlyContinue)) {
    Fail 'IIS (W3SVC) is not installed. Re-run with -EnableIisFeatures, or install IIS via Windows Features first.'
}
Ok 'IIS present'

Import-Module WebAdministration -ErrorAction Stop
Ok 'WebAdministration module loaded'

# The SPA web.config uses a URL Rewrite proxy rule (deploy\web.config), which
# needs BOTH modules. Without them IIS returns 500.19 on every request.
if (-not (Test-Path "$env:SystemRoot\System32\inetsrv\rewrite.dll")) {
    Fail 'URL Rewrite module missing. Install it: https://www.iis.net/downloads/microsoft/url-rewrite'
}
Ok 'URL Rewrite present'
if (-not (Test-Path "$env:SystemRoot\System32\inetsrv\requestRouter.dll")) {
    Fail 'Application Request Routing (ARR) missing - required by the /api proxy rule. https://www.iis.net/downloads/microsoft/application-request-routing'
}
Ok 'ARR present'

# ARR only proxies when the proxy feature is switched on at the server level.
$proxyEnabled = (Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
                    -Filter 'system.webServer/proxy' -Name 'enabled' -ErrorAction SilentlyContinue).Value
if (-not $proxyEnabled) {
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' `
        -Name 'enabled' -Value 'True'
    Ok 'enabled ARR proxy at server level'
} else { Ok 'ARR proxy already enabled' }

Warn 'Database prerequisite NOT checked by this script: the app refuses to start in'
Warn 'Production unless its role is least-privilege. Run db-init.sql + migrations.sql'
Warn '(as qams_owner) + harden-runtime-role.sql first. See deploy\DEPLOY.md section 1.'

# ------------------------------------------------------- 1 publish artifacts
Write-Host "`n[1/6] Publish API + SPA" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $apiDir, $webDir -Force | Out-Null

if ($SkipPublish) {
    Warn "-SkipPublish: deploying existing contents of $TargetRoot"
} else {
    $dotnet = @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
        'C:\Program Files\dotnet\dotnet.exe'
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $dotnet) { Fail 'dotnet SDK not found - needed to publish. Install the .NET 9 SDK or use -SkipPublish.' }

    & $dotnet publish (Join-Path $repo 'src\NT.QAMS.WebApi') -c Release -o $apiDir
    if ($LASTEXITCODE -ne 0) { Fail 'API publish failed' }
    Ok "API published to $apiDir"

    $npm = 'C:\Program Files\nodejs\npm.cmd'
    if (-not (Test-Path $npm)) { Fail 'npm not found - Angular 22 needs Node >= 20.19 (Node 24 recommended)' }
    Push-Location (Join-Path $repo 'frontend')
    try {
        & $npm ci
        if ($LASTEXITCODE -ne 0) { Fail 'npm ci failed' }
        & $npm run build
        if ($LASTEXITCODE -ne 0) { Fail 'Angular production build failed' }
    } finally { Pop-Location }

    # Angular writes to dist/<project>/browser with the modern application builder.
    $dist = Get-ChildItem (Join-Path $repo 'frontend\dist') -Directory | Select-Object -First 1
    $src  = if (Test-Path (Join-Path $dist.FullName 'browser')) { Join-Path $dist.FullName 'browser' } else { $dist.FullName }
    Remove-Item (Join-Path $webDir '*') -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $src '*') $webDir -Recurse -Force
    Ok "SPA published to $webDir (from $src)"
}

# The repo's IIS config: security headers + SPA fallback + /api proxy to Kestrel.
$webConfigSrc = Join-Path $repo 'deploy\web.config'
if (-not (Test-Path $webConfigSrc)) { Fail "missing $webConfigSrc" }
$cfg = Get-Content $webConfigSrc -Raw
# Point the proxy rule at the port this install actually uses.
$cfg = [regex]::Replace($cfg, 'http://(localhost|127\.0\.0\.1):\d+', "http://127.0.0.1:$KestrelPort")
Set-Content -Path (Join-Path $webDir 'web.config') -Value $cfg -Encoding UTF8
Ok "web.config deployed (proxy target 127.0.0.1:$KestrelPort)"

# ------------------------------------------------- 2 Kestrel Windows service
Write-Host "`n[2/6] Kestrel as an auto-start Windows service" -ForegroundColor Cyan
$exe = Join-Path $apiDir 'NT.QAMS.WebApi.exe'
if (-not (Test-Path $exe)) { Fail "published API exe not found at $exe" }

$existing = Get-Service $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') { Stop-Service $serviceName -Force; Start-Sleep 3 }
    Ok "existing service '$serviceName' stopped for update"
} else {
    & sc.exe create $serviceName binPath= "`"$exe`"" start= auto DisplayName= "NT.QMS API" | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "sc.exe create failed ($LASTEXITCODE)" }
    & sc.exe description $serviceName "NT.QMS Quality Management System API (Kestrel behind IIS)" | Out-Null
    Ok "service '$serviceName' created with start=auto (this is the reboot survival)"
}
# Restart automatically if the process dies.
& sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
Ok 'service configured to auto-restart on failure'

# --------------------------------------------------------- 3 service config
Write-Host "`n[3/6] Service environment" -ForegroundColor Cyan
Warn 'Secrets are NOT set by this script. It will not invent or store your'
Warn 'credentials. Set them once on the service key, then start the service:'
$envKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"
Write-Host @"
      `$env = @(
        'ASPNETCORE_ENVIRONMENT=Production'
        'ASPNETCORE_URLS=http://127.0.0.1:$KestrelPort'
        'Database__MigrateOnStartup=false'
        'ConnectionStrings__Postgres=Host=DBHOST;Port=5432;Database=ntqams;Username=qams_app;Password=***'
        'Jwt__Secret=<random 48+ chars>'
        'Jwt__Issuer=nt-qams'
        'Jwt__Audience=nt-qams'
        'PlatformAdmin__Email=<bootstrap admin>'
        'PlatformAdmin__Password=<strong password>'
      )
      New-ItemProperty -Path '$envKey' -Name Environment -PropertyType MultiString -Value `$env -Force
"@ -ForegroundColor Gray
Warn 'MigrateOnStartup MUST stay false: the runtime role has no DDL rights, and a'
Warn 'cold start with the database down would otherwise fail fast (see OPS-010).'
Warn "The service key is readable by administrators - restrict `"$envKey`" ACLs if"
Warn 'your threat model requires it, or use a managed secret store.'

# Grant the service account write access to the evidence/attachment volume only.
$dataDir = Join-Path $apiDir 'data\files'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
& icacls $dataDir /grant "NT SERVICE\${serviceName}:(OI)(CI)M" /T | Out-Null
Ok "granted the service write access to $dataDir (attachments only)"

# ------------------------------------------------------------- 4 IIS objects
Write-Host "`n[4/6] IIS app pool + site" -ForegroundColor Cyan
$poolName = "$SiteName-web"
if (-not (Test-Path "IIS:\AppPools\$poolName")) { New-WebAppPool -Name $poolName | Out-Null; Ok "app pool '$poolName' created" }
else { Ok "app pool '$poolName' exists" }

# Static SPA only - no managed code. AlwaysRunning + no idle timeout + no
# periodic recycle so the site does not go cold (and so nothing is ever
# half-restarted while proxying).
Set-ItemProperty "IIS:\AppPools\$poolName" -Name managedRuntimeVersion       -Value ''
Set-ItemProperty "IIS:\AppPools\$poolName" -Name startMode                   -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$poolName" -Name processModel.idleTimeout     -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$poolName" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$poolName" -Name recycling.disallowOverlappingRotation -Value $true
Ok 'app pool: no managed code, AlwaysRunning, no idle timeout, no periodic recycle'

if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    New-Website -Name $SiteName -PhysicalPath $webDir -ApplicationPool $poolName `
                -Port $HttpPort -HostHeader $SiteHostName | Out-Null
    Ok "site '$SiteName' created on http://${SiteHostName}:$HttpPort"
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath      -Value $webDir
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool   -Value $poolName
    Ok "site '$SiteName' updated"
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name serverAutoStart -Value $true
Ok 'site set to auto-start with IIS'

if ($CertificateThumbprint) {
    $binding = Get-WebBinding -Name $SiteName -Protocol https -ErrorAction SilentlyContinue
    if (-not $binding) {
        New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $SiteHostName -SslFlags 1
    }
    $cert = Get-Item "Cert:\LocalMachine\My\$CertificateThumbprint" -ErrorAction SilentlyContinue
    if (-not $cert) { Fail "certificate $CertificateThumbprint not found in LocalMachine\My" }
    $sni = "$SiteHostName!$HttpsPort"
    Push-Location IIS:\SslBindings
    try {
        if (-not (Test-Path ".\$sni")) { $cert | New-Item ".\$sni" -Force | Out-Null }
    } finally { Pop-Location }
    Ok "HTTPS binding on :$HttpsPort with certificate $CertificateThumbprint (TLS terminates here - ADR-0002)"
} else {
    Warn 'No -CertificateThumbprint given: HTTP only. ADR-0002 requires TLS at this proxy'
    Warn 'for anything beyond an isolated internal trial, and HSTS is only meaningful over HTTPS.'
}

& icacls $webDir /grant "IIS AppPool\${poolName}:(OI)(CI)RX" /T | Out-Null
Ok 'granted the app pool read access to the SPA files'

# ------------------------------------------------------------------ 5 start
Write-Host "`n[5/6] Start" -ForegroundColor Cyan
$svc = Get-Service $serviceName
$envSet = (Get-ItemProperty -Path $envKey -Name Environment -ErrorAction SilentlyContinue).Environment
if (-not $envSet) {
    Warn "service '$serviceName' NOT started: set its Environment values first (step 3 above), then:"
    Warn "  Start-Service $serviceName"
} else {
    Start-Service $serviceName
    Ok "service '$serviceName' started"
}
Start-Website -Name $SiteName -ErrorAction SilentlyContinue
Ok "site '$SiteName' started"

# ----------------------------------------------------------------- 6 verify
Write-Host "`n[6/6] Verify" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'Verify-NTQMS-IIS.ps1') -SiteHostName $SiteHostName -KestrelPort $KestrelPort `
    -HttpPort $HttpPort -ServiceName $serviceName
