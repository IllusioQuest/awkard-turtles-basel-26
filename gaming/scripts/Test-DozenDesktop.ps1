$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/GamingLab.Common.ps1"
$labRoot = Split-Path -Parent $PSScriptRoot
$login = Get-Content (Join-Path $labRoot 'TestResults/dozen-credentials.json') -Raw | ConvertFrom-Json
if ($login.user -ne 'player' -or [string]::IsNullOrWhiteSpace($login.password)) {
    throw 'Invalid local Node C credentials file'
}
$anonymousCode = Get-GamingHttpStatusCode -Url 'http://localhost:18883' -TimeoutSeconds 5
$authenticatedCode = Get-GamingHttpStatusCode -Url 'http://localhost:18883' -Username $login.user -Password $login.password -TimeoutSeconds 5
if ($anonymousCode -ne 401 -or $authenticatedCode -ne 200) {
    throw "Node C login check failed: anonymous HTTP $anonymousCode, local credentials HTTP $authenticatedCode."
}
[pscustomobject]@{
    url = 'http://localhost:18883'
    username = 'player'
    anonymous_http = $anonymousCode
    authenticated_http = $authenticatedCode
    password_field = 'password in gaming/TestResults/dozen-credentials.json'
    browser_hint = 'Use a separate Incognito window; do not use password_a or password_b.'
}
