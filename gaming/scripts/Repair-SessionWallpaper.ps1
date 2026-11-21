param([ValidateSet('session-a','session-b','session-c')][string]$Node='session-a')
$ErrorActionPreference='Stop'
$project=if($Node -eq 'session-c'){'digitalheat-dozen'}else{'digitalheat-gaming'}
$ids=@(& docker ps -q --filter "label=com.docker.compose.project=$project" --filter "label=com.docker.compose.service=$Node")
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1) { throw 'Expected one running gaming node' }
# Ask the desktop manager to repaint the wallpaper; no session/container restart.
& docker exec --user ubuntu --env DISPLAY=:20 $ids[0] pcmanfm-qt --profile=lxqt --wallpaper-mode=color
if($LASTEXITCODE -ne 0) { throw 'Wallpaper repaint failed' }
Write-Output 'Uniform desktop background requested. Check the result in the browser; input/session were preserved.'
