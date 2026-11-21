$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
$reportRoot=Join-Path $labRoot 'TestResults'
$image=(& docker image inspect digitalheat-selkies-dzn:experiment --format '{{.Id}}' | Out-String).Trim()
if($LASTEXITCODE -ne 0 -or $image -notmatch '^sha256:[a-f0-9]{64}$') { throw 'Build the Dozen image first' }
$enumeration=Get-Content (Join-Path $reportRoot 'dozen-vulkan-summary.json') -Raw | ConvertFrom-Json
$render=Get-Content (Join-Path $reportRoot 'dozen-render.json') -Raw | ConvertFrom-Json
if($enumeration.image -ne $image -or $render.image -ne $image -or $enumeration.exit_code -ne 0 -or
    -not $enumeration.intel_device_enumerated -or $render.exit_code -ne 0 -or -not $render.intel_device_enumerated) {
    throw 'Run enumeration and -RenderSmoke successfully against the current image before starting desktop C'
}
$credentialsPath=Join-Path $reportRoot 'dozen-credentials.json'
if(Test-Path $credentialsPath) { $credentials=Get-Content $credentialsPath -Raw | ConvertFrom-Json }
else { $credentials=@{user='player';password=[Guid]::NewGuid().ToString('N')}; $credentials|ConvertTo-Json|Set-Content $credentialsPath }
$previousImage=$env:GAMING_DOZEN_IMAGE; $previousPassword=$env:GAMING_DOZEN_PASSWORD
try {
    $env:GAMING_DOZEN_IMAGE=$image; $env:GAMING_DOZEN_PASSWORD=$credentials.password
    & docker compose -f "$labRoot/deploy/dozen-desktop.compose.yaml" up -d
    if($LASTEXITCODE -ne 0){throw 'Desktop C startup failed'}
    Write-Output 'Experimental desktop C: http://localhost:18883 (A/B unchanged)'
    Write-Output "Credentials: $credentialsPath"
} finally { $env:GAMING_DOZEN_IMAGE=$previousImage; $env:GAMING_DOZEN_PASSWORD=$previousPassword }
