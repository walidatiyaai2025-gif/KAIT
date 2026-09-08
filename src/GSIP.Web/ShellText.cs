using System.Globalization;
using System.Resources;

namespace GSIP.Web;

/// <summary>
/// Deterministic resource facade for the shared bilingual P01 shell.
/// The manifest base name matches Resources/ShellResource*.resx explicitly,
/// while RequestLocalization controls CurrentUICulture per request.
/// </summary>
public sealed class ShellText
{
    private static readonly ResourceManager Resources = new(
        "GSIP.Web.Resources.ShellResource",
        typeof(ShellResource).Assembly);

    public string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        }
    }
}
