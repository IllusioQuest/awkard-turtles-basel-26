param([ValidateSet('linux-x64','linux-arm64')][string]$Runtime='linux-x64')
$ErrorActionPreference='Stop'
# Official Blender archive in this preset is x64; never claim ARM GPU compatibility.
if($Runtime -ne 'linux-x64'){throw 'The pinned Blender image is x64; supply and verify a separate ARM image first.'}
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $PSScriptRoot ('TestResults/linux-worker-'+[guid]::NewGuid().ToString('N'))
# Fresh staging prevents a prior local node.json, token or state file entering the archive.
New-Item -ItemType Directory -Force "$out/app" | Out-Null
dotnet publish "$root/src/DigitalHeat.WorkloadRun" -c Release -r $Runtime --self-contained true -p:InvariantGlobalization=true -o "$out/app" --nologo -v:q
if($LASTEXITCODE -ne 0){throw 'Worker publish failed'}
Copy-Item "$PSScriptRoot/render.html" "$out/app/render.html"
foreach($name in @('landscape.py','native_worker.py','scene_builder.py','scene_settings.json')){Copy-Item "$PSScriptRoot/$name" "$out/$name"}
Copy-Item "$PSScriptRoot/linux-worker/*" $out
Get-ChildItem $out -Filter '*.sh' | ForEach-Object {
 $body=[IO.File]::ReadAllText($_.FullName).Replace("`r`n","`n")
 [IO.File]::WriteAllText($_.FullName,$body,(New-Object Text.UTF8Encoding $false))
}
$archive=Join-Path $PSScriptRoot 'TestResults/linux-render-worker.tar.gz'
& tar -czf $archive -C $out .
if($LASTEXITCODE -ne 0){throw 'Bundle archive failed'}
Write-Host "Prepared $archive. No remote host was contacted; GPU verification is still required."
