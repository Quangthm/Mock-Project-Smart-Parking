[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][uri]$ManagementUri,
    [Parameter(Mandatory=$true)][pscredential]$Credential,
    [string]$VirtualHost = 'smartpark',
    [switch]$VerifyOnly,
    [ValidateRange(1, 600)][int]$ReadinessTimeoutSec = 60
)
$ErrorActionPreference = 'Stop'
$vhost = [uri]::EscapeDataString($VirtualHost)
$exchange = 'smartpark.user-access.user-profile-name-changed.v1'
$queue = 'smartpark.notification-audit.user-profile-name-changed.v1'
$baseUri = $ManagementUri.AbsoluteUri.TrimEnd('/')
$pair = '{0}:{1}' -f $Credential.UserName, $Credential.GetNetworkCredential().Password
$headers = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pair)) }
function Invoke-Broker([string]$Method, [string]$Path, $Body) {
    $request = @{ Method=$Method; Uri="$baseUri/api/$Path"; Headers=$headers; ErrorAction='Stop'; TimeoutSec=5 }
    if ($null -ne $Body) { $request.Body = ConvertTo-Json $Body -Depth 5 -Compress; $request.ContentType='application/json' }
    Invoke-RestMethod @request
}
# Compose's rabbitmq-diagnostics ping checks the Erlang node, not HTTP readiness.
# Probe before any topology writes; never retry a write with an unknown outcome.
$readinessClock = [Diagnostics.Stopwatch]::StartNew()
while ($true) {
    try {
        Invoke-Broker 'GET' "vhosts/$vhost" $null | Out-Null
        break
    } catch {
        $status = if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        if ($status -eq 401 -or $status -eq 403) {
            throw 'RabbitMQ management authentication/authorization failed. Check SMARTPARK_RABBITMQ_USER and SMARTPARK_RABBITMQ_PASSWORD against the existing broker volume; changing .env does not update stored broker credentials.'
        }
        if ($status -eq 404) {
            throw 'RabbitMQ management API or configured virtual host was not found. Check ManagementUri and SMARTPARK_RABBITMQ_VHOST against the existing broker.'
        }
        $transportFailure = $_.Exception -is [System.Net.WebException] -or $_.Exception -is [System.Net.Http.HttpRequestException] -or $_.Exception -is [System.Threading.Tasks.TaskCanceledException]
        if (!(($status -eq 0 -and $transportFailure) -or $status -in @(502, 503, 504))) { throw }
        if ($readinessClock.Elapsed.TotalSeconds -ge $ReadinessTimeoutSec) {
            throw "RabbitMQ management API did not become ready within the ${ReadinessTimeoutSec}s readiness window. Check smartpark-rabbitmq logs and SMARTPARK_RABBITMQ_MGMT_PORT (HTTP management, not the AMQP port). No topology writes were attempted."
        }
        Write-Verbose 'Waiting for RabbitMQ management HTTP API...'
        Start-Sleep -Seconds 1
    }
}
if (!$VerifyOnly) {
    # No consumers, purge, delete, queue expiry, message TTL or overflow-drop policies.
    Invoke-Broker 'PUT' "exchanges/$vhost/$exchange" @{ type='fanout'; durable=$true; auto_delete=$false; internal=$false; arguments=@{} } | Out-Null
    Invoke-Broker 'PUT' "queues/$vhost/$queue" @{ durable=$true; auto_delete=$false; arguments=@{} } | Out-Null
    Invoke-Broker 'POST' "bindings/$vhost/e/$exchange/q/$queue" @{ routing_key=''; arguments=@{} } | Out-Null
}
$actualExchange = Invoke-Broker 'GET' "exchanges/$vhost/$exchange" $null
$actualQueue = Invoke-Broker 'GET' "queues/$vhost/$queue" $null
$bindings = @(Invoke-Broker 'GET' "bindings/$vhost/e/$exchange/q/$queue" $null)
if (!$actualExchange.durable -or $actualExchange.auto_delete -or $actualExchange.type -ne 'fanout' -or
    !$actualQueue.durable -or $actualQueue.auto_delete -or $actualQueue.exclusive -or $bindings.Count -eq 0) {
    throw 'Durable profile audit topology verification failed. Keep outbox delivery disabled.'
}
Write-Output 'Durable profile audit exchange, queue and binding verified. Review broker policies before enabling outbox delivery.'
