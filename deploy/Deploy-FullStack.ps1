<#
.SYNOPSIS
  NT.QAMS v1.52.0 - from-source, full-stack deployment to a Windows Server.
  API (self-contained Kestrel Windows service on loopback) + Angular 22 SPA
  (IIS/TLS, reverse-proxying /api and /health to the API).

  This is the scripted equivalent of deploy/WINDOWS-FULLSTACK-FROMSOURCE-DEPLOY-PROMPT.md.

.DESCRIPTION
  Runs in PHASES, selected by switches. Run -Build on a machine with the .NET 9 SDK
  and Node 24; run -InstallApi / -InstallUi / -Verify on the target server (which
  needs no SDK - the backend is self-contained). Build and target may be the same box.

  HARD RULES enforced by this script:
   * Backend binds 127.0.0.1 only - never exposed to the network.
   * All secrets are GENERATED fresh, written only to $SecretsFile (Administrators-only
     ACL) and to machine-scope environment variables - never into appsettings.json or
     any file under the web root.
   * Exactly ONE API instance per database (ADR-0001).
   * Any error stops the phase ($ErrorActionPreference = 'Stop').

.EXAMPLE
  # On the build machine - produce artifacts under C:\build
  .\Deploy-FullStack.ps1 -Build

.EXAMPLE
  # On the target server - first-time install (creates DB roles), then UI, then verify
  .\Deploy-FullStack.ps1 -InstallApi -InitDatabase -PlatformAdminEmail admin@contoso.com
  .\Deploy-FullStack.ps1 -InstallUi -CertThumbprint 1a2b3c... -HostHeader qams.contoso.com
  .\Deploy-FullStack.ps1 -Verify

.NOTES
  Run elevated (Administrator) for -InstallApi / -InstallUi. Windows PowerShell 5.1 compatible.
  The -InitDatabase step invokes psql and may prompt for the postgres superuser password
  unless -PostgresSuperPassword is supplied.
#>
[CmdletBinding()]
param(
    # ---- phase selectors ----
    [switch]$Build,
    [switch]$InstallApi,
    [switch]$InstallUi,
    [switch]$Verify,

    # ---- build phase ----
    [string]$RepoRoot   = '',   # defaults to the repo root (parent of this script's deploy\ folder)
    [string]$BuildOut   = 'C:\build',

    # ---- install: paths ----
    [string]$AppDir     = 'C:\apps\ntqams',
    [string]$UiDir      = 'C:\inetpub\wwwroot\qams-ui',
    [string]$DataDir    = 'D:\ntqams-data\files',
    [string]$StageApi   = 'C:\install\ntqams-api',   # where Part-0 artifacts were copied
    [string]$StageUi    = 'C:\install\ntqams-ui',

    # ---- install: API ----
    [string]$DbHost     = 'localhost',
    [int]   $ApiPort    = 5000,
    [string]$ServiceName = 'NTQAMS',
    [string]$PlatformAdminEmail,
    [switch]$InitDatabase,                 # create roles+db and apply schema (first install only)
    [string]$PsqlPath   = 'psql',          # or C:\Program Files\PostgreSQL\17\bin\psql.exe
    [string]$PostgresSuperPassword,        # optional; else psql prompts

    # ---- install: UI / IIS ----
    [string]$IisSiteName = 'qams-ui',
    [string]$HostHeader  = '',             # e.g. qams.contoso.com; blank = all unassigned
    [string]$CertThumbprint,               # operator-provided TLS cert in LocalMachine\My

    # ---- secrets sink ----
    [string]$SecretsFile = 'C:\install\SECRETS-README.txt'
)

$ErrorActionPreference = 'Stop'
if(-not $RepoRoot){
    $scriptDir = if($PSScriptRoot){ $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}
function Info($m){ Write-Host "[NTQAMS] $m" -ForegroundColor Cyan }
function Ok($m){ Write-Host "[  OK  ] $m" -ForegroundColor Green }
function Die($m){ Write-Host "[FAIL ] $m" -ForegroundColor Red; throw $m }

function New-RandomString([int]$Length){
    $chars = [char[]]((48..57)+(65..90)+(97..122))            # a-z A-Z 0-9
    -join (1..$Length | ForEach-Object { $chars | Get-Random })
}
function New-StrongPassword([int]$Length = 20){
    $lower='abcdefghijkmnpqrstuvwxyz'; $upper='ABCDEFGHJKLMNPQRSTUVWXYZ'
    $digit='23456789'; $sym='!@#%^*-_=+'
    $all = ($lower+$upper+$digit+$sym).ToCharArray()
    $pw  = @(($lower[(Get-Random -Max $lower.Length)]),
             ($upper[(Get-Random -Max $upper.Length)]),
             ($digit[(Get-Random -Max $digit.Length)]),
             ($sym[(Get-Random   -Max $sym.Length)]))
    $pw += 1..($Length-4) | ForEach-Object { $all | Get-Random }
    -join ($pw | Sort-Object { Get-Random })
}
function Save-Secret([string]$name,[string]$value){
    $dir = Split-Path $SecretsFile
    if(-not (Test-Path $dir)){ New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    if(-not (Test-Path $SecretsFile)){
        New-Item -ItemType File -Path $SecretsFile -Force | Out-Null
        # Administrators-only ACL: disable inheritance, grant Administrators + SYSTEM
        $acl = Get-Acl $SecretsFile
        $acl.SetAccessRuleProtection($true,$false)
        foreach($id in 'BUILTIN\Administrators','NT AUTHORITY\SYSTEM'){
            $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
                $id,'FullControl','Allow')
            $acl.AddAccessRule($rule)
        }
        Set-Acl $SecretsFile $acl
        Add-Content $SecretsFile "NT.QAMS deployment secrets - move to a vault, then DELETE this file."
        Add-Content $SecretsFile ("Generated on {0}" -f (Get-Date -Format o))
        Add-Content $SecretsFile ("-"*60)
    }
    Add-Content $SecretsFile ("{0} = {1}" -f $name,$value)
}

# =====================================================================
function Invoke-Build {
    Info "PART 0 - build & publish from source (RepoRoot=$RepoRoot)"
    if(-not (Get-Command dotnet -ErrorAction SilentlyContinue)){ Die ".NET SDK (dotnet) not found." }
    $apiOut = Join-Path $BuildOut 'ntqams-api'
    $uiOut  = Join-Path $BuildOut 'ntqams-ui'
    New-Item -ItemType Directory -Path $apiOut,$uiOut -Force | Out-Null

    Info "0.2 Publishing backend (self-contained win-x64)..."
    dotnet publish (Join-Path $RepoRoot 'src\NT.QAMS.WebApi\NT.QAMS.WebApi.csproj') `
        -c Release -r win-x64 --self-contained true -o $apiOut
    if(-not (Test-Path (Join-Path $apiOut 'NT.QAMS.WebApi.exe'))){ Die "Backend publish did not produce the exe." }

    Info "0.3 Generating a FRESH idempotent migration script..."
    if(-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)){
        Info "dotnet-ef not found - installing globally..."; dotnet tool install --global dotnet-ef | Out-Null
    }
    dotnet ef migrations script --idempotent `
        --project (Join-Path $RepoRoot 'src\NT.QAMS.Infrastructure') `
        --startup-project (Join-Path $RepoRoot 'src\NT.QAMS.WebApi') `
        -o (Join-Path $apiOut 'migrations.sql')
    Copy-Item (Join-Path $RepoRoot 'deploy\db-init.sql')            $apiOut -Force
    Copy-Item (Join-Path $RepoRoot 'deploy\harden-runtime-role.sql') $apiOut -Force

    Info "0.4 Building the Angular SPA (production)..."
    Push-Location (Join-Path $RepoRoot 'frontend')
    try {
        cmd /c "npm ci"
        cmd /c "node node_modules\@angular\cli\bin\ng.js build --configuration production"
    } finally { Pop-Location }
    $browser = Join-Path $RepoRoot 'frontend\dist\nt-qams-frontend\browser'
    if(-not (Test-Path (Join-Path $browser 'index.html'))){ Die "SPA build output not found at $browser." }
    Copy-Item (Join-Path $browser '*') $uiOut -Recurse -Force

    Info "0.5 Writing IIS web.config..."
    Set-Content -Path (Join-Path $uiOut 'web.config') -Encoding UTF8 -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="proxy-api" stopProcessing="true">
          <match url="^(api|health)(/.*)?$" />
          <action type="Rewrite" url="http://127.0.0.1:$ApiPort/{R:0}" />
        </rule>
        <rule name="spa-fallback" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
"@
    Ok "Build complete. Artifacts:`n  API -> $apiOut`n  UI  -> $uiOut`nCopy these to the target server ($StageApi / $StageUi)."
}

# =====================================================================
function Invoke-InstallApi {
    Info "PART A - backend API install"
    if(-not [Environment]::Is64BitOperatingSystem){ Die "Not a 64-bit OS." }
    if(-not (Test-Path (Join-Path $StageApi 'NT.QAMS.WebApi.exe'))){ Die "Backend artifacts not found at $StageApi (run -Build and copy them here)." }
    if(-not $PlatformAdminEmail){ Die "-PlatformAdminEmail is required." }

    New-Item -ItemType Directory -Path $AppDir,$DataDir -Force | Out-Null
    Info "A2. Copying backend -> $AppDir"
    Copy-Item (Join-Path $StageApi '*') $AppDir -Recurse -Force

    # ---- passwords/secrets ----
    $appDbPwd = New-RandomString 32
    $jwt      = New-RandomString 48
    $paPwd    = New-StrongPassword 20

    if($InitDatabase){
        $ownerPwd = New-RandomString 32
        Info "A3. Initializing database (roles + schema)..."
        $tmpInit = Join-Path $env:TEMP 'ntqams-db-init.sql'
        (Get-Content (Join-Path $AppDir 'db-init.sql')) `
            -replace 'CHANGE_ME_OWNER_BEFORE_RUNNING',$ownerPwd `
            -replace 'CHANGE_ME_APP_BEFORE_RUNNING',$appDbPwd | Set-Content $tmpInit -Encoding UTF8
        if($PostgresSuperPassword){ $env:PGPASSWORD = $PostgresSuperPassword }
        try {
            & $PsqlPath -U postgres -h $DbHost -f $tmpInit
            $env:PGPASSWORD = $ownerPwd
            & $PsqlPath -U qams_owner -h $DbHost -d ntqams -f (Join-Path $AppDir 'migrations.sql')
            if($PostgresSuperPassword){ $env:PGPASSWORD = $PostgresSuperPassword }
            & $PsqlPath -U postgres -h $DbHost -d ntqams -f (Join-Path $AppDir 'harden-runtime-role.sql')
        } finally {
            Remove-Item $tmpInit -Force -ErrorAction SilentlyContinue
            Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        }
        Save-Secret 'PostgreSQL qams_owner password' $ownerPwd
        Ok "Database initialized."
    } else {
        Info "A3. Skipping DB init (-InitDatabase not set). Ensure the qams_app password below matches the existing role, or reset it."
    }

    Info "A4. Setting machine-scope environment variables..."
    $conn = "Host=$DbHost;Port=5432;Database=ntqams;Username=qams_app;Password=$appDbPwd"
    $vars = @{
        'ConnectionStrings__Postgres' = $conn
        'Jwt__Secret'                 = $jwt
        'PlatformAdmin__Email'        = $PlatformAdminEmail
        'PlatformAdmin__Password'     = $paPwd
        'ASPNETCORE_URLS'             = "http://127.0.0.1:$ApiPort"
        'ASPNETCORE_ENVIRONMENT'      = 'Production'
        'Database__MigrateOnStartup'  = 'false'
        'FileStorage__RootPath'       = $DataDir
    }
    foreach($k in $vars.Keys){ [Environment]::SetEnvironmentVariable($k,$vars[$k],'Machine') }
    Save-Secret 'PostgreSQL qams_app password' $appDbPwd
    Save-Secret 'Jwt__Secret'                  $jwt
    Save-Secret 'PlatformAdmin__Email'         $PlatformAdminEmail
    Save-Secret 'PlatformAdmin__Password'      $paPwd

    Info "A5. Installing + starting the Windows service '$ServiceName'..."
    if(Get-Service $ServiceName -ErrorAction SilentlyContinue){
        Info "Service exists - stopping to update binary path."; Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null; Start-Sleep 2
    }
    sc.exe create $ServiceName binPath= "$AppDir\NT.QAMS.WebApi.exe" start= auto | Out-Null
    Start-Service $ServiceName
    Start-Sleep 8
    if((Get-Service $ServiceName).Status -ne 'Running'){ Die "Service failed to reach Running - run the exe in a console to read the startup error." }

    Info "A6. Backend smoke test (loopback)..."
    $ready = (Invoke-WebRequest "http://127.0.0.1:$ApiPort/health/ready" -UseBasicParsing).StatusCode
    if($ready -ne 200){ Die "health/ready returned $ready (expected 200)." }
    try { Invoke-WebRequest "http://127.0.0.1:$ApiPort/api/tenants" -UseBasicParsing | Out-Null; Die "unauthenticated /api/tenants did not return 401." }
    catch { if($_.Exception.Response.StatusCode.value__ -ne 401){ Die "deny-by-default check failed (expected 401)." } }
    Ok "Backend healthy, deny-by-default confirmed. Platform admin: $PlatformAdminEmail (password in $SecretsFile)."
}

# =====================================================================
function Invoke-InstallUi {
    Info "PART B - frontend SPA on IIS"
    if(-not (Test-Path (Join-Path $StageUi 'index.html'))){ Die "SPA artifacts not found at $StageUi." }
    Import-Module WebAdministration -ErrorAction Stop

    Info "B1. Enabling ARR reverse-proxy..."
    try {
        Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
            -filter "system.webServer/proxy" -name "enabled" -value "True"
    } catch { Die "Could not enable the ARR proxy - is Application Request Routing installed? ($_)" }

    Info "B2. Deploying SPA -> $UiDir"
    New-Item -ItemType Directory -Path $UiDir -Force | Out-Null
    Copy-Item (Join-Path $StageUi '*') $UiDir -Recurse -Force

    Info "B3. Configuring IIS site '$IisSiteName' + TLS..."
    if(-not $CertThumbprint){ Die "-CertThumbprint is required (operator-provided TLS cert in Cert:\LocalMachine\My)." }
    if(-not (Test-Path "Cert:\LocalMachine\My\$CertThumbprint")){ Die "Certificate $CertThumbprint not found in LocalMachine\My." }

    if(Get-Website -Name $IisSiteName -ErrorAction SilentlyContinue){ Remove-Website -Name $IisSiteName }
    New-Website -Name $IisSiteName -PhysicalPath $UiDir -Port 443 -Ssl -HostHeader $HostHeader -Force | Out-Null
    # bind the cert
    $binding = Get-WebBinding -Name $IisSiteName -Protocol https
    $binding.AddSslCertificate($CertThumbprint,'My')
    Set-ItemProperty "IIS:\AppPools\$IisSiteName" -Name managedRuntimeVersion -Value '' -ErrorAction SilentlyContinue
    Ok "IIS site '$IisSiteName' bound on 443 with cert $CertThumbprint. (Add an HTTP:80 -> HTTPS redirect separately if required.)"
}

# =====================================================================
function Invoke-Verify {
    Info "PART C - full-stack verification"
    $base = "https://localhost"
    # accept the server's own TLS cert for these loopback checks only
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

    $c1 = (Invoke-WebRequest "$base/health/ready" -UseBasicParsing).StatusCode
    if($c1 -eq 200){ Ok "C1 health/ready proxied -> 200" } else { Die "C1 failed ($c1)" }
    $c2 = (Invoke-WebRequest "$base/" -UseBasicParsing).Content
    if($c2 -match '<qams-root'){ Ok "C2 SPA index served" } else { Die "C2 failed - <qams-root> not found" }
    $c3 = (Invoke-WebRequest "$base/nonconformances" -UseBasicParsing).Content
    if($c3 -match '<qams-root'){ Ok "C3 SPA fallback works" } else { Die "C3 failed" }
    try { Invoke-WebRequest "$base/api/tenants" -UseBasicParsing | Out-Null; Die "C4 failed - expected 401" }
    catch { if($_.Exception.Response.StatusCode.value__ -eq 401){ Ok "C4 proxy + deny-by-default -> 401" } else { Die "C4 unexpected status" } }
    Ok "C1-C4 passed. Do C5-C7 (tenant provisioning + browser login) per the runbook."
}

# =====================================================================
if(-not ($Build -or $InstallApi -or $InstallUi -or $Verify)){
    Get-Help $PSCommandPath -Detailed
    Write-Host "`nSelect a phase: -Build | -InstallApi | -InstallUi | -Verify" -ForegroundColor Yellow
    return
}
if($Build){ Invoke-Build }
if($InstallApi){ Invoke-InstallApi }
if($InstallUi){ Invoke-InstallUi }
if($Verify){ Invoke-Verify }

Info "Done. Reminder: move secrets from $SecretsFile into a vault and delete the file; back up PostgreSQL AND $DataDir."
