$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p01-ui'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$port = 5081
$baseUrl = "http://127.0.0.1:$port"
$stdout = Join-Path $artifactDir 'server.stdout.log'
$stderr = Join-Path $artifactDir 'server.stderr.log'

# Closed P01 regression needs to inspect the shell directly after P02 introduces the
# first-run gate. The application accepts this bypass only in the dedicated RegressionTesting environment.
$previousBypass = $env:Setup__BypassGateForRegression
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
$previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:Setup__BypassGateForRegression = 'true'
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

    $en = Invoke-WebRequest "$baseUrl/?culture=en" -UseBasicParsing
    $ar = Invoke-WebRequest "$baseUrl/?culture=ar-KW" -UseBasicParsing
    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing

    $en.Content | Set-Content (Join-Path $artifactDir 'dashboard-en.html') -Encoding utf8
    $ar.Content | Set-Content (Join-Path $artifactDir 'dashboard-ar.html') -Encoding utf8
    $login.Content | Set-Content (Join-Path $artifactDir 'login-en.html') -Encoding utf8

    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    $loginDecoded = [System.Net.WebUtility]::HtmlDecode($login.Content)

    if ($en.Content -notmatch '<html lang="en" dir="ltr" data-culture-name="en">') { throw 'English shell culture/direction is incorrect.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'Arabic shell culture/direction is incorrect.' }
    if ($arDecoded -notmatch 'بوابة تكامل الخدمات الحكومية') { throw 'Arabic localized product name is missing after HTML decoding.' }
    if ($arDecoded -match '>ProductName<') { throw 'Arabic resource lookup fell back to a resource key.' }
    if ($enDecoded -notmatch 'Government Services Integration Portal') { throw 'English localized product name is missing.' }
    if ($enDecoded -match '@Model\.ProductVersion') { throw 'Razor model expression leaked as literal text.' }
    if ($enDecoded -notmatch 'v0\.1\.0') { throw 'Expected visible semantic version v0.1.0 is missing.' }
    if ($loginDecoded -notmatch 'data-auth-state="shell-only"') { throw 'P01 login shell boundary marker is missing.' }
    if ($loginDecoded -notmatch 'disabled') { throw 'P01 login controls must remain disabled.' }

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P01 screenshot evidence.' }

    $captures = @(
        @{ Name = 'p01-dashboard-en.png'; Url = "$baseUrl/?culture=en" },
        @{ Name = 'p01-dashboard-ar.png'; Url = "$baseUrl/?culture=ar-KW" },
        @{ Name = 'p01-login-en.png'; Url = "$baseUrl/login?culture=en" },
        @{ Name = 'p01-login-ar.png'; Url = "$baseUrl/login?culture=ar-KW" }
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
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        [pscustomobject]@{ file = $file.Name; bytes = $file.Length; sha256 = $hash; url = $capture.Url }
    }
    $manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $artifactDir 'screenshot-manifest.json') -Encoding utf8
    Write-Host 'P01 runtime/browser verification passed.'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if ($null -eq $previousBypass) { Remove-Item Env:Setup__BypassGateForRegression -ErrorAction SilentlyContinue }
    else { $env:Setup__BypassGateForRegression = $previousBypass }
    if ($null -eq $previousDotnetEnvironment) { Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue }
    else { $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment }
    if ($null -eq $previousAspNetCoreEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue }
    else { $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment }
}
