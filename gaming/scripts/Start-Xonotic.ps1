param([switch]$StartMatch)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/GamingLab.Common.ps1"
$labRoot=Split-Path -Parent $PSScriptRoot
$config=Get-Content "$labRoot/deploy/xonotic-demo.json" -Raw | ConvertFrom-Json
if($config.width -lt 320 -or $config.width -gt 1920 -or $config.height -lt 240 -or $config.height -gt 1080 -or
    $config.max_fps -lt 10 -or $config.max_fps -gt 60 -or $config.bot_count -lt 0 -or $config.bot_count -gt 8 -or
    $config.map -notmatch '^[a-z0-9_-]+$') { throw 'Invalid presentation settings' }
$containerId=Get-GamingContainerId -Node session-c
$arguments="-window +vid_width $([int]$config.width) +vid_height $([int]$config.height) +vid_fullscreen 0 +cl_maxfps $([int]$config.max_fps)"
if($StartMatch){$arguments+=" +map $($config.map) +bot_number $([int]$config.bot_count)"}
$command="exec flock -n /tmp/digitalheat-xonotic.lock /opt/Xonotic/xonotic-linux64-sdl $arguments >> /home/ubuntu/xonotic-demo.log 2>&1"
& docker exec -d --user ubuntu --workdir /opt/Xonotic --env DISPLAY=:20 --env XDG_RUNTIME_DIR=/tmp/runtime-ubuntu --env PULSE_SERVER=unix:/tmp/runtime-ubuntu/pulse/native $containerId sh -c $command
if($LASTEXITCODE -ne 0){throw 'Could not dispatch Xonotic launch'}
Write-Output 'Launch requested. Open http://localhost:18883; server-side log: /home/ubuntu/xonotic-demo.log.'
Write-Output 'An existing game holds a lock; a second launch does not create another instance.'
