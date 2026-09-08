using GSIP.Application.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GSIP.Infrastructure.Identity;

internal sealed class AccountSecurityPolicyProvider(
    GsipDbContext dbContext,
    IOptions<IdentitySecurityOptions> options) : IAccountSecurityPolicyProvider
{
    public async Task<AccountSecurityPolicy> GetAsync(CancellationToken cancellationToken = default)
    {
        var setup = await dbContext.SystemSetup
            .AsNoTracking()
            .SingleAsync(x => x.Id == 1, cancellationToken);
        var configured = options.Value;

        return new AccountSecurityPolicy(
            Math.Clamp(setup.SessionTimeoutMinutes, 5, 1440),
            Math.Clamp(setup.LockoutMinutes, 1, 1440),
            Math.Clamp(setup.MaxFailedAccessAttempts, 3, 20),
            setup.RequireMfaForPrivilegedAccounts,
            configured.AllowRememberMe,
            Math.Clamp(configured.RememberMeDays, 1, 90),
            Math.Clamp(configured.MfaChallengeMinutes, 2, 15));
    }
}

