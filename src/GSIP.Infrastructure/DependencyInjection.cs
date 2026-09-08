using GSIP.Application.Abstractions;
using GSIP.Application.Setup;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GSIP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGsipInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IPasswordHasher<BootstrapAdministrator>, PasswordHasher<BootstrapAdministrator>>();
        return services;
    }

    private sealed class SystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
