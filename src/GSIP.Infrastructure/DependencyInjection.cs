using GSIP.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GSIP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGsipInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISystemClock, SystemClock>();
        return services;
    }

    private sealed class SystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
