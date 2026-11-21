$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$labRoot=Split-Path -Parent $PSScriptRoot
$login=Get-Content (Join-Path $labRoot 'TestResults/dozen-credentials.json') -Raw | ConvertFrom-Json
if($login.user -ne 'player' -or [string]::IsNullOrWhiteSpace($login.password)) { throw 'Invalid local Node C credentials file' }
$client=[Net.Http.HttpClient]::new()
$client.Timeout=[TimeSpan]::FromSeconds(5)
try {
    $anonymous=$client.GetAsync('http://localhost:18883').GetAwaiter().GetResult()
    try { $anonymousCode=[int]$anonymous.StatusCode } finally { $anonymous.Dispose() }
    $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Basic',
        [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("player:$($login.password)")))
    $authenticated=$client.GetAsync('http://localhost:18883').GetAwaiter().GetResult()
    try { $authenticatedCode=[int]$authenticated.StatusCode } finally { $authenticated.Dispose() }
    if($anonymousCode -ne 401 -or $authenticatedCode -ne 200) {
        throw "Node C login check failed: anonymous HTTP $anonymousCode, local credentials HTTP $authenticatedCode."
    }
    [pscustomobject]@{url='http://localhost:18883';username='player';anonymous_http=$anonymousCode;authenticated_http=$authenticatedCode;
        password_field='password in gaming/TestResults/dozen-credentials.json';browser_hint='Use a separate Incognito window; do not use password_a or password_b.'}
} finally { $client.Dispose(); $login=$null }
