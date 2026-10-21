param(
    [string]$EnvPath
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($EnvPath)) { $EnvPath = Join-Path $repoRoot '.env' }
$testConfig = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport

function Get-TestConnection([string]$Key, [string]$Service, [int]$DefaultPort) {
    if (![string]::IsNullOrWhiteSpace($testConfig[$Key])) { return $testConfig[$Key] }
    if ([string]::IsNullOrWhiteSpace($testConfig['SMARTPARK_DEV_PASSWORD'])) {
        throw "Set $Key or SMARTPARK_DEV_PASSWORD in the local environment file."
    }
    $port = $DefaultPort
    $configuredPort = $testConfig['SMARTPARK_DB_' + $Service.ToUpperInvariant() + '_PORT']
    if ($configuredPort -and (![int]::TryParse($configuredPort, [ref]$port) -or $port -lt 1 -or $port -gt 65535)) {
        throw "Invalid PostgreSQL port for $Service."
    }
    $password = $testConfig['SMARTPARK_DEV_PASSWORD'].Replace('"', '""')
    $database = 'smartpark_' + $Service.ToLowerInvariant()
    return 'Host=127.0.0.1;Port=' + $port + ';Database=' + $database + ';Username=' + $database + ';Password="' + $password + '"'
}

$connections = [ordered]@{
    SMARTPARK_AUTH_TEST_CONNECTION = Get-TestConnection 'SMARTPARK_AUTH_TEST_CONNECTION' 'User' 5441
    SMARTPARK_STRUCTURE_TEST_CONNECTION = Get-TestConnection 'SMARTPARK_STRUCTURE_TEST_CONNECTION' 'Parking' 5442
}
$rabbitPort = 5679
$configuredRabbitPort = $testConfig['SMARTPARK_RABBIT_TEST_PORT']
if ($configuredRabbitPort -and (![int]::TryParse($configuredRabbitPort, [ref]$rabbitPort) -or $rabbitPort -lt 1 -or $rabbitPort -gt 65535)) {
    throw 'Invalid SMARTPARK_RABBIT_TEST_PORT.'
}
$connections['SMARTPARK_RABBIT_TEST_PORT'] = [string]$rabbitPort

# XmlWriter escapes passwords containing XML characters. Never print credentials.
# This file is machine-local and ignored by Git; do not send it to teammates.
$outputPath = Join-Path $repoRoot 'smartpark.local.runsettings'
$xmlSettings = New-Object System.Xml.XmlWriterSettings
$xmlSettings.Indent = $true
$xmlSettings.Encoding = New-Object System.Text.UTF8Encoding($false)
$writer = [System.Xml.XmlWriter]::Create($outputPath, $xmlSettings)
try {
    $writer.WriteStartDocument()
    $writer.WriteStartElement('RunSettings')
    $writer.WriteStartElement('RunConfiguration')
    $writer.WriteStartElement('EnvironmentVariables')
    foreach ($key in $connections.Keys) { $writer.WriteElementString($key, $connections[$key]) }
    $writer.WriteEndElement()
    $writer.WriteEndElement()
    $writer.WriteEndElement()
    $writer.WriteEndDocument()
}
finally { $writer.Dispose() }
Write-Host "Created $outputPath"
Write-Host 'Test projects automatically use this file. Reload the solution if Visual Studio is already open.'
Write-Host 'Manual selection: Test > Configure Run Settings > Select Solution Wide runsettings File.'
Write-Host 'Or pass --settings smartpark.local.runsettings to dotnet test.'
Write-Host 'PostgreSQL must be running. Test accounts need permission to create temporary databases.'
Write-Host 'Start the test broker: docker compose -f compose.tests.yml up -d --wait'
