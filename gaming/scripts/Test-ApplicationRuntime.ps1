# Offline integration smoke; only creates/removes its own disposable, non-networked container.
param([string]$Image='alpine:3.22')
$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
$name='digitalheat-runtime-smoke-'+[Guid]::NewGuid().ToString('N').Substring(0,10)
$context=(& docker context show | Out-String).Trim()
if($LASTEXITCODE -ne 0){throw 'Docker context lookup failed'}
$profileFile=Join-Path ([IO.Path]::GetTempPath()) ($name+'.json')
$id=$null
try {
    $id=(& docker --context $context run -d --pull never --name $name --network none --read-only --tmpfs /tmp:rw,mode=1777 --cap-drop ALL --security-opt no-new-privileges --memory 64m --cpus 0.25 --label "com.docker.compose.project=$name" --label com.docker.compose.service=probe $Image sleep 300 | Out-String).Trim()
    if($LASTEXITCODE -ne 0 -or $id -notmatch '^[a-f0-9]{64}$'){throw 'Could not start isolated smoke container'}
    $literal="literal' ; touch /tmp/injected; #"
    $app=@{id='echo'; executable='/bin/echo'; working_directory='/tmp'; user='nobody'; log_path='/tmp/echo.log'; arguments=@($literal); variants=@{}; environment=@{}; required_capabilities=@()}
    $profiles=@{applications=@($app); nodes=@(@{id='probe'; docker_context=$context; compose_project=$name; compose_service='probe'; browser_url='http://localhost:1'; hardware_profile_id='fixture'}); hardware_profiles=@(@{id='fixture'; capabilities=@(); source='Disposable integration fixture, no hardware or heat claim'})}
    function Invoke-Probe {
        $profiles | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $profileFile
        # PS5.1 writes a BOM; .NET File.ReadAllText removes it before parsing.
        & dotnet run --project "$labRoot/runtime/GamingRuntime.csproj" -- $profileFile echo probe --dispatch | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Probe dispatch failed'}
    }
    Invoke-Probe
    $observed=''
    for($attempt=0;$attempt -lt 30;$attempt++){
        $observed=(& docker --context $context exec $id sh -c 'test ! -f /tmp/echo.log || cat /tmp/echo.log' | Out-String).TrimEnd()
        if($observed){break}; Start-Sleep -Milliseconds 100
    }
    if($observed -cne $literal){throw 'Application argv did not round-trip literally'}
    & docker --context $context exec $id test ! -e /tmp/injected
    if($LASTEXITCODE -ne 0){throw 'Shell metacharacters were executed'}
    & docker --context $context exec -d --user nobody $id flock -n /tmp/digitalheat-desktop.lock sleep 30
    if($LASTEXITCODE -ne 0){throw 'Could not dispatch legacy lock holder'}
    $held=$false
    for($attempt=0;$attempt -lt 30;$attempt++){
        & docker --context $context exec --user nobody $id flock -n /tmp/digitalheat-desktop.lock true
        if($LASTEXITCODE -eq 1){$held=$true;break}; Start-Sleep -Milliseconds 100
    }
    if(!$held){throw 'Legacy lock holder did not become ready'}
    $app.log_path='/tmp/blocked.log'
    Invoke-Probe
    Start-Sleep -Milliseconds 500
    & docker --context $context exec $id test ! -e /tmp/blocked.log
    if($LASTEXITCODE -ne 0){throw 'Runtime bypassed an existing legacy session lock'}
    Write-Output 'PASS: literal argv preserved, shell injection absent, legacy session lock respected.'
} finally {
    if($id -match '^[a-f0-9]{64}$'){& docker --context $context rm -f $id | Out-Null}
    if(Test-Path -LiteralPath $profileFile){Remove-Item -LiteralPath $profileFile}
}
