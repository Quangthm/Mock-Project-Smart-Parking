[CmdletBinding()]
param(
    # Accepted for command-line parity with setup.ps1; this script never prompts.
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $taskRoot '.env'
$composeFile = Join-Path $taskRoot 'compose.schema-integration.yml'

if (!(Test-Path -LiteralPath $envFile -PathType Leaf)) {
    throw "Missing $envFile. Run setup.ps1 at the repository root first."
}

$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile -NoExport
$ownerEmail = [string]$settings['SMARTPARK_OWNER_EMAIL']
$operatorEmail = [string]$settings['SMARTPARK_OPERATOR_EMAIL']
if ([string]::IsNullOrWhiteSpace($ownerEmail)) { $ownerEmail = 'owner@smartpark.local' }
if ([string]::IsNullOrWhiteSpace($operatorEmail)) { $operatorEmail = 'operator@smartpark.local' }
if ($ownerEmail -cnotmatch '^[^@\s]+@[^@\s]+$') { throw "Invalid SMARTPARK_OWNER_EMAIL: $ownerEmail" }
if ($operatorEmail -cnotmatch '^[^@\s]+@[^@\s]+$') { throw "Invalid SMARTPARK_OPERATOR_EMAIL: $operatorEmail" }
if ($ownerEmail -eq $operatorEmail) { throw 'Owner and operator demo emails must differ.' }

$ids = @{
    tenant_id = 'a0000000-0000-4000-8000-000000000001'
    site_id   = 'b0000000-0000-4000-8000-000000000001'
    floor_id  = 'c0000000-0000-4000-8000-000000000001'
    zone_id   = 'c0000000-0000-4000-8000-000000000002'
    slot1     = 'd0000000-0000-4000-8000-000000000001'
    slot2     = 'd0000000-0000-4000-8000-000000000002'
    slot3     = 'd0000000-0000-4000-8000-000000000003'
    slot4     = 'd0000000-0000-4000-8000-000000000004'
    slot5     = 'd0000000-0000-4000-8000-000000000005'
    slot6     = 'd0000000-0000-4000-8000-000000000006'
    slot7     = 'd0000000-0000-4000-8000-000000000007'
    slot8     = 'd0000000-0000-4000-8000-000000000008'
    owner_email = $ownerEmail
    operator_email = $operatorEmail
}

$composeArgs = @(
    'compose',
    '--project-name', 'smartpark-schema',
    '--env-file', $envFile,
    '-f', $composeFile
)

Write-Host '[1/6] Checking schema containers...' -ForegroundColor Cyan
$running = & docker @composeArgs ps --status running --services 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'Cannot inspect schema containers. Start Docker Desktop, then run setup.ps1.'
}
$running = @($running | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
foreach ($service in @('user-db','parking-db','reservation-db')) {
    if ($running -notcontains $service) {
        throw "Container for service '$service' is not running. Run setup.ps1 first."
    }
}
Write-Host '      OK (user-db, parking-db, reservation-db)' -ForegroundColor DarkGray

Write-Host '[2/6] Resolving demo accounts...' -ForegroundColor Cyan
Write-Host "      owner=$ownerEmail operator=$operatorEmail" -ForegroundColor DarkGray
$accountCheckSql = @'
SELECT
  (SELECT count(DISTINCT a.id)
     FROM users u
     JOIN accounts a ON a.user_id = u.id
     JOIN account_roles r ON r.account_id = a.id
    WHERE lower(u.email) = lower(:'owner_email')
      AND u.deleted_at IS NULL
      AND a.deleted_at IS NULL
      AND r.role_code = 'BUSINESS_OWNER')::text
  || '|' ||
  (SELECT count(DISTINCT a.id)
     FROM users u
     JOIN accounts a ON a.user_id = u.id
     JOIN account_roles r ON r.account_id = a.id
    WHERE lower(u.email) = lower(:'operator_email')
      AND u.deleted_at IS NULL
      AND a.deleted_at IS NULL
      AND r.role_code = 'SITE_OPERATOR')::text;
'@
$checkArgs = $composeArgs + @(
    'exec', '-T', 'user-db', 'psql', '-U', 'smartpark_user', '-d', 'smartpark_user',
    '-v', 'ON_ERROR_STOP=1',
    '-v', "owner_email=$ownerEmail",
    '-v', "operator_email=$operatorEmail",
    '-tA'
)
$accountCheck = $accountCheckSql | & docker @checkArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to inspect UserDb accounts. Check the user-db container and setup.ps1.'
}
$accountText = (($accountCheck | Out-String).Trim())
$accountParts = @($accountText -split '\|')
if ($accountParts.Count -ne 2 -or $accountParts[0].Trim() -ne '1' -or $accountParts[1].Trim() -ne '1') {
    Write-Host '      Owner/operator demo accounts were not found in UserDb.' -ForegroundColor Red
    Write-Host '      Start UserService once so DataSeeder creates them:' -ForegroundColor Yellow
    Write-Host '        .\scripts\run-schema-service.ps1 -Service User' -ForegroundColor Yellow
    Write-Host '      Wait for "Now listening on: http://localhost:5035", then rerun this script.' -ForegroundColor Yellow
    exit 2
}
Write-Host '      OK (1 owner, 1 operator)' -ForegroundColor DarkGray

function Invoke-SeedFile {
    param(
        [Parameter(Mandatory)][string]$Service,
        [Parameter(Mandatory)][string]$DatabaseUser,
        [Parameter(Mandatory)][string]$DatabaseName,
        [Parameter(Mandatory)][string]$Path
    )

    $sql = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $psqlArgs = $composeArgs + @(
        'exec', '-T', $Service, 'psql',
        '-U', $DatabaseUser,
        '-d', $DatabaseName,
        '-v', 'ON_ERROR_STOP=1',
        '-q'
    )
    foreach ($key in $ids.Keys) {
        $psqlArgs += @('-v', "$key=$($ids[$key])")
    }
    $sql | & docker @psqlArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Seed failed: $Path (transaction rolled back by ON_ERROR_STOP)."
    }
}

function Get-SeedCounts {
    param(
        [Parameter(Mandatory)][string]$Service,
        [Parameter(Mandatory)][string]$DatabaseUser,
        [Parameter(Mandatory)][string]$DatabaseName,
        [Parameter(Mandatory)][string]$Sql
    )

    $psqlArgs = $composeArgs + @(
        'exec', '-T', $Service, 'psql',
        '-U', $DatabaseUser,
        '-d', $DatabaseName,
        '-v', 'ON_ERROR_STOP=1',
        '-tA',
        '-q'
    )
    foreach ($key in $ids.Keys) {
        $psqlArgs += @('-v', "$key=$($ids[$key])")
    }
    $output = $Sql | & docker @psqlArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Verification query failed on $Service."
    }
    return (($output | Out-String).Trim())
}

function Assert-Counts {
    param(
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Actual,
        [Parameter(Mandatory)][string[]]$Expected
    )

    $actualParts = @($Actual -split '\|' | ForEach-Object { ([string]$_).Trim() })
    if ($actualParts.Count -ne $Expected.Count) {
        throw "$Label count mismatch: expected $($Expected -join '|'), got $Actual"
    }
    for ($i = 0; $i -lt $Expected.Count; $i++) {
        if ($actualParts[$i] -ne $Expected[$i]) {
            throw "$Label count mismatch: expected $($Expected -join '|'), got $Actual"
        }
    }
}

$demoDir = Join-Path $PSScriptRoot 'database/seeds/demo'

Write-Host '[3/6] Seeding identity DB...' -ForegroundColor Cyan
Invoke-SeedFile -Service 'user-db' -DatabaseUser 'smartpark_user' -DatabaseName 'smartpark_user' -Path (Join-Path $demoDir '01-identity.sql')
$identityCounts = Get-SeedCounts -Service 'user-db' -DatabaseUser 'smartpark_user' -DatabaseName 'smartpark_user' -Sql @'
SELECT
  (SELECT count(*) FROM tenants WHERE id = :'tenant_id'::uuid)::text || '|' ||
  (SELECT count(DISTINCT a.id)
     FROM accounts a JOIN users u ON u.id = a.user_id
    WHERE lower(u.email) = lower(:'owner_email')
      AND a.tenant_id = :'tenant_id'::uuid
      AND a.deleted_at IS NULL)::text || '|' ||
  (SELECT count(*)
     FROM operator_grants g
     JOIN accounts a ON a.id = g.account_id
     JOIN users u ON u.id = a.user_id
    WHERE lower(u.email) = lower(:'operator_email')
      AND a.tenant_id = :'tenant_id'::uuid
      AND a.deleted_at IS NULL)::text;
'@
Assert-Counts -Label 'identity' -Actual $identityCounts -Expected @('1','1','1')
Write-Host '      OK (tenant + 2 accounts + 1 grant)' -ForegroundColor DarkGray

Write-Host '[4/6] Seeding facility DB...' -ForegroundColor Cyan
Invoke-SeedFile -Service 'parking-db' -DatabaseUser 'smartpark_parking' -DatabaseName 'smartpark_parking' -Path (Join-Path $demoDir '02-facility.sql')
$facilityCounts = Get-SeedCounts -Service 'parking-db' -DatabaseUser 'smartpark_parking' -DatabaseName 'smartpark_parking' -Sql @'
SELECT
  (SELECT count(*) FROM parking_sites WHERE id = :'site_id'::uuid)::text || '|' ||
  (SELECT count(*) FROM spatial_units WHERE site_id = :'site_id'::uuid AND deleted_at IS NULL)::text || '|' ||
  (SELECT count(*) FROM parking_slots WHERE site_id = :'site_id'::uuid AND deleted_at IS NULL)::text || '|' ||
  (SELECT count(*) FROM spatial_unit_capacities WHERE site_id = :'site_id'::uuid)::text || '|' ||
  (SELECT count(*) FROM slot_compatibilities WHERE site_id = :'site_id'::uuid)::text;
'@
Assert-Counts -Label 'facility' -Actual $facilityCounts -Expected @('1','2','8','2','8')
Write-Host '      OK (1 site, 2 units, 8 slots)' -ForegroundColor DarkGray

Write-Host '[5/6] Seeding reservation DB...' -ForegroundColor Cyan
Invoke-SeedFile -Service 'reservation-db' -DatabaseUser 'smartpark_reservation' -DatabaseName 'smartpark_reservation' -Path (Join-Path $demoDir '03-reservation.sql')
$reservationCounts = Get-SeedCounts -Service 'reservation-db' -DatabaseUser 'smartpark_reservation' -DatabaseName 'smartpark_reservation' -Sql @'
SELECT
  (SELECT count(*) FROM site_capacity_pools
    WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid)::text || '|' ||
  (SELECT count(*) FROM operational_capacity_configs
    WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid)::text || '|' ||
  (SELECT count(*) FROM operational_policies
    WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid)::text;
'@
Assert-Counts -Label 'reservation' -Actual $reservationCounts -Expected @('2','2','1')
Write-Host '      OK (2 pools, 2 configs, 1 policy)' -ForegroundColor DarkGray

Write-Host '[6/6] Demo data ready.' -ForegroundColor Green
Write-Host "      Site: DEMO-HQ ($($ids['site_id']))"
Write-Host "      Owner: $ownerEmail (password: SMARTPARK_OWNER_PASSWORD in .env)"
Write-Host "      Operator: $operatorEmail (password: SMARTPARK_OPERATOR_PASSWORD in .env)"
