param([string]$OutputPath,[string]$ApiCheckout)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$root=Split-Path -Parent $PSScriptRoot
if(!$OutputPath){$OutputPath=Join-Path $root 'results/performance/monitor-api-cold-warm.json'}
if(!$ApiCheckout){$ApiCheckout=$root}
$policy=Get-Content (Join-Path $ApiCheckout 'gaming/deploy/monitor-api.json') -Raw | ConvertFrom-Json
if(!$policy){throw 'Run from the monitoring feature checkout or copy its trusted API policy; this diagnostic requires the existing local API.'}
$base="http://127.0.0.1:$($policy.port)/api/monitor"
$config=Invoke-RestMethod "$base/config" -TimeoutSec 3
$request=$config.default_inputs
$request.observe_docker=$true
$request.initial_reservations=1
$client=[Net.Http.HttpClient]::new()
$rows=@()
try {
    $body=$request | ConvertTo-Json -Compress
    for($i=0;$i -lt 4;$i++){
        # Ensure two fresh-cache attempts. This pause does not enter the request timer.
        if($i -eq 0 -or $i -eq 2){Start-Sleep -Seconds ($config.observation_cache_seconds+1)}
        $content=[Net.Http.StringContent]::new($body,[Text.Encoding]::UTF8,'application/json')
        $started=[DateTimeOffset]::UtcNow
        $timer=[Diagnostics.Stopwatch]::StartNew()
        $response=$client.PostAsync("$base/snapshot",$content).GetAwaiter().GetResult()
        $raw=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $timer.Stop()
        if(!$response.IsSuccessStatusCode){throw "API returned $($response.StatusCode)"}
        $snapshot=$raw | ConvertFrom-Json
        $stamp=$snapshot.nodes[0].observed_at_utc
        # PowerShell 7 may deserialize ISO timestamps into DateTime, unlike Windows PowerShell 5.
        $observed=if($stamp -is [DateTime]){[DateTimeOffset]$stamp}else{[DateTimeOffset]::Parse($stamp,[Globalization.CultureInfo]::InvariantCulture)}
        $rows+=[ordered]@{request_index=$i;elapsed_ms=$timer.Elapsed.TotalMilliseconds;
            cache_refreshed_during_request=($observed -ge $started);observation_age_ms=([DateTimeOffset]::UtcNow-$observed).TotalMilliseconds}
        $content.Dispose();$response.Dispose()
    }
} finally {$client.Dispose()}
New-Item -ItemType Directory -Path (Split-Path ([IO.Path]::GetFullPath($OutputPath))) -Force | Out-Null
$apiCommit=(& git -C $ApiCheckout rev-parse HEAD | Out-String).Trim()
[ordered]@{mode='Existing PR35 local API, not pure scheduler';source_code=$apiCommit;
    method='HttpClient request through complete response read; parsing and forced cache expiry wait excluded. Two fresh and two warm attempts; not a percentile distribution. No Docker writes.';
    measurements=$rows} | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $OutputPath
$rows | ConvertTo-Json -Depth 4
