#requires -Version 5.1
<# Preserve an explicitly selected existing UserService key in a private local file.
   First run inspect-assistant-signing.ps1. Source must exactly match its output.
   Without a live JWKS check, ConfirmExistingSource asserts that the operator has
   independently confirmed this source belongs to the existing UserService.
   Never rotates keys, changes .env, prints key material, or overwrites a file.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Source,
    [string]$EnvPath = '.env',
    [string]$UserSecretsPath,
    [string]$OutputPath = '.cache/assistant-signing/user-private.pem',
    [uri]$UserJwksUri,
    [switch]$ConfirmExistingSource
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')
. (Join-Path $PSScriptRoot 'assistant-signing-native.ps1')
if (!$UserJwksUri -and !$ConfirmExistingSource) {
    throw 'Supply UserJwksUri to verify the live key, or ConfirmExistingSource after independently confirming the existing UserService source.'
}
if (![IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $root $OutputPath }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$allowed = [IO.Path]::GetFullPath((Join-Path $root '.cache')) + [IO.Path]::DirectorySeparatorChar
if (!$OutputPath.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPath must be inside the repository .cache directory.' }
if (Test-Path -LiteralPath $OutputPath) { throw 'Output already exists; nothing was overwritten.' }
$arguments = @{EnvPath=$EnvPath}
if ($UserSecretsPath) { $arguments.UserSecretsPath=$UserSecretsPath }
if ($UserJwksUri) { $arguments.UserJwksUri=$UserJwksUri }
$report = (& (Join-Path $PSScriptRoot 'inspect-assistant-signing.ps1') @arguments) | ConvertFrom-Json
$selected = @($report.Sources | Where-Object Source -ceq $Source)
if ($selected.Count -ne 1 -or $selected[0].Status -ne 'CONFIGURED') { throw 'Select exactly one CONFIGURED source from the inspector output.' }
if ($UserJwksUri -and ($report.LiveJwksStatus -ne 'READ' -or $selected[0].MatchesLiveJwks -ne $true)) { throw 'Selected key does not match the live UserService JWKS; nothing exported.' }
$value = $null; $base64 = $false
if ($Source.StartsWith('process:')) {
    $name = $Source.Substring('process:'.Length)
    $normalized = $name.Replace('_','').Replace(':','')
    if ($normalized -notmatch 'PRIVATEKEYPEM(?:BASE64)?$') { throw 'Export supports process PEM/Base64 aliases only.' }
    $value = [Environment]::GetEnvironmentVariable($name, 'Process')
    $base64 = $normalized.EndsWith('BASE64', [StringComparison]::OrdinalIgnoreCase)
} elseif ($Source.StartsWith('UserService user-secrets:')) {
    if (!$UserSecretsPath) {
        $project = Join-Path $root 'src/Services/UserService/SmartParking.UserService.API/SmartParking.UserService.API.csproj'
        $id = ([xml][IO.File]::ReadAllText($project)).Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1
        $directory = if ($env:APPDATA) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
        $UserSecretsPath = Join-Path $directory "$id/secrets.json"
    } elseif (![IO.Path]::IsPathRooted($UserSecretsPath)) { $UserSecretsPath=Join-Path $root $UserSecretsPath }
    try {
        $secrets = ConvertFrom-AssistantSigningJson ([IO.File]::ReadAllText($UserSecretsPath))
        $name = $Source.Substring('UserService user-secrets:'.Length)
        if ($name.StartsWith('nested Jwt.')) { $value=$secrets['Jwt'][$name.Substring('nested Jwt.'.Length)] }
        else { $value=$secrets[$name] }
        $base64 = $name.EndsWith('Base64', [StringComparison]::OrdinalIgnoreCase)
    } catch { throw 'Selected user-secrets source could not be read; nothing exported.' }
} else { throw 'Export supports process and UserService user-secrets sources only. Existing .env sources already have a provisioning path.' }
if ($base64) {
    try { $value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value)) }
    catch { throw 'Selected signing source changed or is invalid; nothing exported.' }
}
$directory = Split-Path $OutputPath -Parent
$null = New-Item -ItemType Directory -Path $directory -Force
# Reject junctions/symlinks in the destination ancestry before writing secrets.
for ($current=$directory; $current -and $current -ne $root; $current=Split-Path $current -Parent) {
    if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Destination contains a link; nothing exported.' }
}
$result = Invoke-AssistantCrypto @{Operation='export';Pem=$value;ExpectedFingerprint=$selected[0].PublicSpkiSha256;OutputPath=$OutputPath}
Write-Host 'Existing signing key preserved in the requested .cache file. No .env or source values changed.'
Write-Host ('Public SPKI SHA256: ' + $result.Fingerprint)
