#requires -Version 5.1
<#
.SYNOPSIS
Run Assistant regression tests against synthetic fixtures.
.DESCRIPTION
Offline is the default. PowerShell 7 includes the detailed RSA fixtures.
Windows PowerShell 5.1 runs the compatible checks including the signing handoff.
DockerPreflight validates fixture configuration without starting containers.
Docker and Postgres provision uniquely named, disposable test resources.
#>
[CmdletBinding()]
param(
    [ValidateSet('Offline', 'DockerPreflight', 'Docker', 'Postgres')]
    [string]$Suite = 'Offline',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if ($NoBuild -and $Suite -notin @('DockerPreflight', 'Docker')) {
    throw '-NoBuild is supported only with DockerPreflight or Docker.'
}

function Test-AssistantReadiness {
    $scratch = Join-Path $testWorkspace 'assistant-check'
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
        'Assistant__Guest__ReleaseEnabled=TRUE', 'Assistant__Chat__ReleaseEnabled=FALSE') |
        Set-Content -LiteralPath $fixture -Encoding UTF8
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
    foreach ($flag in @('Assistant__Guest__ReleaseEnabled', 'Assistant__Chat__ReleaseEnabled')) {
        if (!($rows | Where-Object { $_.Check -eq $flag -and $_.Status -eq 'INVALID' })) {
            throw "Uppercase release flag accepted: $flag."
        }
    }
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
}

function Test-AssistantHostRunner {
    $scratch = Join-Path $testWorkspace 'assistant-runner'
    New-Item -ItemType Directory -Path (Join-Path $scratch 'scripts') -Force | Out-Null
    foreach ($name in @('run-schema-service.ps1','load-env.ps1','jwt-environment.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $scratch "scripts/$name")
    }
    $previous = @{}
    foreach ($entry in Get-ChildItem Env:) { $previous[$entry.Name] = $entry.Value }
    $launches = @{ Count = 0 }
    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')
    function dotnet {
        if ((Get-JwtSigningEnvironment).Count) { throw 'Assistant inherited signing material.' }
        $launches.Count++
        if ($env:ASPNETCORE_ENVIRONMENT -ne 'Development' -or $env:DOTNET_ENVIRONMENT -ne 'Development') { throw 'Demo environment leaked.' }
        if ($args -notcontains '--no-launch-profile' -or $args -notcontains 'https://localhost:5075') { throw 'Unexpected launch arguments.' }
        if ($env:ConnectionStrings__IoT -ne 'synthetic-private-value') { throw 'Datasource did not reach process.' }
        $global:LASTEXITCODE = 0
    }
    function Write-Fixture([string]$Origin = 'https://localhost:5036', [string]$Connection = 'synthetic-private-value') {
        @(
            'SMARTPARK_DEV_PASSWORD=synthetic-db-password',
            'SMARTPARK_SERVICE_KEY=synthetic-service-key-at-least-32-characters',
            "ConnectionStrings__IoT=$Connection", 'Assistant__Jwt__Issuer=synthetic-issuer',
            'Assistant__Jwt__Audience=synthetic-audience', 'Assistant__Jwt__PublicKeyPem=synthetic-public-key',
            "Assistant__Management__KeyDirectory=$scratch", "Assistant__Guest__KnowledgeDirectory=$scratch",
            'Assistant__Reads__User__Enabled=true', "Assistant__Reads__User__Origin=$Origin",
            'Assistant__Reads__Operations__Enabled=false', 'Assistant__Reads__Search__Enabled=false',
            'SMARTPARK_ASSISTANT_URL=https://localhost:5075'
        ) | Set-Content -LiteralPath (Join-Path $scratch '.env') -Encoding UTF8
    }
    function Expect-Rejection([string]$Pattern) {
        $before = $launches.Count
        $rejected = $false
        try { & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service Assistant }
        catch {
            if ($_.Exception.Message -like '*synthetic-private-value*') { throw 'Secret exposed.' }
            $rejected = $_.Exception.Message -match $Pattern
        }
        if (!$rejected -or $launches.Count -ne $before) { throw "Expected prelaunch rejection: $Pattern" }
    }
    try {
        # Explicitly simulate a shell previously used for Demo.
        $env:DOTNET_ENVIRONMENT = 'Demo'
        $env:Jwt__PrivateKeyPem = 'synthetic-leaked-signing-key'
        $env:SMARTPARK_JWT_PRIVATE_KEY_PEM = 'synthetic-inline-signing-key'
        $env:SMARTPARK_JWT_PRIVATE_KEY_FILE = 'synthetic-signing-path'
        $env:ASPNETCORE_Jwt__PrivateKeyPemBase64 = 'synthetic-prefixed-signing-key'
        Write-Fixture
        & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service Assistant
        if ($launches.Count -ne 1) { throw 'Normal host not launched once.' }
        $publicFile = Join-Path $scratch 'public.pem'
        [IO.File]::WriteAllText($publicFile, "-----BEGIN PUBLIC KEY-----`nY3ludGhldGljLXB1YmxpYy1rZXk=`nc2Vjb25kLWxpbmU=`n-----END PUBLIC KEY-----")
        Add-Content -LiteralPath (Join-Path $scratch '.env') -Value "Assistant__Jwt__PublicKeyFile=$publicFile"
        & (Join-Path $scratch 'scripts/run-schema-service.ps1') -Service Assistant
        if ($env:Assistant__Jwt__PublicKeyPem -cne "-----BEGIN PUBLIC KEY-----`nY3ludGhldGljLXB1YmxpYy1rZXk=`nc2Vjb25kLWxpbmU=`n-----END PUBLIC KEY-----") { throw 'Multiline public key was not loaded.' }
        $env:Assistant__Jwt__PublicKeyFile = ''
        Write-Fixture -Origin 'http://localhost:5035'
        Expect-Rejection 'fixed HTTPS'
        Write-Fixture -Connection ''
        Expect-Rejection 'ConnectionStrings__IoT'
        foreach ($relativePath in @('\DefinitelyMissingSmartParkKeyring', 'C:\DefinitelyMissingSmartParkKeyring')) {
            Write-Fixture
            Add-Content -LiteralPath (Join-Path $scratch '.env') -Value "Assistant__Management__KeyDirectory=$relativePath"
            Expect-Rejection 'Assistant__Management__KeyDirectory'
        }
        Write-Host 'PASS: normal host dispatch, process configuration, Demo isolation, HTTPS guard, missing configuration, secret-safe errors.'
    } finally {
        foreach ($entry in Get-ChildItem Env:) {
            if (!$previous.ContainsKey($entry.Name)) { Remove-Item -LiteralPath "Env:$($entry.Name)" }
        }
        foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    }
}

function Test-AssistantMigrationPlan {
    function docker { throw 'Unexpected Docker call.' }
    function dotnet { throw 'Unexpected dotnet call.' }
    $runner = Join-Path $PSScriptRoot 'apply-workflow-migrations.ps1'
    $iot = @(& $runner -Service Iot -SchemaRoot $root -PlanAssistant)
    $facility = @(& $runner -Service Parking -SchemaRoot $root -PlanAssistant)
    if ($iot.Count -ne 4 -or $facility.Count -ne 1) { throw 'Migration count differs.' }
    if ($iot[0].File -notlike '*20261103_iot_assistant_management.sql' -or
        $iot[1].File -notlike '*20261105_iot_assistant_s02.sql' -or
        $iot[2].File -notlike '*20261105_iot_assistant_s05.sql' -or
        $iot[3].File -notlike '*20261110_iot_assistant_current_chat.sql') { throw 'Dependency order differs.' }
    $compose = Get-Content -LiteralPath (Join-Path $root 'compose.schema-integration.yml') -Raw
    $mounts = [regex]::Matches($compose, '(?m)^\s+- \./(scripts/database/migrations/[^:\s]+_iot_assistant_[^:\s]+\.sql):/docker-entrypoint-initdb.d/([0-9]+)-[^:\s]+:ro\s*$')
    $mountedOrder = $mounts | Sort-Object { [int]$_.Groups[2].Value } | ForEach-Object { $_.Groups[1].Value }
    if (($mountedOrder -join '|') -cne ($iot.File -join '|')) { throw 'IoT Compose init and Assistant migration order differ.' }
    foreach ($row in @($iot) + @($facility)) {
        if ($row.State -ne 'UNVERIFIED' -or $row.Sha256 -cne (Get-FileHash -LiteralPath (Join-Path $root $row.File) -Algorithm SHA256).Hash) {
            throw 'Invalid approval artifact.'
        }
    }
    if (@($iot | Where-Object { $_.Database -ne 'smartpark_iot' }).Count -or $facility[0].Database -ne 'smartpark_facility') { throw 'Wrong DB mapping.' }
    $rejected = $false
    try { & $runner -Service User -SchemaRoot $root -PlanAssistant }
    catch { $rejected = $_.Exception.Message -match 'supports only' }
    if (!$rejected) { throw 'Unsupported service accepted.' }
    Write-Host 'PASS: ordered SQL approval artifacts, DB targets, exact SHA256, UNVERIFIED state, unsupported-service guard; no DB/CLI.'
}

function Test-AssistantKnowledgePublication {
    $fixture = Join-Path $testWorkspace 'publication-test'
    $null = [IO.Directory]::CreateDirectory($fixture)
    $sourcePath = Join-Path $fixture 'source.md'
    $output = Join-Path $fixture 'published'
    $helper = Join-Path $PSScriptRoot 'setup-assistant-docker.ps1'
    $source = @'
---
version: fixture-1
locale: vi
audience: [public, driver]
publication_state: draft
review_status: pending
effective_from: null
effective_to: null
published_at: null
published_by: null
---
## [fixture] Dữ liệu kiểm thử
Từ khóa: kiểm thử; fixture
### Nội dung
Đây là dữ liệu tổng hợp dùng kiểm tra script, không phải tài liệu nghiệp vụ.
'@
    $utf8 = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($sourcePath, $source, $utf8)
    $params = @{ SourcePath = $sourcePath; OutputDirectory = $output; ApprovedBy = 'Fixture Admin (synthetic test only)'; ApprovalReference = 'fixture://synthetic-review/1' }
    function Assert($condition, [string]$message) { if (!$condition) { throw $message } }
    function Reject([scriptblock]$action, [string]$message) {
        $failed = $false
        try { & $action } catch { $failed = $true }
        Assert $failed $message
    }
    & $helper -PublishKnowledge @params
    $bytes = [IO.File]::ReadAllBytes((Join-Path $output 'smartpark-knowledge.md'))
    $manifestText = [IO.File]::ReadAllText((Join-Path $output 'publication.json'))
    $manifest = $manifestText | ConvertFrom-Json
    # Read the JSON string without PowerShell 7.5 automatic date conversion or System.Text.Json.
    $publishedAt = ([regex]::Match($manifestText, '"publishedAt"\s*:\s*("[^"\\]*")').Groups[1].Value.Trim('"'))
    $text = $utf8.GetString($bytes)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $digest = [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace('-', '') } finally { $sha256.Dispose() }
    Assert ($manifest.sha256 -ceq $digest) 'SHA does not match exact deployed bytes.'
    Assert ($bytes[0] -eq 45) 'Published Markdown must not have a UTF-8 BOM.'
    Assert ($manifest.version -ceq 'fixture-1' -and $manifest.role -ceq 'admin' -and $manifest.approvalReference -ceq $params.ApprovalReference) 'Manifest metadata mismatch.'
    Assert ($text.Contains('publication_state: published') -and $text.Contains('review_status: approved') -and $text.Contains('published_at: ' + $publishedAt)) 'Published frontmatter mismatch.'
    Assert ([DateTimeOffset]::Parse($publishedAt) -le [DateTimeOffset]::UtcNow) 'Publication timestamp is future.'
    Assert ([IO.File]::ReadAllText($sourcePath) -ceq $source) 'Editing source was changed.'
    # Verify the entire UTF-8 publication, including Vietnamese text and LF normalization.
    $expected = $source.Replace("`r`n", "`n").Replace('publication_state: draft', 'publication_state: published').Replace('review_status: pending', 'review_status: approved').Replace('published_at: null', 'published_at: ' + $publishedAt).Replace('published_by: null', 'published_by: ' + ($params.ApprovedBy | ConvertTo-Json -Compress))
    Assert ([Convert]::ToBase64String($bytes) -ceq [Convert]::ToBase64String($utf8.GetBytes($expected))) 'Published UTF-8 bytes differ from the exact expected corpus.'
    Assert ($text.Contains('Dữ liệu kiểm thử') -and $text.Contains('Đây là dữ liệu tổng hợp')) 'Vietnamese text was corrupted.'
    Reject { & $helper -PublishKnowledge @params } 'Existing publication must be preserved.'
    Assert ([IO.File]::ReadAllText((Join-Path $output 'publication.json')) -ceq $manifestText) 'Existing manifest was overwritten.'
    # A BOM and CRLF in the editing source must not leak into deployment bytes.
    $bomSource = [Text.UTF8Encoding]::new($true).GetPreamble() + $utf8.GetBytes($source.Replace("`r`n", "`n").Replace("`n", "`r`n"))
    [IO.File]::WriteAllBytes($sourcePath, $bomSource)
    $params.OutputDirectory = Join-Path $fixture 'published-bom-crlf'
    & $helper -PublishKnowledge @params
    $normalizedBytes = [IO.File]::ReadAllBytes((Join-Path $params.OutputDirectory 'smartpark-knowledge.md'))
    $normalizedManifestText = [IO.File]::ReadAllText((Join-Path $params.OutputDirectory 'publication.json'))
    $normalizedAt = ([regex]::Match($normalizedManifestText, '"publishedAt"\s*:\s*("[^"\\]*")').Groups[1].Value.Trim('"'))
    Assert ([Convert]::ToBase64String($normalizedBytes) -ceq [Convert]::ToBase64String($utf8.GetBytes($expected.Replace($publishedAt, $normalizedAt)))) 'BOM/CRLF source did not produce exact normalized UTF-8 bytes.'
    Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($sourcePath)) -ceq [Convert]::ToBase64String($bomSource)) 'BOM/CRLF editing source bytes were changed.'
    $params.OutputDirectory = Join-Path $fixture 'rejected'
    foreach ($placeholder in @('', 'admin', '<actual-admin>', 'REPLACE_WITH_ADMIN', 'your-admin')) {
        $params.ApprovedBy = $placeholder
        Reject { & $helper -PublishKnowledge @params } 'Placeholder approver must be rejected.'
    }
    $params.ApprovedBy = 'Fixture Admin (synthetic test only)'
    $params.ApprovalReference = 'TODO'
    Reject { & $helper -PublishKnowledge @params } 'Placeholder approval reference must be rejected.'
    $params.ApprovalReference = 'fixture://synthetic-review/1'
    [IO.File]::WriteAllText($sourcePath, $source.Replace('effective_to: null', 'effective_to: 2000-01-01T00:00:00Z'), $utf8)
    Reject { & $helper -PublishKnowledge @params } 'Expired corpus must be rejected.'
    [IO.File]::WriteAllText($sourcePath, $source.Replace('### Nội dung', '### Broken'), $utf8)
    Reject { & $helper -PublishKnowledge @params } 'Malformed corpus must be rejected.'
    foreach ($tight in @(
        ($source + "`n### English: Booking`n### Đối chiếu — không gửi đến AI`nprivate note"),
        ($source.Substring(0, $source.IndexOf('### Nội dung')) + "### Nội dung`n### English: Booking`nEnglish only.")
    )) {
        [IO.File]::WriteAllText($sourcePath, $tight, $utf8)
        Reject { & $helper -PublishKnowledge @params } 'Empty language block immediately followed by a heading must be rejected.'
    }
    Assert (!(Test-Path -LiteralPath $params.OutputDirectory)) 'Rejected publication must not create output.'
    Write-Host 'PASS: publication integrity, source preservation, no overwrite, approval input and invalid-corpus checks.'
}

function Test-AssistantSigningHandoff {
    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')
    $aliases = Get-JwtSigningEnvironment
    $previousAppData = $env:APPDATA
    $fixture = Join-Path $testWorkspace ('handoff-' + [char]0x1ec7)
    try {
        Clear-JwtSigningEnvironment
        $env:APPDATA = $fixture
        $null = [IO.Directory]::CreateDirectory($fixture)
        $envFile = Join-Path $fixture '.env'
        Copy-Item -LiteralPath (Join-Path $root '.env.example') -Destination $envFile
        $setup = Join-Path $PSScriptRoot 'setup-assistant-docker.ps1'
        if ([IO.Path]::DirectorySeparatorChar -eq '\') {
            $envHash = (Get-FileHash -LiteralPath $envFile).Hash
            $lockedEnv = [IO.File]::Open($envFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            $replaceRejected = $false
            try { & $setup -Initialize -EnvPath $envFile } catch {
                if ($_.InvocationInfo.Line -notmatch 'File\]::Replace') { throw }
                $replaceRejected = $true
            } finally { $lockedEnv.Dispose() }
            if (!$replaceRejected -or (Get-FileHash -LiteralPath $envFile).Hash -ne $envHash -or
                (Get-ChildItem -LiteralPath (Join-Path $fixture '.cache') -Filter 'assistant-docker*') -or
                (Get-ChildItem -LiteralPath $fixture -Filter '*.assistant-*.tmp' -Force)) {
                throw 'Failed initialization changed .env or left signing material behind.'
            }
        }
        & $setup -Initialize -EnvPath $envFile
        $keyFile = Join-Path $fixture '.cache/assistant-docker/user-private.pem'
        $source = 'UserService user-secrets:Jwt:PrivateKeyPem'
        $secretsFile = Join-Path $fixture 'secrets.json'
        $secretsJson = @{'Jwt:PrivateKeyPem' = [IO.File]::ReadAllText($keyFile)} | ConvertTo-Json
        [IO.File]::WriteAllText($secretsFile, $secretsJson)
        $inputs = @{EnvPath = $envFile; UserSecretsPath = $secretsFile}
        $inspect = Join-Path $PSScriptRoot 'check-assistant-setup.ps1'
        $before = (& $inspect -InspectSigning @inputs) | ConvertFrom-Json
        if ($before.DistinctPrivateKeyCount -ne 1 -or @($before.Sources | Where-Object Status -eq CONFIGURED).Count -ne 2) {
            throw 'Expected identical keys in the .env file source and user-secrets.'
        }
        $copy = Join-Path $fixture 'recovered.pem'
        & $setup -ExportSigning @inputs -Source $source -ConfirmExistingSource -OutputPath $copy
        [IO.File]::WriteAllText($envFile, 'SMARTPARK_JWT_PRIVATE_KEY_FILE=recovered.pem')
        $after = (& $inspect -InspectSigning @inputs) | ConvertFrom-Json
        if ($after.DistinctPrivateKeyCount -ne 1 -or @($after.Sources | Where-Object Status -eq CONFIGURED).Count -ne 2) {
            throw 'Recovered file did not match the original signing key.'
        }
        $env:Jwt__PrivateKeyFile = 'recovered.pem'
        $processCopy = Join-Path $fixture 'process-copy.pem'
        & $setup -ExportSigning @inputs -Source 'process:Jwt__PrivateKeyFile' -ConfirmExistingSource -OutputPath $processCopy
        if ((Get-FileHash -LiteralPath $processCopy).Hash -cne (Get-FileHash -LiteralPath $copy).Hash) {
            throw 'Export from the process FILE alias did not preserve the existing key.'
        }
        Write-Host "PASS: initialize, inspect and recover the existing key on PowerShell $($PSVersionTable.PSVersion)."
    } finally {
        $env:APPDATA = $previousAppData
        Restore-JwtSigningEnvironment $aliases
    }
}

function Test-AssistantDockerInitializer {
    $fixture = Join-Path $testWorkspace 'assistant-init-fixture'
    $null = New-Item -ItemType Directory -Path $fixture
    $envFile = Join-Path $fixture '.env'
    $initializer = Join-Path $PSScriptRoot 'setup-assistant-docker.ps1'
    $loader = Join-Path $PSScriptRoot 'load-env.ps1'
    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')

    [IO.File]::WriteAllText($envFile, "SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=replace-with-fixture`nKEEP_ME=fixture`n")
    $oldAppData = $env:APPDATA
    try {
        $env:APPDATA = $fixture
        $output = (& $initializer -Initialize -EnvPath $envFile 6>&1 | Out-String)
    } finally { $env:APPDATA = $oldAppData }
    $settings = & $loader -EnvPath $envFile -NoExport
    $snapshot = (Get-FileHash -LiteralPath $envFile).Hash
    $jwtFile = $settings['SMARTPARK_JWT_PRIVATE_KEY_FILE']
    $privateKeyHash = (Get-FileHash -LiteralPath (Join-Path $fixture $jwtFile)).Hash
    $null = & $initializer -Initialize -EnvPath $envFile
    if ((Get-FileHash -LiteralPath $envFile).Hash -ne $snapshot -or
        (Get-FileHash -LiteralPath (Join-Path $fixture $jwtFile)).Hash -ne $privateKeyHash -or
        $settings['KEEP_ME'] -cne 'fixture') { throw 'Rerun replaced existing data.' }
    if ([IO.Path]::IsPathRooted($jwtFile) -or
        (Read-JwtPrivateKeyFile $jwtFile $fixture) -notmatch '-----BEGIN PRIVATE KEY-----') { throw 'Root-relative signing file did not resolve.' }
    if ($output -match 'PRIVATE KEY' -or $output.Contains($settings['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD']) -or
        $output.Contains($settings['Assistant__Management__KeyEncryptionCertificatePassword'])) { throw 'Initializer disclosed signing or certificate credentials.' }

    $existingEnv = Join-Path $fixture 'existing.env'
    [IO.File]::WriteAllText($existingEnv, "SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=existing-fixture-source`n")
    $existingHash = (Get-FileHash -LiteralPath $existingEnv).Hash
    $rejected = $false
    try { & $initializer -Initialize -EnvPath $existingEnv }
    catch { $rejected = $_.Exception.Message -match 'Existing local configuration' }
    if (!$rejected -or (Get-FileHash -LiteralPath $existingEnv).Hash -ne $existingHash) { throw 'Existing signing configuration was accepted or changed.' }
    Write-Host 'PASS: local initializer is repeatable, keeps existing signing configuration, and resolves JWT paths from the environment directory.'


    # Existing User signing sources must remain unchanged and produce exactly the same public trust.
    $originalSigner = [Security.Cryptography.RSA]::Create(3072)
    $otherSigner = [Security.Cryptography.RSA]::Create(3072)
    try {
        $expectedPublic = $originalSigner.ExportSubjectPublicKeyInfoPem()
        $originalPem = $originalSigner.ExportPkcs8PrivateKeyPem()
        foreach ($mode in @('file', 'inline', 'base64', 'conflict', 'missing', 'placeholder')) {
            $caseDirectory = Join-Path $fixture $mode
            $null = New-Item -ItemType Directory -Path $caseDirectory
            $caseEnv = Join-Path $caseDirectory '.env'
            $source = switch ($mode) {
                'file' {
                    [IO.File]::WriteAllText((Join-Path $caseDirectory 'existing.pem'), $originalPem)
                    'SMARTPARK_JWT_PRIVATE_KEY_FILE=existing.pem'
                }
                'inline' { 'SMARTPARK_JWT_PRIVATE_KEY_PEM=' + $originalPem.Replace("`n", '\n') }
                'base64' { 'SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($originalPem)) }
                'conflict' {
                    [IO.File]::WriteAllText((Join-Path $caseDirectory 'existing.pem'), $originalPem)
                    'SMARTPARK_JWT_PRIVATE_KEY_FILE=existing.pem' + "`nSMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=" +
                        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($otherSigner.ExportPkcs8PrivateKeyPem()))
                }
                'placeholder' {
                    'SMARTPARK_JWT_PRIVATE_KEY_PEM=' + $originalPem.Replace("`n", '\n') +
                        "`nSMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=replace-with-base64-rsa-private-key"
                }
                default { 'KEEP_ME=fixture' }
            }
            [IO.File]::WriteAllText($caseEnv, $source + "`n")
            $before = (Get-FileHash -LiteralPath $caseEnv).Hash
            if ($mode -in @('conflict', 'missing', 'placeholder')) {
                $didReject = $false
                try { & $initializer -Initialize -EnvPath $caseEnv -UseExistingSigningKey }
                catch { $didReject = $true }
                if (!$didReject -or (Get-FileHash -LiteralPath $caseEnv).Hash -ne $before -or
                    (Test-Path (Join-Path $caseDirectory '.cache/assistant-docker'))) {
                    throw "Unsafe signing input mutated the fixture: $mode."
                }
                continue
            }
            Push-Location $root
            try { $caseOutput = (& $initializer -Initialize -EnvPath $caseEnv -UseExistingSigningKey 6>&1 | Out-String) }
            finally { Pop-Location }
            $after = & $loader -EnvPath $caseEnv -NoExport
            $publicPath = Join-Path $caseDirectory $after['Assistant__Jwt__PublicKeyFile']
            if ([IO.File]::ReadAllText($publicPath) -cne $expectedPublic) { throw "Existing signer changed: $mode." }
            foreach ($line in ($source -split "`n")) {
                if (![IO.File]::ReadAllLines($caseEnv).Contains($line)) { throw "Signing configuration rewritten: $mode." }
            }
            if (Test-Path (Join-Path $caseDirectory '.cache/assistant-docker/user-private.pem')) { throw 'Existing private key was copied.' }
            if ($mode -eq 'file' -and [IO.File]::ReadAllText((Join-Path $caseDirectory 'existing.pem')) -cne $originalPem) { throw 'Original key file changed.' }
            if ($caseOutput -match 'PRIVATE KEY' -or $caseOutput.Contains($after['Assistant__Management__KeyEncryptionCertificatePassword'])) { throw 'Existing-signing initializer leaked secret.' }
        }
    } finally { $originalSigner.Dispose(); $otherSigner.Dispose() }
    Write-Host 'PASS: existing FILE, PEM and Base64 signing sources preserved; invalid, mismatched and placeholder sources rejected before writes.'
}

function Test-AssistantSigningInspector {
    $fixture = Join-Path $testWorkspace 'signing-inspector'
    $null = New-Item -ItemType Directory -Path $fixture
    $inspector = Join-Path $PSScriptRoot 'check-assistant-setup.ps1'
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
        $raw = & $inspector -InspectSigning -EnvPath $envFile -UserSecretsPath $secretsFile
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
            $result = (& $inspector -InspectSigning -EnvPath $envFile -UserSecretsPath $secretsFile -UserJwksUri 'http://127.0.0.1:5035/.well-known/jwks.json') | ConvertFrom-Json
            if ($result.LiveJwksStatus -ne 'READ' -or @($result.Sources | Where-Object MatchesLiveJwks -eq $true).Count -ne 6) { throw 'Mocked public JWKS matching failed.' }
        } finally { Remove-Item Function:Invoke-RestMethod }
        $env:DOTNET_Jwt__PrivateKeyPem = $other.ExportPkcs8PrivateKeyPem()
        $result = (& $inspector -InspectSigning -EnvPath $envFile -UserSecretsPath $secretsFile) | ConvertFrom-Json
        if (!$result.MultipleDifferentKeys -or $result.DistinctPrivateKeyCount -ne 2) { throw 'Conflicting aliases were not identified.' }
        Clear-JwtSigningEnvironment
        [IO.File]::WriteAllText($envFile, "SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=invalid-fixture")
        [IO.File]::WriteAllText($secretsFile, (@{'Jwt:PrivateKeyPem'=$small.ExportPkcs8PrivateKeyPem()} | ConvertTo-Json))
        $result = (& $inspector -InspectSigning -EnvPath $envFile -UserSecretsPath $secretsFile) | ConvertFrom-Json
        if (@($result.Sources | Where-Object Status -eq INVALID).Count -ne 2) { throw 'Invalid Base64 or weak RSA key accepted.' }
        $rejected = $false
        try { $null = & $inspector -InspectSigning -EnvPath $envFile -UserSecretsPath $secretsFile -UserJwksUri 'https://example.com/jwks' } catch { $rejected = $true }
        if (!$rejected) { throw 'Remote JWKS endpoint accepted.' }
        [IO.File]::WriteAllText($secretsFile, 'not-json')
        $result = (& $inspector -InspectSigning -EnvPath $envFile -UserSecretsPath $secretsFile) | ConvertFrom-Json
        if (@($result.Sources | Where-Object Status -eq UNREADABLE).Count -ne 1) { throw 'Unreadable secrets file not reported.' }
        Write-Host 'PASS: matching/conflicting sources, process aliases, nested secrets, invalid/weak keys, no secret output, no source mutation, mocked JWKS match, remote JWKS rejected.'
    } finally {
        Restore-JwtSigningEnvironment $saved
        $rsa.Dispose(); $other.Dispose(); $small.Dispose()
    }
}

function Test-AssistantSigningExport {
    $fixture=Join-Path $testWorkspace 'signing-export'
    $null=New-Item -ItemType Directory -Path $fixture
    $exporter=Join-Path $PSScriptRoot 'setup-assistant-docker.ps1'
    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')
    $saved=Get-JwtSigningEnvironment
    $rsa=[Security.Cryptography.RSA]::Create(2048)
    $envFile=Join-Path $fixture '.env'
    $secretsFile=Join-Path $fixture 'secrets.json'
    function Assert-Rejected([hashtable]$Arguments) {
        $rejected=$false
        try { $null=& $exporter -ExportSigning @Arguments 6>&1 } catch { $rejected=$true }
        if (!$rejected) { throw 'Unsafe export was accepted.' }
    }
    try {
        Clear-JwtSigningEnvironment
        $pem=$rsa.ExportPkcs8PrivateKeyPem()
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pem))
        [IO.File]::WriteAllText($envFile, 'SMARTPARK_JWT_PRIVATE_KEY_PEM=')
        [IO.File]::WriteAllText($secretsFile, (@{'Jwt:PrivateKeyPem'=$pem; Jwt=@{PrivateKeyPemBase64=$encoded}} | ConvertTo-Json))
        $hash=(Get-FileHash $secretsFile).Hash
        $base=@{EnvPath=$envFile;UserSecretsPath=$secretsFile;OutputPath=(Join-Path $fixture 'preserved.pem'); Source='UserService user-secrets:Jwt:PrivateKeyPem'}
        Assert-Rejected $base
        $base.ConfirmExistingSource=$true
        $output=(& $exporter -ExportSigning @base 6>&1 | Out-String)
        if ($output.Contains($pem) -or $output.Contains($encoded) -or $output -match 'BEGIN PRIVATE KEY') { throw 'Export printed secret.' }
        $copy=[Security.Cryptography.RSA]::Create()
        try {
            $copy.ImportFromPem([IO.File]::ReadAllText($base.OutputPath))
            if ([Convert]::ToBase64String($copy.ExportSubjectPublicKeyInfo()) -cne [Convert]::ToBase64String($rsa.ExportSubjectPublicKeyInfo())) { throw 'Export changed key identity.' }
        } finally { $copy.Dispose() }
        Assert-Rejected $base
        $base.OutputPath=Join-Path $fixture 'nested.pem'
        $base.Source='UserService user-secrets:nested Jwt.PrivateKeyPemBase64'
        $null=& $exporter -ExportSigning @base 6>&1
        $env:Jwt__PrivateKeyPem=$pem
        $base.OutputPath=Join-Path $fixture 'process.pem'
        $base.Source='process:Jwt__PrivateKeyPem'
        $null=& $exporter -ExportSigning @base 6>&1
        if ($env:Jwt__PrivateKeyPem -cne $pem -or (Get-FileHash $secretsFile).Hash -cne $hash) { throw 'Export modified source.' }
        $base.OutputPath=Join-Path $root 'must-not-exist.pem'
        Assert-Rejected $base
        $base.OutputPath=Join-Path $fixture 'public-only.pem'
        $env:Jwt__PrivateKeyPem=$rsa.ExportSubjectPublicKeyInfoPem()
        Assert-Rejected $base
        if (Test-Path $base.OutputPath) { throw 'Rejected export wrote a file.' }
        Write-Host 'PASS: explicit confirmation, flat/nested/process recovery, same key identity, no secret output, source preservation, no overwrite, public-only and outside-cache rejection.'
    } finally {
        Restore-JwtSigningEnvironment $saved
        $rsa.Dispose()
    }
}

function Assert-LocalDockerEndpoint {
    if (!(Get-Command docker -CommandType Application -ErrorAction SilentlyContinue)) {
        throw 'Docker CLI is required for this suite.'
    }
    $endpoint = $env:DOCKER_HOST
    if ($env:DOCKER_CONTEXT -or !$endpoint) {
        $contextArgs = @('context', 'inspect', '--format', '{{.Endpoints.docker.Host}}')
        if ($env:DOCKER_CONTEXT) { $contextArgs += $env:DOCKER_CONTEXT }
        $endpoints = @(& docker @contextArgs)
        if ($LASTEXITCODE -ne 0 -or $endpoints.Count -ne 1) { throw 'Could not identify the Docker endpoint.' }
        $endpoint = $endpoints[0]
    }
    if ($endpoint -notmatch '^(npipe:////\./pipe/|unix:///)') { throw 'Use a local Docker endpoint for this suite.' }
}

function Test-AssistantDockerSetup {
    param([switch]$PreflightOnly)

    $project = 'assistantqa-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
    $scratch = Join-Path $testWorkspace "assistant fixture $project"
    $envPath = Join-Path $scratch 'test.env'
    $schema = $null
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $certificateRsa = [Security.Cryptography.RSA]::Create(2048)

    function Test-WindowsHost {
        if ($env:OS -eq 'Windows_NT' -or $PSVersionTable.PSEdition -eq 'Desktop') { return $true }
        try {
            return [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
                [Runtime.InteropServices.OSPlatform]::Windows)
        } catch {
            return $false
        }
    }

    $isWindowsHost = Test-WindowsHost

    function Get-PowerShellExecutable {
        $name = if ($PSVersionTable.PSEdition -eq 'Desktop') { 'powershell.exe' }
            elseif ($isWindowsHost) { 'pwsh.exe' } else { 'pwsh' }
        $executable = Join-Path $PSHOME $name
        if (!(Test-Path -LiteralPath $executable -PathType Leaf)) {
            throw "Cannot find $name in the current PowerShell installation."
        }
        return $executable
    }

    function ConvertTo-DerLength([int]$Length) {
        if ($Length -lt 128) { return [byte[]]@([byte]$Length) }
        $lengthBytes = [BitConverter]::GetBytes($Length)
        [Array]::Reverse($lengthBytes)
        $first = 0
        while ($first -lt $lengthBytes.Length -and $lengthBytes[$first] -eq 0) { $first++ }
        $significant = [byte[]]$lengthBytes[$first..($lengthBytes.Length - 1)]
        return [byte[]](@([byte](0x80 -bor $significant.Length)) + $significant)
    }

    function ConvertTo-DerInteger([byte[]]$Value) {
        $first = 0
        while ($first -lt ($Value.Length - 1) -and $Value[$first] -eq 0) { $first++ }
        $content = [byte[]]$Value[$first..($Value.Length - 1)]
        if (($content[0] -band 0x80) -ne 0) { $content = [byte[]](@([byte]0) + $content) }
        $header = [byte[]](@([byte]0x02) + (ConvertTo-DerLength $content.Length))
        return [byte[]]($header + $content)
    }

    function Convert-RsaPublicKeyToPem([Security.Cryptography.RSA]$Rsa) {
        # PKCS#1 RSA PUBLIC KEY is accepted by RSA.ImportFromPem on .NET 10 and
        # avoids requiring ExportSubjectPublicKeyInfoPem on Windows PowerShell 5.1.
        $parameters = $Rsa.ExportParameters($false)
        $modulus = [byte[]](ConvertTo-DerInteger $parameters.Modulus)
        $exponent = [byte[]](ConvertTo-DerInteger $parameters.Exponent)
        $body = [byte[]]($modulus + $exponent)
        $der = [byte[]](@([byte]0x30) + (ConvertTo-DerLength $body.Length) + $body)
        $base64 = [Convert]::ToBase64String($der)
        $lines = for ($offset = 0; $offset -lt $base64.Length; $offset += 64) {
            $count = [Math]::Min(64, $base64.Length - $offset)
            $base64.Substring($offset, $count)
        }
        return "-----BEGIN RSA PUBLIC KEY-----`r`n$($lines -join "`r`n")`r`n-----END RSA PUBLIC KEY-----`r`n"
    }

    function Get-FreePort {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        try { $listener.Start(); return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
    }
    function ConvertTo-Base64Url([byte[]]$Bytes) {
        return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+','-').Replace('/','_')
    }
    function Get-TestToken([string]$Role, [Guid]$Account = [Guid]::NewGuid(), [Guid]$User = [Guid]::NewGuid()) {
        $now = [DateTimeOffset]::UtcNow
        $payload = @{ iss='test-user'; aud='test-assistant'; sub=$User; account_id=$Account
            jti=[Guid]::NewGuid(); role=$Role; nbf=$now.AddMinutes(-1).ToUnixTimeSeconds(); exp=$now.AddMinutes(10).ToUnixTimeSeconds() }
        $header = ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"RS256","typ":"JWT"}'))
        $claims = ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Compress)))
        $signed = "$header.$claims"
        $signature = $rsa.SignData([Text.Encoding]::ASCII.GetBytes($signed), [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        return "$signed.$(ConvertTo-Base64Url $signature)"
    }
    function Invoke-Docker([string[]]$Arguments) {
        & docker @Arguments
        if ($LASTEXITCODE -ne 0) { throw 'Disposable Docker test command failed.' }
    }
    function Invoke-Assistant([string]$Path, [hashtable]$Headers, [hashtable]$Body) {
        $http = @{ Uri="$baseUrl/api/assistant/$Path"; TimeoutSec=10 }
        if ($Headers) { $http.Headers = $Headers }
        if ($null -ne $Body) {
            $http.Method = 'Post'
            $http.ContentType = 'application/json; charset=utf-8'
            $http.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8))
        }
        return (Invoke-RestMethod @http).data
    }
    function Get-HttpStatusCode([string]$Uri, [hashtable]$Headers) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -Headers $Headers -TimeoutSec 10
            return [int]$response.StatusCode
        } catch {
            if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
                return [int]$_.Exception.Response.StatusCode
            }
            throw
        }
    }
    function Invoke-Setup([string]$SettingsPath, [string]$ExpectedFailure, [switch]$ValidateOnly) {
        $shell = Get-PowerShellExecutable
        $arguments = @('-NoProfile')
        if ($isWindowsHost) { $arguments += @('-ExecutionPolicy','Bypass') }
        $runnerOptions = @('-NoBuild')
        if ($ValidateOnly) { $runnerOptions += '-ValidateOnly' }
        # Windows PowerShell promotes native stderr to a terminating error when
        # $ErrorActionPreference is Stop. Capture the child exit code explicitly so
        # expected validation failures can be asserted on both PowerShell editions.
        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & $shell @arguments -File $runner -EnvPath $SettingsPath -SchemaProject $project @runnerOptions 2>&1
            $exitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorAction
        }
        $failed = $exitCode -ne 0
        if ($ExpectedFailure) {
            if (!$failed -or ($output -join ' ') -notmatch $ExpectedFailure) {
                $output | ForEach-Object { Write-Host $_ }
                throw "Expected Assistant setup rejection matching: $ExpectedFailure."
            }
        } else {
            $output | ForEach-Object { Write-Host $_ }
            if ($failed) { throw 'Disposable Assistant setup failed.' }
        }
    }

    try {
        Assert-LocalDockerEndpoint
        foreach ($directory in @('FE','keys','knowledge','scripts/database/migrations','scripts/database/microservices')) {
            New-Item -ItemType Directory -Path (Join-Path $scratch $directory) -Force | Out-Null
        }
        foreach ($file in @('compose.assistant.yml','compose.schema-integration.yml','scripts/setup-assistant-docker.ps1',
            'scripts/apply-workflow-migrations.ps1','scripts/load-env.ps1','scripts/jwt-environment.ps1','scripts/discovery-docker.ps1',
            'scripts/database/microservices/06-iot-recognition-service-db.sql',
            'scripts/database/migrations/20261103_iot_assistant_management.sql','scripts/database/migrations/20261105_iot_assistant_s02.sql',
            'scripts/database/migrations/20261105_iot_assistant_s05.sql','scripts/database/migrations/20261110_iot_assistant_current_chat.sql')) {
            Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $scratch $file)
        }
        if (!$NoBuild -and !$PreflightOnly) {
            Invoke-Docker @('build','--file',(Join-Path $root 'docker/Dockerfile.assistant'),'--tag','smartpark/assistant:local',
                (Join-Path $root 'src/Services/IoTService'))
        }
        $publicKeyPath = Join-Path $scratch 'assistant-public.pem'
        [IO.File]::WriteAllText($publicKeyPath, (Convert-RsaPublicKeyToPem $rsa))
        $certPath = Join-Path $scratch 'assistant.pfx'
        $password = [Guid]::NewGuid().ToString('N')
        $certRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=assistant-docker-fixture',
            $certificateRsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $certificate = $certRequest.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1))
        [IO.File]::WriteAllBytes($certPath, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password))
        $md = Join-Path $scratch 'knowledge/smartpark-knowledge.md'
        [IO.File]::WriteAllText($md, @'
---
version: docker-fixture-v1
locale: vi
audience: [public, driver]
publication_state: published
review_status: approved
---
## [smartpark-overview] SmartPark

Từ khóa: smartpark; giới thiệu

### Nội dung

Hướng dẫn giả lập cho Docker.
'@)
        $publicationJson = @{ sha256=(Get-FileHash -LiteralPath $md).Hash; version='docker-fixture-v1'; approvedBy='synthetic-test-only';
            role='admin'; publishedAt=[DateTimeOffset]::UtcNow.AddMinutes(-2).ToString('O') } |
            ConvertTo-Json -Compress
        [IO.File]::WriteAllText((Join-Path $scratch 'knowledge/publication.json'), $publicationJson, [Text.UTF8Encoding]::new($false))

        $port = Get-FreePort
        do { $dbPort = Get-FreePort } while ($dbPort -eq $port)
        $baseUrl = "http://localhost:$port"
        @(
            "SMARTPARK_DEV_PASSWORD=$([Guid]::NewGuid().ToString('N'))"
            "SMARTPARK_RABBITMQ_PASSWORD=$([Guid]::NewGuid().ToString('N'))"
            "SMARTPARK_DB_IOT_PORT=$dbPort"
            "SMARTPARK_ASSISTANT_DOCKER_PORT=$port"
            'Assistant__Jwt__PublicKeyFile=assistant-public.pem'
            'Assistant__Jwt__Issuer=test-user'
            'Assistant__Jwt__Audience=test-assistant'
            'Assistant__Management__KeyDirectory=keys'
            "Assistant__Management__KeyEncryptionCertificatePath=$([IO.Path]::GetFileName($certPath))"
            "Assistant__Management__KeyEncryptionCertificatePassword=$password"
            'Assistant__Guest__KnowledgeDirectory=knowledge'
            'Assistant__Guest__ReleaseEnabled=true'
            'Assistant__Chat__ReleaseEnabled=true'
        ) | Set-Content -LiteralPath $envPath -Encoding utf8
        $frontendEnv = Join-Path $scratch 'FE/.env'
        'VITE_OTHER_SETTING=preserve' | Set-Content -LiteralPath $frontendEnv -Encoding utf8
        $runner = Join-Path $scratch 'scripts/setup-assistant-docker.ps1'
        $searchEnv = Join-Path $scratch 'search.env'
        $searchSettings = @(Get-Content -LiteralPath $envPath) + @(
            'Assistant__Reads__Search__Enabled=true'
            'Assistant__Reads__Search__MaxAgeSeconds=60'
            'Assistant__Reads__Search__ClockSkewSeconds=5'
        )
        ($searchSettings + 'Assistant__Reads__Search__Origin=https://localhost:5056') |
            Set-Content -LiteralPath $searchEnv -Encoding UTF8
        Invoke-Setup $searchEnv 'Invalid Docker Assistant origin: Assistant__Reads__Search__Origin' -ValidateOnly
        ($searchSettings + 'Assistant__Reads__Search__Origin=https://operations.example.test') |
            Set-Content -LiteralPath $searchEnv -Encoding UTF8
        Invoke-Setup $searchEnv -ValidateOnly
        if ($PreflightOnly) {
            Write-Host 'PASS: Docker Assistant preflight rejects loopback and accepts a non-loopback HTTPS origin.'
            return
        }
        $schema = @('compose','--project-name',$project,'--env-file',$envPath,'-f',(Join-Path $scratch 'compose.schema-integration.yml'))
        Invoke-Docker ($schema + @('up','-d','--wait','--wait-timeout','60','iot-db'))
        $schemaProbe = "SELECT xmin FROM assistant_management_state WHERE id = 1 AND to_regclass('ux_assistant_conversation_current') IS NOT NULL"
        $initialState = & docker @schema exec -T iot-db psql -U smartpark_iot -d smartpark_iot -Atc $schemaProbe
        if ($LASTEXITCODE -ne 0 -or $initialState -notmatch '^\d+$') { throw 'IoT Compose did not initialize the current Assistant schema.' }
        $wrongPassword = Join-Path $scratch 'wrong-password.env'
        (Get-Content -LiteralPath $envPath) -replace '^SMARTPARK_DEV_PASSWORD=.*$', 'SMARTPARK_DEV_PASSWORD=invalid-fixture-password' |
            Set-Content -LiteralPath $wrongPassword -Encoding utf8
        Invoke-Setup $wrongPassword 'password authentication failed'
        $stateAfterFailure = & docker @schema exec -T iot-db psql -U smartpark_iot -d smartpark_iot -Atc $schemaProbe
        if ($LASTEXITCODE -ne 0 -or $stateAfterFailure -cne $initialState) { throw 'Failed authentication changed the initial Assistant state.' }
        Invoke-Setup $envPath
        Invoke-Setup $envPath
        $stateAfterSetup = & docker @schema exec -T iot-db psql -U smartpark_iot -d smartpark_iot -Atc $schemaProbe
        if ($LASTEXITCODE -ne 0 -or $stateAfterSetup -cne $initialState) { throw 'Assistant setup replayed the fresh-volume migrations.' }
        if (!(Select-String -LiteralPath $frontendEnv -SimpleMatch "VITE_ASSISTANT_API_BASE_URL=$baseUrl" -Quiet) -or
            !(Select-String -LiteralPath $frontendEnv -SimpleMatch 'VITE_OTHER_SETTING=preserve' -Quiet)) {
            throw 'Assistant frontend URL or preservation check failed.'
        }
        $admin = @{ Authorization = "Bearer $(Get-TestToken 'admin')" }
        $account = [Guid]::NewGuid()
        $user = [Guid]::NewGuid()
        $driver = @{ Authorization = "Bearer $(Get-TestToken 'driver' $account $user)" }
        $owner = @{ Authorization = "Bearer $(Get-TestToken 'owner' $account $user)" }
        $settings = (Invoke-Assistant 'admin/settings' $admin).settings
        if ($settings.revision -ne 0 -or $settings.enabled) { throw 'Expected disabled Assistant Settings on fresh schema.' }
        if ((Get-HttpStatusCode "$baseUrl/api/assistant/admin/settings" $driver) -ne 403) {
            throw 'Driver unexpectedly accessed Assistant Settings.'
        }

        function Save-Settings([hashtable]$Mutation) {
            $Mutation.mutationId = [Guid]::NewGuid()
            $Mutation.expectedRevision = $settings.revision
            return (Invoke-Assistant 'admin/mutations' $admin $Mutation).settings
        }
        $providers = $settings.providers | ForEach-Object {
            @{ id=$_.id; model=$_.model; enabled=$_.id -eq 'openai'; priority=$_.priority }
        }
        $settings = Save-Settings @{ action='save-config'; enabled=$false; providers=@($providers); maintenanceMessage='Docker fixture' }
        $model = 'gpt-5.4-mini'
        $settings = Save-Settings @{ action='add-configuration'; provider='openai'; label='Docker fixture'; secret='synthetic-only';
            model=$model; priority=1 }
        $key = $settings.configurations[0].id
        $settings = Save-Settings @{ action='set-configuration'; keyId=$key; label='Docker fixture'; model=$model; priority=1; enabled=$true }
        $settings = Save-Settings @{ action='set-enabled'; enabled=$true }
        # Keep global enabled with no active credential. No test request is sent to a provider.
        $settings = Save-Settings @{ action='set-configuration'; keyId=$key; label='Docker fixture'; model=$model; priority=1; enabled=$false }
        if (!(Invoke-Assistant 'status').guestFaq) { throw 'Guest FAQ is not ready on the Docker Assistant.' }
        $faq = Invoke-Assistant 'guest/messages' @{} @{ requestId=[Guid]::NewGuid(); message='SmartPark là gì?'; locale='vi' }
        if ($faq.mode -ne 'knowledge' -or $faq.sources[0].version -ne 'docker-fixture-v1') {
            throw 'Docker Guest FAQ did not use the fixture publication.'
        }
        $chat = Invoke-Assistant 'conversations' $driver @{ clientConversationId=[Guid]::NewGuid(); locale='vi' }
        $ownerChat = Invoke-Assistant 'conversations' $owner @{ clientConversationId=[Guid]::NewGuid(); locale='vi' }
        $adminChat = Invoke-Assistant 'conversations' $admin @{ clientConversationId=[Guid]::NewGuid(); locale='vi' }
        $driverPath = "conversations/$($chat.id)"
        if ((Get-HttpStatusCode "$baseUrl/api/assistant/$driverPath/messages" $owner) -ne 404) {
            throw 'Owner can access Driver history on the same Account.'
        }
        $message = Invoke-Assistant "conversations/$($chat.id)/messages" $driver @{
            clientMessageId=[Guid]::NewGuid(); expectedRevision=0; message='SmartPark'
        }
        if ($message.state -ne 'completed' -or $message.answer.mode -ne 'knowledge') { throw 'Docker Driver FAQ failed.' }
        # A missing Current Chat marker in a populated schema must stop setup.
        Invoke-Docker ($schema + @('exec','-T','iot-db','psql','-U','smartpark_iot','-d','smartpark_iot','-v','ON_ERROR_STOP=1',
            '-c','ALTER INDEX ux_assistant_conversation_current RENAME TO fixture_current_chat_index'))
        Invoke-Setup $envPath 'Assistant current chat index is missing'
        Invoke-Docker ($schema + @('exec','-T','iot-db','psql','-U','smartpark_iot','-d','smartpark_iot','-v','ON_ERROR_STOP=1',
            '-c','ALTER INDEX fixture_current_chat_index RENAME TO ux_assistant_conversation_current'))
        Invoke-Setup $envPath 'Existing Assistant data needs its original certificate and keyring'
        Add-Content -LiteralPath $envPath -Value 'SMARTPARK_ASSISTANT_DOCKER_USE_EXISTING_KEYRING=true'
        Invoke-Setup $envPath
        $driver = @{ Authorization = "Bearer $(Get-TestToken 'driver' $account $user)" }
        $history = Invoke-Assistant "$driverPath/messages" $driver
        if ($history.items.Count -ne 2) { throw 'Docker rerun did not preserve Driver history.' }
        if (!(Invoke-Assistant "$driverPath/reset" $driver @{}).reset) { throw 'Reset was not acknowledged.' }
        $oldHistoryStatus = Get-HttpStatusCode "$baseUrl/api/assistant/$driverPath/messages" $driver
        if ($oldHistoryStatus -ne 404 -or (Invoke-Assistant 'conversations' $driver).items.Count -ne 0) {
            throw 'Reset did not hide Driver history.'
        }
        $replacement = Invoke-Assistant 'conversations' $driver @{ clientConversationId=[Guid]::NewGuid(); locale='vi' }
        if ($replacement.id -eq $chat.id -or (Invoke-Assistant "conversations/$($replacement.id)/messages" $driver).items.Count -ne 0) {
            throw 'The replacement conversation is not empty.'
        }
        if (!(Invoke-Assistant "$driverPath/reset" $driver @{}).reset -or
            (Invoke-Assistant 'conversations' $driver).items[0].id -ne $replacement.id -or
            (Invoke-Assistant 'conversations' $owner).items[0].id -ne $ownerChat.id -or
            (Invoke-Assistant 'conversations' $admin).items[0].id -ne $adminChat.id) {
            throw 'Reset retry affected a current conversation.'
        }
        Write-Host 'PASS: IoT Compose init, Docker setup replay, Admin JWT, Guest FAQ, role isolation, resumed Driver history and reset.'
    } catch {
        if ($schema) { & docker @schema logs --tail 40 iot-db }
        throw
    } finally {
        if ($certificate) { $certificate.Dispose() }
        $certificateRsa.Dispose()
        $rsa.Dispose()
        if ($schema) {
            $dockerIds = @(docker ps -aq --filter "label=com.docker.compose.project=$project-assistant")
            if ($LASTEXITCODE -eq 0 -and $dockerIds.Count) { docker rm -f @dockerIds | Out-Null }
            docker @schema down --volumes | Out-Null
        }
    }
}

function Test-AssistantPostgres {
    Assert-LocalDockerEndpoint
    $nonce = [Guid]::NewGuid().ToString('N')
    $database = "assistant_test_$nonce"
    $image = 'postgres:16-alpine'
    $oldPassword = $env:POSTGRES_PASSWORD
    $oldPort = $env:SMARTPARK_TEST_ASSISTANT_PORT
    $oldNonce = $env:SMARTPARK_TEST_ASSISTANT_NONCE
    $oldTestPassword = $env:SMARTPARK_TEST_ASSISTANT_PASSWORD
    $container = $null
    $baseSchema = 'scripts/database/microservices/06-iot-recognition-service-db.sql'
    $assistantMigrations = @(
        'scripts/database/migrations/20261103_iot_assistant_management.sql'
        'scripts/database/migrations/20261105_iot_assistant_s02.sql'
        'scripts/database/migrations/20261105_iot_assistant_s05.sql',
        'scripts/database/migrations/20261110_iot_assistant_current_chat.sql'
    )

    function Invoke-TestSchema([string[]]$SqlFiles) {
        $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        foreach ($relativePath in $SqlFiles) {
            Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root $relativePath) |
                docker exec -i $container psql -X -q -v ON_ERROR_STOP=1 -U postgres -d $database
            if ($LASTEXITCODE -ne 0) { throw "Disposable database setup failed at $relativePath." }
        }
    }

    function Get-AssistantDataFingerprint([string]$DatabaseName) {
        $sql = @'
SELECT md5(string_agg(payload, E'\n' ORDER BY payload COLLATE "C")) FROM (
    SELECT 'settings:' || to_jsonb(row_data)::text AS payload FROM assistant_management_state row_data
    UNION ALL SELECT 'credential:' || to_jsonb(row_data)::text FROM assistant_management_credentials row_data
    UNION ALL SELECT 'mutation:' || to_jsonb(row_data)::text FROM assistant_management_receipts row_data
    UNION ALL SELECT 'diagnostic:' || to_jsonb(row_data)::text FROM assistant_connection_tests row_data
    UNION ALL SELECT 'conversation:' || to_jsonb(row_data)::text FROM assistant_conversations row_data
    UNION ALL SELECT 'message:' || to_jsonb(row_data)::text FROM assistant_messages row_data
    UNION ALL SELECT 'request:' || to_jsonb(row_data)::text FROM assistant_requests row_data
    UNION ALL SELECT 'tool:' || to_jsonb(row_data)::text FROM assistant_tool_runs row_data
) fingerprint;
'@
        # Piping the SQL preserves quoted PostgreSQL identifiers under Windows PowerShell 5.1.
        $fingerprint = $sql | docker exec -i $container psql -X -A -t -q -v ON_ERROR_STOP=1 -U postgres -d $DatabaseName
        if ($LASTEXITCODE -ne 0 -or $fingerprint -notmatch '^[a-f0-9]{32}$') { throw 'Could not verify disposable Assistant rows.' }
        return $fingerprint
    }

    try {
        $env:POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
        $container = docker run --rm --detach --pull never --name $database --publish '127.0.0.1::5432' `
            --env POSTGRES_PASSWORD --env "POSTGRES_DB=$database" $image
        if ($LASTEXITCODE -ne 0 -or $container -notmatch '^[a-f0-9]{64}$') {
            throw "Failed to start the disposable PostgreSQL container. The $image image must be available locally."
        }
        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            docker exec $container pg_isready -h 127.0.0.1 -U postgres -d $database | Out-Null
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
            Start-Sleep -Seconds 1
        }
        if (!$ready) { throw 'Disposable PostgreSQL did not start within the test timeout.' }

        Invoke-TestSchema (@($baseSchema) + $assistantMigrations[0..2])
        $legacy = @'
INSERT INTO assistant_conversations
    (id,user_id,owner_account_id,client_conversation_id,title,locale,created_at,updated_at,last_message_at,expires_at,retention_hold)
SELECT id::uuid,'10000000-0000-4000-8000-000000000011','10000000-0000-4000-8000-000000000012',id::uuid,
    title,'vi',statement_timestamp()-interval '10 days',statement_timestamp(),
    statement_timestamp()-age,expires_at,held
FROM (VALUES
    ('10000000-0000-4000-8000-000000000001','Latest eligible',interval '1 day','2099-01-01 UTC'::timestamptz,false),
    ('10000000-0000-4000-8000-000000000002','Older eligible',interval '2 days','2099-01-02 UTC'::timestamptz,false),
    ('10000000-0000-4000-8000-000000000003','Expired held',interval '1 hour','2000-01-01 UTC'::timestamptz,true)
) AS fixture(id,title,age,expires_at,held);
INSERT INTO assistant_messages (id,conversation_id,sender_role,content,created_at,sequence,client_message_id,response)
VALUES ('10000000-0000-4000-8000-000000000004','10000000-0000-4000-8000-000000000002','USER',
    'Legacy fixture',statement_timestamp(),1,'10000000-0000-4000-8000-000000000005','{}');
'@
        $legacy | docker exec -i $container psql -X -q -v ON_ERROR_STOP=1 -U postgres -d $database
        if ($LASTEXITCODE -ne 0) { throw 'Could not seed the disposable S05 upgrade fixture.' }
        Invoke-TestSchema @($assistantMigrations[-1])
        $upgradeCheck = @'
SELECT
    (SELECT chat_role='driver' AND expires_at IS NULL FROM assistant_conversations WHERE title='Latest eligible')
    AND (SELECT chat_role IS NULL AND expires_at='2099-01-02 UTC' FROM assistant_conversations WHERE title='Older eligible')
    AND (SELECT chat_role IS NULL AND retention_hold AND expires_at='2000-01-01 UTC' FROM assistant_conversations WHERE title='Expired held')
    AND EXISTS (SELECT 1 FROM assistant_messages WHERE content='Legacy fixture');
'@
        $upgraded = $upgradeCheck | docker exec -i $container psql -X -qAt -v ON_ERROR_STOP=1 -U postgres -d $database
        if ($LASTEXITCODE -ne 0 -or $upgraded -cne 't') { throw 'The current-chat migration did not preserve the expected S05 data.' }
        Write-Host 'PASS: populated S05 upgrade keeps the latest eligible chat, legacy deadlines, hold and message.'

        $mapping = @(docker port $container '5432/tcp')
        if ($LASTEXITCODE -ne 0 -or $mapping.Count -ne 1 -or $mapping[0] -notmatch '^127\.0\.0\.1:(\d+)$') {
            throw 'Expected one loopback PostgreSQL port.'
        }
        $env:SMARTPARK_TEST_ASSISTANT_PORT = $Matches[1]
        $env:SMARTPARK_TEST_ASSISTANT_NONCE = $nonce
        $env:SMARTPARK_TEST_ASSISTANT_PASSWORD = $env:POSTGRES_PASSWORD
        $project = Join-Path $root 'tests/Services/SmartParking.IoTService.Tests/SmartParking.IoTService.Tests.csproj'
        dotnet test $project -c Release --nologo --no-restore --filter 'FullyQualifiedName~AssistantPostgres'
        if ($LASTEXITCODE -ne 0) { throw 'Assistant PostgreSQL integration tests failed.' }

        $before = Get-AssistantDataFingerprint $database
        $restoreDatabase = "assistant_restore_$nonce"
        $dump = '/tmp/assistant-test.dump'
        docker exec $container pg_dump -Fc -U postgres -d $database -f $dump
        if ($LASTEXITCODE -ne 0) { throw 'Backup of the disposable PostgreSQL fixture failed.' }
        Invoke-TestSchema @($assistantMigrations[-1])
        if ((Get-AssistantDataFingerprint $database) -ne $before) {
            throw 'Reapplying Assistant migrations modified the test Settings or conversation data.'
        }
        docker exec $container createdb -U postgres $restoreDatabase
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the disposable restore database.' }
        docker exec $container pg_restore --exit-on-error -U postgres -d $restoreDatabase $dump
        if ($LASTEXITCODE -ne 0 -or (Get-AssistantDataFingerprint $restoreDatabase) -ne $before) {
            throw 'Restored Assistant data did not match the backup.'
        }
        Write-Host 'PASS: Assistant migration replay preserved data and the backup restored into a separate disposable database.'
    } finally {
        $env:POSTGRES_PASSWORD = $oldPassword
        $env:SMARTPARK_TEST_ASSISTANT_PORT = $oldPort
        $env:SMARTPARK_TEST_ASSISTANT_NONCE = $oldNonce
        $env:SMARTPARK_TEST_ASSISTANT_PASSWORD = $oldTestPassword
        if ($container -match '^[a-f0-9]{64}$') {
            docker stop $container | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Could not stop the disposable test container $database." }
        }
    }
}

if ($Suite -eq 'Postgres') {
    Test-AssistantPostgres
    return
}

$cache = [IO.Path]::GetFullPath((Join-Path $root '.cache'))
$testWorkspace = Join-Path $cache ('assistant-script-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testWorkspace
try {
    if ($Suite -in @('Docker', 'DockerPreflight')) {
        Test-AssistantDockerSetup -PreflightOnly:($Suite -eq 'DockerPreflight')
        return
    }

    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')
    $previousSigning = Get-JwtSigningEnvironment
    $previousAppData = $env:APPDATA
    try {
        Clear-JwtSigningEnvironment
        $env:APPDATA = $testWorkspace
        Test-AssistantReadiness
        Test-AssistantHostRunner
        Test-AssistantMigrationPlan
        Test-AssistantKnowledgePublication
        Test-AssistantSigningHandoff
        if ($PSVersionTable.PSVersion.Major -ge 7) {
            Test-AssistantDockerInitializer
            Test-AssistantSigningInspector
            Test-AssistantSigningExport
        } else {
            Write-Host 'SKIP: run the offline suite in PowerShell 7 for the initializer and detailed signing fixtures.'
        }
    } finally {
        $env:APPDATA = $previousAppData
        Restore-JwtSigningEnvironment $previousSigning
    }
} finally {
    $resolved = (Resolve-Path -LiteralPath $testWorkspace).Path
    $directory = Get-Item -LiteralPath $resolved -Force
    if ($directory.Parent.FullName -ne $cache -or $directory.Name -notmatch '^assistant-script-test-[a-f0-9]{32}$' -or
        ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Unexpected Assistant fixture cleanup path.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
