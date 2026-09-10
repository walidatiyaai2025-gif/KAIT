$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactDir = Join-Path $root 'artifacts/p16-full-acceptance/recovery'
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$candidateSha = (git -C $root rev-parse HEAD).Trim()
if ([string]::IsNullOrWhiteSpace($candidateSha)) { throw 'Could not resolve exact P16 candidate SHA.' }
if ($env:CANDIDATE_SHA -and $candidateSha -ne $env:CANDIDATE_SHA) {
    throw "P16 recovery candidate mismatch: expected=$env:CANDIDATE_SHA actual=$candidateSha"
}

$databaseName = 'GSIP_P16_RECOVERY_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;MultipleActiveResultSets=true"
$backupPath = Join-Path $env:RUNNER_TEMP ($databaseName + '.bak')
$marker = 'SYNTHETIC-P16-RECOVERY-' + [Guid]::NewGuid().ToString('N')
$helperDir = Join-Path $env:RUNNER_TEMP ('gsip-p16-recovery-helper-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $helperDir | Out-Null

$infraProject = (Join-Path $root 'src/GSIP.Infrastructure/GSIP.Infrastructure.csproj').Replace('\','/')
$helperProject = Join-Path $helperDir 'P16Recovery.csproj'
$helperSource = Join-Path $helperDir 'Program.cs'

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="$infraProject" /></ItemGroup>
</Project>
"@ | Set-Content $helperProject -Encoding utf8

@'
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var connectionString = args[0];
var backupPath = Path.GetFullPath(args[1]);
var marker = args[2];
var builder = new SqlConnectionStringBuilder(connectionString);
var databaseName = builder.InitialCatalog;
if (string.IsNullOrWhiteSpace(databaseName)) throw new InvalidOperationException("Database name is required.");

var dbOptions = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connectionString).Options;
await using (var db = new GsipDbContext(dbOptions))
{
    await db.Database.MigrateAsync();
    if (await db.SystemSetup.AnyAsync()) throw new InvalidOperationException("Synthetic recovery database was not clean.");
    db.SystemSetup.Add(new SystemSetupRecord
    {
        Id = 1,
        CompletedAtUtc = DateTimeOffset.UtcNow,
        OrganizationNameEn = marker,
        OrganizationNameAr = "بيئة استعادة اصطناعية P16",
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
    await db.SaveChangesAsync();
}

builder.InitialCatalog = "master";
var masterConnectionString = builder.ConnectionString;
var safeDatabase = databaseName.Replace("]", "]]", StringComparison.Ordinal);
var safeBackup = backupPath.Replace("'", "''", StringComparison.Ordinal);

async Task ExecuteMasterAsync(string sql)
{
    await using var connection = new SqlConnection(masterConnectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandTimeout = 120;
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
}

await ExecuteMasterAsync($"BACKUP DATABASE [{safeDatabase}] TO DISK = N'{safeBackup}' WITH INIT, COPY_ONLY, CHECKSUM");
await ExecuteMasterAsync($"RESTORE VERIFYONLY FROM DISK = N'{safeBackup}' WITH CHECKSUM");
SqlConnection.ClearAllPools();
await ExecuteMasterAsync($"ALTER DATABASE [{safeDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{safeDatabase}]");
await ExecuteMasterAsync($"RESTORE DATABASE [{safeDatabase}] FROM DISK = N'{safeBackup}' WITH REPLACE, RECOVERY");

await using (var verify = new GsipDbContext(dbOptions))
{
    var restored = await verify.SystemSetup.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1);
    if (restored is null || !string.Equals(restored.OrganizationNameEn, marker, StringComparison.Ordinal))
        throw new InvalidOperationException("Restored database did not preserve the synthetic recovery marker.");
}

SqlConnection.ClearAllPools();
await ExecuteMasterAsync($"ALTER DATABASE [{safeDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{safeDatabase}]");
Console.WriteLine("P16_SQL_BACKUP_RESTORE=PASS");
'@ | Set-Content $helperSource -Encoding utf8

$stateRoot = Join-Path $env:RUNNER_TEMP ('GSIP-P16-State-' + [Guid]::NewGuid().ToString('N'))
$liveAppData = Join-Path $stateRoot 'live/App_Data'
$backupAppData = Join-Path $stateRoot 'backup/App_Data'
$liveKeys = Join-Path $liveAppData 'keys'
$liveSetup = Join-Path $liveAppData 'setup'
New-Item -ItemType Directory -Force -Path $liveKeys,$liveSetup | Out-Null

$syntheticKey = Join-Path $liveKeys 'synthetic-p16-key.xml'
$syntheticSetup = Join-Path $liveSetup 'completed.protected'
('synthetic-key-' + $marker) | Set-Content $syntheticKey -Encoding utf8
('synthetic-protected-setup-' + $marker) | Set-Content $syntheticSetup -Encoding utf8
$keyHashBefore = (Get-FileHash $syntheticKey -Algorithm SHA256).Hash.ToLowerInvariant()
$setupHashBefore = (Get-FileHash $syntheticSetup -Algorithm SHA256).Hash.ToLowerInvariant()

try {
    Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
    & dotnet run --project $helperProject --configuration Release -- $connectionString $backupPath $marker
    if ($LASTEXITCODE -ne 0) { throw 'P16 SQL backup/restore helper failed.' }
    if (-not (Test-Path $backupPath)) { throw 'P16 SQL backup file was not created.' }
    $backupHash = (Get-FileHash $backupPath -Algorithm SHA256).Hash.ToLowerInvariant()

    New-Item -ItemType Directory -Force -Path (Split-Path $backupAppData -Parent) | Out-Null
    Copy-Item $liveAppData $backupAppData -Recurse -Force
    Remove-Item $liveAppData -Recurse -Force
    Copy-Item $backupAppData $liveAppData -Recurse -Force

    $restoredKey = Join-Path $liveAppData 'keys/synthetic-p16-key.xml'
    $restoredSetup = Join-Path $liveAppData 'setup/completed.protected'
    if (-not (Test-Path $restoredKey) -or -not (Test-Path $restoredSetup)) { throw 'Protected App_Data state was not restored.' }
    $keyHashAfter = (Get-FileHash $restoredKey -Algorithm SHA256).Hash.ToLowerInvariant()
    $setupHashAfter = (Get-FileHash $restoredSetup -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($keyHashBefore -ne $keyHashAfter) { throw 'Data Protection key-state hash changed during restore rehearsal.' }
    if ($setupHashBefore -ne $setupHashAfter) { throw 'Protected setup-state hash changed during restore rehearsal.' }

    $manifest = [ordered]@{
        CandidateSha = $candidateSha
        Unit = 'P16::full-acceptance-release-candidate'
        DatabaseEngine = 'SQL Server LocalDB synthetic rehearsal'
        DatabaseBackupSha256 = $backupHash
        SqlBackupRestore = 'PASS'
        SqlBackupVerifyOnly = 'PASS'
        DataProtectionStateRestore = 'PASS'
        ProtectedSetupStateRestore = 'PASS'
        DataProtectionStateSha256 = $keyHashAfter
        ProtectedSetupStateSha256 = $setupHashAfter
        SyntheticDataOnly = $true
        ProductionEvidence = 'NOT_CLAIMED'
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $artifactDir 'backup-restore-manifest.json') -Encoding utf8
    Write-Host 'P16_BACKUP_RESTORE_REHEARSAL=PASS'
}
finally {
    Remove-Item $helperDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $stateRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
}
