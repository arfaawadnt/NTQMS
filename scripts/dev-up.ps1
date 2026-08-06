<#
.SYNOPSIS
    Brings the NT.QMS dev stack up: API on :5080 and the Angular SPA on :4200.

.DESCRIPTION
    Why this script exists: both dev servers used to be started ad-hoc as children
    of whatever shell happened to run them, so they died whenever that shell ended
    - and the API additionally has to be stopped for every 'dotnet build' (the
    running WebApi locks its own DLLs on Windows). The result was an app that
    appeared to "randomly stop working".

    This script starts both servers DETACHED (they outlive the shell that launched
    them), waits until each actually answers, and reports the outcome. It is
    idempotent: a server that is already listening is left alone.

    Logs go to %TEMP%\ntqms-dev\ so the repo stays clean.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-up.ps1
#>
[CmdletBinding()]
param(
    # Skip the frontend and bring up the API only.
    [switch]$ApiOnly,
    # Seconds to wait for each server to answer before giving up.
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$repo    = Split-Path -Parent $PSScriptRoot
$dotnet  = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = Join-Path $env:LOCALAPPDATA 'Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\Microsoft\dotnet\dotnet.exe'
}
if (-not (Test-Path $dotnet)) {
    $found = Get-ChildItem -Path "$env:LOCALAPPDATA\Packages" -Filter "dotnet.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if ($found) { $dotnet = $found }
}
$npm     = 'C:\Program Files\nodejs\npm.cmd'
$logDir  = Join-Path $env:TEMP 'ntqms-dev'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

function Test-Port([int]$Port) {
    [bool](Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue | Where-Object { $_.State -eq 'Listen' })
}

function Wait-Http([string]$Url, [int]$Seconds, [string]$LogPath) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri $Url -TimeoutSec 3 -UseBasicParsing
            if ($r.StatusCode -eq 200) { return $true }
        } catch { }
        Start-Sleep -Seconds 2
    }
    Write-Host "  timed out after ${Seconds}s. Last 15 log lines:" -ForegroundColor Yellow
    if (Test-Path $LogPath) { Get-Content $LogPath -Tail 15 | ForEach-Object { "      $_" } }
    return $false
}

Write-Host "NT.QMS dev stack" -ForegroundColor Cyan
Write-Host "  repo: $repo"
Write-Host "  logs: $logDir"

# ---------------------------------------------------------------- API (:5080)
if (Test-Port 5080) {
    Write-Host "`nAPI    :5080  already listening - left running" -ForegroundColor Green
} else {
    if (-not (Test-Path $dotnet)) { throw "dotnet not found at $dotnet (see CLAUDE.md section 6)" }

    # The API is launched with --no-build for speed; build first if it has never
    # been built, otherwise 'dotnet run --no-build' fails with a missing assembly.
    $apiDll = Join-Path $repo 'src\NT.QAMS.WebApi\bin\Debug\net9.0\NT.QAMS.WebApi.dll'
    if (-not (Test-Path $apiDll)) {
        Write-Host "`nAPI    :5080  no build output yet - building first (one time)..." -ForegroundColor Yellow
        & $dotnet build (Join-Path $repo 'src\NT.QAMS.WebApi') -c Debug | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "build failed - run scripts\dev-rebuild.ps1 to see the errors" }
    }

    $apiLog = Join-Path $logDir 'api.log'
    Write-Host "`nAPI    :5080  starting..." -NoNewline

    # Env vars are set on this shell only; Start-Process inherits them, and the
    # child keeps running after this script (and its shell) exits.
    $env:ASPNETCORE_ENVIRONMENT     = 'Development'
    $env:ASPNETCORE_URLS            = 'http://0.0.0.0:5080'
    $env:Database__MigrateOnStartup = 'true'
    if (-not $env:ConnectionStrings__Postgres) {
        $env:ConnectionStrings__Postgres = 'Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local'
    }
    if (-not $env:Jwt__Secret) {
        $env:Jwt__Secret = 'DevOnlySecretKeyAtLeast48CharactersLongForNTQAMSSystem1234567890!'
    }
    if (-not $env:PlatformAdmin__Email) {
        $env:PlatformAdmin__Email = 'platform-admin@localhost'
    }
    if (-not $env:PlatformAdmin__Password) {
        $env:PlatformAdmin__Password = 'Dev-Only-Platform-Pass-1!'
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $dotnet
    $psi.Arguments = "exec `"$apiDll`""
    $psi.WorkingDirectory = $repo
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    [System.Diagnostics.Process]::Start($psi) | Out-Null

    if (Wait-Http 'http://localhost:5080/health/ready' $TimeoutSeconds $apiLog) {
        Write-Host " ready (health/ready 200)" -ForegroundColor Green
    } else {
        Write-Host "  API did not become ready - see $apiLog" -ForegroundColor Red
    }
}

# ----------------------------------------------------------- Frontend (:4200)
if ($ApiOnly) {
    Write-Host "`nSPA    :4200  skipped (-ApiOnly)" -ForegroundColor DarkGray
} elseif (Test-Port 4200) {
    Write-Host "SPA    :4200  already listening - left running" -ForegroundColor Green
} else {
    if (-not (Test-Path $npm)) { throw "npm not found at $npm - Angular 22 needs Node >= 20.19 (system Node 24 expected)" }

    $feLog = Join-Path $logDir 'frontend.log'
    Write-Host "SPA    :4200  starting..." -NoNewline

    Start-Process -FilePath $npm `
        -ArgumentList @('start','--prefix','frontend','--','--host','0.0.0.0','--disable-host-check','--proxy-config','proxy.conf.json') `
        -WorkingDirectory $repo -WindowStyle Hidden `
        -RedirectStandardOutput $feLog -RedirectStandardError (Join-Path $logDir 'frontend.err.log') | Out-Null

    if (Wait-Http 'http://localhost:4200/' $TimeoutSeconds $feLog) {
        Write-Host " ready" -ForegroundColor Green
    } else {
        Write-Host "  SPA did not become ready - see $feLog" -ForegroundColor Red
    }
}

Write-Host "`nOpen: http://localhost:4200/t/demo-lab" -ForegroundColor Cyan
Write-Host "Status: scripts\dev-status.ps1   Stop: scripts\dev-down.ps1   After code changes: scripts\dev-rebuild.ps1"
