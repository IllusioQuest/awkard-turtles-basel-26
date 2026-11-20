param([double]$EngineReadyTimeoutSeconds = 45)
$ErrorActionPreference = 'Stop'
$webhostingRoot = Split-Path -Parent $PSScriptRoot
$ingress = Join-Path $webhostingRoot 'deploy/ingress.compose.yaml'
$engines = Join-Path $webhostingRoot 'deploy/engines.compose.yaml'
$gatewayConfiguration = Get-Content -LiteralPath (Join-Path $webhostingRoot 'src/Webhosting.Gateway/appsettings.json') -Raw | ConvertFrom-Json
function Invoke-Docker([string[]]$Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed: $Arguments" }
}
# Replace only our simple sample containers; retain the two Linux gateway containers.
Invoke-Docker @('compose', '-f', $ingress, 'stop', 'sample-a', 'sample-b')
Invoke-Docker @('compose', '-f', $engines, 'up', '-d')
$resultsDirectory = Join-Path $webhostingRoot 'TestResults'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$archive = Join-Path $resultsDirectory 'sample-image.tar'
Invoke-Docker @('save', '--output', $archive, 'webhosting-sample:local')
$identities = @()
$engineConfiguration = (& docker compose -f $engines config --format json | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0) { throw 'Cannot read engine configuration' }
foreach ($name in @('engine-a', 'engine-b', 'engine-c')) {
    $container = (& docker compose -f $engines ps -q $name).Trim()
    if (-not $container) { throw "Engine container missing: $name" }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    do {
        $identity = & docker exec $container docker info --format '{{.ID}}' 2>$null
        if ($LASTEXITCODE -eq 0) { break }
        if ($clock.Elapsed.TotalSeconds -ge $EngineReadyTimeoutSeconds) { throw "$name did not become ready" }
        Start-Sleep -Milliseconds 500
    } while ($true)
    $identities += [pscustomobject]@{ Node = $name; EngineId = $identity }
    # The official dind wrapper mounts a private /tmp; use the shared container root filesystem.
    Invoke-Docker @('cp', $archive, "${container}:/root/webhosting-sample.tar")
    Invoke-Docker @('exec', $container, 'docker', 'load', '--input', '/root/webhosting-sample.tar')
    Invoke-Docker @('exec', $container, 'rm', '/root/webhosting-sample.tar')
    $existing = & docker exec $container docker ps -aq --filter name=^webhosting-sample$
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect nested sample' }
    if ($existing) { Invoke-Docker @('exec', $container, 'docker', 'rm', '-f', 'webhosting-sample') }
    $port = $engineConfiguration.services.$name.ports[0].target
    Invoke-Docker @('exec', $container, 'docker', 'run', '-d', '--name', 'webhosting-sample', '--network', 'host',
        '--restart', 'unless-stopped', '--env', "ASPNETCORE_URLS=http://0.0.0.0:$port", '--env', "Sample__InstanceId=$name", 'webhosting-sample:local')
}
if (@($identities.EngineId | Sort-Object -Unique).Count -ne $identities.Count) { throw 'Engines must have distinct identities' }
$identities | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resultsDirectory 'engines.json')
Invoke-Docker @('compose', '-f', $ingress, 'up', '-d', '--no-build', 'gateway-a', 'gateway-b')
$identities | Format-Table -AutoSize
Write-Output "Open http://127.0.0.1:$($gatewayConfiguration.Gateway.Port)"
