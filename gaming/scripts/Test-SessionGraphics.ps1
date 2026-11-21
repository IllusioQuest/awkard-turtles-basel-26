param([ValidateSet('session-a')][string]$Node='session-a')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/GamingLab.Common.ps1"
$labRoot=Split-Path -Parent $PSScriptRoot
$reportRoot=Join-Path $labRoot 'TestResults'
New-Item -ItemType Directory -Force $reportRoot | Out-Null
$containerId=Get-GamingContainerId -Node $Node -IncludeStopped
$probe=@'
set +e
printf '%s\n' '=== Device paths ==='
for device_path in /dev/dri /dev/dxg /usr/lib/wsl/lib; do
    if [ -e "$device_path" ]; then ls -ld "$device_path"; else printf 'Not exposed: %s\n' "$device_path"; fi
done
printf '\n=== Driver libraries ===\n'
find /usr/lib -name '*d3d12*' -o -name '*dzn*' -o -name '*vulkan*' 2>/dev/null | head -30
printf '\n=== OpenGL renderer ===\n'
if command -v glxinfo >/dev/null; then timeout 15 glxinfo -B; else echo 'glxinfo unavailable'; fi
printf '\n=== Vulkan devices ===\n'
if command -v vulkaninfo >/dev/null; then timeout 20 vulkaninfo --summary; else echo 'vulkaninfo unavailable: Vulkan compatibility unverified'; fi
printf '\n=== Video capabilities ===\n'
if command -v vainfo >/dev/null; then timeout 15 vainfo; else echo 'vainfo unavailable: VAAPI compatibility unverified'; fi
printf '\n=== Encoder tools ===\n'
command -v ffmpeg || true
'@
# Merge diagnostics INSIDE Linux. Windows PowerShell 5.1 otherwise promotes native stderr
# (including expected vainfo failures) to terminating NativeCommandError under Stop.
# Strip CR from the Windows pipeline before feeding the probe to the Linux shell.
$output=$probe | & docker exec -i --user ubuntu --env DISPLAY=:20 $containerId sh -c 'exec 2>&1; tr -d ''\r'' | sh' | Out-String
$probeExitCode=$LASTEXITCODE
$output | Set-Content (Join-Path $reportRoot "$Node-graphics.txt")
if ($probeExitCode -ne 0) { throw "Docker probe failed (exit $probeExitCode). See $reportRoot/$Node-graphics.txt" }
Write-Output $output
Write-Output 'Capability enumeration does not prove a successful game or hardware encode.'
