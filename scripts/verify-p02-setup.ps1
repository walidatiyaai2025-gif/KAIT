$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p02-setup'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$statePath = Join-Path $root 'src/GSIP.Web/App_Data'
if (Test-Path $statePath) { Remove-Item $statePath -Recurse -Force }

$port = 5082
$baseUrl = "http://127.0.0.1:$port"
$stdout = Join-Path $artifactDir 'server.stdout.log'
$stderr = Join-Path $artifactDir 'server.stderr.log'
$process = Start-Process dotnet -ArgumentList @('run','--project','src/GSIP.Web/GSIP.Web.csproj','--configuration','Release','--no-build','--urls',$baseUrl) -WorkingDirectory $root -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru

try {
    $healthy = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            $health = Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2
            if ($health.StatusCode -eq 200) { $healthy = $true; break }
        } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy. See $stderr" }

    # Invoke-WebRequest treats -MaximumRedirection 0 as an error on a valid 3xx response.
    # Use HttpClient with redirects disabled so the gate assertion can inspect the first response directly.
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $gate = $client.GetAsync("$baseUrl/login?culture=en").GetAwaiter().GetResult()
        $gateStatusCode = [int]$gate.StatusCode
        if ($gateStatusCode -notin @(301,302,303,307,308)) { throw "Login did not return a redirect before setup completion. Status: $gateStatusCode" }
        $location = if ($gate.Headers.Location) { $gate.Headers.Location.OriginalString } else { '' }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }

    if ([string]::IsNullOrWhiteSpace($location) -or -not $location.StartsWith('/setup', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Login redirect did not target /setup before setup completion. Location: $location"
    }

    $gatedWelcome = Invoke-WebRequest "$baseUrl$location" -UseBasicParsing
    if ($gatedWelcome.Content -notmatch 'data-setup-step="Welcome"') { throw 'First-run Welcome screen was not returned after following the setup gate.' }

    $en = Invoke-WebRequest "$baseUrl/setup?culture=en" -UseBasicParsing
    $ar = Invoke-WebRequest "$baseUrl/setup?culture=ar-KW" -UseBasicParsing
    $database = Invoke-WebRequest "$baseUrl/setup?step=Database&culture=en" -UseBasicParsing
    $en.Content | Set-Content (Join-Path $artifactDir 'setup-welcome-en.html') -Encoding utf8
    $ar.Content | Set-Content (Join-Path $artifactDir 'setup-welcome-ar.html') -Encoding utf8
    $database.Content | Set-Content (Join-Path $artifactDir 'setup-database-en.html') -Encoding utf8

    if ($en.Content -notmatch '<html lang="en" dir="ltr" data-culture-name="en">') { throw 'English setup direction/culture is incorrect.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'Arabic setup direction/culture is incorrect.' }
    if ($en.Content -notmatch 'GSIP Setup Wizard') { throw 'English setup wizard title is missing.' }
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    if ($arDecoded -notmatch 'معالج إعداد بوابة GSIP') { throw 'Arabic setup wizard title is missing.' }
    if ($en.Content -notmatch 'data-setup-step="Welcome"' -or $ar.Content -notmatch 'data-setup-step="Welcome"') { throw 'Setup step marker is missing.' }
    if ($database.Content -notmatch 'Test Connection' -or $database.Content -notmatch 'SQL Authentication') { throw 'Database setup controls are missing from runtime output.' }
    if ($database.Content -match 'name="Password"[^>]+value="[^"\s]+"') { throw 'Database password field rendered a value back to the client.' }

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P02 screenshot evidence.' }

    $captures = @(
        @{ Name = 'p02-setup-welcome-en.png'; Url = "$baseUrl/setup?culture=en" },
        @{ Name = 'p02-setup-welcome-ar.png'; Url = "$baseUrl/setup?culture=ar-KW" },
        @{ Name = 'p02-setup-database-en.png'; Url = "$baseUrl/setup?step=Database&culture=en" },
        @{ Name = 'p02-setup-database-ar.png'; Url = "$baseUrl/setup?step=Database&culture=ar-KW" }
    )
    foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        $arguments = @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars','--window-size=1440,1000',"--screenshot=$path",$capture.Url)
        $browserProcess = Start-Process $browser -ArgumentList $arguments -Wait -PassThru
        if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $path)) { throw "Browser screenshot failed: $($capture.Name)" }
        if ((Get-Item $path).Length -lt 10000) { throw "Screenshot is unexpectedly small: $($capture.Name)" }
    }

    $manifest = foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        $file = Get-Item $path
        [pscustomobject]@{
            file = $file.Name
            bytes = $file.Length
            sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
            url = $capture.Url
        }
    }
    $manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $artifactDir 'screenshot-manifest.json') -Encoding utf8
    Write-Host 'P02 first-run redirect and bilingual browser verification passed.'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (Test-Path $statePath) { Remove-Item $statePath -Recurse -Force }
}
