param(
    [Parameter(Mandatory)][ValidateSet('User','Facility','Parking','Operation','Reservation','Payment','Assistant')][string]$Service
)
$ErrorActionPreference = 'Stop'
# Parking remains an alias so existing scripts and configuration continue to work.
if ($Service -eq 'Facility') { $Service = 'Parking' }
# Keep Reservation configuration keys and database settings compatible.
if ($Service -eq 'Operation') { $Service = 'Reservation' }
$taskRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $taskRoot '.env'
$priorSigningEnvironment = @{}
foreach ($name in @('Jwt__PrivateKeyPem','SMARTPARK_JWT_PRIVATE_KEY_PEM','SMARTPARK_JWT_PRIVATE_KEY_FILE')) {
    $priorSigningEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {

if (!(Test-Path -LiteralPath $envFile -PathType Leaf)) {
    throw "Missing $envFile. Run setup.ps1 at the repository root first."
}

# Load .env through the shared parser; it exports every key to the process env.
$null = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile

# Normal Assistant host uses its own reviewed datasource/trust/keyring configuration.
# Keep this before the legacy service defaults: Assistant must not inherit guessed owner URLs.
if ($Service -eq 'Assistant') {
    # PEM is multiline; root .env parser intentionally handles single-line values.
    # An explicit public-key file avoids persisting escaped/malformed PEM in .env.
    if ($env:Assistant__Jwt__PublicKeyFile) {
        $keyFile = $env:Assistant__Jwt__PublicKeyFile
        $keyAbsolute = [IO.Path]::IsPathRooted($keyFile)
        if ([IO.Path]::DirectorySeparatorChar -eq '\') {
            $keyAbsolute = $keyFile -match '^[A-Za-z]:[\\/]' -or $keyFile -match '^\\\\[^\\/]+[\\/][^\\/]+'
        }
        if (!$keyAbsolute -or !(Test-Path -LiteralPath $keyFile -PathType Leaf)) {
            throw 'Assistant public-key file must exist and be absolute.'
        }
        $env:Assistant__Jwt__PublicKeyPem = [IO.File]::ReadAllText($keyFile)
    }
    function Require-AssistantSetting([string]$Name) {
        $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
        if ([string]::IsNullOrWhiteSpace($value) -or $value.StartsWith('replace-with-')) {
            throw "Missing Assistant setting: $Name. Configure it locally after deployment review."
        }
        return $value
    }
    foreach ($name in @('ConnectionStrings__IoT','Assistant__Jwt__Issuer','Assistant__Jwt__Audience','Assistant__Jwt__PublicKeyPem',
        'Assistant__Management__KeyDirectory','Assistant__Guest__KnowledgeDirectory')) {
        $null = Require-AssistantSetting $name
    }
    foreach ($name in @('Assistant__Management__KeyDirectory','Assistant__Guest__KnowledgeDirectory')) {
        $path = Require-AssistantSetting $name
        # PS 5.1 lacks Path.IsPathFullyQualified. Reject Windows drive-relative and
        # root-relative forms explicitly; a rooted path alone is insufficient.
        $fullyQualified = [IO.Path]::IsPathRooted($path)
        if ([IO.Path]::DirectorySeparatorChar -eq '\') {
            $fullyQualified = $path -match '^[A-Za-z]:[\\/]' -or $path -match '^\\\\[^\\/]+[\\/][^\\/]+'
        }
        if (!$fullyQualified -or !(Test-Path -LiteralPath $path -PathType Container)) {
            throw "Assistant directory must exist and be absolute: $name. No directory was created."
        }
    }
    foreach ($owner in @('User','Operations','Search')) {
        $enabled = [Environment]::GetEnvironmentVariable("Assistant__Reads__${owner}__Enabled", 'Process')
        if ($enabled -and $enabled -notmatch '^(true|false)$') { throw "Invalid Assistant read flag: $owner" }
        if ($enabled -eq 'true') {
            $origin = Require-AssistantSetting "Assistant__Reads__${owner}__Origin"
            $uri = $null
            if (![Uri]::TryCreate($origin, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or
                $uri.UserInfo -or $uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment) {
                throw "Assistant read requires a fixed HTTPS origin: $owner"
            }
        }
    }
    $listen = Require-AssistantSetting 'SMARTPARK_ASSISTANT_URL'
    $listenUri = $null
    if (![Uri]::TryCreate($listen, [UriKind]::Absolute, [ref]$listenUri) -or !$listenUri.IsLoopback -or
        $listenUri.Scheme -ne 'https' -or $listenUri.UserInfo -or $listenUri.AbsolutePath -ne '/' -or $listenUri.Query -or $listenUri.Fragment) {
        throw 'SMARTPARK_ASSISTANT_URL requires a loopback HTTPS origin.'
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $projectDir = Join-Path $taskRoot 'src/Services/IoTService/SmartParking.IoTService.API'
    foreach ($name in $priorSigningEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    dotnet run --project $projectDir --no-restore --no-launch-profile -- --contentRoot $projectDir --urls $listen
    if ($LASTEXITCODE -ne 0) { throw "Assistant exited with code $LASTEXITCODE" }
    return
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
foreach ($remoteService in @('User','Parking','Reservation','Payment')) {
    $url = [Environment]::GetEnvironmentVariable('SMARTPARK_SERVICE_' + $remoteService.ToUpperInvariant() + '_URL')
    if ($url) { [Environment]::SetEnvironmentVariable('Services__' + $remoteService, $url, 'Process') }
}

$dbPortByService = @{ User = 'SMARTPARK_DB_USER_PORT'; Parking = 'SMARTPARK_DB_PARKING_PORT'; Reservation = 'SMARTPARK_DB_RESERVATION_PORT'; Payment = 'SMARTPARK_DB_PAYMENT_PORT' }
$defaultDbPortByService = @{ User = '5441'; Parking = '5442'; Reservation = '5443'; Payment = '5444' }
$apiPortByService = @{ User = '5035'; Parking = '5045'; Reservation = '5055'; Payment = '5065' }

$dbPort = [Environment]::GetEnvironmentVariable($dbPortByService[$Service])
if ([string]::IsNullOrWhiteSpace($dbPort)) { $dbPort = $defaultDbPortByService[$Service] }
$apiPort = $apiPortByService[$Service]
$listenUrls = "http://localhost:$apiPort"
$listenSetting = 'SMARTPARK_SERVICE_' + $Service.ToUpperInvariant() + '_LISTEN_URLS'
$configuredListen = [Environment]::GetEnvironmentVariable($listenSetting, 'Process')
if (![string]::IsNullOrWhiteSpace($configuredListen)) {
    # Add HTTPS alongside existing HTTP when reviewed local trust is available.
    # Do not install certificates or weaken HttpClient certificate validation here.
    foreach ($endpoint in $configuredListen.Split(';')) {
        $listenUri = $null
        if (![Uri]::TryCreate($endpoint, [UriKind]::Absolute, [ref]$listenUri) -or !$listenUri.IsLoopback -or
            $listenUri.Scheme -notin @('http','https') -or $listenUri.UserInfo -or $listenUri.AbsolutePath -ne '/' -or
            $listenUri.Query -or $listenUri.Fragment) {
            throw "Invalid local listener configuration: $listenSetting. Expected loopback HTTP(S) origins."
        }
    }
    $listenUrls = $configuredListen
}

$dbName = @{ User='smartpark_user'; Parking='smartpark_facility'; Reservation='smartpark_operations'; Payment='smartpark_payment' }[$Service]
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
    if ($env:SMARTPARK_JWT_PRIVATE_KEY_PEM) { $env:Jwt__PrivateKeyPem = $env:SMARTPARK_JWT_PRIVATE_KEY_PEM }
    if ($env:SMARTPARK_JWT_PRIVATE_KEY_FILE) {
        $privateKeyFile = $env:SMARTPARK_JWT_PRIVATE_KEY_FILE
        $absolute = [IO.Path]::IsPathRooted($privateKeyFile)
        if ([IO.Path]::DirectorySeparatorChar -eq '\') {
            $absolute = $privateKeyFile -match '^[A-Za-z]:[\\/]' -or $privateKeyFile -match '^\\\\[^\\/]+[\\/][^\\/]+'
        }
        if (!$absolute -or !(Test-Path -LiteralPath $privateKeyFile -PathType Leaf)) {
            throw 'User signing-key file must exist and be absolute.'
        }
        $env:Jwt__PrivateKeyPem = [IO.File]::ReadAllText($privateKeyFile)
    }
    if ($env:SMARTPARK_WORKFLOW_KEY_DIRECTORY) {
        $keyDirectory = $env:SMARTPARK_WORKFLOW_KEY_DIRECTORY
        if (![IO.Path]::IsPathRooted($keyDirectory)) { $keyDirectory = Join-Path $taskRoot $keyDirectory }
        $env:Workflow__KeyDirectory = $keyDirectory
    }
}
$projectService = switch ($Service) { 'Parking' { 'Facility' } 'Reservation' { 'Operation' } default { $Service } }
$projectDir = Join-Path $taskRoot "src/Services/${projectService}Service/SmartParking.${projectService}Service.API"
dotnet run --project $projectDir --no-restore --no-launch-profile -- --contentRoot $projectDir --urls $listenUrls
if ($LASTEXITCODE -ne 0) { throw "${projectService}Service exited with code $LASTEXITCODE" }
} finally {
    foreach ($name in $priorSigningEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $priorSigningEnvironment[$name], 'Process') }
}
