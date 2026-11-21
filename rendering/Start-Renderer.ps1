param([switch]$SkipBuild,[string]$RendererImage='digitalheat-landscape:lab',[string]$GpuConfiguration)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$local=Join-Path $PSScriptRoot 'TestResults'
New-Item -ItemType Directory -Force $local | Out-Null
function D { param([Parameter(ValueFromRemainingArguments=$true)][string[]]$A)
 & docker.exe --context desktop-linux @A
 if($LASTEXITCODE -ne 0){throw "Docker failed: $($A[0])"}
}
Push-Location $root
try{
 if(!$SkipBuild){
  if($RendererImage -eq 'digitalheat-landscape:lab'){ D @('build','-t',$RendererImage,"$PSScriptRoot") }
  dotnet publish src/DigitalHeat.WorkloadRun -c Release -r linux-musl-x64 --self-contained true -p:InvariantGlobalization=true -o "$local/publish" --nologo -v:q
  if($LASTEXITCODE -ne 0){throw 'Publish failed'}
  Copy-Item "$PSScriptRoot/start-node.sh" "$local/publish/start-node.sh"
  Copy-Item "$PSScriptRoot/render.html" "$local/publish/render.html"
  D @('build','-t','digitalheat-render-node:lab','-f',"$root/mesh/node/Dockerfile","$local/publish")
 }
 D @('image','save','-o',"$local/render.tar",$RendererImage)
 $manifest=(& tar -xOf "$local/render.tar" manifest.json | ConvertFrom-Json)[0]
 $image='sha256:'+(($manifest.Config -split '/')[-1] -replace '\.json$','')
 $tokenPath=Join-Path $local 'token.txt'
 if(!(Test-Path $tokenPath)){
  $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create()
  try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
  [IO.File]::WriteAllText($tokenPath,[BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant())
 }
 $env:MESH_TOKEN=[IO.File]::ReadAllText($tokenPath);$env:MESH_JOB_IMAGE=$image
 $peers=@{home='http://home:8080';hotel='http://hotel:8080';spa='http://spa:8080'}
 $gpuNodes=if($GpuConfiguration){Get-Content $GpuConfiguration -Raw|ConvertFrom-Json}else{$null}
 foreach($id in @('home','hotel','spa')){
  $cfg=[ordered]@{id=$id;image=$image;state_directory='/var/lib/mesh/render-overlay';physical_host_id='shared-windows-laptop';peers=$peers;heat_demand_kwh=@{home=100;hotel=300;spa=200}[$id];cpu_cores=1;ram_mib=1536;frame_heat_kwh=0.8;frames=48;deadline_seconds=7200;source='Rendering benchmark fixture: synthetic 100/300/200 kWh headroom prevents 96 conservative per-frame hourly claims exhausting the lab. NOT a building heat measurement. Case abstract 0.8kWh/server-hour per frame reservation. 1 CPU/1536MiB per frame; three 1.25 CPU/2304MiB outer slices share ONE laptop. 7200s explicit lab expiry, not SLA. 48 frames 640x360 32 samples: user approved 2026-11-21.'}
  if($gpuNodes -and $gpuNodes.$id){$cfg.gpu=$gpuNodes.$id.gpu;$cfg.gpu_backend=$gpuNodes.$id.gpu_backend}
  [IO.File]::WriteAllText("$local/$id.json",($cfg|ConvertTo-Json -Depth 8))
 }
 D @('compose','-p','digitalheat-render','-f',"$PSScriptRoot/compose.yaml",'up','-d')
 $gpuNodes=if($GpuConfiguration){Get-Content $GpuConfiguration -Raw|ConvertFrom-Json}else{$null}
 foreach($id in @('home','hotel','spa')){
  $container="digitalheat-render-$id-1";$ready=$false
  for($i=0;$i -lt 60;$i++){
   & docker.exe --context desktop-linux exec $container docker info *> $null
   if($LASTEXITCODE -eq 0){$ready=$true;break};Start-Sleep -Seconds 1
  }
  if(!$ready){throw "Engine unavailable: $id"}
  & docker.exe --context desktop-linux exec $container docker image inspect $image *> $null
  if($LASTEXITCODE -eq 0){continue}
  D @('cp',"$local/render.tar",($container+':/var/lib/mesh/render.tar'))
  D @('exec',$container,'docker','load','-i','/var/lib/mesh/render.tar')
  D @('exec',$container,'rm','/var/lib/mesh/render.tar')
 }
 Write-Host 'Open http://localhost:18900/ui after device probing. Run comparison starts 48 + 48 real frames.'
}finally{Pop-Location;Remove-Item Env:MESH_TOKEN,Env:MESH_JOB_IMAGE -ErrorAction SilentlyContinue}


