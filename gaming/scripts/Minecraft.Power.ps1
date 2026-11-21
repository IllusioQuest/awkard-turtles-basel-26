# Pure measurement arithmetic shared by the battery runner and offline fixture tests.
function Get-MinecraftCapacityWindow {
    param([Parameter(Mandatory=$true)][object[]]$Samples)
    if($Samples.Count -lt 3){throw 'At least three energy-capacity samples are required'}
    $times=@($Samples | ForEach-Object {[DateTimeOffset]::Parse($_.observed_utc)})
    $start=$times[0];$elapsed=@($times | ForEach-Object {($_-$start).TotalSeconds})
    for($i=0;$i -lt $Samples.Count;$i++){
        if($null -eq $Samples[$i].capacity_mwh -or [double]$Samples[$i].capacity_mwh -le 0 -or [double]$Samples[$i].capacity_mwh -ge [uint32]::MaxValue -or
            ($i -gt 0 -and ($elapsed[$i] -le $elapsed[$i-1] -or [double]$Samples[$i].capacity_mwh -gt [double]$Samples[$i-1].capacity_mwh))){throw 'Invalid/non-monotonic battery capacity window'}
    }
    $duration=$elapsed[-1]
    $meanTime=($elapsed | Measure-Object -Average).Average
    $meanCapacity=($Samples | Measure-Object capacity_mwh -Average).Average
    $covariance=0.0;$variance=0.0
    for($i=0;$i -lt $Samples.Count;$i++){$dt=$elapsed[$i]-$meanTime;$covariance+=$dt*([double]$Samples[$i].capacity_mwh-$meanCapacity);$variance+=$dt*$dt}
    $slope=$covariance/$variance
    $maxResidual=0.0
    for($i=0;$i -lt $Samples.Count;$i++){$fit=$meanCapacity+$slope*($elapsed[$i]-$meanTime);$maxResidual=[Math]::Max($maxResidual,[Math]::Abs([double]$Samples[$i].capacity_mwh-$fit))}
    # 1 mWh/s = 3.6 W. Bound observed staircase/noise over this window; no independence/confidence claim.
    return [pscustomobject]@{mean_w=(-$slope*3.6);energy_drop_wh=(([double]$Samples[0].capacity_mwh-[double]$Samples[-1].capacity_mwh)/1000);
        duration_seconds=$duration;midpoint_utc=$start.AddSeconds($meanTime).ToString('o');noise_bound_w=($maxResidual*3.6/$duration);samples=$Samples.Count}
}
function Compare-MinecraftPowerWindows {
    param($Before,$Load,$After,[double]$NoiseMultiplier,[double]$MinimumIncrementalW)
    $beforeTime=[DateTimeOffset]::Parse($Before.midpoint_utc);$loadTime=[DateTimeOffset]::Parse($Load.midpoint_utc);$afterTime=[DateTimeOffset]::Parse($After.midpoint_utc)
    if($loadTime -le $beforeTime -or $afterTime -le $loadTime){throw 'Baseline/load time ordering is invalid'}
    $fraction=($loadTime-$beforeTime).TotalSeconds/($afterTime-$beforeTime).TotalSeconds
    $baseline=$Before.mean_w+$fraction*($After.mean_w-$Before.mean_w)
    $delta=$Load.mean_w-$baseline
    $drift=[Math]::Abs($After.mean_w-$Before.mean_w)
    $noise=[Math]::Max($Before.noise_bound_w,[Math]::Max($Load.noise_bound_w,$After.noise_bound_w))
    $threshold=[Math]::Max($MinimumIncrementalW,[Math]::Max($NoiseMultiplier*$drift,$NoiseMultiplier*$noise))
    return [pscustomobject]@{accepted_electrical_measurement=($delta -gt $threshold -and $Before.mean_w -gt 0 -and $Load.mean_w -gt 0 -and $After.mean_w -gt 0);
        baseline_interpolated_w=$baseline;incremental_electric_w=$delta;baseline_drift_w=$drift;noise_bound_w=$noise;required_effect_w=$threshold;
        useful_heat_verified=$false;note='Whole-laptop incremental electrical power, zero-player idle server. Not recovered heat, active-player peak or a statistically independent confidence interval.'}
}

function Get-ValidatedMinecraftElectricalEvidence {
    param([string]$Path,[string]$ImageId,[string]$Fingerprint,[string]$Version,[string]$Context,[string]$EngineId,$Policy)
    $power=Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if($power.comparison.accepted_electrical_measurement -ne $true -or $power.image_id -ne $ImageId -or $power.config_fingerprint -ne $Fingerprint -or
        $power.server_version -ne $Version -or $power.docker_context -ne $Context -or $power.docker_engine_id -ne $EngineId -or
        $power.method_kind -ne 'battery-capacity-regression-v1' -or $power.players_at_boundaries -ne 0){
        throw 'Electrical measurement rejected or mismatched; no heat-based startup profile produced'
    }
    foreach($field in @('sample_interval_seconds','baseline_seconds','warmup_seconds','load_seconds','cooldown_seconds','minimum_incremental_w','noise_multiplier')){
        if($null -eq $power.policy.$field -or [double]$power.policy.$field -ne [double]$Policy.$field){throw 'Measurement acceptance policy does not match the required policy'}
    }
    $directory=[IO.Path]::GetFullPath((Split-Path -Parent ([IO.Path]::GetFullPath($Path))))
    if([IO.Path]::GetFullPath($power.raw_directory) -ne $directory){throw 'Raw evidence must be alongside its measurement report'}
    $windows=@()
    foreach($phase in @('baseline-before','server-idle','baseline-after')){
        $rows=@(Get-Content -LiteralPath (Join-Path $directory ($phase+'.jsonl')) | Where-Object {$_} | ForEach-Object {$_ | ConvertFrom-Json})
        $window=Get-MinecraftCapacityWindow -Samples $rows
        $seconds=if($phase -eq 'server-idle'){$Policy.load_seconds}else{$Policy.baseline_seconds}
        if($window.samples -lt ([Math]::Floor($seconds/$Policy.sample_interval_seconds)-2) -or $window.duration_seconds -lt ($seconds-2*$Policy.sample_interval_seconds) -or
            $window.duration_seconds -gt ($seconds+2*$Policy.sample_interval_seconds)){throw 'Raw measurement window is incomplete or interrupted'}
        $windows+=$window
    }
    $comparison=Compare-MinecraftPowerWindows $windows[0] $windows[1] $windows[2] $Policy.noise_multiplier $Policy.minimum_incremental_w
    if(!$comparison.accepted_electrical_measurement -or [Math]::Abs($comparison.incremental_electric_w-[double]$power.comparison.incremental_electric_w) -gt 0.000001){
        throw 'Electrical measurement rejected when recalculated from raw samples'
    }
    return $comparison
}
