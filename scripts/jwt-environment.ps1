# Shared signing-secret boundary for setup and host runners (PowerShell 5.1).
# Match raw .env names and ASP.NET configuration aliases, including host prefixes.
function Test-JwtSigningSecretName([string]$Name) {
    $normalized = $Name.Replace('_', '').Replace(':', '')
    return $normalized -match '^(?:(?:ASPNETCORE|DOTNET))?(?:SMARTPARK)?JWT.*(?:PRIVATE|SIGNING|SECRET)'
}

function Test-JwtConfiguredValue([string]$Value) {
    return (![string]::IsNullOrWhiteSpace($Value) -and $Value.Trim() -notlike 'replace-with-*')
}

function Read-JwtPrivateKeyFile([string]$Path, [string]$EnvDirectory) {
    if (![IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $EnvDirectory $Path }
    if ([IO.Path]::DirectorySeparatorChar -eq '\' -and
        $Path -notmatch '^[A-Za-z]:[\\/]' -and $Path -notmatch '^\\\\[^\\/]+[\\/][^\\/]+') {
        throw 'Use an absolute User JWT signing-key file path or one relative to .env.'
    }
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'User JWT signing-key file does not exist.' }
    $pem = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path)
    if ([string]::IsNullOrWhiteSpace($pem)) { throw 'User JWT signing-key file is empty.' }
    return $pem
}

function Get-JwtSigningEnvironment {
    $values = @{}
    foreach ($entry in [Environment]::GetEnvironmentVariables('Process').GetEnumerator()) {
        if (Test-JwtSigningSecretName ([string]$entry.Key)) { $values[[string]$entry.Key] = [string]$entry.Value }
    }
    return $values
}

function Clear-JwtSigningEnvironment {
    foreach ($name in (Get-JwtSigningEnvironment).Keys) {
        Remove-Item -LiteralPath "Env:$name" -ErrorAction Stop
    }
}

function Restore-JwtSigningEnvironment([hashtable]$Values) {
    Clear-JwtSigningEnvironment
    foreach ($name in $Values.Keys) {
        [Environment]::SetEnvironmentVariable($name, $Values[$name], 'Process')
    }
}
