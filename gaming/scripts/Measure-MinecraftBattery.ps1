param([switch]$RunCalibration,[switch]$AcceptEula,[switch]$LeaveRunning)
$ErrorActionPreference='Stop'
if(!$RunCalibration -or !$AcceptEula){throw 'Explicit -RunCalibration -AcceptEula is required; this never enables heat admission.'}
. "$PSScriptRoot/Minecraft.Common.ps1"
. "$PSScriptRoot/Minecraft.Power.ps1"
$script:mc=Get-MinecraftLab
$policy=$mc.Config.measurement
$report=Join-Path $mc.Results ('power-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $report | Out-Null
$null=[Windows.Devices.Power.Battery,Windows.Devices.Power,ContentType=WindowsRuntime]
function Read-Capacity {
    $battery=Get-CimInstance -Namespace root/WMI -ClassName BatteryStatus | Select-Object -First 1
    if($battery.PowerOnline -or !$battery.Discharging -or $battery.Charging){throw 'Battery measurement requires continuous unplugged discharge'}
    $energy=[Windows.Devices.Power.Battery]::AggregateBattery.GetReport().RemainingCapacityInMilliwattHours
    if($null -eq $energy -or $energy -le 0){throw 'No valid battery capacity in explicit mWh units'}
    return [pscustomobject]@{observed_utc=[DateTimeOffset]::UtcNow.ToString('o');capacity_mwh=[double]$energy;wmi_discharge_rate_mw=$battery.DischargeRate}
}
function Collect([string]$Name,[int]$Seconds){
    Write-Host "Measurement phase: $Name ($Seconds seconds); keep other activity steady."
    $samples=[Collections.Generic.List[object]]::new()
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds($Seconds)
    do {
        $sample=Read-Capacity;$samples.Add($sample)
        $sample | ConvertTo-Json -Compress | Add-Content -Encoding UTF8 (Join-Path $report ($Name+'.jsonl'))
        Start-Sleep -Seconds $policy.sample_interval_seconds
    } while([DateTimeOffset]::UtcNow -lt $deadline)
    return $samples.ToArray()
}
function Window([string]$Name,[int]$Seconds){
    $samples=@(Collect $Name $Seconds)
    return Get-MinecraftCapacityWindow -Samples $samples
}
$accepted=$false
$startedByThisRun=$false
$completed=$false
try {
    $null=Read-Capacity
    $engineId=Invoke-MinecraftDocker -DockerArguments @('info','--format','{{.ID}}')
    $container=Get-MinecraftContainer
    if($container -and $container.State.Running){& "$PSScriptRoot/Stop-MinecraftCalibration.ps1" | Out-Host}
    Write-Host 'Cooling down before baseline; no heat estimate will be generated automatically.'
    for($i=0;$i -lt $policy.cooldown_seconds;$i++){$null=Read-Capacity;Start-Sleep -Seconds 1}
    $before=Window 'baseline-before' $policy.baseline_seconds
    $startedByThisRun=$true
    & "$PSScriptRoot/Start-MinecraftCalibration.ps1" -AcceptEula -CalibrationOnly | Out-Host
    $status=Get-MinecraftStatus
    $activeRun=Get-MinecraftContainer
    if($status.players.online -ne 0){throw 'The defined idle-server measurement requires no players'}
    Write-Host 'Warm-up excludes startup/world generation and readiness-probe overhead.'
    for($i=0;$i -lt $policy.warmup_seconds;$i++){$null=Read-Capacity;Start-Sleep -Seconds 1}
    $load=Window 'server-idle' $policy.load_seconds
    $status=Get-MinecraftStatus
    if($status.players.online -ne 0){throw 'Player conditions changed; idle-server measurement rejected'}
    $afterRun=Get-MinecraftContainer
    if($afterRun.Id -ne $activeRun.Id -or $afterRun.State.StartedAt -ne $activeRun.State.StartedAt){throw 'Server restarted during measurement; evidence rejected'}
    $runLogs=Invoke-MinecraftDocker -DockerArguments @('logs','--since',$activeRun.State.StartedAt,$activeRun.Id)
    if($runLogs -match 'joined the game|logged in with entity id'){throw 'Player activity occurred during calibration; idle evidence rejected'}
    & "$PSScriptRoot/Stop-MinecraftCalibration.ps1" | Out-Host
    for($i=0;$i -lt $policy.cooldown_seconds;$i++){$null=Read-Capacity;Start-Sleep -Seconds 1}
    $after=Window 'baseline-after' $policy.baseline_seconds
    $comparison=Compare-MinecraftPowerWindows $before $load $after $policy.noise_multiplier $policy.minimum_incremental_w
    $build=Get-Content (Join-Path $mc.Results 'build.json') -Raw | ConvertFrom-Json
    $result=[ordered]@{method_kind='battery-capacity-regression-v1';method='WinRT RemainingCapacityInMilliwattHours linear-regression slope; baseline interpolated at load midpoint';
        source='https://learn.microsoft.com/en-us/uwp/api/windows.devices.power.batteryreport.remainingcapacityinmilliwatthours';
        image_id=$build.image_id;server_version=$mc.Config.version;config_fingerprint=$mc.Fingerprint;docker_context=$mc.Context;docker_engine_id=$engineId;
        server_container_id=$activeRun.Id;server_started_at_utc=$activeRun.State.StartedAt;players_at_boundaries=0;
        before=$before;load=$load;after=$after;comparison=$comparison;policy=$policy;raw_directory=$report;observed_utc=[DateTimeOffset]::UtcNow.ToString('o')}
    $result | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $report 'measurement.json')
    $accepted=$comparison.accepted_electrical_measurement
    $completed=$true
    $comparison | Format-List | Out-Host
    if(!$accepted){Write-Warning 'Electrical effect is inconclusive under the predeclared drift/noise gate. No heat-based admission is enabled.'}
    if($LeaveRunning){& "$PSScriptRoot/Start-MinecraftCalibration.ps1" -AcceptEula -CalibrationOnly | Out-Host}
    Write-Output "Raw measurement and result: $report. Useful heat remains unverified."
} catch {
    [ordered]@{accepted_electrical_measurement=$false;useful_heat_verified=$false;reason=$_.Exception.Message;raw_directory=$report;observed_utc=[DateTimeOffset]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $report 'failed.json')
    throw
} finally {
    if($startedByThisRun -and (!$LeaveRunning -or !$completed)){
        try {
            $owned=Get-MinecraftContainer
            if($owned -and $owned.State.Running){& "$PSScriptRoot/Stop-MinecraftCalibration.ps1" | Out-Host}
        } catch {Write-Warning "Calibration cleanup could not stop the owned server safely: $($_.Exception.Message). Inspect it; original measurement result is preserved."}
    }
}
