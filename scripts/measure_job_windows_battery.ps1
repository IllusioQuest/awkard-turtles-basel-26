# Measure runtime and whole-laptop energy of a containerised batch job on Windows, on battery.
#
# Windows equivalent of the battery meter in scripts/measure_job.py: it samples the battery
# discharge rate (WMI root\wmi BatteryStatus.DischargeRate, mW) once per second. Only valid while
# the laptop runs on battery (PowerOnline = False). The firmware smooths the value, so short
# phases are blurred; use runs of tens of seconds or more.
#
# Usage (from the repository root, image built first):
#   powershell -File scripts/measure_job_windows_battery.ps1 -Image digitalheat/research-sim `
#     -Cpus 8 -Workers 8 -Runs 2 -Passes 10 -IdleSeconds 120 -OutDir data/raw/measurements/research-sim-ising/energy-2026-11-21
param(
    [string]$Image = "digitalheat/research-sim",
    [int]$Cpus = 8,
    [int]$Workers = 8,
    [int]$Runs = 3,
    [int]$Passes = 1,
    [int]$IdleSeconds = 60,
    [string]$OutDir = "data/raw/measurements/research-sim-ising/energy-2026-11-21"
)
$ErrorActionPreference = "Stop"

function Get-Battery { Get-CimInstance -Namespace root\wmi -ClassName BatteryStatus | Select-Object -First 1 }

New-Item -ItemType Directory -Force $OutDir | Out-Null
if ((Get-Battery).PowerOnline) { throw "Laptop is on mains power; the battery meter is not valid." }

# Idle baseline: same machine, no job running.
$idle = @()
for ($i = 0; $i -lt $IdleSeconds; $i++) { $idle += (Get-Battery).DischargeRate; Start-Sleep -Seconds 1 }
$idleW = ($idle | Measure-Object -Average).Average / 1000

$results = @()
for ($r = 1; $r -le $Runs; $r++) {
    $work = Join-Path (Resolve-Path $OutDir) "work-run$r"
    if (Test-Path $work) { Remove-Item -Recurse -Force $work }
    New-Item -ItemType Directory -Force $work | Out-Null
    docker run --rm -v "${work}:/work" $Image prepare | Out-Null
    $tasks = (Get-ChildItem (Join-Path $work "in") -Filter "task_*.json").Count

    $samples = @()
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process docker -ArgumentList @("run", "--rm", "--cpus", "$Cpus", "-e", "WORKERS=$Workers",
        "-e", "PASSES=$Passes", "-v", "${work}:/work", $Image, "run") -NoNewWindow -PassThru
    while (-not $proc.HasExited) { $samples += (Get-Battery).DischargeRate; Start-Sleep -Seconds 1 }
    $sw.Stop()
    if ((Get-Battery).PowerOnline) { throw "Mains power was connected during run $r." }

    $seconds = $sw.Elapsed.TotalSeconds
    $meanW = ($samples | Measure-Object -Average).Average / 1000
    $results += [pscustomobject]@{
        run = $r; tasks = $tasks * $Passes; runtime_s = [math]::Round($seconds, 1)
        mean_power_w = [math]::Round($meanW, 2); idle_power_w = [math]::Round($idleW, 2)
        energy_gross_wh = [math]::Round($meanW * $seconds / 3600, 4)
        energy_net_wh = [math]::Round(($meanW - $idleW) * $seconds / 3600, 4)
        samples = $samples.Count
    }
    ($samples -join "`n") | Set-Content -Encoding utf8 (Join-Path $OutDir "power-mw-run$r.txt")
}

$results | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $OutDir "summary.json")
$results | Format-Table -AutoSize
