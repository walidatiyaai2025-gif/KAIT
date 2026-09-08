$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p05-evidence'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    if (-not $Text.Contains($Needle, [System.StringComparison]::Ordinal)) { throw $Message }
}

$controller = Get-Content (Join-Path $root 'src/GSIP.Web/Controllers/MetadataController.cs') -Raw
$view = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Metadata/Index.cshtml') -Raw
$layout = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Shared/_Layout.cshtml') -Raw
$css = Get-Content (Join-Path $root 'src/GSIP.Web/wwwroot/css/metadata.css') -Raw
$arResource = Get-Content (Join-Path $root 'src/GSIP.Web/Resources/MetadataResource.ar-KW.resx') -Raw
$schema = Get-Content (Join-Path $root 'docs/schemas/gsip-metadata.schema.json') -Raw

Assert-Contains $controller '[Authorize(Policy = GsipPermissions.EntitiesManage)]' 'Entity metadata mutations are not protected by Entities.Manage.'
Assert-Contains $controller '[Authorize(Policy = GsipPermissions.ServicesManage)]' 'Service metadata mutations are not protected by Services.Manage.'
Assert-Contains $controller '[ValidateAntiForgeryToken]' 'Metadata mutations are missing antiforgery validation.'
Assert-Contains $view '@Html.AntiForgeryToken()' 'Metadata forms do not emit antiforgery tokens.'
Assert-Contains $view 'serviceJson' 'Generic service definition editor is missing.'
Assert-Contains $layout 'href="/metadata?culture=' 'Metadata navigation is not active in the shell.'
Assert-Contains $layout '<strong>P05</strong>' 'Shell phase marker was not advanced to P05.'
Assert-Contains $css '@media(max-width:620px)' 'Narrow responsive metadata layout is missing.'
Assert-Contains $arResource 'دليل الجهات والخدمات' 'Arabic metadata resource is missing.'
Assert-Contains $schema '"schemaVersion"' 'Metadata JSON schema does not govern schemaVersion.'
Assert-Contains $schema '"environmentConfigs"' 'Metadata JSON schema does not govern environment bindings.'

$databaseName = 'GSIP_P05_UI_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
$syntheticUsername = 'p05-ui-admin'
$syntheticPassword = 'Synthetic-P05-Only!9347'
$helperDir = Join-Path $env:TEMP ('gsip-p05-ui-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$domainProject = (Join-Path $root 'src/GSIP.Domain/GSIP.Domain.csproj').Replace('\','/')
$applicationProject = (Join-Path $root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'SeedP05.csproj'
$helperSource = Join-Path $helperDir 'Program.cs'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$infraProject" /><ProjectReference Include="$domainProject" /><ProjectReference Include="$applicationProject" /></ItemGroup>
</Project>
"@ | Set-Content $helperProject -Encoding utf8
@'
using GSIP.Application.Authorization;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Authorization;
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
        Id = 1, CompletedAtUtc = DateTimeOffset.UtcNow, OrganizationNameEn = "Synthetic GSIP P05 Evidence",
        OrganizationNameAr = "بيئة إثبات P05", PrimaryColor = "#17324d", TimeZoneId = "Arab Standard Time",
        SessionTimeoutMinutes = 30, LockoutMinutes = 15, MaxFailedAccessAttempts = 5,
        RequireMfaForPrivilegedAccounts = false, DefaultEnvironment = "UAT", IntegrationTimeoutSeconds = 30,
        ValidateServerCertificate = true
    });
}

var adminRoleId = Guid.Parse(GsipRoles.SystemAdministratorId);
var user = new ApplicationUser
{
    Id = Guid.NewGuid(), UserName = "p05-ui-admin", NormalizedUserName = "P05-UI-ADMIN",
    Email = "p05-ui-admin@example.invalid", NormalizedEmail = "P05-UI-ADMIN@EXAMPLE.INVALID", EmailConfirmed = true,
    DisplayName = "Synthetic P05 Administrator", IsEnabled = true, IsPrivileged = true, MustChangePassword = false,
    SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
};
user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
db.Users.Add(user);
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = adminRoleId });

var entity = new CatalogEntity
{
    Id = Guid.NewGuid(), Code = "SYNTH-UI", NameAr = "جهة تجريبية للواجهة", NameEn = "Synthetic UI Authority",
    Logo = "/images/synthetic.svg", Active = true, DisplayOrder = 10, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
};
var service = new CatalogService
{
    Id = Guid.NewGuid(), DefinitionKey = Guid.NewGuid(), EntityId = entity.Id, Code = "UI-SAMPLE",
    NameAr = "خدمة واجهة تجريبية", NameEn = "Synthetic UI Service", DescriptionAr = "تعريف وصفي تجريبي",
    DescriptionEn = "Synthetic metadata definition", Active = true, Version = 1, IsCurrent = true,
    CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
};
service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
{
    Id = Guid.NewGuid(), EnvironmentId = CatalogEnvironmentCodes.UatId, BaseUrl = "https://uat.example.invalid",
    RelativePath = "/api/sample", HttpMethod = "POST", ContentType = "application/json", NonSecretHeadersJson = "{}",
    TimeoutSeconds = 30, TlsPolicy = "SystemDefault", ValidateServerCertificate = true, ProxyUrl = "", HealthPath = "/health",
    HealthMethod = "HEAD", Active = true, LastTestStatus = ""
});
service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
{
    Id = Guid.NewGuid(), EnvironmentId = CatalogEnvironmentCodes.ProductionId, BaseUrl = "https://prod.example.invalid",
    RelativePath = "/api/sample", HttpMethod = "POST", ContentType = "application/json", NonSecretHeadersJson = "{}",
    TimeoutSeconds = 45, TlsPolicy = "SystemDefault", ValidateServerCertificate = true, ProxyUrl = "", HealthPath = "/health",
    HealthMethod = "HEAD", Active = true, LastTestStatus = ""
});
service.Fields.Add(new ServiceFieldDefinition
{
    Id = Guid.NewGuid(), Key = "CIVIL-ID", LabelAr = "الرقم المدني", LabelEn = "Civil ID", FieldType = "text",
    Required = true, Regex = "^[0-9]{12}$", MinLength = 12, MaxLength = 12, OptionsJson = "[]", DisplayOrder = 10,
    Sensitive = true, Masking = "Last4"
});
service.ResultMappings.Add(new ResultMappingDefinition
{
    Id = Guid.NewGuid(), SourcePath = "$.data.status", LabelAr = "الحالة", LabelEn = "Status", ResultType = "text",
    Formatter = "", Sensitive = false, DisplayOrder = 10
});
entity.Services.Add(service);
db.CatalogEntities.Add(entity);
await db.SaveChangesAsync();
'@ | Set-Content $helperSource -Encoding utf8

& dotnet run --project $helperProject --configuration Release -- $connectionString $syntheticPassword
if ($LASTEXITCODE -ne 0) { throw 'Synthetic P05 UI evidence database seed failed.' }

$port = 5085
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

    $handler = [System.Net.Http.HttpClientHandler]::new(); $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $unauthorized = $client.GetAsync("$baseUrl/metadata?culture=en").GetAwaiter().GetResult()
        if ([int]$unauthorized.StatusCode -notin @(301,302,303,307,308)) { throw "Unauthenticated metadata route was not blocked. Status $([int]$unauthorized.StatusCode)" }
        $location = if ($unauthorized.Headers.Location) { $unauthorized.Headers.Location.OriginalString } else { '' }
        if ($location -notmatch '/login') { throw "Unauthenticated metadata route did not challenge to login. Location: $location" }
    } finally { $client.Dispose(); $handler.Dispose() }

    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable session
    $tokenMatch = [regex]::Match($login.Content, '<input[^>]+name="__RequestVerificationToken"[^>]+value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'Could not obtain the real login antiforgery token.' }
    $token = [System.Net.WebUtility]::HtmlDecode($tokenMatch.Groups[1].Value)
    $loginResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{
        Username = $syntheticUsername; Password = $syntheticPassword; RememberMe = 'false'; __RequestVerificationToken = $token
    }
    if ($loginResult.StatusCode -ne 200) { throw "Synthetic P05 login failed. Status $($loginResult.StatusCode)" }

    $en = Invoke-WebRequest "$baseUrl/metadata?culture=en" -UseBasicParsing -WebSession $session
    $ar = Invoke-WebRequest "$baseUrl/metadata?culture=ar-KW" -UseBasicParsing -WebSession $session
    if ($en.StatusCode -ne 200 -or $ar.StatusCode -ne 200) { throw 'Authenticated metadata route did not return HTTP 200 in both cultures.' }
    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    if ($enDecoded -notmatch 'Entity & Service Catalog' -or $enDecoded -notmatch 'SYNTH-UI' -or $enDecoded -notmatch 'Synthetic UI Service') { throw 'English metadata dashboard content is incomplete.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">' -or $arDecoded -notmatch 'دليل الجهات والخدمات') { throw 'Arabic RTL metadata dashboard evidence is incomplete.' }

    $enRawPath = Join-Path $artifactDir 'metadata-en-response.html'; $arRawPath = Join-Path $artifactDir 'metadata-ar-response.html'
    $en.Content | Set-Content $enRawPath -Encoding utf8; $ar.Content | Set-Content $arRawPath -Encoding utf8
    $stylePaths = @(
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/gsip.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/identity.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/permissions.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/metadata.css')
    )
    $styleBundle = ($stylePaths | ForEach-Object { Get-Content $_ -Raw }) -join "`n"
    $inlineStyles = "<style data-evidence-inline-styles>`n$styleBundle`n</style>"
    $enHtml = $en.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $arHtml = $ar.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $enPath = Join-Path $artifactDir 'metadata-en.html'; $arPath = Join-Path $artifactDir 'metadata-ar.html'
    $enHtml | Set-Content $enPath -Encoding utf8; $arHtml | Set-Content $arPath -Encoding utf8

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P05 screenshot evidence.' }

    $captures = @(
        @{ Name='p05-metadata-en-desktop.png'; File=$enPath; Size='1440,1000'; Culture='en'; Direction='ltr' },
        @{ Name='p05-metadata-ar-desktop.png'; File=$arPath; Size='1440,1000'; Culture='ar-KW'; Direction='rtl' },
        @{ Name='p05-metadata-en-mobile.png'; File=$enPath; Size='390,844'; Culture='en'; Direction='ltr' },
        @{ Name='p05-metadata-ar-mobile.png'; File=$arPath; Size='390,844'; Culture='ar-KW'; Direction='rtl' }
    )
    foreach ($capture in $captures) {
        $output = Join-Path $artifactDir $capture.Name
        $fileUrl = 'file:///' + $capture.File.Replace('\','/')
        $arguments = @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars','--allow-file-access-from-files',"--window-size=$($capture.Size)","--screenshot=$output",$fileUrl)
        $browserProcess = Start-Process $browser -ArgumentList $arguments -Wait -PassThru
        if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $output) -or (Get-Item $output).Length -lt 12000) { throw "Browser screenshot failed: $($capture.Name)" }
    }

    $manifest = foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        [pscustomobject]@{ file=$capture.Name; bytes=(Get-Item $path).Length; sha256=(Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant(); route='/metadata'; culture=$capture.Culture; direction=$capture.Direction; viewport=$capture.Size; phase='P05'; commit=$env:GITHUB_SHA; dataClassification='synthetic-only' }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifactDir 'ui-screenshot-manifest.json') -Encoding utf8
    Write-Host 'P05 metadata authorization, bilingual and responsive browser verification passed.'
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (Test-Path $helperDir) { Remove-Item $helperDir -Recurse -Force }
    if ($null -eq $previousBypass) { Remove-Item Env:Setup__BypassGateForRegression -ErrorAction SilentlyContinue } else { $env:Setup__BypassGateForRegression = $previousBypass }
    if ($null -eq $previousRegressionConnection) { Remove-Item Env:IdentitySecurity__RegressionConnectionString -ErrorAction SilentlyContinue } else { $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection }
    if ($null -eq $previousDotnetEnvironment) { Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment }
    if ($null -eq $previousAspNetCoreEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment }
    try { Invoke-Sqlcmd -ConnectionString $connectionString -Query "ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName];" -ErrorAction SilentlyContinue } catch { }
}
