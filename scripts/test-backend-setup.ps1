# Regression checks for private test settings; never starts a container.
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $repoRoot ('TestResults/settings-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$envPath = Join-Path $scratch 'synthetic.env'
$settingsPath = Join-Path $scratch 'synthetic.runsettings'
$keys = @('SMARTPARK_RABBIT_TEST_PORT', 'SMARTPARK_TEST_RABBITMQ_USER', 'SMARTPARK_TEST_RABBITMQ_PASSWORD', 'SMARTPARK_TEST_RABBITMQ_VHOST')
$original = @{}
foreach ($key in $keys) {
    $original[$key] = [Environment]::GetEnvironmentVariable($key)
    Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue
}
function Require([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Read-Values {
    [xml]$xml = Get-Content -LiteralPath $settingsPath -Raw
    $result = @{}
    foreach ($entry in $xml.RunSettings.RunConfiguration.EnvironmentVariables.ChildNodes) { $result[$entry.Name] = $entry.InnerText }
    return $result
}
try {
    $connection = 'Host=localhost;Password="synthetic<&""db"'
    $password = 'synthetic<&"broker'
    $lines = @("SMARTPARK_AUTH_TEST_CONNECTION=$connection", "SMARTPARK_STRUCTURE_TEST_CONNECTION=$connection",
        'SMARTPARK_RABBIT_TEST_PORT=15679', 'SMARTPARK_TEST_RABBITMQ_USER=setup_test',
        "SMARTPARK_TEST_RABBITMQ_PASSWORD=$password", 'SMARTPARK_TEST_RABBITMQ_VHOST=/isolated-test')
    $lines | Set-Content -LiteralPath $envPath -Encoding UTF8
    & "$PSScriptRoot/configure-test-settings.ps1" -EnvPath $envPath -OutputPath $settingsPath -DatabaseOnly | Out-Null
    $values = Read-Values
    Require ($values.Count -eq 2) 'DatabaseOnly included broker settings.'
    Require ($values.SMARTPARK_AUTH_TEST_CONNECTION -ceq $connection) 'Connection XML round trip failed.'
    & "$PSScriptRoot/configure-test-settings.ps1" -EnvPath $envPath -OutputPath $settingsPath | Out-Null
    $values = Read-Values
    Require ($values.Count -eq 7) 'Full settings must contain two database and five broker values.'
    Require ($values.SMARTPARK_RABBIT_TEST_PORT -eq '15679' -and $values.SMARTPARK_PROFILE_RABBITMQ_PORT -eq '15679') "Broker ports differ: identity=$($values.SMARTPARK_RABBIT_TEST_PORT), profile=$($values.SMARTPARK_PROFILE_RABBITMQ_PORT)."
    Require ($values.SMARTPARK_RABBITMQ_USER -ceq 'setup_test') 'Broker user override lost.'
    Require ($values.SMARTPARK_RABBITMQ_PASSWORD -ceq $password) 'Broker password XML round trip failed.'
    Require ($values.SMARTPARK_RABBITMQ_VHOST -ceq '/isolated-test') 'Broker vhost override lost.'
    $env:SMARTPARK_RABBIT_TEST_PORT = '15680'
    & "$PSScriptRoot/configure-test-settings.ps1" -EnvPath $envPath -OutputPath $settingsPath | Out-Null
    Require ((Read-Values).SMARTPARK_PROFILE_RABBITMQ_PORT -eq '15680') 'Compose shell port override was not preserved.'
    Remove-Item Env:SMARTPARK_RABBIT_TEST_PORT
    ($lines + 'SMARTPARK_PROFILE_RABBITMQ_PORT=15681') | Set-Content -LiteralPath $envPath -Encoding UTF8
    $rejected = $false
    try { & "$PSScriptRoot/configure-test-settings.ps1" -EnvPath $envPath -OutputPath $settingsPath | Out-Null }
    catch { $rejected = $_.Exception.Message -like '*same isolated broker*' }
    Require $rejected 'Conflicting delivery ports were accepted.'
    $rejected = $false
    try { & "$PSScriptRoot/test-backend.ps1" -Mode Postgres -Settings (Join-Path $scratch 'missing.runsettings') -NoBuild }
    catch { $rejected = $_.Exception.Message -like '*settings file not found*' }
    Require $rejected 'Requested integration accepted missing settings.'
    # Reproduce the adapter's observed TRX shape: a skipped row with a zero
    # notExecuted counter and a successful process exit must still fail the gate.
    function dotnet {
        $script:runnerResults = $args[[Array]::IndexOf($args, '--results-directory') + 1]
        New-Item -ItemType Directory -Path $script:runnerResults | Out-Null
        '<TestRun><Results><UnitTestResult outcome="NotExecuted" /></Results><ResultSummary><Counters total="1" passed="0" failed="0" notExecuted="0" /></ResultSummary></TestRun>' |
            Set-Content -LiteralPath (Join-Path $script:runnerResults 'skipped.trx')
        $global:LASTEXITCODE = 0
    }
    $rejected = $false
    try { & "$PSScriptRoot/test-backend.ps1" -Mode Architecture -NoBuild }
    catch { $rejected = $_.Exception.Message -like '*1 skipped tests*' }
    finally { Remove-Item Function:dotnet }
    Require $rejected 'Requested run accepted skipped result rows.'
    Write-Host 'PASS: database-only, broker overrides, XML escaping, shell precedence, conflicting ports, missing integration settings and unexpected skips.'
}
finally {
    foreach ($key in $keys) {
        if ($null -eq $original[$key]) { Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue }
        else { Set-Item -LiteralPath "Env:$key" -Value $original[$key] }
    }
    # Delete only this invocation's generated scratch directory inside TestResults.
    $resolved = [IO.Path]::GetFullPath($scratch)
    $allowed = [IO.Path]::GetFullPath((Join-Path $repoRoot 'TestResults')) + [IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe scratch cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    if ($script:runnerResults) {
        $resolvedResults = [IO.Path]::GetFullPath($script:runnerResults)
        if (!$resolvedResults.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe runner cleanup path.' }
        Remove-Item -LiteralPath $resolvedResults -Recurse -Force
    }
}
