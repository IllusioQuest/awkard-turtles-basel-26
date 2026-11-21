$ErrorActionPreference='Stop'
$request=Join-Path $PSScriptRoot 'Invoke-MeshRequest.ps1'
function State([string]$Peer) { & $request -Peer $Peer }
function Heat([string]$Peer,[double]$Kwh) { & $request -Peer $Peer -Path heat -Json (@{demand_kwh=$Kwh;accepting_work=$true}|ConvertTo-Json -Compress) | Out-Null }
function Wait-Attempt([string]$Peer,[string]$Job,[string[]]$Phases) {
    $timeout=if ('released' -in $Phases) { 600 } else { 60 }
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $timeout) {
        $row=(State $Peer).attempts | Where-Object {$_.attempt.request.id -eq $Job}
        if ($row -and $row.phase -in $Phases) { return $row }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $Peer/$Job in $Phases"
}
$before=@(& docker.exe --context desktop-linux ps --format '{{.ID}} {{.Names}}') | Where-Object {$_ -notmatch ' digitalheat-mesh-'}
$states=@('home','hotel','spa')|ForEach-Object {State $_}
if (($states.engine_id | Sort-Object -Unique).Count -ne 3) { throw 'Peers share engine identity' }
$unauthorized=& docker.exe --context desktop-linux exec digitalheat-mesh-home-1 sh -c 'wget -q -O /dev/null http://127.0.0.1:8080/status 2>/dev/null; test $? -ne 0'
if ($LASTEXITCODE -ne 0) { throw 'Unauthenticated status request succeeded' }
Heat hotel 8; Heat spa 6; Heat home 1
$id='verify-'+[guid]::NewGuid().ToString('N').Substring(0,8)
$job=(& $request -Peer home -Path jobs -Json (@{job_id=$id}|ConvertTo-Json -Compress)).job_id
$running=Wait-Attempt hotel $job @('running')
$owned=(& docker.exe --context desktop-linux exec digitalheat-mesh-hotel-1 docker ps -q --filter "label=digitalheat.workload=$job").Trim()
$inspect=((& docker.exe --context desktop-linux exec digitalheat-mesh-hotel-1 docker inspect $owned) -join "`n"|ConvertFrom-Json)[0]
$volume=($inspect.Mounts|Where-Object Destination -eq '/work').Name
if (!$volume -or $inspect.HostConfig.NanoCpus -ne 250000000 -or $inspect.HostConfig.Memory -ne 134217728) { throw 'Unexpected job limits/storage' }
$cpu=& docker.exe --context desktop-linux exec digitalheat-mesh-hotel-1 docker stats --no-stream --format '{{.CPUPerc}}' $owned
Heat hotel 0
$paused=Wait-Attempt hotel $job @('paused')
$pausedDocker=& docker.exe --context desktop-linux exec digitalheat-mesh-hotel-1 docker inspect $owned --format '{{.State.Paused}}'
if ($pausedDocker -ne 'true') { throw 'Actual container not paused' }
# Origin recovery must retain the remote attempt identity, never submit a replacement.
& docker.exe --context desktop-linux restart digitalheat-mesh-home-1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Origin restart failed' }
Heat hotel 8
$ended=Wait-Attempt hotel $job @('released')
if ($ended.deadline.status -ne 'on_time') { throw 'Finite batch did not successfully finish on time' }
$origin=State home
$intent=$origin.jobs|Where-Object {$_.request.id -eq $job}
if ($origin.engine_id -ne $states[0].engine_id -or $intent.target -ne 'hotel' -or $intent.status -ne 'accepted') { throw 'Origin recovery changed engine/target ownership' }
if (@((State hotel).attempts|Where-Object {$_.attempt.request.id -eq $job}).Count -ne 1) { throw 'Duplicate execution attempt' }
$image=$running.attempt.profile.image
$result=& docker.exe --context desktop-linux exec digitalheat-mesh-hotel-1 docker run --rm --network none --mount "type=volume,source=$volume,target=/work,readonly" --entrypoint python $image -c 'import json,pathlib; p=pathlib.Path("/work"); r=list(p.glob("out/pass_*/*.json")); assert len(r)==2; print(json.dumps({"result_files":len(r),"progress":json.loads((p/"progress.json").read_text())}))'
if ($LASTEXITCODE -ne 0) { throw 'Persistent finite results missing' }
$after=@(& docker.exe --context desktop-linux ps --format '{{.ID}} {{.Names}}') | Where-Object {$_ -notmatch ' digitalheat-mesh-'}
if (Compare-Object $before $after) { throw 'Non-mesh container identities changed during test' }
$report=[ordered]@{source='Real Linux nested-Docker lab; synthetic heat/quota signals, not measured recovered heat';tested_at_utc=[DateTimeOffset]::UtcNow.ToString('o');engines=$states|Select-Object peer_id,engine_id;job_id=$job;target='hotel';attempt_id=$running.attempt.attempt_id;container_id=$owned;cpu_sample=$cpu;results_volume=$volume;paused=$true;origin_restarted=$true;completion=$ended.deadline;results=($result|ConvertFrom-Json);other_running_containers_unchanged=$true}
$report|ConvertTo-Json -Depth 12|Set-Content (Join-Path $PSScriptRoot 'TestResults/verification.json')
$report|ConvertTo-Json -Depth 12
