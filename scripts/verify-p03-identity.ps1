$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p03-identity'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$statePath = Join-Path $root 'src/GSIP.Web/App_Data'
if (Test-Path $statePath) { Remove-Item $statePath -Recurse -Force }

$port = 5083
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
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            $health = Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2
            if ($health.StatusCode -eq 200) { $healthy = $true; break }
        } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy. See $stderr" }

    $en = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing
    $ar = Invoke-WebRequest "$baseUrl/login?culture=ar-KW" -UseBasicParsing
    $en.Content | Set-Content (Join-Path $artifactDir 'login-en.html') -Encoding utf8
    $ar.Content | Set-Content (Join-Path $artifactDir 'login-ar.html') -Encoding utf8
    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)

    if ($en.Content -notmatch '<html lang="en" dir="ltr" data-culture-name="en">') { throw 'P03 English login direction/culture is incorrect.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'P03 Arabic login direction/culture is incorrect.' }
    if ($enDecoded -notmatch 'data-auth-state="active"') { throw 'P03 active login marker is missing.' }
    if ($enDecoded -match 'type="button" disabled') { throw 'P03 login still contains the disabled P01 shell control.' }
    if ($enDecoded -notmatch 'name="Username"' -or $enDecoded -notmatch 'name="Password"') { throw 'P03 enabled username/password fields are missing.' }
    if ($enDecoded -notmatch 'name="__RequestVerificationToken"') { throw 'P03 login antiforgery token is missing.' }
    if ($arDecoded -notmatch 'دخول آمن إلى الحساب') { throw 'P03 Arabic identity localization is missing.' }
    if ($en.Headers['X-Frame-Options'] -ne 'DENY') { throw 'P03 security headers are not wired into the runtime pipeline.' }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $gate = $client.GetAsync("$baseUrl/").GetAwaiter().GetResult()
        if ([int]$gate.StatusCode -notin @(301,302,303,307,308)) { throw "Unauthenticated dashboard bypass was not blocked. Status: $([int]$gate.StatusCode)" }
        $location = if ($gate.Headers.Location) { $gate.Headers.Location } else { $null }
        $locationPath = if ($null -eq $location) { '' } elseif ($location.IsAbsoluteUri) { $location.AbsolutePath } else { $location.OriginalString.Split('?')[0] }
        if (-not $locationPath.StartsWith('/login', [System.StringComparison]::OrdinalIgnoreCase)) { throw "Unauthenticated dashboard did not redirect to login. Location: $location" }

        $pairs = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]]::new()
        $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('Username', 'synthetic-csrf-user'))
        $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('Password', 'SyntheticPasswordNotSubmittedToService1!'))
        $body = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
        $csrf = $client.PostAsync("$baseUrl/login", $body).GetAwaiter().GetResult()
        if ([int]$csrf.StatusCode -ne 400) { throw "Login POST without antiforgery token was not rejected with HTTP 400. Status: $([int]$csrf.StatusCode)" }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P03 screenshot evidence.' }

    $captures = @(
        @{ Name = 'p03-login-en-desktop.png'; Url = "$baseUrl/login?culture=en"; Size = '1440,1000' },
        @{ Name = 'p03-login-ar-desktop.png'; Url = "$baseUrl/login?culture=ar-KW"; Size = '1440,1000' },
        @{ Name = 'p03-login-en-mobile.png'; Url = "$baseUrl/login?culture=en"; Size = '390,844' },
        @{ Name = 'p03-login-ar-mobile.png'; Url = "$baseUrl/login?culture=ar-KW"; Size = '390,844' }
    )
    foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        $arguments = @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars',"--window-size=$($capture.Size)","--screenshot=$path",$capture.Url)
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
            viewport = $capture.Size
            phase = 'P03'
        }
    }
    $manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $artifactDir 'screenshot-manifest.json') -Encoding utf8
    Write-Host 'P03 auth gate, CSRF, security-header and bilingual responsive browser verification passed.'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (Test-Path $statePath) { Remove-Item $statePath -Recurse -Force }
    if ($null -eq $previousBypass) { Remove-Item Env:Setup__BypassGateForRegression -ErrorAction SilentlyContinue }
    else { $env:Setup__BypassGateForRegression = $previousBypass }
    if ($null -eq $previousRegressionConnection) { Remove-Item Env:IdentitySecurity__RegressionConnectionString -ErrorAction SilentlyContinue }
    else { $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection }
    if ($null -eq $previousDotnetEnvironment) { Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue }
    else { $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment }
    if ($null -eq $previousAspNetCoreEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue }
    else { $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment }
}
