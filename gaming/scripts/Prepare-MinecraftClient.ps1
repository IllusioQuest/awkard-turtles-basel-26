$ErrorActionPreference='Stop'
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab
$client=Get-Content (Join-Path $mc.Root 'minecraft/client.json') -Raw | ConvertFrom-Json
$profiles=Get-Content (Join-Path $mc.Root 'minecraft/client-runtime.json') -Raw | ConvertFrom-Json
$node=@($profiles.nodes | Where-Object {$_.id -eq $client.node_id})[0]
if(!$node -or $node.docker_context -ne $mc.Context){throw 'Client node/context mismatch'}
$ids=@(& docker --context $mc.Context ps -q --filter "label=com.docker.compose.project=$($node.compose_project)" --filter "label=com.docker.compose.service=$($node.compose_service)")
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$'){throw 'Expected exactly one running client desktop'}
# No native Windows install, package-manager mutation, authentication bypass or account-data reads.
$install=@'
set -eu
url=$1; expected=$2; directory=$3; timeout=$4; relative=$5
test "$url" = https://launcher.mojang.com/download/Minecraft.tar.gz
case "$directory" in /home/ubuntu/.local/share/digitalheat) ;; *) exit 64;; esac
archive=$(mktemp /tmp/digitalheat-launcher.XXXXXX)
trap 'rm -f "$archive"' EXIT
curl --fail --silent --show-error --location --proto '=https' --max-time "$timeout" "$url" -o "$archive"
printf '%s  %s\n' "$expected" "$archive" | sha256sum -c - >/dev/null
# Exact archive contents verified before extraction. No token/cache directories are read or copied.
test "$(tar -tzf "$archive")" = "$(printf 'minecraft-launcher/\nminecraft-launcher/minecraft-launcher')"
if [ -f "$directory/$relative" ]; then
    installed=$(sha256sum "$directory/$relative" | cut -d ' ' -f 1)
    packaged=$(tar -xOzf "$archive" "$relative" | sha256sum | cut -d ' ' -f 1)
    if [ "$installed" = "$packaged" ]; then echo 'Pinned official launcher already installed; left running process and account data unchanged.'; exit 0; fi
fi
if pgrep -x minecraft-launc >/dev/null; then echo 'Close the launcher before replacing its executable.' >&2; exit 65; fi
mkdir -p "$directory"
tar --no-same-owner -xzf "$archive" -C "$directory"
test -x "$directory/$relative"
if ldd "$directory/$relative" | grep -q 'not found'; then echo 'Missing launcher library' >&2; exit 65; fi
echo 'Official launcher installed; account login and game download remain user actions.'
'@
$install=$install.Replace("`r`n","`n")
$install | & docker --context $mc.Context exec -i --user ubuntu $ids[0] sh -s -- $client.launcher_url $client.launcher_sha256 $client.install_directory $client.download_timeout_seconds $client.launcher_relative_path
if($LASTEXITCODE -ne 0){throw 'Client preparation failed; do not bypass checksum or install unofficial launchers'}
Write-Output "Client prepared on $($node.id). Use Start-MinecraftClient.ps1. Existing persistent home retained."
