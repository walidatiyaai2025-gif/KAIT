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
    Expect(program.Contains("AddScoped<ShellText>", StringComparison.Ordinal), "Shared shell resource facade is not registered.", failures);
    Expect(program.Contains("ar-KW", StringComparison.Ordinal) && program.Contains("\"en\"", StringComparison.Ordinal), "Arabic/English supported cultures are missing.", failures);

    var shellText = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "ShellText.cs"));
    Expect(shellText.Contains("GSIP.Web.Resources.ShellResource", StringComparison.Ordinal), "Shared shell ResourceManager must bind the exact embedded resource base name.", failures);
    Expect(shellText.Contains("CultureInfo.CurrentUICulture", StringComparison.Ordinal), "Shared shell resource lookup must use the request UI culture.", failures);

    var layout = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Views", "Shared", "_Layout.cshtml"));
    Expect(layout.Contains("dir=\"@direction\"", StringComparison.Ordinal), "Layout must set real RTL/LTR direction.", failures);
    Expect(layout.Contains("data-culture-name=\"@CultureInfo.CurrentUICulture.Name\"", StringComparison.Ordinal), "Layout must expose the exact selected culture for runtime evidence.", failures);
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

    var dashboard = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Views", "Shell", "Index.cshtml"));
    Expect(dashboard.Contains("v@(Model.ProductVersion)", StringComparison.Ordinal), "Dashboard version must use an explicit Razor expression instead of leaking model syntax.", failures);
}

static void CheckPhaseBoundaries(string root, List<string> failures)
{
    var login = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Views", "Shell", "Login.cshtml"));
    Expect(login.Contains("data-auth-state=\"shell-only\"", StringComparison.Ordinal), "Login must retain the closed-P01 shell boundary until a later phase deliberately replaces it.", failures);
    Expect(login.Contains("type=\"button\" disabled", StringComparison.Ordinal), "Closed-P01 regression gate requires the login shell to remain non-submitting until later-phase implementation replaces it.", failures);

    var currentPhase = File.ReadAllText(Path.Combine(root, "CURRENT_PHASE.md"));
    var ledger = File.ReadAllText(Path.Combine(root, "docs", "TASK_LEDGER.md"));
    var p01StillCurrent = currentPhase.Contains("P01 — Solution architecture and bilingual shell", StringComparison.Ordinal);
    var laterCanonicalPhase = Enumerable.Range(2, 16).Any(number => currentPhase.Contains($"P{number:00} —", StringComparison.Ordinal));
    var p01Closed = ledger.Contains("| P01 | CLOSED |", StringComparison.Ordinal);

    Expect(p01StillCurrent || (laterCanonicalPhase && p01Closed), "P01 regression gate requires either active P01 or a later canonical phase with P01 CLOSED in the ledger.", failures);
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}
