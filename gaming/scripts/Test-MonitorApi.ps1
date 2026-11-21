param([switch]$ObserveDocker)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$policy=Get-Content (Join-Path $root 'gaming/deploy/monitor-api.json') -Raw | ConvertFrom-Json
$base="http://127.0.0.1:$($policy.port)/api/monitor"
$config=Invoke-RestMethod "$base/config"
$inputs=$config.default_inputs
$inputs.selected_hour_local=$config.summer_hour_local
$inputs.initial_reservations=3
$inputs.bypass_capabilities=$true
$inputs.observe_docker=[bool]$ObserveDocker
$snapshot=Invoke-RestMethod "$base/snapshot" -Method Post -ContentType 'application/json' -Body ($inputs | ConvertTo-Json)
if($snapshot.schema_version -ne 1 -or $snapshot.local_hour -ne $config.summer_hour_local -or $snapshot.sites.Count -eq 0){throw 'Invalid snapshot contract'}
if(!$ObserveDocker -and @($snapshot.nodes | Where-Object {$null -ne $_.container_running -or $null -ne $_.cpu_percent}).Count){throw 'Disabled observations fabricated readings'}
$cases=@(
    @{Name='Host';Uri="$base/config";Headers=@{Host="untrusted.example:$($policy.port)"};Method='Get';Expected=403},
    @{Name='Origin';Uri="$base/config";Headers=@{Origin='http://untrusted.example'};Method='Get';Expected=403},
    @{Name='Required inputs';Uri="$base/snapshot";Headers=@{};Method='Post';Body='{}';ContentType='application/json';Expected=400},
    @{Name='Content type';Uri="$base/snapshot";Headers=@{};Method='Post';Body='{}';ContentType='text/plain';Expected=400},
    @{Name='Unknown command field';Uri="$base/snapshot";Headers=@{};Method='Post';Body=(($inputs | ConvertTo-Json) -replace '^\{','{"command":"stop",');ContentType='application/json';Expected=400}
)
foreach($case in $cases){
    $status=$null
    try {
        $http=@{Uri=$case.Uri;Method=$case.Method;Headers=$case.Headers;UseBasicParsing=$true}
        if($case.ContainsKey('Body')){$http.Body=$case.Body;$http.ContentType=$case.ContentType}
        $response=Invoke-WebRequest @http
        $status=[int]$response.StatusCode
    } catch {
        if(!$_.Exception.Response){throw}
        $status=[int]$_.Exception.Response.StatusCode
    }
    if($status -ne $case.Expected){throw "$($case.Name): expected $($case.Expected), got $status"}
}
Write-Output 'PASS: snapshot contract, unavailable observation semantics, Host/Origin restrictions, required/unknown inputs and JSON content type. No Docker writes.'
