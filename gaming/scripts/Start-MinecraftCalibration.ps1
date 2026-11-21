param([switch]$AcceptEula,[switch]$CalibrationOnly,[string]$ContainerName,[string]$VolumeName,[int]$HostPort)
$ErrorActionPreference='Stop'
if(!$AcceptEula){throw 'Explicit -AcceptEula is required after reviewing https://www.minecraft.net/en-us/eula'}
if(!$CalibrationOnly){throw 'Measured useful heat is not established. Only explicit -CalibrationOnly startup is currently available.'}
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab -ContainerName $ContainerName -VolumeName $VolumeName -HostPort $HostPort
$config=$mc.Config
$build=Get-Content (Join-Path $mc.Results 'build.json') -Raw | ConvertFrom-Json
$image=(Invoke-MinecraftDocker -DockerArguments @('image','inspect',$build.image_id) | ConvertFrom-Json)[0]
if($image.Config.Labels.'digitalheat.minecraft.version' -ne $config.version -or $image.Config.Labels.'digitalheat.minecraft.server_sha256' -ne $build.server_sha256){throw 'Built server provenance mismatch'}
$volumeIds=@(& docker --context $mc.Context volume ls -q --filter "name=^$($config.lab.volume_name)`$")
if($LASTEXITCODE -ne 0){throw 'Cannot inspect local world volume'}
if($volumeIds.Count -eq 0){
    Invoke-MinecraftDocker -DockerArguments @('volume','create','--label',"digitalheat.minecraft.world=$($config.version)",$config.lab.volume_name) | Out-Null
    Invoke-MinecraftDocker -DockerArguments @('run','--rm','--pull','never','--network','none','--read-only','--user','0','--cap-drop','ALL','--cap-add','CHOWN','--security-opt','no-new-privileges:true','--entrypoint','chown','--mount',"type=volume,source=$($config.lab.volume_name),target=/data",$build.image_id,"$($config.lab.user_id):$($config.lab.user_id)",'/data') | Out-Null
} else {
    $volume=(Invoke-MinecraftDocker -DockerArguments @('volume','inspect',$config.lab.volume_name) | ConvertFrom-Json)[0]
    if($volume.Labels.'digitalheat.minecraft.world' -ne $config.version){throw 'Existing volume is not this owned/versioned Minecraft lab; no data changed'}
}
$container=Get-MinecraftContainer
if(!$container){
    $parameters=@('create','--pull','never','--name',$config.lab.container_name,'--restart','no','--user',([string]$config.lab.user_id),
        '--cpus',([string]$config.lab.cpu_cores),'--memory',"$($config.lab.ram_mib)m",'--cap-drop','ALL','--security-opt','no-new-privileges:true',
        '--network','bridge','--publish',"127.0.0.1:$($config.lab.calibration_host_port):$($config.lab.server_port)",
        '--label','digitalheat.minecraft.lab=calibration','--label',"digitalheat.minecraft.version=$($config.version)",'--label',"digitalheat.minecraft.config=$($mc.Fingerprint)",
        '--env','MC_EULA_ACCEPTED=true','--env','MC_CALIBRATION_MODE=true','--env',"MC_HEAP_MIN_MIB=$($config.lab.heap_min_mib)",'--env',"MC_HEAP_MAX_MIB=$($config.lab.heap_max_mib)",
        '--mount',"type=volume,source=$($config.lab.volume_name),target=/data",$build.image_id)
    Invoke-MinecraftDocker -DockerArguments $parameters | Out-Null
    $container=Get-MinecraftContainer
}
if($container.Image -ne $build.image_id -or $container.Config.Labels.'digitalheat.minecraft.config' -ne $mc.Fingerprint){throw 'Existing lab uses another image/config; stop and review explicitly before replacement'}
if($container.State.Paused){throw 'Lab container is paused; no implicit unpause is performed'}
if(!$container.State.Running){Invoke-MinecraftDocker -DockerArguments @('start',$container.Id) | Out-Null}
Write-Output 'Starting EULA-approved Minecraft calibration server. This is NOT heat-based placement.'
$deadline=[DateTimeOffset]::UtcNow.AddSeconds($config.lab.startup_timeout_seconds)
$status=$null
while([DateTimeOffset]::UtcNow -lt $deadline){
    try {$status=Get-MinecraftStatus;break} catch {Start-Sleep -Seconds 2}
    $container=Get-MinecraftContainer
    if(!$container.State.Running){throw "Server exited before readiness. Inspect docker logs $($container.Id). World preserved."}
}
if(!$status){throw 'Minecraft startup timed out; inspect the owned container, no data deleted'}
$container=Get-MinecraftContainer
[ordered]@{mode='calibration_only_not_heat_placed';container_id=$container.Id;image_id=$container.Image;version=$config.version;volume=$config.lab.volume_name;
    started_at_utc=$container.State.StartedAt;config_fingerprint=$mc.Fingerprint;status=$status;endpoint="127.0.0.1:$($config.lab.calibration_host_port)"} |
    ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $mc.Results ($config.lab.container_name+'-running.json'))
Write-Output "Ready: Minecraft Java $($config.version), 127.0.0.1:$($config.lab.calibration_host_port). World persists in $($config.lab.volume_name). Heat-based startup remains blocked."
