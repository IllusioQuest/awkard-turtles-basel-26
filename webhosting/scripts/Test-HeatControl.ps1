param(
    [string]$Date = '2025-07-15',
    [double]$CpuCores = 1,
    [double]$StepSeconds = 0.1,
    [int]$ForcedPauseStep = 12
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$container = $null
Push-Location $root
try {
    # Dedicated disposable replica: no published ports, no Docker socket, no live gateway role.
    $container = & docker run -d --cpus $CpuCores --label digitalheat.experiment=T-23 webhosting-sample:local
    if ($LASTEXITCODE -ne 0) { throw 'Build webhosting-sample:local before this experiment.' }
    $container = $container.Trim()
    & dotnet run --project src/DigitalHeat.LiveRun -- --date $Date --job webhosting/deploy/heat-control-probe.json --container $container --cpu-cores $CpuCores --step-seconds $StepSeconds --force-pause-step $ForcedPauseStep
    if ($LASTEXITCODE -ne 0) { throw 'Shared scheduler replay failed.' }
    $paused = & docker inspect --format '{{.State.Paused}}' $container
    if ($LASTEXITCODE -ne 0 -or $paused -ne 'false') { throw 'Executor did not restore the sample.' }
} finally {
    if ($container) { & docker rm -f $container | Out-Host }
    Pop-Location
}
