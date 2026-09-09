using GSIP.Application.Auditing;

namespace GSIP.Web.Models;

public sealed class AuditDashboardViewModel
{
    public required AuditTrailPage Page { get; init; }
    public required AuditMonitoringSnapshot Monitoring { get; init; }
    public required AuditTrailFilter Filter { get; init; }
    public AuditTrailEntry? Selected { get; init; }
    public bool CanExport { get; init; }
    public bool CanVerifyIntegrity { get; init; }
    public bool IsArabic { get; init; }

    public int SuccessfulOnPage => Page.Items.Count(item => item.Succeeded);
    public int FailedOnPage => Page.Items.Count(item => !item.Succeeded);

    public IReadOnlyList<AuditVolumePoint> Volume { get; init; } = [];
}

public sealed record AuditVolumePoint(DateTimeOffset BucketUtc, int Total, int Failed);
