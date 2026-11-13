# Read-only validation shared by app and Assistant Docker runners. No env export.
function Get-DiscoveryDockerEnvironment {
    param([hashtable]$Settings, [string]$EnvDirectory)
    $result = @{}
    $flag = [string]$Settings['SMARTPARK_DISCOVERY_DOCKER_ENABLED']
    if ($flag -cnotin @('', 'false', 'true')) { throw 'Invalid SMARTPARK_DISCOVERY_DOCKER_ENABLED.' }
    if ($flag -cne 'true') { return $result }
    $directory = [string]$Settings['SMARTPARK_DISCOVERY_DOCKER_DIRECTORY']
    if (!$directory) { throw 'Initialize discovery Docker first.' }
    if (![IO.Path]::IsPathRooted($directory)) { $directory = Join-Path $EnvDirectory $directory }
    if (!(Test-Path -LiteralPath $directory -PathType Container)) { throw 'Discovery Docker directory missing.' }
    $directory = (Resolve-Path -LiteralPath $directory).Path
    $password = [string]$Settings['SMARTPARK_DISCOVERY_DOCKER_CERT_PASSWORD']
    if (!$password) { throw 'Discovery Docker certificate password missing.' }
    $ca = $null
    try {
        $ca = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $directory 'discovery-ca.crt'))
        if ($ca.HasPrivateKey -or $ca.NotAfter -le (Get-Date) -or $ca.NotBefore -gt (Get-Date)) { throw 'Invalid CA.' }
        foreach ($service in @('facility-service','operation-service','payment-service')) {
            $cert = $null
            try {
                $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
                    (Join-Path $directory ($service + '.pfx')), $password,
                    [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
                $san = @($cert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' } | ForEach-Object { $_.Format($true) }) -join ';'
                if (!$cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date) -or $cert.NotBefore -gt (Get-Date) -or
                    $san -notmatch ('(?im)(?:^|[,;\r\n])\s*(?:DNS Name=|DNS:)\s*' + [regex]::Escape($service) + '\s*(?=$|[,;\r\n])')) { throw 'Invalid TLS certificate.' }
                $chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
                try {
                    $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
                    $chain.ChainPolicy.VerificationFlags = [Security.Cryptography.X509Certificates.X509VerificationFlags]::AllowUnknownCertificateAuthority
                    $null = $chain.ChainPolicy.ExtraStore.Add($ca)
                    $null = $chain.ChainPolicy.ApplicationPolicy.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))
                    if (!$chain.Build($cert) -or $chain.ChainElements[$chain.ChainElements.Count - 1].Certificate.Thumbprint -ne $ca.Thumbprint) { throw 'Untrusted TLS certificate.' }
                } finally { $chain.Dispose() }
            } finally { if ($cert) { $cert.Dispose() } }
        }
    } catch { throw 'Discovery Docker requires valid public CA and unexpired service PFX files with matching password and DNS SAN.' }
    finally { if ($ca) { $ca.Dispose() } }
    if (!(Test-Path -LiteralPath (Join-Path $directory 'pricing-bindings.env') -PathType Leaf)) { throw 'Discovery pricing bindings file missing.' }
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $directory 'pricing-bindings.env'))) {
        if (!$line.Trim() -or $line.TrimStart().StartsWith('#')) { continue }
        if ($line -notmatch '^DiscoveryPricing__SiteTenants__([a-fA-F0-9-]{36})=([a-fA-F0-9-]{36})$') { throw 'Only explicit site-tenant UUID bindings are allowed in pricing-bindings.env.' }
        $site = $tenant = [Guid]::Empty
        if (![Guid]::TryParse($Matches[1],[ref]$site) -or ![Guid]::TryParse($Matches[2],[ref]$tenant) -or $site -eq [Guid]::Empty -or $tenant -eq [Guid]::Empty) { throw 'Invalid discovery site-tenant UUID binding.' }
    }
    $expected = @{
        Discovery__FacilityOrigin = 'https://facility-service:8443'
        Discovery__PaymentOrigin = 'https://payment-service:8443'
        Discovery__UserOrigin = 'https://user-service:8443'
        Assistant__Reads__Search__Origin = 'https://operation-service:8443'
    }
    foreach ($key in $expected.Keys) {
        if ([string]$Settings[$key] -cne $expected[$key]) { throw "Invalid local Docker origin: $key." }
        $result[$key] = $expected[$key]
    }
    foreach ($prefix in @('Discovery','Assistant__Reads__Search')) {
        $age = $skew = 0
        if (![int]::TryParse([string]$Settings[($prefix + '__MaxAgeSeconds')], [ref]$age) -or $age -lt 1 -or $age -gt 3600 -or
            ![int]::TryParse([string]$Settings[($prefix + '__ClockSkewSeconds')], [ref]$skew) -or $skew -lt 0 -or $skew -gt 60) { throw "Invalid freshness settings: $prefix." }
        $result[($prefix + '__MaxAgeSeconds')] = [string]$age
        $result[($prefix + '__ClockSkewSeconds')] = [string]$skew
    }
    $keyValue = [string]$Settings['Discovery__PaymentServiceKey']
    $expiry = [DateTimeOffset]::MinValue
    if ($keyValue.Length -lt 32 -or $keyValue.Length -gt 256 -or $keyValue -match '[^!-~]' -or
        $keyValue -cne [string]$Settings['DiscoveryPricing__Operations__Current__Key'] -or
        ![DateTimeOffset]::TryParse([string]$Settings['DiscoveryPricing__Operations__Current__ExpiresAt'], [ref]$expiry) -or
        $expiry -le [DateTimeOffset]::UtcNow) { throw 'Discovery service credential missing, mismatched or expired.' }
    if ([string]$Settings['Discovery__Enabled'] -cnotin @('true','false')) { throw 'Invalid Discovery__Enabled.' }
    $result['Discovery__Enabled'] = [string]$Settings['Discovery__Enabled']
    $result['Discovery__PaymentServiceKey'] = $keyValue
    $result['DiscoveryPricing__Operations__Current__Key'] = $keyValue
    $result['DiscoveryPricing__Operations__Current__ExpiresAt'] = [string]$Settings['DiscoveryPricing__Operations__Current__ExpiresAt']
    $userCa = [string]$Settings['SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH']
    if (!$userCa) { throw 'User public CA required for Facility publication.' }
    if (![IO.Path]::IsPathRooted($userCa)) { $userCa = Join-Path $EnvDirectory $userCa }
    if (!(Test-Path -LiteralPath $userCa -PathType Leaf) -or [string]$Settings['SMARTPARK_USER_DOCKER_TLS_ENABLED'] -cne 'true') { throw 'User TLS and public CA must be configured first.' }
    $userCaText = [IO.File]::ReadAllText($userCa)
    if ($userCaText -notmatch '(?s)\A\s*-----BEGIN CERTIFICATE-----[A-Za-z0-9+/=\r\n]+-----END CERTIFICATE-----\s*\z') { throw 'User CA must contain one public PEM certificate only.' }
    $result['SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH'] = (Resolve-Path -LiteralPath $userCa).Path
    $result['SMARTPARK_DISCOVERY_DOCKER_DIRECTORY'] = $directory
    $result['SMARTPARK_DISCOVERY_DOCKER_CERT_PASSWORD'] = $password
    return $result
}
