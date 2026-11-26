#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('.cache/assistant-init-fixture-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixture
$envFile = Join-Path $fixture '.env'
$initializer = Join-Path $PSScriptRoot 'initialize-assistant-docker.ps1'
$loader = Join-Path $PSScriptRoot 'load-env.ps1'
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')

[IO.File]::WriteAllText($envFile, "SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=replace-with-fixture`nKEEP_ME=fixture`n")
$oldAppData = $env:APPDATA
try {
    $env:APPDATA = $fixture
    $output = (& $initializer -EnvPath $envFile 6>&1 | Out-String)
} finally { $env:APPDATA = $oldAppData }
$settings = & $loader -EnvPath $envFile -NoExport
$snapshot = (Get-FileHash -LiteralPath $envFile).Hash
$jwtFile = $settings['SMARTPARK_JWT_PRIVATE_KEY_FILE']
$privateKeyHash = (Get-FileHash -LiteralPath (Join-Path $fixture $jwtFile)).Hash
$null = & $initializer -EnvPath $envFile
if ((Get-FileHash -LiteralPath $envFile).Hash -ne $snapshot -or
    (Get-FileHash -LiteralPath (Join-Path $fixture $jwtFile)).Hash -ne $privateKeyHash -or
    $settings['KEEP_ME'] -cne 'fixture') { throw 'Rerun replaced existing data.' }
if ([IO.Path]::IsPathRooted($jwtFile) -or
    (Read-JwtPrivateKeyFile $jwtFile $fixture) -notmatch '-----BEGIN PRIVATE KEY-----') { throw 'Root-relative signing file did not resolve.' }
if ($output -match 'PRIVATE KEY' -or $output.Contains($settings['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD']) -or
    $output.Contains($settings['Assistant__Management__KeyEncryptionCertificatePassword'])) { throw 'Initializer disclosed signing or certificate credentials.' }

$existingEnv = Join-Path $fixture 'existing.env'
[IO.File]::WriteAllText($existingEnv, "SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=existing-fixture-source`n")
$existingHash = (Get-FileHash -LiteralPath $existingEnv).Hash
$rejected = $false
try { & $initializer -EnvPath $existingEnv }
catch { $rejected = $_.Exception.Message -match 'Existing local configuration' }
if (!$rejected -or (Get-FileHash -LiteralPath $existingEnv).Hash -ne $existingHash) { throw 'Existing signing configuration was accepted or changed.' }
Write-Host 'PASS: local initializer is repeatable, keeps existing signing configuration, and resolves JWT paths from the environment directory.'


# Existing User signing sources must remain unchanged and produce exactly the same public trust.
$originalSigner = [Security.Cryptography.RSA]::Create(3072)
$otherSigner = [Security.Cryptography.RSA]::Create(3072)
try {
    $expectedPublic = $originalSigner.ExportSubjectPublicKeyInfoPem()
    $originalPem = $originalSigner.ExportPkcs8PrivateKeyPem()
    foreach ($mode in @('file', 'inline', 'base64', 'conflict', 'missing', 'placeholder')) {
        $caseDirectory = Join-Path $fixture $mode
        $null = New-Item -ItemType Directory -Path $caseDirectory
        $caseEnv = Join-Path $caseDirectory '.env'
        $source = switch ($mode) {
            'file' {
                [IO.File]::WriteAllText((Join-Path $caseDirectory 'existing.pem'), $originalPem)
                'SMARTPARK_JWT_PRIVATE_KEY_FILE=existing.pem'
            }
            'inline' { 'SMARTPARK_JWT_PRIVATE_KEY_PEM=' + $originalPem.Replace("`n", '\n') }
            'base64' { 'SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($originalPem)) }
            'conflict' {
                [IO.File]::WriteAllText((Join-Path $caseDirectory 'existing.pem'), $originalPem)
                'SMARTPARK_JWT_PRIVATE_KEY_FILE=existing.pem' + "`nSMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=" +
                    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($otherSigner.ExportPkcs8PrivateKeyPem()))
            }
            'placeholder' {
                'SMARTPARK_JWT_PRIVATE_KEY_PEM=' + $originalPem.Replace("`n", '\n') +
                    "`nSMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64=replace-with-base64-rsa-private-key"
            }
            default { 'KEEP_ME=fixture' }
        }
        [IO.File]::WriteAllText($caseEnv, $source + "`n")
        $before = (Get-FileHash -LiteralPath $caseEnv).Hash
        if ($mode -in @('conflict', 'missing', 'placeholder')) {
            $didReject = $false
            try { & $initializer -EnvPath $caseEnv -UseExistingSigningKey }
            catch { $didReject = $true }
            if (!$didReject -or (Get-FileHash -LiteralPath $caseEnv).Hash -ne $before -or
                (Test-Path (Join-Path $caseDirectory '.cache/assistant-docker'))) {
                throw "Unsafe signing input mutated the fixture: $mode."
            }
            continue
        }
        Push-Location $root
        try { $caseOutput = (& $initializer -EnvPath $caseEnv -UseExistingSigningKey 6>&1 | Out-String) }
        finally { Pop-Location }
        $after = & $loader -EnvPath $caseEnv -NoExport
        $publicPath = Join-Path $caseDirectory $after['Assistant__Jwt__PublicKeyFile']
        if ([IO.File]::ReadAllText($publicPath) -cne $expectedPublic) { throw "Existing signer changed: $mode." }
        foreach ($line in ($source -split "`n")) {
            if (![IO.File]::ReadAllLines($caseEnv).Contains($line)) { throw "Signing configuration rewritten: $mode." }
        }
        if (Test-Path (Join-Path $caseDirectory '.cache/assistant-docker/user-private.pem')) { throw 'Existing private key was copied.' }
        if ($mode -eq 'file' -and [IO.File]::ReadAllText((Join-Path $caseDirectory 'existing.pem')) -cne $originalPem) { throw 'Original key file changed.' }
        if ($caseOutput -match 'PRIVATE KEY' -or $caseOutput.Contains($after['Assistant__Management__KeyEncryptionCertificatePassword'])) { throw 'Existing-signing initializer leaked secret.' }
    }
} finally { $originalSigner.Dispose(); $otherSigner.Dispose() }
Write-Host 'PASS: existing FILE, PEM and Base64 signing sources preserved; invalid, mismatched and placeholder sources rejected before writes.'
