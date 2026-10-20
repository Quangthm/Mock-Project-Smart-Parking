# =============================================================================
# setup.ps1 - SmartPark local onboarding (Windows PowerShell 5.1 compatible).
#
# Single source of truth for local setup. Idempotent: safe to re-run; nothing
# is overwritten without confirmation and no container/network/volume is
# created twice. Configuration is read from <repo>\.env; setup echoes the
# loaded values to the console per team decision (do not share the output).
# =============================================================================
$ErrorActionPreference = 'Stop'

# Repository root = the directory that contains this script (no hardcoded paths).
$repoRoot = $PSScriptRoot
$envFile = Join-Path $repoRoot '.env'
$envExampleFile = Join-Path $repoRoot '.env.example'
$loadEnvScript = Join-Path $repoRoot 'scripts\load-env.ps1'

$script:StepResults = @()
$script:CurrentStep = 0
$script:ComposeStyle = 'plugin'   # 'plugin' => docker compose ; 'legacy' => docker-compose

# Shared team defaults. This script is gitignored and distributed internally,
# so these values are applied whenever .env is created/overwritten from the
# tracked .env.example template (which keeps only placeholders).
$script:SharedEnvDefaults = @{
    'SMARTPARK_SERVICE_KEY'    = 'adk+XcMJKURPti4o/TRyX+cspV5ftV0vGWEU+ogQkEE='
    'SMARTPARK_ADMIN_PASSWORD' = 'Admin@123'
    'SONAR_DB_PASSWORD'        = 'sonar_db_Password@123'
}

# Shared MapTiler key for the frontend; written into FE\.env on first bootstrap.
$script:MapTilerApiKey = 'tpX28VpW87LtzYmUWk8A'

# -----------------------------------------------------------------------------
# Logging / step helpers
# -----------------------------------------------------------------------------
function Complete-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Status,
        [string]$Detail = '',
        [string[]]$Lines = @()
    )
    $head = "[$script:CurrentStep/7] $Name ... $Status"
    if ($Detail) { $head = "$head - $Detail" }

    $color = 'Green'
    if ($Status -eq 'FAIL') { $color = 'Red' }
    elseif ($Status -eq 'SKIP') { $color = 'Yellow' }
    Write-Host $head -ForegroundColor $color
    foreach ($line in $Lines) { Write-Host "    $line" }

    $script:StepResults += New-Object PSObject -Property @{
        Index  = $script:CurrentStep
        Name   = $Name
        Status = $Status
        Detail = $Detail
    }
}

function Show-StepSummary {
    Write-Host ''
    Write-Host 'Step summary' -ForegroundColor Cyan
    foreach ($stepResult in $script:StepResults) {
        $color = 'Green'
        if ($stepResult.Status -eq 'FAIL') { $color = 'Red' }
        elseif ($stepResult.Status -eq 'SKIP') { $color = 'Yellow' }
        $detail = ''
        if ($stepResult.Detail) { $detail = '  ' + $stepResult.Detail }
        Write-Host ("  [{0}/7] {1,-15} {2}{3}" -f $stepResult.Index, $stepResult.Name, $stepResult.Status, $detail) -ForegroundColor $color
    }
}

function Invoke-SetupStep {
    param(
        [Parameter(Mandatory)][int]$Index,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Body
    )
    $script:CurrentStep = $Index
    $status = 'OK'
    $detail = ''
    $lines = @()

    try {
        $result = & $Body
        if ($null -ne $result) {
            if ($result.Status) { $status = [string]$result.Status }
            if ($result.Detail) { $detail = [string]$result.Detail }
            if ($result.Lines) { $lines = @($result.Lines) }
        }
    } catch {
        $status = 'FAIL'
        $detail = $_.Exception.Message
    }

    Complete-Step -Name $Name -Status $status -Detail $detail -Lines $lines

    if ($status -eq 'FAIL') {
        Write-Host ''
        Write-Host "Setup stopped at step [$Index/7]. Fix the issue above, then run setup.ps1 again." -ForegroundColor Red
        Show-StepSummary
        exit 1
    }
}

# Runs a native command, temporarily relaxing $ErrorActionPreference so redirected
# stderr is swallowed instead of becoming a terminating error on PowerShell 5.1.
function Invoke-SwallowedCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @()
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $exitCode = 0
    try {
        $null = & $FilePath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } catch {
        $exitCode = -1
    } finally {
        $ErrorActionPreference = $previous
    }
    return $exitCode
}

function Get-ToolVersion {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @()
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $text = ''
    $exitCode = -1
    try {
        $text = (& $FilePath @Arguments 2>&1 | Out-String).Trim()
        $exitCode = $LASTEXITCODE
    } catch {
        $text = $_.Exception.Message
        $exitCode = -1
    } finally {
        $ErrorActionPreference = $previous
    }
    return New-Object PSObject -Property @{ ExitCode = $exitCode; Text = $text }
}

function Invoke-Compose {
    param([Parameter(Mandatory)][string[]]$ComposeArguments)
    # Send output to the host so it is shown live and stays out of the step result.
    if ($script:ComposeStyle -eq 'legacy') {
        & docker-compose @ComposeArguments | Out-Host
    } else {
        & docker compose @ComposeArguments | Out-Host
    }
    $script:LastComposeExitCode = $LASTEXITCODE
}

function Test-Placeholder {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value.Trim().StartsWith('replace-with-')
}

# Replaces (or appends) a "KEY=value" line in an env file, quoting the value,
# and writes the file back as UTF-8 without a BOM (PowerShell 5.1 has no
# utf8NoBOM encoding name). Returns $true on success.
function Set-EnvKeyValue {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Value
    )
    $lines = @(Get-Content -LiteralPath $Path)
    $pattern = '^' + [regex]::Escape($Key) + '='
    $updated = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $pattern) {
            $lines[$i] = $Key + '="' + $Value + '"'
            $updated = $true
        }
    }
    if (!$updated) { $lines += ($Key + '="' + $Value + '"') }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($Path, $lines, $encoding)
    return $true
}

# =============================================================================
# [0/7] Prerequisites
# =============================================================================
Invoke-SetupStep -Index 0 -Name 'Prerequisites' -Body {
    $problems = @()
    $versions = @()

    $dockerFound = [bool](Get-Command docker -ErrorAction SilentlyContinue)
    if (!$dockerFound) {
        $problems += 'docker CLI not found. Install Docker Desktop for Windows.'
    } else {
        $dockerProbe = Get-ToolVersion -FilePath 'docker' -Arguments @('--version')
        if ($dockerProbe.ExitCode -ne 0) {
            $problems += ('docker CLI is not working: ' + $dockerProbe.Text)
        } else {
            $versions += $dockerProbe.Text
        }
    }

    if ($dockerFound) {
        $composeProbe = Get-ToolVersion -FilePath 'docker' -Arguments @('compose', 'version')
        if ($composeProbe.ExitCode -eq 0) {
            $script:ComposeStyle = 'plugin'
            $versions += $composeProbe.Text
        } elseif (Get-Command docker-compose -ErrorAction SilentlyContinue) {
            $legacyProbe = Get-ToolVersion -FilePath 'docker-compose' -Arguments @('--version')
            if ($legacyProbe.ExitCode -eq 0) {
                $script:ComposeStyle = 'legacy'
                $versions += ($legacyProbe.Text + ' (legacy docker-compose)')
            } else {
                $problems += 'Docker Compose not found (tried "docker compose" and "docker-compose").'
            }
        } else {
            $problems += 'Docker Compose not found. Update Docker Desktop to get the Compose plugin.'
        }
    }

    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
        $problems += 'dotnet SDK not found. Install the .NET SDK 10.'
    } else {
        $dotnetProbe = Get-ToolVersion -FilePath 'dotnet' -Arguments @('--version')
        if ($dotnetProbe.ExitCode -ne 0) {
            $problems += ('dotnet CLI is not working: ' + $dotnetProbe.Text)
        } else {
            $versions += ('.NET SDK ' + $dotnetProbe.Text)
            $dotnetMajor = 0
            $majorText = $dotnetProbe.Text.Split('.')[0]
            if ([int]::TryParse($majorText, [ref]$dotnetMajor)) {
                if ($dotnetMajor -ne 10) {
                    $versions += ('.NET SDK ' + $dotnetMajor + '.x detected; the repository targets net10.0.')
                }
            }
        }
    }

    $psVersion = $PSVersionTable.PSVersion
    if ($psVersion.Major -lt 5 -or ($psVersion.Major -eq 5 -and $psVersion.Minor -lt 1)) {
        $problems += ("Windows PowerShell 5.1 or newer is required (found " + $psVersion + ').')
    } else {
        $versions += ('Windows PowerShell ' + $psVersion)
    }

    if ($problems.Count -gt 0) {
        return [pscustomobject]@{ Status = 'FAIL'; Detail = ($problems.Count.ToString() + ' prerequisite(s) missing'); Lines = $problems }
    }
    return [pscustomobject]@{ Status = 'OK'; Detail = 'docker, compose, dotnet, powershell'; Lines = $versions }
}

# =============================================================================
# [1/7] Env bootstrap
# =============================================================================
Invoke-SetupStep -Index 1 -Name 'Env bootstrap' -Body {
    $lines = @()
    $fromTemplate = $false
    $detail = ''

    if (!(Test-Path -LiteralPath $envFile -PathType Leaf)) {
        if (!(Test-Path -LiteralPath $envExampleFile -PathType Leaf)) {
            throw "Template not found: $envExampleFile"
        }
        Copy-Item -LiteralPath $envExampleFile -Destination $envFile
        $fromTemplate = $true
        $detail = 'created from .env.example'
    } else {
        while ($true) {
            $answer = Read-Host '.env already exists - [O]verwrite from template, [K]eep current, or [A]bort'
            if ($null -eq $answer) { $answer = 'k' }
            $answer = $answer.Trim()
            if (!$answer) { $answer = 'k' }
            $lower = $answer.ToLowerInvariant()

            if ($lower -eq 'k' -or $lower -eq 'keep') {
                $detail = 'kept existing .env'
                break
            }
            if ($lower -eq 'o' -or $lower -eq 'overwrite') {
                Copy-Item -LiteralPath $envExampleFile -Destination $envFile -Force
                $fromTemplate = $true
                $detail = 'overwritten from .env.example'
                break
            }
            if ($lower -eq 'a' -or $lower -eq 'abort') {
                return [pscustomobject]@{ Status = 'FAIL'; Detail = 'aborted by user at the .env prompt' }
            }
            Write-Host 'Please answer O (overwrite), K (keep) or A (abort).'
        }
        if (!$fromTemplate) {
            $lines += 'Existing .env kept as-is; shared team defaults were not applied.'
        }
    }

    if ($fromTemplate) {
        foreach ($sharedKey in $script:SharedEnvDefaults.Keys) {
            $null = Set-EnvKeyValue -Path $envFile -Key $sharedKey -Value $script:SharedEnvDefaults[$sharedKey]
        }
        $lines += ('Shared team defaults applied: ' + (($script:SharedEnvDefaults.Keys) -join ', '))
        $lines += 'Replace the remaining "replace-with-..." placeholders before running the services.'
    }

    # Frontend env: create FE\.env from the template with the shared MapTiler key.
    $feDir = Join-Path $repoRoot 'FE'
    $feEnvFile = Join-Path $feDir '.env'
    $feExampleFile = Join-Path $feDir '.env.example'
    if (!(Test-Path -LiteralPath $feExampleFile -PathType Leaf)) {
        $lines += 'FE\.env.example not found; frontend env bootstrap skipped.'
    } elseif (Test-Path -LiteralPath $feEnvFile -PathType Leaf) {
        $lines += 'FE\.env already exists; kept as-is.'
    } else {
        Copy-Item -LiteralPath $feExampleFile -Destination $feEnvFile
        $null = Set-EnvKeyValue -Path $feEnvFile -Key 'VITE_MAPTILER_API_KEY' -Value $script:MapTilerApiKey
        $lines += 'FE\.env created from FE\.env.example with the shared VITE_MAPTILER_API_KEY.'
    }

    return [pscustomobject]@{ Status = 'OK'; Detail = $detail; Lines = $lines }
}

# =============================================================================
# [2/7] Validate
# =============================================================================
Invoke-SetupStep -Index 2 -Name 'Validate' -Body {
    if (!(Test-Path -LiteralPath $envFile -PathType Leaf)) {
        throw "Missing $envFile. .env bootstrap (step 1) did not produce one."
    }

    # Dot-source the shared parser: it exports every key into the process
    # environment and returns the parsed map.
    $envMap = . $loadEnvScript -EnvPath $envFile

    $issues = @()
    $warnings = @()
    $placeholderKeys = @()

    $devPassword = $envMap['SMARTPARK_DEV_PASSWORD']
    if ([string]::IsNullOrWhiteSpace($devPassword)) {
        $issues += 'SMARTPARK_DEV_PASSWORD is empty.'
    } elseif (Test-Placeholder $devPassword) {
        $issues += 'SMARTPARK_DEV_PASSWORD still uses the .env.example placeholder.'
    }

    $serviceKey = $envMap['SMARTPARK_SERVICE_KEY']
    if ([string]::IsNullOrWhiteSpace($serviceKey)) {
        $issues += 'SMARTPARK_SERVICE_KEY is missing.'
    } elseif (Test-Placeholder $serviceKey) {
        $issues += 'SMARTPARK_SERVICE_KEY still uses the .env.example placeholder.'
    } elseif ($serviceKey.Length -lt 32) {
        $issues += ('SMARTPARK_SERVICE_KEY must be at least 32 characters (current length: ' + $serviceKey.Length + ').')
    }

    # Optional settings: warn only.
    $smtpKeys = @('SMARTPARK_SMTP_HOST', 'SMARTPARK_SMTP_USERNAME', 'SMARTPARK_SMTP_PASSWORD', 'SMARTPARK_SMTP_FROM')
    $smtpMissing = @()
    foreach ($smtpKey in $smtpKeys) {
        if ([string]::IsNullOrWhiteSpace($envMap[$smtpKey])) { $smtpMissing += $smtpKey }
    }
    if ($smtpMissing.Count -gt 0) {
        $warnings += ('SMTP settings incomplete (' + ($smtpMissing -join ', ') + '); OTP emails will not be sent locally.')
    }
    if ([string]::IsNullOrWhiteSpace($envMap['SMARTPARK_JWT_PRIVATE_KEY_PEM'])) {
        $warnings += 'SMARTPARK_JWT_PRIVATE_KEY_PEM is empty; UserService will use its development signing key.'
    }

    # Report remaining placeholders by key name only (never print values).
    foreach ($mapKey in $envMap.Keys) {
        if (Test-Placeholder $envMap[$mapKey]) { $placeholderKeys += $mapKey }
    }
    if ($placeholderKeys.Count -gt 0) {
        $warnings += ('Still using .env.example placeholders: ' + ($placeholderKeys -join ', '))
    }

    # Per team decision, echo the loaded configuration with values.
    $loadedLines = @('Loaded configuration (values shown per team decision):')
    foreach ($mapKey in ($envMap.Keys | Sort-Object)) {
        $loadedLines += ($mapKey + ' = ' + $envMap[$mapKey])
    }

    if ($issues.Count -gt 0) {
        return [pscustomobject]@{
            Status = 'FAIL'
            Detail = ($issues.Count.ToString() + ' required setting(s) invalid')
            Lines  = @($issues + $warnings + $loadedLines)
        }
    }
    return [pscustomobject]@{
        Status = 'OK'
        Detail = ($envMap.Count.ToString() + ' keys loaded')
        Lines  = @($warnings + $loadedLines)
    }
}

# =============================================================================
# [3/7] Docker DB
# =============================================================================
Invoke-SetupStep -Index 3 -Name 'Docker DB' -Body {
    $composeFile = Join-Path $repoRoot 'compose.schema-integration.yml'
    if (!(Test-Path -LiteralPath $composeFile -PathType Leaf)) {
        throw "Missing compose file: $composeFile"
    }
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DEV_PASSWORD)) {
        throw 'SMARTPARK_DEV_PASSWORD is not available in the process environment (see step 2).'
    }

    # Keep the documented defaults when .env does not define the host ports.
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DB_USER_PORT)) { $env:SMARTPARK_DB_USER_PORT = '5441' }
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DB_PARKING_PORT)) { $env:SMARTPARK_DB_PARKING_PORT = '5442' }
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DB_RESERVATION_PORT)) { $env:SMARTPARK_DB_RESERVATION_PORT = '5443' }
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DB_PAYMENT_PORT)) { $env:SMARTPARK_DB_PAYMENT_PORT = '5444' }
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DB_NOTIFICATION_PORT)) { $env:SMARTPARK_DB_NOTIFICATION_PORT = '5445' }
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_DB_IOT_PORT)) { $env:SMARTPARK_DB_IOT_PORT = '5446' }

    Write-Host '    Starting the six PostgreSQL containers (docker compose up -d --wait); this can take a minute...'
    Invoke-Compose -ComposeArguments @('-f', $composeFile, 'up', '-d', '--wait')
    if ($script:LastComposeExitCode -ne 0) {
        throw 'docker compose up failed. Is Docker Desktop running, and is .env configured (step 2)?'
    }
    return [pscustomobject]@{ Status = 'OK'; Detail = 'user-db, parking-db, reservation-db, payment-db, notification-db, iot-db ready' }
}

# =============================================================================
# [4/7] Migrations
# =============================================================================
Invoke-SetupStep -Index 4 -Name 'Migrations' -Body {
    $migrationScript = Join-Path $repoRoot 'scripts\apply-workflow-migrations.ps1'
    if (!(Test-Path -LiteralPath $migrationScript -PathType Leaf)) {
        throw "Missing migration script: $migrationScript"
    }
    foreach ($serviceName in @('User', 'Parking', 'Reservation')) {
        & $migrationScript -Service $serviceName -SchemaRoot $repoRoot | Out-Host
    }
    return [pscustomobject]@{ Status = 'OK'; Detail = 'workflow migrations applied for all 3 services' }
}

# =============================================================================
# [5/7] SonarQube
# =============================================================================
Invoke-SetupStep -Index 5 -Name 'SonarQube' -Body {
    $networkName = 'smartpark-net'

    $networkNames = & docker network ls --format '{{.Name}}'
    if ($networkNames -notcontains $networkName) {
        Invoke-SwallowedCommand -FilePath 'docker' -Arguments @('network', 'create', $networkName) | Out-Null
    }
    $networkNames = & docker network ls --format '{{.Name}}'
    if ($networkNames -notcontains $networkName) {
        throw "Could not create Docker network $networkName. Is Docker Desktop running?"
    }

    $sonarDbName = $env:SONAR_DB_NAME
    if ([string]::IsNullOrWhiteSpace($sonarDbName)) { $sonarDbName = 'sonar' }
    $sonarDbUser = $env:SONAR_DB_USER
    if ([string]::IsNullOrWhiteSpace($sonarDbUser)) { $sonarDbUser = 'sonar' }
    $sonarDbPassword = $env:SONAR_DB_PASSWORD
    if ([string]::IsNullOrWhiteSpace($sonarDbPassword)) {
        throw 'SONAR_DB_PASSWORD is empty in .env. Set it and run setup.ps1 again.'
    }
    $sonarPort = $env:SONAR_PORT
    if ([string]::IsNullOrWhiteSpace($sonarPort)) { $sonarPort = '9000' }

    $lines = @()
    if (Test-Placeholder $sonarDbPassword) {
        $lines += 'SONAR_DB_PASSWORD still uses the .env.example placeholder.'
    }

    $containerNames = & docker ps -a --format '{{.Names}}'
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not list Docker containers. Is Docker Desktop running?'
    }

    if ($containerNames -contains 'sonarqube-db') {
        Invoke-SwallowedCommand -FilePath 'docker' -Arguments @('start', 'sonarqube-db') | Out-Null
        $dbAction = 'reused'
    } else {
        & docker run -d --name sonarqube-db --network $networkName `
            -e POSTGRES_USER=$sonarDbUser -e POSTGRES_DB=$sonarDbName -e POSTGRES_PASSWORD=$sonarDbPassword `
            -v sonarqube_db_data:/var/lib/postgresql/data `
            postgres:16-alpine | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to start the sonarqube-db container.' }
        $dbAction = 'created'
    }
    # Make sure reused containers sit on the shared network too (idempotent).
    Invoke-SwallowedCommand -FilePath 'docker' -Arguments @('network', 'connect', $networkName, 'sonarqube-db') | Out-Null

    if ($containerNames -contains 'sonarqube') {
        Invoke-SwallowedCommand -FilePath 'docker' -Arguments @('start', 'sonarqube') | Out-Null
        $webAction = 'reused'
    } else {
        $jdbcUrl = "jdbc:postgresql://sonarqube-db:5432/$sonarDbName"
        & docker run -d --name sonarqube --network $networkName -p "${sonarPort}:9000" `
            -e "SONAR_JDBC_URL=$jdbcUrl" `
            -e "SONAR_JDBC_USERNAME=$sonarDbUser" `
            -e "SONAR_JDBC_PASSWORD=$sonarDbPassword" `
            -e SONAR_ES_BOOTSTRAP_CHECKS_DISABLE=true `
            -v sonarqube_data_extensions:/opt/sonarqube/extensions `
            -v sonarqube_data_logs:/opt/sonarqube/logs `
            --restart unless-stopped `
            sonarqube:community | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to start the sonarqube container.' }
        $webAction = 'created'
    }

    $lines += "SonarQube Community UI: http://localhost:$sonarPort (container: $webAction, db: $dbAction)."
    $lines += 'Community edition defaults to admin/admin; change the password on first login (it cannot be set via environment variables).'
    $lines += 'If the port or database settings changed, remove the existing containers and re-run.'
    return [pscustomobject]@{ Status = 'OK'; Detail = "network $networkName, sonarqube-db $dbAction, sonarqube $webAction"; Lines = $lines }
}

# =============================================================================
# [6/7] User-secrets
# =============================================================================
Invoke-SetupStep -Index 6 -Name 'User-secrets' -Body {
    if ([string]::IsNullOrWhiteSpace($env:SMARTPARK_SERVICE_KEY) -or $env:SMARTPARK_SERVICE_KEY.Length -lt 32) {
        throw 'SMARTPARK_SERVICE_KEY is missing or shorter than 32 characters (see step 2).'
    }

    $dbPortByService = @{
        User        = $env:SMARTPARK_DB_USER_PORT
        Parking     = $env:SMARTPARK_DB_PARKING_PORT
        Reservation = $env:SMARTPARK_DB_RESERVATION_PORT
    }
    $defaultDbPortByService = @{ User = '5441'; Parking = '5442'; Reservation = '5443' }

    $sharedSettings = [ordered]@{
        'Services:Key'         = $env:SMARTPARK_SERVICE_KEY
        'Services:User'        = 'http://localhost:5035/'
        'Services:Parking'     = 'http://localhost:5045/'
        'Services:Reservation' = 'http://localhost:5055/'
    }

    $lines = @('WARNING: secret values are printed in plaintext; do not share terminal output.')
    foreach ($serviceName in @('User', 'Parking', 'Reservation')) {
        $projectDir = Join-Path $repoRoot "src\Services\${serviceName}Service\SmartParking.${serviceName}Service.API"
        if (!(Test-Path -LiteralPath $projectDir -PathType Container)) {
            throw "Project directory not found: $projectDir"
        }

        $settings = [ordered]@{}
        foreach ($sharedKey in $sharedSettings.Keys) { $settings[$sharedKey] = $sharedSettings[$sharedKey] }

        $dbPort = $dbPortByService[$serviceName]
        if ([string]::IsNullOrWhiteSpace($dbPort)) { $dbPort = $defaultDbPortByService[$serviceName] }
        $dbName = 'smartpark_' + $serviceName.ToLowerInvariant()
        # Quote the password for Npgsql syntax so ';' inside a password is safe.
        $quotedPassword = $env:SMARTPARK_DEV_PASSWORD.Replace('"', '""')
        $settings["ConnectionStrings:$serviceName"] = "Host=127.0.0.1;Port=$dbPort;Database=$dbName;Username=$dbName;Password=`"$quotedPassword`""

        if ($serviceName -eq 'User') {
            $adminEmail = $env:SMARTPARK_ADMIN_EMAIL
            if ([string]::IsNullOrWhiteSpace($adminEmail)) { $adminEmail = 'admin@smartpark.local' }
            $settings['DevelopmentAdmin:Email'] = $adminEmail
            if (![string]::IsNullOrWhiteSpace($env:SMARTPARK_ADMIN_PASSWORD)) {
                $settings['DevelopmentAdmin:Password'] = $env:SMARTPARK_ADMIN_PASSWORD
            }
            if (![string]::IsNullOrWhiteSpace($env:SMARTPARK_SMTP_HOST)) { $settings['Otp:Smtp:Host'] = $env:SMARTPARK_SMTP_HOST }
            if (![string]::IsNullOrWhiteSpace($env:SMARTPARK_SMTP_PORT)) { $settings['Otp:Smtp:Port'] = $env:SMARTPARK_SMTP_PORT }
            if (![string]::IsNullOrWhiteSpace($env:SMARTPARK_SMTP_USERNAME)) { $settings['Otp:Smtp:Username'] = $env:SMARTPARK_SMTP_USERNAME }
            if (![string]::IsNullOrWhiteSpace($env:SMARTPARK_SMTP_PASSWORD)) { $settings['Otp:Smtp:Password'] = $env:SMARTPARK_SMTP_PASSWORD }
            if (![string]::IsNullOrWhiteSpace($env:SMARTPARK_SMTP_FROM)) { $settings['Otp:Smtp:From'] = $env:SMARTPARK_SMTP_FROM }
            if (![string]::IsNullOrWhiteSpace($env:SMARTPARK_JWT_PRIVATE_KEY_PEM)) { $settings['Jwt:PrivateKeyPem'] = $env:SMARTPARK_JWT_PRIVATE_KEY_PEM }
            $workflowDir = $env:SMARTPARK_WORKFLOW_KEY_DIRECTORY
            if ([string]::IsNullOrWhiteSpace($workflowDir)) { $workflowDir = '.cache/workflow-keys' }
            $settings['Workflow:KeyDirectory'] = $workflowDir
        }

        # Piped JSON keeps secrets out of command-line arguments; the set
        # output is discarded (values are echoed later from user-secrets list
        # per team decision).
        $previousEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $settings | ConvertTo-Json | & dotnet user-secrets set --project $projectDir | Out-Null
            $setExitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousEap
        }
        if ($setExitCode -ne 0) { throw "dotnet user-secrets set failed for ${serviceName}Service." }

        $listProbe = Get-ToolVersion -FilePath 'dotnet' -Arguments @('user-secrets', 'list', '--project', $projectDir)
        if ($listProbe.ExitCode -ne 0) { throw "dotnet user-secrets list failed for ${serviceName}Service." }

        $secretLines = @()
        foreach ($listLine in ($listProbe.Text -split "`r?`n")) {
            $text = $listLine.TrimEnd()
            if (![string]::IsNullOrWhiteSpace($text)) { $secretLines += $text }
        }
        $lines += "${serviceName}Service user secrets (values shown per team decision):"
        foreach ($secretLine in $secretLines) { $lines += $secretLine }
    }

    return [pscustomobject]@{ Status = 'OK'; Detail = '3 projects configured'; Lines = $lines }
}

# =============================================================================
# [7/7] Summary
# =============================================================================
Invoke-SetupStep -Index 7 -Name 'Summary' -Body {
    Show-StepSummary
    Write-Host ''
    Write-Host 'Next steps' -ForegroundColor Cyan
    Write-Host '  1. Frontend: FE\.env was created with the shared VITE_MAPTILER_API_KEY; run npm install && npm run dev in FE\.'
    Write-Host '  2. Run an API: .\scripts\run-schema-service.ps1 -Service User   (or Parking / Reservation).'
    Write-Host '  3. API ports: User 5035, Parking 5045, Reservation 5055.'
    $sonarPort = $env:SONAR_PORT
    if ([string]::IsNullOrWhiteSpace($sonarPort)) { $sonarPort = '9000' }
    Write-Host "  4. SonarQube: http://localhost:$sonarPort - log in as admin/admin and change the password."
    Write-Host '  5. setup.ps1 is idempotent; re-run it any time.'
    return [pscustomobject]@{ Status = 'OK'; Detail = 'setup complete' }
}
