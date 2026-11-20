#requires -Version 5.1
<#
.SYNOPSIS
Check Assistant configuration, inspect signing or print schema inventory SQL.
.DESCRIPTION
Readiness reads configuration and files without running external tools, contacting
services or exporting environment variables. InspectSigning reports source names
and public fingerprints. SchemaSql prints SQL without opening a database connection.
#>
[CmdletBinding(DefaultParameterSetName = 'Readiness')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Readiness', Position = 0)]
    [Parameter(ParameterSetName = 'InspectSigning')]
    [string]$EnvPath = '.env',

    [Parameter(Mandatory, ParameterSetName = 'InspectSigning')][switch]$InspectSigning,
    [Parameter(ParameterSetName = 'InspectSigning')][string]$UserSecretsPath,
    [Parameter(ParameterSetName = 'InspectSigning')][uri]$UserJwksUri,

    [Parameter(Mandatory, ParameterSetName = 'SchemaSql')][switch]$SchemaSql
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-AssistantSigningReport {
    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')
    Import-Module (Join-Path $root 'src/Services/IoTService/Tools/Assistant/AssistantSigning.psm1') -Scope Local
    if ($UserJwksUri -and ($UserJwksUri.Scheme -notin @('http','https') -or
        !$UserJwksUri.IsLoopback -or $UserJwksUri.UserInfo -or $UserJwksUri.Fragment)) {
        throw 'UserJwksUri must be an explicit loopback HTTP(S) endpoint without credentials or fragment.'
    }
    if (![IO.Path]::IsPathRooted($EnvPath)) { $EnvPath = Join-Path $root $EnvPath }
    $envDirectory = Split-Path ([IO.Path]::GetFullPath($EnvPath)) -Parent
    $records = [Collections.Generic.List[object]]::new()
    function Add-KeySource([string]$Source, [string]$Value, [string]$Kind) {
        $status = 'MISSING'; $fingerprint = $null
        if (Test-JwtConfiguredValue $Value) {
            $status = 'INVALID'
            try {
                if ($Kind -eq 'FILE') { $Value = Read-JwtPrivateKeyFile $Value $envDirectory }
                elseif ($Kind -eq 'BASE64') { $Value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Value)) }
                elseif ($Kind -eq 'ENVPEM') { $Value = $Value.Replace('\n', "`n") }
                elseif ($Kind -ne 'PEM') { throw 'Unsupported signing alias.' }
                $fingerprint = (Invoke-AssistantCrypto @{Operation='private';Pem=$Value}).Fingerprint
                $status = 'CONFIGURED'
            } catch [InvalidOperationException] { throw } catch { $status = 'INVALID' }
        }
        $records.Add([pscustomobject]@{ Source=$Source; Status=$status; PublicSpkiSha256=$fingerprint; MatchesLiveJwks=$null })
    }
    $settings = @{}
    if (Test-Path -LiteralPath $EnvPath -PathType Leaf) {
        try { $settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport }
        catch { $records.Add([pscustomobject]@{Source='.env file';Status='UNREADABLE';PublicSpkiSha256=$null;MatchesLiveJwks=$null}) }
    }
    foreach ($slot in @(
        @('SMARTPARK_JWT_PRIVATE_KEY_FILE','FILE'),
        @('SMARTPARK_JWT_PRIVATE_KEY_PEM','ENVPEM'),
        @('SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64','BASE64'))) {
        Add-KeySource ('.env:' + $slot[0]) $settings[$slot[0]] $slot[1]
    }
    $processSources = Get-JwtSigningEnvironment
    if (!$processSources.Count) { Add-KeySource 'process:signing aliases' '' 'PEM' }
    foreach ($name in ($processSources.Keys | Sort-Object)) {
        $normalized = $name.Replace('_','').Replace(':','')
        $kind = if ($normalized -match 'PRIVATEKEYPEMBASE64$') { 'BASE64' }
            elseif ($normalized -match 'PRIVATEKEYPEM$') { 'PEM' }
            elseif ($normalized -match 'PRIVATEKEYFILE$') { 'FILE' } else { 'UNKNOWN' }
        Add-KeySource ('process:' + $name) $processSources[$name] $kind
    }
    if (!$UserSecretsPath) {
        $project = Join-Path $root 'src/Services/UserService/SmartParking.UserService.API/SmartParking.UserService.API.csproj'
        $id = ([xml][IO.File]::ReadAllText($project)).Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1
        $secretsDirectory = if ($env:APPDATA) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' }
            else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
        $UserSecretsPath = Join-Path $secretsDirectory "$id/secrets.json"
    } elseif (![IO.Path]::IsPathRooted($UserSecretsPath)) { $UserSecretsPath = Join-Path $root $UserSecretsPath }
    $secrets = @{}
    if (Test-Path -LiteralPath $UserSecretsPath -PathType Leaf) {
        try { $secrets = ConvertFrom-AssistantSigningJson ([IO.File]::ReadAllText($UserSecretsPath)) }
        catch { $records.Add([pscustomobject]@{Source='UserService user-secrets file';Status='UNREADABLE';PublicSpkiSha256=$null;MatchesLiveJwks=$null}) }
    }
    foreach ($name in @('PrivateKeyPem','PrivateKeyPemBase64')) {
        $kind = if ($name.EndsWith('Base64')) { 'BASE64' } else { 'PEM' }
        Add-KeySource ('UserService user-secrets:Jwt:' + $name) $secrets["Jwt:$name"] $kind
        if ($secrets['Jwt'] -is [Collections.IDictionary] -and $secrets['Jwt'].Contains($name)) {
            Add-KeySource ('UserService user-secrets:nested Jwt.' + $name) $secrets['Jwt'][$name] $kind
        }
    }
    $liveFingerprints = @(); $jwksStatus = 'NOT_REQUESTED'
    if ($UserJwksUri) {
        try {
            # No redirects: a local endpoint must not redirect this request elsewhere.
            $jwks = Invoke-RestMethod -Uri $UserJwksUri -TimeoutSec 10 -MaximumRedirection 0 -ErrorAction Stop
            foreach ($key in $jwks.keys) {
                if ($key.kty -ne 'RSA') { continue }
                $liveFingerprints += (Invoke-AssistantCrypto @{Operation='public';Modulus=[string]$key.n;Exponent=[string]$key.e}).Fingerprint
            }
            if (!$liveFingerprints.Count) { throw 'No RSA public keys.' }
            $jwksStatus = 'READ'
            foreach ($record in $records) {
                if ($record.Status -eq 'CONFIGURED') { $record.MatchesLiveJwks = $record.PublicSpkiSha256 -in $liveFingerprints }
            }
        } catch { $jwksStatus = 'UNAVAILABLE_OR_INVALID' }
    }
    $distinct = @($records | Where-Object Status -eq 'CONFIGURED' | Select-Object -ExpandProperty PublicSpkiSha256 -Unique)
    # Structured output prevents table truncation; only explicitly safe fields leave the script.
    [pscustomobject]@{
        Sources = @($records.ToArray())
        DistinctPrivateKeyCount = $distinct.Count
        MultipleDifferentKeys = $distinct.Count -gt 1
        LiveJwksStatus = $jwksStatus
        LivePublicSpkiSha256 = @($liveFingerprints)
        Note = 'Read-only inventory; no key selected. A fingerprint match identifies public-key identity, not configuration precedence. Missing live matches do not authorize rotation.'
    } | ConvertTo-Json -Depth 5
}

if ($PSCmdlet.ParameterSetName -eq 'InspectSigning') {
    if (!$InspectSigning) { throw '-InspectSigning must be enabled for signing inspection.' }
    Get-AssistantSigningReport
    return
}
if ($PSCmdlet.ParameterSetName -eq 'SchemaSql') {
    if (!$SchemaSql) { throw '-SchemaSql must be enabled to print schema inventory SQL.' }
    @'
-- REVIEW ONLY: diagnostic inventory, not a migration or readiness certificate.
-- Execute only after approval against the reviewed IoT datasource and schema.
-- Current migrations use unqualified tables: this inventory assumes public.
-- If datasource search_path differs, stop and review schema mapping first.
-- psql: use the selected connection/service securely, ON_ERROR_STOP=1.
-- No application rows, credentials, JWTs, plaintext or ciphertext are returned.
BEGIN READ ONLY;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL statement_timeout = '5s';
SET LOCAL lock_timeout = '1s';

WITH expected(table_name, column_name, type_name, required_not_null) AS (
    VALUES
    ('assistant_management_state','id','integer',true),
    ('assistant_management_state','state','jsonb',true),
    ('assistant_management_credentials','slot_id','uuid',true),
    ('assistant_management_credentials','version','uuid',true),
    ('assistant_management_credentials','protected_value','text',true),
    ('assistant_management_receipts','actor','uuid',true),
    ('assistant_management_receipts','mutation_id','uuid',true),
    ('assistant_management_receipts','fingerprint','text',true),
    ('assistant_management_receipts','revision','bigint',true),
    ('assistant_management_receipts','action','text',true),
    ('assistant_management_receipts','at','timestamp with time zone',true),
    ('assistant_connection_tests','actor','uuid',true),
    ('assistant_connection_tests','request_id','uuid',true),
    ('assistant_connection_tests','configuration_id','uuid',true),
    ('assistant_connection_tests','revision','bigint',true),
    ('assistant_connection_tests','model','text',true),
    ('assistant_connection_tests','admitted_at','timestamp with time zone',true),
    ('assistant_connection_tests','result','jsonb',false),
    ('assistant_conversations','id','uuid',true),
    ('assistant_conversations','owner_account_id','uuid',true),
    ('assistant_conversations','client_conversation_id','uuid',true),
    ('assistant_conversations','locale','character varying(2)',true),
    ('assistant_conversations','revision','bigint',true),
    ('assistant_conversations','expires_at','timestamp with time zone',true),
    ('assistant_conversations','retention_hold','boolean',true),
    ('assistant_messages','id','uuid',true),
    ('assistant_messages','conversation_id','uuid',true),
    ('assistant_messages','sequence','bigint',true),
    ('assistant_messages','client_message_id','uuid',true),
    ('assistant_messages','response','jsonb',true),
    ('assistant_requests','id','uuid',true),
    ('assistant_requests','conversation_id','uuid',true),
    ('assistant_requests','owner_account_id','uuid',true),
    ('assistant_requests','client_message_id','uuid',true),
    ('assistant_requests','fingerprint','character varying(64)',true),
    ('assistant_requests','state','character varying(16)',true),
    ('assistant_requests','deadline','timestamp with time zone',true),
    ('assistant_requests','created_at','timestamp with time zone',true),
    ('assistant_requests','finished_at','timestamp with time zone',false),
    ('assistant_requests','user_message_id','uuid',true),
    ('assistant_requests','answer','jsonb',false),
    ('assistant_requests','error_code','character varying(64)',false),
    ('assistant_requests','snapshot','jsonb',true)
), observed AS (
    SELECT e.*, c.oid AS table_oid, c.relkind, a.attname,
        pg_catalog.format_type(a.atttypid,a.atttypmod) AS actual_type, a.attnotnull
    FROM expected e
    LEFT JOIN pg_catalog.pg_namespace n ON n.nspname='public'
    LEFT JOIN pg_catalog.pg_class c ON c.relnamespace=n.oid AND c.relname=e.table_name
    LEFT JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attname=e.column_name
        AND a.attnum>0 AND NOT a.attisdropped
)
SELECT table_name, column_name,
    CASE WHEN table_oid IS NULL THEN 'MISSING_TABLE'
         WHEN relkind NOT IN ('r','p') THEN 'WRONG_RELATION_KIND'
         WHEN attname IS NULL THEN 'MISSING_COLUMN'
         WHEN actual_type<>type_name THEN 'TYPE_MISMATCH'
         WHEN attnotnull<>required_not_null THEN 'NULLABILITY_MISMATCH'
         ELSE 'COLUMN_SHAPE_MATCH' END AS diagnostic
FROM observed ORDER BY table_name,column_name;

-- Inventory definitions for review: matching names alone cannot prove correctness.
SELECT tablename, indexname, indexdef
FROM pg_catalog.pg_indexes
WHERE schemaname='public' AND tablename IN (
    'assistant_management_state','assistant_management_credentials','assistant_management_receipts',
    'assistant_connection_tests','assistant_conversations','assistant_messages','assistant_requests')
ORDER BY tablename,indexname;

SELECT c.relname AS table_name, con.conname, pg_catalog.pg_get_constraintdef(con.oid) AS definition,
    con.convalidated AS validated
FROM pg_catalog.pg_constraint con
JOIN pg_catalog.pg_class c ON c.oid=con.conrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname='public' AND c.relname IN (
    'assistant_management_state','assistant_management_credentials','assistant_management_receipts',
    'assistant_connection_tests','assistant_conversations','assistant_messages','assistant_requests')
ORDER BY c.relname,con.conname;
ROLLBACK;
-- Missing new ownership/sequence fields on existing conversation/message tables
-- identifies a legacy/partial shape, not permission to reset or infer Account IDs.
-- Even all COLUMN_SHAPE_MATCH does not establish complete schema, privileges,
-- JSON state migration, legacy-row conversion, locks/concurrency or runtime readiness.
'@
    return
}

# Default static readiness report.
$envFile = (Resolve-Path -LiteralPath $EnvPath).Path
$envDirectory = Split-Path -Parent $envFile
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile -NoExport
$dockerEnabled = $settings['SMARTPARK_ASSISTANT_DOCKER_ENABLED'] -ceq 'true'
. (Join-Path $PSScriptRoot 'assistant-knowledge-path.ps1')
$knowledge = Resolve-AssistantKnowledgeDirectory ([string]$settings['Assistant__Guest__KnowledgeDirectory']) $root $envDirectory -UseBundledDefault:$dockerEnabled
function Report([string]$Check, [string]$Status, [string]$Detail) {
    [pscustomobject]@{ Check = $Check; Status = $Status; Detail = $Detail }
}
function Present([string]$Name) {
    $value = $settings[$Name]
    return ![string]::IsNullOrWhiteSpace($value) -and !$value.StartsWith('replace-with-')
}
function Resolve-ConfiguredPath([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if ([IO.Path]::IsPathRooted($Value)) { return $Value }
    return Join-Path $envDirectory $Value
}
foreach ($name in @('SMARTPARK_ASSISTANT_URL','ConnectionStrings__IoT','Assistant__Jwt__Issuer',
    'Assistant__Jwt__Audience','Assistant__Jwt__PublicKeyPem')) {
    if ($name -in @('SMARTPARK_ASSISTANT_URL','ConnectionStrings__IoT') -and $dockerEnabled -and !(Present $name)) {
        Report $name 'NOT_REQUIRED' 'Docker setup derives the IoT connection and uses the Docker Assistant port.'
        continue
    }
    if ($name -in @('SMARTPARK_ASSISTANT_URL','ConnectionStrings__IoT') -and !$dockerEnabled -and !(Present $name)) {
        Report $name 'MISSING' 'Required when using the host Assistant runner.'
        continue
    }
    if ($name -eq 'Assistant__Jwt__PublicKeyPem' -and (Present 'Assistant__Jwt__PublicKeyFile')) {
        $file = Resolve-ConfiguredPath ([string]$settings['Assistant__Jwt__PublicKeyFile'])
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            Report $name 'CONFIGURED' 'Public-key file present; path may be relative to .env, cryptographic validity unverified.'
        } else { Report $name 'MISSING' 'Configured public-key file does not exist.' }
    }
    elseif (Present $name) { Report $name 'CONFIGURED' 'Present; value redacted, runtime validity unverified.' }
    else { Report $name 'MISSING' 'Configure locally after review.' }
}
foreach ($name in @('Assistant__Management__KeyDirectory','Assistant__Guest__KnowledgeDirectory')) {
    $path = if ($name -eq 'Assistant__Guest__KnowledgeDirectory') { $knowledge }
        else { Resolve-ConfiguredPath ([string]$settings[$name]) }
    if ($path -and (Test-Path -LiteralPath $path -PathType Container)) {
        Report $name 'CONFIGURED' 'Existing directory; path may be relative to .env, permissions/protection unverified.'
    } else { Report $name 'MISSING' 'An existing keyring/knowledge directory is required; nothing created.' }
}
$certificatePath = Resolve-ConfiguredPath ([string]$settings['Assistant__Management__KeyEncryptionCertificatePath'])
if ($certificatePath -and (Test-Path -LiteralPath $certificatePath -PathType Leaf) -and
    (Present 'Assistant__Management__KeyEncryptionCertificatePassword')) {
    Report 'Assistant__Management__KeyEncryptionCertificatePath' 'CONFIGURED' 'PFX and password are present; certificate validity is checked during setup.'
} else {
    Report 'Assistant__Management__KeyEncryptionCertificatePath' 'MISSING' 'An existing PFX path and password are required for the Assistant keyring.'
}
if (!$settings['SMARTPARK_ASSISTANT_DOCKER_ENABLED'] -or $settings['SMARTPARK_ASSISTANT_DOCKER_ENABLED'] -eq 'false') {
    Report 'SMARTPARK_ASSISTANT_DOCKER_ENABLED' 'DISABLED' 'Docker Assistant provisioning is disabled.'
} elseif ($dockerEnabled) {
    Report 'SMARTPARK_ASSISTANT_DOCKER_ENABLED' 'CONFIGURED' 'Docker Assistant provisioning is enabled.'
} else {
    Report 'SMARTPARK_ASSISTANT_DOCKER_ENABLED' 'INVALID' 'Expected true or false.'
}
foreach ($name in @('Assistant__Guest__ReleaseEnabled','Assistant__Chat__ReleaseEnabled')) {
    if ($settings[$name] -ceq 'true') { Report $name 'CONFIGURED' 'Release flag enabled; runtime gates still apply.' }
    elseif (!$settings[$name] -or $settings[$name] -ceq 'false') { Report $name 'DISABLED' 'Admission disabled.' }
    else { Report $name 'INVALID' 'Expected true or false.' }
}
foreach ($owner in @('User','Operations','Search')) {
    $flag = $settings["Assistant__Reads__${owner}__Enabled"]
    $name = "Assistant__Reads__${owner}__Origin"
    if (!$flag -or $flag -eq 'false') { Report $name 'DISABLED' 'Owner read disabled.'; continue }
    $origin = [string]$settings[$name]
    $uri = $null
    if ($flag -ne 'true' -or [string]::IsNullOrWhiteSpace($origin) -or
        ![Uri]::TryCreate($origin, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment) {
        Report $name 'INVALID' 'Enabled reads need fixed HTTPS origins.'
    } elseif ($settings['SMARTPARK_ASSISTANT_DOCKER_ENABLED'] -ceq 'true' -and $uri.IsLoopback) {
        Report $name 'INVALID' 'Docker Assistant is enabled. Use an HTTPS origin reachable from the container.'
    } else { Report $name 'CONFIGURED' 'HTTPS origin configured; TLS/auth/reachability unverified.' }
}
$userReadEnabled = $settings['Assistant__Reads__User__Enabled'] -ceq 'true'
if (!$userReadEnabled) {
    Report 'SMARTPARK_USER_DOCKER_TLS_ENABLED' 'NOT_REQUIRED' 'User vehicle read is disabled.'
} else {
    if ($settings['SMARTPARK_USER_DOCKER_TLS_ENABLED'] -cne 'true') {
        Report 'SMARTPARK_USER_DOCKER_TLS_ENABLED' 'INVALID' 'User vehicle read requires the Docker User TLS overlay.'
    } else {
        Report 'SMARTPARK_USER_DOCKER_TLS_ENABLED' 'CONFIGURED' 'TLS overlay enabled; certificate SAN/reachability are checked during setup.'
    }
    foreach ($name in @('SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH','SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH')) {
        $path = Resolve-ConfiguredPath ([string]$settings[$name])
        if ($path -and (Test-Path -LiteralPath $path -PathType Leaf)) {
            Report $name 'CONFIGURED' 'Certificate file present; cryptographic validity is checked during setup.'
        } else {
            Report $name 'MISSING' 'An existing certificate/PFX file is required when User vehicle read is enabled.'
        }
    }
    if (Present 'SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD') {
        Report 'SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD' 'CONFIGURED' 'Password is present; value redacted.'
    } else {
        Report 'SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD' 'MISSING' 'TLS PFX password is required when User vehicle read is enabled.'
    }
}
if ($settings['Assistant__Reads__Search__Enabled'] -eq 'true') {
    foreach ($bounds in @(@('MaxAgeSeconds', 1, 3600), @('ClockSkewSeconds', 0, 60))) {
        $name = 'Assistant__Reads__Search__' + $bounds[0]
        $seconds = 0
        if (![int]::TryParse($settings[$name], [ref]$seconds) -or $seconds -lt $bounds[1] -or $seconds -gt $bounds[2]) {
            Report $name 'INVALID' "Enabled search requires an integer in $($bounds[1])-$($bounds[2])."
        } else { Report $name 'CONFIGURED' 'Value is in bounds; deployment freshness policy unverified.' }
    }
}
foreach ($file in @('smartpark-knowledge.md','publication.json')) {
    if ($knowledge -and (Test-Path -LiteralPath (Join-Path $knowledge $file) -PathType Leaf)) {
        Report "Corpus/$file" 'UNVERIFIED' 'File present; host must validate content/hash/version/approval/expiry.'
    } else { Report "Corpus/$file" 'MISSING' 'Approved runtime corpus required; nothing published.' }
}
foreach ($migration in @(
    @('smartpark_iot','20261103_iot_assistant_management.sql'),
    @('smartpark_iot','20261105_iot_assistant_s02.sql'),
    @('smartpark_iot','20261105_iot_assistant_s05.sql'),
    @('smartpark_iot','20261110_iot_assistant_current_chat.sql'),
    @('smartpark_facility','20261106_facility_discovery_publication.sql'))) {
    $exists = Test-Path -LiteralPath (Join-Path $root "scripts/database/migrations/$($migration[1])") -PathType Leaf
    if (!$exists) { Report $migration[1] 'MISSING' 'Required SQL source missing.' }
    else { Report $migration[1] 'UNVERIFIED' "Review target $($migration[0]) and actual datasource/schema; no SQL executed." }
}
foreach ($check in @('SDK and prerequisites','DB authentication/schema/legacy data','JWT validation and Account isolation',
    'Keyring decrypt/restart','Owner TLS/trust/site bindings/freshness','Provider connection','Functional readiness')) {
    Report $check 'UNVERIFIED' 'Requires separately authorized environment verification; static report is not a pass.'
}
