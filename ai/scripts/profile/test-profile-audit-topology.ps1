# PowerShell 5.1 regression checks. HTTP and sleep doubles; no broker writes.
$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot 'messaging/provision-profile-audit-topology.ps1'
$credential = New-Object PSCredential('fixture', (ConvertTo-SecureString 'fixture' -AsPlainText -Force))
Add-Type @'
public class TopologyTestResponse : System.Net.WebResponse {
    public int StatusCode { get; set; }
}
'@
function Assert-Topology($condition, $message) { if (!$condition) { throw $message } }
function Start-Sleep { param($Seconds) }
function Invoke-RestMethod {
    param($Method, $Uri, $Headers, $ErrorAction, $TimeoutSec, $Body, $ContentType)
    Assert-Topology ($TimeoutSec -eq 5) 'Requests must have a timeout.'
    $global:topologyTestCalls += "$Method $Uri"
    if ($Uri -match '/vhosts/') {
        if ($global:topologyTestFailureCount -gt 0) {
            $global:topologyTestFailureCount--
            if ($global:topologyTestStatus -eq 0) { throw [Net.WebException]::new('Connection closed unexpectedly') }
            $response = New-Object TopologyTestResponse
            $response.StatusCode = $global:topologyTestStatus
            throw [Net.WebException]::new('Fixture HTTP error', $null, [Net.WebExceptionStatus]::ProtocolError, $response)
        }
        return @{ name='smartpark' }
    }
    if ($Method -ne 'GET') { return }
    if ($Uri -match '/exchanges/') { return @{ durable=$true; auto_delete=$false; type='fanout' } }
    if ($Uri -match '/queues/') { return @{ durable=$true; auto_delete=$false; exclusive=$false } }
    return @{ routing_key='' }
}
foreach ($code in @(0, 503)) {
    $global:topologyTestCalls = @(); $global:topologyTestStatus = $code; $global:topologyTestFailureCount = 2
    $null = & $target -ManagementUri 'http://localhost:15672' -Credential $credential
    Assert-Topology ($global:topologyTestCalls.Count -eq 9) 'Expected readiness retries followed by three writes and three verification reads.'
    Assert-Topology ($global:topologyTestCalls[2] -match '^GET .*vhosts/') 'Writes occurred before readiness.'
    Assert-Topology ($global:topologyTestCalls[3] -match '^PUT ') 'Provisioning did not follow readiness.'
}
foreach ($case in @(@{Status=401; Message='authentication/authorization'}, @{Status=403; Message='authentication/authorization'}, @{Status=404; Message='virtual host'}, @{Status=400; Message='Fixture HTTP error'})) {
    $global:topologyTestCalls = @(); $global:topologyTestStatus = $case.Status; $global:topologyTestFailureCount = 1
    $failure = $null
    try { & $target -ManagementUri 'http://localhost:15672' -Credential $credential } catch { $failure = $_ }
    Assert-Topology ($null -ne $failure -and $failure.Exception.Message -match $case.Message) 'Expected actionable permanent failure.'
    Assert-Topology ($global:topologyTestCalls.Count -eq 1) 'Permanent failure was retried or topology was written.'
}
$global:topologyTestCalls = @(); $global:topologyTestStatus = 0; $global:topologyTestFailureCount = [int]::MaxValue
$failure = $null
try { & $target -ManagementUri 'http://localhost:15672' -Credential $credential -ReadinessTimeoutSec 1 } catch { $failure = $_ }
Assert-Topology ($null -ne $failure -and $failure.Exception.Message -match 'No topology writes') 'Expected bounded readiness failure.'
Assert-Topology (@($global:topologyTestCalls | Where-Object { $_ -notmatch '^GET .*vhosts/' }).Count -eq 0) 'Unavailable broker received writes.'
$global:topologyTestCalls = @(); $global:topologyTestFailureCount = 0
$null = & $target -ManagementUri 'http://localhost:15672' -Credential $credential -VerifyOnly
Assert-Topology ($global:topologyTestCalls.Count -eq 4 -and @($global:topologyTestCalls | Where-Object { $_ -notmatch '^GET ' }).Count -eq 0) 'VerifyOnly must only read.'
Write-Output 'PASS: readiness recovery, fail-fast HTTP errors, bounded outage, write ordering and VerifyOnly.'
