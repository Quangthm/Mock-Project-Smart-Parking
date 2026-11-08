# Static, read-only readiness report. Never export env, query DB or call providers.
param([Parameter(Mandatory)][string]$EnvPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport
function Report([string]$Check, [string]$Status, [string]$Detail) {
    [pscustomobject]@{ Check = $Check; Status = $Status; Detail = $Detail }
}
function Present([string]$Name) {
    $value = $settings[$Name]
    return ![string]::IsNullOrWhiteSpace($value) -and !$value.StartsWith('replace-with-')
}
foreach ($name in @('SMARTPARK_ASSISTANT_URL','ConnectionStrings__IoT','Assistant__Jwt__Issuer',
    'Assistant__Jwt__Audience','Assistant__Jwt__PublicKeyPem')) {
    if ($name -eq 'Assistant__Jwt__PublicKeyPem' -and (Present 'Assistant__Jwt__PublicKeyFile')) {
        $file = $settings['Assistant__Jwt__PublicKeyFile']
        $absolute = [IO.Path]::IsPathRooted($file)
        if ([IO.Path]::DirectorySeparatorChar -eq '\') {
            $absolute = $file -match '^[A-Za-z]:[\\/]' -or $file -match '^\\\\[^\\/]+[\\/][^\\/]+'
        }
        if ($absolute -and (Test-Path -LiteralPath $file -PathType Leaf)) {
            Report $name 'CONFIGURED' 'Public-key file present; host runner loads it, cryptographic validity unverified.'
        } else { Report $name 'MISSING' 'Public-key file must exist and be absolute.' }
    }
    elseif (Present $name) { Report $name 'CONFIGURED' 'Present; value redacted, runtime validity unverified.' }
    else { Report $name 'MISSING' 'Configure locally after review.' }
}
foreach ($name in @('Assistant__Management__KeyDirectory','Assistant__Guest__KnowledgeDirectory')) {
    $path = $settings[$name]
    $absolute = $false
    if (Present $name) {
        $absolute = [IO.Path]::IsPathRooted($path)
        if ([IO.Path]::DirectorySeparatorChar -eq '\') {
            $absolute = $path -match '^[A-Za-z]:[\\/]' -or $path -match '^\\\\[^\\/]+[\\/][^\\/]+'
        }
    }
    if ($absolute -and (Test-Path -LiteralPath $path -PathType Container)) {
        Report $name 'CONFIGURED' 'Existing absolute directory; permissions/protection unverified.'
    } else { Report $name 'MISSING' 'An existing absolute directory is required; nothing created.' }
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
    $uri = $null
    if ($flag -ne 'true' -or ![Uri]::TryCreate($settings[$name], [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment) {
        Report $name 'INVALID' 'Enabled reads need fixed HTTPS origins.'
    } else { Report $name 'CONFIGURED' 'HTTPS origin configured; TLS/auth/reachability unverified.' }
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
    @('smartpark_facility','20261106_facility_discovery_publication.sql'))) {
    $exists = Test-Path -LiteralPath (Join-Path $root "scripts/database/migrations/$($migration[1])") -PathType Leaf
    if (!$exists) { Report $migration[1] 'MISSING' 'Required SQL source missing.' }
    else { Report $migration[1] 'UNVERIFIED' "Review target $($migration[0]) and actual datasource/schema; no SQL executed." }
}
foreach ($check in @('SDK and prerequisites','DB authentication/schema/legacy data','JWT validation and Account isolation',
    'Keyring decrypt/restart','Owner TLS/trust/site bindings/freshness','Provider connection','Functional readiness')) {
    Report $check 'UNVERIFIED' 'Requires separately authorized environment verification; static report is not a pass.'
}
