using System.Security.Claims;
using GSIP.Application.Authorization;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;

namespace GSIP.Application.Execution;

public sealed class ServiceExecutionRejectedException : InvalidOperationException
{
    public const string SafeMessage = "Service execution request was rejected.";

    public ServiceExecutionRejectedException()
        : base(SafeMessage)
    {
    }
}

public sealed record AuthorizedServiceExecutionBinding(
    Guid ServiceId,
    string ServiceCode,
    Guid EnvironmentId,
    string EnvironmentCode,
    string BaseUrl,
    string RelativePath,
    string HttpMethod,
    string ContentType,
    int TimeoutSeconds,
    string TlsPolicy,
    bool ValidateServerCertificate,
    string ProxyUrl,
    Guid? AuthProfileId,
    long? AuthProfileVersion,
    string ConfiguredHeadersJson = "{}");

public interface IServiceExecutionSecurityGate
{
    Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        CancellationToken cancellationToken = default);
}

public sealed class ServiceExecutionSecurityGate(
    IMetadataCatalogService metadataCatalog,
    IGsipPermissionEvaluator permissionEvaluator,
    IAuthProfileService authProfiles) : IServiceExecutionSecurityGate
{
    public async Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true || serviceId == Guid.Empty || environmentId == Guid.Empty)
        {
            throw new ServiceExecutionRejectedException();
        }

        try
        {
            var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
            var serviceMatches = snapshot.Services
                .Where(service => service.Id == serviceId && service.IsCurrent && service.Active)
                .Take(2)
                .ToArray();
            if (serviceMatches.Length != 1 || string.IsNullOrWhiteSpace(serviceMatches[0].Code))
            {
                throw new ServiceExecutionRejectedException();
            }

            var service = serviceMatches[0];
            if (!await permissionEvaluator.HasServicePermissionAsync(
                    principal,
                    service.Code,
                    GsipPermissions.ServicesExecute,
                    cancellationToken))
            {
                throw new ServiceExecutionRejectedException();
            }

            var environmentMatches = snapshot.Environments
                .Where(environment => environment.Id == environmentId && environment.Active)
                .Take(2)
                .ToArray();
            if (environmentMatches.Length != 1 || string.IsNullOrWhiteSpace(environmentMatches[0].Code))
            {
                throw new ServiceExecutionRejectedException();
            }

            var configMatches = service.EnvironmentConfigs
                .Where(config => config.ServiceId == service.Id
                    && config.EnvironmentId == environmentId
                    && config.Active)
                .Take(2)
                .ToArray();
            if (configMatches.Length != 1)
            {
                throw new ServiceExecutionRejectedException();
            }

            var config = configMatches[0];
            long? authProfileVersion = null;
            if (config.AuthProfileId is Guid expectedAuthProfileId)
            {
                var profile = await authProfiles.ResolveAsync(service.Id, environmentId, cancellationToken);
                if (profile is null || !profile.IsEnabled || profile.Id != expectedAuthProfileId)
                {
                    throw new ServiceExecutionRejectedException();
                }

                var exactBindings = profile.Bindings
                    .Where(binding => binding.ServiceId == service.Id && binding.EnvironmentId == environmentId)
                    .Take(2)
                    .ToArray();
                if (exactBindings.Length != 1)
                {
                    throw new ServiceExecutionRejectedException();
                }

                var ownsExactScope = profile.OwnerServiceId == service.Id
                    && profile.OwnerEnvironmentId == environmentId;
                if (!ownsExactScope && !exactBindings[0].IsShared)
                {
                    throw new ServiceExecutionRejectedException();
                }

                authProfileVersion = profile.Version;
            }

            return new AuthorizedServiceExecutionBinding(
                service.Id,
                service.Code,
                environmentId,
                environmentMatches[0].Code,
                config.BaseUrl,
                config.RelativePath,
                config.HttpMethod,
                config.ContentType,
                config.TimeoutSeconds,
                config.TlsPolicy,
                config.ValidateServerCertificate,
                config.ProxyUrl,
                config.AuthProfileId,
                authProfileVersion,
                config.NonSecretHeadersJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ServiceExecutionRejectedException)
        {
            throw;
        }
        catch
        {
            throw new ServiceExecutionRejectedException();
        }
    }
}
