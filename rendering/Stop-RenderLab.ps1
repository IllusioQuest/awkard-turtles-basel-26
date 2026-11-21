param([Parameter(Mandatory=$true)][string]$LabDirectory)
$ErrorActionPreference='Stop'
$base=(Resolve-Path "$PSScriptRoot/TestResults").Path
$lab=(Resolve-Path -LiteralPath $LabDirectory).Path
if(!$lab.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Lab must be inside rendering/TestResults'}
$configs=@(Get-ChildItem -LiteralPath $lab -Directory|ForEach-Object {$path=Join-Path $_.FullName 'config.json';if(Test-Path -LiteralPath $path){Get-Content -LiteralPath $path -Raw|ConvertFrom-Json}})
$ui=@($configs|Where-Object serve_ui|Select-Object -First 1)
if($ui.Count){
 $url=$ui[0].peers.($ui[0].id)
 try{$status=Invoke-RestMethod "$url/ui/status" -TimeoutSec 3}catch{$status=$null}
 if($status){
  if(@($status.peers|ForEach-Object attempts|Where-Object phase -ne released).Count -or (!$status.held -and $status.phase -notin @('idle','complete','cancelled'))){
   Invoke-WebRequest "$url/ui/pause" -Method Post -Headers @{'X-Render-Request'='local'}|Out-Null
   $until=[DateTimeOffset]::UtcNow.AddMinutes(3)
   do{$status=Invoke-RestMethod "$url/ui/status";if($status.held -and !$status.pause_pending){break};if([DateTimeOffset]::UtcNow -ge $until){throw 'Render did not drain; no processes stopped'};Start-Sleep -Milliseconds 250}while($true)
  }
 }
}
$recordFile=Join-Path $lab 'processes.json'
if(Test-Path -LiteralPath $recordFile){
 foreach($record in @(Get-Content -LiteralPath $recordFile -Raw|ConvertFrom-Json)){
  $process=Get-Process -Id $record.id -ErrorAction SilentlyContinue
  if($process -and $process.StartTime.ToUniversalTime() -eq ([DateTimeOffset]$record.started_utc).UtcDateTime -and (Split-Path $process.Path -Leaf) -eq 'dotnet.exe'){
   $process.Kill();$process.WaitForExit(5000)|Out-Null;Write-Host "Stopped recorded renderer host $($record.node)"
  }
 }
}
foreach($cfg in $configs){
 $uri=[Uri]$cfg.peers.($cfg.id)
 $connections=@(Get-NetTCPConnection -State Listen -LocalPort $uri.Port -ErrorAction SilentlyContinue)
 foreach($connection in $connections){
  $process=Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue
  $details=Get-CimInstance Win32_Process -Filter "ProcessId=$($connection.OwningProcess)"
  if($process -and $details.CommandLine -and $details.CommandLine.Replace('/','\').Contains($lab.Replace('/','\'),[StringComparison]::OrdinalIgnoreCase) -and $details.CommandLine.Contains('render-node')){$process.Kill();$process.WaitForExit(5000)|Out-Null}
 }
 $pool=Join-Path $cfg.state_directory 'native-warm-worker/owned.json'
 if(Test-Path -LiteralPath $pool){
  $owned=Get-Content -LiteralPath $pool -Raw|ConvertFrom-Json
  if(!$owned.active_activation -and $owned.process.pid){
   $process=Get-Process -Id $owned.process.pid -ErrorAction SilentlyContinue
   if($process -and $process.StartTime.ToUniversalTime() -eq ([DateTimeOffset]$owned.process.started_utc).UtcDateTime -and [string]::Equals($process.Path,$cfg.native.executable,[StringComparison]::OrdinalIgnoreCase)){$process.Kill();$process.WaitForExit(5000)|Out-Null}
  }
 }
}
Write-Host "Stopped only matching render processes; retained all files: $lab"
