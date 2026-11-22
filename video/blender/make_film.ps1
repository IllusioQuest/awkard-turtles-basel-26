# Build, render and encode the intro film (Windows PowerShell).
#   .\video\blender\make_film.ps1                 # preview: 960x540, 4 samples, every 2nd frame
#   .\video\blender\make_film.ps1 -Preset final   # 1920x1080, 64 samples, all frames
# Optional: -Blender "C:\...\blender.exe"  -Python python
# Needs Blender 4.x, Python 3 with Pillow (pip install pillow) and ffmpeg on the PATH.
param(
    [ValidateSet('preview', 'final')][string]$Preset = 'preview',
    [string]$Blender = $env:BLENDER,
    [string]$Python = $(if ($env:PYTHON) { $env:PYTHON } else { 'python' })
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $Blender) {
    $cmd = Get-Command blender -ErrorAction SilentlyContinue
    if ($cmd) { $Blender = $cmd.Source }
    else {
        $found = Get-ChildItem "$env:ProgramFiles\Blender Foundation\Blender 4*\blender.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        if (-not $found) { throw "Blender 4.x not found. Pass -Blender 'C:\path\to\blender.exe'." }
        $Blender = $found.FullName
    }
}
Write-Host "Blender: $Blender"

if (-not (Test-Path data/film-data.json) -or -not (Test-Path numbers.json)) {
    & $Python prepare_data.py
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}
if ((Split-Path $Blender -Leaf) -like 'python*') {
    & $Blender render.py --preset $Preset
} else {
    & $Blender -b --factory-startup -P render.py -- --preset $Preset
}
if ($LASTEXITCODE) { exit $LASTEXITCODE }
& $Python post.py --preset $Preset
exit $LASTEXITCODE
