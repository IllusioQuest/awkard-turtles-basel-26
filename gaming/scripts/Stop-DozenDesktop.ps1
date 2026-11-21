$ErrorActionPreference='Stop'
$ids=@(& docker ps -q --filter label=com.docker.compose.project=digitalheat-dozen --filter label=com.docker.compose.service=session-c)
if($LASTEXITCODE -ne 0) { throw 'Cannot enumerate experimental desktop C' }
if($ids.Count -gt 0) {
    & docker stop @ids
    if($LASTEXITCODE -ne 0) { throw 'Cannot stop experimental desktop C' }
}
