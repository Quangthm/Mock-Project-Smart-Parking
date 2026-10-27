# Run in Windows PowerShell 5.1. All writes go to a scratch checkout; CLI/secrets are doubles.
param()
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $sourceRoot ('.cache/setup-tests-' + [Guid]::NewGuid().ToString('N'))
$global:setupTestSecretMaps = @{}
$global:setupTestSecretWrites = 0
$global:setupTestDockerCalls = @()
$global:setupTestOldVolume = $false
function Assert-Setup($condition, $message) { if (!$condition) { throw $message } }
function Test-Path {
    param([Parameter(Position=0)][string]$Path, [string]$LiteralPath, [string]$PathType)
    $probe = if ($LiteralPath) { $LiteralPath } else { $Path }
    if ($probe -match 'smartpark-setup-test-(User|Parking|Reservation)[/\\]secrets.json$') { return $global:setupTestSecretMaps.ContainsKey($matches[1]) }
    Microsoft.PowerShell.Management\Test-Path @PSBoundParameters
}
function Get-Content {
    param([Parameter(Position=0)][string]$Path, [string]$LiteralPath, [switch]$Raw, [string]$Encoding)
    $probe = if ($LiteralPath) { $LiteralPath } else { $Path }
    if ($probe -match 'smartpark-setup-test-(User|Parking|Reservation)[/\\]secrets.json$') { return ($global:setupTestSecretMaps[$matches[1]] | ConvertTo-Json) }
    Microsoft.PowerShell.Management\Get-Content @PSBoundParameters
}
function dotnet {
    if ($args[0] -eq '--version') { $global:LASTEXITCODE = 0; return '10.0.302' }
    if ($args[0] -eq 'user-secrets') {
        $project = $args[[Array]::IndexOf($args, '--project') + 1]
        $service = @('User','Facility','Operation') | Where-Object { $project -match "${_}Service.API$" }
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
function docker {
    $global:setupTestDockerCalls += ,@($args)
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'volume' -and $global:setupTestOldVolume) { return 'existing-test-volume' }
}
function New-Fixture($name) {
    $fixture = Join-Path $testRoot $name
    foreach ($directory in @('scripts','FE')) { New-Item -ItemType Directory -Force -Path (Join-Path $fixture $directory) | Out-Null }
    foreach ($file in @('setup.ps1','.env.example','scripts/load-env.ps1')) { Copy-Item -LiteralPath (Join-Path $sourceRoot $file) -Destination (Join-Path $fixture $file) }
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'FE/.env.example') -Destination (Join-Path $fixture 'FE/.env.example')
    foreach ($service in @('User','Parking','Reservation')) {
        $projectService = switch ($service) { 'Parking' { 'Facility' } 'Reservation' { 'Operation' } default { $service } }
        $project = Join-Path $fixture "src/Services/${projectService}Service/SmartParking.${projectService}Service.API"
        New-Item -ItemType Directory -Force -Path $project | Out-Null
        [IO.File]::WriteAllText((Join-Path $project "SmartParking.${projectService}Service.API.csproj"), "<Project><PropertyGroup><UserSecretsId>smartpark-setup-test-$service</UserSecretsId></PropertyGroup></Project>")
    }
    return $fixture
}
function Invoke-Fixture($fixture, $extra = @{}) {
    $log = (& (Join-Path $fixture 'setup.ps1') -SkipDatabase -SkipRestore -NonInteractive @extra | Out-String)
    Assert-Setup ($log -notmatch 'review-team-password|review-team-service-key|Review@123') 'Credential leaked into setup output.'
}
function Expect-Failure($action, $pattern) {
    $failed = $false
    try { & $action } catch { $failed = $_.Exception.Message -match $pattern }
    Assert-Setup $failed "Expected failure: $pattern"
}
$local = New-Fixture 'local'
Invoke-Fixture $local
$localBytes = [IO.File]::ReadAllBytes((Join-Path $local '.env'))
$writes = $global:setupTestSecretWrites
Invoke-Fixture $local
Assert-Setup ([Convert]::ToBase64String($localBytes) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $local '.env')))) 'Rerun changed .env.'
Assert-Setup ($global:setupTestSecretWrites -eq $writes) 'Rerun rewrote existing secrets.'
$key = $global:setupTestSecretMaps['User']['Services:Key']
Assert-Setup ($key -eq $global:setupTestSecretMaps['Parking']['Services:Key'] -and $key -eq $global:setupTestSecretMaps['Reservation']['Services:Key']) 'Local service keys differ.'
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
Write-Output 'PASS: local reuse, idempotence, shared Team baseline, remote URL, MapTiler import, secrets sync/overwrite/removal, skip-secrets and old-volume protection.'
Write-Output "Scratch evidence retained at $testRoot"
