param(
    [Parameter(Mandatory)][ValidateSet('User','Facility','Parking','Operation','Reservation','Payment','Notification')][string]$Service
)
$ErrorActionPreference = 'Stop'
# Parking remains an alias so existing scripts and configuration continue to work.
if ($Service -eq 'Facility') { $Service = 'Parking' }
# Keep Reservation configuration keys and database settings compatible.
if ($Service -eq 'Operation') { $Service = 'Reservation' }
$taskRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $taskRoot '.env'
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')
$previousSigningEnvironment = Get-JwtSigningEnvironment
$mediaNames = @('SMARTPARK_CLOUDINARY_CLOUD_NAME','SMARTPARK_CLOUDINARY_API_KEY','SMARTPARK_CLOUDINARY_API_SECRET','SMARTPARK_USER_MEDIA_ENABLED','MediaStorage__Cloudinary__CloudName','MediaStorage__Cloudinary__ApiKey','MediaStorage__Cloudinary__ApiSecret','MediaStorage__Enabled')
$previousMediaEnvironment = @{}
foreach ($name in $mediaNames) { $previousMediaEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {

if (!(Test-Path -LiteralPath $envFile -PathType Leaf)) {
    throw "Missing $envFile. Run setup.ps1 at the repository root first."
}

# Only UserService may receive raw or mapped private signing material.
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile -IncludeUserServiceSigningSecrets:($Service -eq 'User')

# Read the parsed file, not inherited variables: removed keys must clear stale
# raw settings and override saved user-secrets for this launch. Whitespace is
# deliberately unconfigured and survives Windows PowerShell 5.1 environment IO.
foreach ($field in @{'CloudName'='CLOUD_NAME'; 'ApiKey'='API_KEY'; 'ApiSecret'='API_SECRET'}.GetEnumerator()) {
    $source = 'SMARTPARK_CLOUDINARY_' + $field.Value
    $value = $settings[$source]
    if ([string]::IsNullOrWhiteSpace($value)) { $value = ' ' }
    [Environment]::SetEnvironmentVariable($source, $value, 'Process')
    [Environment]::SetEnvironmentVariable('MediaStorage__Cloudinary__' + $field.Key, $value, 'Process')
}
if ($Service -eq 'User') {
    $env:MediaStorage__Enabled = if ($settings['SMARTPARK_USER_MEDIA_ENABLED']) { $settings['SMARTPARK_USER_MEDIA_ENABLED'] } else { 'false' }
    $env:SMARTPARK_USER_MEDIA_ENABLED = $env:MediaStorage__Enabled
} else {
    [Environment]::SetEnvironmentVariable('MediaStorage__Enabled', $null, 'Process')
    [Environment]::SetEnvironmentVariable('SMARTPARK_USER_MEDIA_ENABLED', $null, 'Process')
}

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
$env:Services__Payment = 'http://localhost:5065/'
$env:Services__Notification = 'http://localhost:5068/'
foreach ($remoteService in @('User','Parking','Reservation','Payment','Notification')) {
    $url = [Environment]::GetEnvironmentVariable('SMARTPARK_SERVICE_' + $remoteService.ToUpperInvariant() + '_URL')
    if ($url) { [Environment]::SetEnvironmentVariable('Services__' + $remoteService, $url, 'Process') }
}

$jwtIssuer = if ($env:SMARTPARK_JWT_ISSUER) { $env:SMARTPARK_JWT_ISSUER } else { 'SmartParking.UserService' }
$jwtAudience = if ($env:SMARTPARK_JWT_AUDIENCE) { $env:SMARTPARK_JWT_AUDIENCE } else { 'SmartParking' }
$jwtPath = if ($env:SMARTPARK_JWT_JWKS_PATH) { $env:SMARTPARK_JWT_JWKS_PATH } else { '/.well-known/jwks.json' }
if ($Service -eq 'User') {
    $env:Jwt__Issuer = $jwtIssuer
    $env:Jwt__Audience = $jwtAudience
    $env:Jwt__JwksPath = $jwtPath
    # A blank environment value disappears on Windows PowerShell 5.1. A space
    # explicitly overrides stale user-secrets while remaining unconfigured.
    $env:Jwt__KeyId = if ($env:SMARTPARK_JWT_KEY_ID) { $env:SMARTPARK_JWT_KEY_ID } else { ' ' }
    foreach ($field in @('PrivateKeyPem','PrivateKeyPemBase64')) {
        $sourceName = if ($field -eq 'PrivateKeyPem') { 'SMARTPARK_JWT_PRIVATE_KEY_PEM' } else { 'SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64' }
        $value = [Environment]::GetEnvironmentVariable($sourceName, 'Process')
        if (!(Test-JwtConfiguredValue $value)) { $value = ' ' }
        [Environment]::SetEnvironmentVariable('Jwt__' + $field, $value, 'Process')
    }
} else {
    $env:JwtValidation__Issuer = $jwtIssuer
    $env:JwtValidation__Audience = $jwtAudience
    $env:JwtValidation__JwksUri = ([Uri]::new([Uri]$env:Services__User, $jwtPath)).AbsoluteUri
}

# Task-scoped reservation admission configuration; credentials remain service-specific.
if ($Service -in @('User', 'Reservation')) {
    $env:ReservationAdmission__Enabled = if ($env:SMARTPARK_RESERVATION_ADMISSION_ENABLED) { $env:SMARTPARK_RESERVATION_ADMISSION_ENABLED } else { 'false' }
    $brokerPrefix = if ($Service -eq 'User') { 'SMARTPARK_RESERVATION_USER' } else { 'SMARTPARK_RESERVATION_OPERATION' }
    $env:ReservationAdmission__BrokerUser = [Environment]::GetEnvironmentVariable($brokerPrefix + '_BROKER_USER')
    $env:ReservationAdmission__BrokerPassword = [Environment]::GetEnvironmentVariable($brokerPrefix + '_BROKER_PASSWORD')
    if ([string]::IsNullOrWhiteSpace($env:ReservationAdmission__BrokerUser)) { $env:ReservationAdmission__BrokerUser = ' ' }
    if ([string]::IsNullOrWhiteSpace($env:ReservationAdmission__BrokerPassword)) { $env:ReservationAdmission__BrokerPassword = ' ' }
    if ($Service -eq 'Reservation') {
        $env:SMARTPARK_RABBITMQ_HOST = '127.0.0.1'
        $env:JwtValidation__AllowInsecureTransport = if ($env:SMARTPARK_RESERVATION_ALLOW_HTTP_JWKS) { $env:SMARTPARK_RESERVATION_ALLOW_HTTP_JWKS } else { 'false' }
        $env:Cors__AllowedOrigins = $env:SMARTPARK_OPERATION_CORS_ORIGINS
        $env:Messaging__AuditEnabled = if ($env:SMARTPARK_OPERATION_AUDIT_ENABLED) { $env:SMARTPARK_OPERATION_AUDIT_ENABLED } else { 'false' }
        if (!$env:Messaging__OutboxDeliveryEnabled) { $env:Messaging__OutboxDeliveryEnabled = 'false' }
    }
}

if ($Service -in @('Parking', 'Payment', 'Reservation')) {
    $env:ReservationIntegration__Enabled = if ($env:SMARTPARK_RESERVATION_FACILITY_ENABLED) { $env:SMARTPARK_RESERVATION_FACILITY_ENABLED } else { 'false' }
    if ($Service -eq 'Reservation') {
        $env:ReservationIntegration__AllowHttp = if ($env:SMARTPARK_RESERVATION_FACILITY_ALLOW_HTTP) { $env:SMARTPARK_RESERVATION_FACILITY_ALLOW_HTTP } else { 'false' }
        $env:ReservationIntegration__OperationsPrivateKeyPath = if ($env:SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PATH) { $env:SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PATH } else { '' }
    } else {
        $env:ReservationIntegration__OperationsPublicKeyPath = if ($env:SMARTPARK_FACILITY_OPERATION_PUBLIC_KEY_PATH) { $env:SMARTPARK_FACILITY_OPERATION_PUBLIC_KEY_PATH } else { '' }
    }
}
$dbPortByService = @{ User = 'SMARTPARK_DB_USER_PORT'; Parking = 'SMARTPARK_DB_PARKING_PORT'; Reservation = 'SMARTPARK_DB_RESERVATION_PORT'; Payment = 'SMARTPARK_DB_PAYMENT_PORT' }
$defaultDbPortByService = @{ User = '5441'; Parking = '5442'; Reservation = '5443'; Payment = '5444' }
$apiPortByService = @{ User = '5035'; Parking = '5045'; Reservation = '5055'; Payment = '5065' }

$dbPort = [Environment]::GetEnvironmentVariable($dbPortByService[$Service])
if ([string]::IsNullOrWhiteSpace($dbPort)) { $dbPort = $defaultDbPortByService[$Service] }
$apiPort = $apiPortByService[$Service]

$dbName = @{ User='smartpark_user'; Parking='smartpark_facility'; Reservation='smartpark_operations'; Payment='smartpark_payment'; Notification='smartpark_notification' }[$Service]
# Quote the password for Npgsql syntax so ';' inside a password is safe.
$quotedPassword = $env:SMARTPARK_DEV_PASSWORD.Replace('"', '""')
[Environment]::SetEnvironmentVariable('ConnectionStrings__' + $Service, "Host=127.0.0.1;Port=$dbPort;Database=$dbName;Username=$dbName;Password=`"$quotedPassword`"", 'Process')
if ($Service -eq 'Notification') {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__NotificationAudit', "Host=127.0.0.1;Port=$dbPort;Database=$dbName;Username=$dbName;Password=`"$quotedPassword`"", 'Process')
}

if ($Service -eq 'User' -and $env:SMARTPARK_ADMIN_PASSWORD) {
    $adminEmail = $env:SMARTPARK_ADMIN_EMAIL
    if ([string]::IsNullOrWhiteSpace($adminEmail)) { $adminEmail = 'admin@smartpark.local' }
    $env:DevelopmentAdmin__Email = $adminEmail
    $env:DevelopmentAdmin__Password = $env:SMARTPARK_ADMIN_PASSWORD
}
if ($Service -eq 'User') {
    $env:SMARTPARK_RABBITMQ_HOST = '127.0.0.1'
    $env:SMARTPARK_RABBITMQ_PORT = $env:SMARTPARK_RABBITMQ_PORT
    $env:SMARTPARK_RABBITMQ_USER = $env:SMARTPARK_RABBITMQ_USER
    $env:SMARTPARK_RABBITMQ_PASSWORD = $env:SMARTPARK_RABBITMQ_PASSWORD
    $env:SMARTPARK_RABBITMQ_VHOST = $env:SMARTPARK_RABBITMQ_VHOST
    $env:Cors__AllowedOrigins = $env:SMARTPARK_USER_CORS_ORIGINS
    if (!$env:Messaging__OutboxDeliveryEnabled) { $env:Messaging__OutboxDeliveryEnabled = 'false' }
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
if ($Service -eq 'Payment') {
    # PaymentService resolves the reservation API through Services:ReservationService:BaseUrl.
    $reservationUrl = $env:SMARTPARK_SERVICE_RESERVATION_URL
    if ([string]::IsNullOrWhiteSpace($reservationUrl)) { $reservationUrl = 'http://localhost:5055/' }
    $env:Services__ReservationService__BaseUrl = $reservationUrl
    # Empty payOS values keep the sandbox emulation and saved user-secrets intact.
    if ($env:SMARTPARK_PAYOS_CLIENT_ID) { $env:PayOs__ClientId = $env:SMARTPARK_PAYOS_CLIENT_ID }
    if ($env:SMARTPARK_PAYOS_API_KEY) { $env:PayOs__ApiKey = $env:SMARTPARK_PAYOS_API_KEY }
    if ($env:SMARTPARK_PAYOS_CHECKSUM_KEY) { $env:PayOs__ChecksumKey = $env:SMARTPARK_PAYOS_CHECKSUM_KEY }
    if ($env:SMARTPARK_PAYOS_BASE_URL) { $env:PayOs__BaseUrl = $env:SMARTPARK_PAYOS_BASE_URL }
}
if ($Service -eq 'User') {
    if ($env:SMARTPARK_ONBOARDING_PUBLIC_ORIGIN) { $env:Onboarding__PublicOrigin = $env:SMARTPARK_ONBOARDING_PUBLIC_ORIGIN }
    if ($env:SMARTPARK_ONBOARDING_ALLOW_HTTP_LOOPBACK) { $env:Onboarding__AllowHttpLoopback = $env:SMARTPARK_ONBOARDING_ALLOW_HTTP_LOOPBACK }
    if ($env:SMARTPARK_MESSAGING_ENABLED) { $env:Messaging__Enabled = $env:SMARTPARK_MESSAGING_ENABLED }
    foreach ($field in @('Host','Port','Username','Password','From')) {
        $value = [Environment]::GetEnvironmentVariable('SMARTPARK_SMTP_' + $field.ToUpperInvariant())
        if ($value) { [Environment]::SetEnvironmentVariable('Otp__Smtp__' + $field, $value, 'Process') }
    }
    if ($env:SMARTPARK_TURNSTILE_ENABLED) { $env:Turnstile__Enabled = $env:SMARTPARK_TURNSTILE_ENABLED }
    if ($env:SMARTPARK_TURNSTILE_SECRET_KEY) { $env:Turnstile__SecretKey = $env:SMARTPARK_TURNSTILE_SECRET_KEY }
    if ($env:SMARTPARK_WORKFLOW_KEY_DIRECTORY) {
        $keyDirectory = $env:SMARTPARK_WORKFLOW_KEY_DIRECTORY
        if (![IO.Path]::IsPathRooted($keyDirectory)) { $keyDirectory = Join-Path $taskRoot $keyDirectory }
        $env:Workflow__KeyDirectory = $keyDirectory
    }
}
if ($Service -eq 'Notification') {
    $projectDir = Join-Path $taskRoot "src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API"
    $serviceDisplay = 'NotificationAuditService'
} else {
    $projectService = switch ($Service) { 'Parking' { 'Facility' } 'Reservation' { 'Operation' } default { $Service } }
    $projectDir = Join-Path $taskRoot "src/Services/${projectService}Service/SmartParking.${projectService}Service.API"
    $serviceDisplay = "${projectService}Service"
}
dotnet run --project $projectDir --no-restore --no-launch-profile -- --contentRoot $projectDir --urls "http://localhost:$apiPort"
if ($LASTEXITCODE -ne 0) { throw "$serviceDisplay exited with code $LASTEXITCODE" }
} finally {
    # Do not leave UserService .env signing secrets in the launching shell.
    Restore-JwtSigningEnvironment $previousSigningEnvironment
    foreach ($name in $mediaNames) { [Environment]::SetEnvironmentVariable($name, $previousMediaEnvironment[$name], 'Process') }
}
