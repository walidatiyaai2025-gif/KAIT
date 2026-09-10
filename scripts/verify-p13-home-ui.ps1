$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p13-home-ui'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

function Get-AntiForgeryToken([string]$Html) {
    $match = [regex]::Match($Html, '<input[^>]+name="__RequestVerificationToken"[^>]+value="([^"]+)"')
    if (-not $match.Success) { throw 'Could not obtain antiforgery token.' }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Save-EvidenceHtml([string]$Html, [string]$Path, [string]$Css) {
    $safe = [regex]::Replace(
        $Html,
        '(<input[^>]+name="__RequestVerificationToken"[^>]+value=")[^"]+("[^>]*>)',
        '$1[REDACTED]$2',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $safe = [regex]::Replace($safe, '<link[^>]+rel="stylesheet"[^>]*>', '', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $safe = $safe.Replace('</head>', "<style>$Css</style></head>")
    $safe | Set-Content $Path -Encoding utf8
}

$controller = Get-Content (Join-Path $root 'src/GSIP.Web/Controllers/ShellController.cs') -Raw
$view = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Shell/Index.cshtml') -Raw
$layout = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Shared/_Layout.cshtml') -Raw
$p13Css = Get-Content (Join-Path $root 'src/GSIP.Web/wwwroot/css/p13.css') -Raw
$baseCss = Get-Content (Join-Path $root 'src/GSIP.Web/wwwroot/css/gsip.css') -Raw
$baseline = Get-Content (Join-Path $root 'docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg') -Raw
if (-not $controller.Contains('IMetadataCatalogService metadataCatalog', [System.StringComparison]::Ordinal)) { throw 'Home is not catalogue-backed.' }
if (-not $view.Contains('data-testid="p13-home-dashboard"', [System.StringComparison]::Ordinal)) { throw 'P13 Home runtime marker is missing.' }
if (-not $layout.Contains('<strong>P13</strong>', [System.StringComparison]::Ordinal)) { throw 'Shared shell is not marked P13.' }
if (-not $p13Css.Contains(':focus-visible', [System.StringComparison]::Ordinal)) { throw 'P13 shared focus styling is missing.' }
if (-not $baseline.Contains('Government Services Integration Portal', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Home visual baseline is unavailable.' }

$databaseName = 'GSIP_P13_HOME_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
$syntheticUsername = 'p13-home-user'
$syntheticPassword = 'Synthetic-P13-Home-Only!9157'
$entityId = '81000000-0000-0000-0000-000000001301'
$serviceId = '82000000-0000-0000-0000-000000001301'
$helperDir = Join-Path $env:TEMP ('gsip-p13-home-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$domainProject = (Join-Path $root 'src/GSIP.Domain/GSIP.Domain.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'SeedP13Home.csproj'
$helperSource = Join-Path $helperDir 'Program.cs'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$infraProject" /><ProjectReference Include="$domainProject" /></ItemGroup>
</Project>
"@ | Set-Content $helperProject -Encoding utf8
@'
using GSIP.Domain.Metadata;
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
        OrganizationNameEn = "Synthetic P13 Home Evidence",
        OrganizationNameAr = "بيئة إثبات P13 الاصطناعية",
        PrimaryColor = "#0b4f7d",
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

var user = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "p13-home-user",
    NormalizedUserName = "P13-HOME-USER",
    Email = "p13-home-user@example.invalid",
    NormalizedEmail = "P13-HOME-USER@EXAMPLE.INVALID",
    EmailConfirmed = true,
    DisplayName = "Synthetic P13 Home User",
    IsEnabled = true,
    IsPrivileged = false,
    MustChangePassword = false,
    SecurityStamp = Guid.NewGuid().ToString("N"),
    ConcurrencyStamp = Guid.NewGuid().ToString("N")
};
user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
db.Users.Add(user);

var entity = new CatalogEntity
{
    Id = Guid.Parse("81000000-0000-0000-0000-000000001301"),
    Code = "SYNTH-HOME",
    NameAr = "جهة الخدمات الاصطناعية",
    NameEn = "Synthetic Services Authority",
    Logo = "",
    Active = true,
    DisplayOrder = 10,
    CreatedAtUtc = DateTimeOffset.UtcNow,
    UpdatedAtUtc = DateTimeOffset.UtcNow
};
var service = new CatalogService
{
    Id = Guid.Parse("82000000-0000-0000-0000-000000001301"),
    DefinitionKey = Guid.NewGuid(),
    EntityId = entity.Id,
    Code = "HOME-INQUIRY",
    NameAr = "خدمة استعلام اصطناعية",
    NameEn = "Synthetic Inquiry Service",
    DescriptionAr = "بيانات اصطناعية محلية لإثبات واجهة P13 فقط.",
    DescriptionEn = "Local synthetic metadata used only for P13 browser evidence.",
    Active = true,
    Version = 1,
    IsCurrent = true,
    CreatedAtUtc = DateTimeOffset.UtcNow,
    UpdatedAtUtc = DateTimeOffset.UtcNow
};
service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
{
    Id = Guid.NewGuid(),
    ServiceId = service.Id,
    EnvironmentId = CatalogEnvironmentCodes.UatId,
    BaseUrl = "https://example.invalid",
    RelativePath = "/synthetic",
    HttpMethod = "GET",
    ContentType = "application/json",
    NonSecretHeadersJson = "{}",
    TimeoutSeconds = 10,
    TlsPolicy = "SystemDefault",
    ValidateServerCertificate = true,
    ProxyUrl = "",
    HealthPath = "",
    HealthMethod = "HEAD",
    Active = true
});
entity.Services.Add(service);
db.CatalogEntities.Add(entity);
await db.SaveChangesAsync();
'@ | Set-Content $helperSource -Encoding utf8

& dotnet run --project $helperProject --configuration Release -- $connectionString $syntheticPassword
if ($LASTEXITCODE -ne 0) { throw 'Synthetic P13 Home evidence database seed failed.' }

$port = 5093
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
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            if ((Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $healthy = $true; break }
        } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy. See $stderr" }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $unauthorized = $client.GetAsync("$baseUrl/?culture=en").GetAwaiter().GetResult()
        if ([int]$unauthorized.StatusCode -notin @(301,302,303,307,308)) { throw "Unauthenticated Home route was not blocked. Status $([int]$unauthorized.StatusCode)" }
        $location = if ($unauthorized.Headers.Location) { $unauthorized.Headers.Location.OriginalString } else { '' }
        if ($location -notmatch '/login') { throw "Unauthenticated Home route did not challenge to login. Location: $location" }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }

    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable session
    $loginToken = Get-AntiForgeryToken $login.Content
    $loginResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{
        Username = $syntheticUsername
        Password = $syntheticPassword
        RememberMe = 'false'
        __RequestVerificationToken = $loginToken
    }
    if ($loginResult.StatusCode -ne 200) { throw "Synthetic P13 login failed. Status $($loginResult.StatusCode)" }

    $en = Invoke-WebRequest "$baseUrl/?culture=en" -UseBasicParsing -WebSession $session
    $ar = Invoke-WebRequest "$baseUrl/?culture=ar-KW" -UseBasicParsing -WebSession $session
    if ($en.Content -notmatch '<html lang="en" dir="ltr" data-culture-name="en">') { throw 'English Home direction/culture is incorrect.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'Arabic Home direction/culture is incorrect.' }
    if ($en.Content -notmatch 'data-testid="p13-home-dashboard"' -or $ar.Content -notmatch 'data-testid="p13-home-dashboard"') { throw 'P13 Home runtime marker is missing.' }
    if ($en.Content -notmatch 'Synthetic Services Authority' -or $en.Content -notmatch 'Synthetic Inquiry Service') { throw 'English catalogue-backed Home data is missing.' }
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    if ($arDecoded -notmatch 'جهة الخدمات الاصطناعية' -or $arDecoded -notmatch 'خدمة استعلام اصطناعية') { throw 'Arabic catalogue-backed Home data is missing.' }
    if ($en.Content -match 'Future phase' -or $en.Content -match 'Available after setup') { throw 'Stale P01 preview content remains in Home runtime output.' }
    if ($en.Content -notmatch 'href="/requests' -or $en.Content -notmatch 'href="/audit' -or $en.Content -notmatch 'href="/permissions' -or $en.Content -notmatch 'href="/operations') { throw 'Primary navigation is missing real post-P01 routes.' }

    $combinedCss = $baseCss + "`n" + $p13Css
    $enPath = Join-Path $artifactDir 'p13-home-en.html'
    $arPath = Join-Path $artifactDir 'p13-home-ar.html'
    Save-EvidenceHtml $en.Content $enPath $combinedCss
    Save-EvidenceHtml $ar.Content $arPath $combinedCss

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P13 screenshot evidence.' }

    $captures = @(
        @{ Name = 'p13-home-en-desktop.png'; Html = $enPath; Culture = 'en'; Direction = 'ltr'; Viewport = '1440x1000' },
        @{ Name = 'p13-home-ar-desktop.png'; Html = $arPath; Culture = 'ar-KW'; Direction = 'rtl'; Viewport = '1440x1000' },
        @{ Name = 'p13-home-en-narrow.png'; Html = $enPath; Culture = 'en'; Direction = 'ltr'; Viewport = '390x900' },
        @{ Name = 'p13-home-ar-narrow.png'; Html = $arPath; Culture = 'ar-KW'; Direction = 'rtl'; Viewport = '390x900' }
    )

    foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        $htmlUri = [System.Uri]::new($capture.Html).AbsoluteUri
        $arguments = @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars','--allow-file-access-from-files',"--window-size=$($capture.Viewport.Replace('x',','))","--screenshot=$path",$htmlUri)
        $browserProcess = Start-Process $browser -ArgumentList $arguments -Wait -PassThru
        if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $path)) { throw "Browser screenshot failed: $($capture.Name)" }
        if ((Get-Item $path).Length -lt 10000) { throw "Screenshot is unexpectedly small: $($capture.Name)" }
    }

    $candidateSha = (git -C $root rev-parse HEAD).Trim()
    if ([string]::IsNullOrWhiteSpace($candidateSha)) { throw 'Could not resolve exact P13 candidate SHA.' }
    $manifest = [ordered]@{
        CandidateSha = $candidateSha
        Unit = 'P13::ui-accessibility-convergence'
        Baseline = 'docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg'
        Route = '/'
        SyntheticDataOnly = $true
        RealCredentials = $false
        PersonalData = $false
        TestId = 'p13-home-dashboard'
        Captures = @($captures | ForEach-Object {
            $path = Join-Path $artifactDir $_.Name
            [ordered]@{
                File = $_.Name
                Culture = $_.Culture
                Direction = $_.Direction
                Viewport = $_.Viewport
                Sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        })
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $artifactDir 'manifest.json') -Encoding utf8

    $retainedText = Get-ChildItem $artifactDir -File | Where-Object { $_.Extension -in @('.html','.json','.log','.txt') } | Get-Content -Raw | Out-String
    if ($retainedText -match 'Synthetic-P13-Home-Only!9157') { throw 'P13 evidence leaked the synthetic password.' }
    if ($retainedText -match 'name="__RequestVerificationToken"[^>]+value="(?!\[REDACTED\])') { throw 'P13 retained HTML contains an unredacted antiforgery value.' }

    Write-Host "P13_HOME_RUNTIME_ACCEPTANCE=PASS candidate=$candidateSha captures=4"
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $env:Setup__BypassGateForRegression = $previousBypass
    $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    Remove-Item $helperDir -Recurse -Force -ErrorAction SilentlyContinue
    try {
        sqlcmd -S '(localdb)\MSSQLLocalDB' -Q "IF DB_ID(N'$databaseName') IS NOT NULL BEGIN ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]; END" | Out-Null
    } catch { }
}
