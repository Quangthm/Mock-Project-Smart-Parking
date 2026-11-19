#requires -Version 5.1
<#
.SYNOPSIS
Set up Assistant Docker or explicitly select a local provisioning operation.
.DESCRIPTION
The default mode preserves Docker setup and preflight. Initialize provisions local
signing and TLS material. ExportSigning copies an explicitly verified existing key
into .cache. PublishKnowledge prepares a deployment copy after recorded Admin review.
#>
[CmdletBinding(DefaultParameterSetName = 'Docker')]
param(
    [Parameter(ParameterSetName = 'Docker', Position = 0)]
    [Parameter(ParameterSetName = 'Initialize')]
    [Parameter(ParameterSetName = 'ExportSigning')]
    [string]$EnvPath,

    [Parameter(ParameterSetName = 'Docker')][switch]$ValidateOnly,
    [Parameter(ParameterSetName = 'Docker')][switch]$NoBuild,
    [Parameter(ParameterSetName = 'Docker')]
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$SchemaProject = 'smartpark-schema',

    [Parameter(Mandatory, ParameterSetName = 'Initialize')][switch]$Initialize,
    [Parameter(ParameterSetName = 'Initialize')][switch]$UseExistingSigningKey,

    [Parameter(Mandatory, ParameterSetName = 'ExportSigning')][switch]$ExportSigning,
    [Parameter(Mandatory, ParameterSetName = 'ExportSigning')][string]$Source,
    [Parameter(ParameterSetName = 'ExportSigning')][string]$UserSecretsPath,
    [Parameter(ParameterSetName = 'ExportSigning')][uri]$UserJwksUri,
    [Parameter(ParameterSetName = 'ExportSigning')][switch]$ConfirmExistingSource,
    [Parameter(ParameterSetName = 'ExportSigning')][string]$OutputPath = '.cache/assistant-signing/user-private.pem',

    [Parameter(Mandatory, ParameterSetName = 'PublishKnowledge')][switch]$PublishKnowledge,
    [Parameter(Mandatory, ParameterSetName = 'PublishKnowledge')][string]$ApprovedBy,
    [Parameter(Mandatory, ParameterSetName = 'PublishKnowledge')][string]$ApprovalReference,
    [Parameter(ParameterSetName = 'PublishKnowledge')]
    [string]$SourcePath = 'docs/03-design/ai/p1/knowledge/smartpark-knowledge.md',
    [Parameter(ParameterSetName = 'PublishKnowledge')]
    [string]$OutputDirectory = '.cache/assistant-docker/knowledge'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Initialize-AssistantDocker {
    if (!$EnvPath) { $EnvPath = Join-Path $root '.env' }
    if (!(Test-Path -LiteralPath $EnvPath -PathType Leaf)) {
        throw 'Copy .env.example to .env before initializing the local Assistant.'
    }
    $EnvPath = (Resolve-Path -LiteralPath $EnvPath).Path
    $envDirectory = Split-Path $EnvPath -Parent
    $settings = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $EnvPath -NoExport
    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')

    $relativeDirectory = '.cache/assistant-docker'
    $directory = Join-Path $envDirectory $relativeDirectory
    $signingNames = @('SMARTPARK_JWT_PRIVATE_KEY_FILE', 'SMARTPARK_JWT_PRIVATE_KEY_PEM', 'SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64')
    $protectedNames = @(
        'SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH', 'SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD',
        'SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH', 'Assistant__Jwt__PublicKeyFile', 'Assistant__Jwt__PublicKeyPem',
        'Assistant__Management__KeyDirectory', 'Assistant__Management__KeyEncryptionCertificatePath',
        'Assistant__Management__KeyEncryptionCertificatePassword', 'Assistant__Guest__KnowledgeDirectory'
    )
    if (!$UseExistingSigningKey) { $protectedNames = $signingNames + $protectedNames }
    foreach ($name in $protectedNames) {
        if ((Test-JwtConfiguredValue $settings[$name]) -or
            (Test-JwtConfiguredValue ([Environment]::GetEnvironmentVariable($name, 'Process')))) {
            if ($settings['SMARTPARK_JWT_PRIVATE_KEY_FILE'] -ceq "$relativeDirectory/user-private.pem" -and
                (Test-Path -LiteralPath $directory -PathType Container)) {
                Write-Host 'Existing local Assistant files and settings were preserved. Run setup.ps1 -AssistantCheck to inspect the configuration.'
                return
            }
            throw "Existing local configuration found for $name. Keep its original JWT, certificates, and keyring and configure the Assistant inputs manually."
        }
    }
    if (Test-Path -LiteralPath $directory) {
        throw 'Local Assistant material directory already exists. Review it before retrying.'
    }
    if ((Get-JwtSigningEnvironment).Count) {
        throw 'Existing process JWT signing configuration found. Preserve it and configure Assistant manually.'
    }
    $userProject = Join-Path $root 'src/Services/UserService/SmartParking.UserService.API/SmartParking.UserService.API.csproj'
    $userSecretsId = ([xml][IO.File]::ReadAllText($userProject)).Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1
    $userSecretsDirectory = if ($env:APPDATA) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
    $userSecretsPath = Join-Path $userSecretsDirectory "$userSecretsId/secrets.json"
    if (!$UseExistingSigningKey -and (Test-Path -LiteralPath $userSecretsPath -PathType Leaf) -and
        [IO.File]::ReadAllText($userSecretsPath) -match '"(?:Jwt:)?PrivateKeyPem(?:Base64)?"\s*:') {
        throw 'Existing UserService signing material found in .NET user-secrets. Preserve it and configure Assistant manually.'
    }

    # Windows PowerShell 5.1 uses .NET Framework, which lacks the modern PEM and
    # certificate APIs. Run only cryptography in the already-required .NET 10 SDK.
    $cryptoInput = @{ UseExistingSigningKey = [bool]$UseExistingSigningKey }
    if ($UseExistingSigningKey) {
        $file = $settings[$signingNames[0]]
        $inline = $settings[$signingNames[1]]
        $base64 = $settings[$signingNames[2]]
        if (!($signingNames | Where-Object { Test-JwtConfiguredValue $settings[$_] })) {
            throw 'Set the existing UserService signing key in .env before using -UseExistingSigningKey. If it lives in user-secrets, preserve the same key in a private PEM file and configure SMARTPARK_JWT_PRIVATE_KEY_FILE.'
        }
        if (Test-JwtConfiguredValue $file) { $cryptoInput['Pem'] = Read-JwtPrivateKeyFile $file $envDirectory }
        elseif (Test-JwtConfiguredValue $inline) { $cryptoInput['Pem'] = $inline.Replace('\n', "`n") }
        if (Test-JwtConfiguredValue $base64) { $cryptoInput['Base64'] = $base64 }
        elseif ($cryptoInput['Pem'] -and $base64 -like 'replace-with-*') {
            throw 'Clear the Base64 template placeholder before using an existing PEM signing key.'
        }
    }
    Import-Module (Join-Path $root 'src/Services/IoTService/Tools/Assistant/AssistantSigning.psm1') -Scope Local
    $changes = @{
        SMARTPARK_USER_DOCKER_TLS_ENABLED = 'true'
        SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH = "$relativeDirectory/user-service.pfx"
        SMARTPARK_ASSISTANT_DOCKER_USER_CA_PATH = "$relativeDirectory/user-ca.crt"
        SMARTPARK_ASSISTANT_DOCKER_ENABLED = 'true'
        Assistant__Jwt__PublicKeyFile = "$relativeDirectory/user-public.pem"
        Assistant__Jwt__Issuer = if ($settings['SMARTPARK_JWT_ISSUER']) { $settings['SMARTPARK_JWT_ISSUER'] } else { 'SmartParking.UserService' }
        Assistant__Jwt__Audience = if ($settings['SMARTPARK_JWT_AUDIENCE']) { $settings['SMARTPARK_JWT_AUDIENCE'] } else { 'SmartParking' }
        Assistant__Management__KeyDirectory = "$relativeDirectory/keyring"
        Assistant__Management__KeyEncryptionCertificatePath = "$relativeDirectory/assistant.pfx"
        Assistant__Guest__KnowledgeDirectory = "$relativeDirectory/knowledge"
        Assistant__Guest__ReleaseEnabled = 'false'
        Assistant__Chat__ReleaseEnabled = 'true'
        Assistant__Reads__User__Enabled = 'true'
        Assistant__Reads__User__Origin = 'https://user-service:8443'
    }
    if (!$UseExistingSigningKey) {
        $changes['SMARTPARK_JWT_PRIVATE_KEY_FILE'] = "$relativeDirectory/user-private.pem"
        $changes['SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64'] = ''
    }
    $attempt = [Guid]::NewGuid().ToString('N')
    $stagingDirectory = "$directory-$attempt.tmp"
    $temporary = "$EnvPath.assistant-$attempt.tmp"
    $cryptoInput.Directory = $stagingDirectory
    try {
        $material = Invoke-AssistantCrypto -Request $cryptoInput -HelperName 'assistant-docker-crypto.cs'
        $changes['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD'] = $material.ServerPassword
        $changes['Assistant__Management__KeyEncryptionCertificatePassword'] = $material.ProtectionPassword
        $seen = @{}
        $updated = @(foreach ($line in [IO.File]::ReadAllLines($EnvPath)) {
            if ($line -match '^\s*([^#=\s]+)\s*=' -and $changes.ContainsKey($Matches[1])) {
                $name = $Matches[1]
                if (!$seen[$name]) { "$name=$($changes[$name])"; $seen[$name] = $true }
            } else { $line }
        })
        foreach ($name in ($changes.Keys | Sort-Object)) {
            if (!$seen[$name]) { $updated += "$name=$($changes[$name])" }
        }
        [IO.File]::WriteAllLines($temporary, $updated, [Text.UTF8Encoding]::new($false))
        # Directory.Move refuses an existing destination, including one created by a competing initializer.
        [IO.Directory]::Move($stagingDirectory, $directory)
        try {
            [IO.File]::Replace($temporary, $EnvPath, [System.Management.Automation.Language.NullString]::Value)
        } catch {
            [IO.Directory]::Move($directory, $stagingDirectory)
            throw
        }
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -ErrorAction Stop }
        if (Test-Path -LiteralPath $stagingDirectory) {
            $staged = Get-Item -LiteralPath $stagingDirectory -Force
            if ($staged.FullName -ne [IO.Path]::GetFullPath($stagingDirectory) -or !$staged.PSIsContainer -or
                ($staged.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Unexpected Assistant staging path. Inspect the directory before retrying.'
            }
            Remove-Item -LiteralPath $staged.FullName -Recurse -Force -ErrorAction Stop
        }
    }
    Write-Host 'Local Assistant JWT, keyring protection, and User HTTPS material configured in .env.'
    Write-Host 'For chat, deploy the approved Markdown and publication.json into the local knowledge directory, then configure the provider and enable Assistant in Admin Settings.'
}

function Export-AssistantSigning {
    . (Join-Path $PSScriptRoot 'jwt-environment.ps1')
    Import-Module (Join-Path $root 'src/Services/IoTService/Tools/Assistant/AssistantSigning.psm1') -Scope Local
    if (!$UserJwksUri -and !$ConfirmExistingSource) {
        throw 'Supply UserJwksUri to verify the live key, or ConfirmExistingSource after independently confirming the existing UserService source.'
    }
    if (![IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $root $OutputPath }
    $OutputPath = [IO.Path]::GetFullPath($OutputPath)
    $allowed = [IO.Path]::GetFullPath((Join-Path $root '.cache')) + [IO.Path]::DirectorySeparatorChar
    if (!$OutputPath.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPath must be inside the repository .cache directory.' }
    if (Test-Path -LiteralPath $OutputPath) { throw 'Output already exists; nothing was overwritten.' }
    if (!$EnvPath) { $EnvPath = '.env' }
    if (![IO.Path]::IsPathRooted($EnvPath)) { $EnvPath = Join-Path $root $EnvPath }
    $EnvPath = [IO.Path]::GetFullPath($EnvPath)
    $arguments = @{ EnvPath = $EnvPath }
    if ($UserSecretsPath) { $arguments.UserSecretsPath=$UserSecretsPath }
    if ($UserJwksUri) { $arguments.UserJwksUri=$UserJwksUri }
    $report = (& (Join-Path $PSScriptRoot 'check-assistant-setup.ps1') -InspectSigning @arguments) | ConvertFrom-Json
    $selected = @($report.Sources | Where-Object Source -ceq $Source)
    if ($selected.Count -ne 1 -or $selected[0].Status -ne 'CONFIGURED') { throw 'Select exactly one CONFIGURED source from the inspector output.' }
    if ($UserJwksUri -and ($report.LiveJwksStatus -ne 'READ' -or $selected[0].MatchesLiveJwks -ne $true)) { throw 'Selected key does not match the live UserService JWKS; nothing exported.' }
    $value = $null; $base64 = $false
    if ($Source.StartsWith('process:')) {
        $name = $Source.Substring('process:'.Length)
        $normalized = $name.Replace('_','').Replace(':','')
        $value = [Environment]::GetEnvironmentVariable($name, 'Process')
        if ($normalized -match 'PRIVATEKEYFILE$') {
            if (!(Test-JwtConfiguredValue $value)) { throw 'Selected signing source changed or is invalid; nothing exported.' }
            $value = Read-JwtPrivateKeyFile $value (Split-Path -Parent $EnvPath)
        } elseif ($normalized -notmatch 'PRIVATEKEYPEM(?:BASE64)?$') {
            throw 'Export supports process FILE, PEM and Base64 aliases only.'
        }
        $base64 = $normalized.EndsWith('BASE64', [StringComparison]::OrdinalIgnoreCase)
    } elseif ($Source.StartsWith('UserService user-secrets:')) {
        if (!$UserSecretsPath) {
            $project = Join-Path $root 'src/Services/UserService/SmartParking.UserService.API/SmartParking.UserService.API.csproj'
            $id = ([xml][IO.File]::ReadAllText($project)).Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1
            $directory = if ($env:APPDATA) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
            $UserSecretsPath = Join-Path $directory "$id/secrets.json"
        } elseif (![IO.Path]::IsPathRooted($UserSecretsPath)) { $UserSecretsPath=Join-Path $root $UserSecretsPath }
        try {
            $secrets = ConvertFrom-AssistantSigningJson ([IO.File]::ReadAllText($UserSecretsPath))
            $name = $Source.Substring('UserService user-secrets:'.Length)
            if ($name.StartsWith('nested Jwt.')) { $value=$secrets['Jwt'][$name.Substring('nested Jwt.'.Length)] }
            else { $value=$secrets[$name] }
            $base64 = $name.EndsWith('Base64', [StringComparison]::OrdinalIgnoreCase)
        } catch { throw 'Selected user-secrets source could not be read; nothing exported.' }
    } else { throw 'Export supports process and UserService user-secrets sources only. Existing .env sources already have a provisioning path.' }
    if ($base64) {
        try { $value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value)) }
        catch { throw 'Selected signing source changed or is invalid; nothing exported.' }
    }
    $directory = Split-Path $OutputPath -Parent
    $null = New-Item -ItemType Directory -Path $directory -Force
    # Reject junctions/symlinks in the destination ancestry before writing secrets.
    for ($current=$directory; $current -and $current -ne $root; $current=Split-Path $current -Parent) {
        if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Destination contains a link; nothing exported.' }
    }
    $result = Invoke-AssistantCrypto @{Operation='export';Pem=$value;ExpectedFingerprint=$selected[0].PublicSpkiSha256;OutputPath=$OutputPath}
    Write-Host 'Existing signing key preserved in the requested .cache file. No .env or source values changed.'
    Write-Host ('Public SPKI SHA256: ' + $result.Fingerprint)
}

function Publish-AssistantKnowledge {
    foreach ($entry in @(@('ApprovedBy', $ApprovedBy, 128), @('ApprovalReference', $ApprovalReference, 1024))) {
        $value = $entry[1].Trim()
        if (!$value -or $value.Length -gt $entry[2] -or $value -match '[\x00-\x1f<>]' -or
            $value -match '(?i)^(admin|test|tester|example|placeholder|null|none|todo|tbd)$|replace[_ -]?with|your[_ -]|actual[_ -]|real[_ -](admin|review)|approved_admin_record_reference') {
            throw "$($entry[0]) must identify the actual recorded Admin approval, not a placeholder."
        }
    }
    $ApprovedBy = $ApprovedBy.Trim()
    $ApprovalReference = $ApprovalReference.Trim()
    function RootPath([string]$value) {
        if (![IO.Path]::IsPathRooted($value)) { $value = Join-Path $root $value }
        return [IO.Path]::GetFullPath($value)
    }
    $SourcePath = RootPath $SourcePath
    $OutputDirectory = RootPath $OutputDirectory
    $markdownPath = Join-Path $OutputDirectory 'smartpark-knowledge.md'
    $manifestPath = Join-Path $OutputDirectory 'publication.json'
    if ((Test-Path -LiteralPath $markdownPath) -or (Test-Path -LiteralPath $manifestPath)) {
        throw 'Publication target already contains a corpus or manifest. Preserve it; use a new deployment directory for a reviewed new version.'
    }
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $sourceBytes = [IO.File]::ReadAllBytes($SourcePath)
    if ($sourceBytes.Length -gt 262144) { throw 'Knowledge source exceeds the runtime 256 KiB limit.' }
    $source = $utf8.GetString($sourceBytes).TrimStart([char]0xfeff).Replace("`r`n", "`n")
    $parts = $source -split "`n---`n", 2
    if (!$source.StartsWith("---`n") -or $parts.Count -ne 2) { throw 'Missing Markdown frontmatter.' }
    $metadata = $parts[0] -split "`n"
    function Field([string]$name) {
        $values = @($metadata | Where-Object { $_.StartsWith($name + ':', [StringComparison]::Ordinal) })
        if ($values.Count -ne 1) { throw "Expected exactly one frontmatter field: $name." }
        return $values[0].Substring($name.Length + 1).Trim()
    }
    $version = Field 'version'
    if ($version -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$' -or (Field 'locale') -cne 'vi') { throw 'Invalid version or locale.' }
    $audience = Field 'audience'
    if ($audience -notmatch '^\[.*\]$' -or 'public' -cnotin @($audience.Trim('[', ']') -split ',' | ForEach-Object { $_.Trim() })) {
        throw 'The approved source must explicitly include public audience.'
    }
    # Keep the reviewed validity window; never extend expired or future content.
    $now = [DateTimeOffset]::UtcNow
    $times = @{}
    foreach ($fieldName in @('effective_from', 'effective_to')) {
        $value = Field $fieldName
        $instant = [DateTimeOffset]::MinValue
        if ($value -ceq 'null') { $times[$fieldName] = $null; continue }
        if ($value.Length -gt 64 -or $value -cnotmatch 'T.*(?:Z|[+-]\d{2}:\d{2})$' -or
            ![DateTimeOffset]::TryParse($value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$instant)) {
            throw "Invalid $fieldName timestamp."
        }
        $times[$fieldName] = $instant
    }
    if (($null -ne $times.effective_from -and $times.effective_from -gt $now) -or
        ($null -ne $times.effective_to -and $times.effective_to -le $now) -or
        ($null -ne $times.effective_from -and $null -ne $times.effective_to -and $times.effective_from -ge $times.effective_to)) {
        throw 'The reviewed corpus is not currently effective.'
    }
    $publishedAt = $now.ToString('O', [Globalization.CultureInfo]::InvariantCulture)
    $updates = @{ publication_state = 'published'; review_status = 'approved'; published_at = $publishedAt; published_by = ($ApprovedBy | ConvertTo-Json -Compress) }
    foreach ($key in $updates.Keys) {
        $null = Field $key
        $metadata = @($metadata | ForEach-Object { if ($_.StartsWith($key + ':', [StringComparison]::Ordinal)) { "${key}: $($updates[$key])" } else { $_ } })
    }
    $published = ($metadata -join "`n") + "`n---`n" + $parts[1]
    # Match the bounded Markdown contract before creating any deployment files.
    $body = $parts[1]
    $headings = [regex]::Matches($body, '(?m)^## \[([a-zA-Z0-9._-]+)\] ([^\n]+)$')
    if ($headings.Count -lt 1 -or $headings.Count -gt 30 -or
        [regex]::Matches($body, '(?m)^## ').Count -ne $headings.Count) { throw 'Expected 1–30 valid knowledge sections.' }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($i = 0; $i -lt $headings.Count; $i++) {
        $heading = $headings[$i]
        $id = $heading.Groups[1].Value
        $title = $heading.Groups[2].Value.Trim()
        $start = $heading.Index + $heading.Length
        $end = if ($i + 1 -lt $headings.Count) { $headings[$i + 1].Index } else { $body.Length }
        $section = $body.Substring($start, $end - $start)
        $contentMarker = "`n### Nội dung`n"
        $contentStart = $section.IndexOf($contentMarker, [StringComparison]::Ordinal)
        if ($id -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$' -or !$ids.Add($id) -or
            !$title -or $title.Length -gt 200 -or $title -match '[\x00-\x1f\x7f-\x9f]' -or
            $contentStart -lt 0 -or $section.LastIndexOf($contentMarker, [StringComparison]::Ordinal) -ne $contentStart) {
            throw 'Invalid section identifier, title, or content heading.'
        }
        $aliases = @($section.Substring(0, $contentStart) -split "`n" | Where-Object { $_.StartsWith('Từ khóa:', [StringComparison]::Ordinal) })
        if ($aliases.Count -ne 1 -or $aliases[0].Length -gt 2000) { throw 'Invalid section search aliases.' }
        $terms = @($aliases[0].Substring('Từ khóa:'.Length) -split ';' | Where-Object { $_.Trim() })
        if ($terms.Count -lt 1 -or $terms.Count -gt 100) { throw 'Invalid section search alias count.' }
        $content = $section.Substring($contentStart + $contentMarker.Length - 1)
        $englishMarker = "`n### English: "
        $englishStart = $content.IndexOf($englishMarker, [StringComparison]::Ordinal)
        if ($englishStart -ge 0) {
            if ($content.IndexOf("`n### ", [StringComparison]::Ordinal) -ne $englishStart -or
                $content.LastIndexOf($englishMarker, [StringComparison]::Ordinal) -ne $englishStart) { throw 'Invalid English block position or duplicate.' }
            $titleEnd = $content.IndexOf("`n", $englishStart + $englishMarker.Length)
            if ($titleEnd -lt 0) { throw 'Missing English content.' }
            $englishTitle = $content.Substring($englishStart + $englishMarker.Length, $titleEnd - $englishStart - $englishMarker.Length).Trim()
            $english = $content.Substring($titleEnd)
            $englishNotes = $english.IndexOf("`n### ", [StringComparison]::Ordinal)
            if ($englishNotes -ge 0) {
                if (!$english.Substring($englishNotes).StartsWith("`n### Đối chiếu — không gửi đến AI`n", [StringComparison]::Ordinal)) { throw 'Unknown English note heading.' }
                $english = $english.Substring(0, $englishNotes)
            }
            $english = $english.Trim()
            if (!$englishTitle -or $englishTitle.Length -gt 200 -or $englishTitle -match '[\x00-\x1f\x7f-\x9f]' -or
                !$english -or $english.Length -gt 6000 -or $english -match '[\x00-\x09\x0b-\x1f\x7f-\x9f]') { throw 'Invalid English title or content.' }
            $content = $content.Substring(0, $englishStart)
        }
        $notes = $content.IndexOf("`n### ", [StringComparison]::Ordinal)
        if ($notes -ge 0) {
            if (!$content.Substring($notes).StartsWith("`n### Đối chiếu — không gửi đến AI`n", [StringComparison]::Ordinal)) { throw 'Unknown section note heading.' }
            $content = $content.Substring(0, $notes)
        }
        $content = $content.Trim()
        if (!$content -or $content.Length -gt 6000 -or $content -match '[\x00-\x09\x0b-\x1f\x7f-\x9f]') { throw 'Invalid or oversized section content.' }
    }
    $bytes = $utf8.GetBytes($published)
    if ($bytes.Length -gt 262144) { throw 'Published corpus exceeds the runtime 256 KiB limit.' }
    # SHA256.Create/ComputeHash and BitConverter also work on .NET Framework (Windows PowerShell 5.1).
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $digest = [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace('-', '') } finally { $sha256.Dispose() }
    $manifest = [ordered]@{
        sha256 = $digest
        version = $version
        approvedBy = $ApprovedBy
        approvalReference = $ApprovalReference
        role = 'admin'
        publishedAt = $publishedAt
        effectiveTo = if ($null -eq $times.effective_to) { $null } else { $times.effective_to.ToString('O') }
    } | ConvertTo-Json
    # CreateNew refuses races/overwrites. Manifest is written last: partial publication
    # cannot pass the runtime digest/metadata gate. A failed copy must be reviewed.
    $null = [IO.Directory]::CreateDirectory($OutputDirectory)
    foreach ($file in @(@($markdownPath, $bytes), @($manifestPath, $utf8.GetBytes($manifest + "`n")))) {
        $stream = [IO.File]::Open($file[0], [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $stream.Write($file[1], 0, $file[1].Length); $stream.Flush($true) } finally { $stream.Dispose() }
    }
    Write-Host "Prepared deployment copy version $version in $OutputDirectory."
    Write-Host 'Approval metadata records your supplied review; it does not verify Admin identity. Run setup.ps1 -AssistantCheck before starting the Assistant.'
}

if ($PSCmdlet.ParameterSetName -eq 'Initialize') {
    if (!$Initialize) { throw '-Initialize must be enabled to select this operation.' }
    Initialize-AssistantDocker
    return
}
if ($PSCmdlet.ParameterSetName -eq 'ExportSigning') {
    if (!$ExportSigning) { throw '-ExportSigning must be enabled to select this operation.' }
    Export-AssistantSigning
    return
}
if ($PSCmdlet.ParameterSetName -eq 'PublishKnowledge') {
    if (!$PublishKnowledge) { throw '-PublishKnowledge must be enabled to select this operation.' }
    Publish-AssistantKnowledge
    return
}

# Default Docker setup and preflight.
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
$markdown = Join-Path $knowledge 'smartpark-knowledge.md'
$manifest = Join-Path $knowledge 'publication.json'
if ($guestRelease -ceq 'true' -or (Test-Path -LiteralPath $markdown) -or (Test-Path -LiteralPath $manifest)) {
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
. (Join-Path $PSScriptRoot 'discovery-docker.ps1')
$discoveryEnvironment = Get-DiscoveryDockerEnvironment $settings $envDirectory
if ($discoveryEnvironment.Count -gt 0) {
    # Public CA only. Do not export the Payment service key or owner PFX password to Assistant.
    $composeEnv['SMARTPARK_DISCOVERY_DOCKER_DIRECTORY'] = $discoveryEnvironment['SMARTPARK_DISCOVERY_DOCKER_DIRECTORY']
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
if ($discoveryEnvironment.Count -gt 0) { $assistantCompose += @('-f', (Join-Path $root 'compose.assistant-discovery-trust.yml')) }

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
