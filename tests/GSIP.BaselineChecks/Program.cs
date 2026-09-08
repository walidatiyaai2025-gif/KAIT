using System.Text.Json;
using System.Xml.Linq;

var root = FindRepositoryRoot();
var failures = new List<string>();

CheckGlobalJson(root, failures);
CheckBuildProps(root, failures);
CheckSolution(root, failures);
CheckSecurityBaseline(root, failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine("P00 baseline checks FAILED:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine("P00 baseline checks passed: SDK/runtime, versioning, solution and secret-safety baseline are consistent.");
return 0;

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
            File.Exists(Path.Combine(directory.FullName, "PROJECT_CONTROL.md")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root could not be located.");
}

static void CheckGlobalJson(string root, List<string> failures)
{
    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
    var sdk = document.RootElement.GetProperty("sdk");
    Expect(sdk.GetProperty("version").GetString() == "10.0.400", "global.json must pin SDK 10.0.400.", failures);
    Expect(sdk.GetProperty("rollForward").GetString() == "latestPatch", "global.json rollForward must be latestPatch.", failures);
    Expect(!sdk.GetProperty("allowPrerelease").GetBoolean(), "Preview SDKs must remain disabled.", failures);
}

static void CheckBuildProps(string root, List<string> failures)
{
    var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
    string? Value(string name) => props.Descendants(name).Select(element => element.Value).FirstOrDefault();

    Expect(Value("TargetFramework") == "net10.0", "TargetFramework must be net10.0.", failures);
    Expect(Value("VersionPrefix") == "0.1.0", "Initial semantic version must be 0.1.0.", failures);
    Expect(Value("Nullable") == "enable", "Nullable reference types must be enabled.", failures);
    Expect(Value("TreatWarningsAsErrors") == "true", "Compiler warnings must fail CI.", failures);
}

static void CheckSolution(string root, List<string> failures)
{
    var solution = File.ReadAllText(Path.Combine(root, "GSIP.sln"));
    Expect(solution.Contains("GSIP.Web", StringComparison.Ordinal), "GSIP.sln must contain GSIP.Web.", failures);
    Expect(solution.Contains("GSIP.BaselineChecks", StringComparison.Ordinal), "GSIP.sln must contain the P00 executable checks.", failures);

    var webProject = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "GSIP.Web.csproj"));
    Expect(webProject.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal), "GSIP.Web must use the ASP.NET Core Web SDK.", failures);
}

static void CheckSecurityBaseline(string root, List<string> failures)
{
    var prohibitedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pfx", ".p12", ".key"
    };

    foreach (var topLevel in new[] { "src", "tests" })
    {
        var directory = Path.Combine(root, topLevel);
        if (!Directory.Exists(directory))
        {
            continue;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (prohibitedExtensions.Contains(Path.GetExtension(file)))
            {
                failures.Add($"Private-key material is prohibited: {Path.GetRelativePath(root, file)}");
                continue;
            }

            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".json" or ".xml" or ".config"))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            Expect(!text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal), $"Private key marker found in {Path.GetRelativePath(root, file)}.", failures);
            Expect(!text.Contains("Bearer eyJ", StringComparison.Ordinal), $"JWT-like bearer token found in {Path.GetRelativePath(root, file)}.", failures);
        }
    }
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}
