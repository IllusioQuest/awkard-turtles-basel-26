$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
$policy=Get-Content (Join-Path $lab 'deploy/cs16-lab.json') -Raw | ConvertFrom-Json
foreach($service in (@($policy.nodes|ForEach-Object{$_.id})+@('browser-relay'))){
  $ids=@(& docker --context desktop-linux ps -q --filter "label=com.docker.compose.project=$($policy.compose_project)" --filter "label=com.docker.compose.service=$service")
  if($LASTEXITCODE -ne 0 -or $ids.Count -gt 1){throw 'CS node scope is ambiguous.'}
  foreach($id in $ids){
    if($id -notmatch '^[a-f0-9]{12,64}$'){throw 'Invalid CS node ID.'}
    & docker --context desktop-linux stop $id
    if($LASTEXITCODE -ne 0){throw 'CS node stop failed.'}
  }
}
Write-Output 'Only the CS compatibility nodes stopped. Volumes and all other games retained.'
