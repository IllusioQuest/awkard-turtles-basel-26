param([switch]$FailureTests, [int]$RecoveryTimeoutSeconds = 90)
$ErrorActionPreference = 'Stop'
if ($RecoveryTimeoutSeconds -lt 10) { throw 'Recovery timeout must be at least ten seconds' }
$labRoot = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $labRoot 'deploy/scheduler.compose.yaml'
$planPath = Join-Path $labRoot 'TestResults/hosting-state/plan.json'
$resultPath = Join-Path $labRoot 'TestResults/scheduler-results.json'
$gatewayConfig = Get-Content "$labRoot/deploy/scheduler-gateway.json" -Raw | ConvertFrom-Json
$baseUrl = "http://127.0.0.1:$($gatewayConfig.Gateway.Port)"
$original = Get-Content -LiteralPath $planPath -Raw
$fixture = Get-Content "$labRoot/deploy/hosting-plan.example.json" -Raw
$results = [Collections.Generic.List[object]]::new()
$engines = @{}
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds($RecoveryTimeoutSeconds)
$killedGateway = $null
$slowTask = $null
$failure = $null

function Write-PlanText([string]$Text) {
    $temporary = "$planPath.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        [IO.File]::WriteAllText($temporary, $Text)
        Move-Item -LiteralPath $temporary -Destination $planPath -Force
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}
function New-Plan([string]$Name) {
    $plan = $fixture | ConvertFrom-Json -AsHashtable
    $plan.revision = "integration-$Name-$([Guid]::NewGuid().ToString('N'))"
    $plan.valid_until_utc = [DateTimeOffset]::UtcNow.AddHours(1).ToString('o')
    return $plan
}
function Write-Plan($Plan) { Write-PlanText ($Plan | ConvertTo-Json -Depth 30) }
function Get-Http([string]$Path) {
    Invoke-WebRequest "$baseUrl$Path" -TimeoutSec 3 -SkipHttpErrorCheck
}
function Get-Hosting { return (Get-Http '/_platform/hosting').Content | ConvertFrom-Json }
function Engine([string]$Node) {
    if (-not $engines.ContainsKey($Node)) {
        $id = (& docker compose -f $compose ps -aq $Node | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or -not $id) { throw "Missing lab container: $Node" }
        $engines[$Node] = $id
    }
    return $engines[$Node]
}
function Get-ContainerState([string]$Node) {
    $json = & docker exec (Engine $Node) docker inspect --format '{{json .State}}' webhosting-component
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect inner component: $Node" }
    return $json | ConvertFrom-Json
}
function Get-States {
    $states = @{}
    foreach ($node in ($fixture | ConvertFrom-Json).nodes) { $states[$node.id] = Get-ContainerState $node.id }
    return $states
}
function Test-TwoReady($Snapshot) {
    foreach ($component in @('api', 'frontend')) {
        if (@($Snapshot.replicas | Where-Object { $_.component -eq $component -and $_.ready }).Count -lt 2) { return $false }
    }
    return $true
}
function Test-Website {
    $page = Get-Http '/'
    $api = Get-Http '/api/server'
    return ($page.StatusCode -eq 200 -and $api.StatusCode -eq 200 -and
        ($api.Content | ConvertFrom-Json).component -eq 'api')
}
function Wait-Until([string]$Description, [scriptblock]$Condition, [int]$PollMilliseconds = 250) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $lastError = ''
    while ($clock.Elapsed.TotalSeconds -lt $RecoveryTimeoutSeconds) {
        try { if (& $Condition) { return } } catch { $lastError = $_.Exception.Message }
        Start-Sleep -Milliseconds $PollMilliseconds
    }
    throw "Timed out: $Description. $lastError"
}
function Wait-Layout($Plan, [string[]]$Paused) {
    Wait-Until "layout $($Plan.revision)" {
        $snapshot = Get-Hosting
        if ($snapshot.revision -ne $Plan.revision -or -not (Test-TwoReady $snapshot)) { return $false }
        foreach ($node in $Plan.nodes) {
            $state = Get-ContainerState $node.id
            if (-not $state.Running -or [bool]$state.Paused -ne ($Paused -contains $node.id)) { return $false }
        }
        return (Test-Website)
    }
}
function Record([string]$Name, $Details) {
    $results.Add([ordered]@{ check = $Name; status = 'passed'; observed_utc = [DateTimeOffset]::UtcNow.ToString('o'); details = $Details })
}

try {
    $baseline = New-Plan 'baseline'
    Write-Plan $baseline
    Wait-Layout $baseline @('frontend-e', 'api-f')
    Record 'baseline_actual_pause' (Get-States)

    # Start a real request on the uniquely hottest API before changing its heat rank.
    Wait-Until 'baseline API selection' { ((Get-Http '/api/server').Content | ConvertFrom-Json).node_id -eq 'api-c' }
    $slowTask = $client.GetAsync("$baseUrl/api/slow")
    Wait-Until 'long request admitted on api-c' {
        @((Get-Hosting).replicas | Where-Object { $_.node_id -eq 'api-c' -and $_.in_flight -gt 0 }).Count -eq 1
    } 50
    $swapped = New-Plan 'heat-swap'
    $swapped.heat_signals.'api-f'.demand_kwh = 0.8
    $swapped.heat_signals.'api-c'.demand_kwh = 0
    $swapped.heat_signals.'frontend-e'.demand_kwh = 0.8
    $swapped.heat_signals.'frontend-a'.demand_kwh = 0
    Write-Plan $swapped
    $drainObserved = $false
    $samples = 0
    $requestClock = [Diagnostics.Stopwatch]::StartNew()
    while (-not $slowTask.IsCompleted) {
        if ($requestClock.Elapsed.TotalSeconds -gt $RecoveryTimeoutSeconds) { throw 'Long request did not complete' }
        $snapshot = Get-Hosting
        $beforeInspection = -not $slowTask.IsCompleted
        $state = Get-ContainerState 'api-c'
        # Completion can race Docker inspection, so require the request to still be outstanding afterward.
        if ($beforeInspection -and -not $slowTask.IsCompleted) {
            if ($state.Paused -or -not $state.Running) { throw 'Serving node paused while long request remained in flight' }
            if ($snapshot.phase -eq 'drain:api-c') { $drainObserved = $true; $samples++ }
        }
        Start-Sleep -Milliseconds 50
    }
    $slowResponse = $slowTask.GetAwaiter().GetResult()
    try {
        if (-not $slowResponse.IsSuccessStatusCode) { throw "Long request returned $($slowResponse.StatusCode)" }
    } finally { $slowResponse.Dispose() }
    $slowTask = $null
    if (-not $drainObserved) { throw 'Drain phase was not observed during the long request; test cannot claim drain safety' }
    Wait-Layout $swapped @('frontend-a', 'api-c')
    Wait-Until 'API follows hottest spare' { ((Get-Http '/api/server').Content | ConvertFrom-Json).node_id -eq 'api-f' }
    Record 'heat_swap_and_long_request_drain' @{ drain_samples = $samples; states = Get-States; api_node = 'api-f' }

    $zero = New-Plan 'zero-heat'
    foreach ($signal in $zero.heat_signals.Values) { $signal.demand_kwh = 0 }
    Write-Plan $zero
    Wait-Layout $zero @('frontend-e', 'api-f')
    Record 'zero_heat_serves_requests' (Get-States)

    $stale = New-Plan 'stale'
    $stale.valid_until_utc = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o')
    Write-Plan $stale
    Wait-Layout $stale @()
    Record 'stale_heat_resumes_all' (Get-States)

    Write-Plan $baseline
    Wait-Layout $baseline @('frontend-e', 'api-f')
    $partial = New-Plan 'partial-api'
    $partial.heat_signals.Remove('api-f')
    Write-Plan $partial
    Wait-Layout $partial @('frontend-e')
    Record 'partial_heat_resumes_entire_api_pool' (Get-States)

    $denied = New-Plan 'locality-denied'
    ($denied.services | Where-Object { $_.component -eq 'api' }).placement_policy.allowed_country_codes = @('DE')
    Write-Plan $denied
    Wait-Until 'locality denies API while frontend survives' {
        (Get-Hosting).revision -eq $denied.revision -and (Get-Http '/api/server').StatusCode -eq 503 -and (Get-Http '/').StatusCode -eq 200
    }
    Record 'locality_denial' @{ api_status = 503; frontend_status = 200 }
    Write-Plan $baseline
    Wait-Layout $baseline @('frontend-e', 'api-f')

    if ($FailureTests) {
        # Cache identity before the short pause phase to minimize the injection delay.
        $active = (Get-Http '/_platform/gateway').Content | ConvertFrom-Json
        $activeId = Engine $active.gatewayId
        $faultPlan = New-Plan 'pause-failover'
        $faultPlan.heat_signals.'frontend-e'.demand_kwh = 0.8
        $faultPlan.heat_signals.'frontend-a'.demand_kwh = 0
        Write-Plan $faultPlan
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $observed = $null
        while ($clock.Elapsed.TotalSeconds -lt $RecoveryTimeoutSeconds) {
            $snapshot = Get-Hosting
            if ($snapshot.revision -eq $faultPlan.revision -and $snapshot.phase -like 'pause:*') {
                $observed = $snapshot.phase
                & docker kill $activeId | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Cannot kill observed active gateway' }
                $killedGateway = $activeId
                break
            }
            Start-Sleep -Milliseconds 50
        }
        if (-not $observed) { throw 'Pause phase was not observed; gateway fault test was NOT performed' }
        Wait-Until 'gateway failover with two ready replicas per component' {
            (Test-Website) -and (Test-TwoReady (Get-Hosting)) -and
                (((Get-Http '/_platform/gateway').Content | ConvertFrom-Json).gatewayId -ne $active.gatewayId)
        }
        Record 'gateway_killed_after_observed_pause' @{ phase = $observed; failed_gateway = $active.gatewayId; recovered = Get-Hosting; states = Get-States }
    }
} catch {
    $failure = $_
    $results.Add([ordered]@{ check = 'run'; status = 'failed'; error = $_.Exception.Message })
} finally {
    try {
        # Restore the exact caller-owned plan, including its original expiry and policy.
        Write-PlanText $original
        if ($killedGateway) {
            & docker start $killedGateway | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Cannot restart injected gateway failure' }
        }
        if ($slowTask) {
            try { $pendingResponse = $slowTask.GetAwaiter().GetResult(); $pendingResponse.Dispose() } catch { }
        }
        Wait-Until 'original plan restored and website recovered' {
            (Test-Website) -and (Test-TwoReady (Get-Hosting)) -and
                ((Get-Hosting).revision -eq ($original | ConvertFrom-Json).revision)
        }
        Record 'original_plan_restored' (Get-Hosting)
    } catch {
        $results.Add([ordered]@{ check = 'restore'; status = 'failed'; error = $_.Exception.Message })
        if (-not $failure) { $failure = $_ }
    } finally {
        $client.Dispose()
        $results | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath
    }
}
$results | ConvertTo-Json -Depth 20
if ($failure) { throw $failure }
