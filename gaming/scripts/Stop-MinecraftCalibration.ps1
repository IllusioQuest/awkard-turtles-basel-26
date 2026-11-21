param([string]$ContainerName,[string]$VolumeName,[int]$HostPort,[switch]$ConfirmPlayersMayDisconnect)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab -ContainerName $ContainerName -VolumeName $VolumeName -HostPort $HostPort
$container=Get-MinecraftContainer
if(!$container){throw 'No owned Minecraft calibration container exists'}
if($container.State.Running){
    $status=Get-MinecraftStatus
    if($status.players.online -gt 0 -and !$ConfirmPlayersMayDisconnect){throw 'Players online; explicit -ConfirmPlayersMayDisconnect is required'}
    Invoke-MinecraftDocker -DockerArguments @('stop','--time',([string]$mc.Config.lab.stop_timeout_seconds),$container.Id) | Out-Null
}
$container=Get-MinecraftContainer
$logs=Invoke-MinecraftDocker -DockerArguments @('logs','--since',$container.State.StartedAt,$container.Id)
$shutdown=$logs.Substring([Math]::Max(0,$logs.LastIndexOf('Stopping server',[StringComparison]::Ordinal)))
$saved=($shutdown -match 'Saving players' -and $shutdown -match 'Saving worlds' -and $shutdown -match 'minecraft:overworld' -and
    $shutdown -match 'minecraft:the_nether' -and $shutdown -match 'minecraft:the_end' -and $shutdown -match 'DIGITALHEAT_JAVA_EXIT=0')
$clean=(!$container.State.Running -and !$container.State.OOMKilled -and $container.State.ExitCode -eq 0 -and $saved)
$report=[ordered]@{container_id=$container.Id;image_id=$container.Image;config_fingerprint=$mc.Fingerprint;volume=$mc.Config.lab.volume_name;
    started_at_utc=$container.State.StartedAt;finished_at_utc=$container.State.FinishedAt;exit_code=$container.State.ExitCode;oom_killed=$container.State.OOMKilled;
    save_completed=$saved;clean_stop=$clean;observed_utc=[DateTimeOffset]::UtcNow.ToString('o')}
$report | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $mc.Results ($mc.Config.lab.container_name+'-stop.json'))
$logs | Set-Content -Encoding UTF8 (Join-Path $mc.Results ($mc.Config.lab.container_name+'-stop.log'))
if(!$clean){throw 'Clean world save NOT proven. Do not claim a verified backup; inspect stop evidence. No container/volume deleted.'}
Write-Output 'Confirmed clean save and non-OOM/non-forced exit. Container and persistent world preserved.'
