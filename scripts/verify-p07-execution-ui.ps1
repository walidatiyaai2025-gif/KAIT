$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p07-execution-ui-evidence'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    if (-not $Text.Contains($Needle, [System.StringComparison]::Ordinal)) { throw $Message }
}

$controller = Get-Content (Join-Path $root 'src/GSIP.Web/Controllers/ServiceExecutionController.cs') -Raw
$view = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Execution/Index.cshtml') -Raw
$layout = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Shared/_Layout.cshtml') -Raw
$css = Get-Content (Join-Path $root 'src/GSIP.Web/wwwroot/css/execution.css') -Raw
$arResource = Get-Content (Join-Path $root 'src/GSIP.Web/Resources/ExecutionResource.ar-KW.resx') -Raw
$baseline = Get-Content (Join-Path $root 'docs/ui-baseline/bilingual_kuwait_government_service_portal.svg') -Raw

Assert-Contains $controller 'GsipPermissions.ServicesExecute' 'Service Execution UI does not filter by server-side Services.Execute.'
Assert-Contains $controller 'HasServicePermissionAsync' 'Service Execution UI does not enforce service-scoped authorization.'
Assert-Contains $view 'execution-selectors' 'Service Execution entity/service/environment selectors are missing.'
Assert-Contains $view 'dynamic-fields' 'Metadata-driven request fields are missing.'
Assert-Contains $view 'service-info-card' 'Selected service information card is missing.'
Assert-Contains $view 'result-workspace' 'Result/Raw Response/History workspace is missing.'
Assert-Contains $view 'request-metadata-card' 'Request metadata card is missing.'
Assert-Contains $layout 'href="/execute?culture=' 'Service Execution is not integrated into the GSIP shell.'
Assert-Contains $css '@media(max-width:760px)' 'Narrow Service Execution responsive rules are missing.'
Assert-Contains $css ':focus-visible' 'Keyboard focus styling is missing from Service Execution UI.'
Assert-Contains $arResource 'تنفيذ الخدمة' 'Arabic Service Execution resources are missing.'
Assert-Contains $baseline 'Service Execution' 'Required Service Execution visual baseline is unavailable.'

$databaseName = 'GSIP_P07_UI_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
$syntheticUsername = 'p07-ui-admin'
$syntheticPassword = 'Synthetic-P07-UI-Only!7341'
$helperDir = Join-Path $env:TEMP ('gsip-p07-ui-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$domainProject = (Join-Path $root 'src/GSIP.Domain/GSIP.Domain.csproj').Replace('\','/')
$applicationProject = (Join-Path $root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'SeedP07Ui.csproj'
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
        Id = 1, CompletedAtUtc = DateTimeOffset.UtcNow, OrganizationNameEn = "Synthetic GSIP P07 UI Evidence",
        OrganizationNameAr = "بيئة إثبات واجهة P07", PrimaryColor = "#17324d", TimeZoneId = "Arab Standard Time",
        SessionTimeoutMinutes = 30, LockoutMinutes = 15, MaxFailedAccessAttempts = 5,
        RequireMfaForPrivilegedAccounts = false, DefaultEnvironment = "UAT", IntegrationTimeoutSeconds = 30,
        ValidateServerCertificate = true
    });
}

var adminRoleId = Guid.Parse(GsipRoles.SystemAdministratorId);
var user = new ApplicationUser
{
    Id = Guid.NewGuid(), UserName = "p07-ui-admin", NormalizedUserName = "P07-UI-ADMIN",
    Email = "p07-ui-admin@example.invalid", NormalizedEmail = "P07-UI-ADMIN@EXAMPLE.INVALID", EmailConfirmed = true,
    DisplayName = "Synthetic P07 UI Administrator", IsEnabled = true, IsPrivileged = true, MustChangePassword = false,
    SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
};
user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
db.Users.Add(user);
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = adminRoleId });

if (!await db.RolePermissions.AnyAsync(x => x.RoleId == adminRoleId && x.PermissionKey == GsipPermissions.ServicesExecute))
{
    db.RolePermissions.Add(new RolePermission { RoleId = adminRoleId, PermissionKey = GsipPermissions.ServicesExecute, IsAllowed = true, UpdatedAtUtc = DateTimeOffset.UtcNow });
}

var entity = new CatalogEntity
{
    Id = Guid.Parse("71000000-0000-0000-0000-000000000701"), Code = "SYNTH-EXEC-UI",
    NameAr = "جهة الخدمات الحكومية التجريبية", NameEn = "Synthetic Government Services Authority",
    Logo = "", Active = true, DisplayOrder = 10, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
};
var service = new CatalogService
{
    Id = Guid.Parse("72000000-0000-0000-0000-000000000701"), DefinitionKey = Guid.NewGuid(), EntityId = entity.Id,
    Code = "EXEC-UI-SAMPLE", NameAr = "خدمة الاستعلام التجريبية", NameEn = "Synthetic Inquiry Service",
    DescriptionAr = "خدمة تجريبية لإثبات واجهة التنفيذ فقط دون اتصال خارجي.",
    DescriptionEn = "Synthetic service used only for execution UI evidence with no outbound call.",
    Active = true, Version = 1, IsCurrent = true, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
};
service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
{
    Id = Guid.NewGuid(), ServiceId = service.Id, EnvironmentId = CatalogEnvironmentCodes.UatId,
    BaseUrl = "https://synthetic-uat.invalid", RelativePath = "/api/inquiry", HttpMethod = "POST",
    ContentType = "application/json", NonSecretHeadersJson = "{}", TimeoutSeconds = 30,
    TlsPolicy = "SystemDefault", ValidateServerCertificate = true, Active = true
});
service.Fields.Add(new ServiceFieldDefinition
{
    Id = Guid.NewGuid(), ServiceId = service.Id, Key = "CIVIL-ID", LabelAr = "الرقم المدني", LabelEn = "Civil ID",
    FieldType = "text", Required = true, Regex = "^[0-9]{12}$", MinLength = 12, MaxLength = 12,
    DisplayOrder = 10, Sensitive = true, Masking = "Last4"
});
service.Fields.Add(new ServiceFieldDefinition
{
    Id = Guid.NewGuid(), ServiceId = service.Id, Key = "REFERENCE-NO", LabelAr = "رقم المرجع", LabelEn = "Reference Number",
    FieldType = "text", Required = false, MinLength = 1, MaxLength = 20, DisplayOrder = 20, Sensitive = false, Masking = "None"
});
service.ResultMappings.Add(new ResultMappingDefinition
{
    Id = Guid.NewGuid(), ServiceId = service.Id, SourcePath = "$.data.status", LabelAr = "الحالة", LabelEn = "Status",
    ResultType = "text", Formatter = "", Sensitive = false, DisplayOrder = 10
});
entity.Services.Add(service);
db.CatalogEntities.Add(entity);

if (!await db.RoleServicePermissions.AnyAsync(x => x.RoleId == adminRoleId && x.ServiceCode == service.Code && x.PermissionKey == GsipPermissions.ServicesExecute))
{
    db.RoleServicePermissions.Add(new RoleServicePermission
    {
        RoleId = adminRoleId, ServiceCode = service.Code, PermissionKey = GsipPermissions.ServicesExecute,
        IsAllowed = true, UpdatedAtUtc = DateTimeOffset.UtcNow
    });
}
await db.SaveChangesAsync();
'@ | Set-Content $helperSource -Encoding utf8

& dotnet run --project $helperProject --configuration Release -- $connectionString $syntheticPassword
if ($LASTEXITCODE -ne 0) { throw 'Synthetic P07 UI evidence database seed failed.' }

$port = 5087
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
        $unauthorized = $client.GetAsync("$baseUrl/execute?culture=en").GetAwaiter().GetResult()
        if ([int]$unauthorized.StatusCode -notin @(301,302,303,307,308)) { throw "Unauthenticated execution route was not blocked. Status $([int]$unauthorized.StatusCode)" }
        $location = if ($unauthorized.Headers.Location) { $unauthorized.Headers.Location.OriginalString } else { '' }
        if ($location -notmatch '/login') { throw "Unauthenticated execution route did not challenge to login. Location: $location" }
    } finally { $client.Dispose(); $handler.Dispose() }

    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable session
    $tokenMatch = [regex]::Match($login.Content, '<input[^>]+name="__RequestVerificationToken"[^>]+value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'Could not obtain login antiforgery token.' }
    $token = [System.Net.WebUtility]::HtmlDecode($tokenMatch.Groups[1].Value)
    $loginResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{
        Username = $syntheticUsername; Password = $syntheticPassword; RememberMe = 'false'; __RequestVerificationToken = $token
    }
    if ($loginResult.StatusCode -ne 200) { throw "Synthetic P07 login failed. Status $($loginResult.StatusCode)" }

    $en = Invoke-WebRequest "$baseUrl/execute?culture=en" -UseBasicParsing -WebSession $session
    $ar = Invoke-WebRequest "$baseUrl/execute?culture=ar-KW" -UseBasicParsing -WebSession $session
    if ($en.StatusCode -ne 200 -or $ar.StatusCode -ne 200) { throw 'Authenticated execution route did not return HTTP 200 in both cultures.' }
    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    if ($en.Content -notmatch '<html lang="en" dir="ltr"' -or $enDecoded -notmatch 'Service Execution' -or $enDecoded -notmatch 'Synthetic Inquiry Service' -or $enDecoded -notmatch 'Request Data') { throw 'English LTR execution evidence is incomplete.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl"' -or $arDecoded -notmatch 'تنفيذ الخدمة' -or $arDecoded -notmatch 'خدمة الاستعلام التجريبية' -or $arDecoded -notmatch 'بيانات الطلب') { throw 'Arabic RTL execution evidence is incomplete.' }
    if ($enDecoded -match 'synthetic-uat\.invalid') { throw 'Execution UI exposed the configured base URL instead of endpoint alias metadata.' }

    $stylePaths = @(
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/gsip.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/identity.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/execution.css')
    )
    $styleBundle = ($stylePaths | ForEach-Object { Get-Content $_ -Raw }) -join "`n"
    $inlineStyles = "<style data-evidence-inline-styles>`n$styleBundle`n</style>"
    $enHtml = $en.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $arHtml = $ar.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $enPath = Join-Path $artifactDir 'execution-en.html'; $arPath = Join-Path $artifactDir 'execution-ar.html'
    $enHtml | Set-Content $enPath -Encoding utf8; $arHtml | Set-Content $arPath -Encoding utf8

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P07 screenshot evidence.' }

    $captures = @(
        @{ Name='p07-execution-en-desktop.png'; File=$enPath; Size='1440,1000'; Culture='en'; Direction='ltr'; Viewport='1440x1000' },
        @{ Name='p07-execution-ar-desktop.png'; File=$arPath; Size='1440,1000'; Culture='ar-KW'; Direction='rtl'; Viewport='1440x1000' },
        @{ Name='p07-execution-en-narrow.png'; File=$enPath; Size='390,844'; Culture='en'; Direction='ltr'; Viewport='390x844' },
        @{ Name='p07-execution-ar-narrow.png'; File=$arPath; Size='390,844'; Culture='ar-KW'; Direction='rtl'; Viewport='390x844' }
    )
    foreach ($capture in $captures) {
        $output = Join-Path $artifactDir $capture.Name
        $fileUrl = 'file:///' + $capture.File.Replace('\','/')
        $arguments = @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars','--allow-file-access-from-files',"--window-size=$($capture.Size)","--screenshot=$output",$fileUrl)
        $browserProcess = Start-Process $browser -ArgumentList $arguments -Wait -PassThru
        if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $output) -or (Get-Item $output).Length -lt 14000) { throw "Browser screenshot failed: $($capture.Name)" }
    }

    $head = (git -C $root rev-parse HEAD).Trim()
    $manifest = foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        [pscustomobject]@{
            exactHead = $head
            baseline = 'docs/ui-baseline/bilingual_kuwait_government_service_portal.svg'
            route = '/execute'
            culture = $capture.Culture
            direction = $capture.Direction
            viewport = $capture.Viewport
            screenshot = $capture.Name
            sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifactDir 'manifest.json') -Encoding utf8
    Write-Host "P07 Service Execution UI browser evidence passed on exact head $head."
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $env:Setup__BypassGateForRegression = $previousBypass
    $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    Remove-Item $helperDir -Recurse -Force -ErrorAction SilentlyContinue
}
