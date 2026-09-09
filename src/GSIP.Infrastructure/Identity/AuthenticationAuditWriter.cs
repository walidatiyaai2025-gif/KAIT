using GSIP.Application.Auditing;

namespace GSIP.Infrastructure.Identity;

internal interface IAuthenticationAuditWriter
{
    Task WriteAsync(Guid? userId, string eventType, bool succeeded, string resultCode, CancellationToken cancellationToken);
}

internal sealed class AuthenticationAuditWriter(IAuditTrailWriter auditTrail) : IAuthenticationAuditWriter
{
    public async Task WriteAsync(
        Guid? userId,
        string eventType,
        bool succeeded,
        string resultCode,
        CancellationToken cancellationToken)
    {
        await auditTrail.WriteAsync(new AuditTrailEvent(
            userId,
            eventType,
            "Account",
            userId?.ToString("D"),
            succeeded,
            resultCode,
            Source: "Identity"), cancellationToken);
    }
}
