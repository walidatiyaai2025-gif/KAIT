$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$p06Sha = 'fef5882abf8a6f12990c3e7c0e9f849d08cd7947'
$p07Sha = '9535fa158441160ab7c7d204863776e38e560a33'
$migrationsPath = 'src/GSIP.Infrastructure/Setup/Migrations'
$workId = [Guid]::NewGuid().ToString('N')
$p06Root = Join-Path $env:TEMP "gsip-p07-p06-$workId"
$helperRoot = Join-Path $env:TEMP "gsip-p07-upgrade-$workId"
$keyDirectory = Join-Path $env:TEMP "gsip-p07-keys-$workId"
$manifestPath = Join-Path $helperRoot 'p06-state-manifest.json'
$upgradeDatabase = "GSIP_P07_UPGRADE_$($workId.Substring(0,12))"
$cleanDatabase = "GSIP_P07_CLEAN_$($workId.Substring(0,12))"
$upgradeConnection = "Server=(localdb)\MSSQLLocalDB;Database=$upgradeDatabase;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
$cleanConnection = "Server=(localdb)\MSSQLLocalDB;Database=$cleanDatabase;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"

function Invoke-Checked([string]$Label, [scriptblock]$Action) {
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

Push-Location $root
try {
    Invoke-Checked 'Resolve exact P06 commit' { git cat-file -e "$p06Sha^{commit}" }
    Invoke-Checked 'Resolve exact P07 closure commit' { git cat-file -e "$p07Sha^{commit}" }

    # P07 itself intentionally introduced no database migration. Validate that immutable
    # closed-phase boundary against the exact P07 closure SHA, not against a later phase
    # candidate that may legitimately introduce P10+ migrations.
    $p07MigrationDiff = @(git diff --name-only "$p06Sha..$p07Sha" -- $migrationsPath)
    if ($LASTEXITCODE -ne 0) { throw 'Could not compare exact P06 and P07 migration paths.' }
    if ($p07MigrationDiff.Count -ne 0) {
        throw "Closed P07 baseline introduced unexpected database migration drift: $($p07MigrationDiff -join ', ')"
    }

    New-Item -ItemType Directory -Path $helperRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
    Invoke-Checked 'Create exact P06 worktree' { git worktree add --detach $p06Root $p06Sha }

    $p06Infra = (Join-Path $p06Root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
    $p06Application = (Join-Path $p06Root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
    $p06Domain = (Join-Path $p06Root 'src/GSIP.Domain/GSIP.Domain.csproj').Replace('\','/')
    $currentInfra = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
    $currentApplication = (Join-Path $root 'src/GSIP.Application/GSIP.Application.csproj').Replace('\','/')
    $currentDomain = (Join-Path $root 'src/GSIP.Domain/GSIP.Domain.csproj').Replace('\','/')

    $seedDir = Join-Path $helperRoot 'SeedExactP06'
    $verifyDir = Join-Path $helperRoot 'VerifyP07'
    New-Item -ItemType Directory -Path $seedDir,$verifyDir -Force | Out-Null

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <ProjectReference Include="$p06Infra" />
    <ProjectReference Include="$p06Application" />
    <ProjectReference Include="$p06Domain" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $seedDir 'SeedExactP06.csproj') -Encoding utf8

    @'
using System.Security.Cryptography;
using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

if (args.Length != 3) throw new ArgumentException("Expected connection, key directory and manifest path.");
var connectionString = args[0];
var keyDirectory = args[1];
var manifestPath = args[2];
var now = DateTimeOffset.Parse("2026-09-08T22:30:00Z");
var entityId = Guid.Parse("77000000-0000-0000-0000-000000000701");
var serviceId = Guid.Parse("77000000-0000-0000-0000-000000000702");
var uatConfigId = Guid.Parse("77000000-0000-0000-0000-000000000703");
var prodConfigId = Guid.Parse("77000000-0000-0000-0000-000000000704");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connectionString).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
await db.Database.MigrateAsync();
var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
Assert(appliedMigrations.Length > 0 && appliedMigrations[^1] == "20260908193500_SecretAuthFoundation",
    "Exact P06 did not initialize the expected final P06 schema.");
Assert(await db.CatalogEnvironments.AnyAsync(x => x.Id == CatalogEnvironmentCodes.UatId)
       && await db.CatalogEnvironments.AnyAsync(x => x.Id == CatalogEnvironmentCodes.ProductionId),
    "Exact P06 clean schema is missing UAT/Production environments.");

var service = new CatalogService
{
    Id = serviceId,
    DefinitionKey = Guid.Parse("77000000-0000-0000-0000-000000000705"),
    EntityId = entityId,
    Code = "P07-UPGRADE-SERVICE",
    NameAr = "خدمة ترقية تجريبية",
    NameEn = "P07 Upgrade Synthetic Service",
    DescriptionAr = "بيانات اصطناعية لاختبار حفظ الترقية فقط.",
    DescriptionEn = "Synthetic data used only for upgrade preservation acceptance.",
    Active = true,
    Version = 1,
    IsCurrent = true,
    CreatedAtUtc = now,
    UpdatedAtUtc = now
};
service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
{
    Id = uatConfigId,
    ServiceId = serviceId,
    EnvironmentId = CatalogEnvironmentCodes.UatId,
    BaseUrl = "https://p07-upgrade-uat.example.invalid",
    RelativePath = "/synthetic/uat",
    HttpMethod = "POST",
    ContentType = "application/json",
    NonSecretHeadersJson = "{\"X-Synthetic-Mode\":\"uat\"}",
    TimeoutSeconds = 31,
    TlsPolicy = "SystemDefault",
    ValidateServerCertificate = true,
    Active = true
});
service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
{
    Id = prodConfigId,
    ServiceId = serviceId,
    EnvironmentId = CatalogEnvironmentCodes.ProductionId,
    BaseUrl = "https://p07-upgrade-prod.example.invalid",
    RelativePath = "/synthetic/prod",
    HttpMethod = "POST",
    ContentType = "application/json",
    NonSecretHeadersJson = "{\"X-Synthetic-Mode\":\"prod\"}",
    TimeoutSeconds = 47,
    TlsPolicy = "SystemDefault",
    ValidateServerCertificate = true,
    Active = true
});
var entity = new CatalogEntity
{
    Id = entityId,
    Code = "P07-UPGRADE-ENTITY",
    NameAr = "جهة ترقية تجريبية",
    NameEn = "P07 Upgrade Synthetic Authority",
    Logo = string.Empty,
    Active = true,
    DisplayOrder = 77,
    CreatedAtUtc = now,
    UpdatedAtUtc = now,
    Services = new List<CatalogService> { service }
};
db.CatalogEntities.Add(entity);
await db.SaveChangesAsync();

Directory.CreateDirectory(keyDirectory);
var provider = DataProtectionProvider.Create(
    new DirectoryInfo(keyDirectory),
    builder => builder.SetApplicationName("GSIP.P07.UpgradePersistenceAcceptance"));
var clock = new FixedClock(now);
var vault = new DataProtectionSecretVault(db, provider, clock);
var profiles = new AuthProfileService(db, vault, clock);
var secretMaterial = RandomNumberGenerator.GetBytes(48);
SecretDescriptor secret;
try
{
    secret = await vault.CreateActiveAsync(
        serviceId,
        CatalogEnvironmentCodes.UatId,
        null,
        "api-key",
        secretMaterial);
}
finally
{
    CryptographicOperations.ZeroMemory(secretMaterial);
}

var profile = await profiles.CreateAsync(new CreateAuthProfileCommand(
    serviceId,
    CatalogEnvironmentCodes.UatId,
    "P07 Upgrade Synthetic UAT",
    AuthProfileType.ApiKeyHeader,
    "p07-upgrade-acceptance",
    new Dictionary<string, SecretRef> { ["api-key"] = secret.Reference }));

var uat = await db.ServiceEnvironmentConfigs.AsNoTracking().SingleAsync(x => x.Id == uatConfigId);
var prod = await db.ServiceEnvironmentConfigs.AsNoTracking().SingleAsync(x => x.Id == prodConfigId);
var vaultRow = await db.SecretVaultEntries.AsNoTracking().SingleAsync(x => x.Reference == secret.Reference.Value);
Assert(uat.AuthProfileId == profile.Id, "Exact P06 did not persist the UAT AuthProfile binding.");
Assert(prod.AuthProfileId is null, "Exact P06 unexpectedly propagated the UAT AuthProfile to Production.");
Assert(vaultRow.OwnerServiceId == serviceId
       && vaultRow.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
       && vaultRow.OwnerAuthProfileId == profile.Id,
    "Exact P06 did not persist the expected vault ownership chain.");
Assert(vaultRow.ProtectedPayload.Length > 48, "Vault payload does not appear to be protected ciphertext.");

var manifest = new
{
    SourcePhase = "P06",
    SourceSha = "fef5882abf8a6f12990c3e7c0e9f849d08cd7947",
    AppliedMigrations = appliedMigrations,
    EntityId = entityId,
    ServiceId = serviceId,
    UatConfigId = uatConfigId,
    ProductionConfigId = prodConfigId,
    ProfileId = profile.Id,
    SecretReference = secret.Reference.Value,
    ProtectedPayloadSha256 = Convert.ToHexString(SHA256.HashData(vaultRow.ProtectedPayload)),
    UatBaseUrl = uat.BaseUrl,
    ProductionBaseUrl = prod.BaseUrl,
    UatTimeoutSeconds = uat.TimeoutSeconds,
    ProductionTimeoutSeconds = prod.TimeoutSeconds
};
await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
Console.WriteLine($"P06_SEED_OK migrations={appliedMigrations.Length} entity={entityId} service={serviceId} profile={profile.Id}");

sealed class FixedClock(DateTimeOffset now) : ISystemClock
{
    public DateTimeOffset UtcNow => now;
}
'@ | Set-Content (Join-Path $seedDir 'Program.cs') -Encoding utf8

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <ProjectReference Include="$currentInfra" />
    <ProjectReference Include="$currentApplication" />
    <ProjectReference Include="$currentDomain" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $verifyDir 'VerifyP07.csproj') -Encoding utf8

    @'
using System.Security.Cryptography;
using System.Text.Json;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

if (args.Length != 3) throw new ArgumentException("Expected upgrade connection, clean connection and manifest path.");
var upgradeConnection = args[0];
var cleanConnection = args[1];
var manifestPath = args[2];

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
var manifest = manifestDocument.RootElement;
Assert(manifest.GetProperty("SourceSha").GetString() == "fef5882abf8a6f12990c3e7c0e9f849d08cd7947",
    "Upgrade manifest was not created from the exact closed P06 implementation SHA.");
var expectedMigrations = manifest.GetProperty("AppliedMigrations").EnumerateArray().Select(x => x.GetString()!).ToArray();
var entityId = manifest.GetProperty("EntityId").GetGuid();
var serviceId = manifest.GetProperty("ServiceId").GetGuid();
var uatConfigId = manifest.GetProperty("UatConfigId").GetGuid();
var prodConfigId = manifest.GetProperty("ProductionConfigId").GetGuid();
var profileId = manifest.GetProperty("ProfileId").GetGuid();
var secretReference = manifest.GetProperty("SecretReference").GetString()!;
var protectedPayloadHash = manifest.GetProperty("ProtectedPayloadSha256").GetString()!;
var expectedUatBaseUrl = manifest.GetProperty("UatBaseUrl").GetString()!;
var expectedProdBaseUrl = manifest.GetProperty("ProductionBaseUrl").GetString()!;
var expectedUatTimeout = manifest.GetProperty("UatTimeoutSeconds").GetInt32();
var expectedProdTimeout = manifest.GetProperty("ProductionTimeoutSeconds").GetInt32();

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(upgradeConnection).Options;
await using (var db = new GsipDbContext(options))
{
    var currentKnownMigrations = db.Database.GetMigrations().ToArray();
    Assert(currentKnownMigrations.Length >= expectedMigrations.Length
           && currentKnownMigrations.Take(expectedMigrations.Length).SequenceEqual(expectedMigrations, StringComparer.Ordinal),
        "Current candidate no longer preserves the exact closed-P06 migration prefix.");

    var expectedPostP06Migrations = currentKnownMigrations.Skip(expectedMigrations.Length).ToArray();
    var pendingBeforeUpgrade = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    Assert(pendingBeforeUpgrade.SequenceEqual(expectedPostP06Migrations, StringComparer.Ordinal),
        "Pending migration set does not match the current candidate's post-P06 migration suffix.");

    await db.Database.MigrateAsync();
    var appliedAfterUpgrade = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    Assert(appliedAfterUpgrade.SequenceEqual(currentKnownMigrations, StringComparer.Ordinal),
        "Upgrading the exact closed-P06 database did not produce the current candidate migration set.");

    var entity = await db.CatalogEntities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == entityId);
    var service = await db.CatalogServices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceId);
    var uat = await db.ServiceEnvironmentConfigs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == uatConfigId);
    var prod = await db.ServiceEnvironmentConfigs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == prodConfigId);
    var profile = await db.AuthProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == profileId);
    var binding = await db.AuthProfileBindings.AsNoTracking().SingleOrDefaultAsync(x => x.AuthProfileId == profileId && !x.IsShared);
    var slot = await db.AuthProfileSecrets.AsNoTracking().SingleOrDefaultAsync(x => x.AuthProfileId == profileId && x.SecretName == "api-key");
    var vault = await db.SecretVaultEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Reference == secretReference);

    Assert(entity is not null && entity.Code == "P07-UPGRADE-ENTITY", "Candidate upgrade lost or mutated P06 entity metadata.");
    Assert(service is not null && service.EntityId == entityId && service.Code == "P07-UPGRADE-SERVICE",
        "Candidate upgrade lost or mutated P06 service metadata.");
    Assert(uat is not null && prod is not null, "Candidate upgrade lost an environment configuration.");
    Assert(uat.ServiceId == serviceId && prod.ServiceId == serviceId
           && uat.EnvironmentId == CatalogEnvironmentCodes.UatId
           && prod.EnvironmentId == CatalogEnvironmentCodes.ProductionId,
        "Candidate upgrade corrupted Service+Environment referential isolation.");
    Assert(uat.BaseUrl == expectedUatBaseUrl && prod.BaseUrl == expectedProdBaseUrl
           && uat.BaseUrl != prod.BaseUrl
           && uat.TimeoutSeconds == expectedUatTimeout && prod.TimeoutSeconds == expectedProdTimeout,
        "Candidate upgrade mutated or collapsed independent UAT/Production configuration.");
    Assert(profile is not null && profile.OwnerServiceId == serviceId
           && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId && profile.IsEnabled,
        "Candidate upgrade lost or changed the P06 AuthProfile owner scope.");
    Assert(uat.AuthProfileId == profileId && prod.AuthProfileId is null,
        "Candidate upgrade leaked the UAT AuthProfile binding into Production.");
    Assert(binding is not null && binding.ServiceId == serviceId
           && binding.EnvironmentId == CatalogEnvironmentCodes.UatId,
        "Candidate upgrade broke AuthProfile binding referential integrity.");
    Assert(slot is not null && slot.SecretReference == secretReference && slot.Generation == 1,
        "Candidate upgrade broke the AuthProfile secret slot/reference.");
    Assert(vault is not null && vault.State == SecretLifecycleState.Active
           && vault.OwnerServiceId == serviceId
           && vault.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
           && vault.OwnerAuthProfileId == profileId,
        "Candidate upgrade broke the protected vault ownership chain.");
    Assert(Convert.ToHexString(SHA256.HashData(vault.ProtectedPayload)) == protectedPayloadHash,
        "Candidate upgrade changed the protected vault payload bytes.");
    Assert(vault.ProtectedPayload.Length > 48,
        "Candidate upgrade left a vault payload that does not appear to be protected ciphertext.");

    Console.WriteLine($"P07_UPGRADE_OK migrations={appliedAfterUpgrade.Length} postP06={expectedPostP06Migrations.Length} entity={entityId} service={serviceId} profile={profileId}");
    await db.Database.EnsureDeletedAsync();
}

var cleanOptions = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(cleanConnection).Options;
await using (var clean = new GsipDbContext(cleanOptions))
{
    await clean.Database.EnsureDeletedAsync();
    await clean.Database.MigrateAsync();
    var cleanApplied = (await clean.Database.GetAppliedMigrationsAsync()).ToArray();
    var cleanKnown = clean.Database.GetMigrations().ToArray();
    Assert(cleanApplied.SequenceEqual(cleanKnown, StringComparer.Ordinal),
        "Current candidate clean install did not apply its complete known migration set.");
    Assert(cleanApplied.Length >= expectedMigrations.Length
           && cleanApplied.Take(expectedMigrations.Length).SequenceEqual(expectedMigrations, StringComparer.Ordinal),
        "Current candidate clean install no longer preserves the exact closed-P06 migration prefix.");
    Assert(await clean.CatalogEnvironments.AsNoTracking().AnyAsync(x => x.Id == CatalogEnvironmentCodes.UatId)
           && await clean.CatalogEnvironments.AsNoTracking().AnyAsync(x => x.Id == CatalogEnvironmentCodes.ProductionId),
        "Current candidate clean install schema is missing canonical UAT/Production environments.");
    Assert(await clean.AuthProfiles.CountAsync() == 0 && await clean.SecretVaultEntries.CountAsync() == 0,
        "Current candidate clean install unexpectedly created AuthProfile or vault data.");
    Console.WriteLine($"P07_CLEAN_INSTALL_OK migrations={cleanApplied.Length}");
    await clean.Database.EnsureDeletedAsync();
}
'@ | Set-Content (Join-Path $verifyDir 'Program.cs') -Encoding utf8

    Invoke-Checked 'Seed exact closed-P06 database' {
        dotnet run --project (Join-Path $seedDir 'SeedExactP06.csproj') --configuration Release -- $upgradeConnection $keyDirectory $manifestPath
    }
    Invoke-Checked 'Verify closed-P06 data survives current-candidate upgrade and clean install' {
        dotnet run --project (Join-Path $verifyDir 'VerifyP07.csproj') --configuration Release -- $upgradeConnection $cleanConnection $manifestPath
    }

    $manifestText = Get-Content $manifestPath -Raw
    if ($manifestText -match '(?i)password|bearer\s+[A-Za-z0-9._-]+|api[-_ ]?key\s*[:=]\s*[^"}]+') {
        throw 'Upgrade evidence manifest contains secret-like material.'
    }

    Write-Host "P07_UPGRADE_PERSISTENCE_ACCEPTANCE=PASS"
    Write-Host "SOURCE_P06_SHA=$p06Sha"
    Write-Host "SOURCE_P07_SHA=$p07Sha"
    Write-Host "P07_SCHEMA_CHANGE=NONE_AT_P07_BASELINE"
    Write-Host "POST_P07_MIGRATIONS=ALLOWED_WHEN_CURRENT_PHASE_REQUIRES"
    Write-Host "DATA_PRESERVATION=PASS"
    Write-Host "CONFIGURATION_ISOLATION=PASS"
    Write-Host "PROTECTED_VAULT_PAYLOAD_PRESERVED=PASS"
    Write-Host "CLEAN_INSTALL_SCHEMA=PASS"
}
finally {
    Pop-Location
    if (Test-Path $p06Root) {
        git -C $root worktree remove --force $p06Root 2>$null | Out-Null
        git -C $root worktree prune 2>$null | Out-Null
    }
    Remove-Item $helperRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $keyDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
