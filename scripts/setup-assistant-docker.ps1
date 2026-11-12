param(
    [string]$EnvPath,
    [switch]$ValidateOnly,
    [switch]$NoBuild,
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$SchemaProject = 'smartpark-schema'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$EnvPath) { $EnvPath = Join-Path $root '.env' }
if (!(Test-Path -LiteralPath $EnvPath -PathType Leaf)) {
    throw 'Copy .env.example to .env and configure the approved Assistant inputs before using -WithAssistant.'
}
$EnvPath = (Resolve-Path -LiteralPath $EnvPath).Path
$envDirectory = Split-Path -Parent $EnvPath
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport

function Get-RequiredSetting([string]$Name) {
    $value = [string]$settings[$Name]
    if ([string]::IsNullOrWhiteSpace($value) -or $value.StartsWith('replace-with-')) {
        throw "Missing Assistant setup setting: $Name."
    }
    return $value
}

function Resolve-AssistantPath([string]$Value, [string]$Name) {
    if (![IO.Path]::IsPathRooted($Value)) {
        return Join-Path $envDirectory $Value
    }
    if ([IO.Path]::DirectorySeparatorChar -eq '\' -and
        $Value -notmatch '^[A-Za-z]:[\\/]' -and $Value -notmatch '^\\\\[^\\/]+[\\/][^\\/]+') {
        throw "Use an absolute path or a path relative to the environment file for $Name."
    }
    return $Value
}

function Get-RequiredPath([string]$Name, [string]$Kind) {
    $path = Resolve-AssistantPath (Get-RequiredSetting $Name) $Name
    if (!(Test-Path -LiteralPath $path -PathType $Kind)) {
        throw "Assistant setup cannot find the required $Kind for $Name. Relative paths are resolved from the environment file."
    }
    return (Resolve-Path -LiteralPath $path).Path
}

$publicKeyFile = Get-RequiredPath 'Assistant__Jwt__PublicKeyFile' 'Leaf'
$publicKey = [IO.File]::ReadAllText($publicKeyFile)
if ($publicKey -notmatch '(?s)\A\s*-----BEGIN (RSA |)PUBLIC KEY-----\s+[A-Za-z0-9+/=\r\n]+-----END \1PUBLIC KEY-----\s*\z' -or
    $publicKey -match 'PRIVATE KEY') {
    throw 'Assistant requires a PEM containing only the UserService RSA public key.'
}
$keyDirectory = Get-RequiredPath 'Assistant__Management__KeyDirectory' 'Container'
$certificate = Get-RequiredPath 'Assistant__Management__KeyEncryptionCertificatePath' 'Leaf'
$knowledge = Get-RequiredPath 'Assistant__Guest__KnowledgeDirectory' 'Container'
function Test-UnderDirectory([string]$Path, [string]$Directory) {
    $prefix = $Directory.TrimEnd([char[]]@('\','/')) + [IO.Path]::DirectorySeparatorChar
    return $Path.Equals($Directory, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}
$signingKeyFile = $env:SMARTPARK_JWT_PRIVATE_KEY_FILE
if (!$signingKeyFile) { $signingKeyFile = $settings['SMARTPARK_JWT_PRIVATE_KEY_FILE'] }
if ($signingKeyFile) { $signingKeyFile = Resolve-AssistantPath $signingKeyFile 'SMARTPARK_JWT_PRIVATE_KEY_FILE' }
$excludedFiles = @($EnvPath, (Join-Path $root '.env'))
if ($signingKeyFile -and (Test-Path -LiteralPath $signingKeyFile -PathType Leaf)) {
    $signingKeyFile = (Resolve-Path -LiteralPath $signingKeyFile).Path
    if ($certificate -ieq $signingKeyFile) { throw 'UserService signing material must stay outside Assistant.' }
    $excludedFiles += $signingKeyFile
}
foreach ($file in $excludedFiles) {
    if ((Test-UnderDirectory $file $keyDirectory) -or (Test-UnderDirectory $file $knowledge)) {
        throw 'Use dedicated Assistant mount directories outside .env and UserService signing material.'
    }
}
$certificatePassword = Get-RequiredSetting 'Assistant__Management__KeyEncryptionCertificatePassword'
try {
    $keyringCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificate, $certificatePassword, [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    try {
        $certificateKey = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($keyringCertificate)
        if (!$certificateKey -or $keyringCertificate.NotBefore -gt (Get-Date) -or
            $keyringCertificate.NotAfter -le (Get-Date)) { throw 'Invalid keyring certificate.' }
    } finally {
        if ($certificateKey) { $certificateKey.Dispose() }
        $keyringCertificate.Dispose()
    }
} catch { throw 'Assistant requires an unexpired PFX with an RSA private key and matching password.' }
$markdown = Join-Path $knowledge 'smartpark-knowledge.md'
$manifest = Join-Path $knowledge 'publication.json'
if (!(Test-Path -LiteralPath $markdown -PathType Leaf) -or !(Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw 'Provide the approved Markdown deployment copy and publication.json before setup.'
}
try { $publication = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifest | ConvertFrom-Json }
catch { throw 'Assistant publication.json is invalid.' }
if ($publication.role -cne 'admin' -or !$publication.version -or !$publication.approvedBy -or
    (Get-FileHash -Algorithm SHA256 -LiteralPath $markdown).Hash -ne $publication.sha256) {
    throw 'Assistant publication metadata or Markdown hash does not match the approved deployment copy.'
}
$header = ([IO.File]::ReadAllText($markdown) -split '(?m)^---\s*$', 3)[1]
if ($header -cnotmatch '(?m)^publication_state:\s*published\s*$' -or
    $header -cnotmatch '(?m)^review_status:\s*approved\s*$' -or
    $header -cnotmatch ('(?m)^version:\s*' + [regex]::Escape([string]$publication.version) + '\s*$')) {
    throw 'Assistant setup requires the approved deployment copy with the manifest version.'
}

$port = 5076
if ($settings['SMARTPARK_ASSISTANT_DOCKER_PORT'] -and
    (![int]::TryParse($settings['SMARTPARK_ASSISTANT_DOCKER_PORT'], [ref]$port) -or $port -lt 1 -or $port -gt 65535)) {
    throw 'Invalid SMARTPARK_ASSISTANT_DOCKER_PORT.'
}
$origin = [string]$settings['Assistant__Guest__AllowedOrigins__0']
if (!$origin) { $origin = 'http://localhost:5173' }
$originUri = $null
if (![Uri]::TryCreate($origin, [UriKind]::Absolute, [ref]$originUri) -or
    $originUri.Scheme -notin @('http','https') -or $originUri.UserInfo -or
    $originUri.GetLeftPart([UriPartial]::Authority) -cne $origin) {
    throw 'Assistant frontend origin must be a single HTTP(S) origin without a path.'
}
$guestRelease = [string]$settings['Assistant__Guest__ReleaseEnabled']
$chatRelease = [string]$settings['Assistant__Chat__ReleaseEnabled']
if (($guestRelease -and $guestRelease -cnotin @('true','false')) -or
    ($chatRelease -and $chatRelease -cnotin @('true','false'))) {
    throw 'Assistant release flags must use true or false.'
}
$adopt = [string]$settings['SMARTPARK_ASSISTANT_DOCKER_USE_EXISTING_KEYRING']
if ($adopt -and $adopt -cnotin @('true','false')) {
    throw 'SMARTPARK_ASSISTANT_DOCKER_USE_EXISTING_KEYRING must use true or false.'
}
if (!$adopt) { $adopt = 'false' }
$keyFiles = @(Get-ChildItem -LiteralPath $keyDirectory -Filter 'key-*.xml' -File)
if (($adopt -eq 'true' -and !$keyFiles.Count) -or
    ($keyFiles | Select-String -Pattern 'DpapiXmlDecryptor|DpapiNGXmlDecryptor' -Quiet)) {
    throw 'Existing Assistant data requires the original certificate-protected keyring. Windows DPAPI keys are not portable to this container.'
}
$composeEnv = @{
    # The Assistant and base app use separate files in the same Compose project.
    COMPOSE_IGNORE_ORPHANS = 'true'
    COMPOSE_REMOVE_ORPHANS = 'false'
    SMARTPARK_ASSISTANT_DOCKER_PORT = [string]$port
    SMARTPARK_ASSISTANT_SCHEMA_NETWORK = "${SchemaProject}_default"
    SMARTPARK_ASSISTANT_DOCKER_ISSUER = Get-RequiredSetting 'Assistant__Jwt__Issuer'
    SMARTPARK_ASSISTANT_DOCKER_AUDIENCE = Get-RequiredSetting 'Assistant__Jwt__Audience'
    SMARTPARK_ASSISTANT_DOCKER_PUBLIC_KEY = $publicKey
    SMARTPARK_ASSISTANT_DOCKER_KEYS = $keyDirectory
    SMARTPARK_ASSISTANT_DOCKER_CERT = $certificate
    SMARTPARK_ASSISTANT_DOCKER_CERT_PASSWORD = $certificatePassword
    SMARTPARK_ASSISTANT_DOCKER_KNOWLEDGE = $knowledge
    SMARTPARK_ASSISTANT_DOCKER_ORIGIN = $origin
    SMARTPARK_ASSISTANT_DOCKER_GUEST_RELEASE = $guestRelease
    SMARTPARK_ASSISTANT_DOCKER_CHAT_RELEASE = $chatRelease
}
foreach ($owner in @('User','Operations','Search')) {
    $enabledName = "Assistant__Reads__${owner}__Enabled"
    $originName = "Assistant__Reads__${owner}__Origin"
    $enabled = [string]$settings[$enabledName]
    if (!$enabled) { $enabled = 'false' }
    if ($enabled -notin @('true','false')) { throw "Invalid Assistant setting: $enabledName must be true or false." }
    $composeEnv[$enabledName] = $enabled
    $composeEnv[$originName] = ''
    if ($enabled -eq 'true') {
        $ownerOrigin = Get-RequiredSetting $originName
        $uri = $null
        if (![Uri]::TryCreate($ownerOrigin, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or
            $uri.UserInfo -or $uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment) {
            throw "Assistant read requires a fixed HTTPS origin: $originName."
        }
        if ($uri.IsLoopback) {
            throw "Invalid Docker Assistant origin: $originName points to the Assistant container. Use an HTTPS origin reachable from the container, or disable $enabledName until it is available."
        }
        $composeEnv[$originName] = $ownerOrigin
    }
}
$ageName = 'Assistant__Reads__Search__MaxAgeSeconds'
$skewName = 'Assistant__Reads__Search__ClockSkewSeconds'
$age = $skew = 0
if ($composeEnv['Assistant__Reads__Search__Enabled'] -eq 'true' -and
    (![int]::TryParse($settings[$ageName], [ref]$age) -or $age -lt 1 -or $age -gt 3600 -or
     ![int]::TryParse($settings[$skewName], [ref]$skew) -or $skew -lt 0 -or $skew -gt 60)) {
    throw "Enabled Assistant search needs $ageName (1-3600) and $skewName (0-60)."
}
$composeEnv[$ageName] = [string]$age
$composeEnv[$skewName] = [string]$skew
$userCaPath = $null
$userReadEnabled = $composeEnv['Assistant__Reads__User__Enabled'] -ceq 'true'
if ($settings['SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH']) {
    $userCaPath = Get-RequiredPath 'SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH' 'Leaf'
    $userCaPem = [IO.File]::ReadAllText($userCaPath)
    if ($userCaPem -notmatch '(?s)\A\s*-----BEGIN CERTIFICATE-----\s+[A-Za-z0-9+/=\r\n]+-----END CERTIFICATE-----\s*\z') {
        throw 'Assistant User TLS trust requires one public PEM certificate.'
    }
    $userCaCertificate = $null
    try {
        $userCaCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($userCaPath)
        if ($userCaCertificate.NotBefore -gt (Get-Date) -or $userCaCertificate.NotAfter -le (Get-Date)) {
            throw 'Expired User TLS trust certificate.'
        }
    } catch {
        throw 'Assistant User TLS trust requires a valid, unexpired public certificate.'
    } finally {
        if ($userCaCertificate) { $userCaCertificate.Dispose() }
    }
    $composeEnv['SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH'] = $userCaPath
}
if ($userReadEnabled -and !$userCaPath) {
    throw 'Assistant User read is enabled but SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH is missing. Provide the public CA that signed UserService TLS or disable Assistant__Reads__User__Enabled.'
}
if ($userReadEnabled -and [string]$settings['Assistant__Reads__User__Origin'] -ceq 'https://user-service:8443' -and
    [string]$settings['SMARTPARK_USER_DOCKER_TLS_ENABLED'] -cne 'true') {
    throw 'Assistant User origin is https://user-service:8443 but SMARTPARK_USER_DOCKER_TLS_ENABLED is not true. Start User with the TLS overlay before enabling this read.'
}
if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI not found. Start Docker Desktop and retry.' }
$endpoint = $env:DOCKER_HOST
if ($env:DOCKER_CONTEXT -or !$endpoint) {
    $contextArgs = @('context','inspect','--format','{{.Endpoints.docker.Host}}')
    if ($env:DOCKER_CONTEXT) { $contextArgs += $env:DOCKER_CONTEXT }
    $contextEndpoints = @(& docker @contextArgs 2>$null)
    if ($LASTEXITCODE -ne 0 -or $contextEndpoints.Count -ne 1) {
        throw 'Could not identify the Docker context. Start local Docker Desktop and retry.'
    }
    $endpoint = $contextEndpoints[0]
}
if ($endpoint -notmatch '^(npipe:////\./pipe/|unix:///)') {
    throw 'Assistant Docker setup requires a local named-pipe or Unix-socket Docker endpoint.'
}
if ($ValidateOnly) { return }

$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = 'iot-db'
$connection['Database'] = 'smartpark_iot'
$connection['Username'] = 'smartpark_iot'
$connection['Password'] = Get-RequiredSetting 'SMARTPARK_DEV_PASSWORD'
$composeEnv['SMARTPARK_ASSISTANT_DOCKER_CONNECTION'] = $connection.ConnectionString
$composeEnv['PGPASSWORD'] = $connection['Password']
$schemaCompose = @('compose','--project-name',$SchemaProject,'--env-file',$EnvPath,'-f',(Join-Path $root 'compose.schema-integration.yml'))
$assistantProject = if ($SchemaProject -eq 'smartpark-schema') { 'smartpark-app' } else { "$SchemaProject-assistant" }
$assistantCompose = @('compose','--project-name',$assistantProject,'--env-file',$EnvPath,'-f',(Join-Path $root 'compose.assistant.yml'))
if ($userCaPath) { $assistantCompose += @('-f', (Join-Path $root 'compose.assistant-trust.yml')) }

# New IoT volumes initialize Assistant through Compose. Keep this fallback for
# existing IoT volumes with an untouched Assistant base, in one transaction.
$plan = & (Join-Path $PSScriptRoot 'apply-workflow-migrations.ps1') -Service Iot -SchemaRoot $root -PlanAssistant
$bodies = foreach ($entry in $plan) {
    $body = Get-Content -LiteralPath (Join-Path $root $entry.File) -Raw -Encoding UTF8
    if ([regex]::Matches($body, '(?m)^BEGIN;\r?$').Count -ne 1 -or
        [regex]::Matches($body, '(?m)^COMMIT;\r?$').Count -ne 1) {
        throw 'Unexpected Assistant migration transaction layout. Review setup before applying it.'
    }
    $body -replace '(?m)^(BEGIN|COMMIT);\r?$', ''
}
$guard = @'
BEGIN;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(180213, 20261105);
DO $assistant_setup$
BEGIN
    IF current_database() <> 'smartpark_iot' OR current_user <> 'smartpark_iot'
        OR to_regclass('public.devices') IS NULL OR to_regclass('public.assistant_tool_runs') IS NULL
        OR to_regclass('public.assistant_conversations') IS NULL OR to_regclass('public.assistant_messages') IS NULL THEN
        RAISE EXCEPTION 'Expected the canonical local smartpark_iot base schema.';
    END IF;
END
$assistant_setup$;
SELECT (
    NOT EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname IN (
            'assistant_management_state','assistant_management_credentials','assistant_management_receipts',
            'assistant_connection_tests','assistant_requests'
        )
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND (
            (table_name = 'assistant_conversations' AND column_name IN ('owner_account_id','client_conversation_id','locale','revision','last_message_at','expires_at','retention_hold'))
            OR (table_name = 'assistant_messages' AND column_name IN ('sequence','client_message_id','response'))
        )
    )
) AS assistant_fresh \gset
\if :assistant_fresh
LOCK TABLE assistant_conversations, assistant_messages, assistant_tool_runs IN ACCESS EXCLUSIVE MODE;
DO $assistant_setup$
BEGIN
    IF EXISTS (SELECT 1 FROM assistant_conversations) OR EXISTS (SELECT 1 FROM assistant_messages)
        OR EXISTS (SELECT 1 FROM assistant_tool_runs) THEN
        RAISE EXCEPTION 'Existing Assistant history needs a reviewed conversion. Data was preserved.';
    END IF;
END
$assistant_setup$;
'@
$verify = @'
\endif
DO $assistant_setup$
BEGIN
    IF to_regclass('public.assistant_management_state') IS NULL
        OR to_regclass('public.assistant_management_credentials') IS NULL
        OR to_regclass('public.assistant_management_receipts') IS NULL
        OR to_regclass('public.assistant_connection_tests') IS NULL
        OR to_regclass('public.ux_assistant_conversation_client') IS NULL
        OR to_regclass('public.ux_assistant_message_sequence') IS NULL
        OR to_regclass('public.ux_assistant_request_running') IS NULL THEN
        RAISE EXCEPTION 'Assistant schema is incomplete. Review the IoT schema and migration history before retrying.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM assistant_management_state WHERE id = 1) THEN
        RAISE EXCEPTION 'Assistant management state is missing. Review the IoT schema before retrying.';
    END IF;
    PERFORM owner_account_id, client_conversation_id, locale, revision, last_message_at, expires_at, retention_hold
        FROM assistant_conversations LIMIT 0;
    PERFORM sequence, client_message_id, response FROM assistant_messages LIMIT 0;
    PERFORM id, conversation_id, client_message_id, state, deadline, snapshot FROM assistant_requests LIMIT 0;
    IF to_regclass('public.ux_assistant_conversation_current') IS NULL THEN
        RAISE EXCEPTION 'Assistant current chat index is missing.'
            USING HINT = 'For an intact S05 schema, apply the reviewed 20261110_iot_assistant_current_chat.sql. If chat_role already exists, review and repair the schema before retrying.';
    END IF;
    PERFORM chat_role FROM assistant_conversations LIMIT 0;
END
$assistant_setup$;
SELECT (EXISTS (SELECT 1 FROM assistant_management_credentials)
    OR EXISTS (SELECT 1 FROM assistant_conversations)) AS assistant_has_data \gset
\if :assistant_has_data
\if :assistant_adopt
\else
DO $assistant_setup$
BEGIN
    RAISE EXCEPTION 'Existing Assistant data needs its original certificate and keyring.'
        USING HINT = 'After verifying both match the existing IoT data, set SMARTPARK_ASSISTANT_DOCKER_USE_EXISTING_KEYRING=true in the environment file and retry.';
END
$assistant_setup$;
\endif
\endif
COMMIT;
'@

function Invoke-AssistantDocker([string[]]$Arguments, [string]$Failure, [string]$InputSql, [switch]$Quiet) {
    # Native progress and PostgreSQL notices use stderr in Windows PowerShell.
    $ErrorActionPreference = 'Continue'
    if ($InputSql) {
        $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $InputSql | & docker @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
    } elseif ($Quiet) {
        & docker @Arguments 2>&1 | Out-Null
    } else {
        & docker @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
    }
    if ($LASTEXITCODE -ne 0) { throw $Failure }
}

$previous = @{}
try {
    if ($assistantProject -eq 'smartpark-app') {
        $legacy = @(& docker ps --quiet --filter 'label=com.docker.compose.project=smartpark-assistant' --filter 'label=com.docker.compose.service=assistant')
        if ($LASTEXITCODE -ne 0) { throw 'Could not check for the previous Assistant container.' }
        if ($legacy.Count) { throw 'Stop the previous smartpark-assistant Assistant container before starting Assistant in smartpark-app. Keep its DB volume and keyring.' }
    }
    foreach ($name in $composeEnv.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, [string]$composeEnv[$name], 'Process')
    }
    Invoke-AssistantDocker ($assistantCompose + @('config','--quiet')) 'Invalid Docker Assistant configuration.' -Quiet
    Invoke-AssistantDocker @('network','inspect',"${SchemaProject}_default",'--format','{{.Name}}') 'Start the local SmartPark DB stack before setting up Assistant.'
    if ($NoBuild) {
        Invoke-AssistantDocker @('image','inspect','smartpark/assistant:local','--format','{{.Id}}') 'Assistant image is missing. Run setup without -NoBuild.'
    } else {
        Invoke-AssistantDocker ($assistantCompose + @('build','assistant')) 'Could not build the Assistant image.'
    }
    Invoke-AssistantDocker ($schemaCompose + @('exec','-T','-e','PGPASSWORD','iot-db','psql','-h','iot-db','-X','-q','-U','smartpark_iot','-d','smartpark_iot','-v','ON_ERROR_STOP=1','-v',"assistant_adopt=$adopt")) `
        'Assistant database setup stopped. See the PostgreSQL error above, then resolve it before retrying.' `
        ((@($guard) + $bodies + @($verify)) -join "`n")
    Invoke-AssistantDocker ($assistantCompose + @('up','-d','--wait','--wait-timeout','90','--no-build','assistant')) `
        'Assistant did not become healthy. Check its Docker logs and the configured public key/certificate.'
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}

$url = "http://localhost:$port"
$frontendEnv = Join-Path $root 'FE/.env'
if (Test-Path -LiteralPath (Join-Path $root 'FE') -PathType Container) {
    $existingUrl = $null
    if (Test-Path -LiteralPath $frontendEnv -PathType Leaf) {
        $frontendSettings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $frontendEnv -NoExport
        $existingUrl = $frontendSettings['VITE_ASSISTANT_API_BASE_URL']
    }
    if (!$existingUrl) {
        $lines = if (Test-Path -LiteralPath $frontendEnv -PathType Leaf) {
            @(Get-Content -LiteralPath $frontendEnv -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*VITE_ASSISTANT_API_BASE_URL\s*=' })
        } else { @() }
        [IO.File]::WriteAllLines($frontendEnv, [string[]](@($lines) + "VITE_ASSISTANT_API_BASE_URL=$url"), (New-Object Text.UTF8Encoding($false)))
    } elseif ($existingUrl -cne $url) {
        Write-Warning "Existing Assistant URL in FE/.env was preserved. Use $url there if you want the Docker Assistant."
    }
}
Write-Host "Assistant container is live at $url. Run scripts/run-app-service.ps1 -Service Frontend -NoBuild to refresh an existing Docker frontend."
Write-Host 'Admin Settings, provider activation and FAQ/chat readiness are checked separately.'
