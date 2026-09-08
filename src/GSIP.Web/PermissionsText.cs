using System.Globalization;
using System.Resources;

namespace GSIP.Web;

public sealed class PermissionsText
{
    private static readonly ResourceManager Resources = new(
        "GSIP.Web.Resources.PermissionsResource",
        typeof(PermissionsResource).Assembly);

    public string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        }
    }

    public string Permission(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        var resourceKey = "Permission_" + permissionKey.Replace('.', '_');
        var localized = this[resourceKey];
        return string.Equals(localized, resourceKey, StringComparison.Ordinal) ? permissionKey : localized;
    }

    public string Role(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        var resourceKey = roleName switch
        {
            "System Administrator" => "Role_SystemAdministrator",
            "Integration Manager" => "Role_IntegrationManager",
            "Service Operator" => "Role_ServiceOperator",
            "Auditor" => "Role_Auditor",
            "Read Only" => "Role_ReadOnly",
            _ => string.Empty
        };
        return string.IsNullOrEmpty(resourceKey) ? roleName : this[resourceKey];
    }
}
