param([switch]$SkipDownloads, [switch]$SkipDockerBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$state = Join-Path $repo 'data/external/audience-demo'
$model = Join-Path $state 'Apertus-8B-Instruct-2509-Q4_K_M.gguf'
$tunnel = Join-Path $state 'cloudflared.exe'
$build = Join-Path $state 'build'
$demoConfig = Get-Content (Join-Path $repo 'config/audience-apertus-demo.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $state,$build | Out-Null
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 8 SDK first.' }
if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Install Docker Desktop first.' }
& docker --context desktop-linux info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop with Linux containers first.' }
$memoryGb = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory/1GB,1)
$reservedGb = [math]::Round((4 * $demoConfig.worker_ram_mib)/1024,1)
if ($memoryGb -lt ($reservedGb + 2)) { throw "Four workers can reserve $reservedGb GiB plus Docker/OS overhead; this laptop has $memoryGb GiB. Rehearse on a larger laptop or reduce the configured worker reservation after measuring it." }
if (!(Test-Path $model)) {
    if ($SkipDownloads) { throw "Missing model: $model" }
    Write-Host 'Downloading Apertus 8B Q4 model (about 5 GB) on first start…'
    $url='https://huggingface.co/unsloth/Apertus-8B-Instruct-2509-GGUF/resolve/main/Apertus-8B-Instruct-2509-Q4_K_M.gguf'
    & curl.exe -fL --retry 3 -C - -o $model $url
    if ($LASTEXITCODE -ne 0) { throw 'Model download failed.' }
}
if ((Get-Item $model).Length -ne 5057885440) { throw 'Apertus model size does not match docs/jobs/llm-summary.md; inspect the download.' }
if ((Get-FileHash -Algorithm SHA256 $model).Hash.ToLowerInvariant() -ne '87e7f0eb5a1d33e1a8ea4e7fdd2363764e0465061dd92962e0073aec31a7944d') { throw 'Apertus SHA256 does not match the pinned model.' }
if (!(Test-Path $tunnel)) {
    if ($SkipDownloads) { throw "Missing cloudflared: $tunnel" }
    Write-Host 'Downloading cloudflared from the official release…'
    & curl.exe -fL --retry 3 -o $tunnel 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe'
    if ($LASTEXITCODE -ne 0) { throw 'cloudflared download failed.' }
}
& docker --context desktop-linux image inspect digitalheat/apertus-audience:local *> $null
$audienceImagePresent = $LASTEXITCODE -eq 0
if (!$SkipDockerBuild -and !$audienceImagePresent) {
    Write-Host 'Building the pinned llama.cpp runner and the model image…'
    $base = Join-Path $state 'base-build'
    New-Item -ItemType Directory -Force $base | Out-Null
    $archive = Join-Path $state 'llama.cpp-b12600.tar.gz'
    $source = Join-Path $base 'llama.cpp-b12600'
    if (!(Test-Path $archive)) {
        & curl.exe -fL --retry 3 -o $archive 'https://github.com/ggml-org/llama.cpp/archive/refs/tags/b12600.tar.gz'
        if ($LASTEXITCODE -ne 0) { throw 'Pinned llama.cpp source download failed.' }
    }
    if ((Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant() -ne '54515583887dfc429ca995551554b52caaf2d1301e5de2b8659f56df9c62ad2d') { throw 'Pinned llama.cpp source SHA256 mismatch.' }
    $extractMarker = Join-Path $source '.audience-extract-ok'
    if (!(Test-Path $extractMarker)) {
        & tar.exe -xzf $archive -C $base
        if ($LASTEXITCODE -ne 0 -or !(Test-Path (Join-Path $source 'CMakeLists.txt'))) { throw 'Pinned llama.cpp source extraction failed.' }
        [IO.File]::WriteAllText($extractMarker,'b12600')
    }
    $original = Get-Content (Join-Path $repo 'jobs/llm/Dockerfile') -Raw
    $patched = $original.Replace('RUN git clone --depth 1 --branch "${LLAMA_CPP_VERSION}" https://github.com/ggml-org/llama.cpp /src','COPY llama.cpp-b12600 /src')
    if ($patched -eq $original) { throw 'Pinned llama.cpp Dockerfile no longer matches the expected build step.' }
    Set-Content (Join-Path $base 'Dockerfile') $patched -Encoding ascii
    Copy-Item (Join-Path $repo 'jobs/llm/run_queue.sh') (Join-Path $base 'run_queue.sh') -Force
    & docker --context desktop-linux build -t digitalheat/llm-summary:latest $base
    if ($LASTEXITCODE -ne 0) { throw 'Pinned llama.cpp image build failed.' }
    Copy-Item (Join-Path $repo 'src/DigitalHeat.AudienceDemo/Containerfile') (Join-Path $build 'Containerfile') -Force
    Copy-Item (Join-Path $repo 'src/DigitalHeat.AudienceDemo/answer.sh') (Join-Path $build 'answer.sh') -Force
    $target = Join-Path $build 'model.gguf'
    if (!(Test-Path $target)) { New-Item -ItemType HardLink -Path $target -Target $model | Out-Null }
    & docker --context desktop-linux build -t digitalheat/apertus-audience:local -f (Join-Path $build 'Containerfile') $build
    if ($LASTEXITCODE -ne 0) { throw 'Audience image build failed.' }
}
& docker --context desktop-linux image inspect digitalheat/apertus-audience:local *> $null
if ($LASTEXITCODE -ne 0) { throw 'Audience image missing.' }
& dotnet build (Join-Path $repo 'src/DigitalHeat.AudienceDemo/DigitalHeat.AudienceDemo.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Audience service build failed.' }
$tokenFile = Join-Path $state 'operator-token.txt'
if (!(Test-Path $tokenFile)) {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    [IO.File]::WriteAllText($tokenFile,[BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant())
}
$env:DEMO_OPERATOR_TOKEN = [IO.File]::ReadAllText($tokenFile)
$env:DEMO_STATE_DIR = $state
$env:DEMO_CONFIG = Join-Path $repo 'config/audience-apertus-demo.json'
$env:DEMO_PORT = '18970'
$appLog = Join-Path $state 'app.log'
$appErr = Join-Path $state 'app-error.log'
$project = Join-Path $repo 'src/DigitalHeat.AudienceDemo/DigitalHeat.AudienceDemo.csproj'
$app = Start-Process dotnet -ArgumentList @('run','--no-build','-c','Release','--project',('"' + $project + '"')) -WorkingDirectory $repo -RedirectStandardOutput $appLog -RedirectStandardError $appErr -WindowStyle Hidden -PassThru
$ready = $false
for ($i=0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    if ($app.HasExited) { throw "Audience service exited. See $appErr" }
    try { $ready = (Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:18970/healthz' -TimeoutSec 2).StatusCode -eq 200 } catch {}
    if ($ready) { break }
}
if (!$ready) { throw "Audience service did not become ready. See $appErr" }
[IO.File]::WriteAllText((Join-Path $state 'app.pid'),[string]$app.Id)
$tunnelLog = Join-Path $state 'tunnel.log'
$tunnelErr = Join-Path $state 'tunnel-error.log'
$cf = Start-Process $tunnel -ArgumentList @('tunnel','--url','http://127.0.0.1:18970') -WorkingDirectory $state -RedirectStandardOutput $tunnelLog -RedirectStandardError $tunnelErr -WindowStyle Hidden -PassThru
$public = $null
for ($i=0; $i -lt 40; $i++) {
    Start-Sleep -Seconds 1
    if ($cf.HasExited) { throw "Tunnel exited. See $tunnelErr" }
    $log = Get-Content $tunnelErr -Raw -ErrorAction SilentlyContinue
    if ($log -match 'https://[a-z0-9-]+\.trycloudflare\.com') { $public = $Matches[0]; break }
}
if (!$public) { throw "No public URL from tunnel. See $tunnelErr" }
[IO.File]::WriteAllText((Join-Path $state 'tunnel.pid'),[string]$cf.Id)
Write-Host "Laptop display: http://127.0.0.1:18970/operator"
Write-Host "Public question page: $public/ask"
Write-Host 'Open the laptop display full-screen. Set the Lovable DEMO_BASE_URL to the public URL without /ask.'
Write-Host 'Stop later with: powershell -ExecutionPolicy Bypass -File .\scripts\Stop-AudienceApertus.ps1'
Start-Process 'http://127.0.0.1:18970/operator'
