namespace GSIP.Application.Configuration;

public sealed class PortalShellOptions
{
    public const string SectionName = "PortalShell";

    public string ProductCode { get; init; } = "GSIP";
    public string EnvironmentLabel { get; init; } = "Foundation";
    public string SupportMessage { get; init; } = "Authorized access only";
}
