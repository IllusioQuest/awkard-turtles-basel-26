$ErrorActionPreference='Stop'
$lab=Split-Path -Parent $PSScriptRoot
$policy=Get-Content (Join-Path $lab 'deploy/cs16-lab.json') -Raw | ConvertFrom-Json
& docker --context desktop-linux build --progress plain -f (Join-Path $lab 'deploy/Cs16.Wine.Dockerfile') -t $policy.image (Join-Path $lab 'deploy')
if($LASTEXITCODE -ne 0){throw 'CS 1.6 compatibility image build failed.'}
& docker --context desktop-linux build --progress plain -f (Join-Path $lab 'deploy/TcpRelay.Dockerfile') -t $policy.relay_image (Join-Path $lab 'deploy')
if($LASTEXITCODE -ne 0){throw 'Reusable TCP relay build failed.'}
