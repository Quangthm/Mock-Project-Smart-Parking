#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $root ('.cache/signing-export-' + [Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $fixture
$exporter=Join-Path $PSScriptRoot 'export-assistant-signing.ps1'
. (Join-Path $PSScriptRoot 'jwt-environment.ps1')
$saved=Get-JwtSigningEnvironment
$rsa=[Security.Cryptography.RSA]::Create(2048)
$envFile=Join-Path $fixture '.env'
$secretsFile=Join-Path $fixture 'secrets.json'
function Assert-Rejected([hashtable]$Arguments) {
    $rejected=$false
    try { $null=& $exporter @Arguments 6>&1 } catch { $rejected=$true }
    if (!$rejected) { throw 'Unsafe export was accepted.' }
}
try {
    Clear-JwtSigningEnvironment
    $pem=$rsa.ExportPkcs8PrivateKeyPem()
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pem))
    [IO.File]::WriteAllText($envFile, 'SMARTPARK_JWT_PRIVATE_KEY_PEM=')
    [IO.File]::WriteAllText($secretsFile, (@{'Jwt:PrivateKeyPem'=$pem; Jwt=@{PrivateKeyPemBase64=$encoded}} | ConvertTo-Json))
    $hash=(Get-FileHash $secretsFile).Hash
    $base=@{EnvPath=$envFile;UserSecretsPath=$secretsFile;OutputPath=(Join-Path $fixture 'preserved.pem'); Source='UserService user-secrets:Jwt:PrivateKeyPem'}
    Assert-Rejected $base
    $base.ConfirmExistingSource=$true
    $output=(& $exporter @base 6>&1 | Out-String)
    if ($output.Contains($pem) -or $output.Contains($encoded) -or $output -match 'BEGIN PRIVATE KEY') { throw 'Export printed secret.' }
    $copy=[Security.Cryptography.RSA]::Create()
    try {
        $copy.ImportFromPem([IO.File]::ReadAllText($base.OutputPath))
        if ([Convert]::ToBase64String($copy.ExportSubjectPublicKeyInfo()) -cne [Convert]::ToBase64String($rsa.ExportSubjectPublicKeyInfo())) { throw 'Export changed key identity.' }
    } finally { $copy.Dispose() }
    Assert-Rejected $base
    $base.OutputPath=Join-Path $fixture 'nested.pem'
    $base.Source='UserService user-secrets:nested Jwt.PrivateKeyPemBase64'
    $null=& $exporter @base 6>&1
    $env:Jwt__PrivateKeyPem=$pem
    $base.OutputPath=Join-Path $fixture 'process.pem'
    $base.Source='process:Jwt__PrivateKeyPem'
    $null=& $exporter @base 6>&1
    if ($env:Jwt__PrivateKeyPem -cne $pem -or (Get-FileHash $secretsFile).Hash -cne $hash) { throw 'Export modified source.' }
    $base.OutputPath=Join-Path $root 'must-not-exist.pem'
    Assert-Rejected $base
    $base.OutputPath=Join-Path $fixture 'public-only.pem'
    $env:Jwt__PrivateKeyPem=$rsa.ExportSubjectPublicKeyInfoPem()
    Assert-Rejected $base
    if (Test-Path $base.OutputPath) { throw 'Rejected export wrote a file.' }
    Write-Host 'PASS: explicit confirmation, flat/nested/process recovery, same key identity, no secret output, source preservation, no overwrite, public-only and outside-cache rejection.'
} finally {
    Restore-JwtSigningEnvironment $saved
    $rsa.Dispose()
    $resolved=[IO.Path]::GetFullPath($fixture)
    $allowed=[IO.Path]::GetFullPath((Join-Path $root '.cache')) + [IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture cleanup escaped .cache.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
