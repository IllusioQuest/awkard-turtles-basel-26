param([string]$Image='alpine:3.22')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$context=(& docker context show | Out-String).Trim()
$imageId=(& docker --context $context image inspect $Image --format '{{.Id}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $imageId -notmatch '^sha256:[a-f0-9]{64}$'){throw 'A pre-existing local Alpine image is required; no implicit download'}
$fleet='deadline-smoke-'+[Guid]::NewGuid().ToString('N')
$dir=Join-Path $root ('gaming/TestResults/'+$fleet)
New-Item -ItemType Directory -Path $dir -Force|Out-Null
$state=Join-Path $dir 'state';$path=Join-Path $dir 'config.json'
$now=[DateTimeOffset]::UtcNow
$hour=[DateTimeOffset]::new($now.Ticks-$now.Ticks%[TimeSpan]::TicksPerHour,[TimeSpan]::Zero)
$budget=@{cpu_cores=0.1;ram_mib=32;slots=1}
$request=@{id='bounded-smoke';kind='batch';origin_site_id='lab';placement_policy=@{mode='origin-site';source='Synthetic lab policy';allowed_country_codes=$null};
    resources=$budget;resource_source='Synthetic isolated smoke budget';recoverable_heat_kwh_per_hour=0.01;heat_source='Synthetic smoke-only signal, not measured heat';
    required_capabilities=@();gpu_capability=$null;pinned_node_id=$null;storage_id=$null;latency_limits=@();
    lifecycle=@{allow_pause=$true;allow_stop=$true;restartable=$false};
    deadline=@{submitted_at_utc=$now.ToString('o');due_at_utc=$now.AddSeconds(15).ToString('o');source='Shortened 15-second integration-test deadline, not the user service profiles';stop_when_overdue=$true}}
$config=@{fleet_id=$fleet;inventory=@{revision=1;interval_start_utc=$hour.ToString('o');interval_end_utc=$hour.AddHours(1).ToString('o');
    source='Synthetic smoke heat/resource inventory';resource_observed_at_utc=$now.ToString('o');resource_valid_until_utc=$now.AddMinutes(1).ToString('o');
    sites=@(@{id='lab';country_code='CH';heat=@{demand_kwh=1;tank_stored_kwh=0;tank_capacity_kwh=0;server_count=1;recoverable_kwh_per_server_at_full_load=1};external_committed_heat_kwh=0});
    domains=@(@{id='smoke';capacity=$budget;gpus=@()});nodes=@(@{id='smoke-node';site_id='lab';domain_id='smoke';ready=$true;network_eligible=$true;capacity=$budget;capabilities=@();gpu_device_ids=@();storage_ids=@();latencies=@()});external_allocations=@()};
    runtime_nodes=@(@{node_id='smoke-node';docker_context=$context;network='none';gpus=@();volumes=@()});
    workloads=@(@{request=$request;profile=@{id='deadline-smoke';image=$imageId;user='65534';shm_mib=16;
        command=@('sh','-c','trap "exit 0" TERM INT; while :; do sleep 1 & wait $!; done');env_file=$null;loopback_ports=@();startup_command=@();ready_command=@()}})}
$config|ConvertTo-Json -Depth 25|Set-Content -Encoding UTF8 $path
try {
    & dotnet run --no-build --project (Join-Path $root 'src/DigitalHeat.WorkloadRun') -c Release -- watch-deadlines $path $state
    if($LASTEXITCODE -ne 4){throw "Expected deadline-missed exit 4, got $LASTEXITCODE"}
    $journal=Get-Content (Join-Path $state 'journal.json') -Raw|ConvertFrom-Json
    $attempt=@($journal.attempts)[0]
    if($attempt.phase -ne 'released' -or $attempt.deadline.status -ne 'missed' -or !$attempt.deadline.stop_intent_persisted){throw 'Deadline cleanup proof failed'}
    $remaining=@(& docker --context $context ps -aq --filter "label=digitalheat.fleet=$fleet")
    if($remaining.Count){throw 'Owned smoke container still present'}
    @{passed=$true;fleet=$fleet;deadline_status=$attempt.deadline.status;phase=$attempt.phase;image=$imageId;note='Only random smoke fleet; existing games untouched'}|
        ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $dir 'result.json')
    Write-Output "PASS: live owner recorded overdue status/intent and confirmed stop/removal/release. Evidence: $dir"
} finally {
    foreach($id in @(& docker --context $context ps -aq --filter "label=digitalheat.fleet=$fleet")){
        if($id -notmatch '^[a-f0-9]{12,64}$'){throw 'Unsafe cleanup identity'}
        $owner=(& docker --context $context inspect --format '{{index .Config.Labels "digitalheat.fleet"}}' $id|Out-String).Trim()
        if($owner -ne $fleet){throw 'Smoke ownership mismatch'}
        & docker --context $context rm -f $id|Out-Null
    }
}
