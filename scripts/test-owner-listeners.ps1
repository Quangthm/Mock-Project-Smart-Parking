# Offline host-runner test. Fake dotnet, fixture env, no service/certificate/DB.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $root ('.cache/listeners-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $scratch 'scripts') -Force | Out-Null
foreach ($name in @('load-env.ps1','run-schema-service.ps1','jwt-environment.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $scratch 'scripts')
}
$previous = @{}
foreach ($entry in Get-ChildItem Env:) { $previous[$entry.Name] = $entry.Value }
$global:ownerListenerTestCalls = 0
$global:ownerListenerExpected = ''
function dotnet {
    if ($global:ownerListenerExpectedSigning -and $env:Jwt__PrivateKeyPem -cne $global:ownerListenerExpectedSigning) { throw 'Signing key file not loaded into User child.' }
    $global:ownerListenerTestCalls++
    $urls = $args[[Array]::IndexOf($args,'--urls') + 1]
    if ($urls -cne $global:ownerListenerExpected) { throw 'Listener arguments differ.' }
    $global:LASTEXITCODE = 0
}
function Write-ListenerFixture([string]$Key, [string]$Value) {
    @('SMARTPARK_DEV_PASSWORD=synthetic-password',
      'SMARTPARK_SERVICE_KEY=synthetic-service-key-at-least-32-characters',
      'SMARTPARK_OWNER_PASSWORD=', 'SMARTPARK_OPERATOR_PASSWORD=',
      "$Key=$Value") | Set-Content -LiteralPath (Join-Path $scratch '.env') -Encoding UTF8
}
try {
    foreach ($case in @(
        @('User','USER','5035','5036'), @('Facility','PARKING','5045','5046'),
        @('Operation','RESERVATION','5055','5056'), @('Payment','PAYMENT','5065','5066'))) {
        $key = "SMARTPARK_SERVICE_$($case[1])_LISTEN_URLS"
        Write-ListenerFixture $key ''
        $global:ownerListenerExpected = "http://localhost:$($case[2])"
        & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service $case[0]
        $global:ownerListenerExpected = "http://localhost:$($case[2]);https://localhost:$($case[3])"
        Write-ListenerFixture $key $global:ownerListenerExpected
        & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service $case[0]
    }
    foreach ($bad in @('https://0.0.0.0:5036','https://localhost:5036/private','https://localhost:5036;','https://user:secret@localhost:5036')) {
        Write-ListenerFixture 'SMARTPARK_SERVICE_USER_LISTEN_URLS' $bad
        $before = $global:ownerListenerTestCalls
        $rejected = $false
        try { & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service User }
        catch {
            if ($_.Exception.Message -match 'user:secret') { throw 'Configuration value exposed.' }
            $rejected = $_.Exception.Message -match 'Invalid local listener'
        }
        if (!$rejected -or $global:ownerListenerTestCalls -ne $before) { throw 'Invalid listener was dispatched.' }
    }
    $privateFile=Join-Path $scratch 'synthetic-signing.pem'
    [IO.File]::WriteAllText($privateFile,"synthetic-private-key`nsecond-line")
    Write-ListenerFixture 'SMARTPARK_SERVICE_USER_LISTEN_URLS' ''
    Add-Content -LiteralPath (Join-Path $scratch '.env') -Value "SMARTPARK_JWT_PRIVATE_KEY_FILE=$privateFile"
    $global:ownerListenerExpected='http://localhost:5035'
    $priorSigningPem=$env:Jwt__PrivateKeyPem
    $global:ownerListenerExpectedSigning="synthetic-private-key`nsecond-line"
    & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service User
    if($env:Jwt__PrivateKeyPem -cne $priorSigningPem){throw 'Signing key remained in caller after User exit.'}
    Write-Host 'PASS: four owner defaults, HTTP+HTTPS overrides, aliases, invalid listener rejection before dispatch.'
} finally {
    foreach ($entry in Get-ChildItem Env:) {
        if (!$previous.ContainsKey($entry.Name)) { [Environment]::SetEnvironmentVariable($entry.Name,$null,'Process') }
    }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
