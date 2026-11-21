param([switch]$StartMatch, [string]$Node='session-c')
$ErrorActionPreference='Stop'
$options=@{Application='xonotic'; Node=$Node}
if($StartMatch){$options.Variant='match'}
& "$PSScriptRoot/Start-Application.ps1" @options
