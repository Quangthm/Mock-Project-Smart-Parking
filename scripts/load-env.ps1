# =============================================================================
# load-env.ps1 - parse a .env file into the current process environment.
# Shared by setup.ps1 and scripts/run-schema-service.ps1 (Windows PowerShell 5.1).
#
# Usage (dot-source to reuse the returned map in the caller's scope):
#   $map = . .\scripts\load-env.ps1                  # reads <repo>\.env
#   $map = . .\scripts\load-env.ps1 -EnvPath <path>  # explicit file
#
# Every KEY=VALUE line is exported as a process environment variable under its
# original name (KEY=VALUE -> $env:KEY). Values are never written to the log.
# =============================================================================
param(
    [string]$EnvPath
)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($EnvPath)) {
    $EnvPath = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
}
if (!(Test-Path -LiteralPath $EnvPath -PathType Leaf)) {
    throw "Environment file not found: $EnvPath. Run setup.ps1 at the repository root first."
}

$envPairs = @{}
foreach ($envRawLine in Get-Content -LiteralPath $EnvPath -Encoding UTF8) {
    $envLine = $envRawLine.Trim()
    if (!$envLine) { continue }
    if ($envLine.StartsWith('#')) { continue }

    $envSeparator = $envLine.IndexOf('=')
    if ($envSeparator -lt 1) { continue }

    $envKey = $envLine.Substring(0, $envSeparator).Trim()
    if (!$envKey -or $envKey -match '\s') { continue }

    $envValue = $envLine.Substring($envSeparator + 1).Trim()
    # Strip a matching pair of outer quotes ("value" or 'value').
    if ($envValue.Length -ge 2) {
        $envFirst = $envValue[0]
        $envLast = $envValue[$envValue.Length - 1]
        $envDoubleQuoted = ($envFirst -eq [char]34) -and ($envLast -eq [char]34)
        $envSingleQuoted = ($envFirst -eq [char]39) -and ($envLast -eq [char]39)
        if ($envDoubleQuoted -or $envSingleQuoted) {
            $envValue = $envValue.Substring(1, $envValue.Length - 2)
        }
    }

    $envPairs[$envKey] = $envValue
    [Environment]::SetEnvironmentVariable($envKey, $envValue, 'Process')
}

return $envPairs
