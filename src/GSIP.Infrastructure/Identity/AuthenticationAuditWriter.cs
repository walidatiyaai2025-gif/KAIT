using GSIP.Application.Abstractions;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Http;

namespace GSIP.Infrastructure.Identity;

internal interface IAuthenticationAuditWriter
{
    Task WriteAsync(Guid? userId, string eventType, bool succeeded, string resultCode, CancellationToken cancellationToken);
}

internal sealed class AuthenticationAuditWriter(
    GsipDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    GSIP.Application.Abstractions.ISystemClock clock) : IAuthenticationAuditWriter
{
    public async Task WriteAsync(
        Guid? userId,
        string eventType,
        bool succeeded,
        string resultCode,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        dbContext.AuthenticationAuditEvents.Add(new AuthenticationAuditEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = Truncate(eventType, 64),
            Succeeded = succeeded,
            ResultCode = Truncate(resultCode, 64),
            CorrelationId = Truncate(correlationId, 100),
            OccurredAtUtc = clock.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
