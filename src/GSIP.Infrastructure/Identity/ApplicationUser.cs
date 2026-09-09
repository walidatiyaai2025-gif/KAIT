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
}

