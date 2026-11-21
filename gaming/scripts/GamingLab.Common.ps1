# Internal helpers shared by the existing entry-point scripts; compatible with PowerShell 5.1.
function Get-GamingContainerId {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet('session-a')][string]$Node,
        [switch]$IncludeStopped
    )

    $project = 'digitalheat-gaming'
    $listing = if ($IncludeStopped) { '-aq' } else { '-q' }
    $ids = @(& docker ps $listing --filter "label=com.docker.compose.project=$project" --filter "label=com.docker.compose.service=$Node")
    if ($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -notmatch '^[a-f0-9]{12,64}$') {
        throw "Expected exactly one lab container for $Node in $project."
    }
    return [string]$ids[0]
}

function Get-GamingHttpStatusCode {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [string]$Username,
        [string]$Password,
        [int]$TimeoutSeconds = 30
    )

    # Unlike Invoke-WebRequest -SkipHttpErrorCheck, this also works in Windows PowerShell 5.1.
    Add-Type -AssemblyName System.Net.Http
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    try {
        if ($Username) {
            $token = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${Username}:$Password"))
            $client.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Basic', $token)
        }
        $response = $client.GetAsync($Url).GetAwaiter().GetResult()
        try { return [int]$response.StatusCode } finally { $response.Dispose() }
    } finally { $client.Dispose() }
}
