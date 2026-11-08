# Offline artifact verification; no environment file or DB is needed.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function docker { throw 'Unexpected Docker call.' }
function dotnet { throw 'Unexpected dotnet call.' }
$runner = Join-Path $PSScriptRoot 'apply-workflow-migrations.ps1'
$iot = @(& $runner -Service Iot -SchemaRoot $root -PlanAssistant)
$facility = @(& $runner -Service Parking -SchemaRoot $root -PlanAssistant)
if ($iot.Count -ne 3 -or $facility.Count -ne 1) { throw 'Migration count differs.' }
if ($iot[0].File -notlike '*20261103_iot_assistant_management.sql' -or
    $iot[1].File -notlike '*20261105_iot_assistant_s02.sql' -or
    $iot[2].File -notlike '*20261105_iot_assistant_s05.sql') { throw 'Dependency order differs.' }
foreach ($row in @($iot) + @($facility)) {
    if ($row.State -ne 'UNVERIFIED' -or $row.Sha256 -cne (Get-FileHash -LiteralPath (Join-Path $root $row.File) -Algorithm SHA256).Hash) {
        throw 'Invalid approval artifact.'
    }
}
if (@($iot | Where-Object { $_.Database -ne 'smartpark_iot' }).Count -or $facility[0].Database -ne 'smartpark_facility') { throw 'Wrong DB mapping.' }
$rejected = $false
try { & $runner -Service User -SchemaRoot $root -PlanAssistant }
catch { $rejected = $_.Exception.Message -match 'supports only' }
if (!$rejected) { throw 'Unsupported service accepted.' }
Write-Host 'PASS: ordered SQL approval artifacts, DB targets, exact SHA256, UNVERIFIED state, unsupported-service guard; no DB/CLI.'
