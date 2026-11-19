# Offline path-selection regression; no Docker start, key loading or local .env access.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'reservation-key-mounts.ps1')
$root = Split-Path $PSScriptRoot -Parent
$empty = Get-ReservationKeyMounts @{} $root
if ($empty.Files.Count -ne 0 -or $empty.Environment.Count -ne 0) { throw 'Empty paths enabled mounts.' }
$blank = Get-ReservationKeyMounts @{ SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PATH = ' ' } $root
if ($blank.Files.Count -ne 0) { throw 'Whitespace enabled a mount.' }
foreach ($invalid in @('.', 'missing-synthetic-reservation-key.pem')) {
    $rejected = $false
    try { Get-ReservationKeyMounts @{ SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PATH = $invalid } $root | Out-Null }
    catch { $rejected = $_.Exception.Message -match 'existing key file' }
    if (!$rejected) { throw 'Directory or missing file was accepted.' }
}
# This helper validates only file paths; use this script itself as a harmless existing file.
$valid = Get-ReservationKeyMounts @{
    SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PATH = 'scripts/test-reservation-key-mounts.ps1'
    SMARTPARK_FACILITY_OPERATION_PUBLIC_KEY_PATH = $PSCommandPath
} $root
if ($valid.Files.Count -ne 2 -or $valid.Environment.Count -ne 2) { throw 'Key file selection failed.' }
foreach ($file in $valid.Files) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw 'Missing Compose override.' }
}
Write-Host 'Reservation key mounts: PASS (empty, whitespace, directory, missing, relative and absolute paths).'
