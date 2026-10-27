param(
    [Parameter(Mandatory)][ValidateSet('User','Facility','Parking','Operation','Reservation','Frontend','All')][string]$Service,
    [switch]$NoBuild,
    [switch]$NoLogWindow
)
$ErrorActionPreference = 'Stop'
if ($Service -eq 'Facility') { $Service = 'Parking' }
if ($Service -eq 'Reservation') { $Service = 'Operation' }
$root = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $root '.env'
$composePath = Join-Path $root 'compose.app.yml'
$project = 'smartpark-app'

if (!(Test-Path -LiteralPath $envFile)) { throw 'Run setup.ps1 first (.env missing).' }
if (!(Test-Path -LiteralPath $composePath)) { throw 'compose.app.yml missing.' }
if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI not found. Start Docker Desktop and retry.' }

$composeArgs = @('compose','--project-name',$project,'--env-file',$envFile,'-f',$composePath)

# Docker writes progress and warnings to stderr. PowerShell 5.1 turns native
# stderr into terminating errors under ErrorActionPreference=Stop, so all Docker
# calls go through this helper and are judged by their exit code instead.
function Invoke-Docker {
    param([string[]]$Arguments, [switch]$Capture)
    $previous = $ErrorActionPreference
    try {
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
    }
}

$config = Invoke-Docker -Arguments ($composeArgs + @('config','--quiet')) -Capture
if ($config.ExitCode -ne 0) { throw "compose.app.yml is invalid.`n$($config.Text.Trim())" }

$map = @{
    User        = 'user-service'
    Parking     = 'parking-service'
    Operation   = 'reservation-service'
    Frontend    = 'frontend'
}
$targets = if ($Service -eq 'All') {
    @('user-service','parking-service','reservation-service','frontend')
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
$upExit = Invoke-Docker -Arguments ($composeArgs + $upArgs)
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
Write-Host '  Frontend    http://localhost:5173'
Write-Host ''

if (!$NoLogWindow) {
    foreach ($target in $targets) {
        $logCommand = "title SmartPark - $target && docker compose --project-name $project --env-file `"$envFile`" -f `"$composePath`" logs -f --tail 200 $target"
        Start-Process -FilePath 'cmd.exe' -ArgumentList '/k', $logCommand -WorkingDirectory $root -WindowStyle Hidden | Out-Null
    }
}
