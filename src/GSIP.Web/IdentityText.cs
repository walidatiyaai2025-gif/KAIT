using System.Globalization;
using System.Resources;

namespace GSIP.Web;

public sealed class IdentityText
{
    private static readonly ResourceManager Resources = new(
        "GSIP.Web.Resources.IdentityResource",
        typeof(IdentityResource).Assembly);

    public string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        }
    }
}
