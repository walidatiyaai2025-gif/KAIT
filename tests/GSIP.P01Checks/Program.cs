using System.Xml.Linq;

var root = FindRepositoryRoot();
var failures = new List<string>();

CheckLayeredArchitecture(root, failures);
CheckLocalization(root, failures);
CheckDesignSystem(root, failures);
CheckPhaseBoundaries(root, failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine("P01 checks FAILED:");
    failures.ForEach(failure => Console.Error.WriteLine($" - {failure}"));
    return 1;
}

Console.WriteLine("P01 checks passed: architecture, localization, design tokens and phase boundaries are consistent.");
return 0;

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "GSIP.sln")) && File.Exists(Path.Combine(directory.FullName, "CURRENT_PHASE.md")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Repository root could not be located.");
}

static void CheckLayeredArchitecture(string root, List<string> failures)
{
    var projects = new[] { "GSIP.Web", "GSIP.Application", "GSIP.Domain", "GSIP.Infrastructure", "GSIP.Integrations", "GSIP.Contracts" };
    var solution = File.ReadAllText(Path.Combine(root, "GSIP.sln"));
    foreach (var project in projects)
    {
        Expect(solution.Contains($"{project}.csproj", StringComparison.Ordinal), $"Solution is missing {project}.", failures);
    }

    var expectedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["GSIP.Domain"] = [],
        ["GSIP.Contracts"] = [],
        ["GSIP.Application"] = ["GSIP.Contracts", "GSIP.Domain"],
        ["GSIP.Infrastructure"] = ["GSIP.Application", "GSIP.Domain"],
        ["GSIP.Integrations"] = ["GSIP.Application", "GSIP.Contracts"],
        ["GSIP.Web"] = ["GSIP.Application", "GSIP.Contracts", "GSIP.Infrastructure", "GSIP.Integrations"]
    };

    foreach (var (project, expected) in expectedReferences)
    {
        var csproj = XDocument.Load(Path.Combine(root, "src", project, $"{project}.csproj"));
        var actual = csproj.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
        var sortedExpected = expected.Order(StringComparer.Ordinal).ToArray();
        Expect(actual.SequenceEqual(sortedExpected, StringComparer.Ordinal), $"{project} project references violate the P01 dependency direction. Expected [{string.Join(", ", sortedExpected)}], actual [{string.Join(", ", actual)}].", failures);
    }

    var dataSession = File.ReadAllText(Path.Combine(root, "src", "GSIP.Application", "Abstractions", "IDataSession.cs"));
    Expect(dataSession.Contains("SaveChangesAsync", StringComparison.Ordinal), "EF-compatible persistence abstraction is missing.", failures);
}

static void CheckLocalization(string root, List<string> failures)
{
    var program = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Program.cs"));
    Expect(program.Contains("AddLocalization", StringComparison.Ordinal), "Resource-based localization is not registered.", failures);
    Expect(program.Contains("ar-KW", StringComparison.Ordinal) && program.Contains("\"en\"", StringComparison.Ordinal), "Arabic/English supported cultures are missing.", failures);

    var layout = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Views", "Shared", "_Layout.cshtml"));
    Expect(layout.Contains("dir=\"@direction\"", StringComparison.Ordinal), "Layout must set real RTL/LTR direction.", failures);
    Expect(layout.Contains("data-culture", StringComparison.Ordinal), "Language switch must preserve the current URL state.", failures);

    foreach (var resource in new[] { "ShellResource.resx", "ShellResource.ar-KW.resx" })
    {
        var document = XDocument.Load(Path.Combine(root, "src", "GSIP.Web", "Resources", resource));
        var names = document.Descendants("data").Select(element => element.Attribute("name")?.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var key in new[] { "ProductName", "Welcome", "Login", "Entities", "Services", "Audit", "Permissions" })
        {
            Expect(names.Contains(key), $"{resource} is missing required key {key}.", failures);
        }
    }
}

static void CheckDesignSystem(string root, List<string> failures)
{
    var css = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "wwwroot", "css", "gsip.css"));
    foreach (var token in new[] { "#07345d", "#0b4f7d", "#16a6d9", "#c99a2e", "#f4f8fc", "#dbe6f0" })
    {
        Expect(css.Contains(token, StringComparison.OrdinalIgnoreCase), $"Design token {token} from the mandatory SVG baselines is missing.", failures);
    }
    foreach (var component in new[] { ".topbar", ".sidebar", ".kpi-card", ".workspace-card", ".status-badge", ".input-shell" })
    {
        Expect(css.Contains(component, StringComparison.Ordinal), $"Design-system component {component} is missing.", failures);
    }
}

static void CheckPhaseBoundaries(string root, List<string> failures)
{
    var login = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Views", "Shell", "Login.cshtml"));
    Expect(login.Contains("data-auth-state=\"shell-only\"", StringComparison.Ordinal), "Login must be explicitly marked shell-only in P01.", failures);
    Expect(login.Contains("type=\"button\" disabled", StringComparison.Ordinal), "P01 login must not submit credentials.", failures);

    var currentPhase = File.ReadAllText(Path.Combine(root, "CURRENT_PHASE.md"));
    Expect(currentPhase.Contains("P01 — Solution architecture and bilingual shell", StringComparison.Ordinal), "Canonical current phase must remain P01 while implementation is unclosed.", failures);
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}
