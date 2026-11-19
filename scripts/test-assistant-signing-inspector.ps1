#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('.cache/signing-inspector-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixture
$inspector = Join-Path $PSScriptRoot 'inspect-assistant-signing.ps1'
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')
$saved = Get-JwtSigningEnvironment
$rsa = [Security.Cryptography.RSA]::Create(2048)
$other = [Security.Cryptography.RSA]::Create(2048)
$small = [Security.Cryptography.RSA]::Create(1024)
$envFile = Join-Path $fixture '.env'
$secretsFile = Join-Path $fixture 'secrets.json'
try {
    Clear-JwtSigningEnvironment
    $pem = $rsa.ExportPkcs8PrivateKeyPem()
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pem))
    [IO.File]::WriteAllText((Join-Path $fixture 'key.pem'), $pem)
    [IO.File]::WriteAllText($envFile, "SMARTPARK_JWT_PRIVATE_KEY_FILE=key.pem`nSMARTPARK_JWT_PRIVATE_KEY_PEM=$($pem.Replace("`n",'\n'))`nSMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=$encoded")
    [IO.File]::WriteAllText($secretsFile, (@{'Jwt:PrivateKeyPem'=$pem; Jwt=@{PrivateKeyPemBase64=$encoded}} | ConvertTo-Json))
    $env:Jwt__PrivateKeyPem = $pem
    $hashes = @(Get-FileHash -LiteralPath $envFile,$secretsFile,(Join-Path $fixture 'key.pem') | Select-Object -ExpandProperty Hash)
    $raw = & $inspector -EnvPath $envFile -UserSecretsPath $secretsFile
    $result = $raw | ConvertFrom-Json
    if ($result.DistinctPrivateKeyCount -ne 1 -or $result.MultipleDifferentKeys -or
        @($result.Sources | Where-Object Status -eq CONFIGURED).Count -ne 6 -or $result.LiveJwksStatus -ne 'NOT_REQUESTED') { throw 'Matching source inventory failed.' }
    if ($raw.Contains($pem) -or $raw.Contains($encoded) -or $raw -match 'BEGIN PRIVATE KEY') { throw 'Secret leaked.' }
    if ($env:Jwt__PrivateKeyPem -cne $pem -or (Compare-Object $hashes @(Get-FileHash -LiteralPath $envFile,$secretsFile,(Join-Path $fixture 'key.pem') | Select-Object -ExpandProperty Hash))) { throw 'Inspection mutated sources.' }
    $public = $rsa.ExportParameters($false)
    $n = [Convert]::ToBase64String($public.Modulus).TrimEnd('=').Replace('+','-').Replace('/','_')
    $e = [Convert]::ToBase64String($public.Exponent).TrimEnd('=').Replace('+','-').Replace('/','_')
    $body = @{ keys=@(@{kty='RSA'; n=$n; e=$e}) } | ConvertTo-Json -Depth 4 -Compress
    # Stub only the network boundary; exercise real JWK decoding/fingerprints.
    function Invoke-RestMethod { param($Uri,$TimeoutSec,$MaximumRedirection,$ErrorAction) return ($body | ConvertFrom-Json) }
    try {
        $result = (& $inspector -EnvPath $envFile -UserSecretsPath $secretsFile -UserJwksUri 'http://127.0.0.1:5035/.well-known/jwks.json') | ConvertFrom-Json
        if ($result.LiveJwksStatus -ne 'READ' -or @($result.Sources | Where-Object MatchesLiveJwks -eq $true).Count -ne 6) { throw 'Mocked public JWKS matching failed.' }
    } finally { Remove-Item Function:Invoke-RestMethod }
    $env:DOTNET_Jwt__PrivateKeyPem = $other.ExportPkcs8PrivateKeyPem()
    $result = (& $inspector -EnvPath $envFile -UserSecretsPath $secretsFile) | ConvertFrom-Json
    if (!$result.MultipleDifferentKeys -or $result.DistinctPrivateKeyCount -ne 2) { throw 'Conflicting aliases were not identified.' }
    Clear-JwtSigningEnvironment
    [IO.File]::WriteAllText($envFile, "SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=invalid-fixture")
    [IO.File]::WriteAllText($secretsFile, (@{'Jwt:PrivateKeyPem'=$small.ExportPkcs8PrivateKeyPem()} | ConvertTo-Json))
    $result = (& $inspector -EnvPath $envFile -UserSecretsPath $secretsFile) | ConvertFrom-Json
    if (@($result.Sources | Where-Object Status -eq INVALID).Count -ne 2) { throw 'Invalid Base64 or weak RSA key accepted.' }
    $rejected = $false
    try { $null = & $inspector -EnvPath $envFile -UserSecretsPath $secretsFile -UserJwksUri 'https://example.com/jwks' } catch { $rejected = $true }
    if (!$rejected) { throw 'Remote JWKS endpoint accepted.' }
    [IO.File]::WriteAllText($secretsFile, 'not-json')
    $result = (& $inspector -EnvPath $envFile -UserSecretsPath $secretsFile) | ConvertFrom-Json
    if (@($result.Sources | Where-Object Status -eq UNREADABLE).Count -ne 1) { throw 'Unreadable secrets file not reported.' }
    Write-Host 'PASS: matching/conflicting sources, process aliases, nested secrets, invalid/weak keys, no secret output, no source mutation, mocked JWKS match, remote JWKS rejected.'
} finally {
    Restore-JwtSigningEnvironment $saved
    $rsa.Dispose(); $other.Dispose(); $small.Dispose()
    $resolved = [IO.Path]::GetFullPath($fixture)
    $allowed = [IO.Path]::GetFullPath((Join-Path $root '.cache')) + [IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture cleanup escaped .cache.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
