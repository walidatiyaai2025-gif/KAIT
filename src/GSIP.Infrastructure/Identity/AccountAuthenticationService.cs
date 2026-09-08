using System.Security.Claims;
using GSIP.Application.Abstractions;
using GSIP.Application.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Identity;

internal sealed class AccountAuthenticationService : IAccountAuthenticationService
{
    private const string MfaEnrollmentRestriction = "mfa-enrollment";
    private const string MfaVerificationRestriction = "mfa-verification";
    private const string PasswordChangeRestriction = "password-change";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly GsipDbContext _dbContext;
    private readonly IAccountSecurityPolicyProvider _policyProvider;
    private readonly IAuthenticationAuditWriter _audit;
    private readonly GSIP.Application.Abstractions.ISystemClock _clock;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _dummyPasswordHash;

    public AccountAuthenticationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        GsipDbContext dbContext,
        IAccountSecurityPolicyProvider policyProvider,
        IAuthenticationAuditWriter audit,
        GSIP.Application.Abstractions.ISystemClock clock,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _policyProvider = policyProvider;
        _audit = audit;
        _clock = clock;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
        _dummyPasswordHash = passwordHasher.HashPassword(new ApplicationUser(), "Synthetic timing equalization value only");
    }

    public async Task EnsureBootstrapAdministratorAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = await _dbContext.BootstrapAdministrators.SingleOrDefaultAsync(cancellationToken);
        if (bootstrap is null)
        {
            if (await _dbContext.Users.AnyAsync(cancellationToken))
            {
                return;
            }

            throw new InvalidOperationException("Setup completed without a bootstrap administrator record.");
        }

        var user = await _userManager.FindByIdAsync(bootstrap.Id.ToString());
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = bootstrap.Id,
                DisplayName = bootstrap.DisplayName,
                UserName = bootstrap.Username,
                Email = bootstrap.Email,
                EmailConfirmed = true,
                PasswordHash = bootstrap.PasswordHash,
                IsEnabled = true,
                IsPrivileged = true,
                MustChangePassword = bootstrap.MustChangePassword,
                CreatedAtUtc = bootstrap.CreatedAtUtc,
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };

            EnsureSucceeded(await _userManager.CreateAsync(user), "The bootstrap administrator could not be activated.");
        }

        if (bootstrap.ActivatedAtUtc is null || bootstrap.PasswordHash != "[ACTIVATED]")
        {
            bootstrap.ActivatedAtUtc = _clock.UtcNow;
            bootstrap.PasswordHash = "[ACTIVATED]";
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AccountSignInResult> PasswordSignInAsync(
        string username,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapAdministratorAsync(cancellationToken);
        var normalizedUsername = username?.Trim() ?? string.Empty;
        var user = normalizedUsername.Length == 0
            ? null
            : await _userManager.FindByNameAsync(normalizedUsername);

        if (user is null)
        {
            _ = _passwordHasher.VerifyHashedPassword(new ApplicationUser(), _dummyPasswordHash, password ?? string.Empty);
            await _audit.WriteAsync(null, "LoginFailure", false, "INVALID_CREDENTIALS", cancellationToken);
            return new AccountSignInResult(AccountSignInStatus.InvalidCredentials);
        }

        if (!user.IsEnabled)
        {
            await _audit.WriteAsync(user.Id, "LoginFailure", false, "ACCOUNT_DISABLED", cancellationToken);
            return new AccountSignInResult(AccountSignInStatus.Disabled);
        }

        var now = _clock.UtcNow;
        if (user.LockoutEnabled && user.LockoutEnd is not null && user.LockoutEnd > now)
        {
            await _audit.WriteAsync(user.Id, "AccountLockout", false, "LOCKED_OUT", cancellationToken);
            return new AccountSignInResult(AccountSignInStatus.LockedOut);
        }

        if (!await _userManager.CheckPasswordAsync(user, password ?? string.Empty))
        {
            return await RecordPasswordFailureAsync(user, cancellationToken);
        }

        if (user.AccessFailedCount != 0 || user.LockoutEnd is not null)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            EnsureSucceeded(await _userManager.UpdateAsync(user), "The successful sign-in state could not be persisted.");
        }

        return await ContinueAfterPasswordAsync(user, rememberMe, cancellationToken);
    }

    public async Task<MfaEnrollmentDetails?> GetMfaEnrollmentAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRestrictedUserAsync(principal, MfaEnrollmentRestriction);
        if (user is null || !user.IsEnabled)
        {
            return null;
        }

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            EnsureSucceeded(await _userManager.ResetAuthenticatorKeyAsync(user), "The MFA authenticator key could not be created.");
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("The MFA authenticator key is unavailable.");
        }

        var issuer = Uri.EscapeDataString("GSIP");
        var account = Uri.EscapeDataString(user.UserName ?? user.Id.ToString());
        var uri = $"otpauth://totp/{issuer}:{account}?secret={Uri.EscapeDataString(key)}&issuer={issuer}&digits=6";
        return new MfaEnrollmentDetails(FormatAuthenticatorKey(key), uri);
    }

    public async Task<AccountOperationResult> ConfirmMfaEnrollmentAsync(
        ClaimsPrincipal principal,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRestrictedUserAsync(principal, MfaEnrollmentRestriction);
        if (user is null || !user.IsEnabled)
        {
            return AccountOperationResult.Failure("INVALID_SESSION");
        }

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            NormalizeMfaCode(code));
        if (!valid)
        {
            await _audit.WriteAsync(user.Id, "MfaEnrollment", false, "INVALID_CODE", cancellationToken);
            return AccountOperationResult.Failure("INVALID_MFA_CODE");
        }

        EnsureSucceeded(await _userManager.SetTwoFactorEnabledAsync(user, true), "MFA could not be enabled.");
        await _audit.WriteAsync(user.Id, "MfaEnrollment", true, "ENABLED", cancellationToken);
        await ContinueAfterMfaAsync(user, RememberMeWasRequested(principal), cancellationToken);
        return AccountOperationResult.Success("MFA_ENABLED");
    }

    public async Task<AccountSignInResult> VerifyMfaAsync(
        ClaimsPrincipal principal,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRestrictedUserAsync(principal, MfaVerificationRestriction);
        if (user is null || !user.IsEnabled)
        {
            return new AccountSignInResult(AccountSignInStatus.InvalidCredentials);
        }

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            NormalizeMfaCode(code));
        if (!valid)
        {
            await _audit.WriteAsync(user.Id, "MfaVerification", false, "INVALID_CODE", cancellationToken);
            return new AccountSignInResult(AccountSignInStatus.InvalidCredentials);
        }

        await _audit.WriteAsync(user.Id, "MfaVerification", true, "VERIFIED", cancellationToken);
        return await ContinueAfterMfaAsync(user, RememberMeWasRequested(principal), cancellationToken);
    }

    public async Task<AccountOperationResult> ChangePasswordAsync(
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRestrictedUserAsync(principal, PasswordChangeRestriction);
        if (user is null || !user.IsEnabled)
        {
            return AccountOperationResult.Failure("INVALID_SESSION");
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            await _audit.WriteAsync(user.Id, "PasswordChange", false, "REJECTED", cancellationToken);
            return AccountOperationResult.Failure("PASSWORD_CHANGE_REJECTED");
        }

        user.MustChangePassword = false;
        EnsureSucceeded(await _userManager.UpdateAsync(user), "The password-change state could not be persisted.");
        await _audit.WriteAsync(user.Id, "PasswordChange", true, "CHANGED", cancellationToken);
        await CompleteSignInAsync(user, RememberMeWasRequested(principal), cancellationToken);
        return AccountOperationResult.Success("PASSWORD_CHANGED");
    }

    public async Task<SessionRestriction> GetSessionRestrictionAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return SessionRestriction.InvalidSession;
        }

        if (!user.IsEnabled)
        {
            return SessionRestriction.AccountDisabled;
        }

        var claim = principal.FindFirstValue(GsipIdentityClaims.SessionRestriction);
        if (string.Equals(claim, MfaEnrollmentRestriction, StringComparison.Ordinal))
        {
            return SessionRestriction.MfaEnrollmentRequired;
        }
        if (string.Equals(claim, MfaVerificationRestriction, StringComparison.Ordinal))
        {
            return SessionRestriction.MfaVerificationRequired;
        }
        if (string.Equals(claim, PasswordChangeRestriction, StringComparison.Ordinal))
        {
            return SessionRestriction.PasswordChangeRequired;
        }

        var policy = await _policyProvider.GetAsync(cancellationToken);
        if (user.IsPrivileged && policy.RequireMfaForPrivilegedAccounts && !user.TwoFactorEnabled)
        {
            return SessionRestriction.MfaEnrollmentRequired;
        }
        if (user.MustChangePassword)
        {
            return SessionRestriction.PasswordChangeRequired;
        }

        return SessionRestriction.None;
    }

    public async Task SignOutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        await _signInManager.SignOutAsync();
        await _audit.WriteAsync(user?.Id, "Logout", true, "SIGNED_OUT", cancellationToken);
    }

    public async Task<AccountOperationResult> SetAccountEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountOperationResult.Failure("ACCOUNT_NOT_FOUND");
        }

        user.IsEnabled = enabled;
        if (!enabled)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }
        EnsureSucceeded(await _userManager.UpdateAsync(user), "The account status could not be updated.");
        await _audit.WriteAsync(user.Id, "AccountStatusChange", true, enabled ? "ENABLED" : "DISABLED", cancellationToken);
        return AccountOperationResult.Success(enabled ? "ACCOUNT_ENABLED" : "ACCOUNT_DISABLED");
    }

    private async Task<AccountSignInResult> RecordPasswordFailureAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var policy = await _policyProvider.GetAsync(cancellationToken);
        user.AccessFailedCount++;
        var lockedOut = user.LockoutEnabled && user.AccessFailedCount >= policy.MaxFailedAccessAttempts;
        if (lockedOut)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = _clock.UtcNow.AddMinutes(policy.LockoutMinutes);
        }

        EnsureSucceeded(await _userManager.UpdateAsync(user), "The failed sign-in state could not be persisted.");
        await _audit.WriteAsync(user.Id, "LoginFailure", false, lockedOut ? "LOCKED_OUT" : "INVALID_CREDENTIALS", cancellationToken);
        if (lockedOut)
        {
            await _audit.WriteAsync(user.Id, "AccountLockout", true, "THRESHOLD_REACHED", cancellationToken);
            return new AccountSignInResult(AccountSignInStatus.LockedOut);
        }

        return new AccountSignInResult(AccountSignInStatus.InvalidCredentials);
    }

    private async Task<AccountSignInResult> ContinueAfterPasswordAsync(
        ApplicationUser user,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        var policy = await _policyProvider.GetAsync(cancellationToken);
        if (user.IsPrivileged && policy.RequireMfaForPrivilegedAccounts && !user.TwoFactorEnabled)
        {
            await IssueRestrictedSignInAsync(user, MfaEnrollmentRestriction, rememberMe, policy);
            return new AccountSignInResult(AccountSignInStatus.MfaEnrollmentRequired);
        }
        if (user.TwoFactorEnabled)
        {
            await IssueRestrictedSignInAsync(user, MfaVerificationRestriction, rememberMe, policy);
            return new AccountSignInResult(AccountSignInStatus.MfaVerificationRequired);
        }

        return await ContinueAfterMfaAsync(user, rememberMe, cancellationToken);
    }

    private async Task<AccountSignInResult> ContinueAfterMfaAsync(
        ApplicationUser user,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        var policy = await _policyProvider.GetAsync(cancellationToken);
        if (user.MustChangePassword)
        {
            await IssueRestrictedSignInAsync(user, PasswordChangeRestriction, rememberMe, policy);
            return new AccountSignInResult(AccountSignInStatus.PasswordChangeRequired);
        }

        await CompleteSignInAsync(user, rememberMe, cancellationToken);
        return new AccountSignInResult(AccountSignInStatus.Succeeded);
    }

    private async Task IssueRestrictedSignInAsync(
        ApplicationUser user,
        string restriction,
        bool rememberMe,
        AccountSecurityPolicy policy)
    {
        var properties = new AuthenticationProperties
        {
            AllowRefresh = false,
            IsPersistent = false,
            ExpiresUtc = _clock.UtcNow.AddMinutes(policy.MfaChallengeMinutes)
        };
        var claims = new List<Claim>
        {
            new(GsipIdentityClaims.SessionRestriction, restriction)
        };
        if (rememberMe && policy.AllowRememberMe)
        {
            claims.Add(new Claim(GsipIdentityClaims.RememberMeRequested, bool.TrueString));
        }
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        principal.Identities.First().AddClaims(claims);
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("An HTTP context is required to issue an authentication session.");
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);
    }

    private async Task CompleteSignInAsync(
        ApplicationUser user,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        var policy = await _policyProvider.GetAsync(cancellationToken);
        var persistent = rememberMe && policy.AllowRememberMe;
        user.LastLoginAtUtc = _clock.UtcNow;
        EnsureSucceeded(await _userManager.UpdateAsync(user), "The last-login state could not be persisted.");

        await _signInManager.SignInAsync(user, new AuthenticationProperties
        {
            AllowRefresh = false,
            IsPersistent = persistent,
            ExpiresUtc = persistent
                ? _clock.UtcNow.AddDays(policy.RememberMeDays)
                : _clock.UtcNow.AddMinutes(policy.SessionTimeoutMinutes)
        });
        await _audit.WriteAsync(user.Id, "LoginSuccess", true, "AUTHENTICATED", cancellationToken);
    }

    private async Task<ApplicationUser?> GetRestrictedUserAsync(ClaimsPrincipal principal, string expectedRestriction)
    {
        if (!string.Equals(
                principal.FindFirstValue(GsipIdentityClaims.SessionRestriction),
                expectedRestriction,
                StringComparison.Ordinal))
        {
            return null;
        }
        return await _userManager.GetUserAsync(principal);
    }

    private static bool RememberMeWasRequested(ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirstValue(GsipIdentityClaims.RememberMeRequested), bool.TrueString, StringComparison.Ordinal);

    private static string NormalizeMfaCode(string code) =>
        (code ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

    private static string FormatAuthenticatorKey(string key)
    {
        var normalized = key.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return string.Join(' ', Enumerable.Range(0, (normalized.Length + 3) / 4)
            .Select(index => normalized.Substring(index * 4, Math.Min(4, normalized.Length - (index * 4)))));
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(message);
        }
    }
}
