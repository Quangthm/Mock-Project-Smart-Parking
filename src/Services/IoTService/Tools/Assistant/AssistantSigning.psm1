# Assistant signing tools for Windows PowerShell 5.1 and PowerShell 7.
. (Join-Path $PSScriptRoot '../../../../../scripts/jwt-environment.ps1')

# Windows PowerShell 5.1 bridge to the repository's .NET 10 cryptography APIs.
# Key material travels through redirected stdin only, never process arguments.
function Invoke-AssistantCrypto {
    param(
        [Parameter(Mandatory)][hashtable]$Request,
        [ValidateSet('assistant-signing-crypto.cs','assistant-docker-crypto.cs')]
        [string]$HelperName = 'assistant-signing-crypto.cs'
    )
    $start = New-Object Diagnostics.ProcessStartInfo
    $sdk = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$sdk) { throw [InvalidOperationException]::new('Install .NET SDK 10 before running the Assistant crypto tools.') }
    $start.FileName = $sdk.Source
    $helper = Join-Path $PSScriptRoot $HelperName
    $start.Arguments = 'run --file "' + $helper + '" --verbosity quiet'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.EnvironmentVariables['DOTNET_NOLOGO'] = '1'
    $start.EnvironmentVariables['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    # The crypto child receives the request explicitly; do not propagate ambient signing secrets.
    foreach ($name in @($start.EnvironmentVariables.Keys)) {
        if (Test-JwtSigningSecretName $name) { $start.EnvironmentVariables.Remove($name) }
    }
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        $null = $process.Start()
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        $inputBytes = [Text.Encoding]::UTF8.GetBytes(($Request | ConvertTo-Json -Compress -Depth 5))
        $process.StandardInput.BaseStream.Write($inputBytes, 0, $inputBytes.Length)
        [Array]::Clear($inputBytes, 0, $inputBytes.Length)
        $process.StandardInput.Close()
        $process.WaitForExit()
        $output = $outputTask.GetAwaiter().GetResult()
        $null = $errorTask.GetAwaiter().GetResult()
        if ($process.ExitCode -eq 2) { throw [ArgumentException]::new('Signing key is invalid or source changed.') }
        if ($process.ExitCode -ne 0) { throw [InvalidOperationException]::new('Assistant crypto helper failed. Verify .NET SDK 10 and its build cache. For initialization, also check signing inputs and output directory permissions.') }
        try { return ($output | ConvertFrom-Json) }
        catch { throw [InvalidOperationException]::new('Assistant crypto helper returned an invalid response.') }
    } finally { $process.Dispose() }
}

function ConvertFrom-AssistantSigningJson([string]$Json) {
    # ConvertFrom-Json -AsHashtable is unavailable in Windows PowerShell 5.1.
    function Convert-AssistantJsonValue($Value) {
        if ($Value -is [Management.Automation.PSCustomObject]) {
            $map = @{}
            foreach ($property in $Value.PSObject.Properties) { $map[$property.Name] = Convert-AssistantJsonValue $property.Value }
            return $map
        }
        return $Value
    }
    return (Convert-AssistantJsonValue ($Json | ConvertFrom-Json))
}

Export-ModuleMember -Function Invoke-AssistantCrypto, ConvertFrom-AssistantSigningJson
