param(
    [ValidateSet('frontend-a','frontend-b','api-c','api-d','frontend-e','api-f')]
    [string[]]$Nodes,
    [ValidateSet('isolation','scheduler')][string]$Topology = 'isolation',
    [switch]$Build,
    [switch]$SkipGatewaySwitch,
    [double]$EngineReadyTimeoutSeconds = 60,
    [ValidateRange(1,300)][int]$DemoSlowRequestSeconds = 20
)
$ErrorActionPreference = 'Stop'
$labRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $labRoot
$compose = Join-Path $labRoot "deploy/$Topology.compose.yaml"
function Invoke-Docker([string[]]$Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed: $Arguments" }
}
if ($Build) {
    foreach ($component in @('Frontend','Api')) {
        Invoke-Docker @('build','-f',"$labRoot/deploy/Dockerfile",'--build-arg',
            "PROJECT=samples/Webhosting.$component/Webhosting.$component.csproj",'--build-arg',
            "ENTRY_DLL=Webhosting.$component.dll",'-t',"webhosting-$($component.ToLowerInvariant()):local",$repositoryRoot)
    }
    Invoke-Docker @('build','-f',"$labRoot/deploy/Dockerfile",'-t','webhosting-gateway:local',$repositoryRoot)
}
$configuration = & docker compose -f $compose config --format json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Invalid isolation compose configuration' }
if (-not $Nodes) {
    $Nodes = @($configuration.services.PSObject.Properties | Where-Object {
        $_.Value.labels.'webhosting.component'
    } | ForEach-Object Name)
}
$results = Join-Path $labRoot 'TestResults'
New-Item -ItemType Directory -Force $results | Out-Null
foreach ($node in $Nodes) {
    Invoke-Docker @('compose','-f',$compose,'up','-d',$node)
    $engine = (& docker compose -f $compose ps -q $node).Trim()
    $clock = [Diagnostics.Stopwatch]::StartNew()
    do {
        & docker exec $engine docker info --format '{{.ID}}' 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        if ($clock.Elapsed.TotalSeconds -gt $EngineReadyTimeoutSeconds) { throw "Engine not ready: $node" }
        Start-Sleep -Milliseconds 500
    } while ($true)
    $component = $configuration.services.$node.labels.'webhosting.component'
    $appPort = $configuration.services.$node.ports[0].target
    $image = "webhosting-${component}:local"
    $archive = Join-Path $results "$component-image.tar"
    Invoke-Docker @('save','-o',$archive,$image)
    Invoke-Docker @('cp',$archive,"${engine}:/root/component.tar")
    Invoke-Docker @('exec',$engine,'docker','load','-i','/root/component.tar')
    Invoke-Docker @('exec',$engine,'rm','/root/component.tar')
    $existing = & docker exec $engine docker ps -aq --filter 'name=^webhosting-component$'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect component' }
    if ($existing) { Invoke-Docker @('exec',$engine,'docker','rm','-f','webhosting-component') }
    $bridge = (& docker exec $engine docker network inspect bridge | ConvertFrom-Json)[0]
    if ($LASTEXITCODE -ne 0 -or $bridge.EnableIPv6) {
        throw 'This lab policy supports IPv4 only; refusing startup with an unknown or IPv6-enabled bridge'
    }
    Invoke-Docker @('cp',"$labRoot/deploy/app-firewall.sh","${engine}:/root/app-firewall.sh")
    Invoke-Docker @('exec','--env',"APP_PORT=$appPort",$engine,'sh','-c',"tr -d '\r' < /root/app-firewall.sh | sh")
    # No auto restart: engine restarts lose rules; this script reapplies rules before app startup.
    Invoke-Docker @('exec',$engine,'docker','run','-d','--name','webhosting-component',
        '--network','bridge','--publish',"${appPort}:${appPort}",'--restart','no','--read-only','--tmpfs','/tmp:rw,noexec,nosuid',
        '--cap-drop','ALL','--security-opt','no-new-privileges','--env',"ASPNETCORE_URLS=http://0.0.0.0:$appPort",
        '--label',"webhosting.component=$component",'--env',"Demo__SlowRequestSeconds=$DemoSlowRequestSeconds",
        '--env',"NodeId=$node",$image)
}
if (-not $SkipGatewaySwitch) {
    # Only explicitly named old lab services; unrelated containers are never stopped.
    Invoke-Docker @('compose','-f',"$labRoot/deploy/monitors.compose.yaml",'stop')
    Invoke-Docker @('compose','-f',"$labRoot/deploy/ingress.compose.yaml",'stop','gateway-a','gateway-b')
    $other = if ($Topology -eq 'scheduler') { 'isolation' } else { 'scheduler' }
    Invoke-Docker @('compose','-f',"$labRoot/deploy/$other.compose.yaml",'stop','gateway-a','gateway-b','monitor-a','monitor-b')
    # Gateway port is inherited from the mounted configuration, not Compose environment.
    $gateway = Get-Content "$labRoot/deploy/$Topology-gateway.json" -Raw | ConvertFrom-Json
    $listener = Get-NetTCPConnection -State Listen -LocalPort $gateway.Gateway.Port -ErrorAction SilentlyContinue
    if ($listener) {
        $own = & docker compose -f $compose ps -q gateway-a gateway-b
        if (-not $own) { throw "Port $($gateway.Gateway.Port) is still in use; refusing to stop its owner" }
    }
    Invoke-Docker @('compose','-f',$compose,'up','-d','gateway-a','gateway-b','monitor-a','monitor-b')
    Write-Output "Open http://127.0.0.1:$($gateway.Gateway.Port)"
}
