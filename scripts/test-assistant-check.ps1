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
    'Assistant__Reads__User__Enabled=true', 'Assistant__Reads__User__Origin=http://localhost:5035',
    'Assistant__Guest__ReleaseEnabled=invalid') | Set-Content -LiteralPath $fixture -Encoding UTF8
$before = [IO.File]::ReadAllText($fixture)
$priorConnection = $env:ConnectionStrings__IoT
$rows = @(& (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $fixture)
$text = $rows | Out-String
if ($text -match 'synthetic-secret-not-for-output') { throw 'Secret leaked.' }
if ($env:ConnectionStrings__IoT -cne $priorConnection) { throw 'Process environment changed.' }
if ([IO.File]::ReadAllText($fixture) -cne $before -or (Test-Path -LiteralPath (Join-Path $scratch '.env'))) { throw 'Configuration written.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Reads__User__Origin' -and $_.Status -eq 'INVALID' })) { throw 'HTTP read accepted.' }
if (!($rows | Where-Object { $_.Check -eq 'Functional readiness' -and $_.Status -eq 'UNVERIFIED' })) { throw 'Readiness falsely accepted.' }
if (!($rows | Where-Object { $_.Check -eq 'Assistant__Jwt__Issuer' -and $_.Status -eq 'MISSING' })) { throw 'Missing JWT not reported.' }
$rejected = $false
try { & (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $fixture -RecreateDb }
catch { $rejected = $_.Exception.Message -match 'cannot be combined' }
if (!$rejected) { throw 'Destructive option not rejected.' }
$rejected = $false
try { & (Join-Path $scratch 'setup.ps1') -AssistantCheck -AssistantEnvPath $fixture -WithAssistant }
catch { $rejected = $_.Exception.Message -match 'cannot be combined' }
if (!$rejected) { throw 'Assistant provisioning option not rejected by read-only check.' }
Write-Host 'PASS: setup read-only branch, no CLI calls/env writes, redaction, missing/invalid config, UNVERIFIED readiness, destructive-option rejection.'
