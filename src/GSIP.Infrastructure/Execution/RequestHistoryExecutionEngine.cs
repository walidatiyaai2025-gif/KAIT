using GSIP.Application.Execution;

namespace GSIP.Infrastructure.Execution;

/// <summary>
/// Outermost execution decorator. It proves the exact execution authorization before persistence,
/// then persists only the result returned by the canonical response-masking decorator.
/// </summary>
public sealed class RequestHistoryExecutionEngine(
    IServiceExecutionEngine inner,
    IServiceExecutionSecurityGate securityGate,
    IRequestHistoryStore historyStore) : IServiceExecutionEngine
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
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Cancelled, "Cancelled", CancellationToken.None);
            throw;
        }
        catch (ServiceExecutionValidationException)
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Failed, "ValidationRejected", CancellationToken.None);
            throw;
        }
        catch (ServiceExecutionRejectedException)
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Failed, "ExecutionRejected", CancellationToken.None);
            throw;
        }
        catch
        {
            await historyStore.TerminateAsync(trackingRequestId, RequestLifecycleStatus.Failed, "UnhandledFailure", CancellationToken.None);
            throw;
        }
    }
}
