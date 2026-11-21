param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$local=Join-Path $PSScriptRoot 'TestResults'
New-Item -ItemType Directory -Force $local | Out-Null
function Invoke-LabDocker {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$DockerArgs)
    & docker.exe --context desktop-linux @DockerArgs
    if ($LASTEXITCODE -ne 0) { throw "Docker operation failed: $($DockerArgs[0])" }
}
Push-Location $root
try {
    if (!$SkipBuild) {
        & dotnet publish src/DigitalHeat.WorkloadRun -c Release -r linux-musl-x64 --self-contained true -p:InvariantGlobalization=true -o "$local/publish" --nologo -v:q
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        Copy-Item "$PSScriptRoot/node/start-node.sh" "$local/publish/start-node.sh"
        Invoke-LabDocker @('build','-t','digitalheat-mesh-node:lab','-f',"$PSScriptRoot/node/Dockerfile","$local/publish")
        New-Item -ItemType Directory -Force "$local/batch" | Out-Null
        Copy-Item "$root/jobs/research-sim/ising.py","$PSScriptRoot/batch/run.sh" "$local/batch"
        Invoke-LabDocker @('build','-t','digitalheat-mesh-batch:lab','--build-arg','PYTHON_IMAGE=python:3.12-alpine@sha256:c5ba3deef00c7758561a0f03c26f44f10589263d40e732b8748e0d1f38f6dd89','-f',"$PSScriptRoot/batch/Dockerfile","$local/batch")
    }
    Invoke-LabDocker @('image','save','-o',"$local/batch.tar",'digitalheat-mesh-batch:lab')
    # Outer containerd reports a manifest ID; classic inner Docker uses the config digest.
    $manifest=(& tar -xOf "$local/batch.tar" manifest.json | ConvertFrom-Json)[0]
    $image='sha256:' + (($manifest.Config -split '/')[-1] -replace '\.json$','')
    $tokenPath=Join-Path $local 'mesh-token.txt'
    if (!(Test-Path $tokenPath)) {
        $bytes=New-Object byte[] 32
        $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        [IO.File]::WriteAllText($tokenPath,([BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant()))
    }
    $env:MESH_TOKEN=[IO.File]::ReadAllText($tokenPath)
    $env:MESH_JOB_IMAGE=$image
    $peers=@{home='http://home:8080';hotel='http://hotel:8080';spa='http://spa:8080'}
    foreach ($id in @('home','hotel','spa')) {
        $demand=@{home=1;hotel=8;spa=6}[$id]
        $config=[ordered]@{
            mesh_id='linux-mesh-lab';id=$id;state_directory='/var/lib/mesh/state';job_image=$image;peers=$peers
            initial_demand_kwh=$demand;cpu_cores=0.5;ram_mib=256;slots=2;job_cpu_cores=0.25;job_ram_mib=128
            job_heat_kwh_per_hour=0.4;server_heat_kwh_per_hour=0.8;offer_lifetime_seconds=20;heartbeat_seconds=5
            tick_seconds=1;request_timeout_seconds=3;queue_capacity=256;max_request_bytes=65536;deadline_seconds=600
            source='Synthetic Linux lab quotas and heat signals; 0.4 kWh/job-hour = case 0.8 kWh/server-hour * 0.25/0.5 CPU share, NOT measured power/heat. 600s experimental research deadline: config/live-job-deadlines.json. Home/hotel/spa are illustrative labels, not stock-derived heat profiles.'
        }
        [IO.File]::WriteAllText("$local/$id.json",($config|ConvertTo-Json -Depth 10))
    }
    Invoke-LabDocker @('compose','-p','digitalheat-mesh','-f',"$PSScriptRoot/compose.yaml",'up','-d')
    # Private inner engines receive the local image without internet or an outer socket mount.
    foreach ($id in @('home','hotel','spa')) {
        $container="digitalheat-mesh-$id-1"
        $ready=$false
        for ($i=0;$i -lt 60;$i++) {
            & docker --context desktop-linux exec $container docker info *> $null
            if ($LASTEXITCODE -eq 0) { $ready=$true; break }
            Start-Sleep -Seconds 1
        }
        if (!$ready) { throw "Inner engine unavailable: $id" }
        Invoke-LabDocker @('cp',"$local/batch.tar",($container+':/var/lib/mesh/batch.tar'))
        Invoke-LabDocker @('exec',$container,'docker','load','-i','/var/lib/mesh/batch.tar')
        Invoke-LabDocker @('exec',$container,'rm','/var/lib/mesh/batch.tar')
    }
    Write-Host 'Private mesh starting: home, hotel, spa. Operator requests use docker exec; run mesh/Test-MeshLab.ps1.'
} finally { Pop-Location; Remove-Item Env:MESH_TOKEN,Env:MESH_JOB_IMAGE -ErrorAction SilentlyContinue }



