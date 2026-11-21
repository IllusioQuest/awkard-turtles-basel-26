$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
$policy=Get-Content (Join-Path $lab 'deploy/cs16-lab.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'GamingLab.Common.ps1')
$records=@()
foreach($node in $policy.nodes){
  $ids=@(& docker --context desktop-linux ps -q --filter "label=com.docker.compose.project=$($policy.compose_project)" --filter "label=com.docker.compose.service=$($node.id)")
  if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1){throw 'CS node scope is not unique.'}
  $details=(& docker --context desktop-linux inspect $ids[0] | ConvertFrom-Json)[0]
  $networks=@($details.NetworkSettings.Networks.PSObject.Properties.Name)
  $internalOnly=$networks.Count -gt 0
  foreach($network in $networks){
    if((& docker --context desktop-linux network inspect $network --format '{{.Internal}}'|Out-String).Trim() -ne 'true'){$internalOnly=$false}
  }
  $url="http://127.0.0.1:$($node.browser_port)"
  $unauthenticated=Get-GamingHttpStatusCode -Url $url -TimeoutSeconds 5
  $ok=$details.Config.User -eq 'ubuntu' -and -not $details.HostConfig.Privileged -and
    $details.HostConfig.CapDrop -contains 'ALL' -and $details.HostConfig.CapAdd.Count -eq 0 -and
    $details.HostConfig.SecurityOpt -contains 'no-new-privileges:true' -and
    @($details.Mounts|Where-Object{$_.Type -ne 'volume' -or $_.Destination -ne '/home/ubuntu'}).Count -eq 0 -and
    $details.HostConfig.Memory -eq ([long]$policy.memory_limit_mib_per_node * 1MB) -and
    $details.HostConfig.NanoCpus -eq ([long]$policy.cpu_limit_per_node * 1000000000) -and
    $details.HostConfig.PidsLimit -eq $policy.pids_limit_per_node -and
    $internalOnly -and $unauthenticated -eq 200 -and $details.Config.Env -contains 'SELKIES_ENABLE_BASIC_AUTH=false'
  if(!$ok){throw "CS lab isolation/readiness failed for $($node.id)."}
  $records+=@{node_id=$node.id;user=$details.Config.User;internal_network=$internalOnly;memory_limit_mib=$policy.memory_limit_mib_per_node;
    cpu_limit=$policy.cpu_limit_per_node;unauthenticated_http=$unauthenticated;installer_gameplay_verified=$false}
}
$relay=(& docker --context desktop-linux inspect "$($policy.compose_project)-browser-relay-1" | ConvertFrom-Json)[0]
if($relay.HostConfig.Privileged -or $relay.Config.User -ne '1000:1000' -or !$relay.HostConfig.ReadonlyRootfs -or
  $relay.Mounts.Count -ne 0 -or $relay.HostConfig.CapDrop -notcontains 'ALL' -or
  $relay.HostConfig.SecurityOpt -notcontains 'no-new-privileges:true'){throw 'Relay isolation differs from policy.'}
$bindings=@($relay.HostConfig.PortBindings.PSObject.Properties|ForEach-Object{$_.Value}|ForEach-Object{$_})
if($bindings.Count -ne $policy.nodes.Count -or @($bindings|Where-Object{$_.HostIp -ne '127.0.0.1' -or [int]$_.HostPort -notin @($policy.nodes.browser_port)}).Count -gt 0){throw 'Relay ports differ from loopback-only policy.'}
$records|ConvertTo-Json -Depth 8|Set-Content (Join-Path $lab 'TestResults/cs16-node-preflight.json')
Write-Output 'PASS: two isolated desktops, capped resources, internal networks and loopback-only access without login. This does not prove game installation or multiplayer.'
