$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p12-operations-runtime-ui'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

function Get-AntiForgeryToken([string]$Html) {
    $match = [regex]::Match($Html, '<input[^>]+name="__RequestVerificationToken"[^>]+value="([^"]+)"')
    if (-not $match.Success) { throw 'Could not obtain antiforgery token.' }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Normalize-PersonalDataScanText([string]$Text) {
    $normalized = [regex]::Replace($Text, '(?i)\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b', '[GUID]')
    $normalized = [regex]::Replace($normalized, '(?i)\b[0-9a-f]{32,64}\b', '[HEXID]')
    $normalized = [regex]::Replace($normalized, '(?i)\bGSIP_P12_UI_[0-9a-f]{12}\b', 'GSIP_P12_UI_[ID]')
    return $normalized
}

function Assert-NoForbiddenEvidence([string]$Text, [string]$SyntheticPassword) {
    if ($Text -match 'Authorization\s*:\s*Bearer\s+\S+') { throw 'P12 runtime evidence exposed a bearer header.' }
    if ($Text -match 'x-api-key\s*[:=]\s*\S+') { throw 'P12 runtime evidence exposed an API key.' }
    if ($Text.Contains($SyntheticPassword, [System.StringComparison]::Ordinal)) { throw 'P12 runtime evidence exposed the synthetic password.' }
    $scan = Normalize-PersonalDataScanText $Text
    if ($scan -match '\b\d{12}\b') { throw 'P12 runtime evidence contains a 12-digit personal-data pattern.' }
}

$databaseName = 'GSIP_P12_UI_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;MultipleActiveResultSets=true"
$adminUser = 'p12-operations-admin'
$readOnlyUser = 'p12-operations-readonly'
$syntheticPassword = 'Synthetic-P12-Operations-Only!9143'
$helperDir = Join-Path $env:TEMP ('gsip-p12-ui-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$applicationProject = (Join-Path $root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'SeedP12OperationsUi.csproj'
$helperSource = Join-Path $helperDir 'Program.cs'

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$infraProject" /><ProjectReference Include="$applicationProject" /></ItemGroup>
</Project>
"@ | Set-Content $helperProject -Encoding utf8

@'
using GSIP.Application.Authorization;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var connectionString = args[0];
var password = args[1];
var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connectionString).Options;
await using var db = new GsipDbContext(options);
await db.Database.MigrateAsync();
if (!await db.SystemSetup.AnyAsync())
{
    db.SystemSetup.Add(new SystemSetupRecord
    {
        Id = 1,
        CompletedAtUtc = DateTimeOffset.UtcNow,
        OrganizationNameEn = "Synthetic GSIP P12 Operations Evidence",
        OrganizationNameAr = "بيئة إثبات عمليات P12",
        PrimaryColor = "#17324d",
        TimeZoneId = "Arab Standard Time",
        SessionTimeoutMinutes = 30,
        LockoutMinutes = 15,
        MaxFailedAccessAttempts = 5,
        RequireMfaForPrivilegedAccounts = false,
        DefaultEnvironment = "UAT",
        IntegrationTimeoutSeconds = 30,
        ValidateServerCertificate = true
    });
}
static ApplicationUser User(string name, string normalized, string display, bool privileged) => new()
{
    Id = Guid.NewGuid(), UserName = name, NormalizedUserName = normalized,
    Email = $"{name}@example.invalid", NormalizedEmail = $"{normalized}@EXAMPLE.INVALID",
    EmailConfirmed = true, DisplayName = display, IsEnabled = true, IsPrivileged = privileged,
    MustChangePassword = false, TwoFactorEnabled = false, LockoutEnabled = true,
    CreatedAtUtc = DateTimeOffset.UtcNow, SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
};
var hasher = new PasswordHasher<ApplicationUser>();
var admin = User("p12-operations-admin", "P12-OPERATIONS-ADMIN", "Synthetic P12 Operations Administrator", true);
admin.PasswordHash = hasher.HashPassword(admin, password);
var readOnly = User("p12-operations-readonly", "P12-OPERATIONS-READONLY", "Synthetic P12 Read Only User", false);
readOnly.PasswordHash = hasher.HashPassword(readOnly, password);
db.Users.AddRange(admin, readOnly);
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = Guid.Parse(GsipRoles.SystemAdministratorId) });
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = readOnly.Id, RoleId = Guid.Parse(GsipRoles.ReadOnlyId) });
await db.SaveChangesAsync();
'@ | Set-Content $helperSource -Encoding utf8

& dotnet run --project $helperProject --configuration Release -- $connectionString $syntheticPassword
if ($LASTEXITCODE -ne 0) { throw 'Synthetic P12 Operations database seed failed.' }

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start(); $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port; $listener.Stop()
$baseUrl = "http://127.0.0.1:$port"
$stdout = Join-Path $artifactDir 'server.stdout.log'
$stderr = Join-Path $artifactDir 'server.stderr.log'
$previousBypass = $env:Setup__BypassGateForRegression
$previousRegressionConnection = $env:IdentitySecurity__RegressionConnectionString
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
$previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:Setup__BypassGateForRegression = 'true'
$env:IdentitySecurity__RegressionConnectionString = $connectionString
$env:DOTNET_ENVIRONMENT = 'RegressionTesting'
$env:ASPNETCORE_ENVIRONMENT = 'RegressionTesting'
$process = Start-Process dotnet -ArgumentList @('run','--project','src/GSIP.Web/GSIP.Web.csproj','--configuration','Release','--no-build','--urls',$baseUrl) -WorkingDirectory $root -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru

try {
    $healthy = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        Start-Sleep -Milliseconds 500
        try { if ((Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $healthy = $true; break } } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy for P12 runtime acceptance. See $stderr" }

    $handler = [System.Net.Http.HttpClientHandler]::new(); $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $unauthorized = $client.GetAsync("$baseUrl/operations?culture=en").GetAwaiter().GetResult()
        if ([int]$unauthorized.StatusCode -notin @(301,302,303,307,308)) { throw "Unauthenticated operations route was not blocked: $([int]$unauthorized.StatusCode)" }
        if (-not $unauthorized.Headers.Location -or $unauthorized.Headers.Location.OriginalString -notmatch '/login') { throw 'Unauthenticated operations route did not challenge to login.' }
    } finally { $client.Dispose(); $handler.Dispose() }

    $readonlyLogin = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable readonlySession
    $readonlyToken = Get-AntiForgeryToken $readonlyLogin.Content
    $readonlyResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $readonlySession -Body @{ Username=$readOnlyUser; Password=$syntheticPassword; RememberMe='false'; __RequestVerificationToken=$readonlyToken }
    if ($readonlyResult.StatusCode -ne 200) { throw 'Synthetic Read Only login failed.' }
    $readonlyOps = Invoke-WebRequest "$baseUrl/operations?culture=en" -UseBasicParsing -WebSession $readonlySession -SkipHttpErrorCheck
    if ($readonlyOps.StatusCode -ne 403) { throw "Authenticated user without Diagnostics.Run was not denied: $($readonlyOps.StatusCode)" }

    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable session
    $loginToken = Get-AntiForgeryToken $login.Content
    $loginResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{ Username=$adminUser; Password=$syntheticPassword; RememberMe='false'; __RequestVerificationToken=$loginToken }
    if ($loginResult.StatusCode -ne 200) { throw 'Synthetic P12 administrator login failed.' }

    $en = Invoke-WebRequest "$baseUrl/operations?culture=en" -UseBasicParsing -WebSession $session
    $ar = Invoke-WebRequest "$baseUrl/operations?culture=ar-KW" -UseBasicParsing -WebSession $session
    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content); $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    if ($en.StatusCode -ne 200 -or $ar.StatusCode -ne 200) { throw 'Operations page did not return 200 in both cultures.' }
    if ($en.Content -notmatch '<html lang="en" dir="ltr"' -or $enDecoded -notmatch 'Operations & System Health' -or $enDecoded -notmatch 'data-testid="operations-dashboard"') { throw 'English P12 operations UI contract failed.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl"' -or $arDecoded -notmatch 'العمليات وصحة النظام' -or $arDecoded -notmatch 'data-testid="operations-dashboard"') { throw 'Arabic P12 operations UI contract failed.' }

    $token = Get-AntiForgeryToken $en.Content
    $dbCheck = Invoke-WebRequest "$baseUrl/operations/diagnostics/database?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{ __RequestVerificationToken=$token }
    $dbDecoded = [System.Net.WebUtility]::HtmlDecode($dbCheck.Content)
    if ($dbCheck.StatusCode -ne 200 -or $dbDecoded -notmatch 'data-testid="diagnostic-result"' -or $dbDecoded -notmatch 'Database\|DATABASE_') { throw 'Authorized database diagnostic did not complete through the protected UI.' }

    $csrf = Invoke-WebRequest "$baseUrl/operations/diagnostics/database?culture=en" -Method Post -UseBasicParsing -WebSession $session -SkipHttpErrorCheck
    if ($csrf.StatusCode -ne 400) { throw "Database diagnostic without antiforgery token was not rejected: $($csrf.StatusCode)" }

    $forgedService = [Guid]::NewGuid().ToString(); $forgedEnvironment = [Guid]::NewGuid().ToString()
    $forgedConnection = Invoke-WebRequest "$baseUrl/operations/services/$forgedService/environments/$forgedEnvironment/test-connection?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{ __RequestVerificationToken=$token } -SkipHttpErrorCheck
    if ($forgedConnection.StatusCode -ne 404) { throw "Forged service/environment connection target was not rejected: $($forgedConnection.StatusCode)" }
    $forgedState = Invoke-WebRequest "$baseUrl/operations/services/$forgedService/environments/$forgedEnvironment/state?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{ isActive='true'; __RequestVerificationToken=$token } -SkipHttpErrorCheck
    if ($forgedState.StatusCode -ne 404) { throw "Forged service/environment state target was not rejected: $($forgedState.StatusCode)" }

    Assert-NoForbiddenEvidence $enDecoded $syntheticPassword; Assert-NoForbiddenEvidence $arDecoded $syntheticPassword; Assert-NoForbiddenEvidence $dbDecoded $syntheticPassword

    $stylePaths = @((Join-Path $root 'src/GSIP.Web/wwwroot/css/gsip.css'),(Join-Path $root 'src/GSIP.Web/wwwroot/css/identity.css'),(Join-Path $root 'src/GSIP.Web/wwwroot/css/operations.css'))
    $styleBundle = ($stylePaths | ForEach-Object { Get-Content $_ -Raw }) -join "`n"
    $inlineStyles = "<style data-evidence-inline-styles>`n$styleBundle`n</style>"
    $enHtml = $en.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $arHtml = $ar.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $enPath = Join-Path $artifactDir 'operations-en.html'; $arPath = Join-Path $artifactDir 'operations-ar.html'
    $enHtml | Set-Content $enPath -Encoding utf8; $arHtml | Set-Content $arPath -Encoding utf8

    $browserCandidates = @((Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),(Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P12 screenshot evidence.' }
    $captures = @(
        @{ Name='p12-operations-en-desktop.png'; File=$enPath; Size='1440,1000'; Culture='en'; Direction='ltr'; Viewport='1440x1000' },
        @{ Name='p12-operations-ar-desktop.png'; File=$arPath; Size='1440,1000'; Culture='ar-KW'; Direction='rtl'; Viewport='1440x1000' },
        @{ Name='p12-operations-en-narrow.png'; File=$enPath; Size='500,844'; Culture='en'; Direction='ltr'; Viewport='500x844' },
        @{ Name='p12-operations-ar-narrow.png'; File=$arPath; Size='500,844'; Culture='ar-KW'; Direction='rtl'; Viewport='500x844' }
    )
    foreach ($capture in $captures) {
        $output = Join-Path $artifactDir $capture.Name; $fileUrl = 'file:///' + $capture.File.Replace('\','/')
        $browserProcess = Start-Process $browser -ArgumentList @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars','--allow-file-access-from-files',"--window-size=$($capture.Size)","--screenshot=$output",$fileUrl) -Wait -PassThru
        if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $output) -or (Get-Item $output).Length -lt 12000) { throw "P12 browser screenshot failed: $($capture.Name)" }
    }
    $head = (git -C $root rev-parse HEAD).Trim()
    $manifest = foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        [pscustomobject]@{ exactHead=$head; route='/operations'; culture=$capture.Culture; direction=$capture.Direction; viewport=$capture.Viewport; screenshot=$capture.Name; sha256=(Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant(); authenticatedRuntime='PASS'; unauthenticatedChallenge='PASS'; unauthorizedDeny='PASS'; databaseDiagnostic='PASS'; csrfNegative='PASS'; forgedTargetIdor='PASS'; syntheticOnly=$true; realCredentials=$false; personalData=$false }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifactDir 'manifest.json') -Encoding utf8
    $artifactText = Get-ChildItem $artifactDir -File | Where-Object { $_.Extension -in @('.html','.json','.log','.txt') } | ForEach-Object { Get-Content $_ -Raw } | Out-String
    Assert-NoForbiddenEvidence $artifactText $syntheticPassword
    Write-Host "P12 protected Operations runtime/browser acceptance passed on exact head $head."
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue; $process.WaitForExit(5000) | Out-Null }
    $env:Setup__BypassGateForRegression = $previousBypass
    $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    Remove-Item $helperDir -Recurse -Force -ErrorAction SilentlyContinue
}
