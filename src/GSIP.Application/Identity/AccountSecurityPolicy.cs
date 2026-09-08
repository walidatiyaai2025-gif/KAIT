using GSIP.Application.Setup;

namespace GSIP.Application.Identity;

public sealed record PasswordSecurityPolicy
{
    public int RequiredLength { get; init; } = 12;
    public int RequiredUniqueCharacters { get; init; } = 4;
    public bool RequireDigit { get; init; } = true;
    public bool RequireLowercase { get; init; } = true;
    public bool RequireUppercase { get; init; } = true;
    public bool RequireNonAlphanumeric { get; init; } = true;
}

public sealed record AccountSecurityPolicy
{
    public PasswordSecurityPolicy Password { get; init; } = new();
    public int MaxFailedAccessAttempts { get; init; } = 5;
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan SessionIdleTimeout { get; init; } = TimeSpan.FromMinutes(30);
    public bool AllowRememberMe { get; init; } = true;
    public TimeSpan RememberMeLifetime { get; init; } = TimeSpan.FromDays(14);
    public bool RequireMfaForPrivilegedAccounts { get; init; } = true;

    public static AccountSecurityPolicy FromSetup(
        SecuritySetupOptions setup,
        PasswordSecurityPolicy? password = null,
        bool allowRememberMe = true,
        TimeSpan? rememberMeLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(setup);

        return new AccountSecurityPolicy
        {
            Password = password ?? new PasswordSecurityPolicy(),
            MaxFailedAccessAttempts = setup.MaxFailedAccessAttempts,
            LockoutDuration = TimeSpan.FromMinutes(setup.LockoutMinutes),
            SessionIdleTimeout = TimeSpan.FromMinutes(setup.SessionTimeoutMinutes),
            AllowRememberMe = allowRememberMe,
            RememberMeLifetime = rememberMeLifetime ?? TimeSpan.FromDays(14),
            RequireMfaForPrivilegedAccounts = setup.RequireMfaForPrivilegedAccounts
        }.EnsureValid();
    }

    public AccountSecurityPolicy EnsureValid()
    {
        var violations = Validate();
        if (violations.Count != 0)
        {
            throw new AccountSecurityPolicyException(violations);
        }

        return this;
    }

    public IReadOnlyList<AccountSecurityPolicyViolation> Validate()
    {
        var violations = new List<AccountSecurityPolicyViolation>();

        if (Password.RequiredLength is < 12 or > 128)
        {
            violations.Add(new("PASSWORD_LENGTH", "Password length must be between 12 and 128 characters."));
        }

        if (Password.RequiredUniqueCharacters < 1 || Password.RequiredUniqueCharacters > Password.RequiredLength)
        {
            violations.Add(new("PASSWORD_UNIQUE_CHARACTERS", "Required unique characters must be between 1 and the configured password length."));
        }

        var requiredCharacterClasses = new[]
        {
            Password.RequireDigit,
            Password.RequireLowercase,
            Password.RequireUppercase,
            Password.RequireNonAlphanumeric
        }.Count(required => required);
        if (requiredCharacterClasses < 2)
        {
            violations.Add(new("PASSWORD_COMPLEXITY", "At least two password character classes must be required."));
        }

        if (MaxFailedAccessAttempts is < 3 or > 20)
        {
            violations.Add(new("LOCKOUT_ATTEMPTS", "Maximum failed access attempts must be between 3 and 20."));
        }

        if (LockoutDuration < TimeSpan.FromMinutes(1) || LockoutDuration > TimeSpan.FromDays(1))
        {
            violations.Add(new("LOCKOUT_DURATION", "Lockout duration must be between 1 minute and 24 hours."));
        }

        if (SessionIdleTimeout < TimeSpan.FromMinutes(5) || SessionIdleTimeout > TimeSpan.FromDays(1))
        {
            violations.Add(new("SESSION_TIMEOUT", "Session idle timeout must be between 5 minutes and 24 hours."));
        }

        if (AllowRememberMe && (RememberMeLifetime < SessionIdleTimeout || RememberMeLifetime > TimeSpan.FromDays(90)))
        {
            violations.Add(new("REMEMBER_ME_LIFETIME", "Remember-me lifetime must be at least the session timeout and no more than 90 days."));
        }

        return violations;
    }

    public bool ShouldLockout(int failedAccessCount)
        => failedAccessCount >= MaxFailedAccessAttempts;

    public AuthenticationSessionPolicy ResolveSession(bool rememberMeRequested)
    {
        EnsureValid();
        var persistent = rememberMeRequested && AllowRememberMe;
        return new AuthenticationSessionPolicy(persistent, persistent ? RememberMeLifetime : SessionIdleTimeout);
    }

    public AccountAccessDecision EvaluateAccess(AccountSecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureValid();

        if (!context.IsEnabled)
        {
            return AccountAccessDecision.Deny(AccountAccessDecisionCode.AccountDisabled);
        }

        if (context.LockoutEndUtc is { } lockoutEnd && lockoutEnd > context.UtcNow)
        {
            return AccountAccessDecision.Deny(AccountAccessDecisionCode.LockedOut);
        }

        if (context.MustChangePassword)
        {
            return AccountAccessDecision.Deny(AccountAccessDecisionCode.PasswordChangeRequired);
        }

        if (context.IsPrivileged && RequireMfaForPrivilegedAccounts)
        {
            if (!context.IsMfaEnrolled)
            {
                return AccountAccessDecision.Deny(AccountAccessDecisionCode.MfaEnrollmentRequired);
            }

            if (!context.IsMfaVerifiedForSession)
            {
                return AccountAccessDecision.Deny(AccountAccessDecisionCode.MfaVerificationRequired);
            }
        }

        return AccountAccessDecision.Allow();
    }
}

public sealed record AccountSecurityPolicyViolation(string Code, string Message);

public sealed class AccountSecurityPolicyException(IReadOnlyList<AccountSecurityPolicyViolation> violations)
    : InvalidOperationException($"Account security policy is invalid: {string.Join(", ", violations.Select(violation => violation.Code))}")
{
    public IReadOnlyList<AccountSecurityPolicyViolation> Violations { get; } = violations;
}

public sealed record AuthenticationSessionPolicy(bool IsPersistent, TimeSpan Lifetime);

public sealed record AccountSecurityContext(
    bool IsEnabled,
    bool IsPrivileged,
    bool MustChangePassword,
    bool IsMfaEnrolled,
    bool IsMfaVerifiedForSession,
    DateTimeOffset? LockoutEndUtc,
    DateTimeOffset UtcNow);

public enum AccountAccessDecisionCode
{
    Allowed = 0,
    AccountDisabled = 1,
    LockedOut = 2,
    PasswordChangeRequired = 3,
    MfaEnrollmentRequired = 4,
    MfaVerificationRequired = 5
}

public sealed record AccountAccessDecision(bool IsAllowed, AccountAccessDecisionCode Code)
{
    public static AccountAccessDecision Allow() => new(true, AccountAccessDecisionCode.Allowed);
    public static AccountAccessDecision Deny(AccountAccessDecisionCode code) => new(false, code);
}
