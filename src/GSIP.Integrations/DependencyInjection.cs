using Microsoft.Extensions.DependencyInjection;

namespace GSIP.Integrations;

/// <summary>
/// Composition boundary only. Service execution/authentication implementations remain locked until P06/P07.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddGsipIntegrations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
