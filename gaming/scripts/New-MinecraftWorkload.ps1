param(
    [Parameter(Mandatory=$true)][string]$BaseConfig,
    [Parameter(Mandatory=$true)][string]$NodeId,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$UsefulHeatEvidence,
    [string]$PowerMeasurement,
    [string]$ReviewedRecoveryModel,
    [switch]$AcceptEula
)
$ErrorActionPreference='Stop'
if(!$AcceptEula){throw 'Explicit -AcceptEula is required before exporting a startup profile'}
. "$PSScriptRoot/Minecraft.Common.ps1"
. "$PSScriptRoot/Minecraft.Power.ps1"
$script:mc=Get-MinecraftLab
$build=Get-Content (Join-Path $mc.Results 'build.json') -Raw | ConvertFrom-Json
$heat=$null;$source=$null
if($UsefulHeatEvidence){
    $evidence=Get-Content -LiteralPath $UsefulHeatEvidence -Raw | ConvertFrom-Json
    if($evidence.kind -ne 'measured-useful-heat' -or $evidence.useful_heat_verified -ne $true -or $evidence.image_id -ne $build.image_id -or
        $evidence.config_fingerprint -ne $mc.Fingerprint -or [string]::IsNullOrWhiteSpace($evidence.source)){
        throw 'Matching verified useful-heat evidence is required; synthetic/case heat is not accepted'
    }
    $heat=[double]$evidence.recoverable_heat_kwh_per_hour;$source=[string]$evidence.source
} elseif($PowerMeasurement -and $ReviewedRecoveryModel){
    $engineId=Invoke-MinecraftDocker -DockerArguments @('info','--format','{{.ID}}')
    $power=Get-ValidatedMinecraftElectricalEvidence -Path $PowerMeasurement -ImageId $build.image_id -Fingerprint $mc.Fingerprint -Version $mc.Config.version -Context $mc.Context -EngineId $engineId -Policy $mc.Config.measurement
    $model=Get-Content -LiteralPath $ReviewedRecoveryModel -Raw | ConvertFrom-Json
    if($model.approved_by_operator -ne $true -or [string]::IsNullOrWhiteSpace($model.review_reference) -or [string]::IsNullOrWhiteSpace($model.source) -or
        $null -eq $model.recovery_fraction -or [double]$model.recovery_fraction -le 0 -or [double]$model.recovery_fraction -gt 1){
        throw 'A separately approved, sourced recovery model is required; there is no default recovery fraction'
    }
    $heat=[double]$power.incremental_electric_w/1000*[double]$model.recovery_fraction
    $source="Measured whole-laptop idle electrical delta: $PowerMeasurement; modeled useful heat: $($model.source); review $($model.review_reference). Not directly measured water heat."
} else {throw 'Heat-based startup blocked: provide verified useful heat, or accepted electrical measurement AND a separately reviewed recovery model'}
if([double]::IsNaN($heat) -or [double]::IsInfinity($heat) -or $heat -le 0 -or $source -match 'TODO\(source\)'){throw 'Invalid/unsourced heat evidence'}
if(Test-Path -LiteralPath $OutputPath){throw 'Output already exists; choose a new file'}
$container=Get-MinecraftContainer
if($container -and $container.State.Running){throw 'Stop the calibration server cleanly before binding its world to a scheduled workload'}
& "$PSScriptRoot/Test-MinecraftWorldSave.ps1" | Out-Null
$config=Get-Content -LiteralPath $BaseConfig -Raw | ConvertFrom-Json
$node=@($config.inventory.nodes | Where-Object {$_.id -eq $NodeId})
$runtime=@($config.runtime_nodes | Where-Object {$_.node_id -eq $NodeId})
if($node.Count -ne 1 -or $runtime.Count -ne 1 -or $runtime[0].docker_context -ne $mc.Context){throw 'This first integration requires one explicit local Docker-context node'}
# Never refresh timestamps or reduce external allocations to make a stale/unfit inventory pass.
$node=$node[0];$runtime=$runtime[0];$storage='minecraft-world'
$capability='minecraft-java-'+$mc.Config.java_major
$node.capabilities=@($node.capabilities+$capability | Select-Object -Unique)
$node.storage_ids=@($node.storage_ids+$storage | Select-Object -Unique)
if(@($runtime.volumes | Where-Object {$_.storage_id -eq $storage}).Count){throw 'Storage ID already mapped; review its binding explicitly'}
$runtime.volumes=@($runtime.volumes)+@(@{storage_id=$storage;volume_name=$mc.Config.lab.volume_name;container_path='/data'})
$runtime.network='bridge'
$envPath=Join-Path $mc.Results 'scheduled.env'
[IO.File]::WriteAllText($envPath,"MC_EULA_ACCEPTED=true`nMC_HEAP_MIN_MIB=$($mc.Config.lab.heap_min_mib)`nMC_HEAP_MAX_MIB=$($mc.Config.lab.heap_max_mib)`n",[Text.UTF8Encoding]::new($false))
$id='minecraft-'+[Guid]::NewGuid().ToString('N')
$request=@{id=$id;kind='interactive';origin_site_id=$node.site_id;placement_policy=@{mode='origin-site';source='Owner-authorized local persistent-world lab';allowed_country_codes=$null};
    resources=@{cpu_cores=$mc.Config.lab.cpu_cores;ram_mib=$mc.Config.lab.ram_mib;slots=1};resource_source=$mc.Config.lab.source;
    recoverable_heat_kwh_per_hour=$heat;heat_source=$source;required_capabilities=@($capability);gpu_capability=$null;pinned_node_id=$NodeId;storage_id=$storage;latency_limits=@();
    lifecycle=@{allow_pause=$false;allow_stop=$true;restartable=$false}}
$profile=@{id='minecraft-'+$mc.Config.version;image=$build.image_id;user=[string]$mc.Config.lab.user_id;command=@();env_file=$envPath;
    loopback_ports=@("127.0.0.1:$($mc.Config.lab.server_port):$($mc.Config.lab.server_port)");startup_command=@();
    ready_command=@('java','-cp','/opt/probe','MinecraftStatus','--ready','127.0.0.1',[string]$mc.Config.lab.server_port);shm_mib=64}
$config.workloads=@($config.workloads)+@(@{request=$request;profile=$profile})
$config | ConvertTo-Json -Depth 40 | Set-Content -Encoding UTF8 $OutputPath
Write-Output "Exported $id pinned to $NodeId. Use the universal WorkloadRun CLI; it still enforces fresh heat/resources. Use Minecraft save verification before cold backups."
