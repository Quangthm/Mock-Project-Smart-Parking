#requires -Version 7.0
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

$jwt = [Security.Cryptography.RSA]::Create(3072)
if ($UseExistingSigningKey) {
    $file = $settings[$signingNames[0]]
    $inline = $settings[$signingNames[1]]
    $base64 = $settings[$signingNames[2]]
    if (!($signingNames | Where-Object { Test-JwtConfiguredValue $settings[$_] })) {
        $jwt.Dispose()
        throw 'Set the existing UserService signing key in .env before using -UseExistingSigningKey. If it lives in user-secrets, preserve the same key in a private PEM file and configure SMARTPARK_JWT_PRIVATE_KEY_FILE.'
    }
    try {
        $pem = if (Test-JwtConfiguredValue $file) { Read-JwtPrivateKeyFile $file $envDirectory }
            elseif (Test-JwtConfiguredValue $inline) { $inline.Replace('\n', "`n") }
        $publicKey = $null
        if ($pem) {
            $jwt.ImportFromPem($pem)
            $null = $jwt.ExportParameters($true)
            $publicKey = $jwt.ExportSubjectPublicKeyInfoPem()
        }
        if (Test-JwtConfiguredValue $base64) {
            $jwt.ImportFromPem([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($base64)))
            $null = $jwt.ExportParameters($true)
            if ($publicKey -and $publicKey -cne $jwt.ExportSubjectPublicKeyInfoPem()) { throw 'Conflicting signing keys.' }
        } elseif ($pem -and $base64 -like 'replace-with-*') {
            throw 'Clear the Base64 template placeholder before using an existing PEM signing key.'
        }
    } catch {
        $jwt.Dispose()
        throw 'The existing signing inputs must supply a valid RSA private key. Ensure multiple active inputs represent the same key and clear any Base64 template placeholder when using the PEM file.'
    }
}

$null = New-Item -ItemType Directory -Path $directory
$null = New-Item -ItemType Directory -Path (Join-Path $directory 'keyring'), (Join-Path $directory 'knowledge')
$protection = [Security.Cryptography.RSA]::Create(3072)
$caKey = [Security.Cryptography.RSA]::Create(3072)
$serverKey = [Security.Cryptography.RSA]::Create(3072)
$protectionCert = $ca = $leaf = $serverCert = $null
$pfx = [Security.Cryptography.X509Certificates.X509ContentType]::Pfx
$hash = [Security.Cryptography.HashAlgorithmName]::SHA256
$padding = [Security.Cryptography.RSASignaturePadding]::Pkcs1
$now = [DateTimeOffset]::UtcNow
$protectionPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$serverPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
try {
    if (!$UseExistingSigningKey) {
        [IO.File]::WriteAllText((Join-Path $directory 'user-private.pem'), $jwt.ExportPkcs8PrivateKeyPem())
    }
    [IO.File]::WriteAllText((Join-Path $directory 'user-public.pem'), $jwt.ExportSubjectPublicKeyInfoPem())
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=SmartPark local Assistant keyring', $protection, $hash, $padding)
    $protectionCert = $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddYears(3))
    [IO.File]::WriteAllBytes((Join-Path $directory 'assistant.pfx'), $protectionCert.Export($pfx, $protectionPassword))

    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=SmartPark local User CA', $caKey, $hash, $padding)
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
        [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign, $true))
    $ca = $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddDays(100))
    [IO.File]::WriteAllText((Join-Path $directory 'user-ca.crt'), $ca.ExportCertificatePem())
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=user-service', $serverKey, $hash, $padding)
    $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    $san.AddDnsName('user-service')
    $request.CertificateExtensions.Add($san.Build())
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
    $usage = [Security.Cryptography.OidCollection]::new()
    $null = $usage.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usage, $true))
    $leaf = $request.Create($ca, $now.AddMinutes(-1), $now.AddDays(90), [Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    $serverCert = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($leaf, $serverKey)
    [IO.File]::WriteAllBytes((Join-Path $directory 'user-service.pfx'), $serverCert.Export($pfx, $serverPassword))
} finally {
    foreach ($material in @($serverCert, $leaf, $ca, $protectionCert, $serverKey, $caKey, $protection, $jwt)) {
        if ($material) { $material.Dispose() }
    }
}

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
    [IO.File]::Move($temporary, $EnvPath, $true)
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Host 'Local Assistant JWT, keyring protection, and User HTTPS material configured in .env.'
Write-Host 'For chat, deploy the approved Markdown and publication.json into the local knowledge directory, then configure the provider and enable Assistant in Admin Settings.'
