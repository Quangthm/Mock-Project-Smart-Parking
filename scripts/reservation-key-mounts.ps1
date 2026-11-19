# Optional file mounts. Empty paths preserve inline PEM configuration without mounting the repo.
function Get-ReservationKeyMounts([hashtable]$Settings, [string]$Root) {
    $files = @()
    $environment = @{}
    foreach ($entry in @(
        @{ Name = 'SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PATH'; Kind = 'private' },
        @{ Name = 'SMARTPARK_FACILITY_OPERATION_PUBLIC_KEY_PATH'; Kind = 'public' }
    )) {
        $path = [string]$Settings[$entry.Name]
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        if (![IO.Path]::IsPathRooted($path)) { $path = Join-Path $Root $path }
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "$($entry.Name) must name an existing key file."
        }
        $environment[$entry.Name] = (Resolve-Path -LiteralPath $path).Path
        $files += Join-Path $Root "compose.reservation-$($entry.Kind)-key.yml"
    }
    return @{ Files = $files; Environment = $environment }
}
