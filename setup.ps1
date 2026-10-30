param(
    [switch]$SkipDatabase, [switch]$SkipRestore, [switch]$SkipUserSecrets, [switch]$RecreateDb,
    [ValidateSet('Local','Team')][string]$CredentialMode = 'Local',
    [string]$TeamEnvPath, [switch]$SkipSonarQube, [switch]$NonInteractive,
    [switch]$WithAiCamera, [switch]$RebuildAi,
    # Deprecated: SonarQube now starts by default; kept so older commands still parse.
    [switch]$WithSonarQube
)
# -RecreateDb: DROP schema volumes and re-initialize all 6 DBs from SQL init scripts. Destructive — wipes dev data. Required when DB schema layout changes (e.g., SRS v1.0 hybrid migration).
$ErrorActionPreference = 'Stop'
if ($RebuildAi -and !$WithAiCamera) { throw '-RebuildAi requires -WithAiCamera.' }
# SonarQube credentials are always prepared (like the database passwords); the
# stack starts on a full run. -SkipSonarQube disables both; -SkipDatabase still
# prepares credentials but starts no containers (it must stay Docker-free).
$sonarEnabled = !$SkipSonarQube
$taskRoot = $PSScriptRoot
$composeFile = Join-Path $taskRoot 'compose.schema-integration.yml'
$envFile = Join-Path $taskRoot '.env'
$aiModelRoot = 'src/Services/IoTService/python-ai/models'
$aiModelFiles = @(
    'EDSR_x2.pb', 'ESPCN_x2.pb', 'FSRCNN_x2.pb',
    'license_plate_detector.pt', 'license_plate_detector_live.pt',
    'yolov8n.pt', 'yolov8s.pt'
)
$script:AiCameraSkipped = $false

function New-LocalSecret([int]$Bytes = 32) {
    $buffer = New-Object byte[] $Bytes
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($buffer) } finally { $random.Dispose() }
    [Convert]::ToBase64String($buffer)
}

function Assert-DevelopmentPassword([string]$Name, [string]$Value) {
    if ([string]::IsNullOrEmpty($Value) -or $Value.Length -lt 8 -or $Value.Length -gt 15 -or
        $Value -cnotmatch '^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$') {
        throw "$Name requires 8-15 characters, uppercase, lowercase, digit and special character."
    }
}

# Parse Password from a secrets connection string. DbConnectionStringBuilder can fail to
# tokenize some raw values (it collapses them into a single "ConnectionString" key), so
# fall back to explicit Password= regex matching.
function Get-SecretConnectionStringPassword([string]$ConnectionString) {
    if ([string]::IsNullOrEmpty($ConnectionString)) { return $null }
    try {
        $builder = New-Object System.Data.Common.DbConnectionStringBuilder
        $builder.ConnectionString = $ConnectionString
        if ($builder.ContainsKey('Password')) { return [string]$builder['Password'] }
    } catch { }
    if ($ConnectionString -match 'Password="([^"]*)"') { return $Matches[1] }
    if ($ConnectionString -match 'Password=([^;]+)') { return $Matches[1] }
    return $null
}

function Test-AiModelAssets {
    param([string]$Root = $taskRoot)
    $missing = @()
    foreach ($name in $aiModelFiles) {
        $relative = "$aiModelRoot/$name"
        if (!(Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) { $missing += $relative }
    }
    return $missing
}

function Assert-AiModelAssets {
    $missing = @(Test-AiModelAssets)
    if ($missing.Count -gt 0) {
        $list = ($missing | ForEach-Object { "  - $_" }) -join [Environment]::NewLine
        throw ("AI model assets are missing:`n$list`nRestore the tracked files under $aiModelRoot before using -WithAiCamera.")
    }
    Write-Host '    - AI model assets verified (7 files).' -ForegroundColor DarkGray
}

# Run a Docker query while capturing stderr for diagnostics. Native command stderr is
# redirected only with ErrorActionPreference=Continue so expected "not found" output
# cannot terminate setup under the script-wide Stop preference.
function Invoke-DockerQuery {
    param([string[]]$Arguments)
    $previousErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& docker @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorAction
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output   = $output
        Text     = (@($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine)
    }
}

function Get-AiImageState {
    $result = Invoke-DockerQuery -Arguments @('image','inspect','smartpark-ai:local','--format','{{.Id}}|{{.Created}}|{{.Size}}')
    if ($result.ExitCode -ne 0) {
        if ($result.Text -match 'No such image|No such object|not found') { return $null }
        throw "Cannot inspect AI image smartpark-ai:local: $($result.Text.Trim())"
    }
    $line = @($result.Output | ForEach-Object { [string]$_ } | Where-Object { $_.Trim() } | Select-Object -First 1)
    if (!$line -or !$line[0]) { throw 'Cannot read AI image metadata from docker image inspect.' }
    $parts = ([string]$line[0]).Split('|')
    if ($parts.Count -lt 3 -or !$parts[0]) { throw "Unexpected docker image inspect output: $($line[0])" }
    $sizeBytes = 0L
    if (![long]::TryParse($parts[2], [ref]$sizeBytes)) { throw "Unexpected AI image size: $($parts[2])" }
    $shortId = $parts[0]
    if ($shortId.StartsWith('sha256:')) { $shortId = $shortId.Substring(7) }
    if ($shortId.Length -gt 12) { $shortId = $shortId.Substring(0, 12) }
    return [pscustomobject]@{
        Id        = $parts[0]
        Created   = $parts[1]
        SizeBytes = $sizeBytes
        SizeGb    = [Math]::Round($sizeBytes / 1e9, 2)
        ShortId   = $shortId
    }
}

function Test-AiImageLibraries {
    param([int]$TimeoutSeconds = 60)
    $probe = "import cv2,numpy,scipy,torch,paddleocr,ultralytics,psycopg,flask; print('AI_IMAGE_OK')"
    $process = $null
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = 'docker'
        $startInfo.Arguments = 'run --rm --entrypoint python smartpark-ai:local -c "' + $probe + '"'
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        if (!$process.Start()) { throw 'Process.Start returned false.' }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            Write-Warning "AI image library probe timed out after $TimeoutSeconds seconds. Treating the image as suspect."
            return $false
        }
        $process.WaitForExit()
        $output = $stdoutTask.Result
        $errorText = $stderrTask.Result
        if ($process.ExitCode -ne 0) {
            $detail = if ($errorText) { ": $($errorText.Trim())" } else { '.' }
            Write-Warning "AI image library probe failed (exit $($process.ExitCode))$detail"
            return $false
        }
        if ($output -notmatch 'AI_IMAGE_OK') {
            Write-Warning "AI image library probe did not print AI_IMAGE_OK. Output: $(([string]$output).Trim())"
            return $false
        }
        Write-Host '    - AI image libraries verified (cv2, numpy, scipy, torch, paddleocr, ultralytics, psycopg, flask).' -ForegroundColor DarkGray
        return $true
    } catch {
        Write-Warning "Cannot run the AI image library probe: $($_.Exception.Message)"
        return $false
    } finally {
        if ($process) { $process.Dispose() }
    }
}

function ConvertFrom-AiComposeJson {
    param([string]$Text)
    $trimmed = $Text.Trim()
    if (!$trimmed) { return @() }
    # Compose emits an array on some versions and one JSON object per line (NDJSON) on others.
    if ($trimmed.StartsWith('[')) { return @($trimmed | ConvertFrom-Json) }
    $items = @()
    foreach ($line in ($trimmed -split "`r?`n")) {
        if (!$line.Trim()) { continue }
        $items += @($line | ConvertFrom-Json)
    }
    return $items
}

function Get-AiStackState {
    param([string]$EnvFile)
    $composePath = Join-Path $taskRoot 'compose.ai.yml'
    $result = Invoke-DockerQuery -Arguments @('compose','-p','smartpark-ai','--env-file',$EnvFile,'-f',$composePath,'ps','--all','--format','json')
    $names = @()
    $fromCompose = $false
    if ($result.ExitCode -eq 0) {
        try {
            foreach ($item in @(ConvertFrom-AiComposeJson -Text $result.Text)) {
                $name = [string]$item.Name
                if (!$name) { $name = [string]$item.Names }
                if ($name) { $names += $name }
            }
            $fromCompose = $true
        } catch {
            Write-Warning "Cannot parse 'docker compose ps --format json'; falling back to docker ps labels. $($_.Exception.Message)"
        }
    } else {
        Write-Warning "docker compose ps failed; falling back to docker ps labels. $($result.Text.Trim())"
    }
    if (!$fromCompose) {
        $fallback = Invoke-DockerQuery -Arguments @('ps','--all','--filter','label=com.docker.compose.project=smartpark-ai','--format','{{.Names}}')
        if ($fallback.ExitCode -ne 0) { throw "Cannot list AI containers: $($fallback.Text.Trim())" }
        $names = @($fallback.Output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
    }

    $containers = @()
    foreach ($name in ($names | Select-Object -Unique)) {
        $inspect = Invoke-DockerQuery -Arguments @('inspect',$name)
        if ($inspect.ExitCode -ne 0) {
            Write-Warning "Cannot inspect AI container $name; it may have been removed. $($inspect.Text.Trim())"
            continue
        }
        $data = @($inspect.Text | ConvertFrom-Json)
        if (!$data.Count) { continue }
        $container = $data[0]
        $labels = $container.Config.Labels
        $project = if ($labels) { [string]$labels.'com.docker.compose.project' } else { '' }
        if ($project -ne 'smartpark-ai') { continue }
        $service = if ($labels) { [string]$labels.'com.docker.compose.service' } else { '' }
        $composeImage = if ($labels) { [string]$labels.'com.docker.compose.image' } else { '' }
        $health = 'none'
        if ($container.State.Health -and $container.State.Health.Status) { $health = [string]$container.State.Health.Status }
        $containers += [pscustomobject]@{
            Name           = ([string]$container.Name).TrimStart('/')
            Service        = $service
            State          = [string]$container.State.Status
            Health         = $health
            ImageId        = [string]$container.Image
            ComposeImageId = $composeImage
        }
    }
    return $containers
}

function Test-AiContainerImageStale {
    param($Container, $Image)
    if (!$Image) { return $false }
    # Prefer the image ID the container is actually running. Compose's
    # com.docker.compose.image label is creation-time metadata and can remain stale
    # even after --force-recreate has recreated the container from the current tag.
    $actualImageId = if ($Container.ImageId) { [string]$Container.ImageId } else { [string]$Container.ComposeImageId }
    return ($actualImageId -and $actualImageId -ne [string]$Image.Id)
}

function Show-AiDockerLogs {
    param([string[]]$ComposeArgs)
    Write-Host '    - Recent AI container logs (tail 200):' -ForegroundColor Yellow
    & docker @ComposeArgs logs --tail 200
    if ($LASTEXITCODE -ne 0) { Write-Warning 'Could not read AI container logs; run the logs command manually.' }
}

function Assert-AiContainersRunning {
    param([string]$EnvFile)
    $after = @(Get-AiStackState -EnvFile $EnvFile)
    $notRunning = @($after | Where-Object { $_.State -ne 'running' })
    if ($after.Count -lt 2 -or $notRunning.Count -gt 0) {
        $states = if ($after.Count -gt 0) { ($after | ForEach-Object { "$($_.Service)=$($_.State)" }) -join ', ' } else { 'no containers found' }
        return "AI camera containers are not running ($states). Check logs and retry."
    }
    return $null
}

function Invoke-AiComposeBuild {
    param([string[]]$ComposeArgs, [switch]$NoCache)
    $buildArgs = @('build')
    if ($NoCache) { $buildArgs += '--no-cache' }
    $allArgs = @($ComposeArgs) + $buildArgs
    & docker @allArgs
    if ($LASTEXITCODE -ne 0) { throw 'AI image build failed. The previous image is unchanged unless the build completed; check Docker Desktop resources and retry.' }
}

function Start-AiComposeContainers {
    param(
        [string[]]$ComposeArgs, [string]$EnvFile,
        [switch]$NoBuild, [switch]$ForceRecreate
    )
    $upArgs = @('up','-d','--wait')
    if ($NoBuild) { $upArgs += '--no-build' }
    if ($ForceRecreate) { $upArgs += '--force-recreate' }
    $allArgs = @($ComposeArgs) + $upArgs
    & docker @allArgs
    if ($LASTEXITCODE -ne 0) {
        Show-AiDockerLogs -ComposeArgs $ComposeArgs
        throw 'AI camera containers failed to become healthy. Check: docker compose -p smartpark-ai -f compose.ai.yml logs --tail 200'
    }
    $problem = Assert-AiContainersRunning -EnvFile $EnvFile
    if ($problem) {
        Show-AiDockerLogs -ComposeArgs $ComposeArgs
        throw $problem
    }
    Write-Host '    - AI containers ready: REST http://localhost:5001 · Live http://localhost:5002.' -ForegroundColor Green
}

function Show-AiResolutionPlaybook {
    Write-Host ''
    Write-Host '    AI image đã tồn tại nhưng chưa có container. Chọn một phương án:' -ForegroundColor Yellow
    Write-Host ''
    Write-Host '      [A] Dùng image sẵn có, không rebuild (nhanh, khuyến nghị):' -ForegroundColor DarkGray
    Write-Host '          docker compose -p smartpark-ai --env-file .env -f compose.ai.yml up -d --wait --no-build' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '      [B] Rebuild có cache rồi start:' -ForegroundColor DarkGray
    Write-Host '          docker compose -p smartpark-ai --env-file .env -f compose.ai.yml build' -ForegroundColor DarkGray
    Write-Host '          docker compose -p smartpark-ai --env-file .env -f compose.ai.yml up -d --wait' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '      [C] Rebuild sạch (khi image nghi hỏng):' -ForegroundColor DarkGray
    Write-Host '          docker compose -p smartpark-ai --env-file .env -f compose.ai.yml build --no-cache' -ForegroundColor DarkGray
    Write-Host '          docker compose -p smartpark-ai --env-file .env -f compose.ai.yml up -d --wait' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '      [D] Kiểm tra thư viện trong image:' -ForegroundColor DarkGray
    Write-Host '          docker run --rm --entrypoint python smartpark-ai:local -c "import cv2,numpy,scipy,torch,paddleocr,ultralytics,psycopg,flask; print(''AI_IMAGE_OK'')"' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '      [E] Xoá image và để setup build lại:' -ForegroundColor DarkGray
    Write-Host '          docker image rm smartpark-ai:local' -ForegroundColor DarkGray
    Write-Host '          .\setup.ps1 -WithAiCamera -RebuildAi' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '      Xem log:' -ForegroundColor DarkGray
    Write-Host '          docker compose -p smartpark-ai --env-file .env -f compose.ai.yml logs --tail 200' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '    "Skip" chỉ bỏ qua build; setup vẫn chạy up -d --wait --no-build để đảm bảo container AI đang chạy.' -ForegroundColor DarkGray
    Write-Host '    Các chữ cái trong prompt bên dưới là thao tác, không ánh xạ 1-1 tới A–E ở trên.' -ForegroundColor DarkGray
}

function Read-AiResolutionChoice {
    if ($NonInteractive -or [Console]::IsInputRedirected) {
        Write-Host '    - Non-interactive: starting the existing AI image (no rebuild).' -ForegroundColor DarkGray
        return 'S'
    }
    while ($true) {
        $answer = Read-Host '    [S] Start / [R] Rebuild / [C] Clean rebuild / [K] Skip AI / [A] Abort (Enter = S)'
        $normalized = ([string]$answer).Trim().ToUpperInvariant()
        if (!$normalized) { return 'S' }
        if ($normalized -in @('S','R','C','K','A')) { return $normalized }
        Write-Host '    Enter S, R, C, K or A.' -ForegroundColor Yellow
    }
}

function Invoke-AiCameraSetup {
    param([hashtable]$Settings, [string]$EnvFile, [switch]$Rebuild)
    $script:AiCameraSkipped = $false
    $compose = @('compose','-p','smartpark-ai','--env-file',$EnvFile,'-f',(Join-Path $taskRoot 'compose.ai.yml'))

    Assert-AiModelAssets

    if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI not found. Install and start Docker Desktop before -WithAiCamera.' }
    $network = Invoke-DockerQuery -Arguments @('network','inspect','smartpark-schema_default')
    if ($network.ExitCode -ne 0) {
        if ($network.Text -match 'Cannot connect|error during connect|Is the docker daemon running|dockerDesktopLinuxEngine|open //./pipe/docker') {
            throw 'Docker Desktop is not running or not reachable. Start Docker Desktop and retry -WithAiCamera.'
        }
        throw 'Docker network smartpark-schema_default not found. Run setup.ps1 without -SkipDatabase first so the schema DBs are up.'
    }

    $configArgs = $compose + @('config','--quiet')
    $composeConfig = Invoke-DockerQuery -Arguments $configArgs
    if ($composeConfig.ExitCode -ne 0) { throw "AI Compose configuration is invalid: $($composeConfig.Text.Trim())" }

    $image = Get-AiImageState
    $containers = @(Get-AiStackState -EnvFile $EnvFile)

    if (!$image) {
        if ($containers.Count -gt 0) {
            Write-Host '    - AI image not found but old containers exist -> building, then recreating them...' -ForegroundColor DarkGray
        } else {
            Write-Host '    - AI image not found -> building (first run ~5-10 min)...' -ForegroundColor DarkGray
        }
        Invoke-AiComposeBuild -ComposeArgs $compose
        if ($containers.Count -gt 0) {
            Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile -ForceRecreate
        } else {
            Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile
        }
        return
    }

    if ($Rebuild) {
        Write-Host "    - -RebuildAi requested -> rebuilding AI image (cached layers reused; current $($image.SizeGb) GB image is kept until the build succeeds)..." -ForegroundColor DarkGray
        Invoke-AiComposeBuild -ComposeArgs $compose
        if ($containers.Count -gt 0) {
            Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile -ForceRecreate
        } else {
            Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile
        }
        return
    }

    if ($containers.Count -eq 0) {
        $probeOk = Test-AiImageLibraries
        if (!$probeOk) {
            Write-Warning 'AI image exists but the library probe failed. The image may be broken or missing Python packages. No automatic rebuild will run; choose R/C or rerun with -RebuildAi after confirming.'
        }
        Show-AiResolutionPlaybook
        $choice = Read-AiResolutionChoice
        switch ($choice) {
            'S' {
                if (!$probeOk) { Write-Warning 'Starting the existing AI image after a failed probe (explicit Start). If containers exit, rerun with -RebuildAi.' }
                Write-Host '    - Starting containers from the existing AI image (no rebuild)...' -ForegroundColor DarkGray
                Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile -NoBuild
            }
            'R' {
                Write-Host '    - Rebuilding AI image with cache, then starting...' -ForegroundColor DarkGray
                Invoke-AiComposeBuild -ComposeArgs $compose
                Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile
            }
            'C' {
                Write-Host '    - Rebuilding AI image without cache (clean rebuild), then starting...' -ForegroundColor DarkGray
                Invoke-AiComposeBuild -ComposeArgs $compose -NoCache
                Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile
            }
            'K' {
                Write-Host '    - AI setup skipped; continuing with the rest of setup.' -ForegroundColor Yellow
                $script:AiCameraSkipped = $true
            }
            'A' {
                Write-Host 'Aborted by user.' -ForegroundColor Red
                exit 1
            }
            default { throw "Unexpected AI setup choice: $choice" }
        }
        return
    }

    $unhealthy = @($containers | Where-Object { $_.State -eq 'running' -and $_.Health -eq 'unhealthy' })
    $stopped = @($containers | Where-Object { $_.State -ne 'running' })
    $staleContainers = @($containers | Where-Object { Test-AiContainerImageStale -Container $_ -Image $image })

    Write-Host "    - AI image found ($($image.SizeGb) GB); $($containers.Count) container(s) already exist -> skip build." -ForegroundColor DarkGray

    if ($staleContainers.Count -gt 0) {
        $staleServices = ($staleContainers | ForEach-Object { if ($_.Service) { $_.Service } else { $_.Name } }) -join ', '
        $staleIds = ($staleContainers | ForEach-Object { if ($_.ComposeImageId) { $_.ComposeImageId } else { $_.ImageId } } | Select-Object -Unique) -join ', '
        Write-Warning "AI container(s) $staleServices reference an older image ($staleIds); current smartpark-ai:local is $($image.ShortId). No automatic rebuild will run."
        Write-Host '    - Recreating from the current image with --force-recreate --no-build (no rebuild)...' -ForegroundColor DarkGray
        Write-Host '    - Follow-up: rerun with -RebuildAi only if the current image is incomplete or broken.' -ForegroundColor DarkGray
        Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile -NoBuild -ForceRecreate
        return
    }

    if ($unhealthy.Count -gt 0) {
        $unhealthyServices = ($unhealthy | ForEach-Object { $_.Service }) -join ', '
        Write-Warning "AI container(s) $unhealthyServices are unhealthy. No automatic rebuild will run."
        Write-Host '    - Inspect the logs below; rerun with -RebuildAi only if the image itself is suspected broken.' -ForegroundColor DarkGray
        Show-AiDockerLogs -ComposeArgs $compose
        Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile -NoBuild
        return
    }

    if ($stopped.Count -gt 0) {
        Write-Host '    - AI containers are stopped/exited -> starting them without rebuild...' -ForegroundColor DarkGray
        Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile -NoBuild
        return
    }

    Write-Host '    - AI containers already running; ensuring they stay healthy (no build)...' -ForegroundColor DarkGray
    Start-AiComposeContainers -ComposeArgs $compose -EnvFile $EnvFile -NoBuild
}

# Read existing secrets without printing their values or invoking `list`.
$existingByService = @{}
$projectByService = @{}
foreach ($service in @('User','Parking','Reservation','Payment')) {
    # Keep existing connection/configuration keys while resolving the renamed project.
    $projectService = switch ($service) { 'Parking' { 'Facility' } 'Reservation' { 'Operation' } default { $service } }
    $projectDir = Join-Path $taskRoot "src/Services/${projectService}Service/SmartParking.${projectService}Service.API"
    $projectByService[$service] = $projectDir
    [xml]$projectXml = Get-Content (Join-Path $projectDir "SmartParking.${projectService}Service.API.csproj") -Raw
    $secretId = [string](@($projectXml.Project.PropertyGroup.UserSecretsId | Where-Object { $_ })[0])
    $secretFile = Join-Path ([Environment]::GetFolderPath('ApplicationData')) "Microsoft/UserSecrets/$secretId/secrets.json"
    $existing = @{}
    if (Test-Path -LiteralPath $secretFile -PathType Leaf) {
        $saved = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
        foreach ($property in $saved.PSObject.Properties) { $existing[$property.Name] = $property.Value }
    }
    $existingByService[$service] = $existing
}

Write-Host '[1/7] Checking prerequisites and loading .env configuration...' -ForegroundColor Cyan
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET SDK 10 before setup.' }
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.') { throw 'Setup requires .NET SDK 10.' }
$envExists = Test-Path -LiteralPath $envFile -PathType Leaf
if ($envExists -and -not $NonInteractive -and -not [Console]::IsInputRedirected) {
    Write-Host "  .env already exists: $envFile" -ForegroundColor Yellow
    $answer = Read-Host '  [O]verwrite / [K]eep / [A]bort (Enter = Keep)'
    if ($answer -match '^\s*[Aa]') { Write-Host 'Aborted by user.' -ForegroundColor Red; exit 1 }
    if ($answer -match '^\s*[Oo]') {
        Copy-Item -LiteralPath $envFile -Destination "$envFile.bak" -Force
        $envExists = $false
        Write-Host "  Existing .env backed up to $envFile.bak; regenerating from .env.example." -ForegroundColor DarkGray
    }
}
$inputEnv = if ($envExists) { $envFile } else { Join-Path $taskRoot '.env.example' }
$settings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $inputEnv -NoExport
$generated = @{}
Write-Host '[2/7] Resolving credentials (reuse existing, generate missing)...' -ForegroundColor Cyan
if ($TeamEnvPath -and $CredentialMode -ne 'Team') { throw 'Use -CredentialMode Team with -TeamEnvPath.' }
if ($CredentialMode -eq 'Team') {
    if (!$TeamEnvPath) { throw 'Team mode requires -TeamEnvPath to a private team credential file.' }
    $team = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $TeamEnvPath -NoExport
    foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD')) {
        if (!$team[$name] -or $team[$name] -like 'replace-with-*') { throw "Team file must configure $name." }
    }
    foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD','SONAR_DB_PASSWORD','VITE_MAPTILER_API_KEY','SMARTPARK_SERVICE_USER_URL','SMARTPARK_SERVICE_PARKING_URL','SMARTPARK_SERVICE_RESERVATION_URL','SMARTPARK_SERVICE_PAYMENT_URL')) {
        if (!$team[$name] -or $team[$name] -like 'replace-with-*') { continue }
        if ($settings[$name] -and $settings[$name] -notlike 'replace-with-*' -and $settings[$name] -cne $team[$name]) {
            throw "Team/local conflict for $name. Existing configuration was preserved; reconcile it explicitly before retrying."
        }
        if (!$settings[$name] -or $settings[$name] -like 'replace-with-*') {
            $settings[$name] = $team[$name]
            $generated[$name] = $team[$name]
        }
    }
}
foreach ($name in @('SMARTPARK_DEV_PASSWORD','SMARTPARK_SERVICE_KEY','SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
    if (!$settings[$name] -or $settings[$name] -like 'replace-with-*') {
        $value = $null
        if ($name -eq 'SMARTPARK_SERVICE_KEY') {
            $keys = @($existingByService.Values | ForEach-Object { $_['Services:Key'] } | Where-Object { $_ } | Select-Object -Unique)
            if ($keys.Count -gt 1) { throw 'Existing service keys differ. Reconcile Services:Key before setup.' }
            if ($keys.Count -eq 1) { $value = $keys[0] }
        } elseif ($name -in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
            if ($name -eq 'SMARTPARK_ADMIN_PASSWORD') {
                $value = $existingByService['User']['DevelopmentAdmin:Password']
            }
            if (!$value) {
                # 4 fixed chars + 8 base64 chars = 12 chars; always has upper/lower/digit/special.
                $value = 'Aa1!' + (New-LocalSecret 6)
            }
        } else {
            # Reuse an existing local PostgreSQL password when configuring an old volume.
            $passwords = @()
            foreach ($service in @('User','Parking','Reservation','Payment')) {
                $connection = $existingByService[$service]["ConnectionStrings:$service"]
                $extracted = Get-SecretConnectionStringPassword $connection
                if ($extracted) { $passwords += $extracted }
            }
            $passwords = @($passwords | Select-Object -Unique)
            if ($passwords.Count -gt 1) { throw 'Existing database passwords differ. Configure SMARTPARK_DEV_PASSWORD explicitly.' }
            if ($passwords.Count -eq 1) { $value = $passwords[0] }
            if (!$value -and !$SkipDatabase) {
                $volumes = & docker volume ls --filter 'label=com.docker.compose.project=smartpark-schema' --format '{{.Name}}' 2>$null
                if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker volumes. Start Docker Desktop and retry, or use -SkipDatabase.' }
                if ($volumes) {
                    if ($RecreateDb) {
                        # Existing volumes are about to be dropped by `down -v`; the old password no longer applies.
                        Write-Warning 'Password guard skipped because -RecreateDb will wipe existing schema volumes.'
                    } else {
                        throw 'Existing database volumes found. Recover their original SMARTPARK_DEV_PASSWORD in .env; setup will not replace or delete data.'
                    }
                }
            }
        }
        if (!$value) {
            $value = if ($name -in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) { 'Aa1!' + (New-LocalSecret 6) } else { New-LocalSecret }
        }
        if ($value -match "[\r\n']") { throw "Set $name directly in .env; its existing value cannot be safely represented automatically." }
        $settings[$name] = $value
        $generated[$name] = $value
    }
}
foreach ($pair in @(
    @{ Name = 'SMARTPARK_OWNER_EMAIL'; Default = 'owner@smartpark.local' },
    @{ Name = 'SMARTPARK_OPERATOR_EMAIL'; Default = 'operator@smartpark.local' }
)) {
    if ([string]::IsNullOrWhiteSpace($settings[$pair.Name])) {
        $settings[$pair.Name] = $pair.Default
        $generated[$pair.Name] = $pair.Default
    }
}
if ($sonarEnabled) {
    # SonarQube runs by default, so fill its credentials like the local database
    # password: reuse an existing value, generate only when missing or placeholder.
    if (!$settings['SONAR_DB_PASSWORD'] -or $settings['SONAR_DB_PASSWORD'] -like 'replace-with-*') {
        $settings['SONAR_DB_PASSWORD'] = New-LocalSecret
        $generated['SONAR_DB_PASSWORD'] = $settings['SONAR_DB_PASSWORD']
    }
    if (!$settings['SONAR_PORT']) { $settings['SONAR_PORT'] = '9000'; $generated['SONAR_PORT'] = '9000' }
}
Write-Host '[3/7] Validating configuration...' -ForegroundColor Cyan
foreach ($name in $generated.Keys) {
    if ($generated[$name] -match "[\r\n']") { throw "Configure $name manually; its value cannot be safely written automatically." }
}
if ($settings['SMARTPARK_SERVICE_KEY'].Length -lt 32) { throw 'SMARTPARK_SERVICE_KEY must contain at least 32 characters.' }
foreach ($name in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
    Assert-DevelopmentPassword $name $settings[$name]
}
foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT','SMARTPARK_DB_PAYMENT_PORT','SMARTPARK_DB_NOTIFICATION_PORT','SMARTPARK_DB_IOT_PORT')) {
    $port = 0
    if (![int]::TryParse($settings[$name], [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw "Invalid port: $name." }
}
if ($settings['VITE_MAPTILER_API_KEY'] -match "[\r\n']") { throw 'Configure MapTiler key directly in FE/.env.' }
foreach ($service in @('USER','PARKING','RESERVATION','PAYMENT')) {
    $name = "SMARTPARK_SERVICE_${service}_URL"
    if ($settings[$name]) {
        $uri = $null
        if (![Uri]::TryCreate($settings[$name], [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http','https')) { throw "Invalid service URL: $name." }
    }
}
if ($sonarEnabled) {
    $sonarPort = 0
    if (![int]::TryParse($settings['SONAR_PORT'], [ref]$sonarPort) -or $sonarPort -lt 1 -or $sonarPort -gt 65535) { throw 'Invalid SONAR_PORT.' }
}
if (!$envExists -or $generated.Count) {
    $lines = @(Get-Content -LiteralPath $inputEnv -Encoding UTF8)
    foreach ($name in $generated.Keys) {
        $found = $false
        $lines = @($lines | ForEach-Object {
            if ($_ -match ('^\s*' + [regex]::Escape($name) + '\s*=')) {
                $found = $true
                # Single quotes prevent Compose interpolation of a literal dollar sign.
                "$name='$($generated[$name])'"
            } else { $_ }
        })
        if (!$found) { $lines += "$name='$($generated[$name])'" }
    }
    [IO.File]::WriteAllLines($envFile, $lines, (New-Object Text.UTF8Encoding($false)))
    Write-Host 'Filled missing local credentials; existing configured values were preserved.' -ForegroundColor Green
}
$settings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $envFile
if ($settings['SMARTPARK_SERVICE_KEY'].Length -lt 32) { throw 'SMARTPARK_SERVICE_KEY must contain at least 32 characters.' }
foreach ($name in @('SMARTPARK_ADMIN_PASSWORD','SMARTPARK_OWNER_PASSWORD','SMARTPARK_OPERATOR_PASSWORD')) {
    Assert-DevelopmentPassword $name $settings[$name]
}
foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT','SMARTPARK_DB_PAYMENT_PORT','SMARTPARK_DB_NOTIFICATION_PORT','SMARTPARK_DB_IOT_PORT')) {
    $port = 0
    if (![int]::TryParse($settings[$name], [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw "Invalid port: $name." }
}

if ($SkipDatabase) {
    Write-Host '[4/7] Skipping database initialization (-SkipDatabase).' -ForegroundColor Cyan
}
if (!$SkipDatabase) {
    Write-Host '[4/7] Initializing databases (user, facility, operations, payment, notification, iot)...' -ForegroundColor Cyan
    & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose configuration is invalid.' }
    if ($RecreateDb) {
        # Same project name, env file and compose file as `up` — only the schema project is targeted.
        Write-Host '    - Dropping schema volumes (-RecreateDb); existing dev data in those volumes will be lost...' -ForegroundColor DarkGray
        & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile down -v
        if ($LASTEXITCODE -ne 0) { throw 'Failed to remove schema volumes for -RecreateDb. Existing volumes were preserved.' }
    }
        Write-Host '    - Starting database containers and waiting for healthy (first init can take a few minutes)...' -ForegroundColor DarkGray
    & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Database startup failed. Check Docker Desktop, ports and SQL initialization logs. Existing volumes were preserved.' }
    $migrationIndex = 0
    foreach ($service in @('User','Parking','Reservation','Payment','Notification','Iot')) {
        $migrationIndex++
        Write-Host "    - [$migrationIndex/6] Probing authentication and applying migrations for $service..." -ForegroundColor DarkGray
        $container = @{ User='user-db'; Parking='facility-db'; Reservation='operations-db'; Payment='payment-db'; Notification='notification-db'; Iot='iot-db' }[$service]
        # Container env is used internally; credentials never become command arguments.
        # Send shell code through stdin: PowerShell 5.1 strips native argument quotes.
        # The trailing comment also consumes its Windows CRLF line terminator.
        & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile exec -T $container sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -h 127.0.0.1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -q -c "SELECT 1"' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "$service database authentication failed. Restore the password used to initialize this volume; no data was reset." }
        # Runner status lines print green; psql command chatter prints dim gray.
        $runnerOutput = & (Join-Path $taskRoot 'scripts/apply-workflow-migrations.ps1') -Service $service -SchemaRoot $taskRoot -EnvPath $envFile
        foreach ($runnerLine in @($runnerOutput)) {
            if (!$runnerLine) { continue }
            if ($runnerLine -match 'workflow migrations applied|schema already current') { Write-Host "    $runnerLine" -ForegroundColor Green }
            else { Write-Host "    $runnerLine" -ForegroundColor DarkGray }
        }
    }
}

Write-Host '[5/7] Configuring user-secrets for User, Facility, Operation, Payment...' -ForegroundColor Cyan
if (!$SkipUserSecrets) { foreach ($service in @('User','Parking','Reservation','Payment')) {
    $database = @{ User='smartpark_user'; Parking='smartpark_facility'; Reservation='smartpark_operations'; Payment='smartpark_payment'; Notification='smartpark_notification'; Iot='smartpark_iot' }[$service]
    $port = $settings["SMARTPARK_DB_$($service.ToUpperInvariant())_PORT"]
    $password = $settings['SMARTPARK_DEV_PASSWORD'].Replace('"','""')
    $desired = [ordered]@{
        'Services:Key' = $settings['SMARTPARK_SERVICE_KEY']
        'Services:User' = 'http://localhost:5035/'
        'Services:Parking' = 'http://localhost:5045/'
        'Services:Reservation' = 'http://localhost:5055/'
        'Services:Payment' = 'http://localhost:5065/'
    }
    $desired["ConnectionStrings:$service"] = "Host=127.0.0.1;Port=$port;Database=$database;Username=$database;Password=`"$password`""
    foreach ($remoteService in @('User','Parking','Reservation','Payment')) {
        $url = $settings["SMARTPARK_SERVICE_$($remoteService.ToUpperInvariant())_URL"]
        $configService = @{ User='User'; Parking='Facility'; Reservation='Operation' }[$remoteService]
        if ($url) { $desired["Services:$configService"] = $url }
    }
    if ($service -eq 'User') {
        $desired['DevelopmentAdmin:Email'] = $settings['SMARTPARK_ADMIN_EMAIL']
        $desired['DevelopmentAdmin:Password'] = $settings['SMARTPARK_ADMIN_PASSWORD']
        $desired['DevelopmentOwner:Email'] = $settings['SMARTPARK_OWNER_EMAIL']
        $desired['DevelopmentOwner:Password'] = $settings['SMARTPARK_OWNER_PASSWORD']
        $desired['DevelopmentOperator:Email'] = $settings['SMARTPARK_OPERATOR_EMAIL']
        $desired['DevelopmentOperator:Password'] = $settings['SMARTPARK_OPERATOR_PASSWORD']
        $keyDirectory = $settings['SMARTPARK_WORKFLOW_KEY_DIRECTORY']
        if (!$keyDirectory) { $keyDirectory = '.cache/workflow-keys' }
        if (![IO.Path]::IsPathRooted($keyDirectory)) { $keyDirectory = Join-Path $taskRoot $keyDirectory }
        $desired['Workflow:KeyDirectory'] = $keyDirectory
        foreach ($field in @('Host','Port','Username','Password','From')) {
            $value = $settings["SMARTPARK_SMTP_$($field.ToUpperInvariant())"]
            if ($value) { $desired["Otp:Smtp:$field"] = $value }
        }
        if ($settings['SMARTPARK_JWT_PRIVATE_KEY_PEM']) { $desired['Jwt:PrivateKeyPem'] = $settings['SMARTPARK_JWT_PRIVATE_KEY_PEM'] }
    }
    if ($service -eq 'Payment') {
        # PaymentService resolves the reservation API through Services:ReservationService:BaseUrl.
        $reservationUrl = $settings['SMARTPARK_SERVICE_RESERVATION_URL']
        if (!$reservationUrl) { $reservationUrl = 'http://localhost:5055/' }
        $desired['Services:ReservationService:BaseUrl'] = $reservationUrl
        # Empty payOS values keep the sandbox emulation; keys are never removed automatically.
        if ($settings['SMARTPARK_PAYOS_CLIENT_ID']) { $desired['PayOs:ClientId'] = $settings['SMARTPARK_PAYOS_CLIENT_ID'] }
        if ($settings['SMARTPARK_PAYOS_API_KEY']) { $desired['PayOs:ApiKey'] = $settings['SMARTPARK_PAYOS_API_KEY'] }
        if ($settings['SMARTPARK_PAYOS_CHECKSUM_KEY']) { $desired['PayOs:ChecksumKey'] = $settings['SMARTPARK_PAYOS_CHECKSUM_KEY'] }
        if ($settings['SMARTPARK_PAYOS_BASE_URL']) { $desired['PayOs:BaseUrl'] = $settings['SMARTPARK_PAYOS_BASE_URL'] }
    }
    $toSync = @{}
    foreach ($name in $desired.Keys) {
        if (!$existingByService[$service].ContainsKey($name) -or $existingByService[$service][$name] -cne $desired[$name]) {
            $toSync[$name] = $desired[$name]
        }
    }
    # Optional keys (JWT PEM, SMTP fields) that are no longer configured in .env are removed.
    $removable = @()
    foreach ($name in @('Jwt:PrivateKeyPem','Otp:Smtp:Host','Otp:Smtp:Port','Otp:Smtp:Username','Otp:Smtp:Password','Otp:Smtp:From')) {
        if (!$desired.Contains($name) -and $existingByService[$service].ContainsKey($name)) { $removable += $name }
    }
    if ($service -eq 'User' -and $existingByService[$service].ContainsKey('DevelopmentAdmin:Password') -and $toSync.ContainsKey('DevelopmentAdmin:Password')) {
        Write-Warning 'DevelopmentAdmin:Password differs between user-secrets and .env; user-secrets will be overwritten to match .env. Setup does not change the Admin password already seeded in the database — if Admin login fails afterwards, restore the old password in .env or rerun with -RecreateDb.'
    }
    if ($toSync.Count) {
        foreach ($name in $toSync.Keys) {
            & dotnet user-secrets set $name $toSync[$name] --project $projectByService[$service] | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Cannot configure user-secrets for $service ($name)." }
        }
    }
    foreach ($name in $removable) {
        & dotnet user-secrets remove $name --project $projectByService[$service] | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Cannot remove user-secret $name for $service." }
    }
    Write-Host "$service configuration ready ($($toSync.Count) synced, $($removable.Count) removed)." -ForegroundColor Green
} }
Write-Host '[6/7] Preparing frontend environment (FE/.env, MapTiler, API base URLs)...' -ForegroundColor Cyan
$frontendTemplate = Join-Path $taskRoot 'FE/.env.example'
$frontendEnv = Join-Path $taskRoot 'FE/.env'
if ((Test-Path $frontendTemplate) -and !(Test-Path $frontendEnv)) { Copy-Item $frontendTemplate $frontendEnv }
if ($settings['VITE_MAPTILER_API_KEY']) {
    $frontendSettings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $frontendEnv -NoExport
    if (!$frontendSettings['VITE_MAPTILER_API_KEY']) {
        $mapKey = $settings['VITE_MAPTILER_API_KEY']
        if ($mapKey -match "[\r\n']") { throw 'Configure MapTiler key directly in FE/.env.' }
        $frontendLines = @(Get-Content -LiteralPath $frontendEnv -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*VITE_MAPTILER_API_KEY\s*=' })
        $frontendLines += "VITE_MAPTILER_API_KEY='$mapKey'"
        [IO.File]::WriteAllLines($frontendEnv, $frontendLines, (New-Object Text.UTF8Encoding($false)))
    } elseif ($frontendSettings['VITE_MAPTILER_API_KEY'] -cne $settings['VITE_MAPTILER_API_KEY']) {
        Write-Warning 'FE/.env has a different MapTiler key; existing frontend configuration was preserved.'
    }
}
# Backend/AI endpoints written into FE/.env when empty; existing frontend values are preserved.
$frontendDefaults = [ordered]@{
    'VITE_AI_API_BASE_URL'      = 'http://localhost:5001'
    'VITE_AI_LIVE_API_BASE_URL' = 'http://localhost:5002'
    'VITE_PAYMENT_API_BASE_URL' = 'http://localhost:5065'
}
if ($settings['VITE_AI_API_BASE_URL']) { $frontendDefaults['VITE_AI_API_BASE_URL'] = $settings['VITE_AI_API_BASE_URL'] }
if ($settings['VITE_AI_LIVE_API_BASE_URL']) { $frontendDefaults['VITE_AI_LIVE_API_BASE_URL'] = $settings['VITE_AI_LIVE_API_BASE_URL'] }
if ($settings['VITE_PAYMENT_API_BASE_URL']) { $frontendDefaults['VITE_PAYMENT_API_BASE_URL'] = $settings['VITE_PAYMENT_API_BASE_URL'] }
if (Test-Path -LiteralPath $frontendEnv -PathType Leaf) {
    $frontendSettings = & (Join-Path $taskRoot 'scripts/load-env.ps1') -EnvPath $frontendEnv -NoExport
    $frontendMissing = @{}
    foreach ($name in $frontendDefaults.Keys) {
        $value = $frontendDefaults[$name]
        if ($value -match "[\r\n']") { throw "Configure $name directly in FE/.env." }
        if (!$frontendSettings[$name]) { $frontendMissing[$name] = $value }
        elseif ($frontendSettings[$name] -cne $value) { Write-Warning "FE/.env has a different $name; existing frontend configuration was preserved." }
    }
    if ($frontendMissing.Count) {
        $frontendLines = @(Get-Content -LiteralPath $frontendEnv -Encoding UTF8)
        foreach ($name in $frontendMissing.Keys) {
            $frontendLines = @($frontendLines | Where-Object { $_ -notmatch ('^\s*' + [regex]::Escape($name) + '\s*=') })
            $frontendLines += "$name='$($frontendMissing[$name])'"
        }
        [IO.File]::WriteAllLines($frontendEnv, $frontendLines, (New-Object Text.UTF8Encoding($false)))
        Write-Host 'Configured frontend API endpoint URLs in FE/.env; existing values were preserved.' -ForegroundColor Green
    }
}
if ($sonarEnabled -and !$SkipDatabase) {
    Write-Host '    - Starting SonarQube stack...' -ForegroundColor DarkGray
    $sonarCompose = Join-Path $taskRoot 'compose.sonar.yml'
    & docker compose --project-name smartpark-sonar --env-file $envFile -f $sonarCompose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'SonarQube Compose configuration is invalid.' }
    & docker compose --project-name smartpark-sonar --env-file $envFile -f $sonarCompose up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'SonarQube startup failed. Check logs and existing DB password; no volume was reset.' }
}
if ($WithAiCamera) {
    if ($SkipDatabase) { throw 'AI camera containers need the schema DBs running; -SkipDatabase is not supported with -WithAiCamera.' }
    if (!$settings['SMARTPARK_DEV_PASSWORD'] -or $settings['SMARTPARK_DEV_PASSWORD'] -like 'replace-with-*') {
        throw 'Configure SMARTPARK_DEV_PASSWORD before using -WithAiCamera.'
    }
    Invoke-AiCameraSetup -Settings $settings -EnvFile $envFile -Rebuild:$RebuildAi
}
Write-Host '[7/7] Restoring .NET dependencies...' -ForegroundColor Cyan
if (!$SkipRestore) {
    & dotnet restore (Join-Path $taskRoot 'SmartParking.slnx') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
}
$setupDone = 'Setup complete. Run scripts/run-schema-service.ps1 in separate terminals for User, Facility, Operation and Payment.'
if ($sonarEnabled -and !$SkipDatabase) { $setupDone += " SonarQube UI: http://localhost:$($settings['SONAR_PORT']) (first login admin/admin)." }
if ($WithAiCamera -and !$script:AiCameraSkipped) { $setupDone += " AI REST: http://localhost:5001 · Live: http://localhost:5002." }
Write-Host $setupDone -ForegroundColor Green
