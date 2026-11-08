# Offline process test: no service, DB, certificate installation or user-secrets access.
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $sourceRoot ('.cache/assistant-runner-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $scratch 'scripts') -Force | Out-Null
foreach ($name in @('run-schema-service.ps1','load-env.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $scratch "scripts/$name")
}
$previous = @{}
foreach ($entry in Get-ChildItem Env:) { $previous[$entry.Name] = $entry.Value }
$global:assistantRunnerTestLaunches = 0
function dotnet {
    if ($env:Jwt__PrivateKeyPem -or $env:SMARTPARK_JWT_PRIVATE_KEY_PEM -or $env:SMARTPARK_JWT_PRIVATE_KEY_FILE) { throw 'Assistant inherited signing material.' }
    $global:assistantRunnerTestLaunches++
    if ($env:ASPNETCORE_ENVIRONMENT -ne 'Development' -or $env:DOTNET_ENVIRONMENT -ne 'Development') { throw 'Demo environment leaked.' }
    if ($args -notcontains '--no-launch-profile' -or $args -notcontains 'https://localhost:5075') { throw 'Unexpected launch arguments.' }
    if ($env:ConnectionStrings__IoT -ne 'synthetic-private-value') { throw 'Datasource did not reach process.' }
    $global:LASTEXITCODE = 0
}
function Write-Fixture([string]$Origin = 'https://localhost:5036', [string]$Connection = 'synthetic-private-value') {
    @(
        "ConnectionStrings__IoT=$Connection", 'Assistant__Jwt__Issuer=synthetic-issuer',
        'Assistant__Jwt__Audience=synthetic-audience', 'Assistant__Jwt__PublicKeyPem=synthetic-public-key',
        "Assistant__Management__KeyDirectory=$scratch", "Assistant__Guest__KnowledgeDirectory=$scratch",
        'Assistant__Reads__User__Enabled=true', "Assistant__Reads__User__Origin=$Origin",
        'Assistant__Reads__Operations__Enabled=false', 'Assistant__Reads__Search__Enabled=false',
        'SMARTPARK_ASSISTANT_URL=https://localhost:5075'
    ) | Set-Content -LiteralPath (Join-Path $scratch '.env') -Encoding UTF8
}
function Expect-Rejection([string]$Pattern) {
    $before = $global:assistantRunnerTestLaunches
    $rejected = $false
    try { & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service Assistant }
    catch {
        if ($_.Exception.Message -like '*synthetic-private-value*') { throw 'Secret exposed.' }
        $rejected = $_.Exception.Message -match $Pattern
    }
    if (!$rejected -or $global:assistantRunnerTestLaunches -ne $before) { throw "Expected prelaunch rejection: $Pattern" }
}
try {
    # Explicitly simulate a shell previously used for Demo.
    $env:DOTNET_ENVIRONMENT = 'Demo'
    $env:Jwt__PrivateKeyPem = 'synthetic-leaked-signing-key'
    $env:SMARTPARK_JWT_PRIVATE_KEY_PEM = 'synthetic-inline-signing-key'
    $env:SMARTPARK_JWT_PRIVATE_KEY_FILE = 'synthetic-signing-path'
    Write-Fixture
    & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service Assistant
    if ($global:assistantRunnerTestLaunches -ne 1) { throw 'Normal host not launched once.' }
    $publicFile = Join-Path $scratch 'public.pem'
    [IO.File]::WriteAllText($publicFile, "synthetic-public-key`nsecond-line")
    Add-Content -LiteralPath (Join-Path $scratch '.env') -Value "Assistant__Jwt__PublicKeyFile=$publicFile"
    & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service Assistant
    if ($env:Assistant__Jwt__PublicKeyPem -cne "synthetic-public-key`nsecond-line") { throw 'Multiline public key was not loaded.' }
    $env:Assistant__Jwt__PublicKeyFile = ''
    Write-Fixture -Origin 'http://localhost:5035'
    Expect-Rejection 'fixed HTTPS'
    Write-Fixture -Connection ''
    Expect-Rejection 'ConnectionStrings__IoT'
    foreach ($relativePath in @('\Windows', 'C:Windows')) {
        Write-Fixture
        Add-Content -LiteralPath (Join-Path $scratch '.env') -Value "Assistant__Management__KeyDirectory=$relativePath"
        Expect-Rejection 'must exist and be absolute'
    }
    Write-Host 'PASS: normal host dispatch, process configuration, Demo isolation, HTTPS guard, missing configuration, secret-safe errors.'
} finally {
    foreach ($entry in Get-ChildItem Env:) {
        if (!$previous.ContainsKey($entry.Name)) { [Environment]::SetEnvironmentVariable($entry.Name, $null, 'Process') }
    }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    # Scratch retained under ignored .cache for inspection; no recursive deletion.
}
