using System.Xml.Linq;

var root = FindRepositoryRoot();
var viewPath = Path.Combine(root, "src", "GSIP.Web", "Views", "AuthProfiles", "Index.cshtml");
var modelPath = Path.Combine(root, "src", "GSIP.Web", "Models", "AuthProfileAdminViewModels.cs");
var cssPath = Path.Combine(root, "src", "GSIP.Web", "wwwroot", "css", "auth-profiles.css");
var enPath = Path.Combine(root, "src", "GSIP.Web", "Resources", "AuthProfileResource.resx");
var arPath = Path.Combine(root, "src", "GSIP.Web", "Resources", "AuthProfileResource.ar-KW.resx");

var view = File.ReadAllText(viewPath);
var model = File.ReadAllText(modelPath);
var css = File.ReadAllText(cssPath);
var en = XDocument.Load(enPath);
var ar = XDocument.Load(arPath);

Check(!model.Contains("SecretValue", StringComparison.Ordinal), "Presentation model cannot carry plaintext SecretValue.");
Check(!model.Contains("PlaintextValue", StringComparison.Ordinal), "Presentation model cannot carry plaintext secret aliases.");
Check(view.CountOccurrences("type=\"password\"") >= 2, "Create/replace and rotate must use password inputs.");
Check(view.CountOccurrences("autocomplete=\"new-password\"") >= 2, "Secret inputs must not be browser-recovered values.");
Check(view.CountOccurrences("@Html.AntiForgeryToken()") >= 7, "Every mutation form family, including metadata edit, must render an anti-forgery token.");
Check(view.Contains("action=\"/auth-profiles/@profile.Id\"", StringComparison.Ordinal), "Existing AuthProfile metadata must be editable.");
Check(view.Contains("metadata-edit-form", StringComparison.Ordinal), "AuthProfile metadata edit form is missing.");
Check(!view.Contains("value=\"@secret.", StringComparison.OrdinalIgnoreCase), "Secret metadata must never populate a value attribute.");
Check(!view.Contains("@secret.SecretValue", StringComparison.OrdinalIgnoreCase)
    && !view.Contains("@Model.SecretValue", StringComparison.OrdinalIgnoreCase)
    && !view.Contains(".PlaintextValue", StringComparison.OrdinalIgnoreCase),
    "Razor must not bind a plaintext secret property.");
Check(view.CountOccurrences("confirmShared") >= 2 && view.Contains("profile.IsShared || profile.BindingCount == 0", StringComparison.Ordinal), "Create/edit sharing must be explicit and isolated profiles cannot be offered for implicit reuse.");
Check(view.Contains("confirmRotation", StringComparison.Ordinal), "Rotation requires an explicit confirmation.");
Check(view.Contains("CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft", StringComparison.Ordinal), "UI must respond to RTL/LTR culture direction.");
Check(css.Contains(":focus-visible", StringComparison.Ordinal) && css.Contains("@media(max-width:480px)", StringComparison.Ordinal), "Keyboard focus and narrow mobile behavior are required.");

var enKeys = ResourceKeys(en);
var arKeys = ResourceKeys(ar);
Check(enKeys.SetEquals(arKeys), "English and Arabic localization keys must remain in parity.");
Check(ar.Descendants("value").Any(value => value.Value.Any(ch => ch is >= '\u0600' and <= '\u06FF')), "Arabic resource must contain Arabic localized content.");

Console.WriteLine("P06 admin UI safety checks: PASS");
Console.WriteLine($"Anti-forgery tokens: {view.CountOccurrences("@Html.AntiForgeryToken()")}");
Console.WriteLine($"Write-only password inputs: {view.CountOccurrences("type=\"password\"")}");
Console.WriteLine($"Localization keys: {enKeys.Count}");
return;

static HashSet<string> ResourceKeys(XDocument document) => document
    .Root!
    .Elements("data")
    .Select(element => element.Attribute("name")?.Value ?? string.Empty)
    .Where(name => name.Length > 0)
    .ToHashSet(StringComparer.Ordinal);

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src", "GSIP.Web")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Repository root was not found.");
}

static class StringExtensions
{
    public static int CountOccurrences(this string value, string fragment)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += fragment.Length;
        }
        return count;
    }
}
