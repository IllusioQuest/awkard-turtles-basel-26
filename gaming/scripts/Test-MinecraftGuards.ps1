$ErrorActionPreference='Stop'
. "$PSScriptRoot/Minecraft.Common.ps1"
$script:mc=Get-MinecraftLab
$build=Get-Content (Join-Path $mc.Results 'build.json') -Raw | ConvertFrom-Json
function Refuses-Startup([string[]]$Environment){
    $parameters=@('run','--rm','--pull','never','--network','none','--read-only','--user',([string]$mc.Config.lab.user_id),'--cap-drop','ALL','--security-opt','no-new-privileges:true','--cpus','0.25','--memory','128m')
    foreach($entry in $Environment){$parameters+=@('--env',$entry)}
    $old=$ErrorActionPreference
    try {$ErrorActionPreference='Continue'; $null=& docker --context $mc.Context @parameters $build.image_id 2>&1; $code=$LASTEXITCODE}
    finally {$ErrorActionPreference=$old}
    if($code -ne 64){throw "Expected startup guard exit 64, got $code"}
}
Refuses-Startup @()
Refuses-Startup @('MC_EULA_ACCEPTED=true','MC_HEAP_MIN_MIB=invalid','MC_HEAP_MAX_MIB=2048')
# Use the actual inconclusive evidence when present: exporting a scheduled profile must refuse it.
$measurement=Get-ChildItem -LiteralPath $mc.Results -Filter measurement.json -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if($measurement){
    $evidence=Get-Content -LiteralPath $measurement.FullName -Raw | ConvertFrom-Json
    if($evidence.comparison.accepted_electrical_measurement -eq $false){
        $output=Join-Path $mc.Results ('must-not-export-'+[Guid]::NewGuid().ToString('N')+'.json')
        $refused=$false
        try {& "$PSScriptRoot/New-MinecraftWorkload.ps1" -BaseConfig 'not-read.json' -NodeId 'not-read' -OutputPath $output -PowerMeasurement $measurement.FullName -ReviewedRecoveryModel 'not-read.json' -AcceptEula | Out-Null}
        catch {if($_.Exception.Message -match 'measurement rejected'){$refused=$true}else{throw}}
        if(!$refused -or (Test-Path -LiteralPath $output)){throw 'Rejected measurement enabled profile export'}
    }
}
Write-Output 'PASS: no implicit EULA acceptance, invalid heap refusal and rejected-measurement startup gate.'
