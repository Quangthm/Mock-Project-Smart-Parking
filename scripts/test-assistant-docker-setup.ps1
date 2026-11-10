#Requires -Version 7.0
# Disposable Docker projects and locally signed test JWTs.
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = 'assistantqa-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$scratch = Join-Path $root ".cache/$project"
$envPath = Join-Path $scratch 'test.env'
$rsa = [Security.Cryptography.RSA]::Create(2048)
$certificateRsa = [Security.Cryptography.RSA]::Create(2048)

function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}
function ConvertTo-Base64Url([byte[]]$Bytes) {
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+','-').Replace('/','_')
}
function Get-TestToken([string]$Role) {
    $now = [DateTimeOffset]::UtcNow
    $payload = @{ iss='test-user'; aud='test-assistant'; sub=[Guid]::NewGuid(); account_id=[Guid]::NewGuid()
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
    if ($Body) {
        $http.Method = 'Post'
        $http.ContentType = 'application/json'
        $http.Body = $Body | ConvertTo-Json -Depth 8
    }
    return (Invoke-RestMethod @http).data
}
function Invoke-Setup([string]$SettingsPath, [string]$ExpectedFailure) {
    $shell = if ($IsWindows) { 'powershell.exe' } else { 'pwsh' }
    $arguments = @('-NoProfile')
    if ($IsWindows) { $arguments += @('-ExecutionPolicy','Bypass') }
    $output = & $shell @arguments -File $runner -EnvPath $SettingsPath -SchemaProject $project -NoBuild 2>&1
    $failed = $LASTEXITCODE -ne 0
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
    $contextArgs = @('context','inspect','--format','{{.Endpoints.docker.Host}}')
    if ($env:DOCKER_CONTEXT) { $contextArgs += $env:DOCKER_CONTEXT }
    $endpoint = $env:DOCKER_HOST
    if ($env:DOCKER_CONTEXT -or !$endpoint) { $endpoint = & docker @contextArgs }
    if ($endpoint -notmatch '^(npipe:////\./pipe/|unix:///)') { throw 'Use a local Docker endpoint for this fixture.' }
    foreach ($directory in @('FE','keys','knowledge','scripts/database/migrations','scripts/database/microservices')) {
        New-Item -ItemType Directory -Path (Join-Path $scratch $directory) -Force | Out-Null
    }
    foreach ($file in @('compose.assistant.yml','compose.schema-integration.yml','scripts/setup-assistant-docker.ps1',
        'scripts/apply-workflow-migrations.ps1','scripts/load-env.ps1','scripts/jwt-environment.ps1',
        'scripts/database/microservices/06-iot-recognition-service-db.sql',
        'scripts/database/migrations/20261103_iot_assistant_management.sql','scripts/database/migrations/20261105_iot_assistant_s02.sql',
        'scripts/database/migrations/20261105_iot_assistant_s05.sql')) {
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $scratch $file)
    }
    if (!$NoBuild) {
        Invoke-Docker @('build','--file',(Join-Path $root 'docker/Dockerfile.assistant'),'--tag','smartpark/assistant:local',
            (Join-Path $root 'src/Services/IoTService'))
    }
    $publicKeyPath = Join-Path $scratch 'assistant-public.pem'
    [IO.File]::WriteAllText($publicKeyPath, $rsa.ExportSubjectPublicKeyInfoPem())
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
    @{ sha256=(Get-FileHash -LiteralPath $md).Hash; version='docker-fixture-v1'; approvedBy='synthetic-test-only';
        role='admin'; publishedAt=[DateTimeOffset]::UtcNow.AddMinutes(-2).ToString('O') } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scratch 'knowledge/publication.json') -Encoding utf8

    $port = Get-FreePort
    do { $dbPort = Get-FreePort } while ($dbPort -eq $port)
    $baseUrl = "http://localhost:$port"
    @(
        "SMARTPARK_DEV_PASSWORD=$([Guid]::NewGuid().ToString('N'))"
        "SMARTPARK_RABBITMQ_PASSWORD=$([Guid]::NewGuid().ToString('N'))"
        "SMARTPARK_DB_IOT_PORT=$dbPort"
        "SMARTPARK_ASSISTANT_DOCKER_PORT=$port"
        "Assistant__Jwt__PublicKeyFile='$publicKeyPath'"
        'Assistant__Jwt__Issuer=test-user'
        'Assistant__Jwt__Audience=test-assistant'
        "Assistant__Management__KeyDirectory='$(Join-Path $scratch 'keys')'"
        "Assistant__Management__KeyEncryptionCertificatePath='$certPath'"
        "Assistant__Management__KeyEncryptionCertificatePassword=$password"
        "Assistant__Guest__KnowledgeDirectory='$(Join-Path $scratch 'knowledge')'"
        'Assistant__Guest__ReleaseEnabled=true'
        'Assistant__Chat__ReleaseEnabled=true'
    ) | Set-Content -LiteralPath $envPath -Encoding utf8
    $frontendEnv = Join-Path $scratch 'FE/.env'
    'VITE_OTHER_SETTING=preserve' | Set-Content -LiteralPath $frontendEnv -Encoding utf8
    $schema = @('compose','--project-name',$project,'--env-file',$envPath,'-f',(Join-Path $scratch 'compose.schema-integration.yml'))
    $runner = Join-Path $scratch 'scripts/setup-assistant-docker.ps1'
    Invoke-Docker ($schema + @('up','-d','--wait','--wait-timeout','60','iot-db'))
    $wrongPassword = Join-Path $scratch 'wrong-password.env'
    (Get-Content -LiteralPath $envPath) -replace '^SMARTPARK_DEV_PASSWORD=.*$', 'SMARTPARK_DEV_PASSWORD=invalid-fixture-password' |
        Set-Content -LiteralPath $wrongPassword -Encoding utf8
    Invoke-Setup $wrongPassword 'password authentication failed'
    $migration = & docker @schema exec -T iot-db psql -U smartpark_iot -d smartpark_iot -Atc "SELECT to_regclass('assistant_management_state') IS NULL"
    if ($LASTEXITCODE -ne 0 -or ($migration -join '').Trim() -ne 't') { throw 'Setup wrote SQL after authentication failure.' }
    Invoke-Setup $envPath
    Invoke-Setup $envPath
    if (!(Select-String -LiteralPath $frontendEnv -SimpleMatch "VITE_ASSISTANT_API_BASE_URL=$baseUrl" -Quiet) -or
        !(Select-String -LiteralPath $frontendEnv -SimpleMatch 'VITE_OTHER_SETTING=preserve' -Quiet)) {
        throw 'Assistant frontend URL or preservation check failed.'
    }
    $admin = @{ Authorization = "Bearer $(Get-TestToken 'admin')" }
    $driver = @{ Authorization = "Bearer $(Get-TestToken 'driver')" }
    $settings = (Invoke-Assistant 'admin/settings' $admin).settings
    if ($settings.revision -ne 0 -or $settings.enabled) { throw 'Expected disabled Assistant Settings on fresh schema.' }
    $denied = Invoke-WebRequest "$baseUrl/api/assistant/admin/settings" -Headers $driver -TimeoutSec 10 -SkipHttpErrorCheck
    if ($denied.StatusCode -ne 403) { throw 'Driver unexpectedly accessed Assistant Settings.' }

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
    $message = Invoke-Assistant "conversations/$($chat.id)/messages" $driver @{
        clientMessageId=[Guid]::NewGuid(); expectedRevision=0; message='SmartPark'
    }
    if ($message.state -ne 'completed' -or $message.answer.mode -ne 'knowledge') { throw 'Docker Driver FAQ failed.' }
    Invoke-Setup $envPath 'Existing Assistant data needs its original certificate and keyring'
    Add-Content -LiteralPath $envPath -Value 'SMARTPARK_ASSISTANT_DOCKER_USE_EXISTING_KEYRING=true'
    Invoke-Setup $envPath
    $history = Invoke-Assistant "conversations/$($chat.id)/messages" $driver
    if ($history.items.Count -ne 2) { throw 'Docker rerun did not preserve Driver history.' }
    Write-Host 'PASS: Docker setup, password guard, keyring opt-in, Admin JWT, Guest FAQ and Driver history after rerun.'
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
