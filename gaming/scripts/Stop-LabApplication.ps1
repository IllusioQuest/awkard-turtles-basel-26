param(
  [Parameter(Mandatory=$true)][string]$Node,
  [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_.-]{1,15}$')][string]$ProcessName,
  [string]$Policy
)
$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
if(!$Policy){$Policy=Join-Path $lab 'deploy/cs16-lab.json'}
$settings=Get-Content -LiteralPath $Policy -Raw|ConvertFrom-Json
if(@($settings.nodes|Where-Object{$_.id -eq $Node}).Count -ne 1){throw 'Unknown configured node.'}
$ids=@(& docker --context $settings.docker_context ps -q --filter "label=com.docker.compose.project=$($settings.compose_project)" --filter "label=com.docker.compose.service=$Node")
if($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$'){throw 'Expected exactly one running configured node.'}
$script=@'
import os,signal,sys,time
from pathlib import Path
STOP_WAIT_SECONDS=3
POLL_SECONDS=0.1
def matches():
 found=[]
 for p in Path('/proc').iterdir():
  if p.name.isdigit():
   try:
    if p.stat().st_uid==os.getuid() and (p/'comm').read_text().strip()==sys.argv[1]: found.append(int(p.name))
   except OSError: pass
 return found
targets=matches()
for pid in targets:
 try: os.kill(pid,signal.SIGTERM)
 except ProcessLookupError: pass
deadline=time.monotonic()+STOP_WAIT_SECONDS
while matches() and time.monotonic()<deadline: time.sleep(POLL_SECONDS)
remaining=matches()
print('Selected user-owned processes:',len(targets),'; still running:',len(remaining))
sys.exit(3 if remaining else 0)
'@
$script | & docker --context $settings.docker_context exec -i --user ubuntu $ids[0] python3 - $ProcessName
if($LASTEXITCODE -ne 0){throw 'Application did not exit gracefully; no forced kill or node restart was performed.'}
