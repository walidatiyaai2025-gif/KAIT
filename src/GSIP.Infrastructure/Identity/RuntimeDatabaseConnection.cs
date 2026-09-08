using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure.Identity;

internal interface IRuntimeDatabaseConnection
{
    string GetRequiredConnectionString();
}

internal sealed class RuntimeDatabaseConnection(
    IDataProtectionProvider dataProtectionProvider,
    IHostEnvironment environment,
    IConfiguration configuration) : IRuntimeDatabaseConnection
{
    private const string ProtectorPurpose = "GSIP.Setup.State.v1";
    private const string RegressionConnectionKey = "IdentitySecurity:RegressionConnectionString";

    public string GetRequiredConnectionString()
    {
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "setup", "completed.protected");
        if (!File.Exists(path))
        {
            // Browser regression jobs intentionally bypass the completed-setup gate so they can
            // preserve closed P01/P03 UI contracts without persisting a fake production setup
            // record. Keep this escape hatch strictly confined to the dedicated test environment.
            if (environment.IsEnvironment("RegressionTesting"))
            {
                var regressionConnection = configuration[RegressionConnectionKey];
                if (!string.IsNullOrWhiteSpace(regressionConnection))
                {
                    return regressionConnection;
                }
            }

            throw new InvalidOperationException("GSIP setup is not complete; a runtime database connection is unavailable.");
        }

        try
        {
            var protectedText = File.ReadAllText(path);
            var json = dataProtectionProvider.CreateProtector(ProtectorPurpose).Unprotect(protectedText);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("ConnectionString", out var value)
                || string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new InvalidOperationException("The protected setup completion record does not contain a database connection.");
            }

            return value.GetString()!;
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            throw new InvalidOperationException("The protected setup completion record cannot be decrypted with the current Data Protection keys.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The protected setup completion record is invalid.", exception);
        }
    }
}

internal sealed class IdentityDatabaseMigrationService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<GSIP.Application.Setup.ISetupService>();
        if (!(await setup.GetStatusAsync(cancellationToken)).IsCompleted)
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<Setup.GsipDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
