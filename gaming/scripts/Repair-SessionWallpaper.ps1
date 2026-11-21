param([ValidateSet('session-a','session-b','session-c')][string]$Node='session-a')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/GamingLab.Common.ps1"
$containerId=Get-GamingContainerId -Node $Node
# Ask the desktop manager to repaint the wallpaper; no session/container restart.
& docker exec --user ubuntu --env DISPLAY=:20 $containerId pcmanfm-qt --profile=lxqt --wallpaper-mode=color
if($LASTEXITCODE -ne 0) { throw 'Wallpaper repaint failed' }
Write-Output 'Uniform desktop background requested. Check the result in the browser; input/session were preserved.'
