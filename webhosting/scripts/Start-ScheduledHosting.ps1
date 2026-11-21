param([switch]$Build, [double]$SignalValiditySeconds = 3600)
$ErrorActionPreference = 'Stop'
if (-not [double]::IsFinite($SignalValiditySeconds) -or $SignalValiditySeconds -le 0) { throw 'Signal validity must be positive' }
$labRoot = Split-Path -Parent $PSScriptRoot
$state = Join-Path $labRoot 'TestResults/hosting-state'
New-Item -ItemType Directory -Force $state | Out-Null
# Initial lab provisioning is explicit. Later scheduling needs only atomic plan updates, no restart.
& docker compose -f "$labRoot/deploy/scheduler.compose.yaml" stop gateway-a gateway-b monitor-a monitor-b
if ($LASTEXITCODE -ne 0) { throw 'Cannot stop this lab controllers before provisioning' }
$plan = Get-Content "$labRoot/deploy/hosting-plan.example.json" -Raw | ConvertFrom-Json -AsHashtable
$plan.valid_until_utc = [DateTimeOffset]::UtcNow.AddSeconds($SignalValiditySeconds).ToString('o')
$plan | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $state 'plan.tmp')
Move-Item -LiteralPath (Join-Path $state 'plan.tmp') -Destination (Join-Path $state 'plan.json') -Force
& "$PSScriptRoot/Start-IsolatedNetwork.ps1" -Topology scheduler -Build:$Build
