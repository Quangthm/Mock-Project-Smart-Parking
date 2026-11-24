#requires -Version 5.1
# Exercise the tester entry points on a clean synthetic installation.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')
$aliases = Get-JwtSigningEnvironment
$previousAppData = $env:APPDATA
$cache = [IO.Path]::GetFullPath((Join-Path $root '.cache'))
$fixture = Join-Path $cache ('handoff-' + [guid]::NewGuid().ToString('N') + '-' + [char]0x1ec7)
try {
    Clear-JwtSigningEnvironment
    $env:APPDATA = $fixture
    $null = [IO.Directory]::CreateDirectory($fixture)
    $envFile = Join-Path $fixture '.env'
    [IO.File]::WriteAllText($envFile, '')
    & (Join-Path $PSScriptRoot 'initialize-assistant-docker.ps1') -EnvPath $envFile
    $keyFile = Join-Path $fixture '.cache/assistant-docker/user-private.pem'
    $source = 'UserService user-secrets:Jwt:PrivateKeyPem'
    $secretsFile = Join-Path $fixture 'secrets.json'
    $secretsJson = @{'Jwt:PrivateKeyPem' = [IO.File]::ReadAllText($keyFile)} | ConvertTo-Json
    [IO.File]::WriteAllText($secretsFile, $secretsJson)
    $inputs = @{EnvPath = $envFile; UserSecretsPath = $secretsFile}
    $inspect = Join-Path $PSScriptRoot 'inspect-assistant-signing.ps1'
    $before = (& $inspect @inputs) | ConvertFrom-Json
    if ($before.DistinctPrivateKeyCount -ne 1 -or @($before.Sources | Where-Object Status -eq CONFIGURED).Count -ne 2) {
        throw 'Expected identical keys in the .env file source and user-secrets.'
    }
    $copy = Join-Path $fixture 'recovered.pem'
    & (Join-Path $PSScriptRoot 'export-assistant-signing.ps1') @inputs -Source $source -ConfirmExistingSource -OutputPath $copy
    [IO.File]::WriteAllText($envFile, 'SMARTPARK_JWT_PRIVATE_KEY_FILE=recovered.pem')
    $after = (& $inspect @inputs) | ConvertFrom-Json
    if ($after.DistinctPrivateKeyCount -ne 1 -or @($after.Sources | Where-Object Status -eq CONFIGURED).Count -ne 2) {
        throw 'Recovered file did not match the original signing key.'
    }
    Write-Host "PASS: initialize, inspect and recover the existing key on PowerShell $($PSVersionTable.PSVersion)."
} finally {
    $env:APPDATA = $previousAppData
    Restore-JwtSigningEnvironment $aliases
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixture)) -ne $cache) { throw 'Unexpected fixture cleanup path.' }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
