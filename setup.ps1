param(
    [switch]$SkipDatabase, [switch]$SkipRestore, [switch]$SkipUserSecrets,
    [ValidateSet('Local','Team')][string]$CredentialMode = 'Local',
    [string]$TeamEnvPath, [switch]$WithSonarQube
)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$composeFile = Join-Path $taskRoot 'compose.schema-integration.yml'
$envFile = Join-Path $taskRoot '.env'

function New-LocalSecret([int]$Bytes = 32) {
    $buffer = New-Object byte[] $Bytes
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($buffer) } finally { $random.Dispose() }
    [Convert]::ToBase64String($buffer)
}

# Read existing secrets without printing their values or invoking `list`.
$existingByService = @{}
$projectByService = @{}
foreach ($service in @('User','Parking','Reservation')) {
    $projectDir = Join-Path $taskRoot "src/Services/${service}Service/SmartParking.${service}Service.API"
    $projectByService[$service] = $projectDir
    [xml]$projectXml = Get-Content (Join-Path $projectDir "SmartParking.${service}Service.API.csproj") -Raw
    $secretId = [string](@($projectXml.Project.PropertyGroup.UserSecretsId | Where-Object { $_ })[0])
    $secretFile = Join-Path ([Environment]::GetFolderPath('ApplicationData')) "Microsoft/UserSecrets/$secretId/secrets.json"
    $existing = @{}
    if (Test-Path -LiteralPath $secretFile -PathType Leaf) {
        $saved = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
        foreach ($property in $saved.PSObject.Properties) { $existing[$property.Name] = $property.Value }
    }
    $existingByService[$service] = $existing
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET SDK 10 before setup.' }
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.') { throw 'Setup requires .NET SDK 10.' }
$envExists = Test-Path -LiteralPath $envFile -PathType Leaf
$inputEnv = if ($envExists) { $envFile } else { Join-Path $taskRoot '.env.example' }
$settings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $inputEnv -NoExport
$generated = @{}
if ($TeamEnvPath -and $CredentialMode -ne 'Team') { throw 'Use -CredentialMode Team with -TeamEnvPath.' }
if ($CredentialMode -eq 'Team') {
    if (!$TeamEnvPath) { throw 'Team mode requires -TeamEnvPath to a private team credential file.' }
    $team = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $TeamEnvPath -NoExport
    foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD')) {
        if (!$team[$name] -or $team[$name] -like 'replace-with-*') { throw "Team file must configure $name." }
    }
    foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD','SONAR_DB_PASSWORD','VITE_MAPTILER_API_KEY','SMARTPARK_SERVICE_USER_URL','SMARTPARK_SERVICE_PARKING_URL','SMARTPARK_SERVICE_RESERVATION_URL')) {
        if (!$team[$name] -or $team[$name] -like 'replace-with-*') { continue }
        if ($settings[$name] -and $settings[$name] -notlike 'replace-with-*' -and $settings[$name] -cne $team[$name]) {
            throw "Team/local conflict for $name. Existing configuration was preserved; reconcile it explicitly before retrying."
        }
        if (!$settings[$name] -or $settings[$name] -like 'replace-with-*') {
            $settings[$name] = $team[$name]
            $generated[$name] = $team[$name]
        }
    }
}
foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD')) {
    if (!$settings[$name] -or $settings[$name] -like 'replace-with-*') {
        $value = $null
        if ($name -eq 'SMARTPARK_SERVICE_KEY') {
            $keys = @($existingByService.Values | ForEach-Object { $_['Services:Key'] } | Where-Object { $_ } | Select-Object -Unique)
            if ($keys.Count -gt 1) { throw 'Existing service keys differ. Reconcile Services:Key before setup.' }
            if ($keys.Count -eq 1) { $value = $keys[0] }
        } elseif ($name -eq 'SMARTPARK_ADMIN_PASSWORD') {
            $value = $existingByService['User']['DevelopmentAdmin:Password']
        } else {
            # Reuse an existing local PostgreSQL password when configuring an old volume.
            $passwords = @()
            foreach ($service in @('User','Parking','Reservation')) {
                $connection = $existingByService[$service]["ConnectionStrings:$service"]
                if ($connection) {
                    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
                    $builder.ConnectionString = $connection
                    if ($builder.ContainsKey('Password')) { $passwords += [string]$builder['Password'] }
                }
            }
            $passwords = @($passwords | Select-Object -Unique)
            if ($passwords.Count -gt 1) { throw 'Existing database passwords differ. Configure SMARTPARK_DEV_PASSWORD explicitly.' }
            if ($passwords.Count -eq 1) { $value = $passwords[0] }
            if (!$value -and !$SkipDatabase) {
                $volumes = & docker volume ls --filter 'label=com.docker.compose.project=smartpark-schema' --format '{{.Name}}' 2>$null
                if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker volumes. Start Docker Desktop and retry, or use -SkipDatabase.' }
                if ($volumes) { throw 'Existing database volumes found. Recover their original SMARTPARK_DEV_PASSWORD in .env; setup will not replace or delete data.' }
            }
        }
        if (!$value) {
            $value = if ($name -eq 'SMARTPARK_ADMIN_PASSWORD') { 'Aa1!' + (New-LocalSecret 6) } else { New-LocalSecret }
        }
        if ($value -match "[\r\n']") { throw "Set $name directly in .env; its existing value cannot be safely represented automatically." }
        $settings[$name] = $value
        $generated[$name] = $value
    }
}
foreach ($name in $generated.Keys) {
    if ($generated[$name] -match "[\r\n']") { throw "Configure $name manually; its value cannot be safely written automatically." }
}
if ($settings['SMARTPARK_SERVICE_KEY'].Length -lt 32) { throw 'SMARTPARK_SERVICE_KEY must contain at least 32 characters.' }
if ($settings['SMARTPARK_ADMIN_PASSWORD'] -cnotmatch '^(?=.{8,15}$)(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$') {
    throw 'SMARTPARK_ADMIN_PASSWORD requires 8-15 characters, uppercase, lowercase, digit and special character.'
}
foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT','SMARTPARK_DB_PAYMENT_PORT','SMARTPARK_DB_NOTIFICATION_PORT','SMARTPARK_DB_IOT_PORT')) {
    $port = 0
    if (![int]::TryParse($settings[$name], [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw "Invalid port: $name." }
}
if ($settings['VITE_MAPTILER_API_KEY'] -match "[\r\n']") { throw 'Configure MapTiler key directly in FE/.env.' }
if (!$SkipUserSecrets) {
    foreach ($service in @('User','Parking','Reservation')) {
        $saved = $existingByService[$service]
        if ($saved['Services:Key'] -and $saved['Services:Key'] -cne $settings['SMARTPARK_SERVICE_KEY']) {
            throw "$service Services:Key differs from .env. Reconcile user-secrets or use -SkipUserSecrets and the .env runner."
        }
        foreach ($remoteService in @('User','Parking','Reservation')) {
            $url = $settings["SMARTPARK_SERVICE_$($remoteService.ToUpperInvariant())_URL"]
            if ($url -and $saved["Services:$remoteService"] -and $saved["Services:$remoteService"] -cne $url) {
                throw "$service Services:$remoteService differs from .env. Reconcile user-secrets or use -SkipUserSecrets and the .env runner."
            }
        }
        if ($saved["ConnectionStrings:$service"]) {
            $connection = New-Object System.Data.Common.DbConnectionStringBuilder
            $connection.ConnectionString = $saved["ConnectionStrings:$service"]
            if ($connection.ContainsKey('Password') -and $connection['Password'] -cne $settings['SMARTPARK_DEV_PASSWORD']) {
                throw "$service database password differs from .env. Restore the volume password; -SkipUserSecrets requires using the .env runner."
            }
        }
    }
    if ($existingByService['User']['DevelopmentAdmin:Password'] -and $existingByService['User']['DevelopmentAdmin:Password'] -cne $settings['SMARTPARK_ADMIN_PASSWORD']) {
        throw 'Admin password differs from user-secrets. Reconcile it or use -SkipUserSecrets and the .env runner. Setup does not change an existing Admin password in DB.'
    }
}
foreach ($service in @('USER','PARKING','RESERVATION')) {
    $name = "SMARTPARK_SERVICE_${service}_URL"
    if ($settings[$name]) {
        $uri = $null
        if (![Uri]::TryCreate($settings[$name], [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http','https')) { throw "Invalid service URL: $name." }
    }
}
if ($WithSonarQube -and (!$settings['SONAR_DB_PASSWORD'] -or $settings['SONAR_DB_PASSWORD'] -like 'replace-with-*')) {
    throw 'Configure SONAR_DB_PASSWORD before using -WithSonarQube.'
}
if ($WithSonarQube) {
    $sonarPort = 0
    if (![int]::TryParse($settings['SONAR_PORT'], [ref]$sonarPort) -or $sonarPort -lt 1 -or $sonarPort -gt 65535) { throw 'Invalid SONAR_PORT.' }
}
if (!$envExists -or $generated.Count) {
    $lines = @(Get-Content -LiteralPath $inputEnv -Encoding UTF8)
    foreach ($name in $generated.Keys) {
        $found = $false
        $lines = @($lines | ForEach-Object {
            if ($_ -match ('^\s*' + [regex]::Escape($name) + '\s*=')) {
                $found = $true
                # Single quotes prevent Compose interpolation of a literal dollar sign.
                "$name='$($generated[$name])'"
            } else { $_ }
        })
        if (!$found) { $lines += "$name='$($generated[$name])'" }
    }
    [IO.File]::WriteAllLines($envFile, $lines, (New-Object Text.UTF8Encoding($false)))
    Write-Output 'Filled missing local credentials; existing configured values were preserved.'
}
$settings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $envFile
if ($settings['SMARTPARK_SERVICE_KEY'].Length -lt 32) { throw 'SMARTPARK_SERVICE_KEY must contain at least 32 characters.' }
$adminPassword = $settings['SMARTPARK_ADMIN_PASSWORD']
if ($adminPassword.Length -lt 8 -or $adminPassword.Length -gt 15 -or
    $adminPassword -cnotmatch '^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$') {
    throw 'SMARTPARK_ADMIN_PASSWORD requires 8-15 characters, uppercase, lowercase, digit and special character.'
}
foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT')) {
    $port = 0
    if (![int]::TryParse($settings[$name], [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw "Invalid port: $name." }
}

if (!$SkipDatabase) {
    & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose configuration is invalid.' }
    & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Database startup failed. Check Docker Desktop, ports and SQL initialization logs. Existing volumes were preserved.' }
    foreach ($service in @('User','Parking','Reservation')) {
        $container = $service.ToLowerInvariant() + '-db'
        # Container env is used internally; credentials never become command arguments.
        # Send shell code through stdin: PowerShell 5.1 strips native argument quotes.
        # The trailing comment also consumes its Windows CRLF line terminator.
        $probe = 'PGPASSWORD="$POSTGRES_PASSWORD" psql -h 127.0.0.1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -q -c "SELECT 1" # local authentication probe'
        $probe | & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile exec -T $container sh -s | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "$service database authentication failed. Restore the password used to initialize this volume; no data was reset." }
        & (Join-Path $taskRoot 'scripts/apply-workflow-migrations.ps1') -Service $service -SchemaRoot $taskRoot -EnvPath $envFile
    }
}

if (!$SkipUserSecrets) { foreach ($service in @('User','Parking','Reservation')) {
    $database = 'smartpark_' + $service.ToLowerInvariant()
    $port = $settings["SMARTPARK_DB_$($service.ToUpperInvariant())_PORT"]
    $password = $settings['SMARTPARK_DEV_PASSWORD'].Replace('"','""')
    $desired = [ordered]@{
        'Services:Key' = $settings['SMARTPARK_SERVICE_KEY']
        'Services:User' = 'http://localhost:5035/'
        'Services:Parking' = 'http://localhost:5045/'
        'Services:Reservation' = 'http://localhost:5055/'
    }
    $desired["ConnectionStrings:$service"] = "Host=127.0.0.1;Port=$port;Database=$database;Username=$database;Password=`"$password`""
    foreach ($remoteService in @('User','Parking','Reservation')) {
        $url = $settings["SMARTPARK_SERVICE_$($remoteService.ToUpperInvariant())_URL"]
        if ($url) { $desired["Services:$remoteService"] = $url }
    }
    if ($service -eq 'User') {
        $desired['DevelopmentAdmin:Email'] = $settings['SMARTPARK_ADMIN_EMAIL']
        $desired['DevelopmentAdmin:Password'] = $adminPassword
        $keyDirectory = $settings['SMARTPARK_WORKFLOW_KEY_DIRECTORY']
        if (!$keyDirectory) { $keyDirectory = '.cache/workflow-keys' }
        if (![IO.Path]::IsPathRooted($keyDirectory)) { $keyDirectory = Join-Path $taskRoot $keyDirectory }
        $desired['Workflow:KeyDirectory'] = $keyDirectory
        foreach ($field in @('Host','Port','Username','Password','From')) {
            $value = $settings["SMARTPARK_SMTP_$($field.ToUpperInvariant())"]
            if ($value) { $desired["Otp:Smtp:$field"] = $value }
        }
        if ($settings['SMARTPARK_JWT_PRIVATE_KEY_PEM']) { $desired['Jwt:PrivateKeyPem'] = $settings['SMARTPARK_JWT_PRIVATE_KEY_PEM'] }
    }
    $missing = @{}
    foreach ($name in $desired.Keys) {
        if (!$existingByService[$service].ContainsKey($name)) { $missing[$name] = $desired[$name] }
    }
    if ($missing.Count) {
        $previousEncoding = $OutputEncoding
        try {
            $OutputEncoding = New-Object Text.UTF8Encoding($false)
            $missing | ConvertTo-Json | & dotnet user-secrets set --project $projectByService[$service] | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Cannot configure missing user-secrets for $service." }
        } finally { $OutputEncoding = $previousEncoding }
    }
    Write-Output "$service configuration ready; existing user-secrets were preserved."
} }
$frontendTemplate = Join-Path $taskRoot 'FE/.env.example'
$frontendEnv = Join-Path $taskRoot 'FE/.env'
if ((Test-Path $frontendTemplate) -and !(Test-Path $frontendEnv)) { Copy-Item $frontendTemplate $frontendEnv }
if ($settings['VITE_MAPTILER_API_KEY']) {
    $frontendSettings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $frontendEnv -NoExport
    if (!$frontendSettings['VITE_MAPTILER_API_KEY']) {
        $mapKey = $settings['VITE_MAPTILER_API_KEY']
        if ($mapKey -match "[\r\n']") { throw 'Configure MapTiler key directly in FE/.env.' }
        $frontendLines = @(Get-Content -LiteralPath $frontendEnv -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*VITE_MAPTILER_API_KEY\s*=' })
        $frontendLines += "VITE_MAPTILER_API_KEY='$mapKey'"
        [IO.File]::WriteAllLines($frontendEnv, $frontendLines, (New-Object Text.UTF8Encoding($false)))
    } elseif ($frontendSettings['VITE_MAPTILER_API_KEY'] -cne $settings['VITE_MAPTILER_API_KEY']) {
        Write-Warning 'FE/.env has a different MapTiler key; existing frontend configuration was preserved.'
    }
}
if ($WithSonarQube) {
    $sonarCompose = Join-Path $taskRoot 'compose.sonar.yml'
    & docker compose --project-name smartpark-sonar --env-file $envFile -f $sonarCompose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'SonarQube Compose configuration is invalid.' }
    & docker compose --project-name smartpark-sonar --env-file $envFile -f $sonarCompose up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'SonarQube startup failed. Check logs and existing DB password; no volume was reset.' }
}
if (!$SkipRestore) {
    & dotnet restore (Join-Path $taskRoot 'SmartParking.slnx') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
}
Write-Output 'Setup complete. Run scripts/run-schema-service.ps1 in separate terminals for User, Parking and Reservation.'
