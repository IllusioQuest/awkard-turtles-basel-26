param([string]$VolumeName,[string]$OutputPath)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab -VolumeName $VolumeName
$build=Get-Content (Join-Path $mc.Results 'build.json') -Raw | ConvertFrom-Json
$volume=(Invoke-MinecraftDocker -DockerArguments @('volume','inspect',$mc.Config.lab.volume_name) | ConvertFrom-Json)[0]
if($volume.Labels.'digitalheat.minecraft.world' -ne $mc.Config.version){throw 'Unowned/version-mismatched world volume'}
$writers=Invoke-MinecraftDocker -DockerArguments @('ps','-q','--filter',"volume=$($mc.Config.lab.volume_name)")
if($writers.Trim()){throw 'World has a running/paused container; cold-save verification refused'}
$base=@('run','--rm','--pull','never','--network','none','--read-only','--user',([string]$mc.Config.lab.user_id),'--cap-drop','ALL','--security-opt','no-new-privileges:true',
    '--cpus','0.25','--memory','64m','--mount',"type=volume,source=$($mc.Config.lab.volume_name),target=/data,readonly",'--entrypoint','cat',$build.image_id)
$run=Invoke-MinecraftDocker -DockerArguments ($base+@('/data/.digitalheat/run.json')) | ConvertFrom-Json
$exitProof=Invoke-MinecraftDocker -DockerArguments ($base+@('/data/.digitalheat/exit.json')) | ConvertFrom-Json
$logs=Invoke-MinecraftDocker -DockerArguments ($base+@('/data/logs/latest.log'))
$position=$logs.LastIndexOf('Stopping server',[StringComparison]::Ordinal)
$shutdown=if($position -ge 0){$logs.Substring($position)}else{''}
$valid=($run.boot_id -eq $exitProof.boot_id -and $run.version -eq $mc.Config.version -and $exitProof.java_exit_code -eq 0 -and $exitProof.stop_requested -eq $true -and
    $shutdown -match 'Saving players' -and $shutdown -match 'Saving worlds' -and $shutdown -match 'minecraft:overworld' -and $shutdown -match 'minecraft:the_nether' -and $shutdown -match 'minecraft:the_end')
if(!$valid){throw 'No matching clean application-exit/save evidence; backup remains blocked'}
$proof=[ordered]@{clean_save=$true;boot_id=$run.boot_id;volume=$mc.Config.lab.volume_name;version=$mc.Config.version;image_id=$build.image_id;observed_utc=[DateTimeOffset]::UtcNow.ToString('o')}
if($OutputPath){$proof | ConvertTo-Json | Set-Content -Encoding UTF8 $OutputPath}
$proof | ConvertTo-Json
