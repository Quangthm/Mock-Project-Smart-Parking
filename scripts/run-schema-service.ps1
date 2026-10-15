param(
    [Parameter(Mandatory)][ValidateSet('User','Parking','Reservation')][string]$Service
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $taskRoot '.env'

if (!(Test-Path -LiteralPath $envFile -PathType Leaf)) {
    throw "Missing $envFile. Run setup.ps1 at the repository root first."
}

# Load .env through the shared parser; it exports every key to the process env.
$null = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile

if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DEV_PASSWORD)) {
    throw 'SMARTPARK_DEV_PASSWORD is empty in .env. Run setup.ps1 first.'
}
if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_SERVICE_KEY) -or $env:SMARTPARK_SERVICE_KEY.Length -lt 32) {
    throw 'SMARTPARK_SERVICE_KEY must exist in .env and be at least 32 characters. Run setup.ps1 first.'
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Services__Key = $env:SMARTPARK_SERVICE_KEY
$env:Services__User = 'http://localhost:5035/'
$env:Services__Parking = 'http://localhost:5045/'
$env:Services__Reservation = 'http://localhost:5055/'

$dbPortByService = @{ User = 'SMARTPARK_DB_USER_PORT'; Parking = 'SMARTPARK_DB_PARKING_PORT'; Reservation = 'SMARTPARK_DB_RESERVATION_PORT' }
$defaultDbPortByService = @{ User = '5441'; Parking = '5442'; Reservation = '5443' }
$apiPortByService = @{ User = '5035'; Parking = '5045'; Reservation = '5055' }

$dbPort = [Environment]::GetEnvironmentVariable($dbPortByService[$Service])
if ([string]::IsNullOrWhiteSpace($dbPort)) { $dbPort = $defaultDbPortByService[$Service] }
$apiPort = $apiPortByService[$Service]

$dbName = 'smartpark_' + $Service.ToLowerInvariant()
# Quote the password for Npgsql syntax so ';' inside a password is safe.
$quotedPassword = $env:SMARTPARK_DEV_PASSWORD.Replace('"', '""')
[Environment]::SetEnvironmentVariable('ConnectionStrings__' + $Service, "Host=127.0.0.1;Port=$dbPort;Database=$dbName;Username=$dbName;Password=`"$quotedPassword`"", 'Process')

if ($Service -eq 'User' -and $env:SMARTPARK_ADMIN_PASSWORD) {
    $adminEmail = $env:SMARTPARK_ADMIN_EMAIL
    if ([string]::IsNullOrWhiteSpace($adminEmail)) { $adminEmail = 'admin@smartpark.local' }
    $env:DevelopmentAdmin__Email = $adminEmail
    $env:DevelopmentAdmin__Password = $env:SMARTPARK_ADMIN_PASSWORD
}

$projectDir = Join-Path $taskRoot "src/Services/${Service}Service/SmartParking.${Service}Service.API"
dotnet run --project $projectDir --no-restore --no-launch-profile -- --contentRoot $projectDir --urls "http://localhost:$apiPort"
if ($LASTEXITCODE -ne 0) { throw "${Service}Service exited with code $LASTEXITCODE" }
