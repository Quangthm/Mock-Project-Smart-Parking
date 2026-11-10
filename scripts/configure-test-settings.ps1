param(
    [string]$EnvPath,
    [switch]$DatabaseOnly,
    [string]$OutputPath
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
    $database = @{ User='smartpark_user'; Parking='smartpark_facility' }[$Service]
    return 'Host=127.0.0.1;Port=' + $port + ';Database=' + $database + ';Username=' + $database + ';Password="' + $password + '"'
}

$connections = [ordered]@{
    SMARTPARK_AUTH_TEST_CONNECTION = Get-TestConnection 'SMARTPARK_AUTH_TEST_CONNECTION' 'User' 5441
    SMARTPARK_STRUCTURE_TEST_CONNECTION = Get-TestConnection 'SMARTPARK_STRUCTURE_TEST_CONNECTION' 'Parking' 5442
}
if (!$DatabaseOnly) {
    # Compose owns the isolated broker's defaults and overrides. Capture its rendered
    # configuration privately; never emit it because it contains credentials.
    $brokerJson = & docker compose --env-file $EnvPath -f (Join-Path $repoRoot 'compose.tests.yml') config --format json 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read test broker settings. Check Docker Compose and the private env file.' }
    $broker = ($brokerJson | ConvertFrom-Json).services.rabbitmq
    $rabbitPort = 0
    if (![int]::TryParse([string]$broker.ports[0].published, [ref]$rabbitPort) -or $rabbitPort -lt 1 -or $rabbitPort -gt 65535) {
        throw 'Invalid SMARTPARK_RABBIT_TEST_PORT.'
    }
    if ($testConfig['SMARTPARK_PROFILE_RABBITMQ_PORT'] -and $testConfig['SMARTPARK_PROFILE_RABBITMQ_PORT'] -ne [string]$rabbitPort) {
        throw 'Both delivery tests must use the same isolated broker. Set SMARTPARK_RABBIT_TEST_PORT only.'
    }
    $connections['SMARTPARK_RABBIT_TEST_PORT'] = [string]$rabbitPort
    $connections['SMARTPARK_PROFILE_RABBITMQ_PORT'] = [string]$rabbitPort
    $connections['SMARTPARK_RABBITMQ_USER'] = [string]$broker.environment.RABBITMQ_DEFAULT_USER
    $connections['SMARTPARK_RABBITMQ_PASSWORD'] = [string]$broker.environment.RABBITMQ_DEFAULT_PASS
    $connections['SMARTPARK_RABBITMQ_VHOST'] = [string]$broker.environment.RABBITMQ_DEFAULT_VHOST
}

# XmlWriter escapes passwords containing XML characters. Never print credentials.
# This file is machine-local and ignored by Git; do not send it to teammates.
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $repoRoot 'smartpark.local.runsettings' }
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
Write-Host 'Test projects automatically use the default root smartpark.local.runsettings. Select custom output files explicitly.'
Write-Host 'Manual selection: Test > Configure Run Settings > Select Solution Wide runsettings File.'
Write-Host 'Or pass --settings smartpark.local.runsettings to dotnet test.'
Write-Host 'PostgreSQL must be running. Test accounts need permission to create temporary databases.'
if (!$DatabaseOnly) { Write-Host 'Start the test broker with the same --env-file: docker compose -f compose.tests.yml up -d --wait' }
