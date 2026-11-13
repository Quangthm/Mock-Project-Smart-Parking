[CmdletBinding()]
param(
    [Guid]$SiteId = '8fe65b2e-386e-4bee-b7a4-000527af7d18',
    [Parameter(Mandatory)][bool]$ArrivalLateToleranceEnabled,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = 'Stop'
$policyRoot = Split-Path $PSScriptRoot -Parent
$policyEnv = Join-Path $policyRoot '.env'
$policyCompose = Join-Path $policyRoot 'compose.schema-integration.yml'
$policyDefaults = Join-Path $PSScriptRoot 'database/seeds/03b-reservation-policy-defaults.sql'
$policySiteSeed = Join-Path $PSScriptRoot 'database/seeds/demo/05-site-reservation-policy.sql'
foreach ($policyPath in @($policyEnv, $policyCompose, $policyDefaults, $policySiteSeed)) {
    if (!(Test-Path -LiteralPath $policyPath -PathType Leaf)) { throw "Required file missing: $policyPath" }
}
if ($SiteId -eq [Guid]::Empty) { throw 'SiteId must not be empty.' }
$policyComposeArgs = @('compose', '--project-name', 'smartpark-schema', '--env-file', $policyEnv, '-f', $policyCompose)
$policyVariables = @{
    site_id = $SiteId.ToString('D')
    late_tolerance = $ArrivalLateToleranceEnabled.ToString().ToLowerInvariant()
    replace_existing = $ReplaceExisting.IsPresent.ToString().ToLowerInvariant()
}
function Invoke-PolicyPsql {
    param([string]$Service, [string]$Database, [string]$Sql)
    $policyArgs = $policyComposeArgs + @('exec', '-T', $Service, 'psql', '-X', '-U', $Database, '-d', $Database,
        '-v', 'ON_ERROR_STOP=1', '-tA', '-q')
    foreach ($policyKey in $policyVariables.Keys) { $policyArgs += @('-v', "$policyKey=$($policyVariables[$policyKey])") }
    $policyOutput = $Sql | & docker @policyArgs
    if ($LASTEXITCODE -ne 0) { throw "Policy seed stopped on $Service. Its transaction was not committed." }
    return (($policyOutput | Out-String).Trim())
}
$policyTenant = Invoke-PolicyPsql -Service 'facility-db' -Database 'smartpark_facility' -Sql @'
SELECT tenant_id FROM parking_sites WHERE id = :'site_id'::uuid AND deleted_at IS NULL AND status = 'ACTIVE';
'@
$policyTenantId = [Guid]::Empty
if (![Guid]::TryParse($policyTenant, [ref]$policyTenantId) -or $policyTenantId -eq [Guid]::Empty) {
    throw 'An existing active site with a valid tenant is required. Nothing was written.'
}
$policyVariables.tenant_id = $policyTenantId.ToString('D')

# Reuse the approved platform seed only when no effective platform policy exists.
$policyPrefix = @'
BEGIN;
SELECT EXISTS (SELECT 1 FROM operational_policies WHERE tenant_id IS NULL AND site_id IS NULL
  AND is_active AND effective_from <= CURRENT_TIMESTAMP
  AND (effective_until IS NULL OR CURRENT_TIMESTAMP < effective_until)) AS platform_present \gset
\if :platform_present
\else
'@
$policySql = $policyPrefix + "`n" + (Get-Content -LiteralPath $policyDefaults -Raw) + "`n\endif`n" + (Get-Content -LiteralPath $policySiteSeed -Raw)
$policyResult = Invoke-PolicyPsql -Service 'operations-db' -Database 'smartpark_operations' -Sql $policySql
Write-Host $policyResult
Write-Host 'Done. Refresh reservation terms. No service restart is required.'
