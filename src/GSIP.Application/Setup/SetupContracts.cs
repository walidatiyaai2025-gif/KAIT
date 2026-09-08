namespace GSIP.Application.Setup;

public enum SetupStep
{
    Welcome = 0,
    Preflight = 1,
    Database = 2,
    Provision = 3,
    Administrator = 4,
    Branding = 5,
    Security = 6,
    Integration = 7,
    Notifications = 8,
    Review = 9,
    Finish = 10
}

public enum SetupHealthState
{
    Pass = 0,
    Warning = 1,
    Fail = 2
}

public sealed record SetupHealthCheck(string Code, SetupHealthState State, string Message);

public sealed record SetupHealthReport(IReadOnlyList<SetupHealthCheck> Checks)
{
    public bool HasCriticalFailures => Checks.Any(check => check.State == SetupHealthState.Fail);
    public bool HasWarnings => Checks.Any(check => check.State == SetupHealthState.Warning);
}

public sealed class DatabaseSetupOptions
{
    public string Server { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "GSIP";
    public bool UseWindowsAuthentication { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public bool CreateDatabase { get; set; } = true;
}

public sealed class AdministratorSetupOptions
{
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class BrandingSetupOptions
{
    public string OrganizationNameEn { get; set; } = "Government Services Integration Portal";
    public string OrganizationNameAr { get; set; } = "بوابة تكامل الخدمات الحكومية";
    public string PrimaryColor { get; set; } = "#0b4f7d";
    public string TimeZoneId { get; set; } = "Arab Standard Time";
}

public sealed class SecuritySetupOptions
{
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int LockoutMinutes { get; set; } = 15;
    public int MaxFailedAccessAttempts { get; set; } = 5;
    public bool RequireMfaForPrivilegedAccounts { get; set; } = true;
}

public sealed class IntegrationSetupOptions
{
    public string DefaultEnvironment { get; set; } = "UAT";
    public int TimeoutSeconds { get; set; } = 30;
    public bool ValidateServerCertificate { get; set; } = true;
    public string ProxyUrl { get; set; } = string.Empty;
}

public sealed class NotificationSetupOptions
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SenderAddress { get; set; } = string.Empty;
}

public sealed class SetupDraft
{
    public string Culture { get; set; } = "en";
    public SetupStep CurrentStep { get; set; } = SetupStep.Welcome;
    public DatabaseSetupOptions Database { get; set; } = new();
    public AdministratorSetupOptions Administrator { get; set; } = new();
    public BrandingSetupOptions Branding { get; set; } = new();
    public SecuritySetupOptions Security { get; set; } = new();
    public IntegrationSetupOptions Integration { get; set; } = new();
    public NotificationSetupOptions Notifications { get; set; } = new();
    public bool DatabaseConnectionVerified { get; set; }
    public bool DatabaseProvisioned { get; set; }
    public DateTimeOffset? LastSavedAtUtc { get; set; }
}

public sealed record SetupOperationResult(bool Success, string Code, string Message)
{
    public static SetupOperationResult Ok(string code, string message) => new(true, code, message);
    public static SetupOperationResult Fail(string code, string message) => new(false, code, message);
}

public sealed record SetupStatus(bool IsCompleted, SetupDraft Draft, DateTimeOffset? CompletedAtUtc);

public interface ISetupService
{
    Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task SaveDraftAsync(SetupDraft draft, CancellationToken cancellationToken = default);
    Task<SetupOperationResult> RunPreflightAsync(CancellationToken cancellationToken = default);
    Task<SetupOperationResult> TestDatabaseAsync(DatabaseSetupOptions options, CancellationToken cancellationToken = default);
    Task<SetupOperationResult> ProvisionDatabaseAsync(DatabaseSetupOptions options, CancellationToken cancellationToken = default);
    Task<SetupHealthReport> RunHealthCheckAsync(SetupDraft draft, CancellationToken cancellationToken = default);
    Task<SetupOperationResult> CompleteAsync(SetupDraft draft, CancellationToken cancellationToken = default);
}
