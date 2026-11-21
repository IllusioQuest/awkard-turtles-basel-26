param([ValidateSet('home','hotel','spa')][string]$Peer='home',
    [ValidateSet('status','jobs','heat','healthz')][string]$Path='status', [string]$Json)
$ErrorActionPreference='Stop'
$request=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Json))
# The token stays in the container environment. No token in argv, output or committed config.
$script='if [ -n "$REQUEST_BASE64" ]; then wget -q -O - --header "X-Mesh-Token: $MESH_TOKEN" --header "Content-Type: application/json" --post-data "$(printf %s "$REQUEST_BASE64" | base64 -d)" "http://127.0.0.1:8080/$REQUEST_PATH"; else wget -q -O - --header "X-Mesh-Token: $MESH_TOKEN" "http://127.0.0.1:8080/$REQUEST_PATH"; fi'
$output=& docker.exe --context desktop-linux exec --env "REQUEST_BASE64=$request" --env "REQUEST_PATH=$Path" "digitalheat-mesh-$Peer-1" sh -c $script
if ($LASTEXITCODE -ne 0) { throw "Mesh request failed: $Peer/$Path" }
if ($output) { ($output -join "`n") | ConvertFrom-Json }
