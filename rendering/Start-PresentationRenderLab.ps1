param([int]$Port=18950,[ValidateRange(1,48)][int]$Frames=48,[string]$BlenderExecutable,[string]$NodePrefix='presentation-')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$profile=Get-Content "$PSScriptRoot/presentation-profile.json" -Raw|ConvertFrom-Json
if(!$BlenderExecutable){$BlenderExecutable=(Get-ChildItem "$PSScriptRoot/TestResults/native-blender/*/blender.exe"|Select-Object -First 1).FullName}
$BlenderExecutable=(Resolve-Path -LiteralPath $BlenderExecutable).Path
for($i=0;$i -lt $profile.workers.Count;$i++){if(Get-NetTCPConnection -State Listen -LocalPort ($Port+$i) -ErrorAction SilentlyContinue){throw "Port $($Port+$i) is occupied"}}
$local=Join-Path $PSScriptRoot ('TestResults/presentation-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$local/app","$local/assets"|Out-Null
dotnet publish "$root/src/DigitalHeat.WorkloadRun" -c Release --no-self-contained -o "$local/app" --nologo -v:q
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
$token=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
[IO.File]::WriteAllText("$local/token.txt",$token)
$peers=@{};for($i=0;$i -lt $profile.workers.Count;$i++){$peers[$NodePrefix+$profile.workers[$i].id]="http://127.0.0.1:$($Port+$i)"}
$sum=($profile.workers|Measure-Object weight -Sum).Sum;$owned=@()
$priorToken=$env:MESH_TOKEN;$priorUrl=$env:ASPNETCORE_URLS
try{
 $env:MESH_TOKEN=$token
 foreach($worker in $profile.workers){
  $id=$NodePrefix+$worker.id;$node=Join-Path $local $id;New-Item -ItemType Directory -Force $node|Out-Null
  $isGpu=$worker.role -eq 'render'
  $gpu=if($isGpu){@{device_id=$profile.physical_gpu_id;nvidia_device_id=$null;linux_devices=@();read_only_binds=@()}}else{$null}
  $cfg=@{id=$id;work_weight=$worker.weight;worker_role=$worker.role;shared_gpu_group=$profile.shared_gpu_group;shared_gpu_limit=@($profile.workers|Where-Object role -eq render).Count;assemble_video=$true;allow_local_node_shutdown=([bool]$profile.allow_local_node_shutdown -and $worker.role -eq "prepare");image='native-recipe-computed-at-startup';state_directory=$node;physical_host_id=$profile.physical_host_id;peers=$peers;heat_demand_kwh=($profile.total_heat_budget_kwh*$worker.weight/$sum);cpu_cores=$profile.cpu_threads_per_worker;ram_mib=$profile.ram_mib_per_worker;frame_heat_kwh=$profile.frame_heat_kwh;frames=$Frames;deadline_seconds=$profile.deadline_seconds;prepared_scene_mode=$true;serve_ui=($worker.role -eq 'prepare');gpu=$gpu;source=$profile.source;native=@{executable=$BlenderExecutable;scripts_directory=$PSScriptRoot;assets_directory="$local/assets";graphics_api=$profile.graphics_api;expected_version=$profile.expected_blender_version;threads=$profile.cpu_threads_per_worker;enable_gpu=$isGpu;keep_gpu_warm=$isGpu;warm_idle_timeout_seconds=$profile.warm_idle_timeout_seconds;reuse_prepared_scene=[bool]$profile.reuse_prepared_scene};heat_need_per_run=[bool]$profile.gpu_heat_need_per_run}
  [IO.File]::WriteAllText("$node/config.json",($cfg|ConvertTo-Json -Depth 9),(New-Object Text.UTF8Encoding $false))
  $env:ASPNETCORE_URLS=$peers[$id]
  $p=Start-Process dotnet -WindowStyle Hidden -PassThru -ArgumentList ('"'+"$local/app/DigitalHeat.WorkloadRun.dll"+'" render-node "'+"$node/config.json"+'"') -RedirectStandardOutput "$node/host.stdout.log" -RedirectStandardError "$node/host.stderr.log"
  $owned+=@{id=$p.Id;started_utc=$p.StartTime.ToUniversalTime().ToString('o');node=$id};$owned|ConvertTo-Json|Set-Content "$local/processes.json"
  $ready=$false
  for($attempt=0;$attempt -lt 90;$attempt++){
   if($p.HasExited){throw "Worker $id failed startup; see $node/host.stderr.log"}
   try{$status=Invoke-RestMethod ($peers[$id]+'/status') -Headers @{'X-Mesh-Token'=$token} -TimeoutSec 2;if($status.peer_id -eq $id){$ready=$true;break}}catch{}
   Start-Sleep -Seconds 1
  }
  if(!$ready){throw "Worker $id did not become ready"}
  Write-Host "$id ready ($($worker.role))"
 }
 [IO.File]::WriteAllText("$PSScriptRoot/TestResults/latest-presentation-lab.txt",$local)
 Write-Host "Five-worker app: http://127.0.0.1:$Port/ui. State: $local"
}finally{$env:MESH_TOKEN=$priorToken;$env:ASPNETCORE_URLS=$priorUrl}
