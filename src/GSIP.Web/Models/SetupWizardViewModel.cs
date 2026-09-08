using GSIP.Application.Setup;

namespace GSIP.Web.Models;

public sealed class SetupWizardViewModel
{
    public SetupStep Step { get; init; }
    public SetupDraft Draft { get; init; } = new();
    public SetupOperationResult? Operation { get; init; }
    public IReadOnlyList<string> PreflightChecks { get; init; } = [];
    public int ProgressPercent => (int)Math.Round(((int)Step / (double)(int)SetupStep.Finish) * 100, MidpointRounding.AwayFromZero);
    public string Culture => Draft.Culture;
}
