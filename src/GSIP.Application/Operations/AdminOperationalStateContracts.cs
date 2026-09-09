using System.Security.Claims;

namespace GSIP.Application.Operations;

public sealed record AdminEnvironmentStateResult(
    Guid ServiceId,
    string ServiceCode,
    Guid EnvironmentId,
    string EnvironmentCode,
    bool IsActive,
    DateTimeOffset ChangedAtUtc);

public sealed class AdminOperationsStateConflictException : Exception
{
    public AdminOperationsStateConflictException() : base("The requested operational state conflicts with the current service/environment state.") { }
}

public interface IAdminOperationalStateService
{
    Task<AdminEnvironmentStateResult> SetEnvironmentStateAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
