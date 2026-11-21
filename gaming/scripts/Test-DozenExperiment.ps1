param([ValidateRange(5,120)][int]$TimeoutSeconds=30, [switch]$RenderSmoke, [switch]$FullReport)
$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
$resultsRoot=Join-Path $labRoot 'TestResults'
New-Item -ItemType Directory -Force $resultsRoot | Out-Null
$name='digitalheat-dozen-probe-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$image='digitalheat-selkies-dzn:experiment'
$command="exec 2>&1; timeout $TimeoutSeconds vulkaninfo --summary"
if($FullReport) { $command="exec 2>&1; timeout $TimeoutSeconds vulkaninfo" }
if($RenderSmoke) {
    $command='exec 2>&1; export DISPLAY=:99; Xvfb :99 -screen 0 640x480x24 -nolisten tcp >/tmp/xvfb.log 2>&1 & xp=$!; trap "kill $xp 2>/dev/null || true" EXIT; sleep 2; timeout '+$TimeoutSeconds+' vkcube --c 60 --wsi xcb'
}
$output=$command | & docker run -i --rm --name $name --label digitalheat.experiment=dozen --network none --memory 2g --cpus 2 `
    --device /dev/dxg:/dev/dxg `
    --mount type=bind,source=/usr/lib/wsl/lib,target=/usr/lib/wsl/lib,readonly `
    --mount type=bind,source=/usr/lib/wsl/drivers,target=/usr/lib/wsl/drivers,readonly `
    --user ubuntu --entrypoint sh $image -c 'exec 2>&1; tr -d ''\r'' | sh' | Out-String
$exitCode=$LASTEXITCODE
$reportName=if($RenderSmoke){'dozen-render'}elseif($FullReport){'dozen-vulkan-features'}else{'dozen-vulkan-summary'}
$output | Set-Content (Join-Path $resultsRoot "$reportName.txt")
$imageId=& docker image inspect $image --format '{{.Id}}'
$result=[ordered]@{observed_utc=[DateTimeOffset]::UtcNow.ToString('o');image=$imageId;exit_code=$exitCode;timeout_seconds=$TimeoutSeconds;
    intel_device_enumerated=($exitCode -eq 0 -and $output -match 'deviceName\s*=.*Intel|Selected GPU.*Intel' -and $output -notmatch 'PHYSICAL_DEVICE_TYPE_CPU');
    render_test=$(if($RenderSmoke){"exit $exitCode; inspect renderer output separately"}else{'not performed'});cs2='not tested'}
$result | ConvertTo-Json | Set-Content (Join-Path $resultsRoot "$reportName.json")
if($FullReport) { Write-Output "Feature report: $resultsRoot/$reportName.txt" } else { Write-Output $output }
Write-Output ($result | ConvertTo-Json)
if($exitCode -ne 0) { throw "Vulkan probe failed or timed out (exit $exitCode). Existing desktops unchanged." }
if(-not $result.intel_device_enumerated) { throw 'Intel hardware Vulkan device was not observed in probe output.' }
Write-Output 'Probe completed. This does not prove game compatibility or browser-stream performance.'
