param([string]$BlenderExecutable,[int]$Port=18910,[ValidateRange(1,48)][int]$Frames=48,[string]$ExpectedBlenderVersion='5.3.0',[ValidateSet('opengl','vulkan')][string]$GraphicsApi='opengl')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$BlenderExecutable){$BlenderExecutable=(Get-ChildItem "$PSScriptRoot/TestResults/native-blender/*/blender.exe" | Select-Object -First 1).FullName}
if(!$BlenderExecutable -or !(Test-Path -LiteralPath $BlenderExecutable)){throw 'Provide -BlenderExecutable pointing to portable Blender 5.3'}
$BlenderExecutable=(Resolve-Path -LiteralPath $BlenderExecutable).Path
foreach($number in @($Port,($Port+1))){if(Get-NetTCPConnection -State Listen -LocalPort $number -ErrorAction SilentlyContinue){throw "Port $number is in use; choose another -Port"}}
$local=Join-Path $PSScriptRoot ('TestResults/native-lab-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$local/app","$local/assets" | Out-Null
dotnet publish "$root/src/DigitalHeat.WorkloadRun" -c Release --no-self-contained -o "$local/app" --nologo -v:q
if($LASTEXITCODE -ne 0){throw 'Native lab publish failed'}
$bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create()
try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
$token=[BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant();[IO.File]::WriteAllText("$local/token.txt",$token)
$peers=@{cpu="http://127.0.0.1:$Port";gpu="http://127.0.0.1:$($Port+1)"}
$oldToken=$env:MESH_TOKEN;$oldUrl=$env:ASPNETCORE_URLS;$owned=@()
try{
 $env:MESH_TOKEN=$token
 foreach($id in @('cpu','gpu')){
  $node=Join-Path $local $id;New-Item -ItemType Directory -Force $node | Out-Null
  $gpu=if($id -eq 'gpu'){@{device_id='local-physical-gpu';nvidia_device_id=$null;linux_devices=@();read_only_binds=@()}}else{$null}
  $cfg=@{id=$id;image='native-recipe-computed-at-startup';state_directory=$node;physical_host_id='shared-local-test-host';peers=$peers;heat_demand_kwh=$(if($id -eq 'cpu'){300}else{200});cpu_cores=1;ram_mib=1536;frame_heat_kwh=0.8;frames=$Frames;deadline_seconds=7200;prepared_scene_mode=$true;serve_ui=($id -eq 'cpu');gpu=$gpu;native=@{executable=$BlenderExecutable;scripts_directory=$PSScriptRoot;assets_directory="$local/assets";graphics_api=$GraphicsApi;expected_version=$ExpectedBlenderVersion;threads=1;enable_gpu=($id -eq 'gpu')};source='T-66 native local fixture: synthetic 300/200kWh headroom and abstract case 0.8kWh/server-hour reservation per job, NOT measured heat. Two logical peers share one physical laptop/GPU. Native 1-core/1536MiB requests are admission estimates, not OS quotas. 7200s expiry is a lab choice, not SLA.'}
  [IO.File]::WriteAllText("$node/config.json",($cfg|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding $false))
  $env:ASPNETCORE_URLS=$peers[$id]
  $process=Start-Process -FilePath (Get-Command dotnet).Source -WindowStyle Hidden -PassThru -ArgumentList ('"'+"$local/app/DigitalHeat.WorkloadRun.dll"+'" render-node "'+"$node/config.json"+'"') -RedirectStandardOutput "$node/host.stdout.log" -RedirectStandardError "$node/host.stderr.log"
  $owned+=@{id=$process.Id;started_utc=$process.StartTime.ToUniversalTime().ToString('o');node=$id}
 }
 [IO.File]::WriteAllText("$local/processes.json",($owned|ConvertTo-Json -Depth 5))
 [IO.File]::WriteAllText("$PSScriptRoot/TestResults/latest-native-lab.txt",$local)
 Write-Host "Native lab starting at $($peers.cpu)/ui. Logs/configuration: $local. GPU verification must finish first."
}finally{$env:MESH_TOKEN=$oldToken;$env:ASPNETCORE_URLS=$oldUrl}
