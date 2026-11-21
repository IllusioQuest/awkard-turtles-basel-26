param([switch]$Pull, [switch]$IntelWsl)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/GamingLab.Common.ps1"
$labRoot=Split-Path -Parent $PSScriptRoot
$stateRoot=Join-Path $labRoot 'TestResults'
New-Item -ItemType Directory -Force $stateRoot | Out-Null
$tag=(Get-Content (Join-Path $labRoot 'deploy/image.txt') -Raw).Trim()
if ($Pull) { & docker pull $tag; if ($LASTEXITCODE -ne 0) { throw 'Image pull failed' } }
$image=& docker image inspect $tag --format '{{index .RepoDigests 0}}'
if ($LASTEXITCODE -ne 0 -or $image -notmatch '@sha256:[a-f0-9]{64}$') { throw 'Pull the official image first with -Pull' }
$credentialsPath=Join-Path $stateRoot 'credentials.json'
if (Test-Path $credentialsPath) { $credentials=Get-Content $credentialsPath -Raw | ConvertFrom-Json }
else {
    $credentials=@{user='player';password_a=[Guid]::NewGuid().ToString('N');password_b=[Guid]::NewGuid().ToString('N')}
    $credentials | ConvertTo-Json | Set-Content $credentialsPath
}
$names=@('GAMING_IMAGE','GAMING_PASSWORD_A','GAMING_PASSWORD_B')
$previous=@{}; foreach($name in $names) { $previous[$name]=[Environment]::GetEnvironmentVariable($name) }
try {
    $env:GAMING_IMAGE=$image
    $env:GAMING_PASSWORD_A=$credentials.password_a
    $env:GAMING_PASSWORD_B=$credentials.password_b
    $composeArgs=@('compose','-f',"$labRoot/deploy/compose.yaml")
    if ($IntelWsl) { $composeArgs+=@('-f',"$labRoot/deploy/intel-wsl.override.yaml") }
    & docker @composeArgs up -d
    if ($LASTEXITCODE -ne 0) { throw 'Lab startup failed' }
    foreach($port in @(18881,18882)) {
        $ready=$false
        for($attempt=0;$attempt -lt 30;$attempt++) {
            try { if((Get-GamingHttpStatusCode -Url "http://localhost:$port" -TimeoutSeconds 2) -eq 401) { $ready=$true; break } } catch { }
            Start-Sleep -Seconds 1
        }
        if(-not $ready) { throw "Node on port $port did not become ready with authentication enabled" }
    }
    @{image=$image;mode=$(if($IntelWsl){'experimental Intel WSL on A; software B'}else{'software baseline'});nodes=@('http://localhost:18881','http://localhost:18882')} | ConvertTo-Json | Set-Content (Join-Path $stateRoot 'deployment.json')
    Write-Output "Session A: http://localhost:18881; Session B: http://localhost:18882"
    Write-Output "Credentials are local only: $credentialsPath"
} finally { foreach($name in $names) { [Environment]::SetEnvironmentVariable($name,$previous[$name]) } }
