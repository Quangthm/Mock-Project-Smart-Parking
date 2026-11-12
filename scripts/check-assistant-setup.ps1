# Static, read-only readiness report. Never export env, query DB or call providers.
param([Parameter(Mandatory)][string]$EnvPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$envFile = (Resolve-Path -LiteralPath $EnvPath).Path
$envDirectory = Split-Path -Parent $envFile
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile -NoExport
$dockerEnabled = $settings['SMARTPARK_ASSISTANT_DOCKER_ENABLED'] -ceq 'true'
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
    $path = Resolve-ConfiguredPath ([string]$settings[$name])
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
    if ($settings[$name] -eq 'true') { Report $name 'CONFIGURED' 'Release flag enabled; runtime gates still apply.' }
    elseif (!$settings[$name] -or $settings[$name] -eq 'false') { Report $name 'DISABLED' 'Admission disabled.' }
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
$knowledge = $settings['Assistant__Guest__KnowledgeDirectory']
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
