$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p11-audit-runtime-ui'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

function Get-AntiForgeryToken([string]$Html) {
    $match = [regex]::Match($Html, '<input[^>]+name="__RequestVerificationToken"[^>]+value="([^"]+)"')
    if (-not $match.Success) { throw 'Could not obtain antiforgery token.' }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Assert-NoForbiddenEvidence([string]$Text, [string]$SyntheticPassword) {
    if ($Text -match 'Authorization\s*:\s*Bearer\s+\S+') { throw 'Runtime UI evidence exposed a bearer authorization header.' }
    if ($Text -match 'x-api-key\s*[:=]\s*\S+') { throw 'Runtime UI evidence exposed an API key.' }
    if ($Text -match '\b\d{12}\b') { throw 'Runtime UI evidence contains a 12-digit personal-data pattern.' }
    if ($Text.Contains($SyntheticPassword, [System.StringComparison]::Ordinal)) { throw 'Runtime UI evidence exposed the synthetic login password.' }
}

$databaseName = 'GSIP_P11_UI_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;MultipleActiveResultSets=true"
$syntheticUsername = 'p11-audit-admin'
$readonlyUsername = 'p11-audit-readonly'
$syntheticPassword = 'Synthetic-P11-Audit-Only!8427'
$helperDir = Join-Path $env:TEMP ('gsip-p11-ui-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$applicationProject = (Join-Path $root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'SeedP11AuditUi.csproj'
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
        OrganizationNameEn = "Synthetic GSIP P11 Audit Evidence",
        OrganizationNameAr = "بيئة إثبات سجل التدقيق P11",
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

static ApplicationUser SyntheticUser(string username, string normalized, string displayName, bool privileged) => new()
{
    Id = Guid.NewGuid(),
    UserName = username,
    NormalizedUserName = normalized,
    Email = $"{username}@example.invalid",
    NormalizedEmail = $"{normalized}@EXAMPLE.INVALID",
    EmailConfirmed = true,
    DisplayName = displayName,
    IsEnabled = true,
    IsPrivileged = privileged,
    MustChangePassword = false,
    TwoFactorEnabled = false,
    LockoutEnabled = true,
    CreatedAtUtc = DateTimeOffset.UtcNow,
    SecurityStamp = Guid.NewGuid().ToString("N"),
    ConcurrencyStamp = Guid.NewGuid().ToString("N")
};

var hasher = new PasswordHasher<ApplicationUser>();
var admin = SyntheticUser("p11-audit-admin", "P11-AUDIT-ADMIN", "Synthetic P11 Audit Administrator", true);
admin.PasswordHash = hasher.HashPassword(admin, password);
var readOnly = SyntheticUser("p11-audit-readonly", "P11-AUDIT-READONLY", "Synthetic P11 Read Only User", false);
readOnly.PasswordHash = hasher.HashPassword(readOnly, password);

db.Users.AddRange(admin, readOnly);
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = Guid.Parse(GsipRoles.SystemAdministratorId) });
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = readOnly.Id, RoleId = Guid.Parse(GsipRoles.ReadOnlyId) });
await db.SaveChangesAsync();
'@ | Set-Content $helperSource -Encoding utf8

& dotnet run --project $helperProject --configuration Release -- $connectionString $syntheticPassword
if ($LASTEXITCODE -ne 0) { throw 'Synthetic P11 Audit UI database seed failed.' }

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
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
        try {
            if ((Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) {
                $healthy = $true
                break
            }
        } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy for P11 runtime acceptance. See $stderr" }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $unauthorized = $client.GetAsync("$baseUrl/audit?culture=en").GetAwaiter().GetResult()
        if ([int]$unauthorized.StatusCode -notin @(301,302,303,307,308)) {
            throw "Unauthenticated audit route was not blocked. Status $([int]$unauthorized.StatusCode)"
        }
        $location = if ($unauthorized.Headers.Location) { $unauthorized.Headers.Location.OriginalString } else { '' }
        if ($location -notmatch '/login') { throw "Unauthenticated audit route did not challenge to login. Location: $location" }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }

    $readonlyLogin = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable readonlySession
    $readonlyToken = Get-AntiForgeryToken $readonlyLogin.Content
    $readonlyLoginResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $readonlySession -Body @{
        Username = $readonlyUsername
        Password = $syntheticPassword
        RememberMe = 'false'
        __RequestVerificationToken = $readonlyToken
    }
    if ($readonlyLoginResult.StatusCode -ne 200) { throw "Synthetic Read Only login failed. Status $($readonlyLoginResult.StatusCode)" }

    $readonlyAudit = Invoke-WebRequest "$baseUrl/audit?culture=en" -UseBasicParsing -WebSession $readonlySession -SkipHttpErrorCheck
    if ($readonlyAudit.StatusCode -ne 403) { throw "Authenticated user without Audit.View was not denied. Status $($readonlyAudit.StatusCode)" }
    $readonlyExport = Invoke-WebRequest "$baseUrl/audit/export?culture=en" -UseBasicParsing -WebSession $readonlySession -SkipHttpErrorCheck
    if ($readonlyExport.StatusCode -ne 403) { throw "Authenticated user without Audit.Export was not denied. Status $($readonlyExport.StatusCode)" }

    $login = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -SessionVariable session
    $loginToken = Get-AntiForgeryToken $login.Content
    $loginResult = Invoke-WebRequest "$baseUrl/login?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{
        Username = $syntheticUsername
        Password = $syntheticPassword
        RememberMe = 'false'
        __RequestVerificationToken = $loginToken
    }
    if ($loginResult.StatusCode -ne 200) { throw "Synthetic P11 login failed. Status $($loginResult.StatusCode)" }

    $en = Invoke-WebRequest "$baseUrl/audit?culture=en" -UseBasicParsing -WebSession $session
    $ar = Invoke-WebRequest "$baseUrl/audit?culture=ar-KW" -UseBasicParsing -WebSession $session
    if ($en.StatusCode -ne 200 -or $ar.StatusCode -ne 200) { throw 'Authenticated audit route did not return HTTP 200 in both cultures.' }

    $enDecoded = [System.Net.WebUtility]::HtmlDecode($en.Content)
    $arDecoded = [System.Net.WebUtility]::HtmlDecode($ar.Content)
    if ($en.Content -notmatch '<html lang="en" dir="ltr"' -or $enDecoded -notmatch 'Audit Log & Monitoring' -or $enDecoded -notmatch 'LoginSuccess' -or $enDecoded -notmatch 'data-testid="audit-dashboard"') {
        throw 'English LTR Audit runtime evidence is incomplete.'
    }
    if ($ar.Content -notmatch '<html lang="ar" dir="rtl"' -or $arDecoded -notmatch 'سجل التدقيق والمراقبة' -or $arDecoded -notmatch 'LoginSuccess' -or $arDecoded -notmatch 'data-testid="audit-dashboard"') {
        throw 'Arabic RTL Audit runtime evidence is incomplete.'
    }

    $filtered = Invoke-WebRequest "$baseUrl/audit?culture=en&action=LoginSuccess&succeeded=true" -UseBasicParsing -WebSession $session
    $filteredDecoded = [System.Net.WebUtility]::HtmlDecode($filtered.Content)
    if ($filtered.StatusCode -ne 200 -or $filteredDecoded -notmatch 'LoginSuccess') { throw 'Audit runtime filtering did not preserve the canonical login event.' }

    $selectedMatch = [regex]::Match($en.Content, 'selectedId=([0-9a-fA-F-]{36})')
    if (-not $selectedMatch.Success) { throw 'Audit runtime evidence could not locate a canonical event detail link.' }
    $selectedId = $selectedMatch.Groups[1].Value
    $detail = Invoke-WebRequest "$baseUrl/audit?culture=en&selectedId=$selectedId" -UseBasicParsing -WebSession $session
    $detailDecoded = [System.Net.WebUtility]::HtmlDecode($detail.Content)
    if ($detail.StatusCode -ne 200 -or $detailDecoded -notmatch 'data-testid="audit-detail"' -or $detailDecoded -notmatch 'Sanitized metadata' -or $detailDecoded -notmatch 'Record hash') {
        throw 'Audit event detail runtime evidence is incomplete.'
    }

    $export = Invoke-WebRequest "$baseUrl/audit/export?culture=en&action=LoginSuccess" -UseBasicParsing -WebSession $session
    $exportText = [string]$export.Content
    if ($export.StatusCode -ne 200 -or $export.Headers.'Content-Type' -notmatch 'text/csv' -or $exportText -notmatch 'Sequence,TimestampUtc,ActorUserId,Action') {
        throw 'Permissioned Audit CSV export runtime evidence is incomplete.'
    }
    if ($exportText -notmatch 'LoginSuccess') { throw 'Audit CSV export did not include the filtered canonical event.' }

    $verifyPage = Invoke-WebRequest "$baseUrl/audit?culture=en" -UseBasicParsing -WebSession $session
    $verifyToken = Get-AntiForgeryToken $verifyPage.Content
    $verify = Invoke-WebRequest "$baseUrl/audit/verify-integrity?culture=en" -Method Post -UseBasicParsing -WebSession $session -Body @{
        __RequestVerificationToken = $verifyToken
    }
    $verifyDecoded = [System.Net.WebUtility]::HtmlDecode($verify.Content)
    if ($verify.StatusCode -ne 200 -or $verifyDecoded -notmatch 'Audit chain verification completed: healthy') {
        throw 'Authorized integrity verification did not return a healthy runtime result.'
    }

    $csrf = Invoke-WebRequest "$baseUrl/audit/verify-integrity?culture=en" -Method Post -UseBasicParsing -WebSession $session -SkipHttpErrorCheck
    if ($csrf.StatusCode -ne 400) { throw "Audit integrity POST without antiforgery token was not rejected. Status $($csrf.StatusCode)" }

    Assert-NoForbiddenEvidence $enDecoded $syntheticPassword
    Assert-NoForbiddenEvidence $arDecoded $syntheticPassword
    Assert-NoForbiddenEvidence $detailDecoded $syntheticPassword
    Assert-NoForbiddenEvidence $exportText $syntheticPassword
    Assert-NoForbiddenEvidence $verifyDecoded $syntheticPassword

    $stylePaths = @(
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/gsip.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/identity.css'),
        (Join-Path $root 'src/GSIP.Web/wwwroot/css/audit.css')
    )
    $styleBundle = ($stylePaths | ForEach-Object { Get-Content $_ -Raw }) -join "`n"
    $inlineStyles = "<style data-evidence-inline-styles>`n$styleBundle`n</style>"
    $enHtml = $en.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    $arHtml = $ar.Content.Replace('</head>', "$inlineStyles`n</head>").Replace('href="/', "href=`"$baseUrl/").Replace('src="/', "src=`"$baseUrl/")
    Assert-NoForbiddenEvidence $enHtml $syntheticPassword
    Assert-NoForbiddenEvidence $arHtml $syntheticPassword

    $enPath = Join-Path $artifactDir 'audit-en.html'
    $arPath = Join-Path $artifactDir 'audit-ar.html'
    $enHtml | Set-Content $enPath -Encoding utf8
    $arHtml | Set-Content $arPath -Encoding utf8

    $browserCandidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $browser = $browserCandidates | Select-Object -First 1
    if (-not $browser) { throw 'No supported Chrome/Edge browser found for P11 Audit screenshot evidence.' }

    $captures = @(
        @{ Name='p11-audit-en-desktop.png'; File=$enPath; Size='1440,1000'; Culture='en'; Direction='ltr'; Viewport='1440x1000' },
        @{ Name='p11-audit-ar-desktop.png'; File=$arPath; Size='1440,1000'; Culture='ar-KW'; Direction='rtl'; Viewport='1440x1000' },
        @{ Name='p11-audit-en-narrow.png'; File=$enPath; Size='500,844'; Culture='en'; Direction='ltr'; Viewport='500x844' },
        @{ Name='p11-audit-ar-narrow.png'; File=$arPath; Size='500,844'; Culture='ar-KW'; Direction='rtl'; Viewport='500x844' }
    )
    foreach ($capture in $captures) {
        $output = Join-Path $artifactDir $capture.Name
        $fileUrl = 'file:///' + $capture.File.Replace('\','/')
        $arguments = @('--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--hide-scrollbars','--allow-file-access-from-files',"--window-size=$($capture.Size)","--screenshot=$output",$fileUrl)
        $browserProcess = Start-Process $browser -ArgumentList $arguments -Wait -PassThru
        if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $output) -or (Get-Item $output).Length -lt 14000) {
            throw "Browser screenshot failed: $($capture.Name)"
        }
    }

    $head = (git -C $root rev-parse HEAD).Trim()
    $manifest = foreach ($capture in $captures) {
        $path = Join-Path $artifactDir $capture.Name
        [pscustomobject]@{
            exactHead = $head
            baseline = 'docs/ui-baseline/kuwait_government_audit_dashboard.svg'
            route = '/audit'
            culture = $capture.Culture
            direction = $capture.Direction
            viewport = $capture.Viewport
            screenshot = $capture.Name
            sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
            authenticatedRuntime = 'PASS'
            unauthenticatedChallenge = 'PASS'
            authenticatedUnauthorizedDeny = 'PASS'
            auditView = 'PASS'
            auditFilterDetail = 'PASS'
            auditExport = 'PASS'
            integrityPost = 'PASS'
            csrfNegative = 'PASS'
            syntheticOnly = $true
            realCredentials = $false
            personalData = $false
        }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifactDir 'manifest.json') -Encoding utf8

    $artifactText = Get-ChildItem $artifactDir -File | Where-Object { $_.Extension -in @('.html','.json','.log','.txt') } | ForEach-Object { Get-Content $_ -Raw } | Out-String
    Assert-NoForbiddenEvidence $artifactText $syntheticPassword
    Write-Host "P11 protected Audit runtime/browser acceptance passed on exact head $head."
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(5000) | Out-Null
    }
    $env:Setup__BypassGateForRegression = $previousBypass
    $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    Remove-Item $helperDir -Recurse -Force -ErrorAction SilentlyContinue
}
