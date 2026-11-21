$ErrorActionPreference='Stop'
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab
$config=$mc.Config
$context=Join-Path $mc.Results 'build'
New-Item -ItemType Directory -Path $context -Force | Out-Null
$metadata=Join-Path $context 'version.json'
Invoke-WebRequest -UseBasicParsing $config.version_manifest_url -OutFile $metadata
if((Get-FileHash $metadata -Algorithm SHA1).Hash.ToLowerInvariant() -ne $config.version_manifest_sha1){throw 'Mojang version metadata checksum mismatch'}
$version=Get-Content $metadata -Raw | ConvertFrom-Json
if($version.id -ne $config.version -or $version.javaVersion.majorVersion -ne $config.java_major -or
    $version.downloads.server.url -ne $config.server_url -or $version.downloads.server.sha1 -ne $config.server_sha1){throw 'Pinned Mojang metadata does not match configuration'}
$jar=Join-Path $context 'server.jar'
if(!(Test-Path -LiteralPath $jar)){Invoke-WebRequest -UseBasicParsing $config.server_url -OutFile $jar}
if((Get-Item -LiteralPath $jar).Length -ne $config.server_bytes -or (Get-FileHash $jar -Algorithm SHA1).Hash.ToLowerInvariant() -ne $config.server_sha1){throw 'Official server artifact checksum/size mismatch'}
$sha256=(Get-FileHash $jar -Algorithm SHA256).Hash.ToLowerInvariant()
if($sha256 -ne $config.server_sha256){throw 'Recorded SHA256 server artifact fingerprint mismatch'}
foreach($file in @('Dockerfile','MinecraftStatus.java','start-server.sh')){Copy-Item -LiteralPath (Join-Path $mc.Root "minecraft/$file") -Destination $context -Force}
# Linux script line endings are deterministic even from a Windows Git checkout.
$scriptPath=Join-Path $context 'start-server.sh'
$text=[IO.File]::ReadAllText($scriptPath).Replace("`r`n","`n")
[IO.File]::WriteAllText($scriptPath,$text,[Text.UTF8Encoding]::new($false))
$properties=@("server-port=$($config.lab.server_port)",'server-ip=','online-mode=true','enable-status=true','enable-rcon=false',
    "max-players=$($config.lab.max_players)","view-distance=$($config.lab.view_distance_chunks)","simulation-distance=$($config.lab.simulation_distance_chunks)",
    "level-seed=$($config.lab.world_seed)",'level-name=world','motd=DigitalHeat measured Minecraft lab') -join "`n"
[IO.File]::WriteAllText((Join-Path $context 'server.properties'),$properties+"`n",[Text.UTF8Encoding]::new($false))
$old=$ErrorActionPreference
try {
    $ErrorActionPreference='Continue'
    & docker --context $mc.Context build --progress plain --build-arg "JDK_IMAGE=$($config.jdk_image)" --build-arg "JRE_IMAGE=$($config.jre_image)" --build-arg "MC_VERSION=$($config.version)" --build-arg "SERVER_SHA256=$sha256" --build-arg "MC_USER_ID=$($config.lab.user_id)" --build-arg "MC_PORT=$($config.lab.server_port)" -t $config.image_tag $context 2>&1 | ForEach-Object {if($_ -is [Management.Automation.ErrorRecord]){$_.Exception.Message}else{[string]$_}}
    $code=$LASTEXITCODE
} finally {$ErrorActionPreference=$old}
if($code -ne 0){throw 'Minecraft image build failed'}
$image=(Invoke-MinecraftDocker -DockerArguments @('image','inspect',$config.image_tag) | ConvertFrom-Json)[0]
[ordered]@{version=$config.version;java_major=$config.java_major;image_id=$image.Id;server_url=$config.server_url;server_sha1=$config.server_sha1;server_sha256=$sha256;
    jdk_image=$config.jdk_image;jre_image=$config.jre_image;built_utc=[DateTimeOffset]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $mc.Results 'build.json')
Write-Output 'Built pinned Vanilla server image. No server or world was started.'
