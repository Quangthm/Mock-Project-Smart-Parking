# Disposable Docker projects and locally signed test JWTs.
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.
param([switch]$NoBuild, [switch]$PreflightOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = 'assistantqa-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$scratch = Join-Path $root ".cache/assistant fixture $project"
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
    $name = if ($isWindowsHost) { 'powershell.exe' } else { 'pwsh' }
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if (!$command) {
        throw "Cannot find $name. Run this script from an installed PowerShell host."
    }
    return $command.Source
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
        $http.ContentType = 'application/json'
        $http.Body = $Body | ConvertTo-Json -Depth 8
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
