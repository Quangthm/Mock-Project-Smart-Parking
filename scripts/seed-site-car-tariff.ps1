[CmdletBinding()]
param(
    [Guid]$SiteId = '8fe65b2e-386e-4bee-b7a4-000527af7d18'
)

$ErrorActionPreference = 'Stop'
$seedRoot = Split-Path $PSScriptRoot -Parent
$seedEnv = Join-Path $seedRoot '.env'
$seedCompose = Join-Path $seedRoot 'compose.schema-integration.yml'
$seedSql = Join-Path $PSScriptRoot 'database/seeds/demo/04-site-car-tariff.sql'
if ($SiteId -eq [Guid]::Empty) { throw 'SiteId must not be empty.' }
foreach ($path in @($seedEnv, $seedCompose, $seedSql)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file missing: $path" }
}
$seedComposeArgs = @('compose', '--project-name', 'smartpark-schema', '--env-file', $seedEnv, '-f', $seedCompose)
$seedVariables = @{ site_id = $SiteId.ToString('D') }

function Invoke-SeedPsql {
    param([string]$Service, [string]$Database, [string]$Sql)
    $arguments = $seedComposeArgs + @('exec', '-T', $Service, 'psql', '-X', '-U', $Database, '-d', $Database,
        '-v', 'ON_ERROR_STOP=1', '-tA', '-q')
    foreach ($key in $seedVariables.Keys) { $arguments += @('-v', "$key=$($seedVariables[$key])") }
    $output = $Sql | & docker @arguments
    if ($LASTEXITCODE -ne 0) { throw "Tariff seed stopped on $Service. Check the database schema/container." }
    return (($output | Out-String).Trim())
}

# Resolve existing ownership instead of assuming the fixed DEMO-HQ tenant owns this site.
$tenantText = Invoke-SeedPsql -Service 'facility-db' -Database 'smartpark_facility' -Sql @'
SELECT tenant_id FROM parking_sites
WHERE id = :'site_id'::uuid AND deleted_at IS NULL AND status = 'ACTIVE';
'@
$tenantGuid = [Guid]::Empty
if (![Guid]::TryParse($tenantText, [ref]$tenantGuid) -or $tenantGuid -eq [Guid]::Empty) {
    throw "No active existing site found for $SiteId. No tariff was written."
}
$seedVariables.tenant_id = $tenantGuid.ToString('D')
$ownerText = Invoke-SeedPsql -Service 'user-db' -Database 'smartpark_user' -Sql @'
SELECT DISTINCT u.id
FROM users u JOIN accounts a ON a.user_id = u.id
WHERE a.tenant_id = :'tenant_id'::uuid AND a.role_code = 'BUSINESS_OWNER'
  AND a.status = 'ACTIVE' AND a.deleted_at IS NULL AND u.deleted_at IS NULL;
'@
$ownerGuid = [Guid]::Empty
if (![Guid]::TryParse($ownerText, [ref]$ownerGuid) -or $ownerGuid -eq [Guid]::Empty) {
    throw 'Expected exactly one existing active Owner for this tenant. No tariff was written.'
}
$seedVariables.owner_user_id = $ownerGuid.ToString('D')

Write-Host "Seeding CAR tariff for existing site $SiteId (tenant $tenantGuid)."
$result = Invoke-SeedPsql -Service 'payment-db' -Database 'smartpark_payment' -Sql (Get-Content -LiteralPath $seedSql -Raw)
Write-Host $result
Write-Host 'Done. Refresh the booking price preview. No service restart is required.'
