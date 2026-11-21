param([switch]$AcceptEula)
$ErrorActionPreference='Stop'
if(!$AcceptEula){throw 'Explicit -AcceptEula is required for the original/restored server tests'}
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab
$build=Get-Content (Join-Path $mc.Results 'build.json') -Raw | ConvertFrom-Json
$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')
$token=[Guid]::NewGuid().ToString('N')
$backup='digitalheat-minecraft-backup-'+$stamp
$restored='digitalheat-minecraft-restore-'+$token
$restoreContainer='digitalheat-minecraft-restore-'+$token
$archive='world.tar.gz'
$context=$mc.Context
$originalVolume=$mc.Config.lab.volume_name
function Data-Command([string]$Volume,[string]$Command,[string]$Argument){
    $parameters=@('run','--rm','--pull','never','--network','none','--user',([string]$mc.Config.lab.user_id),'--cap-drop','ALL','--security-opt','no-new-privileges:true',
        '--cpus','0.25','--memory','64m','--read-only','--mount',"type=volume,source=$Volume,target=/data",'--entrypoint','sh',$build.image_id,'-c',$Command,'sh')
    if($Argument){$parameters+=$Argument}
    return Invoke-MinecraftDocker -DockerArguments $parameters
}
& "$PSScriptRoot/Stop-MinecraftCalibration.ps1" | Out-Host
& "$PSScriptRoot/Test-MinecraftWorldSave.ps1" | Out-Host
$marker=Data-Command $originalVolume 'test -s /data/world/level.dat || exit 1; if ! test -f /data/world/.digitalheat-persistence-marker; then printf %s "$1" > /data/world/.digitalheat-persistence-marker; fi; cat /data/world/.digitalheat-persistence-marker' $token
& "$PSScriptRoot/Start-MinecraftCalibration.ps1" -AcceptEula -CalibrationOnly | Out-Host
& "$PSScriptRoot/Stop-MinecraftCalibration.ps1" | Out-Host
$after=Data-Command $originalVolume 'cat /data/world/.digitalheat-persistence-marker' $null
if($after -ne $marker){throw 'Same-volume restart lost the persistence marker'}
& "$PSScriptRoot/Test-MinecraftWorldSave.ps1" | Out-Host
foreach($newVolume in @($backup,$restored)){
    $existing=Invoke-MinecraftDocker -DockerArguments @('volume','ls','-q','--filter',"name=^$newVolume`$")
    if($existing.Trim()){throw 'Backup/restore target already exists; no overwrite permitted'}
}
Invoke-MinecraftDocker -DockerArguments @('volume','create','--label',"digitalheat.minecraft.backup=$($mc.Config.version)",$backup) | Out-Null
Invoke-MinecraftDocker -DockerArguments @('volume','create','--label',"digitalheat.minecraft.world=$($mc.Config.version)",$restored) | Out-Null
$common=@('run','--rm','--pull','never','--network','none','--read-only','--user','0','--cap-drop','ALL','--cap-add','DAC_OVERRIDE','--security-opt','no-new-privileges:true','--cpus','0.25','--memory','128m')
Invoke-MinecraftDocker -DockerArguments ($common+@('--mount',"type=volume,source=$originalVolume,target=/data,readonly",'--mount',"type=volume,source=$backup,target=/backup",'--entrypoint','tar',$build.image_id,'-czf',"/backup/$archive",'-C','/data','.')) | Out-Null
Invoke-MinecraftDocker -DockerArguments ($common+@('--cap-add','CHOWN','--cap-add','FOWNER','--mount',"type=volume,source=$restored,target=/data",'--mount',"type=volume,source=$backup,target=/backup,readonly",'--entrypoint','tar',$build.image_id,'-xzf',"/backup/$archive",'-C','/data')) | Out-Null
$restoredMarker=Data-Command $restored 'test -s /data/world/level.dat || exit 1; cat /data/world/.digitalheat-persistence-marker' $null
if($restoredMarker -ne $marker){throw 'Cold restore lost world data or ownership'}
& "$PSScriptRoot/Start-MinecraftCalibration.ps1" -AcceptEula -CalibrationOnly -ContainerName $restoreContainer -VolumeName $restored -HostPort $mc.Config.lab.restore_host_port | Out-Host
& "$PSScriptRoot/Stop-MinecraftCalibration.ps1" -ContainerName $restoreContainer -VolumeName $restored -HostPort $mc.Config.lab.restore_host_port | Out-Host
& "$PSScriptRoot/Test-MinecraftWorldSave.ps1" -VolumeName $restored | Out-Host
[ordered]@{passed=$true;version=$mc.Config.version;image_id=$build.image_id;original_volume=$originalVolume;backup_volume=$backup;archive=$archive;restored_volume=$restored;
    restored_container=$restoreContainer;marker=$marker;note='Actual server/world-file restart and isolated cold restore; not authenticated player gameplay';observed_utc=[DateTimeOffset]::UtcNow.ToString('o')} |
    ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $mc.Results 'persistence.json')
& "$PSScriptRoot/Start-MinecraftCalibration.ps1" -AcceptEula -CalibrationOnly | Out-Host
Write-Output 'PASS: same-world restart and isolated restored-server readiness/save. Original, backup and restored volumes retained; restored server stopped.'
