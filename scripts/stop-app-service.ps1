param(
    [ValidateSet('User','Parking','Reservation','Payment','Notification','Frontend','All')][string]$Service = 'All'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $root '.env'
$composePath = Join-Path $root 'compose.app.yml'
$project = 'smartpark-app'

if (!(Test-Path -LiteralPath $envFile)) { throw 'Run setup.ps1 first (.env missing).' }
if (!(Test-Path -LiteralPath $composePath)) { throw 'compose.app.yml missing.' }
if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI not found. Start Docker Desktop and retry.' }

$composeArgs = @('compose','--project-name',$project,'--env-file',$envFile,'-f',$composePath)
$map = @{
    User        = 'user-service'
    Parking     = 'parking-service'
    Reservation = 'reservation-service'
    Payment      = 'payment-service'
    Notification = 'notification-service'
    Frontend    = 'frontend'
}

function Invoke-Docker {
    param([string[]]$Arguments)
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & docker @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        return $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
}

if ($Service -eq 'All') {
    $exitCode = Invoke-Docker -Arguments ($composeArgs + @('down','--remove-orphans'))
} else {
    $exitCode = Invoke-Docker -Arguments ($composeArgs + @('stop',$map[$Service]))
}
if ($exitCode -ne 0) { throw 'docker compose stop failed.' }

Write-Host "Stopped SmartPark app service(s): $Service." -ForegroundColor Green
