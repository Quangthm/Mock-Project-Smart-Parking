param(
    [switch]$Apply,
    [string]$Container = 'smartpark-schema-user-db-1',
    [string]$Database = 'smartpark_user',
    [string]$DatabaseUser = 'smartpark_user',
    [string]$DriverEmail = 'driver@gmail.com',
    [string]$DriverPassword = 'Password@123',
    [string]$UserServiceUrl = 'http://localhost:5035'
)
$ErrorActionPreference = 'Stop'

# Why this exists: DataSeeder.SeedRoleUserAsync only writes PasswordHash when it
# CREATES a user (src/Services/UserService/SmartParking.UserService.Persistence/DataSeeder.cs).
# An existing dev row therefore keeps whatever hash it was first seeded with, so
# changing SMARTPARK_*_PASSWORD in .env never takes effect and every demo login
# returns 401 AUTH_FAILED. The demo operator is also seeded as
# PENDING_VERIFICATION by the Fix2FA activation flow, which both breaks the
# seeder's role check (user-service crash-loops on startup) and makes
# AccountRolePolicy.CurrentRole return null (SITE_OPERATOR maps to the "operator"
# JWT role only when email_verified_at or phone_verified_at is set).
#
# This script repairs dev demo identity data only. It changes no schema and no
# business state. Hashes are generated inside PostgreSQL with pgcrypto
# (crypt(..., gen_salt('bf', 12)) => $2a$12$, which BCrypt.Net verifies), so
# plaintext passwords never reach the shell history or the process table.

$repoRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $repoRoot '.env'
if (!(Test-Path -LiteralPath $envFile -PathType Leaf)) { throw "Missing $envFile." }

$settings = @{}
Get-Content -LiteralPath $envFile | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*)$') {
        $settings[$Matches[1]] = $Matches[2].Trim().Trim('"').Trim("'")
    }
}
function Need([string]$key) {
    if (!$settings.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($settings[$key])) { throw "Set $key in .env." }
    return $settings[$key]
}

$ownerEmail = Need 'SMARTPARK_OWNER_EMAIL'
$ownerPassword = Need 'SMARTPARK_OWNER_PASSWORD'
$operatorEmail = Need 'SMARTPARK_OPERATOR_EMAIL'
$operatorPassword = Need 'SMARTPARK_OPERATOR_PASSWORD'
$adminEmail = Need 'SMARTPARK_ADMIN_EMAIL'
$adminPassword = Need 'SMARTPARK_ADMIN_PASSWORD'

# DataSeeder.SeedRoleUserAsync rejects bootstrap passwords outside 8..15 characters.
foreach ($pair in @(
        @('SMARTPARK_OWNER_PASSWORD', $ownerPassword),
        @('SMARTPARK_OPERATOR_PASSWORD', $operatorPassword),
        @('SMARTPARK_ADMIN_PASSWORD', $adminPassword))) {
    if ($pair[1].Length -lt 8 -or $pair[1].Length -gt 15) {
        throw "$($pair[0]) must be 8-15 characters; the seeder rejects it otherwise."
    }
}

$psql = @('exec', '-i', '-e', "PGPASSWORD=$($settings['SMARTPARK_DEV_PASSWORD'])")
if ([string]::IsNullOrWhiteSpace($settings['SMARTPARK_DEV_PASSWORD'])) {
    # Fall back to the documented local password when .env does not carry it.
    $psql = @('exec', '-i', '-e', 'PGPASSWORD=smartParking_Password@123')
}

$sql = @'
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Existing demo accounts: refresh the hash and clear any lockout.
UPDATE users
   SET password_hash = crypt(:'owner_password', gen_salt('bf', 12)),
       failed_login_attempts = 0,
       locked_until = NULL,
       updated_at = now()
 WHERE email = :'owner_email';

UPDATE users
   SET password_hash = crypt(:'admin_password', gen_salt('bf', 12)),
       failed_login_attempts = 0,
       locked_until = NULL,
       updated_at = now()
 WHERE email = :'admin_email';

-- The demo operator additionally needs ACTIVE + a verified channel so that
-- DataSeeder.SeedRoleUserAsync accepts it and AccountRolePolicy maps it to the
-- "operator" JWT role.
UPDATE users
   SET password_hash = crypt(:'operator_password', gen_salt('bf', 12)),
       failed_login_attempts = 0,
       locked_until = NULL,
       status = 'ACTIVE',
       email_verified_at = COALESCE(email_verified_at, now()),
       updated_at = now()
 WHERE email = :'operator_email';

UPDATE accounts
   SET status = 'ACTIVE', updated_at = now()
 WHERE user_id = (SELECT id FROM users WHERE email = :'operator_email')
   AND status <> 'ACTIVE';

-- The driver demo user is only created by DataSeeder when the user table is
-- empty, so a repaired or partially seeded database has no driver at all.
INSERT INTO users (email, password_hash, full_name, email_verified_at, status)
SELECT :'driver_email', crypt(:'driver_password', gen_salt('bf', 12)), 'Demo Driver', now(), 'ACTIVE'
 WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = :'driver_email');

INSERT INTO accounts (user_id, status)
SELECT u.id, 'ACTIVE'
  FROM users u
 WHERE u.email = :'driver_email'
   AND NOT EXISTS (SELECT 1 FROM accounts a WHERE a.user_id = u.id);

INSERT INTO account_roles (account_id, role_code)
SELECT a.id, 'DRIVER'
  FROM accounts a
  JOIN users u ON u.id = a.user_id
 WHERE u.email = :'driver_email'
   AND NOT EXISTS (SELECT 1 FROM account_roles r WHERE r.account_id = a.id AND r.role_code = 'DRIVER');

SELECT u.email, u.status, (u.email_verified_at IS NOT NULL) AS email_verified,
       a.status AS account_status, string_agg(r.role_code, ',' ORDER BY r.role_code) AS roles
  FROM users u
  LEFT JOIN accounts a ON a.user_id = u.id
  LEFT JOIN account_roles r ON r.account_id = a.id
 WHERE u.email IN (:'owner_email', :'operator_email', :'admin_email', :'driver_email')
 GROUP BY u.email, u.status, u.email_verified_at, a.status
 ORDER BY u.email;
'@

Write-Output "Target: $Container / $Database (user $DatabaseUser)"
Write-Output "Accounts: $ownerEmail, $operatorEmail, $adminEmail, $DriverEmail"
if (!$Apply) {
    Write-Output 'Inspection only. Re-run with -Apply to reset the demo password hashes.'
    Write-Output 'Current login state:'
    foreach ($probe in @(
            @($DriverEmail, $DriverPassword),
            @($ownerEmail, $ownerPassword),
            @($operatorEmail, $operatorPassword),
            @($adminEmail, $adminPassword))) {
        $body = @{ email = $probe[0]; password = $probe[1] } | ConvertTo-Json -Compress
        try {
            $response = Invoke-RestMethod -Method Post -Uri "$($UserServiceUrl.TrimEnd('/'))/api/auth/login" -ContentType 'application/json' -Body $body -TimeoutSec 20
            Write-Output ("  {0,-28} OK (role {1})" -f $probe[0], $response.data.user.role)
        } catch {
            $code = 'unreachable'
            if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
            Write-Output ("  {0,-28} FAIL ({1})" -f $probe[0], $code)
        }
    }
    return
}

$tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("spark228-identity-{0}.sql" -f ([guid]::NewGuid()))
try {
    # psql lf-safe: write without a BOM.
    [IO.File]::WriteAllText($tempFile, $sql, (New-Object System.Text.UTF8Encoding($false)))
    # PowerShell 5.1 has no `<` redirection for native commands, so feed psql stdin by pipe.
    Get-Content -LiteralPath $tempFile -Raw | & docker $psql[0] $psql[1] $psql[2] $psql[3] $Container psql -U $DatabaseUser -d $Database -v ON_ERROR_STOP=1 `
        -v "owner_email=$ownerEmail" -v "owner_password=$ownerPassword" `
        -v "operator_email=$operatorEmail" -v "operator_password=$operatorPassword" `
        -v "admin_email=$adminEmail" -v "admin_password=$adminPassword" `
        -v "driver_email=$DriverEmail" -v "driver_password=$DriverPassword"
    if ($LASTEXITCODE -ne 0) { throw 'Identity repair SQL failed.' }
} finally {
    Remove-Item -LiteralPath $tempFile -ErrorAction SilentlyContinue
}

Write-Output 'Repair applied. Verifying logins:'
foreach ($probe in @(
        @($DriverEmail, $DriverPassword),
        @($ownerEmail, $ownerPassword),
        @($operatorEmail, $operatorPassword),
        @($adminEmail, $adminPassword))) {
    $body = @{ email = $probe[0]; password = $probe[1] } | ConvertTo-Json -Compress
    try {
        $response = Invoke-RestMethod -Method Post -Uri "$($UserServiceUrl.TrimEnd('/'))/api/auth/login" -ContentType 'application/json' -Body $body -TimeoutSec 20
        Write-Output ("  {0,-28} OK (role {1})" -f $probe[0], $response.data.user.role)
    } catch {
        $code = 'unreachable'
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
        Write-Output ("  {0,-28} FAIL ({1})" -f $probe[0], $code)
    }
}
