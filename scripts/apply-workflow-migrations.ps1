param(
    [Parameter(Mandatory)][ValidateSet('User','Parking','Reservation')][string]$Service,
    [Parameter(Mandatory)][string]$SchemaRoot,
    [string]$EnvPath,
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$ProjectName = 'smartpark-schema'
)
$ErrorActionPreference = 'Stop'
$codeRoot = Split-Path $PSScriptRoot -Parent
$composeFile = Join-Path $SchemaRoot 'compose.schema-integration.yml'
if (!(Test-Path -LiteralPath $composeFile -PathType Leaf)) { throw 'SchemaRoot must contain the canonical schema compose file.' }
if (!$EnvPath) { $EnvPath = Join-Path $SchemaRoot '.env' }
if (!(Test-Path -LiteralPath $EnvPath -PathType Leaf)) { throw 'Migration requires the environment file for this schema checkout.' }
$null = & (Join-Path $codeRoot 'scripts/load-env.ps1') -EnvPath $EnvPath
$database = @{ User='smartpark_user'; Parking='smartpark_parking'; Reservation='smartpark_reservation' }[$Service]
$container = @{ User='user-db'; Parking='parking-db'; Reservation='reservation-db' }[$Service]
$migration = @{ User='05.8-Account-Workflow-Vehicles.sql'; Parking='05.9-Parking-Backup-Operations.sql'; Reservation='05.10-Reservation-Confirmation-Time.sql' }[$Service]
$required = @{ User=@('users','accounts','vehicles'); Parking=@('parking_sites','spatial_units','parking_slots'); Reservation=@('reservations','structure_edit_holds','site_capacity_pools') }[$Service]
foreach ($table in $required) {
    $exists = & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1 -At -c "SELECT to_regclass('$table') IS NOT NULL"
    if ($LASTEXITCODE -ne 0 -or ($exists -join '').Trim() -ne 't') { throw "Missing required $Service table $table; apply the canonical baseline and overlay first." }
}
$sql = Get-Content -LiteralPath (Join-Path $codeRoot "scripts/database/$migration") -Raw
$sql | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw "Migration failed for $Service; its transaction was rolled back. Reconcile conflicting data before retrying." }

if ($Service -in @('User','Parking','Reservation')) {
    $additionalMigration = @{
        User='05.13-Mfa-Lifecycle.sql'
        Parking='05.11-Parking-Structure-Synchronization.sql'
        Reservation='05.12-Reservation-Structure-Receipts.sql'
    }[$Service]
    $serviceSql = Get-Content -LiteralPath (Join-Path $codeRoot "scripts/database/$additionalMigration") -Raw
    $serviceSql | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "Structure synchronization migration failed for $Service." }
}
if ($Service -eq 'User') {
    $identitySql = Get-Content -LiteralPath (Join-Path $codeRoot 'scripts/database/05.14-Identity-Access-Hardening.sql') -Raw
    $identitySql | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Identity access hardening migration failed.' }
}
Write-Output "$Service workflow migrations applied."
