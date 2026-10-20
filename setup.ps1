param(
    [switch]$SkipDatabase, [switch]$SkipRestore, [switch]$SkipUserSecrets, [switch]$RecreateDb,
    [ValidateSet('Local','Team')][string]$CredentialMode = 'Local',
    [string]$TeamEnvPath, [switch]$SkipSonarQube, [switch]$NonInteractive,
    [switch]$WithAiCamera,
    # Deprecated: SonarQube now starts by default; kept so older commands still parse.
    [switch]$WithSonarQube
)
# -RecreateDb: DROP schema volumes and re-initialize all 6 DBs from SQL init scripts. Destructive — wipes dev data. Required when DB schema layout changes (e.g., SRS v1.0 hybrid migration).
$ErrorActionPreference = 'Stop'
# SonarQube credentials are always prepared (like the database passwords); the
# stack starts on a full run. -SkipSonarQube disables both; -SkipDatabase still
# prepares credentials but starts no containers (it must stay Docker-free).
$sonarEnabled = !$SkipSonarQube
$taskRoot = $PSScriptRoot
$composeFile = Join-Path $taskRoot 'compose.schema-integration.yml'
$envFile = Join-Path $taskRoot '.env'

function New-LocalSecret([int]$Bytes = 32) {
    $buffer = New-Object byte[] $Bytes
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($buffer) } finally { $random.Dispose() }
    [Convert]::ToBase64String($buffer)
}

function Assert-DevelopmentPassword([string]$Name, [string]$Value) {
    if ([string]::IsNullOrEmpty($Value) -or $Value.Length -lt 8 -or $Value.Length -gt 15 -or
        $Value -cnotmatch '^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$') {
        throw "$Name requires 8-15 characters, uppercase, lowercase, digit and special character."
    }
}

# Parse Password from a secrets connection string. DbConnectionStringBuilder can fail to
# tokenize some raw values (it collapses them into a single "ConnectionString" key), so
# fall back to explicit Password= regex matching.
function Get-SecretConnectionStringPassword([string]$ConnectionString) {
    if ([string]::IsNullOrEmpty($ConnectionString)) { return $null }
    try {
        $builder = New-Object System.Data.Common.DbConnectionStringBuilder
        $builder.ConnectionString = $ConnectionString
        if ($builder.ContainsKey('Password')) { return [string]$builder['Password'] }
    } catch { }
    if ($ConnectionString -match 'Password="([^"]*)"') { return $Matches[1] }
    if ($ConnectionString -match 'Password=([^;]+)') { return $Matches[1] }
    return $null
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

Write-Host '[1/7] Checking prerequisites and loading .env configuration...' -ForegroundColor Cyan
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET SDK 10 before setup.' }
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.') { throw 'Setup requires .NET SDK 10.' }
$envExists = Test-Path -LiteralPath $envFile -PathType Leaf
if ($envExists -and -not $NonInteractive -and -not [Console]::IsInputRedirected) {
    Write-Host "  .env already exists: $envFile" -ForegroundColor Yellow
    $answer = Read-Host '  [O]verwrite / [K]eep / [A]bort (Enter = Keep)'
    if ($answer -match '^\s*[Aa]') { Write-Host 'Aborted by user.' -ForegroundColor Red; exit 1 }
    if ($answer -match '^\s*[Oo]') {
        Copy-Item -LiteralPath $envFile -Destination "$envFile.bak" -Force
        $envExists = $false
        Write-Host "  Existing .env backed up to $envFile.bak; regenerating from .env.example." -ForegroundColor DarkGray
    }
}
$inputEnv = if ($envExists) { $envFile } else { Join-Path $taskRoot '.env.example' }
$settings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $inputEnv -NoExport
$generated = @{}
Write-Host '[2/7] Resolving credentials (reuse existing, generate missing)...' -ForegroundColor Cyan
if ($TeamEnvPath -and $CredentialMode -ne 'Team') { throw 'Use -CredentialMode Team with -TeamEnvPath.' }
if ($CredentialMode -eq 'Team') {
    if (!$TeamEnvPath) { throw 'Team mode requires -TeamEnvPath to a private team credential file.' }
    $team = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $TeamEnvPath -NoExport
    foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD')) {
        if (!$team[$name] -or $team[$name] -like 'replace-with-*') { throw "Team file must configure $name." }
    }
    foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD','SONAR_DB_PASSWORD','VITE_MAPTILER_API_KEY','SMARTPARK_SERVICE_USER_URL','SMARTPARK_SERVICE_PARKING_URL','SMARTPARK_SERVICE_RESERVATION_URL')) {
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
foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
    if (!$settings[$name] -or $settings[$name] -like 'replace-with-*') {
        $value = $null
        if ($name -eq 'SMARTPARK_SERVICE_KEY') {
            $keys = @($existingByService.Values | ForEach-Object { $_['Services:Key'] } | Where-Object { $_ } | Select-Object -Unique)
            if ($keys.Count -gt 1) { throw 'Existing service keys differ. Reconcile Services:Key before setup.' }
            if ($keys.Count -eq 1) { $value = $keys[0] }
        } elseif ($name -in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
            if ($name -eq 'SMARTPARK_ADMIN_PASSWORD') {
                $value = $existingByService['User']['DevelopmentAdmin:Password']
            }
            if (!$value) {
                # 4 fixed chars + 8 base64 chars = 12 chars; always has upper/lower/digit/special.
                $value = 'Aa1!' + (New-LocalSecret 6)
            }
        } else {
            # Reuse an existing local PostgreSQL password when configuring an old volume.
            $passwords = @()
            foreach ($service in @('User','Parking','Reservation')) {
                $connection = $existingByService[$service]["ConnectionStrings:$service"]
                $extracted = Get-SecretConnectionStringPassword $connection
                if ($extracted) { $passwords += $extracted }
            }
            $passwords = @($passwords | Select-Object -Unique)
            if ($passwords.Count -gt 1) { throw 'Existing database passwords differ. Configure SMARTPARK_DEV_PASSWORD explicitly.' }
            if ($passwords.Count -eq 1) { $value = $passwords[0] }
            if (!$value -and !$SkipDatabase) {
                $volumes = & docker volume ls --filter 'label=com.docker.compose.project=smartpark-schema' --format '{{.Name}}' 2>$null
                if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker volumes. Start Docker Desktop and retry, or use -SkipDatabase.' }
                if ($volumes) {
                    if ($RecreateDb) {
                        # Existing volumes are about to be dropped by `down -v`; the old password no longer applies.
                        Write-Warning 'Password guard skipped because -RecreateDb will wipe existing schema volumes.'
                    } else {
                        throw 'Existing database volumes found. Recover their original SMARTPARK_DEV_PASSWORD in .env; setup will not replace or delete data.'
                    }
                }
            }
        }
        if (!$value) {
            $value = if ($name -in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) { 'Aa1!' + (New-LocalSecret 6) } else { New-LocalSecret }
        }
        if ($value -match "[\r\n']") { throw "Set $name directly in .env; its existing value cannot be safely represented automatically." }
        $settings[$name] = $value
        $generated[$name] = $value
    }
}
foreach ($pair in @(
    @{ Name = 'SMARTPARK_OWNER_EMAIL'; Default = 'owner@smartpark.local' },
    @{ Name = 'SMARTPARK_OPERATOR_EMAIL'; Default = 'operator@smartpark.local' }
)) {
    if ([string]::IsNullOrWhiteSpace($settings[$pair.Name])) {
        $settings[$pair.Name] = $pair.Default
        $generated[$pair.Name] = $pair.Default
    }
}
if ($sonarEnabled) {
    # SonarQube runs by default, so fill its credentials like the local database
    # password: reuse an existing value, generate only when missing or placeholder.
    if (!$settings['SONAR_DB_PASSWORD'] -or $settings['SONAR_DB_PASSWORD'] -like 'replace-with-*') {
        $settings['SONAR_DB_PASSWORD'] = New-LocalSecret
        $generated['SONAR_DB_PASSWORD'] = $settings['SONAR_DB_PASSWORD']
    }
    if (!$settings['SONAR_PORT']) { $settings['SONAR_PORT'] = '9000'; $generated['SONAR_PORT'] = '9000' }
}
Write-Host '[3/7] Validating configuration...' -ForegroundColor Cyan
foreach ($name in $generated.Keys) {
    if ($generated[$name] -match "[\r\n']") { throw "Configure $name manually; its value cannot be safely written automatically." }
}
if ($settings['SMARTPARK_SERVICE_KEY'].Length -lt 32) { throw 'SMARTPARK_SERVICE_KEY must contain at least 32 characters.' }
foreach ($name in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
    Assert-DevelopmentPassword $name $settings[$name]
}
foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT','SMARTPARK_DB_PAYMENT_PORT','SMARTPARK_DB_NOTIFICATION_PORT','SMARTPARK_DB_IOT_PORT')) {
    $port = 0
    if (![int]::TryParse($settings[$name], [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw "Invalid port: $name." }
}
if ($settings['VITE_MAPTILER_API_KEY'] -match "[\r\n']") { throw 'Configure MapTiler key directly in FE/.env.' }
foreach ($service in @('USER','PARKING','RESERVATION')) {
    $name = "SMARTPARK_SERVICE_${service}_URL"
    if ($settings[$name]) {
        $uri = $null
        if (![Uri]::TryCreate($settings[$name], [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http','https')) { throw "Invalid service URL: $name." }
    }
}
if ($sonarEnabled) {
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
    Write-Host 'Filled missing local credentials; existing configured values were preserved.' -ForegroundColor Green
}
$settings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $envFile
if ($settings['SMARTPARK_SERVICE_KEY'].Length -lt 32) { throw 'SMARTPARK_SERVICE_KEY must contain at least 32 characters.' }
foreach ($name in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
    Assert-DevelopmentPassword $name $settings[$name]
}
foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT','SMARTPARK_DB_PAYMENT_PORT','SMARTPARK_DB_NOTIFICATION_PORT','SMARTPARK_DB_IOT_PORT')) {
    $port = 0
    if (![int]::TryParse($settings[$name], [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw "Invalid port: $name." }
}

if ($SkipDatabase) {
    Write-Host '[4/7] Skipping database initialization (-SkipDatabase).' -ForegroundColor Cyan
}
if (!$SkipDatabase) {
    Write-Host '[4/7] Initializing databases (user, facility, operations, payment, notification, iot)...' -ForegroundColor Cyan
    & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose configuration is invalid.' }
    if ($RecreateDb) {
        # Same project name, env file and compose file as `up` — only the schema project is targeted.
        Write-Host '    - Dropping schema volumes (-RecreateDb); existing dev data in those volumes will be lost...' -ForegroundColor DarkGray
        & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile down -v
        if ($LASTEXITCODE -ne 0) { throw 'Failed to remove schema volumes for -RecreateDb. Existing volumes were preserved.' }
    }
        Write-Host '    - Starting database containers and waiting for healthy (first init can take a few minutes)...' -ForegroundColor DarkGray
    & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Database startup failed. Check Docker Desktop, ports and SQL initialization logs. Existing volumes were preserved.' }
    $migrationIndex = 0
    foreach ($service in @('User','Parking','Reservation','Payment','Notification','Iot')) {
        $migrationIndex++
        Write-Host "    - [$migrationIndex/6] Probing authentication and applying migrations for $service..." -ForegroundColor DarkGray
        $container = @{ User='user-db'; Parking='facility-db'; Reservation='operations-db'; Payment='payment-db'; Notification='notification-db'; Iot='iot-db' }[$service]
        # Container env is used internally; credentials never become command arguments.
        # Send shell code through stdin: PowerShell 5.1 strips native argument quotes.
        # The trailing comment also consumes its Windows CRLF line terminator.
        $probe = 'PGPASSWORD="$POSTGRES_PASSWORD" psql -h 127.0.0.1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -q -c "SELECT 1" # local authentication probe'
        $probe | & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile exec -T $container sh -s | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "$service database authentication failed. Restore the password used to initialize this volume; no data was reset." }
        # Runner status lines print green; psql command chatter prints dim gray.
        $runnerOutput = & (Join-Path $taskRoot 'scripts/apply-workflow-migrations.ps1') -Service $service -SchemaRoot $taskRoot -EnvPath $envFile
        foreach ($runnerLine in @($runnerOutput)) {
            if (!$runnerLine) { continue }
            if ($runnerLine -match 'workflow migrations applied|schema already current') { Write-Host "    $runnerLine" -ForegroundColor Green }
            else { Write-Host "    $runnerLine" -ForegroundColor DarkGray }
        }
    }
}

Write-Host '[5/7] Configuring user-secrets for User, Parking, Reservation...' -ForegroundColor Cyan
if (!$SkipUserSecrets) { foreach ($service in @('User','Parking','Reservation')) {
    $database = @{ User='smartpark_user'; Parking='smartpark_facility'; Reservation='smartpark_operations'; Payment='smartpark_payment'; Notification='smartpark_notification'; Iot='smartpark_iot' }[$service]
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
        $desired['DevelopmentAdmin:Password'] = $settings['SMARTPARK_ADMIN_PASSWORD']
        $desired['DevelopmentOwner:Email'] = $settings['SMARTPARK_OWNER_EMAIL']
        $desired['DevelopmentOwner:Password'] = $settings['SMARTPARK_OWNER_PASSWORD']
        $desired['DevelopmentOperator:Email'] = $settings['SMARTPARK_OPERATOR_EMAIL']
        $desired['DevelopmentOperator:Password'] = $settings['SMARTPARK_OPERATOR_PASSWORD']
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
    $toSync = @{}
    foreach ($name in $desired.Keys) {
        if (!$existingByService[$service].ContainsKey($name) -or $existingByService[$service][$name] -cne $desired[$name]) {
            $toSync[$name] = $desired[$name]
        }
    }
    # Optional keys (JWT PEM, SMTP fields) that are no longer configured in .env are removed.
    $removable = @()
    foreach ($name in @('Jwt:PrivateKeyPem','Otp:Smtp:Host','Otp:Smtp:Port','Otp:Smtp:Username','Otp:Smtp:Password','Otp:Smtp:From')) {
        if (!$desired.Contains($name) -and $existingByService[$service].ContainsKey($name)) { $removable += $name }
    }
    if ($service -eq 'User' -and $existingByService[$service].ContainsKey('DevelopmentAdmin:Password') -and $toSync.ContainsKey('DevelopmentAdmin:Password')) {
        Write-Warning 'DevelopmentAdmin:Password differs between user-secrets and .env; user-secrets will be overwritten to match .env. Setup does not change the Admin password already seeded in the database — if Admin login fails afterwards, restore the old password in .env or rerun with -RecreateDb.'
    }
    if ($toSync.Count) {
        $previousEncoding = $OutputEncoding
        try {
            $OutputEncoding = New-Object Text.UTF8Encoding($false)
            $toSync | ConvertTo-Json | & dotnet user-secrets set --project $projectByService[$service] | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Cannot configure user-secrets for $service." }
        } finally { $OutputEncoding = $previousEncoding }
    }
    foreach ($name in $removable) {
        & dotnet user-secrets remove $name --project $projectByService[$service] | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Cannot remove user-secret $name for $service." }
    }
    Write-Host "$service configuration ready ($($toSync.Count) synced, $($removable.Count) removed)." -ForegroundColor Green
} }
Write-Host '[6/7] Preparing frontend environment (FE/.env, MapTiler, AI endpoints)...' -ForegroundColor Cyan
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
# AI endpoints (python-ai): main.py serves the REST API, main_live.py the live stream.
# Fill FE/.env only when its values are empty; existing frontend values are preserved.
$aiDefaults = [ordered]@{
    'VITE_AI_API_BASE_URL'      = 'http://localhost:5001'
    'VITE_AI_LIVE_API_BASE_URL' = 'http://localhost:5002'
}
if ($settings['VITE_AI_API_BASE_URL']) { $aiDefaults['VITE_AI_API_BASE_URL'] = $settings['VITE_AI_API_BASE_URL'] }
if ($settings['VITE_AI_LIVE_API_BASE_URL']) { $aiDefaults['VITE_AI_LIVE_API_BASE_URL'] = $settings['VITE_AI_LIVE_API_BASE_URL'] }
if (Test-Path -LiteralPath $frontendEnv -PathType Leaf) {
    $frontendSettings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $frontendEnv -NoExport
    $aiMissing = @{}
    foreach ($name in $aiDefaults.Keys) {
        $value = $aiDefaults[$name]
        if ($value -match "[\r\n']") { throw "Configure $name directly in FE/.env." }
        if (!$frontendSettings[$name]) { $aiMissing[$name] = $value }
        elseif ($frontendSettings[$name] -cne $value) { Write-Warning "FE/.env has a different $name; existing frontend configuration was preserved." }
    }
    if ($aiMissing.Count) {
        $frontendLines = @(Get-Content -LiteralPath $frontendEnv -Encoding UTF8)
        foreach ($name in $aiMissing.Keys) {
            $frontendLines = @($frontendLines | Where-Object { $_ -notmatch ('^\s*' + [regex]::Escape($name) + '\s*=') })
            $frontendLines += "$name='$($aiMissing[$name])'"
        }
        [IO.File]::WriteAllLines($frontendEnv, $frontendLines, (New-Object Text.UTF8Encoding($false)))
        Write-Host 'Configured AI endpoint URLs in FE/.env; existing values were preserved.' -ForegroundColor Green
    }
}
if ($sonarEnabled -and !$SkipDatabase) {
    Write-Host '    - Starting SonarQube stack...' -ForegroundColor DarkGray
    $sonarCompose = Join-Path $taskRoot 'compose.sonar.yml'
    & docker compose --project-name smartpark-sonar --env-file $envFile -f $sonarCompose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'SonarQube Compose configuration is invalid.' }
    & docker compose --project-name smartpark-sonar --env-file $envFile -f $sonarCompose up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'SonarQube startup failed. Check logs and existing DB password; no volume was reset.' }
}
if ($WithAiCamera) {
    if ($SkipDatabase) { throw 'AI camera containers need the schema DBs running; -SkipDatabase is not supported with -WithAiCamera.' }
    if (!$settings['SMARTPARK_DEV_PASSWORD'] -or $settings['SMARTPARK_DEV_PASSWORD'] -like 'replace-with-*') {
        throw 'Configure SMARTPARK_DEV_PASSWORD before using -WithAiCamera.'
    }
    & docker network inspect smartpark-schema_default *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Docker network smartpark-schema_default not found. Run setup.ps1 without -SkipDatabase first so the schema DBs are up.' }
    $aiCompose = Join-Path $taskRoot 'compose.ai.yml'
    & docker compose --project-name smartpark-ai --env-file $envFile -f $aiCompose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'AI Compose configuration is invalid.' }
    Write-Host '    - Building AI images (first run takes ~5-10 min)...' -ForegroundColor DarkGray
    & docker compose --project-name smartpark-ai --env-file $envFile -f $aiCompose build
    if ($LASTEXITCODE -ne 0) { throw 'AI image build failed. Check: docker compose -p smartpark-ai -f compose.ai.yml logs' }
    & docker compose --project-name smartpark-ai --env-file $envFile -f $aiCompose up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'AI camera containers failed to become healthy. Check: docker compose -p smartpark-ai -f compose.ai.yml logs' }
}
Write-Host '[7/7] Restoring .NET dependencies...' -ForegroundColor Cyan
if (!$SkipRestore) {
    & dotnet restore (Join-Path $taskRoot 'SmartParking.slnx') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
}
$setupDone = 'Setup complete. Run scripts/run-schema-service.ps1 in separate terminals for User, Parking and Reservation.'
if ($sonarEnabled -and !$SkipDatabase) { $setupDone += " SonarQube UI: http://localhost:$($settings['SONAR_PORT']) (first login admin/admin)." }
if ($WithAiCamera) { $setupDone += " AI REST: http://localhost:5001 · Live: http://localhost:5002." }
Write-Host $setupDone -ForegroundColor Green
