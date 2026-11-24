#requires -Version 5.1
param([string]$EnvPath, [switch]$UseExistingSigningKey)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$EnvPath) { $EnvPath = Join-Path $root '.env' }
if (!(Test-Path -LiteralPath $EnvPath -PathType Leaf)) {
    throw 'Copy .env.example to .env before initializing the local Assistant.'
}
$EnvPath = (Resolve-Path -LiteralPath $EnvPath).Path
$envDirectory = Split-Path $EnvPath -Parent
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')

$relativeDirectory = '.cache/assistant-docker'
$directory = Join-Path $envDirectory $relativeDirectory
$signingNames = @('SMARTPARK_JWT_PRIVATE_KEY_FILE', 'SMARTPARK_JWT_PRIVATE_KEY_PEM', 'SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64')
$protectedNames = @(
    'SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH', 'SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD',
    'SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH', 'Assistant__Jwt__PublicKeyFile', 'Assistant__Jwt__PublicKeyPem',
    'Assistant__Management__KeyDirectory', 'Assistant__Management__KeyEncryptionCertificatePath',
    'Assistant__Management__KeyEncryptionCertificatePassword', 'Assistant__Guest__KnowledgeDirectory'
)
if (!$UseExistingSigningKey) { $protectedNames = $signingNames + $protectedNames }
foreach ($name in $protectedNames) {
    if ((Test-JwtConfiguredValue $settings[$name]) -or
        (Test-JwtConfiguredValue ([Environment]::GetEnvironmentVariable($name, 'Process')))) {
        if ($settings['SMARTPARK_JWT_PRIVATE_KEY_FILE'] -ceq "$relativeDirectory/user-private.pem" -and
            (Test-Path -LiteralPath $directory -PathType Container)) {
            Write-Host 'Existing local Assistant files and settings were preserved. Run setup.ps1 -AssistantCheck to inspect the configuration.'
            return
        }
        throw "Existing local configuration found for $name. Keep its original JWT, certificates, and keyring and configure the Assistant inputs manually."
    }
}
if (Test-Path -LiteralPath $directory) {
    throw 'Local Assistant material directory already exists. Review it before retrying.'
}
if ((Get-JwtSigningEnvironment).Count) {
    throw 'Existing process JWT signing configuration found. Preserve it and configure Assistant manually.'
}
$userProject = Join-Path $root 'src/Services/UserService/SmartParking.UserService.API/SmartParking.UserService.API.csproj'
$userSecretsId = ([xml][IO.File]::ReadAllText($userProject)).Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1
$userSecretsDirectory = if ($env:APPDATA) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
$userSecretsPath = Join-Path $userSecretsDirectory "$userSecretsId/secrets.json"
if (!$UseExistingSigningKey -and (Test-Path -LiteralPath $userSecretsPath -PathType Leaf) -and
    [IO.File]::ReadAllText($userSecretsPath) -match '"(?:Jwt:)?PrivateKeyPem(?:Base64)?"\s*:') {
    throw 'Existing UserService signing material found in .NET user-secrets. Preserve it and configure Assistant manually.'
}

# Windows PowerShell 5.1 uses .NET Framework, which lacks the modern PEM and
# certificate APIs. Run only cryptography in the already-required .NET 10 SDK.
$cryptoInput = @{ Directory = $directory; UseExistingSigningKey = [bool]$UseExistingSigningKey }
if ($UseExistingSigningKey) {
    $file = $settings[$signingNames[0]]
    $inline = $settings[$signingNames[1]]
    $base64 = $settings[$signingNames[2]]
    if (!($signingNames | Where-Object { Test-JwtConfiguredValue $settings[$_] })) {
        throw 'Set the existing UserService signing key in .env before using -UseExistingSigningKey. If it lives in user-secrets, preserve the same key in a private PEM file and configure SMARTPARK_JWT_PRIVATE_KEY_FILE.'
    }
    if (Test-JwtConfiguredValue $file) { $cryptoInput['Pem'] = Read-JwtPrivateKeyFile $file $envDirectory }
    elseif (Test-JwtConfiguredValue $inline) { $cryptoInput['Pem'] = $inline.Replace('\n', "`n") }
    if (Test-JwtConfiguredValue $base64) { $cryptoInput['Base64'] = $base64 }
    elseif ($cryptoInput['Pem'] -and $base64 -like 'replace-with-*') {
        throw 'Clear the Base64 template placeholder before using an existing PEM signing key.'
    }
}
. (Join-Path $PSScriptRoot 'assistant-signing-native.ps1')
$material = Invoke-AssistantCrypto -Request $cryptoInput -HelperName 'assistant-docker-crypto.cs'
$protectionPassword = $material.ProtectionPassword
$serverPassword = $material.ServerPassword

$changes = @{
    SMARTPARK_USER_DOCKER_TLS_ENABLED = 'true'
    SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH = "$relativeDirectory/user-service.pfx"
    SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD = $serverPassword
    SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH = "$relativeDirectory/user-ca.crt"
    SMARTPARK_ASSISTANT_DOCKER_ENABLED = 'true'
    Assistant__Jwt__PublicKeyFile = "$relativeDirectory/user-public.pem"
    Assistant__Jwt__Issuer = if ($settings['SMARTPARK_JWT_ISSUER']) { $settings['SMARTPARK_JWT_ISSUER'] } else { 'SmartParking.UserService' }
    Assistant__Jwt__Audience = if ($settings['SMARTPARK_JWT_AUDIENCE']) { $settings['SMARTPARK_JWT_AUDIENCE'] } else { 'SmartParking' }
    Assistant__Management__KeyDirectory = "$relativeDirectory/keyring"
    Assistant__Management__KeyEncryptionCertificatePath = "$relativeDirectory/assistant.pfx"
    Assistant__Management__KeyEncryptionCertificatePassword = $protectionPassword
    Assistant__Guest__KnowledgeDirectory = "$relativeDirectory/knowledge"
    Assistant__Guest__ReleaseEnabled = 'false'
    Assistant__Chat__ReleaseEnabled = 'true'
    Assistant__Reads__User__Enabled = 'true'
    Assistant__Reads__User__Origin = 'https://user-service:8443'
}
if (!$UseExistingSigningKey) {
    $changes['SMARTPARK_JWT_PRIVATE_KEY_FILE'] = "$relativeDirectory/user-private.pem"
    $changes['SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64'] = ''
}
$seen = @{}
$updated = @(foreach ($line in [IO.File]::ReadAllLines($EnvPath)) {
    if ($line -match '^\s*([^#=\s]+)\s*=' -and $changes.ContainsKey($Matches[1])) {
        $name = $Matches[1]
        if (!$seen[$name]) { "$name=$($changes[$name])"; $seen[$name] = $true }
    } else { $line }
})
foreach ($name in ($changes.Keys | Sort-Object)) {
    if (!$seen[$name]) { $updated += "$name=$($changes[$name])" }
}
$temporary = $EnvPath + '.assistant-' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllLines($temporary, $updated, [Text.UTF8Encoding]::new($false))
    [IO.File]::Replace($temporary, $EnvPath, [System.Management.Automation.Language.NullString]::Value)
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Host 'Local Assistant JWT, keyring protection, and User HTTPS material configured in .env.'
Write-Host 'For chat, deploy the approved Markdown and publication.json into the local knowledge directory, then configure the provider and enable Assistant in Admin Settings.'
