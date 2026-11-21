param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
$policy=Get-Content (Join-Path $lab 'deploy/cs16-lab.json') -Raw | ConvertFrom-Json
$inputFile=(Resolve-Path -LiteralPath $Installer).Path
if((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $policy.installer_sha256){throw 'Installer differs from the reviewed artifact.'}
$image=(& docker --context desktop-linux image inspect $policy.image --format '{{.Id}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $image -notmatch '^sha256:[a-f0-9]{64}$'){throw 'Build Cs16.Wine.Dockerfile first.'}
$relayImage=(& docker --context desktop-linux image inspect $policy.relay_image --format '{{.Id}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $relayImage -notmatch '^sha256:[a-f0-9]{64}$'){throw 'Build TcpRelay.Dockerfile first.'}
$reports=Join-Path $lab 'TestResults'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$values=@{CS16_IMAGE=$image;CS16_PORT_D=[string]$policy.nodes[0].browser_port;CS16_PORT_E=[string]$policy.nodes[1].browser_port;
  CS16_CPUS=[string]$policy.cpu_limit_per_node;CS16_MEMORY_MIB=[string]$policy.memory_limit_mib_per_node;CS16_PIDS=[string]$policy.pids_limit_per_node;
  CS16_RELAY_IMAGE=$relayImage;CS16_RELAY_CPUS=$policy.relay_cpu_limit.ToString([Globalization.CultureInfo]::InvariantCulture);
  CS16_RELAY_MEMORY_MIB=[string]$policy.relay_memory_limit_mib;CS16_RELAY_PIDS=[string]$policy.relay_pids_limit;
  CS16_RELAY_ROUTES=(@($policy.nodes|ForEach-Object{@{listen_port=$_.browser_port;target_host=$_.id;target_port=$policy.desktop_port}})|ConvertTo-Json -Compress)}
$previous=@{}
try{
  foreach($key in $values.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$values[$key],'Process')}
  & docker --context desktop-linux compose -f (Join-Path $lab 'deploy/cs16.compose.yaml') up -d
  if($LASTEXITCODE -ne 0){throw 'CS compatibility node startup failed.'}
  foreach($node in $policy.nodes){
    $ids=@(& docker --context desktop-linux ps -q --filter "label=com.docker.compose.project=$($policy.compose_project)" --filter "label=com.docker.compose.service=$($node.id)")
    if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$'){throw 'Expected exactly one CS node.'}
    & docker --context desktop-linux cp $inputFile "$($ids[0]):/home/ubuntu/cs16-installer.exe"
    if($LASTEXITCODE -ne 0){throw 'Copy to isolated node failed.'}
    $details=(& docker --context desktop-linux inspect $ids[0] | ConvertFrom-Json)[0]
    $ports=@($details.HostConfig.PortBindings.'8080/tcp')
    if($details.Config.User -ne 'ubuntu' -or $details.HostConfig.Privileged -or @($details.Mounts | Where-Object {$_.Type -ne 'volume' -or $_.Destination -ne '/home/ubuntu'}).Count -gt 0 -or
      $details.HostConfig.CapAdd.Count -gt 0 -or $details.HostConfig.CapDrop -notcontains 'ALL' -or $details.HostConfig.SecurityOpt -notcontains 'no-new-privileges:true' -or
      $details.HostConfig.NetworkMode -eq 'host' -or $details.HostConfig.Memory -ne ([long]$policy.memory_limit_mib_per_node * 1MB) -or
      $details.HostConfig.NanoCpus -ne ([long]$policy.cpu_limit_per_node * 1000000000) -or $details.HostConfig.PidsLimit -ne $policy.pids_limit_per_node -or
      @($details.HostConfig.PortBindings.PSObject.Properties).Count -ne 0){
      throw 'Runtime differs from the isolated compatibility policy; installer was not launched.'
    }
    $networks=@($details.NetworkSettings.Networks.PSObject.Properties.Name)
    foreach($network in $networks){
      $internal=(& docker --context desktop-linux network inspect $network --format '{{.Internal}}' | Out-String).Trim()
      if($LASTEXITCODE -ne 0 -or $internal -ne 'true'){throw 'Installer trial requires internal-only networks.'}
    }
    Write-Output "$($node.id): http://localhost:$($node.browser_port)"
  }
  Write-Output 'Local test desktops have no login; relay ports must remain bound to 127.0.0.1.'
  Write-Output 'This script stages the installer only; it does not launch it, accept licenses or dispatch workloads.'
}finally{foreach($key in $values.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}}
