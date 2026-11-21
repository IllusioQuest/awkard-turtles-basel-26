$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
$volume=Get-Volume -DriveLetter ([IO.Path]::GetPathRoot($labRoot).Substring(0,1))
if($volume.SizeRemaining -lt 12GB) { throw 'At least 12 GiB free is required for the isolated build; no caches are deleted automatically.' }
$previousPreference=$ErrorActionPreference
try {
    # Docker build progress uses stderr. In Windows PowerShell 5.1 it is informational,
    # not a terminating error; the explicit process exit code remains authoritative.
    $ErrorActionPreference='Continue'
    & docker build --progress plain -f "$labRoot/deploy/Dozen.Dockerfile" -t digitalheat-selkies-dzn:experiment "$labRoot/deploy" 2>&1 | ForEach-Object { "$_" }
    $buildExitCode=$LASTEXITCODE
} finally { $ErrorActionPreference=$previousPreference }
if($buildExitCode -ne 0) { throw 'Dozen build failed; existing desktops are unchanged.' }
& docker image inspect digitalheat-selkies-dzn:experiment --format '{{.Id}}'
