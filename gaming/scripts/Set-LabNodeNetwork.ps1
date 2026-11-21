param(
  [Parameter(Mandatory=$true)][string]$Node,
  [Parameter(Mandatory=$true)][ValidateSet('online','offline')][string]$Mode,
  [string]$Policy
)
$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
if(!$Policy){$Policy=Join-Path $lab 'deploy/cs16-lab.json'}
$settings=Get-Content -LiteralPath $Policy -Raw | ConvertFrom-Json
if(@($settings.nodes|Where-Object{$_.id -eq $Node}).Count -ne 1){throw 'Unknown configured node.'}
if(!$settings.docker_context -or !$settings.online_network){throw 'Explicit context/network policy required.'}
$scope=@('--context',[string]$settings.docker_context)
$ids=@(& docker @scope ps -q --filter "label=com.docker.compose.project=$($settings.compose_project)" --filter "label=com.docker.compose.service=$Node")
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$'){throw 'Expected exactly one running configured node.'}
$network=(& docker @scope network inspect $settings.online_network | ConvertFrom-Json)[0]
if($LASTEXITCODE -ne 0 -or $network.Internal -or $network.Driver -ne 'bridge'){throw 'Expected the configured normal bridge network.'}
$before=(& docker @scope inspect $ids[0]|ConvertFrom-Json)[0]
if($before.HostConfig.Privileged -or $before.HostConfig.NetworkMode -eq 'host' -or
  @($before.HostConfig.PortBindings.PSObject.Properties).Count -gt 0){throw 'Expected an unprivileged node without published ports.'}
$attached=@($before.NetworkSettings.Networks.PSObject.Properties.Name) -contains $settings.online_network
if($Mode -eq 'online' -and !$attached){
  & docker @scope network connect --gw-priority 1 $settings.online_network $ids[0]
}elseif($Mode -eq 'offline' -and $attached){
  & docker @scope network disconnect $settings.online_network $ids[0]
}
if($LASTEXITCODE -ne 0){throw 'Network transition failed.'}
$after=(& docker @scope inspect $ids[0]|ConvertFrom-Json)[0]
if($after.State.Pid -ne $before.State.Pid -or $after.RestartCount -ne $before.RestartCount){throw 'Node unexpectedly restarted.'}
$nowAttached=@($after.NetworkSettings.Networks.PSObject.Properties.Name) -contains $settings.online_network
if($nowAttached -ne ($Mode -eq 'online')){throw 'Network transition was not confirmed.'}
Write-Output "$Node network mode: $Mode; node process and existing loopback browser access preserved."
if($Mode -eq 'online'){Write-Output 'Normal outbound network access is enabled. No inbound game port is published; this does not prove game-server authentication.'}
