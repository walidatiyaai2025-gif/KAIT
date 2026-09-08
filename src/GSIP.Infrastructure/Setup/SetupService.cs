using System.Text.Json;
using System.Text.RegularExpressions;
using GSIP.Application.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure.Setup;

public sealed partial class SetupService : ISetupService
{
    private const string ProtectorPurpose = "GSIP.Setup.State.v1";
    private static readonly string[] MojServices =
    [
        "MarriageCases",
        "IsSingleBasic",
        "MarriageCoupleLastCase",
        "FamilyJudgmentText",
        "ProcurationStatus"
    ];

    private readonly IDataProtector _protector;
    private readonly IPasswordHasher<BootstrapAdministrator> _passwordHasher;
    private readonly string _stateDirectory;
    private readonly string _draftPath;
    private readonly string _completionPath;

    public SetupService(
        IDataProtectionProvider dataProtectionProvider,
        IPasswordHasher<BootstrapAdministrator> passwordHasher,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(environment);

        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _passwordHasher = passwordHasher;
        _stateDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "setup");
        _draftPath = Path.Combine(_stateDirectory, "draft.protected");
        _completionPath = Path.Combine(_stateDirectory, "completed.protected");
    }

    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_stateDirectory);

        var completion = await ReadProtectedAsync<ProtectedCompletion>(_completionPath, cancellationToken);
        if (completion is not null)
        {
            return new SetupStatus(true, new SetupDraft { CurrentStep = SetupStep.Finish }, completion.CompletedAtUtc);
        }

        var draft = await ReadProtectedAsync<SetupDraft>(_draftPath, cancellationToken) ?? new SetupDraft();
        return new SetupStatus(false, draft, null);
    }

    public async Task SaveDraftAsync(SetupDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if ((await GetStatusAsync(cancellationToken)).IsCompleted)
        {
            throw new InvalidOperationException("Setup has already been completed and cannot be reopened from the public wizard.");
        }

        draft.LastSavedAtUtc = DateTimeOffset.UtcNow;
        await WriteProtectedAsync(_draftPath, draft, cancellationToken);
    }

    public async Task<SetupOperationResult> RunPreflightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_stateDirectory);
            var probePath = Path.Combine(_stateDirectory, $"preflight-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probePath, "gsip-preflight", cancellationToken);
            File.Delete(probePath);

            var protectedValue = _protector.Protect("gsip-data-protection-probe");
            if (!string.Equals(_protector.Unprotect(protectedValue), "gsip-data-protection-probe", StringComparison.Ordinal))
            {
                return SetupOperationResult.Fail("DATA_PROTECTION", "Data Protection round-trip validation failed.");
            }

            return SetupOperationResult.Ok("PREFLIGHT_OK", "Application storage and Data Protection are available.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            return SetupOperationResult.Fail("PREFLIGHT_FAILED", "The application cannot securely persist setup state. Verify filesystem permissions and Data Protection configuration.");
        }
    }

    public async Task<SetupOperationResult> TestDatabaseAsync(DatabaseSetupOptions options, CancellationToken cancellationToken = default)
    {
        var validation = ValidateDatabaseOptions(options);
        if (!validation.Success)
        {
            return validation;
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(options, "master"));
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";
            command.CommandTimeout = Math.Clamp(options.TimeoutSeconds, 3, 120);
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return SetupOperationResult.Ok("SQL_CONNECTION_OK", "SQL Server connection succeeded.");
        }
        catch (SqlException)
        {
            return SetupOperationResult.Fail("SQL_CONNECTION_FAILED", "SQL Server connection failed. Verify server, authentication, TLS and network settings.");
        }
        catch (InvalidOperationException)
        {
            return SetupOperationResult.Fail("SQL_CONFIGURATION_INVALID", "SQL Server connection configuration is invalid.");
        }
    }

    public async Task<SetupOperationResult> ProvisionDatabaseAsync(DatabaseSetupOptions options, CancellationToken cancellationToken = default)
    {
        var validation = ValidateDatabaseOptions(options);
        if (!validation.Success)
        {
            return validation;
        }

        try
        {
            await using (var master = new SqlConnection(BuildConnectionString(options, "master")))
            {
                await master.OpenAsync(cancellationToken);
                await using var existsCommand = master.CreateCommand();
                existsCommand.CommandText = "SELECT DB_ID(@databaseName)";
                existsCommand.Parameters.AddWithValue("@databaseName", options.DatabaseName);
                existsCommand.CommandTimeout = Math.Clamp(options.TimeoutSeconds, 3, 120);
                var databaseId = await existsCommand.ExecuteScalarAsync(cancellationToken);
                var databaseExists = databaseId is not null && databaseId is not DBNull;

                if (!databaseExists && !options.CreateDatabase)
                {
                    return SetupOperationResult.Fail("DATABASE_NOT_FOUND", "The selected existing database does not exist.");
                }

                if (!databaseExists)
                {
                    await using var createCommand = master.CreateCommand();
                    createCommand.CommandText = $"CREATE DATABASE [{options.DatabaseName}]";
                    createCommand.CommandTimeout = Math.Clamp(options.TimeoutSeconds, 3, 120);
                    await createCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await using var dbContext = CreateDbContext(BuildConnectionString(options, options.DatabaseName));
            await dbContext.Database.MigrateAsync(cancellationToken);
            return SetupOperationResult.Ok("DATABASE_READY", "Database exists and GSIP migrations completed successfully.");
        }
        catch (SqlException)
        {
            return SetupOperationResult.Fail("DATABASE_PROVISION_FAILED", "Database provisioning or migration failed. Verify SQL permissions and retry.");
        }
        catch (InvalidOperationException)
        {
            return SetupOperationResult.Fail("DATABASE_MIGRATION_FAILED", "Database migration could not be completed safely.");
        }
    }

    public async Task<SetupHealthReport> RunHealthCheckAsync(SetupDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var checks = new List<SetupHealthCheck>();

        var databaseValidation = ValidateDatabaseOptions(draft.Database);
        if (!databaseValidation.Success)
        {
            checks.Add(new SetupHealthCheck("DATABASE_OPTIONS", SetupHealthState.Fail, databaseValidation.Message));
        }
        else
        {
            var connection = await TestDatabaseAsync(draft.Database, cancellationToken);
            checks.Add(new SetupHealthCheck(
                "DATABASE_CONNECTION",
                connection.Success && draft.DatabaseConnectionVerified ? SetupHealthState.Pass : SetupHealthState.Fail,
                connection.Success && draft.DatabaseConnectionVerified
                    ? "SQL Server connection is verified."
                    : "SQL Server connection must be successfully tested again before Finish."));

            if (!draft.DatabaseProvisioned)
            {
                checks.Add(new SetupHealthCheck("DATABASE_MIGRATIONS", SetupHealthState.Fail, "Database provisioning and migrations are not marked complete."));
            }
            else if (connection.Success)
            {
                try
                {
                    await using var dbContext = CreateDbContext(BuildConnectionString(draft.Database, draft.Database.DatabaseName));
                    var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
                    checks.Add(pending.Length == 0
                        ? new SetupHealthCheck("DATABASE_MIGRATIONS", SetupHealthState.Pass, "All GSIP database migrations are applied.")
                        : new SetupHealthCheck("DATABASE_MIGRATIONS", SetupHealthState.Fail, "Pending GSIP database migrations remain and must be applied before Finish."));
                }
                catch (Exception exception) when (exception is SqlException or InvalidOperationException)
                {
                    checks.Add(new SetupHealthCheck("DATABASE_MIGRATIONS", SetupHealthState.Fail, "GSIP could not verify the target database migration state."));
                }
            }
        }

        var administratorValidation = ValidateAdministrator(draft.Administrator);
        checks.Add(new SetupHealthCheck(
            "ADMINISTRATOR",
            administratorValidation.Success ? SetupHealthState.Pass : SetupHealthState.Fail,
            administratorValidation.Message));

        var brandingReady = !string.IsNullOrWhiteSpace(draft.Branding.OrganizationNameEn)
            && !string.IsNullOrWhiteSpace(draft.Branding.OrganizationNameAr)
            && !string.IsNullOrWhiteSpace(draft.Branding.PrimaryColor);
        checks.Add(new SetupHealthCheck(
            "BRANDING",
            brandingReady ? SetupHealthState.Pass : SetupHealthState.Fail,
            brandingReady ? "Organization bilingual identity is configured." : "Organization bilingual names and primary color are required."));

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(draft.Branding.TimeZoneId.Trim());
            checks.Add(new SetupHealthCheck("TIME_ZONE", SetupHealthState.Pass, "Configured time zone is available on this host."));
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            checks.Add(new SetupHealthCheck("TIME_ZONE", SetupHealthState.Fail, "Configured time zone is not available on this host."));
        }

        var securityRangeValid = draft.Security.SessionTimeoutMinutes is >= 5 and <= 1440
            && draft.Security.LockoutMinutes is >= 1 and <= 1440
            && draft.Security.MaxFailedAccessAttempts is >= 3 and <= 20;
        checks.Add(new SetupHealthCheck(
            "SECURITY_BASELINE",
            securityRangeValid ? SetupHealthState.Pass : SetupHealthState.Fail,
            securityRangeValid ? "Session and lockout controls are within the supported secure ranges." : "Security baseline values are outside the supported ranges."));
        if (!draft.Security.RequireMfaForPrivilegedAccounts)
        {
            checks.Add(new SetupHealthCheck("PRIVILEGED_MFA", SetupHealthState.Warning, "Privileged-account MFA is disabled; enable it unless an approved security exception exists."));
        }
        else
        {
            checks.Add(new SetupHealthCheck("PRIVILEGED_MFA", SetupHealthState.Pass, "Privileged-account MFA policy is enabled."));
        }

        var environmentValid = string.Equals(draft.Integration.DefaultEnvironment, "UAT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draft.Integration.DefaultEnvironment, "Production", StringComparison.OrdinalIgnoreCase);
        var integrationTimeoutValid = draft.Integration.TimeoutSeconds is >= 3 and <= 300;
        checks.Add(new SetupHealthCheck(
            "INTEGRATION_BASELINE",
            environmentValid && integrationTimeoutValid ? SetupHealthState.Pass : SetupHealthState.Fail,
            environmentValid && integrationTimeoutValid ? "Integration environment and timeout baseline are valid." : "Integration environment or timeout baseline is invalid."));

        if (!string.IsNullOrWhiteSpace(draft.Integration.ProxyUrl))
        {
            var validProxy = Uri.TryCreate(draft.Integration.ProxyUrl, UriKind.Absolute, out var proxyUri)
                && (string.Equals(proxyUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(proxyUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
            checks.Add(new SetupHealthCheck(
                "INTEGRATION_PROXY",
                validProxy ? SetupHealthState.Pass : SetupHealthState.Fail,
                validProxy ? "Configured proxy URL is syntactically valid." : "Configured proxy URL must be an absolute HTTP or HTTPS URL."));
        }

        checks.Add(new SetupHealthCheck(
            "TLS_VALIDATION",
            draft.Integration.ValidateServerCertificate ? SetupHealthState.Pass : SetupHealthState.Warning,
            draft.Integration.ValidateServerCertificate
                ? "Server certificate validation is enabled for integrations."
                : "Server certificate validation is disabled; this requires an explicitly approved exception."));

        if (draft.Notifications.Enabled)
        {
            var notificationConfigValid = !string.IsNullOrWhiteSpace(draft.Notifications.SmtpHost)
                && draft.Notifications.SmtpPort is >= 1 and <= 65535
                && draft.Notifications.SenderAddress.Contains('@', StringComparison.Ordinal);
            checks.Add(new SetupHealthCheck(
                "NOTIFICATIONS",
                notificationConfigValid ? SetupHealthState.Warning : SetupHealthState.Fail,
                notificationConfigValid
                    ? "SMTP configuration is present; perform a delivery test before relying on notifications in production."
                    : "Enabled notifications require SMTP host, valid port and sender address."));
        }
        else
        {
            checks.Add(new SetupHealthCheck("NOTIFICATIONS", SetupHealthState.Pass, "Notifications are disabled and remain optional for first-run completion."));
        }

        return new SetupHealthReport(checks);
    }

    public async Task<SetupOperationResult> CompleteAsync(SetupDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if ((await GetStatusAsync(cancellationToken)).IsCompleted)
        {
            return SetupOperationResult.Fail("SETUP_ALREADY_COMPLETED", "Setup has already been completed.");
        }

        var databaseValidation = ValidateDatabaseOptions(draft.Database);
        if (!databaseValidation.Success)
        {
            return databaseValidation;
        }

        var administratorValidation = ValidateAdministrator(draft.Administrator);
        if (!administratorValidation.Success)
        {
            return administratorValidation;
        }

        if (!draft.DatabaseProvisioned)
        {
            return SetupOperationResult.Fail("DATABASE_NOT_PROVISIONED", "Database provisioning must complete before Finish.");
        }

        var health = await RunHealthCheckAsync(draft, cancellationToken);
        if (health.HasCriticalFailures)
        {
            var failedCodes = string.Join(", ", health.Checks
                .Where(check => check.State == SetupHealthState.Fail)
                .Select(check => check.Code));
            return SetupOperationResult.Fail("HEALTH_CHECK_FAILED", $"Review health check has critical failures: {failedCodes}. Resolve them before Finish.");
        }

        var connectionString = BuildConnectionString(draft.Database, draft.Database.DatabaseName);
        try
        {
            await using var dbContext = CreateDbContext(connectionString);
            await dbContext.Database.MigrateAsync(cancellationToken);

            var existingSetup = await dbContext.SystemSetup.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
            var completedAt = existingSetup?.CompletedAtUtc ?? DateTimeOffset.UtcNow;

            if (existingSetup is null)
            {
                dbContext.SystemSetup.Add(new SystemSetupRecord
                {
                    Id = 1,
                    CompletedAtUtc = completedAt,
                    OrganizationNameEn = draft.Branding.OrganizationNameEn.Trim(),
                    OrganizationNameAr = draft.Branding.OrganizationNameAr.Trim(),
                    PrimaryColor = draft.Branding.PrimaryColor.Trim(),
                    TimeZoneId = draft.Branding.TimeZoneId.Trim(),
                    SessionTimeoutMinutes = Math.Clamp(draft.Security.SessionTimeoutMinutes, 5, 1440),
                    LockoutMinutes = Math.Clamp(draft.Security.LockoutMinutes, 1, 1440),
                    MaxFailedAccessAttempts = Math.Clamp(draft.Security.MaxFailedAccessAttempts, 3, 20),
                    RequireMfaForPrivilegedAccounts = draft.Security.RequireMfaForPrivilegedAccounts,
                    DefaultEnvironment = NormalizeEnvironment(draft.Integration.DefaultEnvironment),
                    IntegrationTimeoutSeconds = Math.Clamp(draft.Integration.TimeoutSeconds, 3, 300),
                    ValidateServerCertificate = draft.Integration.ValidateServerCertificate
                });

                var administrator = new BootstrapAdministrator
                {
                    Id = Guid.NewGuid(),
                    DisplayName = draft.Administrator.DisplayName.Trim(),
                    Username = draft.Administrator.Username.Trim(),
                    NormalizedUsername = draft.Administrator.Username.Trim().ToUpperInvariant(),
                    Email = draft.Administrator.Email.Trim(),
                    MustChangePassword = true,
                    CreatedAtUtc = completedAt
                };
                administrator.PasswordHash = _passwordHasher.HashPassword(administrator, draft.Administrator.Password);
                dbContext.BootstrapAdministrators.Add(administrator);

                foreach (var serviceCode in MojServices)
                {
                    foreach (var environment in new[] { "UAT", "Production" })
                    {
                        dbContext.ServiceEnvironmentPlaceholders.Add(new ServiceEnvironmentPlaceholder
                        {
                            Id = Guid.NewGuid(),
                            EntityCode = "MOJ",
                            ServiceCode = serviceCode,
                            Environment = environment,
                            IsActive = false
                        });
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var completion = new ProtectedCompletion(completedAt, connectionString, draft.Culture);
            await WriteProtectedAsync(_completionPath, completion, cancellationToken);

            draft.Administrator.Password = string.Empty;
            draft.Database.Password = string.Empty;
            draft.CurrentStep = SetupStep.Finish;
            await WriteProtectedAsync(_draftPath, draft, cancellationToken);

            return SetupOperationResult.Ok("SETUP_COMPLETED", "First-run setup completed successfully.");
        }
        catch (DbUpdateException)
        {
            return SetupOperationResult.Fail("SETUP_DATABASE_WRITE_FAILED", "Setup could not persist the bootstrap records safely.");
        }
        catch (SqlException)
        {
            return SetupOperationResult.Fail("SETUP_DATABASE_UNAVAILABLE", "SQL Server became unavailable while finishing setup.");
        }
    }

    private GsipDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GsipDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(GsipDbContext).Assembly.FullName))
            .Options;
        return new GsipDbContext(options);
    }

    private static SetupOperationResult ValidateDatabaseOptions(DatabaseSetupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Server))
        {
            return SetupOperationResult.Fail("SERVER_REQUIRED", "SQL Server/Instance is required.");
        }
        if (string.IsNullOrWhiteSpace(options.DatabaseName) || !DatabaseNamePattern().IsMatch(options.DatabaseName))
        {
            return SetupOperationResult.Fail("DATABASE_NAME_INVALID", "Database name may contain letters, numbers and underscores only.");
        }
        if (!options.UseWindowsAuthentication && (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password)))
        {
            return SetupOperationResult.Fail("SQL_CREDENTIALS_REQUIRED", "SQL authentication requires username and password.");
        }
        if (options.TimeoutSeconds is < 3 or > 120)
        {
            return SetupOperationResult.Fail("SQL_TIMEOUT_INVALID", "SQL connection timeout must be between 3 and 120 seconds.");
        }
        return SetupOperationResult.Ok("DATABASE_OPTIONS_VALID", "Database options are valid.");
    }

    private static SetupOperationResult ValidateAdministrator(AdministratorSetupOptions administrator)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        if (string.IsNullOrWhiteSpace(administrator.DisplayName) || string.IsNullOrWhiteSpace(administrator.Username) || string.IsNullOrWhiteSpace(administrator.Email))
        {
            return SetupOperationResult.Fail("ADMIN_REQUIRED", "Administrator display name, username and email are required.");
        }
        if (!administrator.Email.Contains('@', StringComparison.Ordinal) || administrator.Username.Length < 3)
        {
            return SetupOperationResult.Fail("ADMIN_IDENTITY_INVALID", "Administrator username or email is invalid.");
        }
        var password = administrator.Password;
        if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return SetupOperationResult.Fail("ADMIN_PASSWORD_WEAK", "Administrator password must be at least 12 characters and include upper, lower, number and symbol characters.");
        }
        return SetupOperationResult.Ok("ADMIN_VALID", "Administrator bootstrap data is valid.");
    }

    private static string NormalizeEnvironment(string value) =>
        string.Equals(value, "Production", StringComparison.OrdinalIgnoreCase) ? "Production" : "UAT";

    private static string BuildConnectionString(DatabaseSetupOptions options, string initialCatalog)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.Server.Trim(),
            InitialCatalog = initialCatalog,
            Encrypt = options.Encrypt,
            TrustServerCertificate = options.TrustServerCertificate,
            ConnectTimeout = Math.Clamp(options.TimeoutSeconds, 3, 120),
            ApplicationName = "GSIP Setup Wizard",
            MultipleActiveResultSets = false
        };

        if (options.UseWindowsAuthentication)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = options.Username.Trim();
            builder.Password = options.Password;
        }

        return builder.ConnectionString;
    }

    private async Task<T?> ReadProtectedAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return default;
        }
        var protectedText = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return default;
        }
        try
        {
            var json = _protector.Unprotect(protectedText);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidOperationException("Protected setup state cannot be decrypted with the current Data Protection keys.");
        }
    }

    private async Task WriteProtectedAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_stateDirectory);
        var json = JsonSerializer.Serialize(value);
        var protectedText = _protector.Protect(json);
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, protectedText, cancellationToken);
        File.Move(temporaryPath, path, true);
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DatabaseNamePattern();

    private sealed record ProtectedCompletion(DateTimeOffset CompletedAtUtc, string ConnectionString, string Culture);
}
