# Run with Windows PowerShell 5.1; fixture-only, no real environment access.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $root ('.cache/assistant-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $scratch 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'setup.ps1') -Destination $scratch
foreach ($name in @('load-env.ps1','jwt-environment.ps1','check-assistant-setup.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $scratch 'scripts')
}
function dotnet { throw 'Unexpected dotnet call from read-only check.' }
function docker { throw 'Unexpected Docker call from read-only check.' }
$fixture = Join-Path $scratch 'review.env'
@('ConnectionStrings__IoT=synthetic-secret-not-for-output',
    'SMARTPARK_ASSISTANT_DOCKER_ENABLED=true',
    'Assistant__Reads__User__Enabled=true', 'Assistant__Reads__User__Origin=http://localhost:5035',
    'Assistant__Reads__Operations__Enabled=true',
    'Assistant__Reads__Search__Enabled=true', 'Assistant__Reads__Search__Origin=https://operations.example.test',
    'Assistant__Reads__Search__MaxAgeSeconds=0', 'Assistant__Reads__Search__ClockSkewSeconds=0',
    'Assistant__Guest__ReleaseEnabled=invalid') | Set-Content -LiteralPath $fixture -Encoding UTF8
$before = [IO.File]::ReadAllText($fixture)
$priorConnection = $env:ConnectionStrings__IoT
$rows = @(& (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $fixture)
$text = $rows | Out-String
if ($text -match 'synthetic-secret-not-for-output') { throw 'Secret leaked.' }
if ($env:ConnectionStrings__IoT -cne $priorConnection) { throw 'Process environment changed.' }
if ([IO.File]::ReadAllText($fixture) -cne $before -or (Test-Path -LiteralPath (Join-Path $scratch '.env'))) { throw 'Configuration written.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Reads__User__Origin' -and $_.Status -eq 'INVALID' })) { throw 'HTTP read accepted.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Reads__Operations__Origin' -and $_.Status -eq 'INVALID' })) { throw 'Missing HTTPS origin not reported.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Reads__Search__Origin' -and $_.Status -eq 'CONFIGURED' })) { throw 'Valid HTTPS origin rejected.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Reads__Search__MaxAgeSeconds' -and $_.Status -eq 'INVALID' })) { throw 'Invalid search freshness accepted.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Reads__Search__ClockSkewSeconds' -and $_.Status -eq 'CONFIGURED' })) { throw 'Valid zero clock skew rejected.' }
if (!($rows | Where-Object { $_.Check -eq 'Functional readiness' -and $_.Status -eq 'UNVERIFIED' })) { throw 'Readiness falsely accepted.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Jwt__Issuer' -and $_.Status -eq 'MISSING' })) { throw 'Missing JWT not reported.' }
$dockerFixture = Join-Path $scratch 'docker-loopback.env'
($before -replace 'https://operations.example.test', 'https://localhost:5056') | Set-Content -LiteralPath $dockerFixture -Encoding UTF8
$dockerRows = @(& (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $dockerFixture)
if (!($dockerRows | Where-Object { $_.Check -eq 'Assistant__Reads__Search__Origin' -and $_.Status -eq 'INVALID' -and $_.Detail -like 'Docker Assistant*' })) {
    throw 'Docker Assistant loopback origin not rejected.'
}
$dockerOnlyFixture = Join-Path $scratch 'docker-only.env'
($before -replace '(?m)^ConnectionStrings__IoT=.*\r?\n', '') | Set-Content -LiteralPath $dockerOnlyFixture -Encoding UTF8
$dockerOnlyRows = @(& (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $dockerOnlyFixture)
if (!($dockerOnlyRows | Where-Object { $_.Check -eq 'ConnectionStrings__IoT' -and $_.Status -eq 'NOT_REQUIRED' }) -or
    !($dockerOnlyRows | Where-Object { $_.Check -eq 'SMARTPARK_ASSISTANT_URL' -and $_.Status -eq 'NOT_REQUIRED' })) {
    throw 'Docker-only setup incorrectly required host Assistant settings.'
}
$relativeRoot = Join-Path $scratch 'relative-config'
New-Item -ItemType Directory -Path (Join-Path $relativeRoot 'keyring'), (Join-Path $relativeRoot 'keys'),
    (Join-Path $relativeRoot 'knowledge') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $relativeRoot 'keys/public.pem') -Value 'synthetic-public-key' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $relativeRoot 'keys/assistant.pfx') -Value 'synthetic-pfx' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $relativeRoot 'knowledge/smartpark-knowledge.md') -Value 'knowledge' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $relativeRoot 'knowledge/publication.json') -Value '{}' -Encoding UTF8
$relativeFixture = Join-Path $relativeRoot 'relative.env'
@('SMARTPARK_ASSISTANT_DOCKER_ENABLED=true', 'Assistant__Jwt__Issuer=synthetic-issuer',
    'Assistant__Jwt__Audience=synthetic-audience', 'Assistant__Jwt__PublicKeyFile=keys/public.pem',
    'Assistant__Management__KeyDirectory=keyring',
    'Assistant__Management__KeyEncryptionCertificatePath=keys/assistant.pfx',
    'Assistant__Management__KeyEncryptionCertificatePassword=synthetic-password',
    'Assistant__Guest__KnowledgeDirectory=knowledge') |
    Set-Content -LiteralPath $relativeFixture -Encoding UTF8
$relativeRows = @(& (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $relativeFixture)
if (!($relativeRows | Where-Object { $_.Check -eq 'Assistant__Jwt__PublicKeyPem' -and $_.Status -eq 'CONFIGURED' }) -or
    !($relativeRows | Where-Object { $_.Check -eq 'Assistant__Management__KeyDirectory' -and $_.Status -eq 'CONFIGURED' }) -or
    !($relativeRows | Where-Object { $_.Check -eq 'Assistant__Guest__KnowledgeDirectory' -and $_.Status -eq 'CONFIGURED' })) {
    throw 'Relative Assistant paths were not resolved from the environment file.'
}
$rejected = $false
try { & (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $fixture -RecreateDb }
catch { $rejected = $_.Exception.Message -match 'cannot be combined' }
if (!$rejected) { throw 'Destructive option not rejected.' }
$rejected = $false
try { & (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $fixture -WithAssistant }
catch { $rejected = $_.Exception.Message -match 'cannot be combined' }
if (!$rejected) { throw 'Assistant provisioning option not rejected by read-only check.' }
Write-Host 'PASS: setup read-only branch, no CLI calls/env writes, redaction, missing/invalid config, UNVERIFIED readiness, destructive-option rejection.'
