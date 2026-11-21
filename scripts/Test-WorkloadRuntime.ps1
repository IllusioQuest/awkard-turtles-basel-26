param([string]$Image='alpine:3.22')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$fleet='dhw-smoke-'+[Guid]::NewGuid().ToString('N')
$report=Join-Path $root ('gaming/TestResults/'+$fleet)
New-Item -ItemType Directory -Path $report -Force | Out-Null
$configPath=Join-Path $report 'config.json'; $statePath=Join-Path $report 'state'
$dataVolume=$fleet+'-data'; $backupVolume=$fleet+'-backup'
$sha=[Security.Cryptography.SHA256]::Create()
try {$hash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($fleet)))).Replace('-','').ToLowerInvariant()} finally {$sha.Dispose()}
$restoreVolume='dhw-'+$hash.Substring(0,12)+'-restore-smoke'
$context=(& docker context show | Out-String).Trim()
if($LASTEXITCODE -ne 0){throw 'Cannot inspect Docker context'}
$imageId=(& docker --context $context image inspect $Image --format '{{.Id}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $imageId -notmatch '^sha256:[a-f0-9]{64}$'){throw 'A local Alpine-compatible image is required; this script never pulls images.'}
$dll=Join-Path $root 'src/DigitalHeat.WorkloadRun/bin/Release/net8.0/DigitalHeat.WorkloadRun.dll'
& dotnet build (Join-Path $root 'src/DigitalHeat.WorkloadRun') -c Release --nologo -v quiet
if($LASTEXITCODE -ne 0){throw 'Workload CLI build failed'}
function Invoke-Lab([string]$Command,[string]$Id,[string[]]$Extra=@(),[int]$ExpectedExit=0){
    $parameters=@($Command,$configPath,$statePath)
    if($Id){$parameters+=$Id}; $parameters+=$Extra
    $output=& dotnet $dll @parameters | Out-String
    if($LASTEXITCODE -ne $ExpectedExit){throw "Workload $Command failed: $output"}
    return $output
}
function Request([string]$Id,[string]$Kind,[double]$Cpu,[string]$Storage){
    return @{id=$Id;kind=$Kind;origin_site_id='home';placement_policy=@{mode='anywhere';source='Owner-authorized synthetic local smoke';allowed_country_codes=$null};
        resources=@{cpu_cores=$Cpu;ram_mib=64;slots=1};resource_source='Small declared smoke budget, not host inventory or measured power';
        recoverable_heat_kwh_per_hour=0.05;heat_source='Synthetic admission-test value only, not measured';required_capabilities=@('linux');gpu_capability=$null;
        pinned_node_id=if($Storage){'b'}else{$null};storage_id=if($Storage){$Storage}else{$null};latency_limits=@();lifecycle=@{allow_pause=($Kind -eq 'batch');allow_stop=$true;restartable=$false}}
}
function Heat([double]$Need){return @{demand_kwh=$Need;tank_stored_kwh=0;tank_capacity_kwh=0;server_count=1;recoverable_kwh_per_server_at_full_load=0.8}}
$now=[DateTimeOffset]::UtcNow
$hour=[DateTimeOffset]::new(($now.Ticks-$now.Ticks%[TimeSpan]::TicksPerHour),[TimeSpan]::Zero)
$budget=@{cpu_cores=0.5;ram_mib=128;slots=2}
$configuration=@{fleet_id=$fleet;inventory=@{revision=1;interval_start_utc=$hour.ToString('o');interval_end_utc=$hour.AddHours(1).ToString('o');
    source='Synthetic small isolated smoke budget, not physical hardware/heat measurements';resource_observed_at_utc=$now.ToString('o');resource_valid_until_utc=$now.AddMinutes(5).ToString('o');
    sites=@(@{id='home';country_code='CH';heat=(Heat 1);external_committed_heat_kwh=0},@{id='spa';country_code='CH';heat=(Heat 2);external_committed_heat_kwh=0});
    domains=@(@{id='shared-lab-budget';capacity=$budget;gpus=@()});nodes=@(
        @{id='a';site_id='home';domain_id='shared-lab-budget';ready=$true;network_eligible=$true;capacity=$budget;capabilities=@('linux');gpu_device_ids=@();storage_ids=@();latencies=@()},
        @{id='b';site_id='spa';domain_id='shared-lab-budget';ready=$true;network_eligible=$true;capacity=$budget;capabilities=@('linux');gpu_device_ids=@();storage_ids=@('data');latencies=@()});external_allocations=@()};
    runtime_nodes=@(@{node_id='a';docker_context=$context;network='none';gpus=@();volumes=@()},@{node_id='b';docker_context=$context;network='none';gpus=@();volumes=@(@{storage_id='data';volume_name=$dataVolume;container_path='/data'})});
    workloads=@(@{request=(Request 'batch' 'batch' 0.25 'data');profile=@{id='hash-batch';image=$imageId;user='65534';command=@('sh','-c','umask 077; printf digitalheat > /data/value; while :; do head -c 1048576 /dev/zero | sha256sum >/dev/null; done');env_file=$null;loopback_ports=@();startup_command=@();ready_command=@() }},
        @{request=(Request 'interactive-probe' 'interactive' 0.5 $null);profile=@{id='interactive-probe';image=$imageId;user='65534';command=@('sleep','300');env_file=$null;loopback_ports=@();startup_command=@();ready_command=@('true')}})}
function Save-Config { $configuration | ConvertTo-Json -Depth 30 | Set-Content -Encoding UTF8 $configPath }
try {
    & docker --context $context volume create --label "digitalheat.smoke=$fleet" $dataVolume | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Cannot create owned smoke volume'}
    & docker --context $context run --rm --pull never --network none --read-only --user 0 --cap-drop ALL --cap-add CHOWN --security-opt no-new-privileges:true --cpus 0.25 --memory 64m --mount "type=volume,source=$dataVolume,target=/data" $imageId chown 65534:65534 /data
    if($LASTEXITCODE -ne 0){throw 'Cannot initialize smoke volume ownership'}
    Save-Config
    $started=Invoke-Lab 'start' 'batch' | ConvertFrom-Json
    $container=$started.observation.container_id
    if($started.reservation.site_id -ne 'spa' -or !$started.observation.ready){throw 'Heat-based start/readiness failed'}
    $retry=Invoke-Lab 'start' 'batch' | ConvertFrom-Json
    if($retry.observation.container_id -ne $container){throw 'Retry created another container'}
    foreach($site in $configuration.inventory.sites){$site.heat=Heat 0}; Save-Config
    Invoke-Lab 'heat' 'batch' | Out-Null
    if(((& docker --context $context inspect --format '{{.State.Paused}}' $container | Out-String).Trim()) -ne 'true'){throw 'Shared scheduler did not pause the real batch'}
    foreach($site in $configuration.inventory.sites){$site.heat=Heat 2}; Save-Config
    Invoke-Lab 'start' 'interactive-probe' | Out-Null
    Invoke-Lab 'resume' 'batch' @() 3 | Out-Null
    Invoke-Lab 'stop' 'interactive-probe' | Out-Null
    Invoke-Lab 'resume' 'batch' | Out-Null
    Invoke-Lab 'stop' 'batch' | Out-Null
    Invoke-Lab 'backup' 'batch' @('data',$backupVolume,'save.tar.gz',$imageId) | Out-Null
    Invoke-Lab 'restore-empty' 'batch' @('data',$backupVolume,'save.tar.gz',$imageId,$restoreVolume) | Out-Null
    $restored=(& docker --context $context run --rm --pull never --network none --read-only --user 65534 --cap-drop ALL --security-opt no-new-privileges:true --cpus 0.25 --memory 64m --mount "type=volume,source=$restoreVolume,target=/data,readonly" $imageId cat /data/value | Out-String).Trim()
    if($LASTEXITCODE -ne 0 -or $restored -ne 'digitalheat'){throw 'Cold restore content/ownership verification failed'}
    $status=Invoke-Lab 'status' $null | ConvertFrom-Json
    if(@($status | Where-Object {$_.phase -ne 'released'}).Count){throw 'Claims were not released'}
    [ordered]@{passed=$true;observed_utc=[DateTimeOffset]::UtcNow.ToString('o');fleet_id=$fleet;image_id=$imageId;
        checks=@('heat-placement','start-idempotency','shared-scheduler-pause','retained-memory','protected-interactive-capacity','resume','confirmed-release','private-file-cold-backup-restore');
        note='Synthetic heat and resource budget; only this random test fleet mutated'} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $report 'result.json')
    Write-Output "PASS: shared heat scheduler paused/resumed real batch; idempotent start, protected interactive capacity, confirmed release and cold backup/restore. Evidence: $report"
} finally {
    # Exact random fleet only; no game, shared engine, global prune or user data deletion.
    $ids=@(& docker --context $context ps -aq --filter "label=digitalheat.fleet=$fleet")
    foreach($id in $ids){
        if($id -notmatch '^[a-f0-9]{12,64}$'){throw 'Unsafe smoke cleanup identity'}
        $owner=(& docker --context $context inspect --format '{{index .Config.Labels "digitalheat.fleet"}}' $id | Out-String).Trim()
        if($owner -ne $fleet){throw 'Smoke ownership mismatch'}
        & docker --context $context rm -f $id | Out-Null
    }
    foreach($volume in @($dataVolume,$backupVolume,$restoreVolume)){
        $existing=@(& docker --context $context volume ls -q --filter "name=^$volume`$")
        if($existing.Count){& docker --context $context volume rm $volume | Out-Null}
    }
}
