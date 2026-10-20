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

function Assert-DevPassword([string]$Name, [string]$Value) {
    if ([string]::IsNullOrEmpty($Value) -or $Value.Length -lt 8 -or $Value.Length -gt 15 -or
        $Value -cnotmatch '^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$') {
        throw "$Name requires 8-15 characters, uppercase, lowercase, digit and special character."
    }
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Services__Key = $env:SMARTPARK_SERVICE_KEY
$env:Services__User = 'http://localhost:5035/'
$env:Services__Parking = 'http://localhost:5045/'
$env:Services__Reservation = 'http://localhost:5055/'
foreach ($remoteService in @('User','Parking','Reservation')) {
    $url = [Environment]::GetEnvironmentVariable('SMARTPARK_SERVICE_' + $remoteService.ToUpperInvariant() + '_URL')
    if ($url) { [Environment]::SetEnvironmentVariable('Services__' + $remoteService, $url, 'Process') }
}

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
if ($Service -eq 'User') {
    if ($env:SMARTPARK_OWNER_PASSWORD) {
        Assert-DevPassword 'SMARTPARK_OWNER_PASSWORD' $env:SMARTPARK_OWNER_PASSWORD
        if ($env:SMARTPARK_OWNER_EMAIL) {
            $env:DevelopmentOwner__Email = $env:SMARTPARK_OWNER_EMAIL
        }
        $env:DevelopmentOwner__Password = $env:SMARTPARK_OWNER_PASSWORD
    }
    if ($env:SMARTPARK_OPERATOR_PASSWORD) {
        Assert-DevPassword 'SMARTPARK_OPERATOR_PASSWORD' $env:SMARTPARK_OPERATOR_PASSWORD
        if ($env:SMARTPARK_OPERATOR_EMAIL) {
            $env:DevelopmentOperator__Email = $env:SMARTPARK_OPERATOR_EMAIL
        }
        $env:DevelopmentOperator__Password = $env:SMARTPARK_OPERATOR_PASSWORD
    }
}

# Optional .env settings override saved user-secrets only for this runner process.
if ($Service -eq 'User') {
    if ($env:SMARTPARK_ONBOARDING_PUBLIC_ORIGIN) { $env:Onboarding__PublicOrigin = $env:SMARTPARK_ONBOARDING_PUBLIC_ORIGIN }
    if ($env:SMARTPARK_ONBOARDING_ALLOW_HTTP_LOOPBACK) { $env:Onboarding__AllowHttpLoopback = $env:SMARTPARK_ONBOARDING_ALLOW_HTTP_LOOPBACK }
    if ($env:SMARTPARK_MESSAGING_ENABLED) { $env:Messaging__Enabled = $env:SMARTPARK_MESSAGING_ENABLED }
    foreach ($field in @('Host','Port','VirtualHost','Username','Password')) {
        $value = [Environment]::GetEnvironmentVariable('SMARTPARK_RABBITMQ_' + $field.ToUpperInvariant())
        if ($value) { [Environment]::SetEnvironmentVariable('Messaging__' + $field, $value, 'Process') }
    }
    foreach ($field in @('Host','Port','Username','Password','From')) {
        $value = [Environment]::GetEnvironmentVariable('SMARTPARK_SMTP_' + $field.ToUpperInvariant())
        if ($value) { [Environment]::SetEnvironmentVariable('Otp__Smtp__' + $field, $value, 'Process') }
    }
    if ($env:SMARTPARK_JWT_PRIVATE_KEY_PEM) { $env:Jwt__PrivateKeyPem = $env:SMARTPARK_JWT_PRIVATE_KEY_PEM }
    if ($env:SMARTPARK_WORKFLOW_KEY_DIRECTORY) {
        $keyDirectory = $env:SMARTPARK_WORKFLOW_KEY_DIRECTORY
        if (![IO.Path]::IsPathRooted($keyDirectory)) { $keyDirectory = Join-Path $taskRoot $keyDirectory }
        $env:Workflow__KeyDirectory = $keyDirectory
    }
}
$projectDir = Join-Path $taskRoot "src/Services/${Service}Service/SmartParking.${Service}Service.API"
dotnet run --project $projectDir --no-restore --no-launch-profile -- --contentRoot $projectDir --urls "http://localhost:$apiPort"
if ($LASTEXITCODE -ne 0) { throw "${Service}Service exited with code $LASTEXITCODE" }
