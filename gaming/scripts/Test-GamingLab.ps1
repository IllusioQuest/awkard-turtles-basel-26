$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
$credentials=Get-Content (Join-Path $labRoot 'TestResults/credentials.json') -Raw | ConvertFrom-Json
$results=@()
foreach($node in @(@{id='session-a';port=18881;password=$credentials.password_a},@{id='session-b';port=18882;password=$credentials.password_b})) {
    $url="http://localhost:$($node.port)"
    $anonymous=Invoke-WebRequest $url -SkipHttpErrorCheck
    if($anonymous.StatusCode -ne 401) { throw 'Unauthenticated desktop access was not rejected' }
    $header=@{Authorization='Basic '+[Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("player:$($node.password)"))}
    $response=Invoke-WebRequest $url -Headers $header
    if($response.StatusCode -ne 200) { throw 'Authenticated desktop page unavailable' }
    $id=(& docker ps -q --filter label=com.docker.compose.project=digitalheat-gaming --filter "label=digitalheat.gaming.node=$($node.id)" | Out-String).Trim()
    if($LASTEXITCODE -ne 0 -or $id -notmatch '^[a-f0-9]{12,64}$') { throw 'Missing or ambiguous node' }
    $inspect=(& docker inspect $id | ConvertFrom-Json)[0]
    if($inspect.HostConfig.Privileged) { throw 'Unexpected privileged node' }
    if(@($inspect.Mounts | Where-Object Destination -eq '/var/run/docker.sock').Count) { throw 'Unexpected Docker socket' }
    if($inspect.HostConfig.PortBindings.'8080/tcp'[0].HostIp -ne '127.0.0.1') { throw 'Unexpected public desktop binding' }
    $results+=@{node_id=$node.id;anonymous_http=401;authenticated_http=200;image=$inspect.Image;home_volume=($inspect.Mounts | Where-Object Destination -eq '/home/ubuntu').Name;browser_video_input='requires manual verification'}
}
if($results[0].home_volume -eq $results[1].home_volume) { throw 'Session homes must be separate' }
$results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $labRoot 'TestResults/lab-results.json')
$results | Select-Object node_id,anonymous_http,authenticated_http,browser_video_input
