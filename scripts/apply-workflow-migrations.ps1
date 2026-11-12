param(
    [Parameter(Mandatory)][ValidateSet('User','Parking','Operation','Reservation','Payment','Notification','Iot')][string]$Service,
    [Parameter(Mandatory)][string]$SchemaRoot,
    [string]$EnvPath,
    [switch]$PlanAssistant,
    [switch]$UpgradeFacilityMaintenance,
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$ProjectName = 'smartpark-schema'
)
$ErrorActionPreference = 'Stop'
# Existing schema maps retain Reservation as their compatibility key.
if ($Service -eq 'Operation') { $Service = 'Reservation' }
if ($UpgradeFacilityMaintenance -and ($Service -ne 'Parking' -or $PlanAssistant)) {
    throw '-UpgradeFacilityMaintenance requires -Service Parking and cannot be combined with -PlanAssistant.'
}
$codeRoot = Split-Path $PSScriptRoot -Parent
# Approval artifact only. No env import, Docker/DB connection, SQL application,
# or assertion that migrations are pending/already applied to a live database.
if ($PlanAssistant) {
    if ($Service -notin @('Iot','Parking')) { throw '-PlanAssistant supports only Iot or Parking (Facility DB).' }
    $files = if ($Service -eq 'Iot') {
        @('20261103_iot_assistant_management.sql','20261105_iot_assistant_s02.sql','20261105_iot_assistant_s05.sql','20261110_iot_assistant_current_chat.sql')
    } else { @('20261106_facility_discovery_publication.sql') }
    $target = if ($Service -eq 'Iot') { 'smartpark_iot' } else { 'smartpark_facility' }
    $order = 0
    foreach ($file in $files) {
        $path = Join-Path $codeRoot "scripts/database/migrations/$file"
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing migration source: $file" }
        $order++
        [pscustomobject]@{
            Order = $order
            Database = $target
            File = "scripts/database/migrations/$file"
            Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            State = 'UNVERIFIED'
            Approval = 'Required; inspect actual datasource/schema/legacy state and backup first.'
        }
    }
    return
}
$composeFile = Join-Path $SchemaRoot 'compose.schema-integration.yml'
if (!(Test-Path -LiteralPath $composeFile -PathType Leaf)) { throw 'SchemaRoot must contain the canonical schema compose file.' }
if (!$EnvPath) { $EnvPath = Join-Path $SchemaRoot '.env' }
if (!(Test-Path -LiteralPath $EnvPath -PathType Leaf)) { throw 'Migration requires the environment file for this schema checkout.' }
$null = & (Join-Path $codeRoot 'scripts/load-env.ps1') -EnvPath $EnvPath

# Ordered base scripts for databases that are still empty.
# Fresh Docker volumes load the full init list in compose.schema-integration.yml.
# The Assistant runner verifies existing IoT overlays before starting Assistant.
$scripts = @{
    User = @(
        'microservices/01-user-access-service-db.sql'
    )
    Parking = @(
        'microservices/02-facility-service-db.sql',
        'microservices/03a-parking-structure-ops-service-db.sql'
    )
    Reservation = @(
        'microservices/03b-reservation-operations-service-db.sql'
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
$database = @{ User='smartpark_user'; Parking='smartpark_facility'; Reservation='smartpark_operations'; Payment='smartpark_payment'; Notification='smartpark_notification'; Iot='smartpark_iot' }[$Service]
$container = @{ User='user-db'; Parking='facility-db'; Reservation='operations-db'; Payment='payment-db'; Notification='notification-db'; Iot='iot-db' }[$Service]
$required = @{
    User = @('users','accounts','roles','vehicles')
    Parking = @('parking_sites','tenants','structure_synchronizations','support_tickets','operator_audit_logs','owner_onboarding_provisions','structure_maintenance_windows')
    Reservation = @('reservations','structure_edit_holds','site_capacity_pools','structure_release_receipts')
    Payment = @()
    Notification = @()
    Iot = @()
}[$Service]
# Sentinel relation created only by the canonical scripts (base or overlay) for
# this database. Index for User (partial unique on normalized_plate), table elsewhere.
$sentinel = @{ User='uq_active_vehicle_plate'; Parking='tenants'; Reservation='site_capacity_pools'; Payment='payment_orders'; Notification='notification_templates'; Iot='devices' }[$Service]

# Block list: remaining legacy 05.1–05.12 workflow scripts and integration/* alignment scripts are
# superseded by the new base files + overlays; applying them would double-apply or conflict.
$blockedPattern = '(^|[\\/])05\.(?:[1-9]|1[0-2])[-.][^\\/]*\.sql$|(^|[\\/])integration[\\/]0[123][^\\/]*\.sql$'
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
if ($UpgradeFacilityMaintenance -and $state -ne 'current') {
    throw 'The Facility maintenance upgrade requires an initialized canonical Facility database.'
}
if ($state -eq 'legacy') {
    throw "$Service database uses a legacy schema layout (canonical sentinel '$sentinel' is missing while public tables exist). Run .\setup.ps1 -RecreateDb to rebuild all six databases from the canonical scripts; setup never drops existing data on its own."
}

# Validate an existing User base before running any incremental feature writers.
# A base sentinel does not establish session/outbox compatibility.
if ($Service -eq 'User' -and $state -eq 'current') {
    Get-Content -LiteralPath (Join-Path $codeRoot 'scripts/database/test/verify-user-access-schema.sql') -Raw | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'User schema incompatible. No feature migration applied; preserve data and pending messages for reviewed migration or explicit reset.' }
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
if ($Service -eq 'User') {
    # The base sentinel predates vehicle binding revisions. Fail before any
    # additive feature writer rather than claiming an old volume is current.
    $bindingSql = "SELECT EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid = to_regclass('public.vehicles') AND attname = 'binding_revision' AND NOT attisdropped AND atttypid = 'bigint'::regtype AND attnotnull)"
    $bindingReady = & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1 -At -c $bindingSql
    if ($LASTEXITCODE -ne 0 -or ($bindingReady -join '').Trim() -ne 't') {
        throw 'User vehicles.binding_revision is missing or incompatible. Back up the selected User database, review and manually apply scripts/database/migrations/20261111_user_vehicle_binding_revision.sql, then rerun setup. No feature migration applied; do not reset the volume.'
    }
    # The existing session/outbox preflight intentionally accepts a pre-media
    # database. Reuse only the bounded, rerunnable media section of the canonical
    # base; replaying the whole initialization script against existing data is unsafe.
    $userSql = Get-Content -LiteralPath (Join-Path $codeRoot 'scripts/database/microservices/01-user-access-service-db.sql') -Raw
    $mediaStart = $userSql.IndexOf('-- SPARK-373 additive', [StringComparison]::Ordinal)
    $mediaEnd = $userSql.IndexOf('-- END SPARK-373 media schema', [StringComparison]::Ordinal)
    if ($mediaStart -lt 0 -or $mediaEnd -le $mediaStart) { throw 'Canonical User media SQL section is missing or invalid; no media upgrade applied.' }
    $userSql.Substring($mediaStart, $mediaEnd - $mediaStart) | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'User media upgrade failed; preserve data and reconcile before retrying. No volume reset is required.' }
    Get-Content -LiteralPath (Join-Path $codeRoot 'scripts/database/test/verify-user-media-schema.sql') -Raw | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'User media post-upgrade verification failed.' }
}
foreach ($table in (@($required) + @($sentinel))) {
    $exists = & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1 -At -c "SELECT to_regclass('public.$table') IS NOT NULL"
    if ($LASTEXITCODE -ne 0 -or ($exists -join '').Trim() -notin @('t','f')) {
        throw "Could not verify required $Service relation $table."
    }
    if (($exists -join '').Trim() -eq 'f') {
        if ($Service -eq 'Parking' -and $table -eq 'structure_maintenance_windows') {
            if (!$UpgradeFacilityMaintenance) {
                throw 'Facility is missing SPARK-238. Back up smartpark_facility, then run .\scripts\apply-workflow-migrations.ps1 -Service Parking -SchemaRoot . -UpgradeFacilityMaintenance from the repository root.'
            }
            $facilitySql = Get-Content -LiteralPath (Join-Path $codeRoot 'scripts/database/microservices/02-facility-service-db.sql') -Raw -Encoding UTF8
            $start = $facilitySql.IndexOf('-- BEGIN SPARK-238 maintenance schema', [StringComparison]::Ordinal)
            $end = $facilitySql.IndexOf('-- END SPARK-238 maintenance schema', [StringComparison]::Ordinal)
            if ($start -lt 0 -or $end -le $start) { throw 'Canonical Facility maintenance SQL section is missing or invalid.' }
            $upgrade = @(
                'BEGIN;'
                'SET LOCAL search_path = public, pg_catalog;'
                "SET LOCAL lock_timeout = '5s';"
                'SELECT pg_advisory_xact_lock(238);'
                'CREATE EXTENSION IF NOT EXISTS "uuid-ossp";'
                'CREATE EXTENSION IF NOT EXISTS btree_gist;'
                $facilitySql.Substring($start, $end - $start)
                'COMMIT;'
            ) -join "`n"
            $upgrade | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -X -U $database -d $database -v ON_ERROR_STOP=1
            if ($LASTEXITCODE -ne 0) { throw 'Facility SPARK-238 upgrade failed. Review the PostgreSQL error before retrying.' }
            Write-Output 'Facility SPARK-238 maintenance schema applied.'
            return
        }
        if ($Service -eq 'Parking' -and $table -eq 'owner_onboarding_provisions') {
            throw 'Missing required Facility relation owner_onboarding_provisions. Existing Facility data requires a separately reviewed migration; do not replay the base schema or recreate database volumes unless a reset is explicitly authorized.'
        }
        throw "Missing required $Service relation $table; the database does not match the canonical schema. Run .\setup.ps1 -RecreateDb to rebuild it from the canonical scripts."
    }
}
if ($Service -eq 'User') {
    Get-Content -LiteralPath (Join-Path $codeRoot 'scripts/database/test/verify-user-access-schema.sql') -Raw | & docker compose --project-name $ProjectName --env-file $EnvPath -f $composeFile exec -T $container psql -U $database -d $database -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'User schema incompatible. Preserve data and pending messages; use a reviewed migration or explicitly authorized reset.' }
    Write-Output 'User schema already current; full session and outbox verifier passed. Consolidated base skipped; incremental features verified/applied.'
} elseif ($Service -eq 'Iot') {
    Write-Output 'IoT base schema verified. The Assistant runner checks the Assistant schema before startup.'
} elseif ($state -eq 'current') {
    Write-Output "$Service schema already current; verified required relations, nothing to apply."
} else {
    Write-Output "$Service workflow migrations applied."
}
