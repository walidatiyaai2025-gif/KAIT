using System.Security.Claims;

namespace GSIP.Application.Identity;

public static class GsipIdentityClaims
{
    public const string SessionRestriction = "gsip:session_restriction";
    public const string RememberMeRequested = "gsip:remember_me_requested";
}

public enum AccountSignInStatus
{
    Succeeded = 0,
    InvalidCredentials = 1,
    LockedOut = 2,
    Disabled = 3,
    MfaEnrollmentRequired = 4,
    MfaVerificationRequired = 5,
    PasswordChangeRequired = 6
}

public enum SessionRestriction
{
    None = 0,
    MfaEnrollmentRequired = 1,
    MfaVerificationRequired = 2,
    PasswordChangeRequired = 3,
    AccountDisabled = 4,
    InvalidSession = 5
}

public sealed record AccountSignInResult(AccountSignInStatus Status)
{
    public bool Succeeded => Status == AccountSignInStatus.Succeeded;
}

public sealed record AccountOperationResult(bool Succeeded, string Code)
{
    public static AccountOperationResult Success(string code) => new(true, code);
    public static AccountOperationResult Failure(string code) => new(false, code);
}

public sealed record MfaEnrollmentDetails(string SharedKey, string AuthenticatorUri);

public sealed record AccountSecurityPolicy(
    int SessionTimeoutMinutes,
    int LockoutMinutes,
    int MaxFailedAccessAttempts,
    bool RequireMfaForPrivilegedAccounts,
    bool AllowRememberMe,
    int RememberMeDays,
    int MfaChallengeMinutes);

public sealed class IdentitySecurityOptions
{
    public const string SectionName = "IdentitySecurity";

    public int PasswordRequiredLength { get; init; } = 12;
    public bool PasswordRequireUppercase { get; init; } = true;
    public bool PasswordRequireLowercase { get; init; } = true;
    public bool PasswordRequireDigit { get; init; } = true;
    public bool PasswordRequireNonAlphanumeric { get; init; } = true;
    public bool AllowRememberMe { get; init; } = true;
    public int RememberMeDays { get; init; } = 14;
    public int MfaChallengeMinutes { get; init; } = 5;
    public int LoginRateLimitPermitCount { get; init; } = 10;
    public int LoginRateLimitWindowSeconds { get; init; } = 60;
}

public interface IAccountSecurityPolicyProvider
{
    Task<AccountSecurityPolicy> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAccountAuthenticationService
{
    Task EnsureBootstrapAdministratorAsync(CancellationToken cancellationToken = default);
    Task<AccountSignInResult> PasswordSignInAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken = default);
    Task<MfaEnrollmentDetails?> GetMfaEnrollmentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<AccountOperationResult> ConfirmMfaEnrollmentAsync(ClaimsPrincipal principal, string code, CancellationToken cancellationToken = default);
    Task<AccountSignInResult> VerifyMfaAsync(ClaimsPrincipal principal, string code, CancellationToken cancellationToken = default);
    Task<AccountOperationResult> ChangePasswordAsync(ClaimsPrincipal principal, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<SessionRestriction> GetSessionRestrictionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task SignOutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<AccountOperationResult> SetAccountEnabledAsync(Guid userId, bool enabled, CancellationToken cancellationToken = default);
}

