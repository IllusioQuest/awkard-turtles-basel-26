$ErrorActionPreference='Stop'
. "$PSScriptRoot/Minecraft.Power.ps1"
function Samples([int]$StartSeconds,[double]$Watts){
    $start=[DateTimeOffset]::Parse('2026-01-01T00:00:00Z').AddSeconds($StartSeconds)
    return @(0..6 | ForEach-Object {[pscustomobject]@{observed_utc=$start.AddSeconds($_*10).ToString('o');capacity_mwh=(30000-$Watts/3.6*$_*10)}})
}
$before=Get-MinecraftCapacityWindow (Samples 0 36)
$load=Get-MinecraftCapacityWindow (Samples 120 54)
$after=Get-MinecraftCapacityWindow (Samples 240 36)
$result=Compare-MinecraftPowerWindows $before $load $after 2 1
if(!$result.accepted_electrical_measurement -or [Math]::Abs($result.incremental_electric_w-18) -gt 0.0001 -or $result.useful_heat_verified){throw 'Known electrical difference or useful-heat guard failed'}
$drift=Get-MinecraftCapacityWindow (Samples 240 72)
if((Compare-MinecraftPowerWindows $before $load $drift 2 1).accepted_electrical_measurement){throw 'Drift/no-effect measurement accepted'}
$bad=Samples 0 36; $bad[3].capacity_mwh=$bad[2].capacity_mwh+1
$rejected=$false
try {Get-MinecraftCapacityWindow $bad | Out-Null} catch {$rejected=$true}
if(!$rejected){throw 'Non-monotonic energy data accepted'}
$fixtures=Join-Path (Split-Path -Parent $PSScriptRoot) ('TestResults/minecraft-math-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
$policy=[pscustomobject]@{sample_interval_seconds=10;baseline_seconds=60;warmup_seconds=30;load_seconds=60;cooldown_seconds=30;minimum_incremental_w=1;noise_multiplier=2}
$phases=@('baseline-before','server-idle','baseline-after')
$watts=@(36,54,36)
for($i=0;$i -lt 3;$i++){
    Samples ($i*120) $watts[$i] | ForEach-Object {$_ | ConvertTo-Json -Compress} | Set-Content -Encoding UTF8 (Join-Path $fixtures ($phases[$i]+'.jsonl'))
}
$evidence=[pscustomobject]@{method_kind='battery-capacity-regression-v1';image_id='synthetic-fixture';config_fingerprint='fixture';server_version='fixture';docker_context='fixture';docker_engine_id='fixture';
    players_at_boundaries=0;comparison=$result;policy=$policy;raw_directory=$fixtures;note='Synthetic math fixture, not an actual hardware measurement'}
$path=Join-Path $fixtures 'fixture.json'
$evidence | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $path
$validated=Get-ValidatedMinecraftElectricalEvidence $path 'synthetic-fixture' 'fixture' 'fixture' 'fixture' 'fixture' $policy
if([Math]::Abs($validated.incremental_electric_w-18) -gt 0.0001){throw 'Raw evidence recomputation failed'}
# A claimed acceptance with a flat post-baseline must not bypass recomputation.
Samples 240 0 | ForEach-Object {$_ | ConvertTo-Json -Compress} | Set-Content -Encoding UTF8 (Join-Path $fixtures 'baseline-after.jsonl')
$rejected=$false
try {Get-ValidatedMinecraftElectricalEvidence $path 'synthetic-fixture' 'fixture' 'fixture' 'fixture' 'fixture' $policy | Out-Null} catch {$rejected=$true}
if(!$rejected){throw 'Tampered acceptance flag bypassed raw evidence checks'}
Write-Output 'PASS: capacity units/slope, interpolation, drift/flat-window rejection, monotonicity, raw-evidence recomputation and useful-heat gate.'
