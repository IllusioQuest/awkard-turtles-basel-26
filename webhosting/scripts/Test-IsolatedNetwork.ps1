param([switch]$FailureTests, [int]$RecoveryTimeoutSeconds = 75)
$ErrorActionPreference = 'Stop'
$labRoot = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $labRoot 'deploy/isolation.compose.yaml'
$config = & docker compose -f $compose config --format json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Cannot read Compose config' }
$gatewayConfig = Get-Content "$labRoot/deploy/isolation-gateway.json" -Raw | ConvertFrom-Json
$baseUrl = "http://127.0.0.1:$($gatewayConfig.Gateway.Port)"
function Engine([string]$Node) {
    $id = (& docker compose -f $compose ps -aq $Node).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $id) { throw "Missing node: $Node" }
    return $id
}
function Get-Http([string]$Url) {
    Invoke-WebRequest -Uri $Url -TimeoutSec 3 -SkipHttpErrorCheck
}
function Wait-Website {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $failures = 0
    $consecutive = 0
    while ($clock.Elapsed.TotalSeconds -lt $RecoveryTimeoutSeconds) {
        try {
            $page = Get-Http "$baseUrl/"
            $api = Get-Http "$baseUrl/api/server"
            if ($page.StatusCode -ne 200 -or $page.Content -notmatch 'One website, separate components' -or
                $api.StatusCode -ne 200 -or ($api.Content | ConvertFrom-Json).component -ne 'api') { throw 'Route not ready' }
            $consecutive++
            if ($consecutive -ge 5) {
                return [pscustomobject]@{ recovery_seconds=$clock.Elapsed.TotalSeconds; failed_probes=$failures }
            }
        } catch { $failures++; $consecutive=0 }
        Start-Sleep -Milliseconds 250
    }
    throw "Website did not recover within $RecoveryTimeoutSeconds seconds"
}
$results = @()
$null = Wait-Website
$engineIds = @()
foreach ($node in @('frontend-a','frontend-b','api-c','api-d')) {
    $engine = Engine $node
    $identity = & docker exec $engine docker info --format '{{.ID}}'
    if ($LASTEXITCODE -ne 0) { throw "Cannot read engine identity: $node" }
    $engineIds += $identity
    $port = $config.services.$node.ports[0].published
    $appPort = $config.services.$node.ports[0].target
    $isFrontend = $config.services.$node.labels.'webhosting.component' -eq 'frontend'
    $expected = if ($isFrontend) { '/' } else { '/api/server' }
    $denied = if ($isFrontend) { '/api/server' } else { '/' }
    if ((Get-Http "http://127.0.0.1:$port$expected").StatusCode -ne 200) { throw "$node does not serve its component" }
    if ((Get-Http "http://127.0.0.1:$port$denied").StatusCode -ne 404) { throw "$node exposes another component" }
    $app = (& docker exec $engine docker inspect webhosting-component | ConvertFrom-Json)[0]
    if (-not $app.HostConfig.ReadonlyRootfs -or $app.Config.User -in @('','0','root') -or
        $app.HostConfig.NetworkMode -ne 'bridge' -or $app.HostConfig.RestartPolicy.Name -ne 'no' -or
        $app.HostConfig.CapDrop -notcontains 'ALL' -or
        $app.HostConfig.SecurityOpt -notcontains 'no-new-privileges') { throw "Unsafe app settings: $node" }
    if (@($app.Mounts | Where-Object { $_.Type -ne 'tmpfs' }).Count -gt 0) { throw "Unexpected app mount: $node" }
    $bridge = (& docker exec $engine docker network inspect bridge | ConvertFrom-Json)[0]
    if ($bridge.EnableIPv6) { throw 'Test policy covers IPv4 only; IPv6 must remain disabled' }
    $peer = if ($node -eq 'api-d') { 'frontend-a' } else { 'api-d' }
    $peerInfo = (& docker inspect (Engine $peer) | ConvertFrom-Json)[0]
    $peerIp = ($peerInfo.NetworkSettings.Networks.PSObject.Properties | Select-Object -First 1).Value.IPAddress
    $peerPort = $config.services.$peer.ports[0].target
    # Positive control: peer HTTP is reachable from the trusted engine before testing app denial.
    & docker exec $engine wget -q -T 3 -O /dev/null "http://${peerIp}:$peerPort/health"
    if ($LASTEXITCODE -ne 0) { throw 'Peer positive control failed' }
    foreach ($target in @("http://${peerIp}:$peerPort/health", "http://$($bridge.IPAM.Config[0].Gateway):$appPort/health", 'http://1.1.1.1')) {
        & docker exec $engine nsenter -t $app.State.Pid -n wget -q -T 2 -O /dev/null $target
        if ($LASTEXITCODE -eq 0) { throw "App egress escaped policy: $node -> $target" }
    }
    $forwardRules = & docker exec $engine iptables -L WH-APP-FWD -nvx
    $hostRules = & docker exec $engine iptables -L WH-APP-HOST -nvx
    if ($LASTEXITCODE -ne 0 -or ($forwardRules -join "`n") -notmatch '(?m)^\s*[1-9]\d*\s+\d+\s+DROP' -or
        ($hostRules -join "`n") -notmatch '(?m)^\s*[1-9]\d*\s+\d+\s+DROP') { throw 'No measured firewall drop evidence' }
    $results += [pscustomobject]@{ node=$node; component_routes='passed'; egress='blocked'; engine_id=$identity }
}
if (@($engineIds | Sort-Object -Unique).Count -ne $engineIds.Count) { throw 'Engines are not independent' }
if ($FailureTests) {
    foreach ($node in @('frontend-a','frontend-b','api-c','api-d')) {
        $engine = Engine $node
        try {
            & docker kill $engine | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Cannot stop $node" }
            $recovery = Wait-Website
            $results += [pscustomobject]@{ failed_node=$node; recovery=$recovery }
        } finally {
            & "$PSScriptRoot/Start-IsolatedNetwork.ps1" -Nodes $node -SkipGatewaySwitch
        }
        $null = Wait-Website
    }
    $active = (Get-Http "$baseUrl/_platform/gateway").Content | ConvertFrom-Json
    $activeId = Engine $active.gatewayId
    try {
        & docker pause $activeId | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Cannot pause gateway' }
        $results += [pscustomobject]@{ paused_gateway=$active.gatewayId; recovery=(Wait-Website) }
    } finally {
        $state = (& docker inspect $activeId | ConvertFrom-Json)[0].State
        if ($state.Paused) { & docker unpause $activeId | Out-Null }
        & docker start $activeId | Out-Null
    }
}
$resultPath = Join-Path $labRoot 'TestResults/isolation-results.json'
$results | ConvertTo-Json -Depth 8 | Set-Content $resultPath
$results | ConvertTo-Json -Depth 8
