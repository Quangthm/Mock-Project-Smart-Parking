# =============================================================================
# DEPRECATED: setup-local.ps1 has been replaced by setup.ps1 at the repo root.
# This shim is kept so existing documentation keeps working; it forwards every
# argument to the new script.
# =============================================================================
$ErrorActionPreference = 'Stop'

Write-Warning 'setup-local.ps1 has been replaced by setup.ps1 at the repository root.'

$rootSetup = Join-Path (Split-Path $PSScriptRoot -Parent) 'setup.ps1'
if (!(Test-Path -LiteralPath $rootSetup -PathType Leaf)) {
    throw "Missing $rootSetup. Restore setup.ps1 at the repository root."
}

& $rootSetup @args
if ($LASTEXITCODE) { exit $LASTEXITCODE }
