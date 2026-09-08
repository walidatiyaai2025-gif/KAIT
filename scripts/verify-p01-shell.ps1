$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p01-ui'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$currentPhaseText = Get-Content (Join-Path $root 'CURRENT_PHASE.md') -Raw
$p03OrLater = $currentPhaseText -match '\*\*P(0[3-9]|1[0-7])\s+—'

$port = 5081
$baseUrl = "http://127.0.0.1:$port"
$stdout = Join-Path $artifactDir 'server.stdout.log'
$stderr = Join-Path $artifactDir 'server.stderr.log'

$previousBypass = $env:Setup__BypassGateForRegression
$previousRegressionConnection = $env:IdentitySecurity__RegressionConnectionString
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
$previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:Setup__BypassGateForRegression = 'true'
$env:IdentitySecurity__RegressionConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
$env:DOTNET_ENVIRONMENT = 'RegressionTesting'
$env:ASPNETCORE_ENVIRONMENT = 'RegressionTesting'
$process = Start-Process dotnet -ArgumentList @('run','--project','src/GSIP.Web/GSIP.Web.csproj','--configuration','Release','--no-build','--urls',$baseUrl) -WorkingDirectory $root -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru

try {
    $healthy = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            $health = Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2
            if ($health.StatusCode -eq 200) { $healthy = $true; break }
        } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy. See $stderr" }

    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing
    $loginAr = Invoke-WebRequest "$baseUrl/login?culture=ar-KW" -UseBasicParsing
    $login.Content | Set-Content (Join-Path $artifactDir 'login-en.html') -Encoding utf8
    $loginAr.Content | Set-Content (Join-Path $artifactDir 'login-ar.html') -Encoding utf8
    $loginDecoded = [System.Net.WebUtility]::HtmlDecode($login.Content)
    $loginArDecoded = [System.Net.WebUtility]::HtmlDecode($loginAr.Content)

    if ($login.Content -notmatch '<html lang="en" dir="ltr" data-culture-name="en">') { throw 'English login culture/direction is incorrect.' }
    if ($loginAr.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'Arabic login culture/direction is incorrect.' }
    if ($loginArDecoded -notmatch 'بوابة تكامل الخدمات الحكومية') { throw 'Arabic localized product name is missing after HTML decoding.' }
    if ($loginDecoded -notmatch 'Government Services Integration Portal') { throw 'English localized product name is missing.' }

    if ($p03OrLater) {
        if ($loginDecoded -notmatch 'data-auth-state="active"') { throw 'P03+ active login marker is missing.' }
        if ($loginDecoded -match 'type="button" disabled') { throw 'P03+ login still exposes the obsolete disabled shell control.' }
        if ($loginDecoded -notmatch 'name="__RequestVerificationToken"') { throw 'P03+ login POST is missing antiforgery protection.' }

        $handler = [System.Net.Http.HttpClientHandler]::new()
        $handler.AllowAutoRedirect = $false
        $client = [System.Net.Http.HttpClient]::new($handler)
        try {
            $gate = $client.GetAsync("$baseUrl/").GetAwaiter().GetResult()
            if ([int]$gate.StatusCode -notin @(301,302,303,307,308)) { throw "P03+ dashboard did not require authentication. Status: $([int]$gate.StatusCode)" }
            $location = if ($gate.Headers.Location) { $gate.Headers.Location.OriginalString } else { '' }
            if (-not $location.StartsWith('/login', [System.StringComparison]::OrdinalIgnoreCase)) { throw "P03+ dashboard redirect did not target login. Location: $location" }
        }
        finally {
            $client.Dispose()
            $handler.Dispose()
        }
    }
    else {
        $en = Invoke-WebRequest "$baseUrl/?culture=en" -UseBasicParsing
        $ar = Invoke-WebRequest "$baseUrl/?culture=ar-KW" -UseBasicParsing
        $en.Content | Set-Content (Join-Path $artifactDir 'dashboard-en.html') -Encoding utf8
        $ar.Content | Set-Content (Join-Path $artifactDir 'dashboard-ar.html') -Encoding utf8
        $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
        $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
        if ($en.Content -notmatch '<html lang="en" dir="ltr" data-culture-name="en">') { throw 'English shell culture/direction is incorrect.' }
        if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'Arabic shell culture/direction is incorrect.' }
        if ($arDecoded -notmatch 'بوابة تكامل الخدمات الحكومية') { throw 'Arabic localized product name is missing after HTML decoding.' }
        if ($enDecoded -notmatch 'v0\.1\.0') { throw 'Expected visible semantic version v0.1.0 is missing.' }
        if ($loginDecoded -notmatch 'data-auth-state="shell-only"') { throw 'P01 login shell boundary marker is missing.' }
        if ($loginDecoded -notmatch 'disabled') { throw 'P01 login controls must remain disabled before P03.' }
    }

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P01 screenshot evidence.' }

    if ($p03OrLater) {
        $captures = @(
            @{ Name = 'p01-login-en.png'; Url = "$baseUrl/login?culture=en" },
            @{ Name = 'p01-login-ar.png'; Url = "$baseUrl/login?culture=ar-KW" }
        )
    }
    else {
        $captures = @(
            @{ Name = 'p01-dashboard-en.png'; Url = "$baseUrl/?culture=en" },
            @{ Name = 'p01-dashboard-ar.png'; Url = "$baseUrl/?culture=ar-KW" },
            @{ Name = 'p01-login-en.png'; Url = "$baseUrl/login?culture=en" },
            @{ Name = 'p01-login-ar.png'; Url = "$baseUrl/login?culture=ar-KW" }
        )
    }

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
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        [pscustomobject]@{ file = $file.Name; bytes = $file.Length; sha256 = $hash; url = $capture.Url }
    }
    $manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $artifactDir 'screenshot-manifest.json') -Encoding utf8
    Write-Host 'P01 runtime/browser regression verification passed.'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if ($null -eq $previousBypass) { Remove-Item Env:Setup__BypassGateForRegression -ErrorAction SilentlyContinue }
    else { $env:Setup__BypassGateForRegression = $previousBypass }
    if ($null -eq $previousRegressionConnection) { Remove-Item Env:IdentitySecurity__RegressionConnectionString -ErrorAction SilentlyContinue }
    else { $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection }
    if ($null -eq $previousDotnetEnvironment) { Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue }
    else { $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment }
    if ($null -eq $previousAspNetCoreEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue }
    else { $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment }
}
