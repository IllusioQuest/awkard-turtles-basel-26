param([string]$OutputPath,[string]$SourceRoot,[ValidateSet('Placement','NodeFeedback','PeerMesh')][string]$Probe='Placement')
$ErrorActionPreference='Stop'
$harnessRoot=Split-Path -Parent $PSScriptRoot
$root=if($SourceRoot){[IO.Path]::GetFullPath($SourceRoot)}else{$harnessRoot}
if(!$OutputPath){$OutputPath=Join-Path $root 'results/performance/scheduler-latency.json'}
$project=Join-Path $env:TEMP ('digitalheat-scheduler-probe-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $project | Out-Null
function Xml([string]$s){[Security.SecurityElement]::Escape($s)}
$core=Xml (Join-Path $root 'src/DigitalHeat.Core/DigitalHeat.Core.csproj')
$network=Xml (Join-Path $root 'src/DigitalHeat.Network/DigitalHeat.Network.csproj')
$orchestration=Xml (Join-Path $root 'src/DigitalHeat.Orchestration/DigitalHeat.Orchestration.csproj')
$probeFile=switch($Probe){'NodeFeedback'{'NodeFeedbackProbe.cs'} 'PeerMesh'{'PeerMeshProbe.cs'} default{'SchedulerProbe.cs'}}
$probePath=Join-Path $harnessRoot "scripts/performance/$probeFile"
$source=Xml $probePath
@"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
 <ItemGroup><ProjectReference Include="$core"/><ProjectReference Include="$network"/><ProjectReference Include="$orchestration"/><Compile Include="$source" Link="Program.cs"/></ItemGroup>
</Project>
"@ | Set-Content -Encoding UTF8 (Join-Path $project 'Probe.csproj')
$commit=(& git -C $root rev-parse HEAD | Out-String).Trim()
if($LASTEXITCODE -ne 0){throw 'Cannot identify source commit'}
& dotnet build (Join-Path $project 'Probe.csproj') -c Release --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Diagnostic build failed'}
# Avoid Tier-0 startup effects; measure optimized steady-state, not first-call JIT latency.
$previousTiering=$env:DOTNET_TieredCompilation
try {
    $env:DOTNET_TieredCompilation='0'
    & dotnet (Join-Path $project 'bin/Release/net8.0/Probe.dll') ([IO.Path]::GetFullPath($OutputPath)) $commit $root
    if($LASTEXITCODE -ne 0){throw 'Scheduler probe failed'}
} finally {$env:DOTNET_TieredCompilation=$previousTiering}
$report=Get-Content $OutputPath -Raw | ConvertFrom-Json
$cpuName=(Get-CimInstance Win32_Processor | Select-Object -First 1).Name
$report | Add-Member -NotePropertyName machine_cpu_name -NotePropertyValue $cpuName
$report | Add-Member -NotePropertyName probe_source_sha256 -NotePropertyValue (Get-FileHash $probePath -Algorithm SHA256).Hash.ToLowerInvariant()
$report | Add-Member -NotePropertyName tiered_compilation -NotePropertyValue 'Disabled explicitly (DOTNET_TieredCompilation=0); optimized steady-state, not default startup behavior'
$report | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 $OutputPath
Write-Output "Saved diagnostic report: $OutputPath"
