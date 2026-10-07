param([Parameter(Mandatory)][ValidateSet('User','Parking','Reservation')][string]$Service)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (!$env:SMARTPARK_DEV_PASSWORD) { throw 'Set SMARTPARK_DEV_PASSWORD in this terminal.' }
if (!$env:SMARTPARK_SERVICE_KEY -or $env:SMARTPARK_SERVICE_KEY.Length -lt 32) { throw 'Set SMARTPARK_SERVICE_KEY (at least 32 characters) in this terminal.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Services__Key = $env:SMARTPARK_SERVICE_KEY
$env:Services__User = 'http://localhost:5035/'
$env:Services__Parking = 'http://localhost:5045/'
$env:Services__Reservation = 'http://localhost:5055/'
$dbPort = @{ User=5441; Parking=5442; Reservation=5443 }[$Service]
$apiPort = @{ User=5035; Parking=5045; Reservation=5055 }[$Service]
$dbName = 'smartpark_' + $Service.ToLowerInvariant()
[Environment]::SetEnvironmentVariable('ConnectionStrings__' + $Service, "Host=127.0.0.1;Port=$dbPort;Database=$dbName;Username=$dbName;Password=$env:SMARTPARK_DEV_PASSWORD", 'Process')
if ($Service -eq 'User' -and $env:SMARTPARK_ADMIN_PASSWORD) {
    $env:DevelopmentAdmin__Email = 'admin@smartpark.local'
    $env:DevelopmentAdmin__Password = $env:SMARTPARK_ADMIN_PASSWORD
}
$projectDir = Join-Path $taskRoot "src/Services/${Service}Service/SmartParking.${Service}Service.API"
dotnet run --project $projectDir --no-restore --no-launch-profile -- --contentRoot $projectDir --urls "http://localhost:$apiPort"
if ($LASTEXITCODE -ne 0) { throw "${Service}Service exited with code $LASTEXITCODE" }
