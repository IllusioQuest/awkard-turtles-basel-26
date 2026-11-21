$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
$volume=Get-Volume -DriveLetter ([IO.Path]::GetPathRoot($labRoot).Substring(0,1))
if($volume.SizeRemaining -lt 8GB) { throw 'At least 8 GiB free is required; no data is deleted automatically.' }
$baseImage=(& docker image inspect digitalheat-selkies-dzn:experiment --format '{{.Id}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $baseImage -notmatch '^sha256:[a-f0-9]{64}$') { throw 'Build the isolated graphics image first' }
$baseTag='digitalheat-dozen-base:'+($baseImage -replace '^sha256:','')
# BuildKit FROM resolves an image name, not the bare local image ID used by docker run.
& docker image tag $baseImage $baseTag
if($LASTEXITCODE -ne 0){throw 'Cannot tag the exact local driver image for the build'}
$old=$ErrorActionPreference
try {
    $ErrorActionPreference='Continue'
    & docker build --progress plain --build-arg "DESKTOP_IMAGE=$baseTag" --label "digitalheat.dozen.base_image=$baseImage" `
        -f "$labRoot/deploy/Xonotic.Dockerfile" -t digitalheat-xonotic:experiment "$labRoot/deploy" 2>&1 | ForEach-Object { "$_" }
    $code=$LASTEXITCODE
} finally { $ErrorActionPreference=$old }
if($code -ne 0){throw 'Xonotic image build failed'}
