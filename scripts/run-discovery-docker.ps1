#requires -Version 7.0
param([switch]$NoBuild, [switch]$BuildOnly, [switch]$ReplaceLegacyContainers)
$ErrorActionPreference = 'Stop'
if ($NoBuild -and $BuildOnly) { throw '-NoBuild and -BuildOnly cannot be combined.' }
$root = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $root '.env'
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile -NoExport
. (Join-Path $PSScriptRoot 'discovery-docker.ps1')
$discovery = Get-DiscoveryDockerEnvironment $settings $root
if ($discovery.Count -eq 0) { throw 'Run pwsh -File scripts/initialize-discovery-docker.ps1 first.' }
function Invoke-DiscoveryDocker {
    param([string[]]$Arguments, [switch]$Capture)
    if ($Capture) {
        $output = @(& docker @Arguments 2>&1)
        if ($LASTEXITCODE -ne 0) { throw 'Docker check failed. Inspect the service locally; captured output is not printed to protect secrets.' }
        return ($output -join "`n")
    }
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Docker command failed.' }
}
$endpoint = $env:DOCKER_HOST
if ($env:DOCKER_CONTEXT -or !$endpoint) {
    $contextArgs = @('context','inspect','--format','{{.Endpoints.docker.Host}}')
    if ($env:DOCKER_CONTEXT) { $contextArgs += $env:DOCKER_CONTEXT }
    $endpoint = Invoke-DiscoveryDocker -Arguments $contextArgs -Capture
}
if ($endpoint -notmatch '^(npipe:////\./pipe/|unix:///)') { throw 'Use a local Docker Desktop named pipe or Unix socket.' }
$compose = @('compose','--project-name','smartpark-app','--env-file',$envFile,'-f',(Join-Path $root 'compose.app.yml'),'-f',(Join-Path $root 'compose.discovery.yml'))
$previous = @{}
try {
    foreach ($key in $discovery.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key,'Process')
        [Environment]::SetEnvironmentVariable($key,[string]$discovery[$key],'Process')
    }
    $null = Invoke-DiscoveryDocker -Arguments ($compose + @('config','--quiet')) -Capture
    if (!$NoBuild) { Invoke-DiscoveryDocker -Arguments ($compose + @('build','facility-service','operation-service','payment-service')) }
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}
if ($BuildOnly) { Write-Host 'Discovery images built. No containers stopped and no database changes.'; return }
$db = (Invoke-DiscoveryDocker -Arguments @('ps','--quiet','--filter','label=com.docker.compose.project=smartpark-schema','--filter','label=com.docker.compose.service=facility-db') -Capture).Trim()
if (!$db -or $db -match '\s') { throw 'Expected exactly one running smartpark-schema Facility database.' }
$sql = "SELECT CASE WHEN to_regclass('facility_discovery_version') IS NOT NULL AND to_regclass('facility_discovery_publications') IS NOT NULL THEN 'ready' ELSE 'missing' END;"
$schema = (Invoke-DiscoveryDocker -Arguments @('exec',$db,'psql','-U','smartpark_facility','-d','smartpark_facility','-At','-v','ON_ERROR_STOP=1','-c',$sql) -Capture).Trim()
if ($schema -cne 'ready') {
    throw 'Facility discovery schema missing. Back up and obtain approval for scripts/database/migrations/20261106_facility_discovery_publication.sql. No containers were stopped; no migration was applied.'
}
$legacy = @()
foreach ($service in @('parking-service','reservation-service')) {
    $id = (Invoke-DiscoveryDocker -Arguments @('ps','--quiet','--filter','label=com.docker.compose.project=smartpark-app','--filter',("label=com.docker.compose.service=$service")) -Capture).Trim()
    if ($id) {
        if ($id -match '\s') { throw 'Multiple legacy containers found; inspect before continuing.' }
        $legacy += $id
    }
}
if ($legacy.Count -gt 0 -and !$ReplaceLegacyContainers) { throw 'Legacy Facility/Operations containers own the host ports. Use -ReplaceLegacyContainers to stop only those two app services, retaining them and all database volumes.' }
foreach ($id in $legacy) { $null = Invoke-DiscoveryDocker -Arguments @('stop','--time','30',$id) -Capture }
# User and frontend images are prerequisites from the normal setup. Ensure they
# are running too, so this command also works after Docker Desktop restarts.
& (Join-Path $PSScriptRoot 'run-app-service.ps1') -Service User -NoBuild -NoLogWindow
foreach ($service in @('Facility','Operation','Payment')) {
    & (Join-Path $PSScriptRoot 'run-app-service.ps1') -Service $service -NoBuild -NoLogWindow
}
& (Join-Path $PSScriptRoot 'run-app-service.ps1') -Service Assistant -NoBuild:$NoBuild -NoLogWindow
$assistant = (Invoke-DiscoveryDocker -Arguments @('ps','--quiet','--filter','label=com.docker.compose.project=smartpark-app','--filter','label=com.docker.compose.service=assistant') -Capture).Trim()
if (!$assistant -or $assistant -match '\s') { throw 'Expected one running Assistant.' }
foreach ($service in @('facility-service','operation-service','payment-service')) {
    $status = Invoke-DiscoveryDocker -Arguments @('exec',$assistant,'curl','--silent','--show-error','--output','/dev/null','--write-out','%{http_code}',"https://${service}:8443/health") -Capture
    if ($status -cne '200') { throw "HTTPS health check failed: $service." }
}
$json = Invoke-DiscoveryDocker -Arguments @('exec',$assistant,'curl','--silent','--show-error','--fail-with-body','https://operation-service:8443/api/parking-search?lat=10.77&lon=106.70&radiusKm=5&vehicleType=CAR') -Capture
$response = $json | ConvertFrom-Json
if (!$response.success -or $null -eq $response.data.items) { throw 'Search did not return a successful typed result. Search activation was not performed.' }
# Activate only after a real authenticated-transport owner search succeeds.
$lines = [IO.File]::ReadAllLines($envFile)
$found = $false
$updated = @(foreach ($line in $lines) {
    if ($line -match '^\s*Assistant__Reads__Search__Enabled\s*=') {
        if (!$found) { 'Assistant__Reads__Search__Enabled=true'; $found = $true }
    } else { $line }
})
if (!$found) { $updated += 'Assistant__Reads__Search__Enabled=true' }
[IO.File]::WriteAllLines($envFile,$updated,[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'run-app-service.ps1') -Service Assistant -NoBuild -NoLogWindow
& (Join-Path $PSScriptRoot 'run-app-service.ps1') -Service Frontend -NoBuild -NoLogWindow
Write-Host ('Discovery HTTPS search verified; public matches in probe: ' + @($response.data.items).Count)
Write-Host 'Assistant Search enabled. Existing unpublished sites remain private; unknown availability remains unknown.'
Write-Host 'Next UI check as Driver: Tim bai o to gan 10.77,106.70 radius=5 (Vietnamese accents are supported).'
