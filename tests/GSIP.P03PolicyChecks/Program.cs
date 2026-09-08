using GSIP.Application.Identity;
using GSIP.Application.Setup;

var checks = new List<(string Name, Action Execute)>
{
    ("secure defaults validate", () => Assert(new AccountSecurityPolicy().Validate().Count == 0, "secure defaults must be valid")),
    ("setup values map into runtime policy", VerifySetupMapping),
    ("weak password length is rejected", () => AssertViolation(new AccountSecurityPolicy { Password = new PasswordSecurityPolicy { RequiredLength = 8 } }, "PASSWORD_LENGTH")),
    ("weak password complexity is rejected", () => AssertViolation(new AccountSecurityPolicy { Password = new PasswordSecurityPolicy { RequireDigit = false, RequireLowercase = true, RequireUppercase = false, RequireNonAlphanumeric = false } }, "PASSWORD_COMPLEXITY")),
    ("unsafe lockout threshold is rejected", () => AssertViolation(new AccountSecurityPolicy { MaxFailedAccessAttempts = 2 }, "LOCKOUT_ATTEMPTS")),
    ("remember-me lifetime is bounded", () => AssertViolation(new AccountSecurityPolicy { RememberMeLifetime = TimeSpan.FromDays(91) }, "REMEMBER_ME_LIFETIME")),
    ("lockout threshold is deterministic", () => Assert(new AccountSecurityPolicy { MaxFailedAccessAttempts = 5 }.ShouldLockout(5), "fifth failure must lock out")),
    ("remember-me can be disabled server-side", VerifyRememberMePolicy),
    ("disabled account is denied before other gates", VerifyDisabledAccount),
    ("active lockout is denied", VerifyLockout),
    ("forced password change is enforced", VerifyForcedPasswordChange),
    ("privileged account requires MFA enrollment", VerifyMfaEnrollment),
    ("privileged account requires MFA verification", VerifyMfaVerification),
    ("privileged account passes after MFA verification", VerifyPrivilegedAllowed),
    ("non-privileged account does not inherit privileged MFA gate", VerifyStandardAccountAllowed)
};

var failures = new List<string>();
foreach (var check in checks)
{
    try
    {
        check.Execute();
        Console.WriteLine($"PASS {check.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {check.Name}: {exception.Message}");
        Console.Error.WriteLine(failures[^1]);
    }
}

if (failures.Count != 0)
{
    Console.Error.WriteLine($"P03 policy checks failed: {failures.Count}/{checks.Count}");
    return 1;
}

Console.WriteLine($"P03 policy checks passed: {checks.Count}/{checks.Count}");
return 0;

static void VerifySetupMapping()
{
    var setup = new SecuritySetupOptions
    {
        SessionTimeoutMinutes = 45,
        LockoutMinutes = 20,
        MaxFailedAccessAttempts = 4,
        RequireMfaForPrivilegedAccounts = true
    };
    var policy = AccountSecurityPolicy.FromSetup(setup, allowRememberMe: true, rememberMeLifetime: TimeSpan.FromDays(7));
    Assert(policy.SessionIdleTimeout == TimeSpan.FromMinutes(45), "session timeout mapping mismatch");
    Assert(policy.LockoutDuration == TimeSpan.FromMinutes(20), "lockout mapping mismatch");
    Assert(policy.MaxFailedAccessAttempts == 4, "failed-attempt mapping mismatch");
    Assert(policy.RequireMfaForPrivilegedAccounts, "privileged MFA mapping mismatch");
}

static void VerifyRememberMePolicy()
{
    var policy = new AccountSecurityPolicy { AllowRememberMe = false };
    var session = policy.ResolveSession(rememberMeRequested: true);
    Assert(!session.IsPersistent, "server policy must override remember-me request");
    Assert(session.Lifetime == policy.SessionIdleTimeout, "non-persistent session must use idle timeout");
}

static void VerifyDisabledAccount()
{
    var decision = new AccountSecurityPolicy().EvaluateAccess(Context(isEnabled: false, isPrivileged: true, mustChangePassword: true));
    Assert(decision.Code == AccountAccessDecisionCode.AccountDisabled, "disabled account must be rejected first");
}

static void VerifyLockout()
{
    var now = DateTimeOffset.Parse("2026-09-08T13:00:00Z");
    var decision = new AccountSecurityPolicy().EvaluateAccess(Context(lockoutEndUtc: now.AddMinutes(1), utcNow: now));
    Assert(decision.Code == AccountAccessDecisionCode.LockedOut, "active lockout must deny access");
}

static void VerifyForcedPasswordChange()
{
    var decision = new AccountSecurityPolicy().EvaluateAccess(Context(mustChangePassword: true));
    Assert(decision.Code == AccountAccessDecisionCode.PasswordChangeRequired, "forced password change must be enforced");
}

static void VerifyMfaEnrollment()
{
    var decision = new AccountSecurityPolicy().EvaluateAccess(Context(isPrivileged: true, isMfaEnrolled: false));
    Assert(decision.Code == AccountAccessDecisionCode.MfaEnrollmentRequired, "privileged account must enroll MFA");
}

static void VerifyMfaVerification()
{
    var decision = new AccountSecurityPolicy().EvaluateAccess(Context(isPrivileged: true, isMfaEnrolled: true, isMfaVerifiedForSession: false));
    Assert(decision.Code == AccountAccessDecisionCode.MfaVerificationRequired, "privileged account must verify MFA");
}

static void VerifyPrivilegedAllowed()
{
    var decision = new AccountSecurityPolicy().EvaluateAccess(Context(isPrivileged: true, isMfaEnrolled: true, isMfaVerifiedForSession: true));
    Assert(decision.IsAllowed, "privileged account with verified MFA must pass policy gate");
}

static void VerifyStandardAccountAllowed()
{
    var decision = new AccountSecurityPolicy().EvaluateAccess(Context(isPrivileged: false));
    Assert(decision.IsAllowed, "standard enabled account must not require privileged MFA");
}

static AccountSecurityContext Context(
    bool isEnabled = true,
    bool isPrivileged = false,
    bool mustChangePassword = false,
    bool isMfaEnrolled = false,
    bool isMfaVerifiedForSession = false,
    DateTimeOffset? lockoutEndUtc = null,
    DateTimeOffset? utcNow = null)
    => new(isEnabled, isPrivileged, mustChangePassword, isMfaEnrolled, isMfaVerifiedForSession, lockoutEndUtc, utcNow ?? DateTimeOffset.Parse("2026-09-08T13:00:00Z"));

static void AssertViolation(AccountSecurityPolicy policy, string code)
{
    Assert(policy.Validate().Any(violation => string.Equals(violation.Code, code, StringComparison.Ordinal)), $"expected violation {code}");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
