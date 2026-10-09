param(
    [string]$ConfigPath,
    [switch]$SkipDatabase
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (!$ConfigPath) { $ConfigPath = Join-Path $taskRoot '.cache/qa-local.json' }

function New-LocalSecret {
    param([int]$Bytes = 32)
    $buffer = New-Object byte[] $Bytes
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($buffer) } finally { $random.Dispose() }
    return [Convert]::ToBase64String($buffer)
}

if (!(Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $ConfigPath -Parent) | Out-Null
    [ordered]@{
        SMARTPARK_DEV_PASSWORD = New-LocalSecret
        SMARTPARK_SERVICE_KEY = New-LocalSecret
        SMARTPARK_SCHEMA_ROOT = $taskRoot
        SMARTPARK_ADMIN_PASSWORD = 'Aa1!' + (New-LocalSecret -Bytes 6)
        'Workflow:KeyDirectory' = Join-Path $taskRoot '.cache/workflow-keys'
    } | ConvertTo-Json | Set-Content -LiteralPath $ConfigPath -Encoding UTF8
    Write-Output 'Created local configuration in the Git-ignored config file.'
}
& (Join-Path $PSScriptRoot 'import-local-config.ps1') -ConfigPath $ConfigPath
if (!$env:SMARTPARK_DEV_PASSWORD) { throw 'Configure SMARTPARK_DEV_PASSWORD in the local config file.' }
if (!$env:SMARTPARK_SERVICE_KEY -or $env:SMARTPARK_SERVICE_KEY.Length -lt 32) {
    throw 'Configure SMARTPARK_SERVICE_KEY (at least 32 characters) in the local config file.'
}
$schemaRoot = if ($env:SMARTPARK_SCHEMA_ROOT) { $env:SMARTPARK_SCHEMA_ROOT } else { $taskRoot }
$composeFile = Join-Path $schemaRoot 'compose.schema-integration.yml'
if (!(Test-Path -LiteralPath $composeFile -PathType Leaf)) { throw 'SMARTPARK_SCHEMA_ROOT must contain compose.schema-integration.yml.' }

if (!$SkipDatabase) {
    & docker compose -f $composeFile up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Docker database startup failed. Check Docker Desktop and the local database configuration.' }
    foreach ($service in @('User', 'Parking', 'Reservation')) {
        & (Join-Path $PSScriptRoot 'apply-workflow-migrations.ps1') -Service $service -SchemaRoot $schemaRoot
    }
}

$localSettings = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
foreach ($service in @('User', 'Parking', 'Reservation')) {
    $projectDir = Join-Path $taskRoot "src/Services/${service}Service/SmartParking.${service}Service.API"
    $database = 'smartpark_' + $service.ToLowerInvariant()
    $port = @{ User = 5441; Parking = 5442; Reservation = 5443 }[$service]
    $settings = [ordered]@{
        'Services:Key' = $env:SMARTPARK_SERVICE_KEY
        'Services:User' = 'http://localhost:5035/'
        'Services:Parking' = 'http://localhost:5045/'
        'Services:Reservation' = 'http://localhost:5055/'
    }
    # Quote the password for Npgsql connection-string syntax, including semicolons.
    $password = $env:SMARTPARK_DEV_PASSWORD.Replace('"', '""')
    $settings["ConnectionStrings:$service"] = "Host=127.0.0.1;Port=$port;Database=$database;Username=$database;Password=`"$password`""
    if ($service -eq 'User') {
        foreach ($property in $localSettings.PSObject.Properties) {
            if ($property.Name.Contains(':')) {
                $settings[$property.Name] = [Environment]::GetEnvironmentVariable($property.Name.Replace(':', '__'), 'Process')
            }
        }
        if ($env:SMARTPARK_ADMIN_PASSWORD) {
            $settings['DevelopmentAdmin:Email'] = 'admin@smartpark.local'
            $settings['DevelopmentAdmin:Password'] = $env:SMARTPARK_ADMIN_PASSWORD
        }
    }
    # Piped JSON keeps secrets out of command-line arguments and console output.
    $settings | ConvertTo-Json | & dotnet user-secrets set --project $projectDir
    if ($LASTEXITCODE -ne 0) { throw "Could not configure User Secrets for $service." }
    Write-Output "$service local configuration ready."
}
& dotnet restore (Join-Path $taskRoot 'SmartParking.slnx') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
Write-Output 'Setup complete. Run the APIs in Visual Studio (Development) or scripts/run-schema-service.ps1.'
