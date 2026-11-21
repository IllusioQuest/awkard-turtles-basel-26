param([string]$OutputPath = (Join-Path $env:TEMP 'digitalheat-gaming-compatibility.json'))
$ErrorActionPreference = 'Stop'
function Capture([string]$Command, [string[]]$Arguments) {
    $result = & $Command @Arguments 2>&1 | Out-String
    return @{ exit_code = $LASTEXITCODE; output = $result.Trim() }
}
$report = [ordered]@{
    observed_utc = [DateTimeOffset]::UtcNow.ToString('o')
    scope = 'Discovery only: device presence does not prove rendering, Vulkan, encoding or CS2 compatibility.'
    windows_gpus = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion)
    docker = Capture docker @('version','--format','{{json .Server}}')
    wsl = Capture wsl @('--list','--verbose')
    wsl_graphics_paths = Capture wsl @('-d','docker-desktop','--','sh','-c','ls -ld /dev/dxg /dev/dri /usr/lib/wsl/lib /mnt/wslg 2>&1')
    steam_installed = Test-Path 'C:/Program Files (x86)/Steam/steam.exe'
    next_checks = @('Container graphics device/library access','Actual Vulkan renderer','Actual H.264 encode','Browser stream and input','Original CS2 compatibility and performance')
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath
Write-Output "Discovery report: $OutputPath"
$report | ConvertTo-Json -Depth 8
