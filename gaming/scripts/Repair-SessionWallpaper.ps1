param([ValidateSet('session-a','session-b')][string]$Node='session-a')
$ErrorActionPreference='Stop'
$ids=@(& docker ps -q --filter label=com.docker.compose.project=digitalheat-gaming --filter "label=digitalheat.gaming.node=$Node")
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1) { throw 'Expected one running gaming node' }
# Ask the desktop manager to repaint the wallpaper; no session/container restart.
& docker exec --user ubuntu --env DISPLAY=:20 $ids[0] pcmanfm-qt --profile=lxqt --set-wallpaper=/usr/share/lxqt/themes/selkies/wallpaper.svg --wallpaper-mode=stretch
if($LASTEXITCODE -ne 0) { throw 'Wallpaper repaint failed' }
Write-Output 'Wallpaper stretch requested. Check the result in the browser; input/session were preserved.'
