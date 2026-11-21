param([switch]$Apply)
$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
$policy=Get-Content (Join-Path $lab 'deploy/cs16-lab.json') -Raw | ConvertFrom-Json
$ids=@(& docker --context $policy.docker_context ps -q --filter "label=com.docker.compose.project=$($policy.compose_project)" --filter 'label=com.docker.compose.service=session-e')
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1){throw 'Expected one running unused E node.'}
$processes=(& docker --context $policy.docker_context exec $ids[0] ps -eo comm | Out-String)
if($LASTEXITCODE -ne 0){throw 'Cannot verify E processes.'}
if($processes -match '(?im)^.*(?:\.exe|wine|wineserver).*$'){throw 'E has a Windows/Wine process; no restart authorized by this experiment.'}
if(!$Apply){Write-Output 'Plan: recreate only unused session-e with Intel WSL device and read-only driver libraries; retain home/limits/network. D, relay and Minecraft are untouched. Run with -Apply to execute.'; return}
$image=(& docker --context $policy.docker_context inspect $ids[0] --format '{{.Image}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $image -notmatch '^sha256:[a-f0-9]{64}$'){throw 'Cannot identify existing E image.'}
$values=@{CS16_IMAGE=$image;CS16_PORT_D=$policy.nodes[0].browser_port;CS16_PORT_E=$policy.nodes[1].browser_port;
 CS16_CPUS=$policy.cpu_limit_per_node;CS16_MEMORY_MIB=$policy.memory_limit_mib_per_node;CS16_PIDS=$policy.pids_limit_per_node;
 CS16_RELAY_IMAGE=$policy.relay_image;CS16_RELAY_CPUS=$policy.relay_cpu_limit;CS16_RELAY_MEMORY_MIB=$policy.relay_memory_limit_mib;
 CS16_RELAY_PIDS=$policy.relay_pids_limit;CS16_RELAY_ROUTES='[]'}
$previous=@{}
try {
 foreach($key in $values.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,[Convert]::ToString($values[$key],[Globalization.CultureInfo]::InvariantCulture),'Process')}
 & docker --context $policy.docker_context compose -f (Join-Path $lab 'deploy/cs16.compose.yaml') -f (Join-Path $lab 'deploy/cs16-intel-e.override.yaml') up -d --no-deps session-e
 if($LASTEXITCODE -ne 0){throw 'Unused E GPU experiment startup failed.'}
} finally {foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}}
Write-Output 'Only E recreated. Allow desktop initialization, then probe with docker --context desktop-linux exec --user ubuntu --env DISPLAY=:20 digitalheat-cs16-session-e-1 timeout 15 glxinfo -B. Do not infer 32-bit Wine, encoder, FPS, free capacity or heat from this result.'
