# Executes the real reset branch with a Docker double. No runtime resources are touched.
$ErrorActionPreference = 'Stop'
$parseErrors = $null
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$setupPath = Join-Path $repoRoot 'setup.ps1'
if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) { throw "setup.ps1 not found at $setupPath" }
$tree = [System.Management.Automation.Language.Parser]::ParseFile(
    $setupPath, [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Setup parse failed.' }
$reset = $tree.Find({ param($node)
    $node -is [System.Management.Automation.Language.IfStatementAst] -and
    $node.Clauses[0].Item1.Extent.Text -eq '$RecreateDb' -and
    $node.Clauses[0].Item2.Extent.Text.Contains('$dbServices')
}, $true)
if (!$reset) { throw 'Reset branch not found.' }
$script:calls = @()
function docker {
    $script:calls += ,@($args)
    $global:LASTEXITCODE = 0
}
$envFile = 'fixture.env'
$composeFile = 'fixture-compose.yml'
$body = $reset.Clauses[0].Item2.Extent.Text
& ([scriptblock]::Create($body.Substring(1, $body.Length - 2)))
$removed = @($script:calls | Where-Object { $_[0] -eq 'volume' -and $_[1] -eq 'rm' } | ForEach-Object { $_[2] })
$expected = @('user','facility','operations','payment','notification','iot') | ForEach-Object { "smartpark-schema_$_-schema-data" }
if (Compare-Object $expected $removed) { throw 'Reset selected unexpected volumes.' }
if (($script:calls | ForEach-Object { $_ -join ' ' }) -match 'rabbitmq|down|--volumes') { throw 'Reset touches shared broker/project.' }
$removeContainers = @($script:calls | Where-Object { $_ -contains '--stop' })
if ($removeContainers.Count -ne 1) { throw 'Expected one targeted container removal.' }
foreach ($name in @('user','facility','operations','payment','notification','iot')) {
    if ($removeContainers[0] -notcontains "$name-db") { throw 'Missing database container.' }
}
Write-Output 'PASS: actual setup reset branch selects only six database containers and volumes.'
