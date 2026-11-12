param(
    [Parameter(Mandatory)][ValidateSet('User','Facility','Parking','Operation','Reservation','Payment','Assistant','Frontend','All')][string]$Service,
    [switch]$NoBuild,
    [switch]$NoLogWindow
)
$ErrorActionPreference = 'Stop'
if ($Service -eq 'Parking') { $Service = 'Facility' } # Legacy CLI alias.
if ($Service -eq 'Reservation') { $Service = 'Operation' }
$root = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $root '.env'
$composePath = Join-Path $root 'compose.app.yml'
$project = 'smartpark-app'

if (!(Test-Path -LiteralPath $envFile)) { throw 'Run setup.ps1 first (.env missing).' }
if (!(Test-Path -LiteralPath $composePath)) { throw 'compose.app.yml missing.' }
if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI not found. Start Docker Desktop and retry.' }

$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile -NoExport
$assistantEnabled = $settings['SMARTPARK_ASSISTANT_DOCKER_ENABLED'] -ceq 'true'

$composeArgs = @('compose','--project-name',$project,'--env-file',$envFile,'-f',$composePath)
$userTlsEnvironment = @{}
$userTlsEnabled = ''
if ($Service -in @('User', 'All')) { $userTlsEnabled = [string]$settings['SMARTPARK_USER_DOCKER_TLS_ENABLED'] }
if ($userTlsEnabled -cnotin @('', 'false', 'true')) {
    throw 'SMARTPARK_USER_DOCKER_TLS_ENABLED must be true or false.'
}
if ($userTlsEnabled -ceq 'true') {
    $userTlsPath = [string]$settings['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH']
    $userTlsPassword = [string]$settings['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD']
    $userTlsAbsolute = [IO.Path]::IsPathRooted($userTlsPath)
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        $userTlsAbsolute = $userTlsPath -match '^[A-Za-z]:[\\/]' -or $userTlsPath -match '^\\\\[^\\/]+[\\/][^\\/]+'
    }
    if (!$userTlsAbsolute -or
        !(Test-Path -LiteralPath $userTlsPath -PathType Leaf) -or !$userTlsPassword) {
        throw 'User Docker TLS requires an existing absolute PFX path and its password.'
    }
    $userTlsCertificate = $null
    try {
        $userTlsCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $userTlsPath, $userTlsPassword, [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
        if (!$userTlsCertificate.HasPrivateKey -or $userTlsCertificate.NotBefore -gt (Get-Date) -or
            $userTlsCertificate.NotAfter -le (Get-Date)) {
            throw 'Invalid User TLS certificate.'
        }
        $san = $userTlsCertificate.Extensions |
            Where-Object { $_.Oid.Value -eq '2.5.29.17' } |
            ForEach-Object { $_.Format($true) }
        if (($san -join ';') -notmatch '(?im)(?:^|[,;\r\n])\s*(?:DNS Name=|DNS:)\s*user-service\s*(?=$|[,;\r\n])') {
            throw 'User TLS certificate SAN must include user-service.'
        }
    } catch {
        throw 'User Docker TLS requires an unexpired PFX with a private key, matching password, and DNS SAN user-service.'
    } finally {
        if ($userTlsCertificate) { $userTlsCertificate.Dispose() }
    }
    $composeArgs += @('-f', (Join-Path $root 'compose.user-tls.yml'))
    $userTlsEnvironment['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH'] = $userTlsPath
    $userTlsEnvironment['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD'] = $userTlsPassword
}

# Docker writes progress and warnings to stderr. PowerShell 5.1 turns native
# stderr into terminating errors under ErrorActionPreference=Stop, so all Docker
# calls go through this helper and are judged by their exit code instead.
function Invoke-Docker {
    param([string[]]$Arguments, [switch]$Capture)
    $previous = $ErrorActionPreference
    $previousTlsEnvironment = @{}
    try {
        foreach ($name in $userTlsEnvironment.Keys) {
            $previousTlsEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $userTlsEnvironment[$name], 'Process')
        }
        $ErrorActionPreference = 'Continue'
        if ($Capture) {
            $output = @(& docker @Arguments 2>&1)
            $code = $LASTEXITCODE
            return [pscustomobject]@{
                ExitCode = $code
                Output   = $output
                Text     = (@($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine)
            }
        }
        & docker @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        return $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
        foreach ($name in $previousTlsEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousTlsEnvironment[$name], 'Process')
        }
    }
}

if ($Service -eq 'Assistant' -or ($Service -eq 'All' -and $assistantEnabled)) {
    & (Join-Path $PSScriptRoot 'setup-assistant-docker.ps1') -EnvPath $envFile -NoBuild:$NoBuild
    if ($Service -eq 'Assistant') {
        $frontend = Invoke-Docker -Arguments @('ps','--quiet','--filter',"label=com.docker.compose.project=$project",'--filter','label=com.docker.compose.service=frontend') -Capture
        if ($frontend.ExitCode -ne 0) { throw 'Assistant started, but the frontend state could not be checked.' }
        if (!$frontend.Text.Trim()) { return }
        Write-Host 'Refreshing the running Docker frontend to use the Assistant container.'
        $Service = 'Frontend'
        $NoBuild = $true
        $NoLogWindow = $true
    }
}

$config = Invoke-Docker -Arguments ($composeArgs + @('config','--quiet')) -Capture
if ($config.ExitCode -ne 0) { throw "compose.app.yml is invalid.`n$($config.Text.Trim())" }

$map = @{
    User        = 'user-service'
    Facility    = 'facility-service'
    Operation   = 'operation-service'
    Payment     = 'payment-service'
    Frontend    = 'frontend'
}
$targets = if ($Service -eq 'All') {
    @('user-service','facility-service','operation-service','payment-service','frontend')
} else {
    @($map[$Service])
}

$network = Invoke-Docker -Arguments @('network','inspect','smartpark-schema_default') -Capture
if ($network.ExitCode -ne 0) { throw 'Network smartpark-schema_default missing. Run setup.ps1 first.' }

function Get-AppServiceItems {
    param([string]$Target)
    $result = Invoke-Docker -Arguments ($composeArgs + @('ps','--all','--format','json',$Target)) -Capture
    if ($result.ExitCode -ne 0) { throw "Cannot read compose state for ${Target}: $($result.Text.Trim())" }
    $text = $result.Text.Trim()
    if (!$text) { return @() }
    if ($text.StartsWith('[')) { return @($text | ConvertFrom-Json) }
    $items = @()
    foreach ($line in ($text -split "`r?`n")) {
        if (!$line.Trim()) { continue }
        $items += @($line | ConvertFrom-Json)
    }
    return $items
}

function Show-AppLogs {
    param([string]$Target)
    Write-Host "    - Recent logs for ${Target} (tail 100):" -ForegroundColor Yellow
    Invoke-Docker -Arguments ($composeArgs + @('logs','--tail','100',$Target)) | Out-Null
}

$upArgs = @('up','-d')
if ($NoBuild) { $upArgs += '--no-build' } else { $upArgs += '--build' }
$upArgs += $targets
$originalSigningKey = $env:SMARTPARK_JWT_PRIVATE_KEY_PEM
$originalAssistantUrl = $env:VITE_ASSISTANT_API_BASE_URL
$originalIgnoreOrphans = $env:COMPOSE_IGNORE_ORPHANS
$originalRemoveOrphans = $env:COMPOSE_REMOVE_ORPHANS
try {
    # Assistant is managed by a separate Compose file in this same project.
    $env:COMPOSE_IGNORE_ORPHANS = 'true'
    $env:COMPOSE_REMOVE_ORPHANS = 'false'
    if ($targets -contains 'frontend') {
        $assistant = Invoke-Docker -Arguments @('ps','--quiet','--filter',"label=com.docker.compose.project=$project",'--filter','label=com.docker.compose.service=assistant','--filter','health=healthy') -Capture
        if ($assistant.ExitCode -ne 0) { throw 'Could not check the Docker Assistant state.' }
        $assistantId = $assistant.Text.Trim()
        if ($assistantId) {
            $binding = Invoke-Docker -Arguments @('port',$assistantId,'8080/tcp') -Capture
            if ($binding.ExitCode -ne 0 -or $binding.Text.Trim() -notmatch '^127\.0\.0\.1:([0-9]+)$') {
                throw 'Could not read the Docker Assistant loopback port.'
            }
            $env:VITE_ASSISTANT_API_BASE_URL = "http://localhost:$($Matches[1])"
        } elseif ($assistantEnabled) {
            throw 'Docker Assistant is enabled but has no healthy container. Run scripts/run-app-service.ps1 -Service Assistant first.'
        }
    }
    if ($targets -contains 'user-service') {
        $signingKeyFile = $env:SMARTPARK_JWT_PRIVATE_KEY_FILE
        if (!$signingKeyFile) { $signingKeyFile = $settings['SMARTPARK_JWT_PRIVATE_KEY_FILE'] }
        if ($signingKeyFile) {
            $absolute = $signingKeyFile -match '^[A-Za-z]:[\\/]' -or $signingKeyFile -match '^\\\\[^\\/]+[\\/][^\\/]+'
            if (!$absolute -or !(Test-Path -LiteralPath $signingKeyFile -PathType Leaf)) {
                throw 'User signing-key file must exist and be absolute.'
            }
            $signingKey = [IO.File]::ReadAllText($signingKeyFile)
            if ([string]::IsNullOrWhiteSpace($signingKey)) { throw 'User signing-key file is empty.' }
            $env:SMARTPARK_JWT_PRIVATE_KEY_PEM = $signingKey
        }
    }
    $upExit = Invoke-Docker -Arguments ($composeArgs + $upArgs)
} finally {
    $env:SMARTPARK_JWT_PRIVATE_KEY_PEM = $originalSigningKey
    $env:VITE_ASSISTANT_API_BASE_URL = $originalAssistantUrl
    $env:COMPOSE_IGNORE_ORPHANS = $originalIgnoreOrphans
    $env:COMPOSE_REMOVE_ORPHANS = $originalRemoveOrphans
}
if ($upExit -ne 0) { throw 'docker compose up failed.' }

# Wait for backend health checks; frontend has no healthcheck.
$deadline = (Get-Date).AddSeconds(120)
while ($true) {
    $allReady = $true
    foreach ($target in $targets) {
        $items = @(Get-AppServiceItems -Target $target)
        if ($items.Count -eq 0) { $allReady = $false; continue }
        foreach ($item in $items) {
            $state = [string]$item.State
            $health = [string]$item.Health
            if ($state -in @('exited','dead')) {
                Show-AppLogs -Target $target
                throw "$target exited with code $($item.ExitCode)."
            }
            if ($target -eq 'frontend') {
                if ($state -ne 'running') { $allReady = $false }
                continue
            }
            if ($health -eq 'unhealthy') {
                Show-AppLogs -Target $target
                throw "$target is unhealthy."
            }
            if ($health -ne 'healthy') { $allReady = $false }
        }
    }
    if ($allReady) { break }
    if ((Get-Date) -gt $deadline) {
        foreach ($target in $targets) { Show-AppLogs -Target $target }
        throw 'Timed out waiting for containers to become healthy.'
    }
    Start-Sleep -Seconds 2
}

Write-Host ''
Write-Host 'SmartPark app containers are running:' -ForegroundColor Green
Write-Host '  User        http://localhost:5035/health'
Write-Host '  Facility    http://localhost:5045/health'
Write-Host '  Operation   http://localhost:5055/health'
Write-Host '  Payment     http://localhost:5065/health'
Write-Host '  Frontend    http://localhost:5173'
Write-Host ''

if (!$NoLogWindow) {
    foreach ($target in $targets) {
        $logCommand = "title SmartPark - $target && docker compose --project-name $project --env-file `"$envFile`" -f `"$composePath`" logs -f --tail 200 $target"
        Start-Process -FilePath 'cmd.exe' -ArgumentList '/k', $logCommand -WorkingDirectory $root -WindowStyle Hidden | Out-Null
    }
}
