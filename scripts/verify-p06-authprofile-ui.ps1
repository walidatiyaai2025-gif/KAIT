$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p06-authprofile-admin'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$databaseName = 'GSIP_P06_ADMIN_UI_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;MultipleActiveResultSets=true"
$syntheticUsername = 'p06-ui-admin'
$syntheticPassword = 'Synthetic-P06-Only!9347'
$helperDir = Join-Path $env:TEMP ('gsip-p06-admin-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$applicationProject = (Join-Path $root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
$domainProject = (Join-Path $root 'src/GSIP.Domain/GSIP.Domain.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'SeedP06Admin.csproj'
$helperSource = Join-Path $helperDir 'Program.cs'

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$infraProject" /><ProjectReference Include="$applicationProject" /><ProjectReference Include="$domainProject" /></ItemGroup>
</Project>
"@ | Set-Content $helperProject -Encoding utf8

@'
using GSIP.Application.Authorization;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
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
        OrganizationNameEn = "Synthetic GSIP P06 UI Evidence",
        OrganizationNameAr = "بيئة اختبار واجهة P06",
        PrimaryColor = "#0b5689",
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

var adminRoleId = Guid.Parse(GsipRoles.SystemAdministratorId);
var user = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "p06-ui-admin",
    NormalizedUserName = "P06-UI-ADMIN",
    Email = "p06-ui-admin@example.invalid",
    NormalizedEmail = "P06-UI-ADMIN@EXAMPLE.INVALID",
    EmailConfirmed = true,
    DisplayName = "Synthetic P06 Administrator",
    IsEnabled = true,
    IsPrivileged = true,
    MustChangePassword = false,
    SecurityStamp = Guid.NewGuid().ToString("N"),
    ConcurrencyStamp = Guid.NewGuid().ToString("N")
};
user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
db.Users.Add(user);
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = adminRoleId });

var entityId = Guid.NewGuid();
var serviceId = Guid.NewGuid();
var profileId = Guid.NewGuid();
var now = DateTimeOffset.UtcNow;
var entity = new CatalogEntity
{
    Id = entityId,
    Code = "MOJ",
    NameAr = "وزارة العدل",
    NameEn = "Ministry of Justice",
    Logo = "MOJ",
    Active = true,
    DisplayOrder = 10,
    CreatedAtUtc = now,
    UpdatedAtUtc = now
};
var service = new CatalogService
{
    Id = serviceId,
    DefinitionKey = Guid.NewGuid(),
    EntityId = entityId,
    Code = "MARRIAGE_CASES",
    NameAr = "خدمة قضايا الزواج",
    NameEn = "Marriage Cases Service",
    DescriptionAr = "بيانات اختبار اصطناعية فقط",
    DescriptionEn = "Synthetic evidence data only",
    Active = true,
    Version = 1,
    IsCurrent = true,
    CreatedAtUtc = now,
    UpdatedAtUtc = now
};
entity.Services.Add(service);
db.CatalogEntities.Add(entity);

var uat = await db.CatalogEnvironments.SingleAsync(x => x.Id == CatalogEnvironmentCodes.UatId);
var production = await db.CatalogEnvironments.SingleAsync(x => x.Id == CatalogEnvironmentCodes.ProductionId);
var uatConfig = new ServiceEnvironmentConfig
{
    Id = Guid.NewGuid(), ServiceId = serviceId, EnvironmentId = uat.Id,
    BaseUrl = "https://uat.invalid", RelativePath = "/synthetic", HttpMethod = "POST",
    ContentType = "application/json", NonSecretHeadersJson = "{}", TimeoutSeconds = 30,
    TlsPolicy = "SystemDefault", ValidateServerCertificate = true, ProxyUrl = "",
    HealthPath = "/health", HealthMethod = "HEAD", Active = true, AuthProfileId = profileId
};
var prodConfig = new ServiceEnvironmentConfig
{
    Id = Guid.NewGuid(), ServiceId = serviceId, EnvironmentId = production.Id,
    BaseUrl = "https://prod.invalid", RelativePath = "/synthetic", HttpMethod = "POST",
    ContentType = "application/json", NonSecretHeadersJson = "{}", TimeoutSeconds = 30,
    TlsPolicy = "SystemDefault", ValidateServerCertificate = true, ProxyUrl = "",
    HealthPath = "/health", HealthMethod = "HEAD", Active = true, AuthProfileId = null
};
service.EnvironmentConfigs.Add(uatConfig);
service.EnvironmentConfigs.Add(prodConfig);

var profile = new AuthProfile
{
    Id = profileId,
    OwnerServiceId = serviceId,
    OwnerEnvironmentId = uat.Id,
    Name = "MOJ UAT API Key",
    AuthType = AuthProfileType.ApiKeyHeader,
    IsEnabled = true,
    CreatedBy = "synthetic-evidence",
    CreatedAtUtc = now,
    UpdatedAtUtc = now
};
profile.Bindings.Add(new AuthProfileBinding
{
    Id = Guid.NewGuid(),
    AuthProfileId = profileId,
    ServiceId = serviceId,
    EnvironmentId = uat.Id,
    IsShared = false,
    DecisionBy = "synthetic-evidence",
    DecisionReason = "OwnerBinding",
    DecisionAtUtc = now
});
db.AuthProfiles.Add(profile);
await db.SaveChangesAsync();
Console.WriteLine(user.Id);
'@ | Set-Content $helperSource -Encoding utf8

& dotnet run --project $helperProject --configuration Release -- $connectionString $syntheticPassword
if ($LASTEXITCODE -ne 0) { throw 'Synthetic P06 admin evidence database seed failed.' }

$port = 5086
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
        try { if ((Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $healthy = $true; break } } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy. See $stderr" }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $unauthorized = $client.GetAsync("$baseUrl/auth-profiles?culture=en").GetAwaiter().GetResult()
        if ([int]$unauthorized.StatusCode -notin @(301,302,303,307,308)) { throw "Unauthenticated auth-profiles route was not blocked. Status $([int]$unauthorized.StatusCode)" }
        $location = if ($unauthorized.Headers.Location) { $unauthorized.Headers.Location.OriginalString } else { '' }
        if ($location -notmatch '/login') { throw "Unauthenticated auth-profiles route did not challenge to login. Location: $location" }
    }
    finally { $client.Dispose(); $handler.Dispose() }

    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable session
    $tokenMatch = [regex]::Match($login.Content, '<input[^>]+name="__RequestVerificationToken"[^>]+value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'Could not obtain the real login antiforgery token.' }
    $token = [System.Net.WebUtility]::HtmlDecode($tokenMatch.Groups[1].Value)
    $loginResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{
        Username = $syntheticUsername
        Password = $syntheticPassword
        RememberMe = 'false'
        __RequestVerificationToken = $token
    }
    if ($loginResult.StatusCode -ne 200) { throw "Synthetic P06 login failed. Status $($loginResult.StatusCode)" }

    $en = Invoke-WebRequest "$baseUrl/auth-profiles?culture=en" -UseBasicParsing -WebSession $session
    $ar = Invoke-WebRequest "$baseUrl/auth-profiles?culture=ar-KW" -UseBasicParsing -WebSession $session
    if ($en.StatusCode -ne 200 -or $ar.StatusCode -ne 200) { throw 'Authenticated AuthProfile route did not return HTTP 200 in both cultures.' }

    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    if ($enDecoded -notmatch 'AuthProfile' -or $enDecoded -notmatch 'Marriage Cases Service' -or $enDecoded -notmatch 'MOJ UAT API Key') { throw 'English AuthProfile hierarchy evidence is incomplete.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'Arabic AuthProfile response does not advertise ar-KW RTL document direction.' }
    if ($arDecoded -notmatch 'وزارة العدل' -or $arDecoded -notmatch 'خدمة قضايا الزواج') { throw 'Arabic AuthProfile hierarchy evidence is incomplete.' }
    if ($en.Content -match 'Synthetic-P06-Only!9347') { throw 'Synthetic password leaked into rendered AuthProfile HTML.' }

    $stylePaths = @(
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/gsip.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/identity.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/auth-profiles.css')
    )
    $styleBundle = ($stylePaths | ForEach-Object { Get-Content $_ -Raw }) -join "`n"
    $inlineStyles = "<style data-evidence-inline-styles>`n$styleBundle`n</style>"
    $enHtml = $en.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $arHtml = $ar.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $enPath = Join-Path $artifactDir 'auth-profiles-en.html'
    $arPath = Join-Path $artifactDir 'auth-profiles-ar.html'
    $enHtml | Set-Content $enPath -Encoding utf8
    $arHtml | Set-Content $arPath -Encoding utf8

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P06 screenshot evidence.' }

    $captures = @(
        @{ Name='p06-auth-profiles-en-desktop.png'; File=$enPath; Size='1440,1000'; Culture='en'; Direction='ltr' },
        @{ Name='p06-auth-profiles-ar-desktop.png'; File=$arPath; Size='1440,1000'; Culture='ar-KW'; Direction='rtl' },
        @{ Name='p06-auth-profiles-en-mobile.png'; File=$enPath; Size='390,844'; Culture='en'; Direction='ltr' },
        @{ Name='p06-auth-profiles-ar-mobile.png'; File=$arPath; Size='390,844'; Culture='ar-KW'; Direction='rtl' }
    )
    foreach ($capture in $captures) {
        $output = Join-Path $artifactDir $capture.Name
        $fileUrl = 'file:///' + $capture.File.Replace('\','/')
        $arguments = @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars','--allow-file-access-from-files',"--window-size=$($capture.Size)","--screenshot=$output",$fileUrl)
        $browserProcess = Start-Process $browser -ArgumentList $arguments -Wait -PassThru
        if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $output)) { throw "Browser screenshot failed: $($capture.Name)" }
        if ((Get-Item $output).Length -lt 12000) { throw "Screenshot is unexpectedly small: $($capture.Name)" }
    }

    $manifest = foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        [pscustomobject]@{
            file = $capture.Name
            bytes = (Get-Item $path).Length
            sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
            route = '/auth-profiles'
            culture = $capture.Culture
            direction = $capture.Direction
            viewport = $capture.Size
            phase = 'P06'
            unit = 'P06::admin-authorization-ui'
            commit = $env:GITHUB_SHA
            build = $env:GITHUB_RUN_ID
            renderSource = 'authenticated server-rendered response from exact candidate; exact candidate CSS embedded for deterministic browser rendering'
            dataClassification = 'synthetic-only; no secret plaintext persisted or rendered'
            intentionalDeviations = 'None'
        }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifactDir 'screenshot-manifest.json') -Encoding utf8
    Write-Host 'P06 AuthProfile bilingual desktop/mobile browser evidence passed.'
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $env:Setup__BypassGateForRegression = $previousBypass
    $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    Remove-Item $helperDir -Recurse -Force -ErrorAction SilentlyContinue
}
