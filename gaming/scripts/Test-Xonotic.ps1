$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
$ids=@(& docker ps -q --filter label=com.docker.compose.project=digitalheat-dozen --filter label=com.docker.compose.service=session-c)
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1){throw 'Expected one running desktop C'}
$pidText=(& docker exec --user ubuntu $ids[0] pidof xonotic-linux64-sdl | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $pidText -notmatch '^\d+$'){throw 'Expected one running Xonotic process'}
$log=& docker exec --user ubuntu $ids[0] cat /home/ubuntu/xonotic-demo.log | Out-String
if($LASTEXITCODE -ne 0){throw 'Cannot read Xonotic startup log'}
$details=(& docker inspect $ids[0]|ConvertFrom-Json)[0]
$result=[ordered]@{observed_utc=[DateTimeOffset]::UtcNow.ToString('o');image=$details.Image;container_user=$details.Config.User;
    game_running=$true;intel_opengl=($log -match 'GL_RENDERER: D3D12 \(Intel');assets_loaded=($log -match 'execing xonotic-client.cfg');
    stereo_audio_initialized=($log -match 'Sound format: 48000Hz, 2 channels');joystick_warning=($log -match 'Failed to init SDL joystick');
    browser_gameplay='requires user confirmation';scheduler_launch='not integrated'}
$result | ConvertTo-Json | Set-Content (Join-Path $labRoot 'TestResults/xonotic-results.json')
$result | ConvertTo-Json
if($result.container_user -notin @('1000','ubuntu') -or -not $result.intel_opengl -or -not $result.assets_loaded){throw 'Startup checks failed'}
