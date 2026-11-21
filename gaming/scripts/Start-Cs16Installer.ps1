param([Parameter(Mandatory=$true)][string]$Node)
$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
$policy=Get-Content (Join-Path $lab 'deploy/cs16-lab.json') -Raw | ConvertFrom-Json
if(@($policy.nodes|Where-Object{$_.id -eq $Node}).Count -ne 1){throw 'Unknown compatibility node.'}
& (Join-Path $PSScriptRoot 'Test-Cs16Lab.ps1')
$ids=@(& docker --context desktop-linux ps -q --filter "label=com.docker.compose.project=$($policy.compose_project)" --filter "label=com.docker.compose.service=$Node")
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$'){throw 'CS node scope is not unique.'}
$hash=(& docker --context desktop-linux exec $ids[0] sha256sum /home/ubuntu/cs16-installer.exe | Out-String).Split(' ')[0].Trim()
if($LASTEXITCODE -ne 0 -or $hash -ne $policy.installer_sha256){throw 'Staged installer differs from the reviewed file.'}
# Non-root, offline, bounded, interactive trial. Never add silent license-acceptance arguments.
$trial=@'
exec flock -n /tmp/digitalheat-installer.lock sh -c '
: > /home/ubuntu/cs16-install-exit.txt
python3 /opt/digitalheat/run-with-bounded-log.py --timeout-seconds "$CS16_INSTALL_TIMEOUT_SECONDS" --max-log-bytes "$CS16_MAX_LOG_BYTES" --log /home/ubuntu/cs16-install.log -- wine /home/ubuntu/cs16-installer.exe
result=$?
printf "%s\n" "$result" > /home/ubuntu/cs16-install-exit.txt
wineboot -k >/dev/null 2>&1
'
'@
# Windows checkouts may make here-strings CRLF; sh treats CR as part of arguments/paths.
$trial=$trial.Replace("`r",'')
& docker --context desktop-linux exec -d --user ubuntu --env DISPLAY=:20 --env WINEDEBUG=err+all --env WINEPREFIX=/home/ubuntu/.wine-cs16 `
  --env "CS16_INSTALL_TIMEOUT_SECONDS=$($policy.installer_timeout_seconds)" --env "CS16_MAX_LOG_BYTES=$($policy.max_installer_log_bytes)" $ids[0] sh -c $trial
if($LASTEXITCODE -ne 0){throw 'Installer trial could not be dispatched.'}
Write-Output 'Interactive installer dispatched; check its desktop. Acknowledgment is not installation success. Handle any license terms yourself; no silent acceptance is performed.'
