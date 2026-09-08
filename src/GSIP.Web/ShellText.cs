using Microsoft.Extensions.Localization;

namespace GSIP.Web;

/// <summary>
/// Explicit resource facade for the shared P01 shell. The localizer factory is given the
/// resource base name and assembly deliberately so runtime lookup cannot depend on inferred
/// namespace/path conventions.
/// </summary>
public sealed class ShellText
{
    private readonly IStringLocalizer _localizer;

    public ShellText(IStringLocalizerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var assemblyName = typeof(ShellResource).Assembly.GetName().Name
            ?? throw new InvalidOperationException("GSIP.Web assembly name is unavailable.");
        _localizer = factory.Create("ShellResource", assemblyName);
    }

    public LocalizedString this[string name] => _localizer[name];
}
