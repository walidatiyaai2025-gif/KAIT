using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GSIP.Infrastructure.Identity;

namespace GSIP.Infrastructure.Auditing;

public static class AuditIntegrityHash
{
    public static string Compute(AuthenticationAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (auditEvent.SequenceNumber is null)
            throw new InvalidOperationException("Canonical audit records require a sequence number before hashing.");

        var builder = new StringBuilder(1024);
        Append(builder, "sequence", auditEvent.SequenceNumber.Value.ToString(CultureInfo.InvariantCulture));
        Append(builder, "id", auditEvent.Id.ToString("D"));
        Append(builder, "actor", auditEvent.UserId?.ToString("D"));
        Append(builder, "action", auditEvent.EventType);
        Append(builder, "targetType", auditEvent.TargetType);
        Append(builder, "targetId", auditEvent.TargetId);
        Append(builder, "occurred", auditEvent.OccurredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Append(builder, "succeeded", auditEvent.Succeeded ? "1" : "0");
        Append(builder, "outcome", auditEvent.ResultCode);
        Append(builder, "correlation", auditEvent.CorrelationId);
        Append(builder, "request", auditEvent.RequestId);
        Append(builder, "entity", auditEvent.EntityCode);
        Append(builder, "service", auditEvent.ServiceCode);
        Append(builder, "source", auditEvent.Source);
        Append(builder, "device", auditEvent.Device);
        Append(builder, "metadata", auditEvent.MetadataJson);
        Append(builder, "retainUntil", auditEvent.RetainUntilUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Append(builder, "previousHash", auditEvent.PreviousHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string key, string? value)
    {
        value ??= string.Empty;
        builder.Append(key).Append(':')
            .Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(value).Append('\n');
    }
}
