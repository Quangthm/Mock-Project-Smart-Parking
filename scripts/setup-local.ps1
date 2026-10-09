param(
    [string]$ConfigPath,
    [switch]$SkipDatabase,
    [switch]$ResetAdminPassword
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
$localSettings = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
# Setup uses the edited file, even if this terminal still has old settings.
foreach ($property in $localSettings.PSObject.Properties) {
    if ($property.Name -notmatch '^[A-Za-z][A-Za-z0-9_:]*$' -or $property.Value -isnot [string]) {
        throw 'Local config must contain flat string settings.'
    }
    [Environment]::SetEnvironmentVariable($property.Name.Replace(':', '__'), $property.Value, 'Process')
}
if (!$env:SMARTPARK_DEV_PASSWORD) { throw 'Configure SMARTPARK_DEV_PASSWORD in the local config file.' }
if (!$env:SMARTPARK_SERVICE_KEY -or $env:SMARTPARK_SERVICE_KEY.Length -lt 32) {
    throw 'Configure SMARTPARK_SERVICE_KEY (at least 32 characters) in the local config file.'
}
$adminPassword = [string]$localSettings.SMARTPARK_ADMIN_PASSWORD
if (($ResetAdminPassword -or $adminPassword) -and
    ($adminPassword.Length -lt 8 -or $adminPassword.Length -gt 15 -or
     $adminPassword -cnotmatch '^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$')) {
    throw 'SMARTPARK_ADMIN_PASSWORD must have 8-15 characters, uppercase, lowercase, a digit and a special character.'
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

if ($ResetAdminPassword) {
    $dockerEndpoint = & docker context inspect --format '{{.Endpoints.docker.Host}}'
    if ($LASTEXITCODE -ne 0 -or ($dockerEndpoint -join '').Trim() -notmatch '^(npipe:|unix:)') {
        throw 'Admin reset requires a local Docker Desktop context.'
    }
    # PostgreSQL creates the bcrypt hash, so no PowerShell DLL or helper project is needed.
    # SQL is sent through stdin; the password is never a process argument or console output.
    $adminSql = @'
BEGIN;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE TEMP TABLE local_admin_reset_target ON COMMIT DROP AS
SELECT u.id FROM users u
WHERE lower(u.email) = 'admin@smartpark.local' AND u.deleted_at IS NULL
  AND u.status IN ('ACTIVE', 'LOCKED')
  AND EXISTS (
    SELECT 1 FROM accounts a JOIN account_roles r ON r.account_id = a.id
    WHERE a.user_id = u.id AND a.status = 'ACTIVE' AND a.deleted_at IS NULL
      AND r.role_code = 'ADMIN'
  );
DO $$
BEGIN
  IF (SELECT count(*) FROM local_admin_reset_target) <> 1 THEN
    RAISE EXCEPTION 'Expected one local Admin. Start UserService once to create it before resetting.';
  END IF;
END $$;
SELECT id FROM users WHERE id IN (SELECT id FROM local_admin_reset_target) FOR UPDATE;
UPDATE users SET password_hash = crypt('__ADMIN_PASSWORD__', gen_salt('bf', 12)),
  status = 'ACTIVE', failed_login_attempts = 0, locked_until = NULL
WHERE id IN (SELECT id FROM local_admin_reset_target);
UPDATE user_refresh_tokens SET is_revoked = true
WHERE user_id IN (SELECT id FROM local_admin_reset_target);
UPDATE auth_challenges SET consumed_at = NOW(), code_hash = ''
WHERE user_id IN (SELECT id FROM local_admin_reset_target)
  AND purpose = 'LOGIN' AND consumed_at IS NULL;
COMMIT;
'@
    $adminSql = $adminSql.Replace('__ADMIN_PASSWORD__', $adminPassword.Replace("'", "''"))
    $previousOutputEncoding = $OutputEncoding
    try {
        $OutputEncoding = New-Object System.Text.UTF8Encoding($false)
        $adminResult = $adminSql | & docker compose -f $composeFile exec -T user-db psql -U smartpark_user -d smartpark_user -v ON_ERROR_STOP=1 -q
        if ($LASTEXITCODE -ne 0) { throw 'Admin reset failed; its transaction was rolled back.' }
    } finally {
        $OutputEncoding = $previousOutputEncoding
    }
    Write-Output 'Local Admin unlocked; password updated from SMARTPARK_ADMIN_PASSWORD. Old sessions were revoked.'
}

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
    $previousOutputEncoding = $OutputEncoding
    try {
        $OutputEncoding = New-Object System.Text.UTF8Encoding($false)
        $settings | ConvertTo-Json | & dotnet user-secrets set --project $projectDir
        if ($LASTEXITCODE -ne 0) { throw "Could not configure User Secrets for $service." }
    } finally {
        $OutputEncoding = $previousOutputEncoding
    }
    Write-Output "$service local configuration ready."
}
& dotnet restore (Join-Path $taskRoot 'SmartParking.slnx') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
Write-Output 'Setup complete. Run the APIs in Visual Studio (Development) or scripts/run-schema-service.ps1.'
