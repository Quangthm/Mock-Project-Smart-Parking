[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][uri]$ManagementUri,
    [Parameter(Mandatory=$true)][pscredential]$Credential,
    [string]$VirtualHost = '/',
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
$vhost = [uri]::EscapeDataString($VirtualHost)
$exchange = 'smartpark.user-access.user-profile-name-changed.v1'
$queue = 'smartpark.notification-audit.user-profile-name-changed.v1'
$baseUri = $ManagementUri.AbsoluteUri.TrimEnd('/')
$pair = '{0}:{1}' -f $Credential.UserName, $Credential.GetNetworkCredential().Password
$headers = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pair)) }
function Invoke-Broker([string]$Method, [string]$Path, $Body) {
    $args = @{ Method=$Method; Uri="$baseUri/api/$Path"; Headers=$headers; ErrorAction='Stop' }
    if ($null -ne $Body) { $args.Body = ConvertTo-Json $Body -Depth 5 -Compress; $args.ContentType='application/json' }
    Invoke-RestMethod @args
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
