#requires -Version 7.0
# Offline fixtures only; no real .env, Docker, keyring or database changes.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $root ('.cache/discovery-fixture-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $scratch
$rsa = [Security.Cryptography.RSA]::Create(2048)
$cert = $null
try {
    $csr = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=fixture', $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $cert = $csr.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1),[DateTimeOffset]::UtcNow.AddDays(1))
    $caPath = Join-Path $scratch 'user-ca.crt'
    [IO.File]::WriteAllText($caPath,$cert.ExportCertificatePem())
} finally { if($cert){$cert.Dispose()}; $rsa.Dispose() }
$fixture = Join-Path $scratch '.env'
[IO.File]::WriteAllLines($fixture,@('SMARTPARK_USER_DOCKER_TLS_ENABLED=true',"SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH=$caPath",'UNRELATED_FIXTURE=keep-me'))
& (Join-Path $PSScriptRoot 'initialize-discovery-docker.ps1') -EnvPath $fixture
$before = [IO.File]::ReadAllText($fixture)
& (Join-Path $PSScriptRoot 'initialize-discovery-docker.ps1') -EnvPath $fixture
if ($before -cne [IO.File]::ReadAllText($fixture)) { throw 'Initialization must preserve existing material on rerun.' }
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $fixture -NoExport
. (Join-Path $PSScriptRoot 'discovery-docker.ps1')
$good = Get-DiscoveryDockerEnvironment $settings $scratch
if ($good.Count -eq 0 -or $settings['Assistant__Reads__Search__Enabled'] -cne 'false' -or $settings['UNRELATED_FIXTURE'] -cne 'keep-me') { throw 'Initialization changed unrelated state or activated search.' }
foreach ($case in @(
    @{Key='Discovery__PaymentServiceKey'; Value='wrong'},
    @{Key='DiscoveryPricing__Operations__Current__ExpiresAt'; Value='2020-01-01T00:00:00Z'},
    @{Key='Assistant__Reads__Search__Origin'; Value='https://localhost:5056'},
    @{Key='Discovery__FacilityOrigin'; Value='http://facility-service:8080'},
    @{Key='Discovery__MaxAgeSeconds'; Value='0'},
    @{Key='SMARTPARK_DISCOVERY_DOCKER_CERT_PASSWORD'; Value='wrong'}
)) {
    $bad = $settings.Clone(); $bad[$case.Key] = $case.Value
    $rejected = $false
    try { $null = Get-DiscoveryDockerEnvironment $bad $scratch } catch { $rejected = $true }
    if (!$rejected) { throw ('Invalid fixture accepted: ' + $case.Key) }
}
$directory = $good['SMARTPARK_DISCOVERY_DOCKER_DIRECTORY']
$bindings = Join-Path $directory 'pricing-bindings.env'
[IO.File]::WriteAllText($bindings,'ASPNETCORE_ENVIRONMENT=Production')
$rejected = $false
try { $null = Get-DiscoveryDockerEnvironment $settings $scratch } catch { $rejected = $true }
if (!$rejected) { throw 'Unexpected env injection in bindings file accepted.' }
[IO.File]::WriteAllText($bindings,'# empty fixture')
$ca = Join-Path $directory 'discovery-ca.crt'
$original = [IO.File]::ReadAllText($ca)
[IO.File]::WriteAllText($ca,[IO.File]::ReadAllText($caPath))
$rejected = $false
try { $null = Get-DiscoveryDockerEnvironment $settings $scratch } catch { $rejected = $true }
if (!$rejected) { throw 'Unrelated CA accepted.' }
[IO.File]::WriteAllText($ca,$original)
$null = Get-DiscoveryDockerEnvironment $settings $scratch
Write-Host 'PASS: initialization, idempotence, unrelated settings preservation, fail-closed origins/credentials/expiry/freshness/PFX/CA/bindings.'
Write-Host 'Synthetic fixture retained under ignored .cache. No Docker or database calls.'
