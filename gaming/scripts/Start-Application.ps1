param(
    [Parameter(Mandatory=$true)][string]$Application,
    [Parameter(Mandatory=$true)][string]$Node,
    [string]$Variant,
    [string]$Profiles,
    [switch]$PlanOnly
)
$ErrorActionPreference='Stop'
$labRoot=Split-Path -Parent $PSScriptRoot
if(!$Profiles){$Profiles=Join-Path $labRoot 'deploy/runtime-profiles.json'}
$runtimeArguments=@($Profiles,$Application,$Node)
if($Variant){$runtimeArguments+="--variant=$Variant"}
if(!$PlanOnly){$runtimeArguments+='--dispatch'}
& dotnet run --project "$labRoot/runtime/GamingRuntime.csproj" -- @runtimeArguments
if($LASTEXITCODE -ne 0){throw 'Application runtime request failed.'}
if(!$PlanOnly){Write-Output 'Dispatch acknowledged; verify the browser/log separately. An occupied desktop refuses a second launch.'}
