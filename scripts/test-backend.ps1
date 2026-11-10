param(
    [ValidateSet('Offline', 'Postgres', 'PostgresRabbitMQ', 'LiveBroker', 'Architecture')]
    [string]$Mode = 'Offline',
    [string]$Settings,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$filters = @{
    Offline = 'Category!=Postgres&Category!=PostgresRabbitMQ&Category!=LiveBroker&Category!=Architecture'
    Postgres = 'Category=Postgres'
    PostgresRabbitMQ = 'Category=PostgresRabbitMQ'
    LiveBroker = 'Category=LiveBroker'
    Architecture = 'Category=Architecture'
}
$integration = $Mode -in @('Postgres', 'PostgresRabbitMQ', 'LiveBroker')
if ($integration -and !$Settings) { $Settings = Join-Path $repoRoot 'smartpark.local.runsettings' }
$values = @{}
if ($Settings) {
    if (!(Test-Path -LiteralPath $Settings -PathType Leaf)) { throw 'Test settings file not found. Run scripts/configure-test-settings.ps1 with a private test env file.' }
    $Settings = (Resolve-Path -LiteralPath $Settings).Path
    [xml]$settingsXml = Get-Content -LiteralPath $Settings -Raw
    foreach ($entry in $settingsXml.RunSettings.RunConfiguration.EnvironmentVariables.ChildNodes) {
        if ($entry.NodeType -eq [System.Xml.XmlNodeType]::Element) { $values[$entry.Name] = $entry.InnerText }
    }
}
function Get-TestValue([string]$Name) {
    if ($values.ContainsKey($Name)) { return $values[$Name] }
    return [Environment]::GetEnvironmentVariable($Name)
}
$required = @()
if ($Mode -eq 'Postgres') { $required += 'SMARTPARK_AUTH_TEST_CONNECTION', 'SMARTPARK_STRUCTURE_TEST_CONNECTION' }
if ($Mode -eq 'PostgresRabbitMQ') { $required += 'SMARTPARK_AUTH_TEST_CONNECTION', 'SMARTPARK_PROFILE_RABBITMQ_PORT' }
if ($Mode -in @('PostgresRabbitMQ', 'LiveBroker')) {
    $required += 'SMARTPARK_RABBIT_TEST_PORT', 'SMARTPARK_RABBITMQ_USER', 'SMARTPARK_RABBITMQ_PASSWORD', 'SMARTPARK_RABBITMQ_VHOST'
}
foreach ($name in $required) {
    if ([string]::IsNullOrWhiteSpace((Get-TestValue $name))) { throw "Missing $name for requested $Mode run. Generate isolated test settings first." }
    if ($name.EndsWith('_PORT')) {
        $port = 0
        if (![int]::TryParse((Get-TestValue $name), [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw "Invalid $name." }
    }
}
if ($Mode -eq 'PostgresRabbitMQ' -and (Get-TestValue 'SMARTPARK_RABBIT_TEST_PORT') -ne (Get-TestValue 'SMARTPARK_PROFILE_RABBITMQ_PORT')) {
    throw 'Both delivery tests must select the same isolated broker port.'
}
$results = Join-Path $repoRoot ('TestResults/backend-' + $Mode + '-' + [guid]::NewGuid().ToString('N'))
$arguments = @('test', (Join-Path $repoRoot 'SmartParking.slnx'), '-c', 'Release', '--nologo', '--filter', $filters[$Mode], '--logger', 'trx', '--results-directory', $results)
if ($Settings) { $arguments += '--settings', $Settings }
if ($NoBuild) { $arguments += '--no-build' }
& dotnet @arguments
$testExit = $LASTEXITCODE
if ($testExit -ne 0) { throw "Backend $Mode tests failed (exit $testExit). Results: $results" }
$total = 0
$notExecuted = 0
$failed = 0
foreach ($file in Get-ChildItem -LiteralPath $results -Filter *.trx) {
    [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    $total += [int]$counters.total
    # xUnit's VSTest adapter can leave Counters.notExecuted at zero while
    # skipped result rows have outcome="NotExecuted". Inspect the rows too.
    $skippedRows = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'NotExecuted' }).Count
    $notExecuted += [Math]::Max([int]$counters.notExecuted, $skippedRows)
    $failed += [int]$counters.failed
}
if ($total -eq 0) { throw "No tests executed for $Mode. Check adapter discovery and filters." }
if ($failed -gt 0 -or $notExecuted -gt 0) { throw "$Mode has $failed failures and $notExecuted skipped tests. Results: $results" }
Write-Host "$Mode verified: $total tests, no failures or skips. Results: $results"
