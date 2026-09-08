using System.Globalization;
using System.Resources;

namespace GSIP.Web;

public sealed class MetadataText
{
    private static readonly ResourceManager Resources = new(
        "GSIP.Web.Resources.MetadataResource",
        typeof(MetadataResource).Assembly);

    public string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        }
    }
}
