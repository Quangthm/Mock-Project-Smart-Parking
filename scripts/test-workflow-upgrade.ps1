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
$schemas = @{
    User = @('microservices/01-user-service-db.sql','integration/01-user-service-alignment.sql','05.8-Account-Workflow-Vehicles.sql')
    Parking = @('microservices/02-parking-service-db.sql','integration/02-parking-service-alignment.sql','05.9-Parking-Backup-Operations.sql')
    Reservation = @('microservices/03-reservation-session-service-db.sql','integration/03-reservation-structure-safety.sql','05.10-Reservation-Confirmation-Time.sql')
}
foreach ($service in @('User','Parking','Reservation')) {
    $name = $service.ToLowerInvariant()
    $compose += @("  ${name}-db:", '    image: postgres:16-alpine', '    environment:', "      POSTGRES_USER: smartpark_$name", "      POSTGRES_DB: smartpark_$name", '      POSTGRES_PASSWORD: ${SMARTPARK_DEV_PASSWORD:?missing}', '    volumes:', "      - ${name}-data:/var/lib/postgresql/data")
    $index = 0
    foreach ($schema in $schemas[$service]) {
        $path = (Join-Path $repoRoot "scripts/database/$schema").Replace('\','/')
        $compose += "      - '${path}:/docker-entrypoint-initdb.d/0$index.sql:ro'"
        $index++
    }
    $compose += @('    healthcheck:', "      test: [CMD-SHELL, 'pg_isready -U smartpark_$name -d smartpark_$name']", '      interval: 1s', '      timeout: 3s', '      retries: 60')
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
        $null = Query-TestDatabase $service 'CREATE TABLE setup_upgrade_sentinel (id integer PRIMARY KEY); INSERT INTO setup_upgrade_sentinel VALUES (42);'
    }
    if ((Query-TestDatabase Parking "SELECT to_regclass('structure_synchronizations') IS NULL") -ne 't') { throw 'Fixture did not represent pre-05.11 state.' }
    if ((Query-TestDatabase Reservation "SELECT to_regclass('structure_release_receipts') IS NULL") -ne 't') { throw 'Fixture did not represent pre-05.12 state.' }
    # Deliberately run from another working directory to prove explicit EnvPath resolution.
    Push-Location $env:TEMP
    try {
        for ($run = 0; $run -lt 2; $run++) {
            foreach ($service in @('User','Parking','Reservation')) {
                & (Join-Path $repoRoot 'scripts/apply-workflow-migrations.ps1') -Service $service -SchemaRoot $fixtureRoot -EnvPath $envFile -ProjectName $projectName
            }
        }
    } finally { Pop-Location }
    Invoke-TestCompose @('down')
    Invoke-TestCompose @('up','-d','--wait')
    foreach ($service in @('User','Parking','Reservation')) {
        if ((Query-TestDatabase $service 'SELECT count(*) FROM setup_upgrade_sentinel WHERE id=42') -ne '1') { throw 'Upgrade/restart lost existing data.' }
    }
    if ((Query-TestDatabase Parking "SELECT to_regclass('structure_synchronizations') IS NOT NULL") -ne 't') { throw '05.11 missing after restart.' }
    if ((Query-TestDatabase Reservation "SELECT to_regclass('structure_release_receipts') IS NOT NULL") -ne 't') { throw '05.12 missing after restart.' }
    Write-Output 'PASS: pre-05.11/05.12 upgrade, repeated migrations, explicit env from another cwd, preserved data and existing-volume restart.'
} finally {
    # Delete only the uniquely named test Compose project/volumes created by this script.
    if ($projectName -notmatch '^smartpark-upgrade-test-[0-9a-f]{32}$' -or
        !(Resolve-Path -LiteralPath $fixtureRoot).Path.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup target.' }
    Invoke-TestCompose @('down','--volumes')
}
