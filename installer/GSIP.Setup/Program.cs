using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;

namespace GSIP.Setup;

internal static class Program
{
    private const string PayloadResourceName = "GSIP.Payload.zip";
    private const string ProductName = "Government Services Integration Portal";
    private const string ProductArchitecture = "win-x64";
    private const string InstallManifestName = "install-manifest.json";
    private const string DefaultSiteName = "GSIP";
    private const string DefaultAppPoolName = "GSIP";
    private const int DefaultPort = 8080;
    private static readonly string[] PreservedStateSegments = ["App_Data"];

    public static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new InvalidOperationException("GSIP Setup is supported only on Windows Server/Windows x64.");

            var options = SetupOptions.Parse(args);
            Console.WriteLine($"GSIP Setup {ProductVersion} - action={options.Action}");

            return options.Action switch
            {
                SetupAction.Install or SetupAction.Repair => InstallOrRepair(options),
                SetupAction.Uninstall => Uninstall(options),
                _ => throw new InvalidOperationException("Unsupported setup action.")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"GSIP_SETUP_FAILED: {exception.Message}");
            return 1;
        }
    }

    private static int InstallOrRepair(SetupOptions options)
    {
        if (!options.SkipIis)
        {
            RequireAdministrator();
            ValidateIisPrerequisites();
        }

        var installRoot = ValidateInstallRoot(options.InstallRoot);
        var ownership = ReadInstallOwnership(installRoot);
        ValidateInstallOwnershipForInstall(options, installRoot, ownership);
        var originalManifestText = ownership is null
            ? null
            : File.ReadAllText(GetInstallManifestPath(installRoot));

        if (!options.SkipIis && ownership is null)
        {
            if (SiteExists(options.SiteName))
                throw new InvalidOperationException("Existing IIS site is not owned by this GSIP installation. Choose a different site name or uninstall the existing application explicitly.");
            if (AppPoolExists(options.AppPoolName))
                throw new InvalidOperationException("Existing IIS application pool is not owned by this GSIP installation. Choose a different application-pool name.");
        }

        var appDirectory = Path.Combine(installRoot, "app");
        var appDataDirectory = Path.Combine(appDirectory, "App_Data");
        var stageDirectory = Path.Combine(Path.GetTempPath(), $"gsip-stage-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(installRoot, $".backup-{Guid.NewGuid():N}");

        Directory.CreateDirectory(stageDirectory);

        var appPoolWasRunning = false;
        try
        {
            ExtractEmbeddedPayload(stageDirectory);
            ValidateStagedPayload(stageDirectory);

            Directory.CreateDirectory(installRoot);
            Directory.CreateDirectory(appDirectory);
            Directory.CreateDirectory(appDataDirectory);

            if (!options.SkipIis)
            {
                appPoolWasRunning = AppPoolExists(options.AppPoolName) && IsAppPoolStarted(options.AppPoolName);
                StopAppPoolIfPresent(options.AppPoolName);
            }

            BackupReplaceableApplicationFiles(appDirectory, backupDirectory);
            CopyDirectory(stageDirectory, appDirectory);

            // Materialize the ownership marker before IIS mutation. If a later IIS step fails,
            // a new installation remains attributable to GSIP; existing maintenance restores its
            // prior marker together with the prior binaries.
            WriteInstallManifest(installRoot, options);

            if (!options.SkipIis)
            {
                ConfigureIis(options, appDirectory, appDataDirectory);
                StartAppPool(options.AppPoolName);
                StartSite(options.SiteName);
                RegisterMaintenanceEntry(installRoot, options);
            }

            DeleteDirectoryIfExists(backupDirectory);
            Console.WriteLine($"GSIP_SETUP_SUCCESS: {options.Action} version={ProductVersion} root={installRoot}");
            return 0;
        }
        catch
        {
            RollBackApplicationFiles(appDirectory, backupDirectory);
            if (originalManifestText is not null)
                WriteInstallManifestText(GetInstallManifestPath(installRoot), originalManifestText);
            else if (ownership is null && !File.Exists(GetInstallManifestPath(installRoot)))
                DeleteDirectoryIfExists(installRoot);
            if (!options.SkipIis && appPoolWasRunning)
                TryRunAppCmd("start", "apppool", $"/apppool.name:{options.AppPoolName}");
            throw;
        }
        finally
        {
            DeleteDirectoryIfExists(stageDirectory);
        }
    }

    private static int Uninstall(SetupOptions options)
    {
        if (!options.SkipIis)
            RequireAdministrator();

        var installRoot = ValidateInstallRoot(options.InstallRoot);
        var ownership = ReadInstallOwnership(installRoot)
            ?? throw new InvalidOperationException("Uninstall requires a valid GSIP ownership manifest at the requested install root.");
        RequireOwnershipMatch(options, ownership);
        var appDirectory = Path.Combine(installRoot, "app");

        if (!options.SkipIis)
        {
            StopSiteIfPresent(ownership.SiteName);
            StopAppPoolIfPresent(ownership.AppPoolName);
            DeleteSiteIfPresent(ownership.SiteName);
            DeleteAppPoolIfPresent(ownership.AppPoolName);
            RemoveMaintenanceEntry();
        }

        if (options.PurgeState)
        {
            DeleteDirectoryIfExists(installRoot);
            Console.WriteLine("GSIP_SETUP_SUCCESS: uninstall with explicit state purge.");
            return 0;
        }

        RemoveReplaceableApplicationFiles(appDirectory);
        Console.WriteLine($"GSIP_SETUP_SUCCESS: uninstall; protected mutable state and ownership manifest preserved at {installRoot}");
        return 0;
    }

    private static void ExtractEmbeddedPayload(string destination)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
            ?? throw new InvalidOperationException("Embedded GSIP application payload is missing.");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false);
        var destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => PreservedStateSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Application payload must never contain mutable App_Data state.");

            var target = Path.GetFullPath(Path.Combine(destination, normalized));
            if (!target.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Application payload contains an unsafe path.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static void ValidateStagedPayload(string stageDirectory)
    {
        var required = new[] { "GSIP.Web.dll", "web.config" };
        foreach (var file in required)
        {
            if (!File.Exists(Path.Combine(stageDirectory, file)))
                throw new InvalidOperationException($"Published application payload is missing required file: {file}");
        }

        if (Directory.Exists(Path.Combine(stageDirectory, "App_Data")))
            throw new InvalidOperationException("Published payload contains App_Data and could overwrite protected runtime state.");
    }

    private static void BackupReplaceableApplicationFiles(string appDirectory, string backupDirectory)
    {
        Directory.CreateDirectory(backupDirectory);
        foreach (var path in Directory.EnumerateFileSystemEntries(appDirectory))
        {
            if (string.Equals(Path.GetFileName(path), "App_Data", StringComparison.OrdinalIgnoreCase))
                continue;
            var destination = Path.Combine(backupDirectory, Path.GetFileName(path));
            if (Directory.Exists(path)) Directory.Move(path, destination);
            else File.Move(path, destination, overwrite: true);
        }
    }

    private static void RollBackApplicationFiles(string appDirectory, string backupDirectory)
    {
        try
        {
            RemoveReplaceableApplicationFiles(appDirectory);
            if (!Directory.Exists(backupDirectory)) return;
            foreach (var path in Directory.EnumerateFileSystemEntries(backupDirectory))
            {
                var destination = Path.Combine(appDirectory, Path.GetFileName(path));
                if (Directory.Exists(path)) Directory.Move(path, destination);
                else File.Move(path, destination, overwrite: true);
            }
        }
        catch (Exception rollbackException)
        {
            Console.Error.WriteLine($"GSIP_SETUP_ROLLBACK_WARNING: {rollbackException.Message}");
        }
    }

    private static void RemoveReplaceableApplicationFiles(string appDirectory)
    {
        if (!Directory.Exists(appDirectory)) return;
        foreach (var path in Directory.EnumerateFileSystemEntries(appDirectory))
        {
            if (string.Equals(Path.GetFileName(path), "App_Data", StringComparison.OrdinalIgnoreCase))
                continue;
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void ConfigureIis(SetupOptions options, string appDirectory, string appDataDirectory)
    {
        if (!AppPoolExists(options.AppPoolName))
            RunAppCmd("add", "apppool", $"/name:{options.AppPoolName}");

        RunAppCmd("set", "apppool", $"{options.AppPoolName}", "/managedRuntimeVersion:", "/startMode:AlwaysRunning", "/processModel.identityType:ApplicationPoolIdentity");

        if (!SiteExists(options.SiteName))
            RunAppCmd("add", "site", $"/name:{options.SiteName}", $"/bindings:http/*:{options.Port}:", $"/physicalPath:{appDirectory}");
        else
            RunAppCmd("set", "vdir", $"{options.SiteName}/", $"/physicalPath:{appDirectory}");

        RunAppCmd("set", "app", $"{options.SiteName}/", $"/applicationPool:{options.AppPoolName}");
        GrantAppPoolStateAccess(appDataDirectory, options.AppPoolName);
    }

    private static void ValidateIisPrerequisites()
    {
        var appCmd = AppCmdPath;
        if (!File.Exists(appCmd))
            throw new InvalidOperationException("IIS is not installed. Enable the IIS Web Server role before running GSIP Setup.");

        var moduleCandidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS", "Asp.Net Core Module", "V2", "aspnetcorev2.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS", "Asp.Net Core Module", "V2", "aspnetcorev2_inprocess.dll")
        };
        if (!moduleCandidates.Any(File.Exists))
            throw new InvalidOperationException("ASP.NET Core Hosting Bundle / IIS AspNetCoreModuleV2 is missing. Install the supported .NET 10 Hosting Bundle first.");
    }

    private static void GrantAppPoolStateAccess(string appDataDirectory, string appPoolName)
    {
        Directory.CreateDirectory(appDataDirectory);
        RunProcess("icacls.exe", appDataDirectory, "/inheritance:e", "/grant", $"IIS AppPool\\{appPoolName}:(OI)(CI)M", "/T", "/C");
    }

    private static void RequireAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Administrator elevation is required for IIS installation and maintenance.");
    }

    private static bool AppPoolExists(string appPoolName) => AppCmdExists("list", "apppool", $"/name:{appPoolName}");
    private static bool SiteExists(string siteName) => AppCmdExists("list", "site", $"/name:{siteName}");
    private static bool IsAppPoolStarted(string appPoolName) => RunAppCmdCapture("list", "apppool", $"/name:{appPoolName}").Contains("state:Started", StringComparison.OrdinalIgnoreCase);
    private static void StopAppPoolIfPresent(string name) { if (AppPoolExists(name)) TryRunAppCmd("stop", "apppool", $"/apppool.name:{name}"); }
    private static void StartAppPool(string name) => RunAppCmd("start", "apppool", $"/apppool.name:{name}");
    private static void DeleteAppPoolIfPresent(string name) { if (AppPoolExists(name)) RunAppCmd("delete", "apppool", name); }
    private static void StopSiteIfPresent(string name) { if (SiteExists(name)) TryRunAppCmd("stop", "site", $"/site.name:{name}"); }
    private static void StartSite(string name) => RunAppCmd("start", "site", $"/site.name:{name}");
    private static void DeleteSiteIfPresent(string name) { if (SiteExists(name)) RunAppCmd("delete", "site", name); }

    private static bool AppCmdExists(params string[] args)
    {
        var output = RunAppCmdCapture(args, throwOnFailure: false, out var exitCode);
        return exitCode == 0 && !string.IsNullOrWhiteSpace(output);
    }

    private static void RunAppCmd(params string[] args) => RunProcess(AppCmdPath, args);
    private static void TryRunAppCmd(params string[] args) { _ = RunAppCmdCapture(args, throwOnFailure: false, out _); }
    private static string RunAppCmdCapture(params string[] args) => RunAppCmdCapture(args, throwOnFailure: true, out _);

    private static string RunAppCmdCapture(string[] args, bool throwOnFailure, out int exitCode)
    {
        var result = RunProcessCapture(AppCmdPath, args);
        exitCode = result.ExitCode;
        if (throwOnFailure && exitCode != 0)
            throw new InvalidOperationException($"IIS configuration command failed with exit code {exitCode}.");
        return result.Output;
    }

    private static void RunProcess(string fileName, params string[] args)
    {
        var result = RunProcessCapture(fileName, args);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Required setup command failed with exit code {result.ExitCode}: {Path.GetFileName(fileName)}");
    }

    private static (int ExitCode, string Output) RunProcessCapture(string fileName, params string[] args)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {Path.GetFileName(fileName)}.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, string.IsNullOrWhiteSpace(output) ? error : output);
    }

    private static void WriteInstallManifest(string installRoot, SetupOptions options)
    {
        var manifest = new
        {
            Product = ProductName,
            Version = ProductVersion,
            Architecture = ProductArchitecture,
            options.SiteName,
            options.AppPoolName,
            options.Port,
            MutableState = @"app\App_Data",
            DataProtectionKeys = @"app\App_Data\keys",
            SetupState = @"app\App_Data\setup\completed.protected",
            WrittenAtUtc = DateTimeOffset.UtcNow
        };
        WriteInstallManifestText(
            GetInstallManifestPath(installRoot),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteInstallManifestText(string manifestPath, string content)
    {
        var temporaryPath = $"{manifestPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static InstallOwnership? ReadInstallOwnership(string installRoot)
    {
        var manifestPath = GetInstallManifestPath(installRoot);
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            if (!TryReadRequiredString(root, "Product", out var product) || !string.Equals(product, ProductName, StringComparison.Ordinal) ||
                !TryReadRequiredString(root, "Architecture", out var architecture) || !string.Equals(architecture, ProductArchitecture, StringComparison.OrdinalIgnoreCase) ||
                !TryReadRequiredString(root, "Version", out _) ||
                !TryReadRequiredString(root, "SiteName", out var siteName) ||
                !TryReadRequiredString(root, "AppPoolName", out var appPoolName))
                throw new InvalidOperationException("Install root contains an invalid or incompatible GSIP ownership manifest.");

            return new InstallOwnership(siteName, appPoolName);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Install root contains a corrupt GSIP ownership manifest.", exception);
        }
    }

    private static bool TryReadRequiredString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static void ValidateInstallOwnershipForInstall(SetupOptions options, string installRoot, InstallOwnership? ownership)
    {
        if (ownership is not null)
        {
            RequireOwnershipMatch(options, ownership);
            return;
        }

        if (options.Action == SetupAction.Repair)
            throw new InvalidOperationException("Repair requires a valid GSIP ownership manifest at the requested install root.");

        if (Directory.Exists(installRoot) && Directory.EnumerateFileSystemEntries(installRoot).Any())
            throw new InvalidOperationException("Install root is not empty and is not owned by GSIP. Choose an empty directory or the existing GSIP installation root.");
    }

    private static void RequireOwnershipMatch(SetupOptions options, InstallOwnership ownership)
    {
        if (!string.Equals(options.SiteName, ownership.SiteName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(options.AppPoolName, ownership.AppPoolName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Requested IIS site/application-pool identity does not match the GSIP ownership manifest.");
    }

    private static string GetInstallManifestPath(string installRoot) => Path.Combine(installRoot, InstallManifestName);

    private static void RegisterMaintenanceEntry(string installRoot, SetupOptions options)
    {
        var currentSetup = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentSetup) || !File.Exists(currentSetup)) return;
        var maintenancePath = Path.Combine(installRoot, "GSIP-Setup-x64.exe");
        if (!string.Equals(Path.GetFullPath(currentSetup), Path.GetFullPath(maintenancePath), StringComparison.OrdinalIgnoreCase))
            File.Copy(currentSetup, maintenancePath, overwrite: true);

        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GSIP", writable: true);
        key?.SetValue("DisplayName", ProductName);
        key?.SetValue("DisplayVersion", ProductVersion);
        key?.SetValue("Publisher", "GSIP");
        key?.SetValue("InstallLocation", installRoot);
        key?.SetValue("UninstallString", $"\"{maintenancePath}\" uninstall --install-root \"{installRoot}\" --site-name \"{options.SiteName}\" --app-pool \"{options.AppPoolName}\"");
        key?.SetValue("ModifyPath", $"\"{maintenancePath}\" repair --install-root \"{installRoot}\" --site-name \"{options.SiteName}\" --app-pool \"{options.AppPoolName}\" --port {options.Port}");
        key?.SetValue("NoModify", 0, RegistryValueKind.DWord);
        key?.SetValue("NoRepair", 0, RegistryValueKind.DWord);
    }

    private static void RemoveMaintenanceEntry() => Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GSIP", throwOnMissingSubKey: false);

    private static string ValidateInstallRoot(string requested)
    {
        var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(requested));
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root) || string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Install root cannot be a drive root.");
        return full.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static string AppCmdPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "inetsrv", "appcmd.exe");
    private static string ProductVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);
            return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        }
    }

    private enum SetupAction { Install, Repair, Uninstall }

    private sealed record InstallOwnership(string SiteName, string AppPoolName);

    private sealed record SetupOptions(SetupAction Action, string InstallRoot, string SiteName, string AppPoolName, int Port, bool SkipIis, bool PurgeState)
    {
        public static SetupOptions Parse(string[] args)
        {
            var index = 0;
            var action = SetupAction.Install;
            if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
            {
                action = args[0].ToLowerInvariant() switch
                {
                    "install" => SetupAction.Install,
                    "repair" => SetupAction.Repair,
                    "uninstall" => SetupAction.Uninstall,
                    _ => throw new ArgumentException("First argument must be install, repair, or uninstall.")
                };
                index = 1;
            }

            var installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GSIP");
            var siteName = DefaultSiteName;
            var appPoolName = DefaultAppPoolName;
            var port = DefaultPort;
            var skipIis = false;
            var purgeState = false;

            while (index < args.Length)
            {
                var arg = args[index++];
                string NextValue()
                {
                    if (index >= args.Length) throw new ArgumentException($"Missing value for {arg}.");
                    return args[index++];
                }

                switch (arg.ToLowerInvariant())
                {
                    case "--install-root": installRoot = NextValue(); break;
                    case "--site-name": siteName = ValidateName(NextValue(), "site name"); break;
                    case "--app-pool": appPoolName = ValidateName(NextValue(), "application pool name"); break;
                    case "--port":
                        if (!int.TryParse(NextValue(), out port) || port is < 1 or > 65535) throw new ArgumentException("Port must be 1-65535.");
                        break;
                    case "--skip-iis": skipIis = true; break;
                    case "--purge-state": purgeState = true; break;
                    default: throw new ArgumentException($"Unknown option: {arg}");
                }
            }

            if (purgeState && action != SetupAction.Uninstall)
                throw new ArgumentException("--purge-state is valid only for uninstall.");
            return new SetupOptions(action, installRoot, siteName, appPoolName, port, skipIis, purgeState);
        }

        private static string ValidateName(string value, string label)
        {
            var normalized = value.Trim();
            if (normalized.Length is < 1 or > 80 || normalized.Any(c => char.IsControl(c) || c is '"' or '\''))
                throw new ArgumentException($"Invalid {label}.");
            return normalized;
        }
    }
}
