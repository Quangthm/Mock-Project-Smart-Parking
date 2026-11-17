#requires -Version 7.0
<# Read-only diagnostic. Prints source names and PUBLIC key fingerprints only.
   It does not choose, export, generate or replace a private key.
   Relative EnvPath/UserSecretsPath arguments resolve from the repository root.
   Private-key FILE values resolve from the directory containing .env.
#>
[CmdletBinding()]
param([string]$EnvPath = '.env', [string]$UserSecretsPath, [uri]$UserJwksUri)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')
if ($UserJwksUri -and ($UserJwksUri.Scheme -notin @('http','https') -or
    !$UserJwksUri.IsLoopback -or $UserJwksUri.UserInfo -or $UserJwksUri.Fragment)) {
    throw 'UserJwksUri must be an explicit loopback HTTP(S) endpoint without credentials or fragment.'
}
if (![IO.Path]::IsPathRooted($EnvPath)) { $EnvPath = Join-Path $root $EnvPath }
$envDirectory = Split-Path ([IO.Path]::GetFullPath($EnvPath)) -Parent
$records = [Collections.Generic.List[object]]::new()
function Add-KeySource([string]$Source, [string]$Value, [string]$Kind) {
    $status = 'MISSING'; $fingerprint = $null
    if (Test-JwtConfiguredValue $Value) {
        $status = 'INVALID'
        $rsa = [Security.Cryptography.RSA]::Create()
        try {
            if ($Kind -eq 'FILE') { $Value = Read-JwtPrivateKeyFile $Value $envDirectory }
            elseif ($Kind -eq 'BASE64') { $Value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Value)) }
            elseif ($Kind -eq 'ENVPEM') { $Value = $Value.Replace('\n', "`n") }
            elseif ($Kind -ne 'PEM') { throw 'Unsupported signing alias.' }
            $rsa.ImportFromPem($Value)
            if ($rsa.KeySize -lt 2048) { throw 'RSA key is too small.' }
            # Signing requires a private key, not just a public PEM.
            $null = $rsa.ExportParameters($true)
            $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($rsa.ExportSubjectPublicKeyInfo()))
            $status = 'CONFIGURED'
        } catch { $status = 'INVALID' } finally { $rsa.Dispose() }
    }
    $records.Add([pscustomobject]@{ Source=$Source; Status=$status; PublicSpkiSha256=$fingerprint; MatchesLiveJwks=$null })
}
$settings = @{}
if (Test-Path -LiteralPath $EnvPath -PathType Leaf) {
    try { $settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport }
    catch { $records.Add([pscustomobject]@{Source='.env file';Status='UNREADABLE';PublicSpkiSha256=$null;MatchesLiveJwks=$null}) }
}
foreach ($slot in @(
    @('SMARTPARK_JWT_PRIVATE_KEY_FILE','FILE'),
    @('SMARTPARK_JWT_PRIVATE_KEY_PEM','ENVPEM'),
    @('SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64','BASE64'))) {
    Add-KeySource ('.env:' + $slot[0]) $settings[$slot[0]] $slot[1]
}
$processSources = Get-JwtSigningEnvironment
if (!$processSources.Count) { Add-KeySource 'process:signing aliases' '' 'PEM' }
foreach ($name in ($processSources.Keys | Sort-Object)) {
    $normalized = $name.Replace('_','').Replace(':','')
    $kind = if ($normalized -match 'PRIVATEKEYPEMBASE64$') { 'BASE64' }
        elseif ($normalized -match 'PRIVATEKEYPEM$') { 'PEM' }
        elseif ($normalized -match 'PRIVATEKEYFILE$') { 'FILE' } else { 'UNKNOWN' }
    Add-KeySource ('process:' + $name) $processSources[$name] $kind
}
if (!$UserSecretsPath) {
    $project = Join-Path $root 'src/Services/UserService/SmartParking.UserService.API/SmartParking.UserService.API.csproj'
    $id = ([xml][IO.File]::ReadAllText($project)).Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1
    $secretsDirectory = if ($env:APPDATA) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' }
        else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
    $UserSecretsPath = Join-Path $secretsDirectory "$id/secrets.json"
} elseif (![IO.Path]::IsPathRooted($UserSecretsPath)) { $UserSecretsPath = Join-Path $root $UserSecretsPath }
$secrets = @{}
if (Test-Path -LiteralPath $UserSecretsPath -PathType Leaf) {
    try { $secrets = [IO.File]::ReadAllText($UserSecretsPath) | ConvertFrom-Json -AsHashtable }
    catch { $records.Add([pscustomobject]@{Source='UserService user-secrets file';Status='UNREADABLE';PublicSpkiSha256=$null;MatchesLiveJwks=$null}) }
}
foreach ($name in @('PrivateKeyPem','PrivateKeyPemBase64')) {
    $kind = if ($name.EndsWith('Base64')) { 'BASE64' } else { 'PEM' }
    Add-KeySource ('UserService user-secrets:Jwt:' + $name) $secrets["Jwt:$name"] $kind
    if ($secrets['Jwt'] -is [Collections.IDictionary] -and $secrets['Jwt'].Contains($name)) {
        Add-KeySource ('UserService user-secrets:nested Jwt.' + $name) $secrets['Jwt'][$name] $kind
    }
}
$liveFingerprints = @(); $jwksStatus = 'NOT_REQUESTED'
if ($UserJwksUri) {
    try {
        # No redirects: a local endpoint must not redirect this request elsewhere.
        $jwks = Invoke-RestMethod -Uri $UserJwksUri -TimeoutSec 10 -MaximumRedirection 0 -ErrorAction Stop
        foreach ($key in $jwks.keys) {
            if ($key.kty -ne 'RSA') { continue }
            $parameters = [Security.Cryptography.RSAParameters]::new()
            foreach ($part in @('n','e')) {
                $encoded = ([string]$key.$part).Replace('-','+').Replace('_','/')
                $encoded = $encoded.PadRight($encoded.Length + ((4 - $encoded.Length % 4) % 4), '=')
                $bytes = [Convert]::FromBase64String($encoded)
                if ($part -eq 'n') { $parameters.Modulus = $bytes } else { $parameters.Exponent = $bytes }
            }
            $rsa = [Security.Cryptography.RSA]::Create()
            try {
                $rsa.ImportParameters($parameters)
                $liveFingerprints += [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($rsa.ExportSubjectPublicKeyInfo()))
            } finally { $rsa.Dispose() }
        }
        if (!$liveFingerprints.Count) { throw 'No RSA public keys.' }
        $jwksStatus = 'READ'
        foreach ($record in $records) {
            if ($record.Status -eq 'CONFIGURED') { $record.MatchesLiveJwks = $record.PublicSpkiSha256 -in $liveFingerprints }
        }
    } catch { $jwksStatus = 'UNAVAILABLE_OR_INVALID' }
}
$distinct = @($records | Where-Object Status -eq 'CONFIGURED' | Select-Object -ExpandProperty PublicSpkiSha256 -Unique)
# Structured output prevents table truncation; only explicitly safe fields leave the script.
[pscustomobject]@{
    Sources = @($records.ToArray())
    DistinctPrivateKeyCount = $distinct.Count
    MultipleDifferentKeys = $distinct.Count -gt 1
    LiveJwksStatus = $jwksStatus
    LivePublicSpkiSha256 = @($liveFingerprints)
    Note = 'Read-only inventory; no key selected. A fingerprint match identifies public-key identity, not configuration precedence. Missing live matches do not authorize rotation.'
} | ConvertTo-Json -Depth 5
