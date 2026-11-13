#requires -Version 7.0
param([string]$EnvPath, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$EnvPath) { $EnvPath = Join-Path $root '.env' }
$EnvPath = (Resolve-Path -LiteralPath $EnvPath).Path
$envDirectory = Split-Path $EnvPath -Parent
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport
. (Join-Path $PSScriptRoot 'discovery-docker.ps1')
if ($CheckOnly) {
    if ($settings['SMARTPARK_DISCOVERY_DOCKER_ENABLED'] -cne 'true') { throw 'Discovery Docker is not initialized.' }
    $null = Get-DiscoveryDockerEnvironment $settings $envDirectory
    Write-Host 'Discovery Docker input checks passed. Runtime/schema not checked.'
    return
}
if ($settings['SMARTPARK_DISCOVERY_DOCKER_DIRECTORY']) {
    if ($settings['SMARTPARK_DISCOVERY_DOCKER_ENABLED'] -cne 'true') { throw 'Existing Discovery directory configured but Docker integration is disabled. Review the existing configuration; credentials will not be replaced.' }
    $null = Get-DiscoveryDockerEnvironment $settings $envDirectory
    Write-Host 'Existing discovery credentials preserved. Input checks passed.'
    return
}
if ($settings['SMARTPARK_USER_DOCKER_TLS_ENABLED'] -cne 'true' -or !$settings['SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH']) {
    throw 'Configure existing User Docker TLS and public CA before initializing discovery.'
}
if ($settings['Discovery__PaymentServiceKey'] -or $settings['DiscoveryPricing__Operations__Current__Key']) {
    throw 'Existing discovery credentials found. Configure the overlay manually; initialization will not replace them.'
}
# Only new local-dev material. Never rotate User JWT, existing TLS or Assistant keyring.
$relativeDirectory = '.cache/discovery-docker-' + [Guid]::NewGuid().ToString('N')
$directory = Join-Path $envDirectory $relativeDirectory
$null = New-Item -ItemType Directory -Path $directory
$password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$serviceKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$now = [DateTimeOffset]::UtcNow
$caKey = [Security.Cryptography.RSA]::Create(3072)
$ca = $null
try {
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=SmartPark local discovery CA', $caKey,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true,$false,0,$true))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
        [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign, $true))
    $ca = $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddDays(100))
    [IO.File]::WriteAllText((Join-Path $directory 'discovery-ca.crt'), $ca.ExportCertificatePem())
    foreach ($service in @('facility-service','operation-service','payment-service')) {
        $rsa = [Security.Cryptography.RSA]::Create(3072)
        $leaf = $withKey = $null
        try {
            $csr = [Security.Cryptography.X509Certificates.CertificateRequest]::new(('CN=' + $service), $rsa,
                [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
            $san.AddDnsName($service)
            $csr.CertificateExtensions.Add($san.Build())
            $csr.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false,$false,0,$true))
            $usage = [Security.Cryptography.OidCollection]::new()
            $null = $usage.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))
            $csr.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usage,$true))
            $leaf = $csr.Create($ca, $now.AddMinutes(-1), $now.AddDays(90), [Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
            $withKey = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($leaf,$rsa)
            [IO.File]::WriteAllBytes((Join-Path $directory ($service + '.pfx')),
                $withKey.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,$password))
        } finally { if ($withKey) { $withKey.Dispose() }; if ($leaf) { $leaf.Dispose() }; $rsa.Dispose() }
    }
} finally { if ($ca) { $ca.Dispose() }; $caKey.Dispose() }
# Empty is intentional: an existing site is not implicitly trusted/published.
[IO.File]::WriteAllText((Join-Path $directory 'pricing-bindings.env'), "# Explicit approved site-to-tenant bindings only.`n")
$changes = @{
    SMARTPARK_DISCOVERY_DOCKER_ENABLED = 'true'
    SMARTPARK_DISCOVERY_DOCKER_DIRECTORY = $relativeDirectory
    SMARTPARK_DISCOVERY_DOCKER_CERT_PASSWORD = $password
    Discovery__Enabled = 'true'
    Discovery__FacilityOrigin = 'https://facility-service:8443'
    Discovery__PaymentOrigin = 'https://payment-service:8443'
    Discovery__UserOrigin = 'https://user-service:8443'
    Discovery__PaymentServiceKey = $serviceKey
    Discovery__MaxAgeSeconds = '60'
    Discovery__ClockSkewSeconds = '5'
    DiscoveryPricing__Operations__Current__Key = $serviceKey
    DiscoveryPricing__Operations__Current__ExpiresAt = $now.AddDays(90).ToString('O')
    Assistant__Reads__Search__Origin = 'https://operation-service:8443'
    Assistant__Reads__Search__MaxAgeSeconds = '60'
    Assistant__Reads__Search__ClockSkewSeconds = '5'
    Assistant__Reads__Search__Enabled = 'false'
}
$merged = $settings.Clone()
foreach ($key in $changes.Keys) { $merged[$key] = $changes[$key] }
$null = Get-DiscoveryDockerEnvironment $merged $envDirectory
$lines = [Collections.Generic.List[string]]::new()
$seen = @{}
foreach ($line in [IO.File]::ReadAllLines($EnvPath)) {
    if ($line -match '^\s*([^#=\s]+)\s*=') {
        $key = $Matches[1]
        if ($changes.ContainsKey($key)) {
            if (!$seen[$key]) { $lines.Add($key + '=' + $changes[$key]); $seen[$key] = $true }
            continue
        }
    }
    $lines.Add($line)
}
foreach ($key in ($changes.Keys | Sort-Object)) { if (!$seen[$key]) { $lines.Add($key + '=' + $changes[$key]) } }
$temporary = $EnvPath + '.discovery-' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllLines($temporary,$lines,[Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $EnvPath -Force
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
Write-Host 'Local discovery TLS and service credentials initialized (90 days). Search remains disabled until runtime verification.'
Write-Host 'No database, publication, User JWT or Assistant keyring changes were made.'
