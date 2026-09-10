$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p04-permissions-ui'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
$candidateSha = (git -C $root rev-parse HEAD).Trim()
if ([string]::IsNullOrWhiteSpace($candidateSha)) { throw 'Could not resolve exact P04 evidence candidate SHA.' }
if ($env:CANDIDATE_SHA -and $candidateSha -ne $env:CANDIDATE_SHA) {
    throw "P04 evidence candidate mismatch: expected=$env:CANDIDATE_SHA actual=$candidateSha"
}

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    if (-not $Text.Contains($Needle, [System.StringComparison]::Ordinal)) { throw $Message }
}

$controller = Get-Content (Join-Path $root 'src/GSIP.Web/Controllers/PermissionsController.cs') -Raw
$view = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Permissions/Index.cshtml') -Raw
$layout = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Shared/_Layout.cshtml') -Raw
$css = Get-Content (Join-Path $root 'src/GSIP.Web/wwwroot/css/permissions.css') -Raw
$arResource = Get-Content (Join-Path $root 'src/GSIP.Web/Resources/PermissionsResource.ar-KW.resx') -Raw

Assert-Contains $controller '[Authorize(Policy = GsipPermissions.RolesManage)]' 'Permissions controller is not protected by Roles.Manage.'
Assert-Contains $controller '[ValidateAntiForgeryToken]' 'Permission mutations are missing antiforgery validation.'
Assert-Contains $controller 'GsipPermissions.IsKnown(permissionKey)' 'Global permission mutation does not reject unknown permission keys.'
Assert-Contains $controller 'GsipPermissions.ServiceScoped.Contains(permissionKey)' 'Service permission mutation does not enforce service-scoped permission keys.'
Assert-Contains $controller 'ServiceEnvironmentPlaceholders' 'Service permission mutation does not validate against configured services.'
Assert-Contains $view 'data-ui="permissions-dashboard"' 'Permissions dashboard semantic marker is missing.'
Assert-Contains $view 'RolesPermissions' 'Roles and permissions matrix is missing.'
Assert-Contains $view 'ServiceMatrix' 'Service permission matrix is missing.'
Assert-Contains $view '@Html.AntiForgeryToken()' 'Permission forms do not emit antiforgery tokens.'
Assert-Contains $layout 'href="/permissions?culture=' 'Permissions navigation is not active in the shell.'
if ($layout -notmatch '<strong>P(?:0[4-9]|1[0-7])</strong>') { throw 'Shell phase marker is missing or predates closed P04.' }
Assert-Contains $css '@media(max-width:680px)' 'Narrow responsive permissions layout is missing.'
Assert-Contains $arResource 'إدارة الصلاحيات والأدوار' 'Arabic permissions resource is missing.'

$databaseName = 'GSIP_P04_UI_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
$syntheticUsername = 'p04-ui-admin'
$syntheticPassword = 'Synthetic-P04-Only!9347'
$helperDir = Join-Path $env:TEMP ('gsip-p04-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$applicationProject = (Join-Path $root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'SeedP04.csproj'
$helperSource = Join-Path $helperDir 'Program.cs'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$infraProject" /><ProjectReference Include="$applicationProject" /></ItemGroup>
</Project>
"@ | Set-Content $helperProject -Encoding utf8
@'
using GSIP.Application.Authorization;
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
        Id = 1,
        CompletedAtUtc = DateTimeOffset.UtcNow,
        OrganizationNameEn = "Synthetic GSIP UI Evidence",
        OrganizationNameAr = "بيئة اختبار واجهة GSIP",
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
var adminRole = await db.Roles.SingleAsync(role => role.Id == adminRoleId);
var user = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "p04-ui-admin",
    NormalizedUserName = "P04-UI-ADMIN",
    Email = "p04-ui-admin@example.invalid",
    NormalizedEmail = "P04-UI-ADMIN@EXAMPLE.INVALID",
    EmailConfirmed = true,
    DisplayName = "Synthetic P04 Administrator",
    IsEnabled = true,
    IsPrivileged = true,
    MustChangePassword = false,
    SecurityStamp = Guid.NewGuid().ToString("N"),
    ConcurrencyStamp = Guid.NewGuid().ToString("N")
};
user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
db.Users.Add(user);
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = adminRole.Id });

var services = new[]
{
    "Marriage Cases Service",
    "Is Single Basic Service",
    "Marriage Couple Last Case Service",
    "Family Judgment Text Service",
    "Procuration Status Service"
};
foreach (var serviceCode in services)
{
    db.ServiceEnvironmentPlaceholders.Add(new ServiceEnvironmentPlaceholder
    {
        Id = Guid.NewGuid(),
        EntityCode = "MOJ",
        ServiceCode = serviceCode,
        Environment = "UAT",
        IsActive = true
    });
    foreach (var permission in GsipPermissions.ServiceScoped)
    {
        db.RoleServicePermissions.Add(new RoleServicePermission
        {
            RoleId = adminRoleId,
            ServiceCode = serviceCode.Trim().ToUpperInvariant(),
            PermissionKey = permission,
            IsAllowed = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
await db.SaveChangesAsync();
Console.WriteLine(user.Id);
'@ | Set-Content $helperSource -Encoding utf8

& dotnet run --project $helperProject --configuration Release -- $connectionString $syntheticPassword
if ($LASTEXITCODE -ne 0) { throw 'Synthetic P04 evidence database seed failed.' }

$port = 5084
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
        $unauthorized = $client.GetAsync("$baseUrl/permissions?culture=en").GetAwaiter().GetResult()
        if ([int]$unauthorized.StatusCode -notin @(301,302,303,307,308)) { throw "Unauthenticated permissions route was not blocked. Status $([int]$unauthorized.StatusCode)" }
        $location = if ($unauthorized.Headers.Location) { $unauthorized.Headers.Location.OriginalString } else { '' }
        if ($location -notmatch '/login') { throw "Unauthenticated permissions route did not challenge to login. Location: $location" }
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
    if ($loginResult.StatusCode -ne 200) { throw "Synthetic P04 login failed. Status $($loginResult.StatusCode)" }

    $en = Invoke-WebRequest "$baseUrl/permissions?culture=en" -UseBasicParsing -WebSession $session
    $ar = Invoke-WebRequest "$baseUrl/permissions?culture=ar-KW" -UseBasicParsing -WebSession $session
    if ($en.StatusCode -ne 200 -or $ar.StatusCode -ne 200) { throw 'Authenticated permissions route did not return HTTP 200 in both cultures.' }

    $enRawPath = Join-Path $artifactDir 'permissions-en-response.html'
    $arRawPath = Join-Path $artifactDir 'permissions-ar-response.html'
    $en.Content | Set-Content $enRawPath -Encoding utf8
    $ar.Content | Set-Content $arRawPath -Encoding utf8
    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)

    if ($en.Content -notmatch 'data-ui="permissions-dashboard"' -or $enDecoded -notmatch 'Permissions & Role Management') { throw 'English permissions dashboard content is incomplete.' }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl" data-culture-name="ar-KW">') { throw 'Arabic permissions response does not advertise the required ar-KW RTL document direction.' }
    if ($arDecoded -notmatch 'إدارة الصلاحيات والأدوار') { throw 'Arabic localized permissions title is missing after HTML decoding.' }
    if ($enDecoded -notmatch 'Synthetic P04 Administrator') { throw 'Authenticated user details panel is not rendering synthetic account data.' }
    if ($enDecoded -notmatch 'Marriage Cases Service') { throw 'Configured service permission matrix did not render.' }

    $stylePaths = @(
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/gsip.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/identity.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/setup.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/permissions.css')
    )
    $styleBundle = ($stylePaths | ForEach-Object { Get-Content $_ -Raw }) -join "`n"
    $inlineStyles = "<style data-evidence-inline-styles>`n$styleBundle`n</style>"
    $enHtml = $en.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $arHtml = $ar.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    if ($enHtml -notmatch 'data-evidence-inline-styles' -or $arHtml -notmatch 'data-evidence-inline-styles') { throw 'Exact-candidate CSS was not embedded into browser evidence documents.' }
    $enPath = Join-Path $artifactDir 'permissions-en.html'
    $arPath = Join-Path $artifactDir 'permissions-ar.html'
    $enHtml | Set-Content $enPath -Encoding utf8
    $arHtml | Set-Content $arPath -Encoding utf8

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P04 screenshot evidence.' }

    $captures = @(
        @{ Name='p04-permissions-en-desktop.png'; File=$enPath; Size='1440,1000'; Culture='en'; Direction='ltr' },
        @{ Name='p04-permissions-ar-desktop.png'; File=$arPath; Size='1440,1000'; Culture='ar-KW'; Direction='rtl' },
        @{ Name='p04-permissions-en-mobile.png'; File=$enPath; Size='390,844'; Culture='en'; Direction='ltr' },
        @{ Name='p04-permissions-ar-mobile.png'; File=$arPath; Size='390,844'; Culture='ar-KW'; Direction='rtl' }
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
            route = '/permissions'
            culture = $capture.Culture
            direction = $capture.Direction
            viewport = $capture.Size
            phase = 'P04'
            commit = $candidateSha
            renderSource = 'authenticated server-rendered response from exact candidate; exact candidate CSS embedded for deterministic browser rendering'
            dataClassification = 'synthetic-only'
        }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifactDir 'screenshot-manifest.json') -Encoding utf8
    Write-Host 'P04 permissions UI semantic, authorization, bilingual and responsive browser verification passed.'
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (Test-Path $helperDir) { Remove-Item $helperDir -Recurse -Force }
    if ($null -eq $previousBypass) { Remove-Item Env:Setup__BypassGateForRegression -ErrorAction SilentlyContinue } else { $env:Setup__BypassGateForRegression = $previousBypass }
    if ($null -eq $previousRegressionConnection) { Remove-Item Env:IdentitySecurity__RegressionConnectionString -ErrorAction SilentlyContinue } else { $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection }
    if ($null -eq $previousDotnetEnvironment) { Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment }
    if ($null -eq $previousAspNetCoreEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment }
}