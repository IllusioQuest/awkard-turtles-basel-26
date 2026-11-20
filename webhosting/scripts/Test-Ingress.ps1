param(
    [double]$RecoveryTimeoutSeconds = 20,
    [int]$ProbeIntervalMilliseconds = 100,
    [int]$RequestTimeoutMilliseconds = 1500,
    [int]$Rounds = 2,
    [switch]$NestedEngines,
    [int]$ConsecutiveSuccesses = 20
)
$ErrorActionPreference = 'Stop'
$webhostingRoot = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $webhostingRoot 'deploy/ingress.compose.yaml'
$engines = Join-Path $webhostingRoot 'deploy/engines.compose.yaml'
$configuration = Get-Content -LiteralPath (Join-Path $webhostingRoot 'src/Webhosting.Gateway/appsettings.json') -Raw | ConvertFrom-Json
$url = "http://127.0.0.1:$($configuration.Gateway.Port)"
if ($RecoveryTimeoutSeconds -le 0 -or $ProbeIntervalMilliseconds -le 0 -or $RequestTimeoutMilliseconds -le 0 -or $Rounds -lt 1 -or $ConsecutiveSuccesses -lt 1) { throw 'Probe parameters must be positive.' }
Add-Type -AssemblyName System.Net.Http
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.UseProxy = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMilliseconds($RequestTimeoutMilliseconds)
$measurements = [System.Collections.Generic.List[object]]::new()

function Invoke-Compose([string[]]$Arguments) {
    & docker compose -f $compose @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Compose failed: $Arguments" }
}
function Get-Sample {
    $response = $client.GetAsync("$url/api/server").GetAwaiter().GetResult()
    try {
        $response.EnsureSuccessStatusCode() | Out-Null
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        return [pscustomobject]@{ Gateway = ($response.Headers.GetValues('X-Webhosting-Gateway') | Select-Object -First 1); Sample = $body.instanceId; Runtime = $body.runtime }
    } finally { $response.Dispose() }
}
function Wait-Standby([string]$Name) {
    $container = (& docker compose -f $compose ps -q $Name).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $container) { throw "Missing standby $Name" }
    $startedAt = & docker inspect --format '{{.State.StartedAt}}' $container
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect $Name" }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $RecoveryTimeoutSeconds) {
        $logs = & docker logs --since $startedAt $container 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Cannot read $Name logs" }
        if ($logs -match "GATEWAY_STANDBY ${Name}:") { return }
        Start-Sleep -Milliseconds $ProbeIntervalMilliseconds
    }
    throw "$Name did not enter standby during its current start"
}
function Wait-Sample([string]$DifferentGateway = '', [string]$ExpectedSample = '', [int]$RequiredSuccesses = 1) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $failed = 0
    $streak = 0
    $firstSuccessSeconds = $null
    while ($clock.Elapsed.TotalSeconds -lt $RecoveryTimeoutSeconds) {
        try {
            $sample = Get-Sample
            if (($DifferentGateway -eq '' -or $sample.Gateway -ne $DifferentGateway) -and
                ($ExpectedSample -eq '' -or $sample.Sample -eq $ExpectedSample)) {
                $streak++
                if ($null -eq $firstSuccessSeconds) { $firstSuccessSeconds = $clock.Elapsed.TotalSeconds }
                if ($streak -ge $RequiredSuccesses) {
                    return [pscustomobject]@{ Sample = $sample; FirstSuccessSeconds = $firstSuccessSeconds; ElapsedSeconds = $clock.Elapsed.TotalSeconds; FailedRequests = $failed }
                }
            } else { $streak = 0; $failed++ }
        } catch { $failed++; $streak = 0 }
        Start-Sleep -Milliseconds $ProbeIntervalMilliseconds
    }
    throw "No acceptable response at unchanged URL $url within $RecoveryTimeoutSeconds seconds; failed requests: $failed"
}

try {
    if ($NestedEngines) {
        # Start-Network.ps1 prepares these engines; do not start conflicting standalone samples.
        Invoke-Compose @('up', '-d', '--no-build', 'gateway-a', 'gateway-b')
        $engineConfiguration = (& docker compose -f $engines config --format json | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read engine configuration' }
        foreach ($name in @('engine-a', 'engine-b', 'engine-c')) {
            $port = $engineConfiguration.services.$name.ports[0].published
            $response = $client.GetAsync("http://127.0.0.1:$port/api/server").GetAwaiter().GetResult()
            try {
                $response.EnsureSuccessStatusCode() | Out-Null
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
                if ($body.instanceId -ne $name) { throw "Unexpected sample on $name" }
            } finally { $response.Dispose() }
        }
    } else {
        $runningEngines = & docker compose -f $engines ps -q --status running
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect engines' }
        if ($runningEngines) { throw 'Nested engines are running. Use -NestedEngines or stop the engines first.' }
        Invoke-Compose @('up', '-d', '--no-build')
    }
    $initial = Wait-Sample
    if ($initial.Sample.Runtime -notmatch 'Linux|Debian|Ubuntu|Alpine') { throw 'Expected Linux sample runtime.' }
    foreach ($round in 1..$Rounds) {
        $active = (Wait-Sample).Sample.Gateway
        if ($active -notin @('gateway-a', 'gateway-b')) { throw "Unexpected gateway identity: $active" }
        $standby = if ($active -eq 'gateway-a') { 'gateway-b' } else { 'gateway-a' }
        # No restart policy: takeover must happen in the OTHER, already-running container.
        Invoke-Compose @('kill', '-s', 'SIGKILL', $standby)
        $afterStandby = Wait-Sample
        if ($afterStandby.Sample.Gateway -ne $active) { throw 'Active identity changed unexpectedly.' }
        Invoke-Compose @('start', $standby)
        Wait-Standby $standby
        Invoke-Compose @('kill', '-s', 'SIGKILL', $active)
        $recovered = Wait-Sample -DifferentGateway $active
        $measurements.Add([pscustomobject]@{
            Scenario = "gateway-crash-$round"; Killed = $active; Survivor = $recovered.Sample.Gateway;
            FirstSuccessSecondsAfterKillCommand = [Math]::Round($recovered.FirstSuccessSeconds, 3);
            ConfirmationSecondsAfterKillCommand = [Math]::Round($recovered.ElapsedSeconds, 3);
            RequiredConsecutiveSuccesses = 1; PostCommandFailedProbes = $recovered.FailedRequests; Url = $url
        })
        Invoke-Compose @('start', $active)
    }
    # Freeze only the worker. Its local watchdog remains runnable and must free the shared port.
    $active = (Wait-Sample).Sample.Gateway
    $standby = if ($active -eq 'gateway-a') { 'gateway-b' } else { 'gateway-a' }
    Wait-Standby $standby
    $container = (& docker compose -f $compose ps -q $active).Trim()
    $startedAt = & docker inspect --format '{{.State.StartedAt}}' $container
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect active gateway' }
    $workerLine = & docker logs --since $startedAt $container 2>&1 | Select-String '^GATEWAY_WORKER_PID (\d+)$' | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0 -or -not $workerLine) { throw 'Gateway watchdog must be enabled for the process-freeze test' }
    $workerId = [int]$workerLine.Matches[0].Groups[1].Value
    & docker exec $container kill -STOP $workerId
    if ($LASTEXITCODE -ne 0) { throw 'Cannot freeze gateway worker' }
    $recovered = Wait-Sample -DifferentGateway $active
    $watchdogLog = & docker logs --since $startedAt $container 2>&1
    if (-not ($watchdogLog -match "GATEWAY_WATCHDOG_FAILED ${active}:")) { throw 'No watchdog termination evidence' }
    $measurements.Add([pscustomobject]@{
        Scenario = 'gateway-worker-freeze'; Killed = $active; Survivor = $recovered.Sample.Gateway;
        FirstSuccessSecondsAfterKillCommand = [Math]::Round($recovered.FirstSuccessSeconds, 3);
        ConfirmationSecondsAfterKillCommand = [Math]::Round($recovered.ElapsedSeconds, 3);
        RequiredConsecutiveSuccesses = 1; PostCommandFailedProbes = $recovered.FailedRequests; Url = $url
    })
    Invoke-Compose @('start', $active)
    $backendCompose = if ($NestedEngines) { $engines } else { $compose }
    $targets = if ($NestedEngines) { @('engine-a', 'engine-b') } else { @('sample-a') }
    foreach ($target in $targets) {
        $survivor = if ($target -eq 'engine-a') { 'engine-b' } elseif ($target -eq 'engine-b') { 'engine-a' } else { 'sample-b' }
        # Ensure both routed replicas are healthy before each independent failure.
        Wait-Sample -ExpectedSample $target | Out-Null
        Wait-Sample -ExpectedSample $survivor | Out-Null
        & docker compose -f $backendCompose kill -s SIGKILL $target | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Cannot kill $target" }
        $backend = Wait-Sample -ExpectedSample $survivor -RequiredSuccesses $ConsecutiveSuccesses
        $measurements.Add([pscustomobject]@{
            Scenario = 'backend-crash'; Killed = $target; Survivor = $survivor;
            FirstSuccessSecondsAfterKillCommand = [Math]::Round($backend.FirstSuccessSeconds, 3);
            ConfirmationSecondsAfterKillCommand = [Math]::Round($backend.ElapsedSeconds, 3);
            RequiredConsecutiveSuccesses = $ConsecutiveSuccesses; PostCommandFailedProbes = $backend.FailedRequests; Url = $url
        })
        & docker compose -f $backendCompose start $target | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Cannot restore $target" }
        Wait-Sample -ExpectedSample $target | Out-Null
    }
    $resultsDirectory = Join-Path $webhostingRoot 'TestResults'
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
    $resultPath = Join-Path $resultsDirectory $(if ($NestedEngines) { 'network.json' } else { 'ingress.json' })
    $measurements | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath
    $measurements | Format-Table -AutoSize
    Write-Output "Measurements: $resultPath"
} finally {
    # Restore only this experiment's containers; never alter unrelated Docker workloads.
    if ($NestedEngines) {
        & docker compose -f $engines start | Out-Host
        & docker compose -f $compose start gateway-a gateway-b | Out-Host
    } else {
        & docker compose -f $compose start gateway-a gateway-b | Out-Host
        if (-not $runningEngines) { & docker compose -f $compose start sample-a sample-b | Out-Host }
    }
    $client.Dispose()
}
