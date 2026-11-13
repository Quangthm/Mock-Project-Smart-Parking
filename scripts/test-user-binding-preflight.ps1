# Offline runner regression: no real Docker calls or database mutations.
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $root ('.cache/binding-preflight-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$fixtureEnv = Join-Path $scratch 'fixture.env'
[IO.File]::WriteAllText($fixtureEnv, '# Deliberately empty offline fixture')
$global:bindingFixtureResult = 'f'
$global:bindingFixtureMediaWrites = 0
$global:bindingFixtureProbes = 0
function docker {
    $body = $input | Out-String
    $global:LASTEXITCODE = 0
    $query = [Array]::IndexOf($args, '-c')
    if ($query -ge 0) {
        $sql = [string]$args[$query + 1]
        if ($sql -match '\b(DROP|ALTER|INSERT|UPDATE|DELETE)\b') { throw 'Unexpected mutating probe.' }
        if ($sql -match "THEN 'current'") { return 'current' }
        if ($sql -match "attname = 'binding_revision'") {
            $global:bindingFixtureProbes++
            return $global:bindingFixtureResult
        }
        if ($sql -match 'to_regclass') { return 't' }
        throw 'Unexpected query in fixture.'
    }
    if ($body -match '-- SPARK-373 additive') { $global:bindingFixtureMediaWrites++ }
}
$runner = Join-Path $PSScriptRoot 'apply-workflow-migrations.ps1'
foreach ($result in @('f', 'unexpected')) {
    $global:bindingFixtureResult = $result
    $failed = $false
    try { & $runner -Service User -SchemaRoot $root -EnvPath $fixtureEnv }
    catch { $failed = $_.Exception.Message -match '20261111_user_vehicle_binding_revision.sql' }
    if (!$failed -or $global:bindingFixtureMediaWrites -ne 0) { throw 'Incompatible binding schema did not stop before feature writes.' }
}
$global:bindingFixtureResult = 't'
& $runner -Service User -SchemaRoot $root -EnvPath $fixtureEnv
if ($global:bindingFixtureProbes -ne 3 -or $global:bindingFixtureMediaWrites -ne 1) { throw 'Compatible binding schema did not continue through the normal User upgrade.' }
Write-Host 'PASS: missing/malformed vehicle binding schema fails before feature writes; compatible schema continues. Docker is mocked.'
