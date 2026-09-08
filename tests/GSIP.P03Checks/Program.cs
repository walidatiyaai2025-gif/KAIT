using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Application.Identity;
using GSIP.Infrastructure;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var failures = new List<string>();
var sqlServer = Environment.GetEnvironmentVariable("GSIP_P03_SQL_SERVER");

CheckSourceContracts(failures);
if (!string.IsNullOrWhiteSpace(sqlServer))
{
    await CheckIdentityPersistenceAsync(sqlServer, failures);
}
else
{
    Console.WriteLine("P03 SQL runtime check skipped because GSIP_P03_SQL_SERVER is not set.");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("P03 checks FAILED:");
    failures.ForEach(failure => Console.Error.WriteLine($" - {failure}"));
    return 1;
}

Console.WriteLine("P03 checks passed: Identity schema, bootstrap transfer, configurable lockout, disabled-account rejection and sanitized authentication audit persistence are valid.");
return 0;

static void CheckSourceContracts(List<string> failures)
{
    var root = FindRepositoryRoot();
    var context = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Setup", "GsipDbContext.cs"));
    var authentication = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Identity", "AccountAuthenticationService.cs"));
    var migration = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Setup", "Migrations", "20260908132000_IdentityFoundation.cs"));

    Expect(context.Contains("IdentityDbContext<ApplicationUser", StringComparison.Ordinal), "GsipDbContext must use the mature ASP.NET Core Identity store.", failures);
    Expect(context.Contains("AuthenticationAuditEvents", StringComparison.Ordinal), "Authentication audit persistence is missing.", failures);
    Expect(authentication.Contains("[ACTIVATED]", StringComparison.Ordinal), "Bootstrap password hashes must be erased after Identity activation.", failures);
    Expect(!authentication.Contains("Console.Write", StringComparison.Ordinal), "Authentication code must not write credentials or account inputs to console output.", failures);
    foreach (var table in new[] { "AspNetUsers", "AspNetUserTokens", "AuthenticationAuditEvents" })
    {
        Expect(migration.Contains(table, StringComparison.Ordinal), $"Identity migration is missing {table}.", failures);
    }
}

static async Task CheckIdentityPersistenceAsync(string sqlServer, List<string> failures)
{
    var temporaryRoot = Path.Combine(Path.GetTempPath(), "gsip-p03-" + Guid.NewGuid().ToString("N"));
    var keys = Path.Combine(temporaryRoot, "keys");
    var setupDirectory = Path.Combine(temporaryRoot, "App_Data", "setup");
    Directory.CreateDirectory(keys);
    Directory.CreateDirectory(setupDirectory);

    var databaseName = "GSIP_P03_CI_" + Guid.NewGuid().ToString("N")[..12];
    var masterConnection = new SqlConnectionStringBuilder
    {
        DataSource = sqlServer,
        InitialCatalog = "master",
        IntegratedSecurity = true,
        Encrypt = false,
        TrustServerCertificate = true
    }.ConnectionString;
    var targetConnection = new SqlConnectionStringBuilder
    {
        DataSource = sqlServer,
        InitialCatalog = databaseName,
        IntegratedSecurity = true,
        Encrypt = false,
        TrustServerCertificate = true
    }.ConnectionString;

    try
    {
        await using (var master = new SqlConnection(masterConnection))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
        }

        var protection = DataProtectionProvider.Create(
            new DirectoryInfo(keys),
            configuration => configuration.SetApplicationName("GSIP"));
        var completionJson = JsonSerializer.Serialize(new
        {
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ConnectionString = targetConnection,
            Culture = "en"
        });
        var protectedCompletion = protection.CreateProtector("GSIP.Setup.State.v1").Protect(completionJson);
        await File.WriteAllTextAsync(Path.Combine(setupDirectory, "completed.protected"), protectedCompletion);

        var environment = new TestHostEnvironment
        {
            ApplicationName = "GSIP.P03Checks",
            EnvironmentName = Environments.Development,
            ContentRootPath = temporaryRoot,
            ContentRootFileProvider = new PhysicalFileProvider(temporaryRoot)
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentitySecurity:AllowRememberMe"] = "true",
                ["IdentitySecurity:RememberMeDays"] = "7",
                ["IdentitySecurity:MfaChallengeMinutes"] = "5"
            })
            .Build();
        var clock = new TestClock { UtcNow = new DateTimeOffset(2026, 9, 8, 13, 20, 0, TimeSpan.Zero) };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keys))
            .SetApplicationName("GSIP");
        services.AddGsipInfrastructure(configuration, environment);
        services.AddSingleton<GSIP.Application.Abstractions.ISystemClock>(clock);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GsipDbContext>();
        await db.Database.MigrateAsync();
        Expect(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Identity migration remains pending.", failures);

        const string bootstrapPassword = "P03!StrongBootstrap123";
        var bootstrap = new BootstrapAdministrator
        {
            Id = Guid.NewGuid(),
            DisplayName = "Synthetic P03 Administrator",
            Username = "p03admin",
            NormalizedUsername = "P03ADMIN",
            Email = "p03admin@example.invalid",
            MustChangePassword = true,
            CreatedAtUtc = clock.UtcNow
        };
        bootstrap.PasswordHash = new PasswordHasher<BootstrapAdministrator>().HashPassword(bootstrap, bootstrapPassword);
        db.SystemSetup.Add(new SystemSetupRecord
        {
            Id = 1,
            CompletedAtUtc = clock.UtcNow,
            OrganizationNameEn = "GSIP Test",
            OrganizationNameAr = "اختبار GSIP",
            PrimaryColor = "#0b4f7d",
            TimeZoneId = "Arab Standard Time",
            SessionTimeoutMinutes = 30,
            LockoutMinutes = 15,
            MaxFailedAccessAttempts = 3,
            RequireMfaForPrivilegedAccounts = true,
            DefaultEnvironment = "UAT",
            IntegrationTimeoutSeconds = 30,
            ValidateServerCertificate = true
        });
        db.BootstrapAdministrators.Add(bootstrap);
        await db.SaveChangesAsync();

        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "p03-synthetic-correlation"
        };

        var authentication = scope.ServiceProvider.GetRequiredService<IAccountAuthenticationService>();
        await authentication.EnsureBootstrapAdministratorAsync();
        var user = await db.Users.SingleAsync();
        var transferred = await db.BootstrapAdministrators.SingleAsync();
        Expect(user.Id == bootstrap.Id && user.IsPrivileged && user.MustChangePassword, "Bootstrap administrator was not transferred with its security flags.", failures);
        Expect(transferred.PasswordHash == "[ACTIVATED]" && transferred.ActivatedAtUtc is not null, "Bootstrap password hash was not erased after activation.", failures);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var rejected = await authentication.PasswordSignInAsync("p03admin", "incorrect synthetic value", false);
            Expect(rejected.Status == AccountSignInStatus.InvalidCredentials, "Pre-threshold invalid password did not return a generic rejection.", failures);
        }
        var locked = await authentication.PasswordSignInAsync("p03admin", "incorrect synthetic value", false);
        Expect(locked.Status == AccountSignInStatus.LockedOut, "Configured failed-attempt threshold did not lock the account.", failures);

        user = await db.Users.SingleAsync();
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await db.SaveChangesAsync();
        var disable = await authentication.SetAccountEnabledAsync(user.Id, false);
        Expect(disable.Succeeded, "Account disable operation failed.", failures);
        var disabled = await authentication.PasswordSignInAsync("p03admin", bootstrapPassword, false);
        Expect(disabled.Status == AccountSignInStatus.Disabled, "A disabled account was not rejected before sign-in.", failures);

        var audit = await db.AuthenticationAuditEvents.AsNoTracking().ToListAsync();
        Expect(audit.Any(x => x.EventType == "LoginFailure" && x.ResultCode == "INVALID_CREDENTIALS"), "Invalid-login audit event is missing.", failures);
        Expect(audit.Any(x => x.EventType == "AccountLockout" && x.ResultCode == "THRESHOLD_REACHED"), "Lockout audit event is missing.", failures);
        Expect(audit.Any(x => x.EventType == "AccountStatusChange" && x.ResultCode == "DISABLED"), "Account-disable audit event is missing.", failures);
        var serializedAudit = JsonSerializer.Serialize(audit);
        Expect(!serializedAudit.Contains(bootstrapPassword, StringComparison.Ordinal)
            && !serializedAudit.Contains("incorrect synthetic value", StringComparison.Ordinal), "Authentication audit data contains credential material.", failures);
    }
    finally
    {
        SqlConnection.ClearAllPools();
        try
        {
            await using var master = new SqlConnection(masterConnection);
            await master.OpenAsync();
            await using var drop = master.CreateCommand();
            drop.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
            await drop.ExecuteNonQueryAsync();
        }
        catch (SqlException exception)
        {
            failures.Add($"P03 test database cleanup failed: {exception.Number}.");
        }
        try
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "global.json"))
            && File.Exists(Path.Combine(directory.FullName, "CURRENT_PHASE.md")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Repository root could not be located.");
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

sealed class TestClock : GSIP.Application.Abstractions.ISystemClock
{
    public DateTimeOffset UtcNow { get; set; }
}

sealed class TestHostEnvironment : IHostEnvironment
{
    public required string EnvironmentName { get; set; }
    public required string ApplicationName { get; set; }
    public required string ContentRootPath { get; set; }
    public required IFileProvider ContentRootFileProvider { get; set; }
}

