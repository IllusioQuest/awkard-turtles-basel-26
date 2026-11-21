param([int]$TimeoutSeconds = 45)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ingress = Join-Path $root 'deploy/ingress.compose.yaml'
$monitors = Join-Path $root 'deploy/monitors.compose.yaml'
function Compose($File, [string[]]$Arguments) {
    & docker compose -f $File @Arguments | Out-Host
    if ($LASTEXITCODE) { throw "Compose failed: $Arguments" }
}
function Identity { (Invoke-RestMethod http://127.0.0.1:18080/_platform/gateway -TimeoutSec 2).gatewayId }
$results = @()
try {
    Compose $ingress @('up','-d','--no-build','gateway-a','gateway-b')
    Compose $monitors @('up','-d')
    Start-Sleep -Seconds 3
    # A paused non-gateway sample must never be managed, even during an ingress outage.
    $unrelated = 'webhosting-engines-engine-c-1'
    docker pause $unrelated | Out-Host
    if ($LASTEXITCODE) { throw 'Cannot pause negative-control container' }
    foreach ($missing in @('monitor-a','monitor-b','none')) {
        if ($missing -ne 'none') { Compose $monitors @('stop',$missing) }
        $active = Identity
        $id = (docker compose -f $ingress ps -q $active).Trim()
        docker pause $id | Out-Host
        if ($LASTEXITCODE) { throw 'Cannot pause gateway' }
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $failed = 0
        $survivor = $null
        while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
            try { $candidate = Identity; if ($candidate -ne $active) { $survivor = $candidate; break } } catch { $failed++ }
            Start-Sleep -Milliseconds 100
        }
        if (-not $survivor) { throw 'Gateway takeover timed out' }
        $state = docker inspect --format '{{.State.Running}} {{.State.Paused}}' $id
        if ($state -ne 'false false') { throw "Frozen gateway not stopped: $state" }
        if ((docker inspect --format '{{.State.Paused}}' $unrelated) -ne 'true') { throw 'Monitor changed unrelated container' }
        $results += [pscustomobject]@{MissingMonitor=$missing; Paused=$active; Survivor=$survivor; SecondsAfterPauseCommand=[Math]::Round($clock.Elapsed.TotalSeconds,3); FailedProbes=$failed}
        Compose $ingress @('start',$active)
        Compose $monitors @('start')
        Start-Sleep -Seconds 3
    }
    $results | ConvertTo-Json | Set-Content (Join-Path $root 'TestResults/container-pause.json')
    $results | Format-Table
} finally {
    & docker compose -f $ingress unpause | Out-Host
    & docker compose -f $ingress start gateway-a gateway-b | Out-Host
    & docker unpause webhosting-engines-engine-c-1 | Out-Host
    & docker compose -f $monitors start | Out-Host
}
