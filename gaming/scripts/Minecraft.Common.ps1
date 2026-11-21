function Get-MinecraftLab {
    param([string]$ContainerName,[string]$VolumeName,[int]$HostPort)
    $labRoot=Split-Path -Parent $PSScriptRoot
    $config=Get-Content (Join-Path $labRoot 'minecraft/server.json') -Raw | ConvertFrom-Json
    if($ContainerName){$config.lab.container_name=$ContainerName}
    if($VolumeName){$config.lab.volume_name=$VolumeName}
    if($HostPort){$config.lab.calibration_host_port=$HostPort}
    if($config.lab.container_name -notmatch '^digitalheat-minecraft-[a-z0-9-]+$' -or $config.lab.volume_name -notmatch '^digitalheat-minecraft-[a-z0-9-]+$' -or
        $config.lab.calibration_host_port -lt 1024 -or $config.lab.calibration_host_port -gt 65535){throw 'Invalid lab identity/port'}
    $results=Join-Path $labRoot 'TestResults/minecraft'
    New-Item -ItemType Directory -Path $results -Force | Out-Null
    $sha=[Security.Cryptography.SHA256]::Create()
    $runtimeConfig=[ordered]@{version=$config.version;java_major=$config.java_major;server_sha1=$config.server_sha1;lab=$config.lab}
    try {$fingerprint=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($runtimeConfig|ConvertTo-Json -Depth 20 -Compress))))).Replace('-','').ToLowerInvariant()}
    finally {$sha.Dispose()}
    return @{Root=$labRoot;Config=$config;Results=$results;Context=$config.lab.docker_context;Fingerprint=$fingerprint}
}
function Invoke-MinecraftDocker {
    param([Parameter(Mandatory=$true)][string[]]$DockerArguments)
    $previous=$ErrorActionPreference
    try {$ErrorActionPreference='Continue'; $output=& docker --context $script:mc.Context @DockerArguments 2>&1 | ForEach-Object {if($_ -is [Management.Automation.ErrorRecord]){$_.Exception.Message}else{[string]$_}} | Out-String; $code=$LASTEXITCODE}
    finally {$ErrorActionPreference=$previous}
    if($code -ne 0){throw "Docker Minecraft operation failed ($($DockerArguments[0])): $($output.Trim())"}
    return $output.TrimEnd()
}
function Get-MinecraftContainer {
    $ids=@(& docker --context $script:mc.Context ps -aq --filter "name=^/$($script:mc.Config.lab.container_name)`$")
    if($LASTEXITCODE -ne 0){throw 'Cannot inspect Minecraft lab'}
    if($ids.Count -eq 0){return $null}
    if($ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$'){throw 'Ambiguous Minecraft lab identity'}
    $details=(Invoke-MinecraftDocker -DockerArguments @('inspect',$ids[0]) | ConvertFrom-Json)[0]
    if($details.Config.Labels.'digitalheat.minecraft.lab' -ne 'calibration' -or $details.Config.Labels.'digitalheat.minecraft.version' -ne $script:mc.Config.version){
        throw 'Existing container is not this owned Minecraft calibration lab'
    }
    return $details
}
function Get-MinecraftStatus {
    $container=Get-MinecraftContainer
    if(!$container -or !$container.State.Running -or $container.State.Paused){throw 'Minecraft lab is not running'}
    $raw=Invoke-MinecraftDocker -DockerArguments @('exec','--user',([string]$script:mc.Config.lab.user_id),$container.Id,'java','-cp','/opt/probe','MinecraftStatus','--ready','127.0.0.1',([string]$script:mc.Config.lab.server_port))
    $status=$raw | ConvertFrom-Json
    if($status.version.name -ne $script:mc.Config.version){throw 'Running Minecraft version does not match the pinned release'}
    return $status
}
