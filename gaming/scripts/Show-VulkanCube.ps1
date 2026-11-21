param([ValidateRange(1,6000)][int]$Frames=600, [ValidateRange(5,120)][int]$TimeoutSeconds=30)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/GamingLab.Common.ps1"
$containerId=Get-GamingContainerId -Node session-c
Write-Output 'Open http://localhost:18883 to view the cube. This is a Vulkan smoke test, not CS2.'
& docker exec --user ubuntu --env DISPLAY=:20 $containerId sh -c "exec 2>&1; timeout $TimeoutSeconds vkcube --c $Frames --wsi xcb"
if($LASTEXITCODE -ne 0) { throw 'Vulkan cube failed or timed out. Inspect the output before testing games.' }
