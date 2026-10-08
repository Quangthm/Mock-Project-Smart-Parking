param([string]$ConfigPath)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (!$ConfigPath) { $ConfigPath = Join-Path $taskRoot '.cache/qa-local.json' }
if (Test-Path -LiteralPath $ConfigPath -PathType Leaf) {
    $settings = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    foreach ($property in $settings.PSObject.Properties) {
        if ($property.Name -notmatch '^[A-Za-z][A-Za-z0-9_:]*$' -or $property.Value -isnot [string]) {
            throw 'Local config must contain flat string settings (configuration keys use colons).'
        }
        $name = $property.Name.Replace(':', '__')
        # Explicit terminal settings take precedence over the local defaults.
        if (![Environment]::GetEnvironmentVariable($name, 'Process')) {
            [Environment]::SetEnvironmentVariable($name, $property.Value, 'Process')
        }
    }
} elseif ($PSBoundParameters.ContainsKey('ConfigPath')) {
    throw 'The specified local config file does not exist.'
}
