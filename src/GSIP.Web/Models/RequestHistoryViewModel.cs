using GSIP.Application.Execution;

namespace GSIP.Web.Models;

public sealed class RequestHistoryViewModel
{
    public required RequestHistoryPage Page { get; init; }
    public required RequestHistoryFilter Filter { get; init; }
    public bool CanViewOwn { get; init; }
    public bool CanViewDepartment { get; init; }
    public bool CanViewAll { get; init; }
    public bool CanExport { get; init; }
    public bool IsArabic { get; init; }

    public string Text(string en, string ar) => IsArabic ? ar : en;
}
