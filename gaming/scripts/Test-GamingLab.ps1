$ErrorActionPreference='Stop'
. "$PSScriptRoot/GamingLab.Common.ps1"
$labRoot=Split-Path -Parent $PSScriptRoot
$credentials=Get-Content (Join-Path $labRoot 'TestResults/credentials.json') -Raw | ConvertFrom-Json
$results=@()
foreach($node in @(@{id='session-a';port=18881;password=$credentials.password_a})) {
    $url="http://localhost:$($node.port)"
    if((Get-GamingHttpStatusCode -Url $url) -ne 401) { throw 'Unauthenticated desktop access was not rejected' }
    if((Get-GamingHttpStatusCode -Url $url -Username player -Password $node.password) -ne 200) { throw 'Authenticated desktop page unavailable' }
    $id=Get-GamingContainerId -Node $node.id
    $inspect=(& docker inspect $id | ConvertFrom-Json)[0]
    if($inspect.HostConfig.Privileged) { throw 'Unexpected privileged node' }
    if(@($inspect.Mounts | Where-Object Destination -eq '/var/run/docker.sock').Count) { throw 'Unexpected Docker socket' }
    if($inspect.HostConfig.PortBindings.'8080/tcp'[0].HostIp -ne '127.0.0.1') { throw 'Unexpected public desktop binding' }
    $results+=[pscustomobject]@{node_id=$node.id;anonymous_http=401;authenticated_http=200;image=$inspect.Image;home_volume=($inspect.Mounts | Where-Object Destination -eq '/home/ubuntu').Name;browser_video_input='requires manual verification'}
}
$results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $labRoot 'TestResults/lab-results.json')
$results | Select-Object node_id,anonymous_http,authenticated_http,browser_video_input
