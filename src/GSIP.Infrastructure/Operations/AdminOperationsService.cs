using System.Data;
using System.Diagnostics;
using System.Net;
using System.Security.Authentication;
using System.Security.Claims;
using GSIP.Application.Abstractions;
using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Operations;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GSIP.Infrastructure.Operations;

internal sealed record AdminOperationsRuntimeState(DateTimeOffset StartedAtUtc);

public sealed class AdminOperationsService(
    GsipDbContext db,
    IGsipPermissionEvaluator permissions,
    IMetadataCatalogService metadataCatalog,
    IAuthenticationProbeService authenticationProbe,
    IAuditTrailWriter auditTrail,
    IDataProtectionProvider dataProtectionProvider,
    IHostEnvironment hostEnvironment,
    IHttpClientFactory httpClientFactory,
    ISystemClock clock,
    IOptions<AdminOperationsOptions> options,
    AdminOperationsRuntimeState runtimeState) : IAdminOperationsService
{
    private const string ExecutionClientName = "GSIP.Execution";
    private readonly AdminOperationsOptions _options = options.Value;

    public async Task<AdminHealthSnapshot> GetHealthAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync(principal, GsipPermissions.DiagnosticsRun, cancellationToken);
        var correlationId = NewCorrelationId();
        var checkedAt = clock.UtcNow;
        var components = new List<AdminHealthComponent>();

        var database = await RunDatabaseDiagnosticCoreAsync(correlationId, cancellationToken);
        components.Add(ComponentFromDiagnostic("database", database));
        components.Add(BuildRuntimeComponent(checkedAt));
        components.Add(BuildDataProtectionComponent(checkedAt));
        components.Add(BuildDiskComponent(checkedAt));

        var integrations = await BuildVisibleIntegrationStatusAsync(principal, cancellationToken);
        components.Add(BuildConfigurationComponent(integrations, checkedAt));
        components.Add(new AdminHealthComponent(
            "background-queue",
            OperationalHealthState.NotConfigured,
            "BACKGROUND_QUEUE_NOT_PRESENT",
            "BackgroundQueueNotPresent",
            checkedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["durableQueueConfigured"] = "false"
            }));

        var alerts = await BuildAlertsAsync(integrations, cancellationToken);
        var overall = CalculateOverallState(components);

        await auditTrail.WriteAsync(new AuditTrailEvent(
            ReadActorId(principal),
            "Admin.Health.Read",
            "SystemHealth",
            null,
            true,
            overall.ToString(),
            CorrelationId: correlationId,
            Metadata: new Dictionary<string, object?>
            {
                ["componentCount"] = components.Count,
                ["integrationCount"] = integrations.Count,
                ["alertCount"] = alerts.Count,
                ["overallState"] = overall.ToString()
            }), cancellationToken);

        return new AdminHealthSnapshot(correlationId, checkedAt, overall, components, integrations, alerts);
    }

    public async Task<OperationalDiagnostic> RunDatabaseDiagnosticAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync(principal, GsipPermissions.DiagnosticsRun, cancellationToken);
        var correlationId = NewCorrelationId();
        var result = await RunDatabaseDiagnosticCoreAsync(correlationId, cancellationToken);
        await AuditDiagnosticAsync(principal, "Admin.Diagnostics.Database", result, null, null, cancellationToken);
        return result;
    }

    public async Task<OperationalDiagnostic> RunIntegrationDiagnosticAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync(principal, GsipPermissions.DiagnosticsRun, cancellationToken);
        var target = await ResolveAuthorizedTargetAsync(principal, serviceId, environmentId, requireSecretsPermission: false, cancellationToken);
        var correlationId = NewCorrelationId();
        var started = Stopwatch.StartNew();
        OperationalDiagnostic result;

        if (string.IsNullOrWhiteSpace(target.Config.HealthPath))
        {
            result = Diagnostic("Integration", "HEALTH_NOT_CONFIGURED", OperationalHealthState.NotConfigured,
                correlationId, "IntegrationHealthNotConfigured", started.ElapsedMilliseconds, serviceId, environmentId);
        }
        else if (!IsSafeHealthMethod(target.Config.HealthMethod))
        {
            result = Diagnostic("Integration", "HEALTH_METHOD_UNSAFE", OperationalHealthState.Unhealthy,
                correlationId, "IntegrationHealthMethodUnsafe", started.ElapsedMilliseconds, serviceId, environmentId);
        }
        else if (!TryBuildHealthEndpoint(target.Config, out var endpoint))
        {
            result = Diagnostic("Integration", "HEALTH_ENDPOINT_INVALID", OperationalHealthState.Unhealthy,
                correlationId, "IntegrationHealthEndpointInvalid", started.ElapsedMilliseconds, serviceId, environmentId);
        }
        else
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(
                Math.Min(target.Config.TimeoutSeconds, _options.IntegrationProbeTimeoutSeconds), 1, 60)));
            try
            {
                var client = httpClientFactory.CreateClient(ExecutionClientName);
                using var request = new HttpRequestMessage(
                    string.Equals(target.Config.HealthMethod, "HEAD", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Head : HttpMethod.Get,
                    endpoint);
                request.Headers.TryAddWithoutValidation("X-GSIP-Correlation-ID", correlationId);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                var status = (int)response.StatusCode;
                var skew = response.Headers.Date is DateTimeOffset serverDate
                    ? (long)Math.Round((serverDate - clock.UtcNow).TotalSeconds)
                    : (long?)null;

                var state = response.StatusCode switch
                {
                    >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices => OperationalHealthState.Healthy,
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => OperationalHealthState.Degraded,
                    >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError => OperationalHealthState.Degraded,
                    _ => OperationalHealthState.Unhealthy
                };
                var code = response.StatusCode switch
                {
                    >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices => "ENDPOINT_REACHABLE",
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "ENDPOINT_REACHABLE_AUTH_REQUIRED",
                    >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError => "ENDPOINT_REACHABLE_HTTP_ERROR",
                    _ => "ENDPOINT_SERVER_ERROR"
                };
                if (skew is long seconds && Math.Abs(seconds) > Math.Clamp(_options.ClockSkewWarningSeconds, 30, 3600)
                    && state == OperationalHealthState.Healthy)
                {
                    state = OperationalHealthState.Degraded;
                    code = "ENDPOINT_REACHABLE_CLOCK_SKEW";
                }
                result = Diagnostic("Integration", code, state, correlationId, code, started.ElapsedMilliseconds,
                    serviceId, environmentId, status, skew);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                result = Diagnostic("Integration", "ENDPOINT_TIMEOUT", OperationalHealthState.Unhealthy,
                    correlationId, "EndpointTimeout", started.ElapsedMilliseconds, serviceId, environmentId);
            }
            catch (HttpRequestException exception) when (IsTlsFailure(exception))
            {
                result = Diagnostic("Integration", "ENDPOINT_TLS_FAILED", OperationalHealthState.Unhealthy,
                    correlationId, "EndpointTlsFailed", started.ElapsedMilliseconds, serviceId, environmentId);
            }
            catch (HttpRequestException)
            {
                result = Diagnostic("Integration", "ENDPOINT_NETWORK_FAILED", OperationalHealthState.Unhealthy,
                    correlationId, "EndpointNetworkFailed", started.ElapsedMilliseconds, serviceId, environmentId);
            }
        }

        await PersistLastTestStatusAsync(serviceId, environmentId, result.Code, result.CheckedAtUtc, cancellationToken);
        await AuditDiagnosticAsync(principal, "Admin.Diagnostics.Integration", result, target.Service.Code, target.Environment.Code, cancellationToken);
        return result;
    }

    public async Task<OperationalDiagnostic> RunAuthenticationDiagnosticAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync(principal, GsipPermissions.DiagnosticsRun, cancellationToken);
        await RequirePermissionAsync(principal, GsipPermissions.ServiceSecretsManage, cancellationToken);
        var target = await ResolveAuthorizedTargetAsync(principal, serviceId, environmentId, requireSecretsPermission: true, cancellationToken);
        if (target.Config.AuthProfileId is not Guid configuredProfileId || configuredProfileId != authProfileId)
            throw new AdminOperationsTargetRejectedException();

        var correlationId = NewCorrelationId();
        var started = Stopwatch.StartNew();
        OperationalDiagnostic result;
        try
        {
            await authenticationProbe.TestTokenGenerationAsync(serviceId, environmentId, authProfileId, cancellationToken);
            result = Diagnostic("Authentication", "AUTHENTICATION_OK", OperationalHealthState.Healthy,
                correlationId, "AuthenticationDiagnosticPassed", started.ElapsedMilliseconds, serviceId, environmentId);
        }
        catch (AuthenticationProbeRejectedException)
        {
            result = Diagnostic("Authentication", "AUTHENTICATION_REJECTED", OperationalHealthState.Unhealthy,
                correlationId, "AuthenticationDiagnosticRejected", started.ElapsedMilliseconds, serviceId, environmentId);
        }

        await PersistLastTestStatusAsync(serviceId, environmentId, result.Code, result.CheckedAtUtc, cancellationToken);
        await AuditDiagnosticAsync(principal, "Admin.Diagnostics.Authentication", result, target.Service.Code, target.Environment.Code, cancellationToken);
        return result;
    }

    private async Task<OperationalDiagnostic> RunDatabaseDiagnosticCoreAsync(string correlationId, CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        try
        {
            if (openedHere)
                await db.Database.OpenConnectionAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 10;
            _ = await command.ExecuteScalarAsync(cancellationToken);
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Take(2).Count();
            return pending == 0
                ? Diagnostic("Database", "DATABASE_HEALTHY", OperationalHealthState.Healthy, correlationId,
                    "DatabaseHealthy", started.ElapsedMilliseconds)
                : Diagnostic("Database", "DATABASE_MIGRATIONS_PENDING", OperationalHealthState.Degraded, correlationId,
                    "DatabaseMigrationsPending", started.ElapsedMilliseconds);
        }
        catch (SqlException exception)
        {
            var classified = SetupSqlFailureClassifier.Classify(exception);
            return Diagnostic("Database", classified.Code, OperationalHealthState.Unhealthy, correlationId,
                classified.Code, started.ElapsedMilliseconds);
        }
        catch (InvalidOperationException)
        {
            return Diagnostic("Database", "DATABASE_CONFIGURATION_UNAVAILABLE", OperationalHealthState.Unhealthy,
                correlationId, "DatabaseConfigurationUnavailable", started.ElapsedMilliseconds);
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open)
            {
                try { await db.Database.CloseConnectionAsync(); } catch { }
            }
        }
    }

    private AdminHealthComponent BuildRuntimeComponent(DateTimeOffset checkedAt)
    {
        var uptime = checkedAt - runtimeState.StartedAtUtc;
        return new AdminHealthComponent(
            "application-runtime",
            OperationalHealthState.Healthy,
            "RUNTIME_HEALTHY",
            "RuntimeHealthy",
            checkedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["uptimeSeconds"] = Math.Max(0, (long)uptime.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["runtimeVersion"] = Environment.Version.ToString(),
                ["environment"] = hostEnvironment.EnvironmentName
            });
    }

    private AdminHealthComponent BuildDataProtectionComponent(DateTimeOffset checkedAt)
    {
        try
        {
            const string marker = "GSIP-P12-DATA-PROTECTION-PROBE";
            var protector = dataProtectionProvider.CreateProtector("GSIP.P12.Health.v1");
            var protectedValue = protector.Protect(marker);
            var healthy = string.Equals(protector.Unprotect(protectedValue), marker, StringComparison.Ordinal);
            return new AdminHealthComponent(
                "data-protection",
                healthy ? OperationalHealthState.Healthy : OperationalHealthState.Unhealthy,
                healthy ? "DATA_PROTECTION_HEALTHY" : "DATA_PROTECTION_ROUNDTRIP_FAILED",
                healthy ? "DataProtectionHealthy" : "DataProtectionRoundtripFailed",
                checkedAt,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["roundTrip"] = healthy ? "pass" : "fail" });
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return new AdminHealthComponent(
                "data-protection",
                OperationalHealthState.Unhealthy,
                "DATA_PROTECTION_FAILED",
                "DataProtectionFailed",
                checkedAt,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["roundTrip"] = "fail" });
        }
    }

    private AdminHealthComponent BuildDiskComponent(DateTimeOffset checkedAt)
    {
        try
        {
            var root = Path.GetPathRoot(hostEnvironment.ContentRootPath);
            if (string.IsNullOrWhiteSpace(root))
                throw new IOException();
            var drive = new DriveInfo(root);
            var freePercent = drive.TotalSize <= 0 ? 0 : (int)Math.Floor(drive.AvailableFreeSpace * 100d / drive.TotalSize);
            var warning = Math.Clamp(_options.DiskFreeWarningPercent, 5, 50);
            var state = freePercent < warning ? OperationalHealthState.Degraded : OperationalHealthState.Healthy;
            return new AdminHealthComponent(
                "disk",
                state,
                state == OperationalHealthState.Healthy ? "DISK_HEALTHY" : "DISK_LOW_FREE_SPACE",
                state == OperationalHealthState.Healthy ? "DiskHealthy" : "DiskLowFreeSpace",
                checkedAt,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["freePercent"] = freePercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["availableMiB"] = (drive.AvailableFreeSpace / 1024 / 1024).ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
        }
        catch (IOException)
        {
            return new AdminHealthComponent(
                "disk",
                OperationalHealthState.Degraded,
                "DISK_STATUS_UNAVAILABLE",
                "DiskStatusUnavailable",
                checkedAt,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }
    }

    private static AdminHealthComponent BuildConfigurationComponent(
        IReadOnlyList<AdminServiceEnvironmentStatus> integrations,
        DateTimeOffset checkedAt)
    {
        if (integrations.Count == 0)
        {
            return new AdminHealthComponent(
                "configuration-readiness",
                OperationalHealthState.NotConfigured,
                "CONFIGURATION_NOT_VISIBLE_OR_NOT_CONFIGURED",
                "ConfigurationNotVisibleOrNotConfigured",
                checkedAt,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["visibleEnvironmentCount"] = "0" });
        }

        var active = integrations.Count(item => item.ServiceActive && item.EnvironmentActive && item.ConfigurationActive);
        var healthConfigured = integrations.Count(item => item.HealthProbeConfigured);
        var authConfigured = integrations.Count(item => item.AuthenticationConfigured);
        var state = active == integrations.Count ? OperationalHealthState.Healthy : OperationalHealthState.Degraded;
        return new AdminHealthComponent(
            "configuration-readiness",
            state,
            state == OperationalHealthState.Healthy ? "CONFIGURATION_READY" : "CONFIGURATION_PARTIALLY_INACTIVE",
            state == OperationalHealthState.Healthy ? "ConfigurationReady" : "ConfigurationPartiallyInactive",
            checkedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["visibleEnvironmentCount"] = integrations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["activeEnvironmentCount"] = active.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["healthProbeConfiguredCount"] = healthConfigured.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["authenticationConfiguredCount"] = authConfigured.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
    }

    private async Task<IReadOnlyList<AdminServiceEnvironmentStatus>> BuildVisibleIntegrationStatusAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
        var result = new List<AdminServiceEnvironmentStatus>();
        foreach (var service in snapshot.Services.Where(item => item.IsCurrent).OrderBy(item => item.Code, StringComparer.Ordinal))
        {
            var canView = await permissions.HasServicePermissionAsync(principal, service.Code, GsipPermissions.ServicesView, cancellationToken)
                || await permissions.HasServicePermissionAsync(principal, service.Code, GsipPermissions.ServicesManage, cancellationToken);
            if (!canView)
                continue;

            var entityCode = snapshot.Entities.SingleOrDefault(item => item.Id == service.EntityId)?.Code ?? string.Empty;
            foreach (var config in service.EnvironmentConfigs.OrderBy(item => item.EnvironmentId))
            {
                var environment = snapshot.Environments.SingleOrDefault(item => item.Id == config.EnvironmentId);
                if (environment is null)
                    continue;
                result.Add(new AdminServiceEnvironmentStatus(
                    service.Id,
                    entityCode,
                    service.Code,
                    service.NameAr,
                    service.NameEn,
                    service.Active,
                    environment.Id,
                    environment.Code,
                    environment.Active,
                    config.Active,
                    !string.IsNullOrWhiteSpace(config.HealthPath) && IsSafeHealthMethod(config.HealthMethod),
                    config.AuthProfileId is not null,
                    BoundCode(config.LastTestStatus)));
            }
        }
        return result;
    }

    private async Task<IReadOnlyList<AdminOperationalAlert>> BuildAlertsAsync(
        IReadOnlyList<AdminServiceEnvironmentStatus> integrations,
        CancellationToken cancellationToken)
    {
        var alerts = new List<AdminOperationalAlert>();
        foreach (var item in integrations)
        {
            if (item.ServiceActive && item.EnvironmentActive && item.ConfigurationActive && !item.HealthProbeConfigured)
            {
                alerts.Add(new AdminOperationalAlert(
                    "HEALTH_PROBE_NOT_CONFIGURED",
                    OperationalHealthState.Degraded,
                    "HealthProbeNotConfigured",
                    item.ServiceCode,
                    item.EnvironmentCode));
            }
        }

        var allowedCodes = integrations.Select(item => item.ServiceCode).Distinct(StringComparer.Ordinal).ToArray();
        if (allowedCodes.Length > 0)
        {
            var since = clock.UtcNow.AddHours(-24);
            var failures = await db.AuthenticationAuditEvents
                .AsNoTracking()
                .Where(item => item.SequenceNumber != null
                    && !item.Succeeded
                    && item.OccurredAtUtc >= since
                    && item.ServiceCode != null
                    && allowedCodes.Contains(item.ServiceCode))
                .GroupBy(item => item.ServiceCode!)
                .Select(group => new { ServiceCode = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
            var threshold = Math.Clamp(_options.RepeatedFailureAlertThreshold, 2, 1000);
            foreach (var failure in failures.Where(item => item.Count >= threshold))
            {
                alerts.Add(new AdminOperationalAlert(
                    "REPEATED_OPERATIONAL_FAILURES",
                    OperationalHealthState.Degraded,
                    "RepeatedOperationalFailures",
                    failure.ServiceCode));
            }
        }
        return alerts;
    }

    private async Task<AuthorizedTarget> ResolveAuthorizedTargetAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        bool requireSecretsPermission,
        CancellationToken cancellationToken)
    {
        if (serviceId == Guid.Empty || environmentId == Guid.Empty)
            throw new AdminOperationsTargetRejectedException();

        var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
        var serviceMatches = snapshot.Services.Where(item => item.Id == serviceId && item.IsCurrent).Take(2).ToArray();
        if (serviceMatches.Length != 1 || string.IsNullOrWhiteSpace(serviceMatches[0].Code))
            throw new AdminOperationsTargetRejectedException();
        var service = serviceMatches[0];

        if (!await permissions.HasServicePermissionAsync(principal, service.Code, GsipPermissions.ServicesManage, cancellationToken))
            throw new AdminOperationsAccessDeniedException();
        if (requireSecretsPermission
            && !await permissions.HasPermissionAsync(principal, GsipPermissions.ServiceSecretsManage, cancellationToken))
            throw new AdminOperationsAccessDeniedException();

        var environmentMatches = snapshot.Environments.Where(item => item.Id == environmentId).Take(2).ToArray();
        if (environmentMatches.Length != 1)
            throw new AdminOperationsTargetRejectedException();
        var configs = service.EnvironmentConfigs.Where(item => item.EnvironmentId == environmentId).Take(2).ToArray();
        if (configs.Length != 1)
            throw new AdminOperationsTargetRejectedException();
        return new AuthorizedTarget(service, environmentMatches[0], configs[0]);
    }

    private async Task PersistLastTestStatusAsync(
        Guid serviceId,
        Guid environmentId,
        string code,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var row = await db.ServiceEnvironmentConfigs.SingleOrDefaultAsync(
            item => item.ServiceId == serviceId && item.EnvironmentId == environmentId,
            cancellationToken);
        if (row is null)
            throw new AdminOperationsTargetRejectedException();
        row.LastTestedAtUtc = checkedAt;
        row.LastTestStatus = BoundCode(code);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RequirePermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true
            || !await permissions.HasPermissionAsync(principal, permission, cancellationToken))
            throw new AdminOperationsAccessDeniedException();
    }

    private async Task AuditDiagnosticAsync(
        ClaimsPrincipal principal,
        string action,
        OperationalDiagnostic result,
        string? serviceCode,
        string? environmentCode,
        CancellationToken cancellationToken)
    {
        await auditTrail.WriteAsync(new AuditTrailEvent(
            ReadActorId(principal),
            action,
            "OperationalDiagnostic",
            serviceCode is null ? result.Category : $"{serviceCode}:{environmentCode}",
            result.State == OperationalHealthState.Healthy,
            result.Code,
            CorrelationId: result.CorrelationId,
            ServiceCode: serviceCode,
            Metadata: new Dictionary<string, object?>
            {
                ["category"] = result.Category,
                ["code"] = result.Code,
                ["state"] = result.State.ToString(),
                ["durationMs"] = result.DurationMilliseconds,
                ["httpStatusCode"] = result.HttpStatusCode,
                ["clockSkewSeconds"] = result.ClockSkewSeconds,
                ["environmentCode"] = environmentCode
            }), cancellationToken);
    }

    private static AdminHealthComponent ComponentFromDiagnostic(string key, OperationalDiagnostic diagnostic) => new(
        key,
        diagnostic.State,
        diagnostic.Code,
        diagnostic.MessageCode,
        diagnostic.CheckedAtUtc,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["correlationId"] = diagnostic.CorrelationId,
            ["durationMs"] = diagnostic.DurationMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

    private static OperationalHealthState CalculateOverallState(IReadOnlyList<AdminHealthComponent> components)
    {
        if (components.Any(item => item.State == OperationalHealthState.Unhealthy)) return OperationalHealthState.Unhealthy;
        if (components.Any(item => item.State == OperationalHealthState.Degraded)) return OperationalHealthState.Degraded;
        return OperationalHealthState.Healthy;
    }

    private static bool TryBuildHealthEndpoint(ServiceEnvironmentConfig config, out Uri endpoint)
    {
        endpoint = null!;
        if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https")
            || !Uri.TryCreate(config.HealthPath, UriKind.Relative, out var relative))
            return false;
        var normalizedBase = new Uri(baseUri.ToString().TrimEnd('/') + "/", UriKind.Absolute);
        endpoint = new Uri(normalizedBase, relative.ToString().TrimStart('/'));
        return string.Equals(endpoint.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(endpoint.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            && endpoint.Port == baseUri.Port;
    }

    private static bool IsSafeHealthMethod(string? method) =>
        string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);

    private static bool IsTlsFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is AuthenticationException) return true;
            if (current.InnerException is null) break;
        }
        return false;
    }

    private OperationalDiagnostic Diagnostic(
        string category,
        string code,
        OperationalHealthState state,
        string correlationId,
        string messageCode,
        long durationMilliseconds,
        Guid? serviceId = null,
        Guid? environmentId = null,
        int? httpStatusCode = null,
        long? clockSkewSeconds = null) => new(
            category,
            BoundCode(code),
            state,
            correlationId,
            BoundCode(messageCode),
            clock.UtcNow,
            Math.Max(0, durationMilliseconds),
            serviceId,
            environmentId,
            httpStatusCode,
            clockSkewSeconds);

    private static string BoundCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    private static Guid? ReadActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string NewCorrelationId() => Guid.NewGuid().ToString("D");

    private sealed record AuthorizedTarget(
        CatalogService Service,
        CatalogEnvironment Environment,
        ServiceEnvironmentConfig Config);
}
