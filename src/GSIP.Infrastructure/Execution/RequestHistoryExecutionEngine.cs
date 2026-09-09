using System.Security.Claims;
using GSIP.Application.Auditing;
using GSIP.Application.Execution;

namespace GSIP.Infrastructure.Execution;

/// <summary>
/// Outermost execution decorator. It proves the exact execution authorization before persistence,
/// then persists only the result returned by the canonical response-masking decorator.
/// P11 records execution facts only; request/response payloads never enter the audit ledger.
/// </summary>
public sealed class RequestHistoryExecutionEngine(
    IServiceExecutionEngine inner,
    IServiceExecutionSecurityGate securityGate,
    IRequestHistoryStore historyStore,
    IAuditTrailWriter? auditTrail = null) : IServiceExecutionEngine
{
    public async Task<ServiceExecutionResult> ExecuteAsync(ServiceExecutionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var binding = await securityGate.AuthorizeAsync(command.Principal, command.ServiceId, command.EnvironmentId, cancellationToken);
        var trackingRequestId = await historyStore.StartAsync(command, binding, cancellationToken);
        try
        {
            var result = await inner.ExecuteAsync(command, cancellationToken);
            await historyStore.CompleteAsync(trackingRequestId, result, cancellationToken);
            await WriteAuditAsync(command.Principal, binding, result.RequestId, result.CorrelationId, true, result.Outcome.ToString(),
                new Dictionary<string, object?>
                {
                    ["environmentCode"] = result.EnvironmentCode,
                    ["statusCode"] = result.StatusCode,
                    ["durationMilliseconds"] = result.DurationMilliseconds,
                    ["attempts"] = result.Attempts
                }, cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Cancelled, "Cancelled", CancellationToken.None);
            await WriteAuditAsync(command.Principal, binding, trackingRequestId, null, false, "Cancelled", null, CancellationToken.None);
            throw;
        }
        catch (ServiceExecutionValidationException)
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Failed, "ValidationRejected", CancellationToken.None);
            await WriteAuditAsync(command.Principal, binding, trackingRequestId, null, false, "ValidationRejected", null, CancellationToken.None);
            throw;
        }
        catch (ServiceExecutionRejectedException)
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Failed, "ExecutionRejected", CancellationToken.None);
            await WriteAuditAsync(command.Principal, binding, trackingRequestId, null, false, "ExecutionRejected", null, CancellationToken.None);
            throw;
        }
        catch
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Failed, "UnhandledFailure", CancellationToken.None);
            await WriteAuditAsync(command.Principal, binding, trackingRequestId, null, false, "UnhandledFailure", null, CancellationToken.None);
            throw;
        }
    }

    private async Task WriteAuditAsync(
        ClaimsPrincipal principal,
        AuthorizedServiceExecutionBinding binding,
        string requestId,
        string? correlationId,
        bool succeeded,
        string outcome,
        IReadOnlyDictionary<string, object?>? metadata,
        CancellationToken cancellationToken)
    {
        if (auditTrail is null)
            return;

        await auditTrail.WriteAsync(new AuditTrailEvent(
            ReadActorId(principal),
            "Service.Execute",
            "Service",
            binding.ServiceId.ToString("D"),
            succeeded,
            outcome,
            correlationId,
            requestId,
            ServiceCode: binding.ServiceCode,
            Source: "ExecutionEngine",
            Metadata: metadata), cancellationToken);
    }

    private static Guid? ReadActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
