#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('.cache/publication-test-' + [guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($fixture)
$sourcePath = Join-Path $fixture 'source.md'
$output = Join-Path $fixture 'published'
$helper = Join-Path $PSScriptRoot 'publish-assistant-knowledge.ps1'
$source = @'
---
version: fixture-1
locale: vi
audience: [public, driver]
publication_state: draft
review_status: pending
effective_from: null
effective_to: null
published_at: null
published_by: null
---
## [fixture] Dữ liệu kiểm thử
Từ khóa: kiểm thử; fixture
### Nội dung
Đây là dữ liệu tổng hợp dùng kiểm tra script, không phải tài liệu nghiệp vụ.
'@
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($sourcePath, $source, $utf8)
$params = @{ SourcePath = $sourcePath; OutputDirectory = $output; ApprovedBy = 'Fixture Admin (synthetic test only)'; ApprovalReference = 'fixture://synthetic-review/1' }
function Assert($condition, [string]$message) { if (!$condition) { throw $message } }
function Reject([scriptblock]$action, [string]$message) {
    $failed = $false
    try { & $action } catch { $failed = $true }
    Assert $failed $message
}
& $helper @params
$bytes = [IO.File]::ReadAllBytes((Join-Path $output 'smartpark-knowledge.md'))
$manifestText = [IO.File]::ReadAllText((Join-Path $output 'publication.json'))
$manifest = $manifestText | ConvertFrom-Json
# Read the JSON string without PowerShell 7.5 automatic date conversion or System.Text.Json.
$publishedAt = ([regex]::Match($manifestText, '"publishedAt"\s*:\s*("[^"\\]*")').Groups[1].Value.Trim('"'))
$text = $utf8.GetString($bytes)
$sha256 = [Security.Cryptography.SHA256]::Create()
try { $digest = [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace('-', '') } finally { $sha256.Dispose() }
Assert ($manifest.sha256 -ceq $digest) 'SHA does not match exact deployed bytes.'
Assert ($bytes[0] -eq 45) 'Published Markdown must not have a UTF-8 BOM.'
Assert ($manifest.version -ceq 'fixture-1' -and $manifest.role -ceq 'admin' -and $manifest.approvalReference -ceq $params.ApprovalReference) 'Manifest metadata mismatch.'
Assert ($text.Contains('publication_state: published') -and $text.Contains('review_status: approved') -and $text.Contains('published_at: ' + $publishedAt)) 'Published frontmatter mismatch.'
Assert ([DateTimeOffset]::Parse($publishedAt) -le [DateTimeOffset]::UtcNow) 'Publication timestamp is future.'
Assert ([IO.File]::ReadAllText($sourcePath) -ceq $source) 'Editing source was changed.'
# Verify the entire UTF-8 publication, including Vietnamese text and LF normalization.
$expected = $source.Replace("`r`n", "`n").Replace('publication_state: draft', 'publication_state: published').Replace('review_status: pending', 'review_status: approved').Replace('published_at: null', 'published_at: ' + $publishedAt).Replace('published_by: null', 'published_by: ' + ($params.ApprovedBy | ConvertTo-Json -Compress))
Assert ([Convert]::ToBase64String($bytes) -ceq [Convert]::ToBase64String($utf8.GetBytes($expected))) 'Published UTF-8 bytes differ from the exact expected corpus.'
Assert ($text.Contains('Dữ liệu kiểm thử') -and $text.Contains('Đây là dữ liệu tổng hợp')) 'Vietnamese text was corrupted.'
Reject { & $helper @params } 'Existing publication must be preserved.'
Assert ([IO.File]::ReadAllText((Join-Path $output 'publication.json')) -ceq $manifestText) 'Existing manifest was overwritten.'
# A BOM and CRLF in the editing source must not leak into deployment bytes.
$bomSource = [Text.UTF8Encoding]::new($true).GetPreamble() + $utf8.GetBytes($source.Replace("`r`n", "`n").Replace("`n", "`r`n"))
[IO.File]::WriteAllBytes($sourcePath, $bomSource)
$params.OutputDirectory = Join-Path $fixture 'published-bom-crlf'
& $helper @params
$normalizedBytes = [IO.File]::ReadAllBytes((Join-Path $params.OutputDirectory 'smartpark-knowledge.md'))
$normalizedManifestText = [IO.File]::ReadAllText((Join-Path $params.OutputDirectory 'publication.json'))
$normalizedAt = ([regex]::Match($normalizedManifestText, '"publishedAt"\s*:\s*("[^"\\]*")').Groups[1].Value.Trim('"'))
Assert ([Convert]::ToBase64String($normalizedBytes) -ceq [Convert]::ToBase64String($utf8.GetBytes($expected.Replace($publishedAt, $normalizedAt)))) 'BOM/CRLF source did not produce exact normalized UTF-8 bytes.'
Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($sourcePath)) -ceq [Convert]::ToBase64String($bomSource)) 'BOM/CRLF editing source bytes were changed.'
$params.OutputDirectory = Join-Path $fixture 'rejected'
foreach ($placeholder in @('', 'admin', '<actual-admin>', 'REPLACE_WITH_ADMIN', 'your-admin')) {
    $params.ApprovedBy = $placeholder
    Reject { & $helper @params } 'Placeholder approver must be rejected.'
}
$params.ApprovedBy = 'Fixture Admin (synthetic test only)'
$params.ApprovalReference = 'TODO'
Reject { & $helper @params } 'Placeholder approval reference must be rejected.'
$params.ApprovalReference = 'fixture://synthetic-review/1'
[IO.File]::WriteAllText($sourcePath, $source.Replace('effective_to: null', 'effective_to: 2000-01-01T00:00:00Z'), $utf8)
Reject { & $helper @params } 'Expired corpus must be rejected.'
[IO.File]::WriteAllText($sourcePath, $source.Replace('### Nội dung', '### Broken'), $utf8)
Reject { & $helper @params } 'Malformed corpus must be rejected.'
Assert (!(Test-Path -LiteralPath $params.OutputDirectory)) 'Rejected publication must not create output.'
Write-Host "PASS: publication integrity, source preservation, no overwrite, approval input and invalid-corpus checks. Synthetic fixtures retained at $fixture"
