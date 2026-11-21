$ErrorActionPreference='Stop'
$request=Join-Path $PSScriptRoot 'Invoke-MeshRequest.ps1'
$container='digitalheat-mesh-spa-1'
$before=& $request -Peer spa
& $request -Peer home -Path heat -Json '{"demand_kwh":0,"accepting_work":false}' | Out-Null
& $request -Peer hotel -Path heat -Json '{"demand_kwh":0,"accepting_work":false}' | Out-Null
& docker.exe --context desktop-linux exec $container sh -c 'rm -f /var/lib/mesh/state/created-before-ack'
& $request -Peer spa -Path heat -Json '{"demand_kwh":20,"accepting_work":true}' | Out-Null
& docker.exe --context desktop-linux exec $container touch /var/lib/mesh/state/inject-after-create
if ($LASTEXITCODE -ne 0) { throw 'Cannot enable scoped create fault' }
$id='recovery-'+[guid]::NewGuid().ToString('N').Substring(0,8)
$job=(& $request -Peer home -Path jobs -Json (@{job_id=$id}|ConvertTo-Json -Compress)).job_id
$created=$null
$watch=[Diagnostics.Stopwatch]::StartNew()
while ($watch.Elapsed.TotalSeconds -lt 60) {
    $created=& docker.exe --context desktop-linux exec $container sh -c 'test ! -f /var/lib/mesh/state/created-before-ack || cat /var/lib/mesh/state/created-before-ack'
    if ($created) { break }
    Start-Sleep -Milliseconds 500
}
if (!$created) { throw 'Create window not reached' }
& docker.exe --context desktop-linux exec $container rm /var/lib/mesh/state/inject-after-create
if ($LASTEXITCODE -ne 0) { throw 'Cannot disable scoped create fault' }
& docker.exe --context desktop-linux restart --time 1 $container | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Target restart failed' }
$row=$null
for ($i=0;$i -lt 60;$i++) {
    try {
        $state=& $request -Peer spa
        $row=$state.attempts|Where-Object {$_.attempt.request.id -eq $job}
        if ($row.phase -in @('running','released')) { break }
    } catch { Start-Sleep -Milliseconds 500 }
    Start-Sleep -Milliseconds 500
}
if (!$row -or $row.phase -notin @('running','released') -or $state.engine_id -ne $before.engine_id) { throw 'Target did not recover its original engine/attempt' }
$ids=@(& docker.exe --context desktop-linux exec $container docker ps -a -q --no-trunc --filter "label=digitalheat.workload=$job")
if ($ids.Count -ne 1 -or $ids[0] -ne $created) { throw 'Target recovery replaced or duplicated the created container' }
$report=[ordered]@{source='Real create-before-ack interruption on private Linux peer; synthetic heat';tested_at_utc=[DateTimeOffset]::UtcNow.ToString('o');job_id=$job;engine_id=$state.engine_id;attempt_id=$row.attempt.attempt_id;original_created_container_id=$created;recovered_container_id=$ids[0];phase=$row.phase;unique_container=$true}
$report|ConvertTo-Json -Depth 6|Set-Content (Join-Path $PSScriptRoot 'TestResults/create-recovery.json')
$report|ConvertTo-Json -Depth 6
& $request -Peer home -Path heat -Json '{"demand_kwh":1,"accepting_work":true}' | Out-Null
& $request -Peer hotel -Path heat -Json '{"demand_kwh":8,"accepting_work":true}' | Out-Null
& $request -Peer spa -Path heat -Json '{"demand_kwh":6,"accepting_work":true}' | Out-Null
