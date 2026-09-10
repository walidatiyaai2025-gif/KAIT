using GSIP.Application.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure.Metadata;

internal sealed class GreenGovernmentCatalogBootstrapService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        var status = await setup.GetStatusAsync(cancellationToken);
        if (!status.IsCompleted)
            return;

        var greenSeed = ActivatorUtilities.CreateInstance<GreenGovernmentCatalogSeedService>(scope.ServiceProvider);
        await greenSeed.SeedAsync(cancellationToken);

        // Official contract overlays are intentionally applied only after the additive green
        // catalog exists so untouched catalog placeholders can be upgraded without guessing.
        var officialSeed = ActivatorUtilities.CreateInstance<OfficialAgencyMetadataSeedService>(scope.ServiceProvider);
        await officialSeed.SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
