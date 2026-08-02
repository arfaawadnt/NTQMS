<#
.SYNOPSIS
    Stop the API, build (or test), then bring the API back up.

.DESCRIPTION
    The single biggest cause of "the app stopped working": on Windows the running
    WebApi holds a lock on its own DLLs, so 'dotnet build', 'dotnet test' and
    'dotnet ef' all fail unless the API is stopped first (CLAUDE.md section 5).
    Stopping it by hand and forgetting to restart is what left the app down.

    This wraps the whole cycle so the API is always restarted, including when the
    build or tests fail.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-rebuild.ps1
    Stop API, build the solution, restart the API.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev-rebuild.ps1 -Test
    Stop API, build, run the full test suite against the real dev database, restart the API.
#>
[CmdletBinding()]
param(
    # Also run the full test suite (needs the real dev PostgreSQL).
    [switch]$Test,
    # Apply pending EF migrations before restarting.
    [switch]$Migrate
)

$repo   = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = Join-Path $env:LOCALAPPDATA 'Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\Microsoft\dotnet\dotnet.exe'
}
if (-not (Test-Path $dotnet)) {
    $found = Get-ChildItem -Path "$env:LOCALAPPDATA\Packages" -Filter "dotnet.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if ($found) { $dotnet = $found }
}
$failed = $false

Write-Host "1/4  stopping the API so its DLLs are unlocked" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'dev-down.ps1') -ApiOnly
Start-Sleep -Seconds 2

Write-Host "`n2/4  building" -ForegroundColor Cyan
& $dotnet build (Join-Path $repo 'NT.QAMS.sln') -c Debug
if ($LASTEXITCODE -ne 0) { $failed = $true; Write-Host "BUILD FAILED" -ForegroundColor Red }

if ($Migrate -and -not $failed) {
    Write-Host "`n2b/4 applying EF migrations" -ForegroundColor Cyan
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    & $dotnet ef database update --project (Join-Path $repo 'src\NT.QAMS.Infrastructure') `
                                 --startup-project (Join-Path $repo 'src\NT.QAMS.WebApi')
    if ($LASTEXITCODE -ne 0) { $failed = $true; Write-Host "MIGRATION FAILED" -ForegroundColor Red }
}

if ($Test -and -not $failed) {
    Write-Host "`n3/4  running the full suite (real PostgreSQL)" -ForegroundColor Cyan
    $env:QMS_ITEST_POSTGRES = 'Host=localhost;Database=ntqams;Username=qams_app;Password=dev-only-local'
    & $dotnet test (Join-Path $repo 'NT.QAMS.sln') --no-build
    if ($LASTEXITCODE -ne 0) { $failed = $true; Write-Host "TESTS FAILED" -ForegroundColor Red }
} elseif (-not $Test) {
    Write-Host "`n3/4  tests skipped (pass -Test to run them)" -ForegroundColor DarkGray
}

# Always restart, even on failure: leaving the app down is the failure mode this
# script exists to prevent.
Write-Host "`n4/4  restarting the API" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'dev-up.ps1') -ApiOnly

if ($failed) {
    Write-Host "`nFinished WITH FAILURES above - the API was restarted anyway." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll good." -ForegroundColor Green
