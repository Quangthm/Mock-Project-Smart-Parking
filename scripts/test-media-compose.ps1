# Read-only Compose configuration verification. No daemon or actual credentials required.
param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $taskRoot ('.cache/media-compose-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$fixtureEnv = Join-Path $fixtureRoot 'fixture.env'
$fixtureLines = @(
    'SMARTPARK_DEV_PASSWORD=synthetic-db-fixture',
    'SMARTPARK_SERVICE_KEY=synthetic-service-key-at-least-32-characters',
    'SMARTPARK_ADMIN_PASSWORD=Synthetic@1',
    'SMARTPARK_OWNER_PASSWORD=Synthetic@1',
    'SMARTPARK_OPERATOR_PASSWORD=Synthetic@1',
    'SMARTPARK_RABBITMQ_PASSWORD=synthetic-broker-fixture',
    'SONAR_DB_PASSWORD=synthetic-sonar-fixture',
    'SMARTPARK_USER_MEDIA_ENABLED=true',
    'SMARTPARK_CLOUDINARY_CLOUD_NAME=synthetic-cloud-name',
    'SMARTPARK_CLOUDINARY_API_KEY=synthetic-cloud-key',
    'SMARTPARK_CLOUDINARY_API_SECRET=synthetic-cloud-secret',
    'SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=synthetic-signing-fixture'
)
[IO.File]::WriteAllLines($fixtureEnv, $fixtureLines)
# Shell environment overrides --env-file; isolate it so personal values cannot
# enter the rendered configuration. Restore every variable afterward.
$savedEnvironment = @{}
foreach ($entry in [Environment]::GetEnvironmentVariables('Process').GetEnumerator()) {
    if ($entry.Key -match '^(SMARTPARK_|SONAR_|VITE_|Messaging__|COMPOSE_)') {
        $savedEnvironment[$entry.Key] = $entry.Value
        [Environment]::SetEnvironmentVariable($entry.Key, $null, 'Process')
    }
}
function Assert-MediaCompose($condition, $message) { if (!$condition) { throw $message } }
try {
    foreach ($file in @('compose.app.yml','compose.ai.yml','compose.schema-integration.yml')) {
        $configText = & docker compose --project-name smartpark-media-fixture --env-file $fixtureEnv -f (Join-Path $taskRoot $file) config --format json 2>$null
        if ($LASTEXITCODE -ne 0) { throw "Compose validation failed for $file; rendered configuration was suppressed." }
        $config = ($configText -join "`n") | ConvertFrom-Json
        foreach ($service in $config.services.PSObject.Properties) {
            $environment = $service.Value.environment
            $serializedEnvironment = $environment | ConvertTo-Json -Compress
            $dotnetMedia = $file -eq 'compose.app.yml' -and $service.Name -in @('user-service','facility-service','operation-service','payment-service')
            $pythonMedia = $file -eq 'compose.ai.yml' -and $service.Name -in @('ai-rest','ai-live')
            if ($dotnetMedia -or $pythonMedia) {
                $expected = if ($dotnetMedia) { @{'MediaStorage__Cloudinary__CloudName'='synthetic-cloud-name'; 'MediaStorage__Cloudinary__ApiKey'='synthetic-cloud-key'; 'MediaStorage__Cloudinary__ApiSecret'='synthetic-cloud-secret'} } else { @{'SMARTPARK_CLOUDINARY_CLOUD_NAME'='synthetic-cloud-name'; 'SMARTPARK_CLOUDINARY_API_KEY'='synthetic-cloud-key'; 'SMARTPARK_CLOUDINARY_API_SECRET'='synthetic-cloud-secret'} }
                foreach ($pair in $expected.GetEnumerator()) { Assert-MediaCompose ($environment.($pair.Key) -ceq $pair.Value) "Missing media mapping in $($service.Name)." }
            } else { Assert-MediaCompose ($serializedEnvironment -notmatch 'synthetic-cloud-|Cloudinary|CLOUDINARY') "Media credentials reached $($service.Name)." }
            if ($service.Name -eq 'user-service') {
                Assert-MediaCompose ($environment.MediaStorage__Enabled -eq 'true') 'UserService media switch missing.'
                Assert-MediaCompose ($environment.Jwt__PrivateKeyPemBase64 -eq 'synthetic-signing-fixture') 'User signing configuration missing.'
            } else {
                Assert-MediaCompose ($serializedEnvironment -notmatch 'MediaStorage__Enabled|SMARTPARK_USER_MEDIA_ENABLED|synthetic-signing-fixture') "Owner-only configuration reached $($service.Name)."
            }
            if ($service.Name -eq 'ai-live') { Assert-MediaCompose ($environment.IOT_DATABASE_HOST -eq 'iot-db' -and $environment.IOT_DATABASE_NAME -eq 'smartpark_iot') 'AI live database environment lost during merge.' }
        }
    }
    Write-Output 'PASS: effective Compose media mappings, AI database merge, frontend/infrastructure exclusion and JWT/switch isolation.'
} finally {
    foreach ($entry in $savedEnvironment.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process') }
}
