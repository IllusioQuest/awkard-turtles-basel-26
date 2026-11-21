$ErrorActionPreference='Stop'
# Stop only this lab's labelled containers; preserve homes and credentials for retesting.
$ids=@(& docker ps -q --filter label=com.docker.compose.project=digitalheat-gaming)
if($LASTEXITCODE -ne 0) { throw 'Cannot enumerate gaming lab' }
if($ids.Count -gt 0) {
    & docker stop @ids
    if($LASTEXITCODE -ne 0) { throw 'Cannot stop gaming lab' }
}
