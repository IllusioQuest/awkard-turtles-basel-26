$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$state = Join-Path $repo 'data/external/audience-demo'
$tunnelPidFile = Join-Path $state 'tunnel.pid'
if (Test-Path $tunnelPidFile) {
    $tunnelId = [int][IO.File]::ReadAllText($tunnelPidFile)
    $tunnel = Get-Process -Id $tunnelId -ErrorAction SilentlyContinue
    if ($tunnel -and $tunnel.ProcessName -eq 'cloudflared') { Stop-Process -Id $tunnelId -Force }
    Remove-Item -LiteralPath $tunnelPidFile -Force
}
$tokenFile = Join-Path $state 'operator-token.txt'
if (Test-Path $tokenFile) {
    try {
        Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:18970/api/operator/shutdown' -Headers @{'X-Operator-Token'=[IO.File]::ReadAllText($tokenFile)} -TimeoutSec 30 | Out-Null
    } catch {
        Write-Warning 'The tunnel is closed, but the local service did not confirm workload cleanup. Inspect app-error.log and the owned Docker containers before restarting.'
        throw
    }
}
$appPidFile = Join-Path $state 'app.pid'
if (Test-Path $appPidFile) { Remove-Item -LiteralPath $appPidFile -Force }
Write-Host 'Audience tunnel closed and owned jobs released.'
