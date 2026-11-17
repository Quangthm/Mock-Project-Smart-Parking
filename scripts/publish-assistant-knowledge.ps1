#requires -Version 5.1
<#
.SYNOPSIS
Prepare a deployment copy AFTER recorded Admin approval of the exact source.
.DESCRIPTION
ApprovedBy and ApprovalReference record an existing review; they do not authenticate
an Admin or grant approval. This helper never changes the editing source or .env.
Relative paths resolve from the repository root. Existing target files are refused.
#>
param(
    [Parameter(Mandatory)][string]$ApprovedBy,
    [Parameter(Mandatory)][string]$ApprovalReference,
    [string]$SourcePath = 'docs/03-design/ai/p1/knowledge/smartpark-knowledge.md',
    [string]$OutputDirectory = '.cache/assistant-docker/knowledge'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
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
    $content = $section.Substring($contentStart + $contentMarker.Length)
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
