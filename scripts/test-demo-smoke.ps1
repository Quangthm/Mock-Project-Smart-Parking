# Local smoke checks. No registration, approval, email or payment mutations.
$ErrorActionPreference = 'Stop'
$smokeRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$smokeSettings = & (Join-Path $smokeRoot 'scripts/load-env.ps1') -EnvPath (Join-Path $smokeRoot '.env') -NoExport
$smokeRows = [Collections.Generic.List[object]]::new()
function Send-SmokeRequest([string]$Url, [string]$Method = 'GET', $Body = $null, [hashtable]$Headers = @{}) {
    $parameters = @{ Uri=$Url; Method=$Method; Headers=$Headers; UseBasicParsing=$true; TimeoutSec=12 }
    if ($null -ne $Body) { $parameters.ContentType='application/json'; $parameters.Body=($Body | ConvertTo-Json -Depth 8 -Compress) }
    try {
        $response = Invoke-WebRequest @parameters
        return @{Status=[int]$response.StatusCode; Content=[string]$response.Content}
    } catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        return @{Status=$status; Content=''}
    }
}
function Add-SmokeResult([string]$Name, $Response, [int]$Expected, [string]$Note = '') {
    $smokeRows.Add([pscustomobject]@{Check=$Name; Expected=$Expected; Actual=$Response.Status; Result=$(if ($Response.Status -eq $Expected) {'PASS'} else {'FAIL'}); Note=$Note})
}
$userUrl='http://localhost:5035'
$parkingUrl='http://localhost:5045'
$reservationUrl='http://localhost:5055'
$randomId=[guid]::NewGuid().ToString()
Add-SmokeResult 'Frontend index' (Send-SmokeRequest 'http://localhost:5173/') 200 'HTTP only; browser interactions not tested'
Add-SmokeResult 'User OpenAPI' (Send-SmokeRequest "$userUrl/openapi/v1.json") 200
Add-SmokeResult 'Anonymous /me' (Send-SmokeRequest "$userUrl/api/auth/me") 401
Add-SmokeResult 'Anonymous owner applications' (Send-SmokeRequest "$userUrl/api/owner-applications") 401
Add-SmokeResult 'Anonymous users' (Send-SmokeRequest "$userUrl/api/users") 401
Add-SmokeResult 'Anonymous operators' (Send-SmokeRequest "$userUrl/api/operators") 401
Add-SmokeResult 'Anonymous Operator assignments' (Send-SmokeRequest "$userUrl/api/operators/me") 401
Add-SmokeResult 'Anonymous Parking list' (Send-SmokeRequest "$parkingUrl/api/parking-lots") 401
Add-SmokeResult 'Parking internal rejects missing service key' (Send-SmokeRequest "$parkingUrl/internal/sites/validate" 'POST' @{siteIds=@();tenantIds=@()}) 401
Add-SmokeResult 'Reservation internal rejects missing service key' (Send-SmokeRequest "$reservationUrl/internal/structure/$randomId/impact" 'POST' @{tenantId=$randomId}) 401
Add-SmokeResult 'Empty password login rejected' (Send-SmokeRequest "$userUrl/api/auth/login" 'POST' @{}) 400
Add-SmokeResult 'Invalid Driver registration rejected' (Send-SmokeRequest "$userUrl/api/auth/register/driver" 'POST' @{fullName='';email='invalid'}) 400 'Invalid DTO; no valid registration or email requested'
Add-SmokeResult 'Invalid Owner registration rejected' (Send-SmokeRequest "$userUrl/api/auth/register/owner" 'POST' @{}) 400 'Invalid DTO; no valid registration or email requested'
Add-SmokeResult 'Empty refresh rejected' (Send-SmokeRequest "$userUrl/api/auth/refresh" 'POST' @{}) 400 'Missing token is MISSING_REFRESH_TOKEN/400; revoked token is 401'
$serviceHeaders=@{'X-Service-Key'=$smokeSettings['SMARTPARK_SERVICE_KEY']}
Add-SmokeResult 'Parking internal authenticated empty validation' (Send-SmokeRequest "$parkingUrl/internal/sites/validate" 'POST' @{siteIds=@();tenantIds=@()} $serviceHeaders) 200 'Read-only lookup, no tenant/site created'
$loginBody=@{email=$smokeSettings['SMARTPARK_ADMIN_EMAIL'];password=$smokeSettings['SMARTPARK_ADMIN_PASSWORD']}
$loginResponse=Send-SmokeRequest "$userUrl/api/auth/login" 'POST' $loginBody
Add-SmokeResult 'Admin login HTTP' $loginResponse 200
$session=$null
try {
    if ($loginResponse.Status -eq 200) {
        $loginData=$loginResponse.Content | ConvertFrom-Json
        if ($loginData.data.accessToken -and $loginData.data.refreshToken) {
            $session=$loginData.data
            $headers=@{Authorization=('Bearer '+$session.accessToken)}
            Add-SmokeResult 'Admin /me' (Send-SmokeRequest "$userUrl/api/auth/me" 'GET' $null $headers) 200
            Add-SmokeResult 'Admin applications list' (Send-SmokeRequest "$userUrl/api/owner-applications" 'GET' $null $headers) 200
            Add-SmokeResult 'Admin users list' (Send-SmokeRequest "$userUrl/api/users" 'GET' $null $headers) 200
            Add-SmokeResult 'Missing application returns 404' (Send-SmokeRequest "$userUrl/api/owner-applications/$randomId" 'GET' $null $headers) 404
            Add-SmokeResult 'Admin denied Owner operator list' (Send-SmokeRequest "$userUrl/api/operators" 'GET' $null $headers) 403
            Add-SmokeResult 'Admin denied Operator assignments' (Send-SmokeRequest "$userUrl/api/operators/me" 'GET' $null $headers) 403
            Add-SmokeResult 'Admin denied Owner Parking list' (Send-SmokeRequest "$parkingUrl/api/parking-lots" 'GET' $null $headers) 403
        } else {
            $smokeRows.Add([pscustomobject]@{Check='Admin normal session';Expected='tokens';Actual='not issued';Result='BLOCKED';Note='MFA challenge or unexpected response; protected checks not executed'})
        }
    }
} finally {
    if ($session) {
        Add-SmokeResult 'Admin logout' (Send-SmokeRequest "$userUrl/api/auth/logout" 'POST' @{refreshToken=$session.refreshToken} $headers) 200
        Add-SmokeResult 'Old access rejected after logout' (Send-SmokeRequest "$userUrl/api/auth/me" 'GET' $null $headers) 401
        Add-SmokeResult 'Old refresh rejected after logout' (Send-SmokeRequest "$userUrl/api/auth/refresh" 'POST' @{refreshToken=$session.refreshToken}) 401
    }
}
# Persist only check names/statuses. Never response bodies, credentials or tokens.
$smokeOutputDirectory = Join-Path $smokeRoot '.cache/demo-smoke'
New-Item -ItemType Directory -Path $smokeOutputDirectory -Force | Out-Null
$smokeRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $smokeOutputDirectory 'results.json') -Encoding UTF8
$smokeRows | Format-Table Check,Expected,Actual,Result -AutoSize
$failed=@($smokeRows | Where-Object Result -eq 'FAIL').Count
$blocked=@($smokeRows | Where-Object Result -eq 'BLOCKED').Count
Write-Output "Total=$($smokeRows.Count); Passed=$(@($smokeRows | Where-Object Result -eq 'PASS').Count); Failed=$failed; Blocked=$blocked"
if ($failed -gt 0 -or $blocked -gt 0) { exit 1 }
