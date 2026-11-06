# Shared signing-secret boundary for setup and host runners (PowerShell 5.1).
# Match raw .env names and ASP.NET configuration aliases, including host prefixes.
function Test-JwtSigningSecretName([string]$Name) {
    $normalized = $Name.Replace('_', '').Replace(':', '')
    return $normalized -match '^(?:(?:ASPNETCORE|DOTNET))?(?:SMARTPARK)?JWT.*(?:PRIVATE|SIGNING|SECRET)'
}

function Test-JwtConfiguredValue([string]$Value) {
    return (![string]::IsNullOrWhiteSpace($Value) -and $Value.Trim() -notlike 'replace-with-*')
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
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}

function Restore-JwtSigningEnvironment([hashtable]$Values) {
    Clear-JwtSigningEnvironment
    foreach ($name in $Values.Keys) {
        [Environment]::SetEnvironmentVariable($name, $Values[$name], 'Process')
    }
}
