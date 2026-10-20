param(
    [Parameter(Mandatory)][ValidateSet('User','Parking','Reservation','Payment','Notification','Iot')][string]$Service,
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

# Ordered script lists mirroring compose.schema-integration.yml init order
# (numeric container names force postgres /docker-entrypoint-initdb.d order).
$scripts = @{
    User = @(
        'microservices/01-user-access-service-db.sql'
    )
    Parking = @(
        'microservices/02-facility-service-db.sql',
        'microservices/03a-parking-structure-ops-service-db.sql',
        'overlay/parking-tenants.sql',
        'overlay/parking-structure-synchronizations.sql',
        'overlay/parking-operator-tables.sql',
        'overlay/parking-slot-constraints.sql'
    )
    Reservation = @(
        'microservices/03b-reservation-operations-service-db.sql',
        'overlay/reservation-site-capacity-pools.sql',
        'overlay/reservation-structure-safety.sql',
        'overlay/reservation-structure-receipts.sql',
        'overlay/reservation-confirmation-time.sql'
    )
    Payment = @(
        'microservices/04-payment-pricing-service-db.sql'
    )
    Notification = @(
        'microservices/05-notification-audit-service-db.sql'
    )
    Iot = @(
        'microservices/06-iot-recognition-service-db.sql'
    )
}[$Service]
$database = @{ User='smartpark_user'; Parking='smartpark_parking'; Reservation='smartpark_reservation'; Payment='smartpark_payment'; Notification='smartpark_notification'; Iot='smartpark_iot' }[$Service]
$container = @{ User='user-db'; Parking='parking-db'; Reservation='reservation-db'; Payment='payment-db'; Notification='notification-db'; Iot='iot-db' }[$Service]
$required = @{
    User = @('users','accounts','roles','vehicles')
    Parking = @('parking_sites','tenants','structure_synchronizations','support_tickets','operator_audit_logs')
    Reservation = @('reservations','structure_edit_holds','site_capacity_pools','structure_release_receipts')
    Payment = @()
    Notification = @()
    Iot = @()
}[$Service]
# Sentinel relation created only by the canonical scripts (base or overlay) for
# this database. Index for User (partial unique on normalized_plate), table elsewhere.
$sentinel = @{ User='uq_active_vehicle_plate'; Parking='tenants'; Reservation='site_capacity_pools'; Payment='payment_orders'; Notification='notification_templates'; Iot='devices' }[$Service]

# Block list: legacy 05.x workflow scripts and integration/* alignment scripts are
# superseded by the new base files + overlays; applying them would double-apply or conflict.
$blockedPattern = '(^|[\\/])05\.[1-9][^\\/]*\.sql$|(^|[\\/])integration[\\/]0[123][^\\/]*\.sql$'
foreach ($script in $scripts) {
    $relative = "scripts/database/$script"
    if ($relative -match $blockedPattern) {
        throw "Blocked migration script '$relative': its content is superseded by the new base files and overlays. Use the canonical microservices/overlay scripts only."
    }
    if (!(Test-Path -LiteralPath (Join-Path $codeRoot $relative) -PathType Leaf)) {
        throw "Migration script not found: $relative."
    }
}

# State probe: compose init already ran the same scripts on a fresh volume, so
# re-applying them fails with "relation ... already exists". Detect the database
# state first and only apply scripts to a truly empty database.
$probeSql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relname = '$sentinel') THEN 'current' WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public') THEN 'legacy' ELSE 'empty' END"
$state = (& docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1 -At -c $probeSql | Select-Object -Last 1)
if ($LASTEXITCODE -ne 0 -or $state -notin @('current','legacy','empty')) {
    throw "Cannot determine the schema state of the $Service database. Check the $container container logs; no data was changed."
}
if ($state -eq 'legacy') {
    throw "$Service database uses a legacy schema layout (canonical sentinel '$sentinel' is missing while public tables exist). Run .\setup.ps1 -RecreateDb to rebuild all six databases from the canonical scripts; setup never drops existing data on its own."
}

if ($state -eq 'empty') {
    foreach ($script in $scripts) {
        $relative = "scripts/database/$script"
        $sql = Get-Content -LiteralPath (Join-Path $codeRoot $relative) -Raw
        $sql | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) { throw "Migration failed for $Service at $relative; its transaction was rolled back. Reconcile conflicting data before retrying." }
    }
}

# Verify required relations for both paths: after applying to an empty database,
# and when skipping because the schema is already current.
foreach ($table in (@($required) + @($sentinel))) {
    $exists = & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1 -At -c "SELECT to_regclass('$table') IS NOT NULL"
    if ($LASTEXITCODE -ne 0 -or ($exists -join '').Trim() -ne 't') { throw "Missing required $Service relation $table; the database does not match the canonical schema. Run .\setup.ps1 -RecreateDb to rebuild it from the canonical scripts." }
}
if ($state -eq 'current') {
    Write-Output "$Service schema already current; verified required relations, nothing to apply."
} else {
    Write-Output "$Service workflow migrations applied."
}
