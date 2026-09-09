using System.ComponentModel.DataAnnotations;
using GSIP.Infrastructure.Execution;
using Microsoft.AspNetCore.Identity;

namespace GSIP.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    [MaxLength(100)] public string? DepartmentCode { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsPrivileged { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public ICollection<RequestExecutionRecord> RequestExecutionRecords { get; set; } = new List<RequestExecutionRecord>();
}

public sealed class AuthenticationAuditEvent
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string ResultCode { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }

    // P11 canonical fields are nullable for pre-P11 identity audit compatibility.
    public long? SequenceNumber { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? RequestId { get; set; }
    public string? EntityCode { get; set; }
    public string? ServiceCode { get; set; }
    public string? Source { get; set; }
    public string? Device { get; set; }
    public string? MetadataJson { get; set; }
    public string? PreviousHash { get; set; }
    public string? RecordHash { get; set; }
    public DateTimeOffset? RetainUntilUtc { get; set; }
}

public sealed class AuditChainState
{
    public int Id { get; set; }
    public long LastSequence { get; set; }
    public string LastHash { get; set; } = string.Empty;
    public long CheckpointSequence { get; set; }
    public string CheckpointHash { get; set; } = string.Empty;
    public DateTimeOffset? LastVerifiedAtUtc { get; set; }
    public string LastIntegrityStatus { get; set; } = "Unknown";
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
