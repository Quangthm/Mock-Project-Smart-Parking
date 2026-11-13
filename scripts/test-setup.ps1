# Run in Windows PowerShell 5.1. All writes go to a scratch checkout; CLI/secrets are doubles.
param()
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $sourceRoot ('.cache/sp-setup-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
$global:setupTestSecretMaps = @{}
$global:setupTestSecretWrites = 0
$global:setupTestDockerCalls = @()
$global:setupTestOldVolume = $false
function Assert-Setup($condition, $message) { if (!$condition) { throw $message } }
function Test-Path {
    param([Parameter(Position=0)][string]$Path, [string]$LiteralPath, [string]$PathType)
    $probe = if ($LiteralPath) { $LiteralPath } else { $Path }
    if ($probe -match 'smartpark-setup-test-(User|Parking|Reservation|Payment|Notification)[/\\]secrets.json$') { return $global:setupTestSecretMaps.ContainsKey($matches[1]) }
    Microsoft.PowerShell.Management\Test-Path @PSBoundParameters
}
function Get-Content {
    param([Parameter(Position=0)][string]$Path, [string]$LiteralPath, [switch]$Raw, [string]$Encoding)
    $probe = if ($LiteralPath) { $LiteralPath } else { $Path }
    if ($probe -match 'smartpark-setup-test-(User|Parking|Reservation|Payment|Notification)[/\\]secrets.json$') { return ($global:setupTestSecretMaps[$matches[1]] | ConvertTo-Json) }
    Microsoft.PowerShell.Management\Get-Content @PSBoundParameters
}
function dotnet {
    if ($args[0] -eq 'run') {
        $global:runnerEnvironment = @{}
        foreach ($entry in [Environment]::GetEnvironmentVariables('Process').GetEnumerator()) { $global:runnerEnvironment[$entry.Key] = $entry.Value }
        if (($args -join ' ') -notmatch 'UserService.API') {
            $childScript = '@([Environment]::GetEnvironmentVariables().Keys | Where-Object { $_ -match "(?i)JWT.*(PRIVATE|SIGNING|SECRET)" }).Count'
            $childCount = & (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe') -NoProfile -EncodedCommand ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($childScript)))
            Assert-Setup ($childCount -eq 0) 'A real consumer child inherited signing variables.'
        }
        $global:LASTEXITCODE = 0
        return
    }
    if ($args[0] -eq '--version') { $global:LASTEXITCODE = 0; return '10.0.302' }
    if ($args[0] -eq 'user-secrets') {
        $project = $args[[Array]::IndexOf($args, '--project') + 1]
        $service = @('User','Facility','Operation','Payment','Notification') | Where-Object { $project -match "(${_}|SmartParking\.NotificationAudit)Service(\.API)?$" -or $project -match "SmartParking\.(Audit)?Service\.API" }
        if ($service -eq 'Facility') { $service = 'Parking' }
        if ($service -eq 'Operation') { $service = 'Reservation' }
        if (!$global:setupTestSecretMaps.ContainsKey($service)) { $global:setupTestSecretMaps[$service] = @{} }
        if ($args[1] -eq 'remove') {
            $null = $global:setupTestSecretMaps[$service].Remove($args[2])
            $global:setupTestSecretWrites++
        } else {
            $values = ($input | Out-String | ConvertFrom-Json)
            foreach ($entry in $values.PSObject.Properties) { $global:setupTestSecretMaps[$service][$entry.Name] = $entry.Value }
            $global:setupTestSecretWrites++
        }
    }
    $global:LASTEXITCODE = 0
}
function cmd.exe {
    if ($args[0] -ne '/c' -or $args[1] -notmatch '^dotnet user-secrets set --project "([^"]+)" < "([^"]+)"$') {
        throw 'Unexpected cmd.exe invocation in the setup fixture.'
    }
    $projectPath = $Matches[1]
    $inputPath = $Matches[2]
    [IO.File]::ReadAllText($inputPath) | dotnet user-secrets set --project $projectPath
}
function docker {
    $global:setupTestDockerCalls += ,@($args)
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'volume' -and $global:setupTestOldVolume) { return 'existing-test-volume' }
}
function New-Fixture($name) {
    $fixture = Join-Path $testRoot $name
    foreach ($directory in @('scripts','FE')) { New-Item -ItemType Directory -Force -Path (Join-Path $fixture $directory) | Out-Null }
    foreach ($file in @('setup.ps1','.env.example','scripts/load-env.ps1','scripts/jwt-environment.ps1','scripts/run-schema-service.ps1')) { Copy-Item -LiteralPath (Join-Path $sourceRoot $file) -Destination (Join-Path $fixture $file) }
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'FE/.env.example') -Destination (Join-Path $fixture 'FE/.env.example')
    foreach ($service in @('User','Parking','Reservation','Payment','Notification')) {
        if ($service -eq 'Notification') {
            $project = Join-Path $fixture "src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API"
            [IO.Directory]::CreateDirectory($project) | Out-Null
            [IO.File]::WriteAllText((Join-Path $project "SmartParking.NotificationAuditService.API.csproj"), "<Project><PropertyGroup><UserSecretsId>smartpark-setup-test-$service</UserSecretsId></PropertyGroup></Project>")
        } else {
            $projectService = switch ($service) { 'Parking' { 'Facility' } 'Reservation' { 'Operation' } default { $service } }
            $project = Join-Path $fixture "src/Services/${projectService}Service/SmartParking.${projectService}Service.API"
            New-Item -ItemType Directory -Force -Path $project | Out-Null
            [IO.File]::WriteAllText((Join-Path $project "SmartParking.${projectService}Service.API.csproj"), "<Project><PropertyGroup><UserSecretsId>smartpark-setup-test-$service</UserSecretsId></PropertyGroup></Project>")
        }
    }
    return $fixture
}
function Invoke-Fixture($fixture, $extra = @{}) {
    $log = (& (Join-Path $fixture 'setup.ps1') -SkipDatabase -SkipRestore -NonInteractive @extra *>&1 | Out-String)
    Assert-Setup ($log -notmatch 'review-team-password|review-team-service-key|Review@123|synthetic-cloud-') 'Credential leaked into setup output.'
}
function Expect-Failure($action, $pattern) {
    $failed = $false
    try { & $action } catch { $failed = $_.Exception.Message -match $pattern }
    Assert-Setup $failed "Expected failure: $pattern"
}
$local = New-Fixture 'local'
Invoke-Fixture $local
$localBytes = [IO.File]::ReadAllBytes((Join-Path $local '.env'))
$localSettings = & (Join-Path $local 'scripts/load-env.ps1') -EnvPath (Join-Path $local '.env') -NoExport
foreach ($pair in @{ Username='USER'; Password='PASSWORD'; VirtualHost='VHOST'; Port='PORT' }.GetEnumerator()) {
    Assert-Setup ($global:setupTestSecretMaps['User']["SMARTPARK_RABBITMQ_$($pair.Value)"] -ceq $localSettings["SMARTPARK_RABBITMQ_$($pair.Value)"]) 'Shared broker setting was not mapped to host configuration.'
}
Assert-Setup ($global:setupTestSecretMaps['User']['SMARTPARK_RABBITMQ_HOST'] -eq '127.0.0.1') 'Host broker address must be loopback.'
Assert-Setup ($global:setupTestSecretMaps['User']['Messaging:OutboxDeliveryEnabled'] -eq 'false') 'Delivery enabled without explicit configuration.'
Assert-Setup ($global:setupTestSecretMaps['User']['Cors:AllowedOrigins'] -eq $localSettings['SMARTPARK_USER_CORS_ORIGINS']) 'CORS origins were not mapped.'
$writes = $global:setupTestSecretWrites
Invoke-Fixture $local
Assert-Setup ([Convert]::ToBase64String($localBytes) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $local '.env')))) 'Rerun changed .env.'
Assert-Setup ($global:setupTestSecretWrites -eq $writes) 'Rerun rewrote existing secrets.'
$key = $global:setupTestSecretMaps['User']['Services:Key']
Assert-Setup ($key -eq $global:setupTestSecretMaps['Parking']['Services:Key'] -and $key -eq $global:setupTestSecretMaps['Reservation']['Services:Key'] -and $key -eq $global:setupTestSecretMaps['Payment']['Services:Key']) 'Local service keys differ.'
Assert-Setup ($global:setupTestSecretMaps['Parking']['ConnectionStrings:Parking'] -match 'Database=smartpark_facility;Username=smartpark_facility;') 'Facility database mapping differs from compose.'
Assert-Setup ($global:setupTestSecretMaps['Reservation']['ConnectionStrings:Reservation'] -match 'Database=smartpark_operations;Username=smartpark_operations;') 'Operations database mapping differs from compose.'
$global:setupTestSecretMaps = @{}
$teamFile = Join-Path $testRoot 'team-private.env'
[IO.File]::WriteAllText($teamFile, "SMARTPARK_DEV_PASSWORD=review-team-password`nSMARTPARK_SERVICE_KEY=review-team-service-key-at-least-32-characters`nSMARTPARK_ADMIN_PASSWORD=Review@123`nVITE_MAPTILER_API_KEY=review-map-key`nSMARTPARK_SERVICE_PARKING_URL=http://parking.example.test:5045/`n")
$teamA = New-Fixture 'team-a'
Invoke-Fixture $teamA @{CredentialMode='Team'; TeamEnvPath=$teamFile}
$teamKey = $global:setupTestSecretMaps['User']['Services:Key']
Assert-Setup ($global:setupTestSecretMaps['User']['Services:Parking'] -eq 'http://parking.example.test:5045/') 'Team URL not mapped.'
$previousKey = $env:SMARTPARK_SERVICE_KEY
$env:SMARTPARK_SERVICE_KEY = 'no-export-sentinel'
$null = & (Join-Path $teamA 'scripts/load-env.ps1') -EnvPath $teamFile -NoExport
Assert-Setup ($env:SMARTPARK_SERVICE_KEY -ceq 'no-export-sentinel') 'NoExport mutated process environment.'
$env:SMARTPARK_SERVICE_KEY = $previousKey
Assert-Setup ((& (Join-Path $teamA 'scripts/load-env.ps1') -EnvPath (Join-Path $teamA 'FE/.env') -NoExport)['VITE_MAPTILER_API_KEY'] -eq 'review-map-key') 'Map key missing.'
$global:setupTestSecretMaps = @{}
$teamB = New-Fixture 'team-b'
Invoke-Fixture $teamB @{CredentialMode='Team'; TeamEnvPath=$teamFile}
Assert-Setup ($global:setupTestSecretMaps['User']['Services:Key'] -eq $teamKey) 'Team baseline differs across machines.'
$before = [IO.File]::ReadAllText((Join-Path $teamB '.env'))
[IO.File]::WriteAllText($teamFile, ([IO.File]::ReadAllText($teamFile).Replace('Review@123','Changed@123')))
Expect-Failure { Invoke-Fixture $teamB @{CredentialMode='Team'; TeamEnvPath=$teamFile} } 'Team/local conflict'
Assert-Setup ([IO.File]::ReadAllText((Join-Path $teamB '.env')) -ceq $before) 'Conflict mutated .env.'
$global:setupTestSecretMaps['Parking']['Services:Key'] = 'different-service-key-at-least-32-characters'
Invoke-Fixture $teamB
Assert-Setup ($global:setupTestSecretMaps['Parking']['Services:Key'] -ceq $teamKey) 'Services:Key was not synced from .env.'
Invoke-Fixture $teamB @{SkipUserSecrets=$true}
$global:setupTestSecretMaps['User']['Services:Parking'] = 'http://old-parking.example.test/'
Invoke-Fixture $teamB
Assert-Setup ($global:setupTestSecretMaps['User']['Services:Parking'] -ceq 'http://parking.example.test:5045/') 'Services:Parking URL was not synced from .env.'
Invoke-Fixture $teamB @{SkipUserSecrets=$true; SkipSonarQube=$true}
$global:setupTestSecretMaps['User']['DevelopmentAdmin:Password'] = 'Different@Admin1'
$global:setupTestSecretMaps['User']['Jwt:PrivateKeyPem'] = 'stale-jwt-key-material'
$global:setupTestSecretMaps['User']['Otp:Smtp:Username'] = 'stale-smtp-user'
Invoke-Fixture $teamB
Assert-Setup ($global:setupTestSecretMaps['User']['DevelopmentAdmin:Password'] -ceq 'Review@123') 'Admin password was not synced from .env.'
Assert-Setup (!$global:setupTestSecretMaps['User'].ContainsKey('Jwt:PrivateKeyPem')) 'Jwt:PrivateKeyPem was not removed when absent from .env.'
Assert-Setup (!$global:setupTestSecretMaps['User'].ContainsKey('Otp:Smtp:Username')) 'Otp:Smtp:Username was not removed when absent from .env.'
$invalidPort = New-Fixture 'invalid-port'
$invalidTemplate = Join-Path $invalidPort '.env.example'
[IO.File]::WriteAllText($invalidTemplate, ([IO.File]::ReadAllText($invalidTemplate).Replace('SMARTPARK_DB_IOT_PORT=5446','SMARTPARK_DB_IOT_PORT=99999')))
Expect-Failure { Invoke-Fixture $invalidPort @{SkipUserSecrets=$true} } 'Invalid port'
Assert-Setup (!(Test-Path -LiteralPath (Join-Path $invalidPort '.env'))) 'Invalid port created .env.'
$fresh = New-Fixture 'invalid-team'
Expect-Failure { Invoke-Fixture $fresh @{CredentialMode='Team'} } 'requires -TeamEnvPath'
Assert-Setup (!(Test-Path -LiteralPath (Join-Path $fresh '.env'))) 'Invalid mode created .env.'
$global:setupTestSecretMaps = @{}
$global:setupTestOldVolume = $true
$freshVolume = New-Fixture 'old-volume'
Expect-Failure { & (Join-Path $freshVolume 'setup.ps1') -SkipRestore -NonInteractive } 'Existing database volumes found'
Assert-Setup (!(Test-Path -LiteralPath (Join-Path $freshVolume '.env'))) 'Old-volume failure wrote .env.'
$global:setupTestOldVolume = $false
$assistantFixture = New-Fixture 'assistant-integration'
Copy-Item -LiteralPath (Join-Path $local '.env') -Destination (Join-Path $assistantFixture '.env')
$assistantBefore = [IO.File]::ReadAllText((Join-Path $assistantFixture '.env'))
New-Item -ItemType Directory -Force -Path (Join-Path $assistantFixture 'scripts/messaging') | Out-Null
[IO.File]::WriteAllText((Join-Path $assistantFixture 'scripts/messaging/provision-profile-audit-topology.ps1'), @'
param($ManagementUri, $Credential, $VirtualHost)
$global:setupTestAssistantSteps += 'broker'
'@)
[IO.File]::WriteAllText((Join-Path $assistantFixture 'scripts/apply-workflow-migrations.ps1'), @'
param($Service, $SchemaRoot, $EnvPath)
$global:setupTestAssistantSteps += "schema-$Service"
'@)
[IO.File]::WriteAllText((Join-Path $assistantFixture 'scripts/setup-assistant-docker.ps1'), @'
param($EnvPath, [switch]$ValidateOnly)
if ($ValidateOnly) {
    $global:setupTestAssistantSteps += 'preflight'
    if ($global:setupTestAssistantReject) { throw 'Fixture Assistant preflight rejected.' }
} else {
    $global:setupTestAssistantSteps += 'assistant'
    if (!$EnvPath -or !(Test-Path -LiteralPath $EnvPath)) { throw 'Missing setup environment path.' }
}
'@)
$global:setupTestAssistantSteps = @()
$global:setupTestAssistantReject = $true
$dockerBefore = $global:setupTestDockerCalls.Count
Expect-Failure { & (Join-Path $assistantFixture 'setup.ps1') -WithAssistant -SkipRestore -SkipSonarQube -SkipUserSecrets -NonInteractive } 'Fixture Assistant preflight rejected'
Assert-Setup ($global:setupTestDockerCalls.Count -eq $dockerBefore) 'Assistant preflight failed after Docker provisioning.'
Assert-Setup ([IO.File]::ReadAllText((Join-Path $assistantFixture '.env')) -ceq $assistantBefore) 'Failed Assistant preflight changed configuration.'
$global:setupTestAssistantReject = $false
$global:setupTestAssistantSteps = @()
& (Join-Path $assistantFixture 'setup.ps1') -WithAssistant -SkipRestore -SkipSonarQube -SkipUserSecrets -NonInteractive
$expectedSteps = @('preflight','broker','schema-User','schema-Parking','schema-Reservation','schema-Payment','schema-Notification','schema-Iot','assistant')
Assert-Setup (($global:setupTestAssistantSteps -join '|') -ceq ($expectedSteps -join '|')) 'Assistant setup must preflight before provisioning and start after schema verification.'
Assert-Setup ([IO.File]::ReadAllText((Join-Path $assistantFixture '.env')) -ceq $assistantBefore) 'Assistant setup replaced an existing .env.'
Expect-Failure { & (Join-Path $assistantFixture 'setup.ps1') -WithAssistant -SkipDatabase } 'cannot be combined'
Expect-Failure { & (Join-Path $assistantFixture 'setup.ps1') -WithAssistant -RecreateDb } 'cannot be combined'
[IO.File]::AppendAllText((Join-Path $assistantFixture '.env'), "`nSMARTPARK_ASSISTANT_DOCKER_ENABLED=true`n")
$global:setupTestAssistantSteps = @()
& (Join-Path $assistantFixture 'setup.ps1') -SkipRestore -SkipSonarQube -SkipUserSecrets -NonInteractive
Assert-Setup (($global:setupTestAssistantSteps -join '|') -ceq ($expectedSteps -join '|')) 'Configured Assistant was not included in normal setup.'
& {
    $runner = Join-Path $assistantFixture 'scripts/run-app-service.ps1'
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'scripts/run-app-service.ps1') -Destination $runner
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'scripts/discovery-docker.ps1') -Destination (Join-Path $assistantFixture 'scripts/discovery-docker.ps1')
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'compose.app.yml') -Destination $assistantFixture
    $global:setupTestAssistantSteps = @()
    $global:setupTestFrontendUrl = ''
    $global:setupTestFrontendRunning = $true
    $global:setupTestAssistantHealthy = $true
    function docker {
        $global:LASTEXITCODE = 0
        if ($args[0] -eq 'ps') {
            if ($args -contains 'label=com.docker.compose.service=frontend') {
                if ($global:setupTestFrontendRunning) { return 'frontend-container' }
            } elseif ($global:setupTestAssistantHealthy) { return 'assistant-container' }
            return
        }
        if ($args[0] -eq 'port') { return '127.0.0.1:6076' }
        if ($args -contains 'up') {
            Assert-Setup ($args -contains '--no-build') 'App runner did not preserve NoBuild.'
            Assert-Setup ($env:COMPOSE_REMOVE_ORPHANS -ceq 'false') 'App runner could remove Assistant as an orphan.'
            if ($global:setupTestRefreshOnly) {
                Assert-Setup ($args[-1] -ceq 'frontend' -and $args -notcontains 'user-service') 'Assistant refreshed another app service.'
            }
            $global:setupTestAssistantSteps += 'app'
            $global:setupTestFrontendUrl = $env:VITE_ASSISTANT_API_BASE_URL
        }
        if ($args[0] -eq 'compose' -and $args -contains 'ps') { return '{"State":"running","Health":"healthy"}' }
    }
    $previousUrl = $env:VITE_ASSISTANT_API_BASE_URL
    $previousRemoveOrphans = $env:COMPOSE_REMOVE_ORPHANS
    try {
        $env:COMPOSE_REMOVE_ORPHANS = 'true'
        $env:VITE_ASSISTANT_API_BASE_URL = 'https://localhost:5075'
        & $runner -Service All -NoBuild -NoLogWindow
        Assert-Setup (($global:setupTestAssistantSteps -join '|') -ceq 'assistant|app') 'All did not start Assistant before the app stack.'
        Assert-Setup ($global:setupTestFrontendUrl -ceq 'http://localhost:6076') 'Docker frontend did not receive the published Assistant port.'
        Assert-Setup ($env:VITE_ASSISTANT_API_BASE_URL -ceq 'https://localhost:5075') 'App runner changed the caller frontend configuration.'
        Assert-Setup ($env:COMPOSE_REMOVE_ORPHANS -ceq 'true') 'App runner changed the caller Compose configuration.'
        $global:setupTestAssistantSteps = @()
        $global:setupTestRefreshOnly = $true
        & $runner -Service Assistant -NoBuild -NoLogWindow
        Assert-Setup (($global:setupTestAssistantSteps -join '|') -ceq 'assistant|app') 'Assistant did not refresh the running Docker frontend.'
        $global:setupTestAssistantSteps = @()
        $global:setupTestFrontendRunning = $false
        & $runner -Service Assistant -NoBuild -NoLogWindow
        Assert-Setup (($global:setupTestAssistantSteps -join '|') -ceq 'assistant') 'Assistant started a previously stopped frontend.'
        $global:setupTestAssistantHealthy = $false
        Expect-Failure { & $runner -Service Frontend -NoBuild -NoLogWindow } 'Docker Assistant is enabled but has no healthy container'
    } finally {
        $env:VITE_ASSISTANT_API_BASE_URL = $previousUrl
        $env:COMPOSE_REMOVE_ORPHANS = $previousRemoveOrphans
    }
}
Write-Output 'PASS: local reuse, idempotence, shared Team baseline, remote URL, MapTiler import, secrets sync/overwrite/removal, skip-secrets and old-volume protection.'
. (Join-Path $sourceRoot 'scripts/jwt-environment.ps1')
$global:setupTestOldVolume = $false
$jwtFixture = New-Fixture 'jwt'
Invoke-Fixture $jwtFixture
Assert-Setup (!$global:setupTestSecretMaps['User'].ContainsKey('Jwt:PrivateKeyPemBase64')) 'Placeholder synchronized as a private key.'
$jwtEnv = Join-Path $jwtFixture '.env'
[IO.File]::AppendAllText($jwtEnv, "`nSMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=synthetic-signing-sentinel`nSMARTPARK_SERVICE_USER_URL=http://localhost:6035/`n")
Invoke-Fixture $jwtFixture
Assert-Setup ($global:setupTestSecretMaps['User']['Jwt:PrivateKeyPemBase64'] -eq 'synthetic-signing-sentinel') 'Issuer secret mapping missing.'
foreach ($consumer in @('Parking','Reservation','Payment')) {
    Assert-Setup (@($global:setupTestSecretMaps[$consumer].Keys | Where-Object { Test-JwtSigningSecretName $_ }).Count -eq 0) 'Consumer user-secrets contain signing material.'
    Assert-Setup ($global:setupTestSecretMaps[$consumer]['JwtValidation:JwksUri'] -eq 'http://localhost:6035/.well-known/jwks.json') 'Consumer trust URL incorrect.'
}
foreach ($service in @('Facility','Parking','Operation','Reservation','Payment','User')) {
    $env:ASPNETCORE_Jwt__PrivateKeyPem = 'inherited-sentinel'
    $env:DOTNET_Jwt__PrivateKeyPemBase64 = 'inherited-sentinel'
    $env:Jwt__PrivateKeyPem = 'inherited-sentinel'
    & (Join-Path $jwtFixture 'scripts/run-schema-service.ps1') -Service $service
    if ($service -eq 'User') {
        Assert-Setup ($global:runnerEnvironment['Jwt__PrivateKeyPemBase64'] -eq 'synthetic-signing-sentinel') 'User runner signing mapping missing.'
    } else {
        Assert-Setup (@($global:runnerEnvironment.Keys | Where-Object { Test-JwtSigningSecretName $_ }).Count -eq 0) 'Consumer inherited signing material.'
        Assert-Setup ($global:runnerEnvironment['JwtValidation__JwksUri'] -eq 'http://localhost:6035/.well-known/jwks.json') 'Host trust URL incorrect.'
    }
    Clear-JwtSigningEnvironment
}
$env:Jwt__PrivateKeyPem = 'inherited-sentinel'
$null = & (Join-Path $jwtFixture 'scripts/load-env.ps1') -EnvPath $jwtEnv
Assert-Setup ((Get-JwtSigningEnvironment).Count -eq 0) 'Default loader retained private settings.'
# Exercise the interactive branch in a scratch copy even when CI stdin is redirected.
$fixtureSetup = Join-Path $jwtFixture 'setup.ps1'
$fixtureSource = [IO.File]::ReadAllText($fixtureSetup).Replace('-and -not [Console]::IsInputRedirected', '')
[IO.File]::WriteAllText($fixtureSetup, $fixtureSource, [Text.UTF8Encoding]::new($true))
function Read-Host { return $global:fixtureChoice }
foreach ($choice in @('', 'K', 'A', 'O')) {
    $global:fixtureChoice = $choice
    $beforeChoice = [IO.File]::ReadAllText($jwtEnv)
    try { & $fixtureSetup -SkipDatabase -SkipRestore | Out-Null } catch { if ($choice -ne 'A') { throw } }
    if ($choice -eq 'O') {
        Assert-Setup ([IO.File]::ReadAllText((Join-Path $jwtFixture '.env.bak')) -ceq $beforeChoice) 'Overwrite backup incorrect.'
        Assert-Setup ([IO.File]::ReadAllText($jwtEnv) -notmatch 'synthetic-signing-sentinel') 'Overwrite did not regenerate template.'
    } else { Assert-Setup ([IO.File]::ReadAllText($jwtEnv) -ceq $beforeChoice) 'Keep/Abort altered environment file.' }
}
Write-Output 'PASS: JWT placeholder, scoped secrets, inherited aliases, host service aliases, public trust, Keep/Enter/Abort/Overwrite backup.'

$global:setupTestSecretMaps = @{}
$mediaFixture = New-Fixture 'media'
Invoke-Fixture $mediaFixture
$mediaEnv = Join-Path $mediaFixture '.env'
$mediaFields = @{'CloudName'='CLOUD_NAME'; 'ApiKey'='API_KEY'; 'ApiSecret'='API_SECRET'}
Assert-Setup ($global:setupTestSecretMaps['User']['MediaStorage:Enabled'] -eq 'false') 'Media must default off.'
foreach ($service in @('User','Parking','Reservation','Payment')) {
    foreach ($field in $mediaFields.Keys) { Assert-Setup (!$global:setupTestSecretMaps[$service].ContainsKey('MediaStorage:Cloudinary:' + $field)) 'Setup generated provider credentials.' }
}
$mediaTeamFile = Join-Path $testRoot 'media-team.env'
$mediaTeamInput = & (Join-Path $mediaFixture 'scripts/load-env.ps1') -EnvPath $mediaEnv -NoExport
$mediaTeamLines = @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD') | ForEach-Object { $_ + '=' + $mediaTeamInput[$_] }
foreach ($field in $mediaFields.GetEnumerator()) { $mediaTeamLines += 'SMARTPARK_CLOUDINARY_' + $field.Value + '=synthetic-cloud-' + $field.Key }
[IO.File]::WriteAllLines($mediaTeamFile, $mediaTeamLines)
Invoke-Fixture $mediaFixture @{CredentialMode='Team'; TeamEnvPath=$mediaTeamFile}
foreach ($service in @('User','Parking','Reservation','Payment')) {
    foreach ($field in $mediaFields.Keys) { Assert-Setup ($global:setupTestSecretMaps[$service]['MediaStorage:Cloudinary:' + $field] -ceq ('synthetic-cloud-' + $field)) 'Shared media credential import failed.' }
    if ($service -ne 'User') { Assert-Setup (!$global:setupTestSecretMaps[$service].ContainsKey('MediaStorage:Enabled')) 'Media switch leaked to another service.' }
}
$mediaWrites = $global:setupTestSecretWrites
Invoke-Fixture $mediaFixture @{CredentialMode='Team'; TeamEnvPath=$mediaTeamFile}
Assert-Setup ($global:setupTestSecretWrites -eq $mediaWrites) 'Media rerun was not idempotent.'
$mediaBeforeConflict = [IO.File]::ReadAllText($mediaEnv)
[IO.File]::AppendAllText($mediaTeamFile, "`nSMARTPARK_CLOUDINARY_API_SECRET=synthetic-cloud-conflict`n")
Expect-Failure { Invoke-Fixture $mediaFixture @{CredentialMode='Team'; TeamEnvPath=$mediaTeamFile} } 'Team/local conflict for SMARTPARK_CLOUDINARY_API_SECRET'
Assert-Setup ([IO.File]::ReadAllText($mediaEnv) -ceq $mediaBeforeConflict) 'Media conflict changed local input.'
[IO.File]::AppendAllText($mediaEnv, "`nSMARTPARK_CLOUDINARY_API_SECRET=synthetic-cloud-rotated`nSMARTPARK_USER_MEDIA_ENABLED=true`n")
Invoke-Fixture $mediaFixture
foreach ($service in @('User','Parking','Reservation','Payment')) {
    Assert-Setup ($global:setupTestSecretMaps[$service]['MediaStorage:Cloudinary:ApiSecret'] -ceq 'synthetic-cloud-rotated') 'Rotation failed.'
}
foreach ($service in @('Facility','Parking','Operation','Reservation','Payment','User')) {
    & (Join-Path $mediaFixture 'scripts/run-schema-service.ps1') -Service $service
    Assert-Setup ($global:runnerEnvironment['MediaStorage__Cloudinary__ApiSecret'] -ceq 'synthetic-cloud-rotated') 'Host media mapping failed.'
    foreach ($field in @('CloudName','ApiKey')) { Assert-Setup ($global:runnerEnvironment['MediaStorage__Cloudinary__' + $field] -ceq ('synthetic-cloud-' + $field)) 'Host media field missing.' }
    if ($service -eq 'User') { Assert-Setup ($global:runnerEnvironment['MediaStorage__Enabled'] -eq 'true') 'User media opt-in missing.' }
    else { Assert-Setup (!$global:runnerEnvironment.ContainsKey('MediaStorage__Enabled') -and !$global:runnerEnvironment.ContainsKey('SMARTPARK_USER_MEDIA_ENABLED')) 'Media switch reached a non-owner host.' }
}
# Removed and blank input must both erase stale mapped and raw credentials.
foreach ($removalMode in @('missing','blank')) {
    $mediaLines = @([IO.File]::ReadAllLines($mediaEnv) | Where-Object { $_ -notmatch '^SMARTPARK_(CLOUDINARY_|USER_MEDIA_ENABLED)' })
    if ($removalMode -eq 'blank') { foreach ($field in $mediaFields.Values) { $mediaLines += 'SMARTPARK_CLOUDINARY_' + $field + '=' } }
    [IO.File]::WriteAllLines($mediaEnv, $mediaLines)
    Invoke-Fixture $mediaFixture
    Assert-Setup ($global:setupTestSecretMaps['User']['MediaStorage:Enabled'] -eq 'false') 'Removed switch did not disable media.'
    foreach ($service in @('User','Parking','Reservation','Payment')) {
        foreach ($field in $mediaFields.Keys) { Assert-Setup (!$global:setupTestSecretMaps[$service].ContainsKey('MediaStorage:Cloudinary:' + $field)) 'Removed media secret retained.' }
    }
    foreach ($service in @('User','Facility','Parking','Operation','Reservation','Payment')) {
        $env:SMARTPARK_CLOUDINARY_API_SECRET = 'synthetic-cloud-stale'
        $env:MediaStorage__Cloudinary__ApiSecret = 'synthetic-cloud-stale'
        & (Join-Path $mediaFixture 'scripts/run-schema-service.ps1') -Service $service
        Assert-Setup ([string]::IsNullOrWhiteSpace($global:runnerEnvironment['MediaStorage__Cloudinary__ApiSecret'])) 'Removed mapped media credential inherited.'
        Assert-Setup ([string]::IsNullOrWhiteSpace($global:runnerEnvironment['SMARTPARK_CLOUDINARY_API_SECRET'])) 'Removed raw media credential inherited.'
        Assert-Setup ($env:MediaStorage__Cloudinary__ApiSecret -ceq 'synthetic-cloud-stale') 'Runner did not restore caller environment.'
    }
}
Remove-Item Env:SMARTPARK_CLOUDINARY_API_SECRET, Env:MediaStorage__Cloudinary__ApiSecret
Assert-Setup ([IO.File]::ReadAllText((Join-Path $mediaFixture 'FE/.env')) -notmatch 'CLOUDINARY|synthetic-cloud-') 'Provider configuration reached frontend.'
Write-Output 'PASS: optional media imports, rotation, removal, idempotence, host aliases, feature isolation and frontend/log secret isolation.'
Write-Output 'PASS: opt-in Assistant preflight order, default schema flow, final dispatch, env preservation and incompatible-option guards.'
Write-Output "Scratch evidence retained at $testRoot"
Write-Output ('PowerShell version: ' + $PSVersionTable.PSVersion.ToString())
