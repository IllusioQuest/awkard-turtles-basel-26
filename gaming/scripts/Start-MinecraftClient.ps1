param([switch]$PlanOnly)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab
$client=Get-Content (Join-Path $mc.Root 'minecraft/client.json') -Raw | ConvertFrom-Json
$profilesPath=Join-Path $mc.Root 'minecraft/client-runtime.json'
$profiles=Get-Content $profilesPath -Raw | ConvertFrom-Json
$node=@($profiles.nodes | Where-Object {$_.id -eq $client.node_id})[0]
$ids=@(& docker --context $mc.Context ps -q --filter "label=com.docker.compose.project=$($node.compose_project)" --filter "label=com.docker.compose.service=$($node.compose_service)")
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$'){throw 'Expected exactly one running client desktop'}
$exe="$($client.install_directory)/$($client.launcher_relative_path)"
& docker --context $mc.Context exec --user ubuntu $ids[0] test -x $exe
if($LASTEXITCODE -ne 0){throw 'Run Prepare-MinecraftClient.ps1 first'}
# A localhost server address inside this client would refer to the client container itself.
$probe='import socket,sys; s=socket.create_connection((sys.argv[1],int(sys.argv[2])),float(sys.argv[3])); s.close()'
& docker --context $mc.Context exec --user ubuntu $ids[0] python3 -c $probe $client.server_host $mc.Config.lab.calibration_host_port $client.tcp_timeout_seconds
if($LASTEXITCODE -ne 0){throw 'Minecraft server is unreachable from the client; no firewall/network settings changed'}
if(!$PlanOnly){
    $running=& docker --context $mc.Context exec --user ubuntu $ids[0] pgrep -x minecraft-launc
    if($LASTEXITCODE -eq 0 -and $running){
        Write-Output "Launcher already running. Open $($node.browser_url); Minecraft server: $($client.server_host):$($mc.Config.lab.calibration_host_port)."
        return
    }
}
$options=@{Application='minecraft';Node=$client.node_id;Profiles=$profilesPath;PlanOnly=$PlanOnly}
& "$PSScriptRoot/Start-Application.ps1" @options
Write-Output "Sign in yourself, select Java $($mc.Config.version), then Multiplayer > Direct Connection: $($client.server_host):$($mc.Config.lab.calibration_host_port)."
Write-Output 'Launcher dispatch is not gameplay proof. Calibration only; no heat-based client/server admission.'
