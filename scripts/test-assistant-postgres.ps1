# Runs only against a fresh PostgreSQL container, with an automatically allocated loopback port.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
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
    'scripts/database/migrations/20261105_iot_assistant_s05.sql'
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

    Invoke-TestSchema (@($baseSchema) + $assistantMigrations)

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
    Invoke-TestSchema $assistantMigrations
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
