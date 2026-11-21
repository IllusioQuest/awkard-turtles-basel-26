param([switch]$Apply,[switch]$AllowSharedGpuProbe)
$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
$policy=Get-Content (Join-Path $lab 'deploy/cs16-lab.json') -Raw | ConvertFrom-Json
$ids=@(& docker --context $policy.docker_context ps -q --filter "label=com.docker.compose.project=$($policy.compose_project)" --filter 'label=com.docker.compose.service=session-e')
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1){throw 'Expected one running unused E node.'}
$processes=(& docker --context $policy.docker_context exec $ids[0] ps -eo comm | Out-String)
if($LASTEXITCODE -ne 0){throw 'Cannot verify E processes.'}
if($processes -match '(?im)^.*(?:\.exe|wine|wineserver).*$'){throw 'E has a Windows/Wine process; no restart authorized by this experiment.'}
if(!$Apply){Write-Output 'Plan: recreate only unused session-e with Intel WSL device and read-only driver libraries; retain home/limits/network. D, relay and Minecraft are untouched. Run with -Apply to execute.'; return}
$running=@(& docker --context $policy.docker_context ps -q)
if($LASTEXITCODE -ne 0){throw 'Cannot inspect shared GPU occupancy.'}
foreach($runningId in $running){
 if($ids[0].StartsWith($runningId) -or $runningId.StartsWith($ids[0])){continue}
 $deviceJson=(& docker --context $policy.docker_context inspect $runningId --format '{{json .HostConfig.Devices}}' | Out-String)
 if($LASTEXITCODE -ne 0){throw 'Cannot inspect shared GPU users.'}
 if($deviceJson -match '/dev/dxg' -and !$AllowSharedGpuProbe){throw 'Another running container exposes the shared GPU. Wait for idle workloads or obtain explicit bounded contention approval and use -AllowSharedGpuProbe.'}
}
$image=(& docker --context $policy.docker_context image inspect $policy.image --format '{{.Id}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $image -notmatch '^sha256:[a-f0-9]{64}$'){throw 'Build the reviewed local CS image first; this probe never pulls images.'}
Write-Output "Using configured local CS lab image: $image"
$values=@{CS16_IMAGE=$image;CS16_PORT_D=$policy.nodes[0].browser_port;CS16_PORT_E=$policy.nodes[1].browser_port;
 CS16_CPUS=$policy.cpu_limit_per_node;CS16_MEMORY_MIB=$policy.memory_limit_mib_per_node;CS16_PIDS=$policy.pids_limit_per_node;
 CS16_RELAY_IMAGE=$policy.relay_image;CS16_RELAY_CPUS=$policy.relay_cpu_limit;CS16_RELAY_MEMORY_MIB=$policy.relay_memory_limit_mib;
 CS16_RELAY_PIDS=$policy.relay_pids_limit;CS16_RELAY_ROUTES='[]'}
$previous=@{}
try {
 foreach($key in $values.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,[Convert]::ToString($values[$key],[Globalization.CultureInfo]::InvariantCulture),'Process')}
 $merged=(& docker --context $policy.docker_context compose -f (Join-Path $lab 'deploy/cs16.compose.yaml') -f (Join-Path $lab 'deploy/cs16-intel-e.override.yaml') config --format json | Out-String) | ConvertFrom-Json
 if($LASTEXITCODE -ne 0 -or $merged.services.'session-e'.environment.LIBGL_ALWAYS_SOFTWARE -ne 'false' -or $merged.services.'session-e'.environment.GALLIUM_DRIVER -ne 'd3d12'){throw 'Merged E configuration does not select the intended graphics path.'}
 Write-Output 'Merged E config verified: D3D12 OpenGL requested; CPU encoding retained. This does not prove GPU compatibility.'
 & docker --context $policy.docker_context compose -f (Join-Path $lab 'deploy/cs16.compose.yaml') -f (Join-Path $lab 'deploy/cs16-intel-e.override.yaml') up -d --no-deps --pull never session-e
 if($LASTEXITCODE -ne 0){throw 'Unused E GPU experiment startup failed.'}
} finally {foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}}
Write-Output 'Only E recreated. Allow desktop initialization, then probe with docker --context desktop-linux exec --user ubuntu --env DISPLAY=:20 digitalheat-cs16-session-e-1 timeout 15 glxinfo -B. Do not infer 32-bit Wine, encoder, FPS, free capacity or heat from this result.'
