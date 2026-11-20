function Resolve-AssistantKnowledgeDirectory([string]$Value, [string]$RepositoryRoot, [string]$EnvDirectory,
    [switch]$UseBundledDefault) {
    $bundled = 'docs/03-design/ai/smartparkdocker/knowledge/0.4.1'
    if ($UseBundledDefault -and [string]::IsNullOrWhiteSpace($Value)) {
        return Join-Path $RepositoryRoot $bundled
    }
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if ([IO.Path]::IsPathRooted($Value)) { return $Value }
    $relativePath = $Value.Replace('\', '/')
    if ($relativePath -cmatch '\Adocs/03-design/ai/smartparkdocker/knowledge/[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}\z') {
        return Join-Path $RepositoryRoot $relativePath
    }
    return Join-Path $EnvDirectory $Value
}
