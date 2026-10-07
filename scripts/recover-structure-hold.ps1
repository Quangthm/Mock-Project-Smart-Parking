param(
    [Parameter(Mandatory)][guid]$SiteId,
    [Parameter(Mandatory)][switch]$ParkingStopped,
    [switch]$Apply,
    [string]$ReservationUrl = 'http://localhost:5055',
    [string]$SchemaRoot = $env:SMARTPARK_SCHEMA_ROOT
)
$ErrorActionPreference = 'Stop'
if (!$ParkingStopped) { throw 'Stop every Parking API instance first, then specify -ParkingStopped.' }
$taskRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($SchemaRoot)) { $SchemaRoot = $taskRoot }
$composeFile = Join-Path $SchemaRoot 'compose.schema-integration.yml'
if (!(Test-Path -LiteralPath $composeFile -PathType Leaf)) {
    throw 'Set SMARTPARK_SCHEMA_ROOT or -SchemaRoot to the arch/database-schema checkout containing compose.schema-integration.yml.'
}
function Read-Database([string]$service, [string]$dbName, [string]$sql) {
    $result = & docker compose -f $composeFile exec -T $service psql -U $dbName -d $dbName -v ON_ERROR_STOP=1 -At -c $sql
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect $service." }
    return ($result -join "`n")
}
# Fail if even an idle Parking connection remains: a paused request could still commit later.
$connections = Read-Database 'parking-db' 'smartpark_parking' "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid()"
if ([int]$connections -ne 0) { throw 'Parking DB still has other connections. Stop API instances and wait for connections to close before recovery.' }
$hold = Read-Database 'reservation-db' 'smartpark_reservation' "SELECT row_to_json(h) FROM structure_edit_holds h WHERE site_id='$SiteId'::uuid"
if (!$hold) { Write-Output 'No pending hold for this site.'; return }
$pending = $hold | ConvertFrom-Json
$snapshotSql = @"
WITH site AS (SELECT * FROM parking_sites WHERE id='$SiteId'::uuid),
units AS (SELECT * FROM spatial_units WHERE site_id='$SiteId'::uuid AND deleted_at IS NULL),
slots AS (SELECT * FROM parking_slots WHERE site_id='$SiteId'::uuid AND deleted_at IS NULL),
capacities AS (
    SELECT NULL::uuid AS unit_id, v.vehicle_type,
        CASE WHEN (SELECT deleted_at IS NULL FROM site) THEN
            (SELECT count(*)::int FROM slots WHERE supported_vehicle_type=v.vehicle_type) ELSE 0 END AS capacity
    FROM (VALUES ('CAR'),('MOTORCYCLE')) v(vehicle_type)
    UNION ALL
    SELECT u.id, v.vehicle_type,
        CASE WHEN (SELECT deleted_at IS NULL FROM site) THEN
            (SELECT count(*)::int FROM slots s JOIN units child ON child.id=s.spatial_unit_id
             WHERE s.supported_vehicle_type=v.vehicle_type AND child.path LIKE u.path || '%') ELSE 0 END
    FROM units u CROSS JOIN (VALUES ('CAR'),('MOTORCYCLE')) v(vehicle_type)
)
SELECT jsonb_build_object('tenantId', tenant_id, 'outcome', jsonb_build_object(
    'siteActive', status='ACTIVE' AND deleted_at IS NULL,
    'removedSlots', coalesce((SELECT jsonb_agg(id) FROM parking_slots WHERE site_id=site.id AND deleted_at IS NOT NULL),'[]'::jsonb),
    'removedUnits', coalesce((SELECT jsonb_agg(id) FROM spatial_units WHERE site_id=site.id AND deleted_at IS NOT NULL),'[]'::jsonb),
    'capacities', coalesce((SELECT jsonb_agg(jsonb_build_object('unitId',unit_id,'vehicleType',vehicle_type,'capacity',capacity)) FROM capacities),'[]'::jsonb)
)) FROM site;
"@
$snapshot = Read-Database 'parking-db' 'smartpark_parking' $snapshotSql
if (!$snapshot) { throw 'Site missing in authoritative Parking DB; manual investigation required.' }
$body = $snapshot | ConvertFrom-Json
if ([guid]$body.tenantId -ne [guid]$pending.tenant_id) { throw 'Hold tenant differs from Parking site tenant; manual investigation required.' }
Write-Output "Hold: $($pending.token); site: $SiteId; created: $($pending.created_at)"
Write-Output $snapshot
if (!$Apply) { Write-Output 'Inspection only. Review the snapshot, then repeat with -Apply while Parking remains stopped.'; return }
if (!$env:SMARTPARK_SERVICE_KEY -or $env:SMARTPARK_SERVICE_KEY.Length -lt 32) { throw 'Set SMARTPARK_SERVICE_KEY.' }
# This publishes authoritative projections and capacities before releasing; never use outcome=null here.
Invoke-RestMethod -Method Post -Uri "$($ReservationUrl.TrimEnd('/'))/internal/structure/$SiteId/$($pending.token)/release" -Headers @{ 'X-Service-Key'=$env:SMARTPARK_SERVICE_KEY } -ContentType 'application/json' -Body $snapshot | Out-Null
Write-Output 'Reconciled and released. Parking API can now restart.'
