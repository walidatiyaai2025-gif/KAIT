using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using GSIP.Application.Secrets;
using GSIP.Application.Setup;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure.Metadata;

internal sealed class MojTokenFlowConvergenceService(IServiceScopeFactory scopeFactory) : IHostedService
{
    private static readonly HashSet<string> SupportedProfileSecrets = new(StringComparer.OrdinalIgnoreCase)
    {
        "username",
        "password",
        TokenEndpointContractMetadata.ApiKeySecretName,
        TokenEndpointContractMetadata.GehaSecretName
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        if (!(await setup.GetStatusAsync(cancellationToken)).IsCompleted)
            return;

        var db = scope.ServiceProvider.GetRequiredService<GsipDbContext>();
        var authProfiles = scope.ServiceProvider.GetRequiredService<IAuthProfileService>();
        var secretVault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var serviceCodes = MojMetadataSeedService.CanonicalServiceCodes.ToArray();

        var services = await db.CatalogServices
            .AsNoTracking()
            .Include(service => service.EnvironmentConfigs)
            .Where(service => service.IsCurrent && serviceCodes.Contains(service.Code))
            .ToListAsync(cancellationToken);

        foreach (var service in services)
        {
            var config = service.EnvironmentConfigs.SingleOrDefault(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
            if (config is null || string.IsNullOrWhiteSpace(config.NonSecretHeadersJson))
                continue;

            JsonObject metadata;
            try
            {
                metadata = JsonNode.Parse(config.NonSecretHeadersJson)?.AsObject() ?? new JsonObject();
            }
            catch
            {
                continue;
            }

            if (!metadata.ContainsKey(TokenEndpointContractMetadata.PathKey))
                continue;

            metadata[TokenEndpointContractMetadata.ApiKeyRequiredKey] = true;
            metadata[TokenEndpointContractMetadata.UsernameRequiredKey] = false;
            metadata[TokenEndpointContractMetadata.PasswordRequiredKey] = false;

            await db.ServiceEnvironmentConfigs
                .Where(item => item.ServiceId == service.Id && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.NonSecretHeadersJson,
                    metadata.ToJsonString()), cancellationToken);

            if (config.AuthProfileId is not Guid profileId)
                continue;

            AuthProfileDescriptor profile;
            try
            {
                profile = await authProfiles.GetAsync(profileId, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (!HasExclusiveOwnerBinding(profile, service.Id))
            {
                await DisableConfigAsync(db, service.Id, cancellationToken);
                continue;
            }

            var unsupported = profile.Secrets.Where(secret => !SupportedProfileSecrets.Contains(secret.Name)).ToArray();
            if (unsupported.Length != 0)
            {
                await authProfiles.SetEnabledAsync(profile.Id, false, cancellationToken);
                await DisableConfigAsync(db, service.Id, cancellationToken);
                continue;
            }

            var wasEnabled = profile.IsEnabled;
            if (profile.IsEnabled)
                profile = await authProfiles.SetEnabledAsync(profile.Id, false, cancellationToken);

            if (profile.AuthType is not (AuthProfileType.TokenEndpoint or AuthProfileType.ApiKeyHeader))
            {
                await DisableConfigAsync(db, service.Id, cancellationToken);
                continue;
            }

            profile = await authProfiles.UpdateAsync(
                new UpdateAuthProfileCommand(
                    profile.Id,
                    $"MOJ {service.Code} UAT token authentication",
                    AuthProfileType.TokenEndpoint),
                cancellationToken);

            profile = await EnsureEmptyCredentialAsync(
                secretVault,
                authProfiles,
                service.Id,
                profile,
                "username",
                cancellationToken);
            profile = await EnsureEmptyCredentialAsync(
                secretVault,
                authProfiles,
                service.Id,
                profile,
                "password",
                cancellationToken);

            profile = await authProfiles.GetAsync(profile.Id, cancellationToken);
            var hasApiKey = profile.Secrets.Any(secret =>
                string.Equals(secret.Name, TokenEndpointContractMetadata.ApiKeySecretName, StringComparison.OrdinalIgnoreCase));
            var gehaRequired = metadata.ContainsKey(TokenEndpointContractMetadata.GehaFieldKey);
            var hasGeha = profile.Secrets.Any(secret =>
                string.Equals(secret.Name, TokenEndpointContractMetadata.GehaSecretName, StringComparison.OrdinalIgnoreCase));

            if (wasEnabled && hasApiKey && (!gehaRequired || hasGeha))
                await authProfiles.SetEnabledAsync(profile.Id, true, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<AuthProfileDescriptor> EnsureEmptyCredentialAsync(
        ISecretVault secretVault,
        IAuthProfileService authProfiles,
        Guid serviceId,
        AuthProfileDescriptor profile,
        string secretName,
        CancellationToken cancellationToken)
    {
        if (profile.Secrets.Any(secret => string.Equals(secret.Name, secretName, StringComparison.OrdinalIgnoreCase)))
            return profile;

        var bytes = Encoding.UTF8.GetBytes(TokenEndpointContractMetadata.EmptyCredentialSentinel);
        SecretRef? createdReference = null;
        try
        {
            var created = await secretVault.CreateActiveAsync(
                serviceId,
                CatalogEnvironmentCodes.UatId,
                profile.Id,
                secretName,
                bytes,
                cancellationToken);
            createdReference = created.Reference;
            profile = await authProfiles.SetSecretReferenceAsync(profile.Id, secretName, created.Reference, cancellationToken);
            createdReference = null;
            return profile;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (createdReference is SecretRef orphanedReference)
            {
                try
                {
                    await secretVault.RevokeAsync(
                        serviceId,
                        CatalogEnvironmentCodes.UatId,
                        profile.Id,
                        secretName,
                        orphanedReference,
                        CancellationToken.None);
                }
                catch
                {
                    // Best-effort cleanup only. The profile stays disabled on incomplete convergence.
                }
            }
        }
    }

    private static bool HasExclusiveOwnerBinding(AuthProfileDescriptor profile, Guid serviceId)
    {
        if (profile.OwnerServiceId != serviceId
            || profile.OwnerEnvironmentId != CatalogEnvironmentCodes.UatId
            || profile.Bindings.Count != 1)
            return false;

        var binding = profile.Bindings[0];
        return binding.ServiceId == serviceId
            && binding.EnvironmentId == CatalogEnvironmentCodes.UatId
            && !binding.IsShared;
    }

    private static Task<int> DisableConfigAsync(GsipDbContext db, Guid serviceId, CancellationToken cancellationToken) =>
        db.ServiceEnvironmentConfigs
            .Where(item => item.ServiceId == serviceId && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Active, false)
                .SetProperty(item => item.LastTestStatus, "TOKEN_FLOW_CONVERGENCE_REQUIRES_REVIEW"),
                cancellationToken);
}
