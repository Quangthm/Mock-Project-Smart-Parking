param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$projectName = 'smartpark-upgrade-test-' + [Guid]::NewGuid().ToString('N')
$fixtureRoot = Join-Path $repoRoot ('.cache/' + $projectName)
New-Item -ItemType Directory -Force -Path $fixtureRoot | Out-Null
$composeFile = Join-Path $fixtureRoot 'compose.schema-integration.yml'
$envFile = Join-Path $fixtureRoot '.env'
[IO.File]::WriteAllText($envFile, "SMARTPARK_DEV_PASSWORD=isolated-upgrade-test-password`n")
$compose = @("name: $projectName", 'services:')
# Empty fixture databases: the runner must apply the full canonical set on the
# first run, then detect the already-current schema and skip on the second run.
foreach ($service in @('User','Parking','Reservation')) {
    $name = $service.ToLowerInvariant()
    $compose += @("  ${name}-db:", '    image: postgres:16-alpine', '    environment:', "      POSTGRES_USER: smartpark_$name", "      POSTGRES_DB: smartpark_$name", '      POSTGRES_PASSWORD: ${SMARTPARK_DEV_PASSWORD:?missing}', '    volumes:', "      - ${name}-data:/var/lib/postgresql/data", '    healthcheck:', "      test: [CMD-SHELL, 'pg_isready -U smartpark_$name -d smartpark_$name']", '      interval: 1s', '      timeout: 3s', '      retries: 60')
}
$compose += @('volumes:', '  user-data:', '  parking-data:', '  reservation-data:')
[IO.File]::WriteAllLines($composeFile, $compose, (New-Object Text.UTF8Encoding($false)))
function Invoke-TestCompose([string[]]$arguments) {
    & docker compose --project-name $projectName --env-file $envFile -f $composeFile @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Isolated Compose command failed.' }
}
function Query-TestDatabase($service, $sql) {
    $name = $service.ToLowerInvariant()
    $result = & docker compose --project-name $projectName --env-file $envFile -f $composeFile exec -T "${name}-db" psql -U "smartpark_$name" -d "smartpark_$name" -v ON_ERROR_STOP=1 -At -c $sql
    if ($LASTEXITCODE -ne 0) { throw 'Isolated database query failed.' }
    return ($result -join '').Trim()
}
try {
    Invoke-TestCompose @('up','-d','--wait')
    foreach ($service in @('User','Parking','Reservation')) {
        if ((Query-TestDatabase $service "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'") -ne '0') { throw 'Fixture database is not empty.' }
    }
    # First run applies the canonical scripts; second run must skip (already current).
    # Deliberately run from another working directory to prove explicit EnvPath resolution.
    Push-Location $env:TEMP
    try {
        for ($run = 0; $run -lt 2; $run++) {
            foreach ($service in @('User','Parking','Reservation')) {
                & (Join-Path $repoRoot 'scripts/apply-workflow-migrations.ps1') -Service $service -SchemaRoot $fixtureRoot -EnvPath $envFile -ProjectName $projectName
            }
        }
    } finally { Pop-Location }
    foreach ($service in @('User','Parking','Reservation')) {
        $null = Query-TestDatabase $service 'CREATE TABLE setup_upgrade_sentinel (id integer PRIMARY KEY); INSERT INTO setup_upgrade_sentinel VALUES (42);'
    }
    Invoke-TestCompose @('down')
    Invoke-TestCompose @('up','-d','--wait')
    foreach ($service in @('User','Parking','Reservation')) {
        if ((Query-TestDatabase $service 'SELECT count(*) FROM setup_upgrade_sentinel WHERE id=42') -ne '1') { throw 'Restart lost existing data.' }
    }
    if ((Query-TestDatabase User "SELECT to_regclass('uq_active_vehicle_plate') IS NOT NULL") -ne 't') { throw 'User canonical schema missing after restart.' }
    if ((Query-TestDatabase Parking "SELECT to_regclass('structure_synchronizations') IS NOT NULL") -ne 't') { throw 'Parking overlay tables missing after restart.' }
    if ((Query-TestDatabase Parking "SELECT to_regclass('tenants') IS NOT NULL") -ne 't') { throw 'Parking tenants overlay missing after restart.' }
    if ((Query-TestDatabase Reservation "SELECT to_regclass('structure_release_receipts') IS NOT NULL") -ne 't') { throw 'Reservation overlay tables missing after restart.' }
    if ((Query-TestDatabase Reservation "SELECT to_regclass('site_capacity_pools') IS NOT NULL") -ne 't') { throw 'Reservation capacity pools overlay missing after restart.' }
    Write-Output 'PASS: empty-fixture canonical apply, second-run skip, explicit env from another cwd, preserved data across existing-volume restart.'
} finally {
    # Delete only the uniquely named test Compose project/volumes created by this script.
    if ($projectName -notmatch '^smartpark-upgrade-test-[0-9a-f]{32}$' -or
        !(Resolve-Path -LiteralPath $fixtureRoot).Path.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup target.' }
    Invoke-TestCompose @('down','--volumes')
}
