using GSIP.Application.Setup;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var root = FindRepositoryRoot();
var failures = new List<string>();
CheckSourceContract(root, failures);

var sqlServer = Environment.GetEnvironmentVariable("GSIP_P02_SQL_SERVER");
if (!string.IsNullOrWhiteSpace(sqlServer))
{
    await CheckRuntimeAsync(sqlServer, failures);
}
else
{
    Console.WriteLine("P02 SQL runtime check skipped because GSIP_P02_SQL_SERVER is not set.");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("P02 checks FAILED:");
    failures.ForEach(failure => Console.Error.WriteLine($" - {failure}"));
    return 1;
}

Console.WriteLine("P02 checks passed: setup gate, regression-bypass isolation, protected/restart-safe state, SQL wrong-credentials and migration-failure/retry paths, provisioning, bootstrap administrator and per-service environment placeholders are valid.");
return 0;

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "GSIP.sln")) && File.Exists(Path.Combine(directory.FullName, "CURRENT_PHASE.md")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Repository root could not be located.");
}

static void CheckSourceContract(string root, List<string> failures)
{
    var program = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Program.cs"));
    Expect(program.Contains("ISetupService", StringComparison.Ordinal), "Web pipeline does not enforce ISetupService setup state.", failures);
    Expect(program.Contains("/setup", StringComparison.Ordinal) && program.Contains("Response.Redirect", StringComparison.Ordinal), "First-run redirect to /setup is missing.", failures);
    Expect(program.Contains("Setup:BypassGateForRegression", StringComparison.Ordinal), "Closed-P01 regression bypass is not explicit/configuration-scoped.", failures);
    Expect(program.Contains("IsEnvironment(\"RegressionTesting\")", StringComparison.Ordinal), "Closed-P01 regression bypass is not restricted to the dedicated RegressionTesting host environment.", failures);
    Expect(program.Contains("PersistKeysToFileSystem", StringComparison.Ordinal), "Data Protection keys are not persisted for restart-safe setup state.", failures);

    var setupService = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Setup", "SetupService.cs"));
    foreach (var required in new[] { "TestDatabaseAsync", "ProvisionDatabaseAsync", "MigrateAsync", "PasswordHasher", "ServiceEnvironmentPlaceholders", "UAT", "Production", "Protect(" })
    {
        Expect(setupService.Contains(required, StringComparison.Ordinal), $"Setup service is missing required behavior: {required}.", failures);
    }
    Expect(!setupService.Contains("LogInformation", StringComparison.Ordinal) && !setupService.Contains("Console.WriteLine", StringComparison.Ordinal), "Setup service must not emit connection credentials to logs.", failures);

    var controller = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Controllers", "SetupController.cs"));
    foreach (var action in new[] { "Welcome", "Preflight", "Database", "Provision", "Administrator", "Branding", "Security", "Integration", "Notifications", "Finish" })
    {
        Expect(controller.Contains($"{action}(", StringComparison.Ordinal), $"Setup controller is missing {action} action.", failures);
    }
    Expect(controller.Contains("ValidateAntiForgeryToken", StringComparison.Ordinal), "Setup POST actions must be antiforgery-protected.", failures);

    var wizard = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Views", "Setup", "Wizard.cshtml"));
    foreach (var marker in new[] { "Test Connection", "SQL Authentication", "Encrypt", "TrustServerCertificate", "Production / Go Live", "Finish Setup" })
    {
        Expect(wizard.Contains(marker, StringComparison.Ordinal), $"Setup UI is missing expected control/text: {marker}.", failures);
    }
    Expect(!wizard.Contains("value=\"@Model.Draft.Database.Password\"", StringComparison.Ordinal), "Database password must never be rendered back into HTML.", failures);

    var infrastructureProject = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "GSIP.Infrastructure.csproj"));
    Expect(infrastructureProject.Contains("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal), "SQL Server EF provider is missing.", failures);

    var migration = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Setup", "Migrations", "20260908102000_InitialSetup.cs"));
    Expect(migration.Contains("BootstrapAdministrators", StringComparison.Ordinal) && migration.Contains("ServiceEnvironmentPlaceholders", StringComparison.Ordinal), "Initial EF migration is incomplete.", failures);

    var currentPhase = File.ReadAllText(Path.Combine(root, "CURRENT_PHASE.md"));
    var ledger = File.ReadAllText(Path.Combine(root, "docs", "TASK_LEDGER.md"));
    var p02StillCurrent = currentPhase.Contains("P02 — Complete first-run Setup Wizard", StringComparison.Ordinal);
    var laterCanonicalPhase = Enumerable.Range(3, 15).Any(number => currentPhase.Contains($"P{number:00} —", StringComparison.Ordinal));
    var p02Closed = ledger.Contains("| P02 | CLOSED |", StringComparison.Ordinal);
    Expect(p02StillCurrent || (laterCanonicalPhase && p02Closed), "P02 regression gate requires either active P02 or a later canonical phase with P02 CLOSED in the ledger.", failures);
}

static async Task CheckRuntimeAsync(string sqlServer, List<string> failures)
{
    var temporaryRoot = Path.Combine(Path.GetTempPath(), "gsip-p02-check-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temporaryRoot);
    var keys = Path.Combine(temporaryRoot, "keys");
    Directory.CreateDirectory(keys);
    var databaseName = "GSIP_P02_CI_" + Guid.NewGuid().ToString("N")[..12];
    var migrationRetryDatabaseName = "GSIP_P02_MIG_" + Guid.NewGuid().ToString("N")[..12];
    const string bootstrapPassword = "P02!StrongPass123";

    var environment = new TestHostEnvironment
    {
        ApplicationName = "GSIP.P02Checks",
        EnvironmentName = Environments.Development,
        ContentRootPath = temporaryRoot,
        ContentRootFileProvider = new PhysicalFileProvider(temporaryRoot)
    };
    var protection = DataProtectionProvider.Create(new DirectoryInfo(keys), configuration => configuration.SetApplicationName("GSIP-P02-Checks"));
    var passwordHasher = new PasswordHasher<BootstrapAdministrator>();
    var service = new SetupService(protection, passwordHasher, environment);

    var database = new DatabaseSetupOptions
    {
        Server = sqlServer,
        DatabaseName = databaseName,
        UseWindowsAuthentication = true,
        Encrypt = false,
        TrustServerCertificate = true,
        TimeoutSeconds = 30,
        CreateDatabase = false
    };

    try
    {
        var preflight = await service.RunPreflightAsync();
        Expect(preflight.Success, $"Preflight failed: {preflight.Code}", failures);

        var invalidName = new DatabaseSetupOptions
        {
            Server = sqlServer,
            DatabaseName = "GSIP-P02-invalid-name",
            UseWindowsAuthentication = true,
            Encrypt = false,
            TrustServerCertificate = true,
            TimeoutSeconds = 30,
            CreateDatabase = true
        };
        var invalidNameResult = await service.TestDatabaseAsync(invalidName);
        Expect(!invalidNameResult.Success && invalidNameResult.Code == "DATABASE_NAME_INVALID", $"Invalid database-name negative path returned {invalidNameResult.Code}.", failures);

        var missingSqlCredentials = new DatabaseSetupOptions
        {
            Server = sqlServer,
            DatabaseName = databaseName,
            UseWindowsAuthentication = false,
            Username = string.Empty,
            Password = string.Empty,
            Encrypt = false,
            TrustServerCertificate = true,
            TimeoutSeconds = 30,
            CreateDatabase = true
        };
        var credentialsResult = await service.TestDatabaseAsync(missingSqlCredentials);
        Expect(!credentialsResult.Success && credentialsResult.Code == "SQL_CREDENTIALS_REQUIRED", $"Missing SQL-credentials negative path returned {credentialsResult.Code}.", failures);

        var wrongSqlCredentials = new DatabaseSetupOptions
        {
            Server = sqlServer,
            DatabaseName = databaseName,
            UseWindowsAuthentication = false,
            Username = "gsip_invalid_login_" + Guid.NewGuid().ToString("N")[..8],
            Password = "WrongP02!Credential123",
            Encrypt = false,
            TrustServerCertificate = true,
            TimeoutSeconds = 3,
            CreateDatabase = false
        };
        var wrongCredentialsResult = await service.TestDatabaseAsync(wrongSqlCredentials);
        Expect(!wrongCredentialsResult.Success && wrongCredentialsResult.Code == "SQL_CONNECTION_FAILED", $"Wrong SQL-credentials negative path returned {wrongCredentialsResult.Code}.", failures);

        var connection = await service.TestDatabaseAsync(database);
        Expect(connection.Success, $"SQL connection test failed: {connection.Code}", failures);

        var migrationRetryDatabase = new DatabaseSetupOptions
        {
            Server = sqlServer,
            DatabaseName = migrationRetryDatabaseName,
            UseWindowsAuthentication = true,
            Encrypt = false,
            TrustServerCertificate = true,
            TimeoutSeconds = 30,
            CreateDatabase = false
        };
        var masterConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = sqlServer,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            Encrypt = false,
            TrustServerCertificate = true
        }.ConnectionString;
        await using (var migrationMaster = new SqlConnection(masterConnectionString))
        {
            await migrationMaster.OpenAsync();
            await using var createMigrationDatabase = migrationMaster.CreateCommand();
            createMigrationDatabase.CommandText = $"CREATE DATABASE [{migrationRetryDatabaseName}]";
            await createMigrationDatabase.ExecuteNonQueryAsync();
        }
        var migrationTargetConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = sqlServer,
            InitialCatalog = migrationRetryDatabaseName,
            IntegratedSecurity = true,
            Encrypt = false,
            TrustServerCertificate = true
        }.ConnectionString;
        await using (var migrationTarget = new SqlConnection(migrationTargetConnectionString))
        {
            await migrationTarget.OpenAsync();
            await using var createConflict = migrationTarget.CreateCommand();
            createConflict.CommandText = "CREATE TABLE [SystemSetup] ([Id] int NOT NULL PRIMARY KEY)";
            await createConflict.ExecuteNonQueryAsync();
        }
        var migrationFailure = await service.ProvisionDatabaseAsync(migrationRetryDatabase);
        Expect(!migrationFailure.Success && migrationFailure.Code == "DATABASE_PROVISION_FAILED", $"Migration-failure negative path returned {migrationFailure.Code}.", failures);
        await using (var migrationTarget = new SqlConnection(migrationTargetConnectionString))
        {
            await migrationTarget.OpenAsync();
            await using var removeConflict = migrationTarget.CreateCommand();
            removeConflict.CommandText = "DROP TABLE [SystemSetup]";
            await removeConflict.ExecuteNonQueryAsync();
        }
        var migrationRetry = await service.ProvisionDatabaseAsync(migrationRetryDatabase);
        Expect(migrationRetry.Success, $"Migration retry failed after removing the blocking schema conflict: {migrationRetry.Code}", failures);

        var missingDatabase = await service.ProvisionDatabaseAsync(database);
        Expect(!missingDatabase.Success && missingDatabase.Code == "DATABASE_NOT_FOUND", $"Existing-database negative path returned {missingDatabase.Code} instead of DATABASE_NOT_FOUND.", failures);

        database.CreateDatabase = true;
        var provision = await service.ProvisionDatabaseAsync(database);
        Expect(provision.Success, $"SQL provisioning/migration retry failed: {provision.Code}", failures);

        var weakAdministratorDraft = new SetupDraft
        {
            Culture = "en",
            CurrentStep = SetupStep.Review,
            Database = database,
            DatabaseConnectionVerified = true,
            DatabaseProvisioned = true,
            Administrator = new AdministratorSetupOptions
            {
                DisplayName = "Weak P02 Administrator",
                Username = "weakadmin",
                Email = "weakadmin@example.invalid",
                Password = "weak"
            },
            Branding = new BrandingSetupOptions(),
            Security = new SecuritySetupOptions(),
            Integration = new IntegrationSetupOptions { DefaultEnvironment = "UAT" },
            Notifications = new NotificationSetupOptions()
        };
        var weakAdministrator = await service.CompleteAsync(weakAdministratorDraft);
        Expect(!weakAdministrator.Success && weakAdministrator.Code == "ADMIN_PASSWORD_WEAK", $"Weak administrator negative path returned {weakAdministrator.Code}.", failures);
        Expect(!(await service.GetStatusAsync()).IsCompleted, "A failed Finish attempt incorrectly marked setup complete.", failures);

        var draft = new SetupDraft
        {
            Culture = "ar-KW",
            CurrentStep = SetupStep.Review,
            Database = database,
            DatabaseConnectionVerified = true,
            DatabaseProvisioned = true,
            Administrator = new AdministratorSetupOptions
            {
                DisplayName = "P02 CI Administrator",
                Username = "p02admin",
                Email = "p02admin@example.invalid",
                Password = bootstrapPassword
            },
            Branding = new BrandingSetupOptions(),
            Security = new SecuritySetupOptions(),
            Integration = new IntegrationSetupOptions { DefaultEnvironment = "UAT" },
            Notifications = new NotificationSetupOptions()
        };

        await service.SaveDraftAsync(draft);
        var draftFile = Directory.EnumerateFiles(Path.Combine(temporaryRoot, "App_Data", "setup"), "draft.protected").Single();
        var rawDraft = await File.ReadAllTextAsync(draftFile);
        Expect(!rawDraft.Contains(bootstrapPassword, StringComparison.Ordinal), "Protected draft leaked administrator plaintext password.", failures);
        Expect(!rawDraft.Contains(databaseName, StringComparison.Ordinal), "Protected draft leaked database configuration as plaintext.", failures);

        var completion = await service.CompleteAsync(draft);
        Expect(completion.Success, $"Finish failed: {completion.Code}", failures);

        var status = await service.GetStatusAsync();
        Expect(status.IsCompleted, "Completed setup state was not readable after Finish.", failures);
        Expect(status.Draft.Administrator.Password.Length == 0, "Completed setup status exposed administrator password.", failures);

        var restartedProtection = DataProtectionProvider.Create(new DirectoryInfo(keys), configuration => configuration.SetApplicationName("GSIP-P02-Checks"));
        var restartedService = new SetupService(restartedProtection, new PasswordHasher<BootstrapAdministrator>(), environment);
        var restartedStatus = await restartedService.GetStatusAsync();
        Expect(restartedStatus.IsCompleted, "Completed setup state was not restart-readable with persisted Data Protection keys.", failures);

        var repeatCompletion = await restartedService.CompleteAsync(new SetupDraft());
        Expect(!repeatCompletion.Success && repeatCompletion.Code == "SETUP_ALREADY_COMPLETED", $"Repeated Finish did not remain locked; result was {repeatCompletion.Code}.", failures);

        var targetConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = sqlServer,
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            Encrypt = false,
            TrustServerCertificate = true
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(targetConnectionString).Options;
        await using var db = new GsipDbContext(options);

        Expect(await db.SystemSetup.CountAsync() == 1, "SystemSetup bootstrap record count is invalid.", failures);
        Expect(await db.BootstrapAdministrators.CountAsync() == 1, "Bootstrap administrator was not persisted exactly once.", failures);
        var administrator = await db.BootstrapAdministrators.SingleAsync();
        Expect(!administrator.PasswordHash.Contains(bootstrapPassword, StringComparison.Ordinal), "Bootstrap administrator password was not one-way hashed.", failures);
        Expect(administrator.MustChangePassword, "Bootstrap administrator must require password change during Identity activation.", failures);

        var placeholders = await db.ServiceEnvironmentPlaceholders.AsNoTracking().ToListAsync();
        Expect(placeholders.Count == 10, $"Expected 10 MOJ Service+Environment placeholders, found {placeholders.Count}.", failures);
        foreach (var group in placeholders.GroupBy(item => item.ServiceCode, StringComparer.Ordinal))
        {
            var environments = group.Select(item => item.Environment).Order(StringComparer.Ordinal).ToArray();
            Expect(environments.SequenceEqual(new[] { "Production", "UAT" }, StringComparer.Ordinal), $"Service {group.Key} does not have isolated UAT+Production placeholders.", failures);
            Expect(group.All(item => !item.IsActive), $"Service {group.Key} placeholder must remain inactive until real service configuration exists.", failures);
        }

        var completionFile = Path.Combine(temporaryRoot, "App_Data", "setup", "completed.protected");
        var rawCompletion = await File.ReadAllTextAsync(completionFile);
        Expect(!rawCompletion.Contains(databaseName, StringComparison.Ordinal), "Protected completion marker leaked connection configuration as plaintext.", failures);
        Expect(!rawCompletion.Contains(bootstrapPassword, StringComparison.Ordinal), "Protected completion marker leaked a bootstrap password.", failures);

        var locked = false;
        try
        {
            await restartedService.SaveDraftAsync(new SetupDraft());
        }
        catch (InvalidOperationException)
        {
            locked = true;
        }
        Expect(locked, "Public setup state was not locked after Finish.", failures);
    }
    finally
    {
        SqlConnection.ClearAllPools();
        foreach (var cleanupDatabase in new[] { databaseName, migrationRetryDatabaseName })
        {
            try
            {
                var masterConnectionString = new SqlConnectionStringBuilder
                {
                    DataSource = sqlServer,
                    InitialCatalog = "master",
                    IntegratedSecurity = true,
                    Encrypt = false,
                    TrustServerCertificate = true
                }.ConnectionString;
                await using var master = new SqlConnection(masterConnectionString);
                await master.OpenAsync();
                await using var drop = master.CreateCommand();
                drop.CommandText = $"IF DB_ID('{cleanupDatabase}') IS NOT NULL BEGIN ALTER DATABASE [{cleanupDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{cleanupDatabase}]; END";
                await drop.ExecuteNonQueryAsync();
            }
            catch (SqlException)
            {
                failures.Add($"P02 test database cleanup failed for {cleanupDatabase}.");
            }
        }
        try { Directory.Delete(temporaryRoot, recursive: true); } catch (IOException) { }
    }
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

file sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
