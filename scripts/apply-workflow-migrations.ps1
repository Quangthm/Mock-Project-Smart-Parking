param(
    [Parameter(Mandatory)][ValidateSet('User','Parking','Reservation')][string]$Service,
    [Parameter(Mandatory)][string]$SchemaRoot
)
$ErrorActionPreference = 'Stop'
$codeRoot = Split-Path $PSScriptRoot -Parent
$composeFile = Join-Path $SchemaRoot 'compose.schema-integration.yml'
if (!(Test-Path -LiteralPath $composeFile -PathType Leaf)) { throw 'SchemaRoot must contain the canonical schema compose file.' }
$database = @{ User='smartpark_user'; Parking='smartpark_parking'; Reservation='smartpark_reservation' }[$Service]
$container = @{ User='user-db'; Parking='parking-db'; Reservation='reservation-db' }[$Service]
$migration = @{ User='05.8-Account-Workflow-Vehicles.sql'; Parking='05.9-Parking-Backup-Operations.sql'; Reservation='05.10-Reservation-Confirmation-Time.sql' }[$Service]
$required = @{ User=@('users','accounts','vehicles'); Parking=@('parking_sites','spatial_units','parking_slots'); Reservation=@('reservations','structure_edit_holds','site_capacity_pools') }[$Service]
foreach ($table in $required) {
    $exists = & docker compose -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1 -At -c "SELECT to_regclass('$table') IS NOT NULL"
    if ($LASTEXITCODE -ne 0 -or ($exists -join '').Trim() -ne 't') { throw "Missing required $Service table $table; apply the canonical baseline and overlay first." }
}
$sql = Get-Content -LiteralPath (Join-Path $codeRoot "scripts/database/$migration") -Raw
$sql | & docker compose -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw "Migration failed for $Service; its transaction was rolled back. Reconcile conflicting data before retrying." }
Write-Output "$Service workflow migration applied."
