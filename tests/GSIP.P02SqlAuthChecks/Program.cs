using System.Reflection;
using GSIP.Application.Setup;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var failures = new List<string>();
var server = RequireEnvironment("GSIP_P02_SQL_SERVER");
var username = RequireEnvironment("GSIP_P02_SQL_USERNAME");
var password = RequireEnvironment("GSIP_P02_SQL_PASSWORD");

var temporaryRoot = Path.Combine(Path.GetTempPath(), "gsip-p02-sql-auth-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryRoot);
var keys = Path.Combine(temporaryRoot, "keys");
Directory.CreateDirectory(keys);

try
{
    var environment = new TestHostEnvironment
    {
        ApplicationName = "GSIP.P02SqlAuthChecks",
        EnvironmentName = Environments.Development,
        ContentRootPath = temporaryRoot,
        ContentRootFileProvider = new PhysicalFileProvider(temporaryRoot)
    };
    var protection = DataProtectionProvider.Create(new DirectoryInfo(keys), options => options.SetApplicationName("GSIP-P02-SQL-Auth-Checks"));
    var service = new SetupService(protection, new PasswordHasher<BootstrapAdministrator>(), environment);

    var database = new DatabaseSetupOptions
    {
        Server = server,
        DatabaseName = "GSIP_P02_SQL_AUTH",
        UseWindowsAuthentication = true,
        Username = username,
        Password = password,
        Encrypt = true,
        TrustServerCertificate = true,
        TimeoutSeconds = 5,
        CreateDatabase = false
    };

    var binding = DatabaseAuthenticationBinding.Apply(DatabaseAuthenticationBinding.SqlMode, database);
    Expect(binding.Success && !database.UseWindowsAuthentication, "Explicit SQL Authentication mode did not remain SQL Authentication.", failures);
    Expect(database.Username == username && database.Password == password, "SQL Authentication binding did not preserve transient credentials.", failures);

    var buildConnectionString = typeof(SetupService).GetMethod("BuildConnectionString", BindingFlags.NonPublic | BindingFlags.Static);
    Expect(buildConnectionString is not null, "Canonical SetupService connection-string builder was not found.", failures);
    if (buildConnectionString is not null)
    {
        var transientConnectionString = buildConnectionString.Invoke(null, [database, "master"]) as string;
        Expect(!string.IsNullOrWhiteSpace(transientConnectionString), "Canonical SetupService did not build the transient SQL connection string.", failures);
        if (!string.IsNullOrWhiteSpace(transientConnectionString))
        {
            var parsed = new SqlConnectionStringBuilder(transientConnectionString);
            Expect(string.Equals(parsed.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase), "Test Connection is not targeted at master.", failures);
            Expect(!parsed.IntegratedSecurity, "SQL Authentication connection string unexpectedly enabled Integrated Security.", failures);
            Expect(string.Equals(parsed.UserID, username, StringComparison.Ordinal), "SQL username did not reach SqlConnectionStringBuilder.", failures);
            Expect(string.Equals(parsed.Password, password, StringComparison.Ordinal), "SQL password did not reach SqlConnectionStringBuilder transiently.", failures);
            Expect(parsed.Encrypt, "Encrypt=true was not preserved in the canonical SQL connection string.", failures);
            Expect(parsed.TrustServerCertificate, "TrustServerCertificate=true was not preserved in the canonical SQL connection string.", failures);
        }
    }

    SetupOperationResult? successfulConnection = null;
    for (var attempt = 0; attempt < 30; attempt++)
    {
        successfulConnection = await service.TestDatabaseAsync(database);
        if (successfulConnection.Success)
        {
            break;
        }
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    Expect(successfulConnection?.Success == true && successfulConnection.Code == "SQL_CONNECTION_OK",
        $"Synthetic/local SQL Authentication did not succeed. Sanitized result: {successfulConnection?.Code ?? "NO_RESULT"}.", failures);

    var wrongCredentials = new DatabaseSetupOptions
    {
        Server = server,
        DatabaseName = database.DatabaseName,
        UseWindowsAuthentication = false,
        Username = username,
        Password = password + "-wrong",
        Encrypt = database.Encrypt,
        TrustServerCertificate = database.TrustServerCertificate,
        TimeoutSeconds = database.TimeoutSeconds,
        CreateDatabase = false
    };
    var wrongResult = await service.TestDatabaseAsync(wrongCredentials);
    Expect(!wrongResult.Success && wrongResult.Code == "SQL_AUTHENTICATION_FAILED",
        $"Wrong SQL credentials were not classified as authentication failure. Sanitized result: {wrongResult.Code}.", failures);
    Expect(!wrongResult.Message.Contains(username, StringComparison.Ordinal)
        && !wrongResult.Message.Contains(password, StringComparison.Ordinal),
        "Sanitized SQL diagnostic leaked a runtime credential.", failures);

    var draft = new SetupDraft { Database = database };
    await service.SaveDraftAsync(draft);
    var protectedDraftPath = Path.Combine(temporaryRoot, "App_Data", "setup", "draft.protected");
    var protectedDraft = await File.ReadAllTextAsync(protectedDraftPath);
    Expect(!protectedDraft.Contains(username, StringComparison.Ordinal)
        && !protectedDraft.Contains(password, StringComparison.Ordinal)
        && !protectedDraft.Contains(server, StringComparison.Ordinal),
        "Protected setup draft exposed SQL connection material as plaintext.", failures);

    var windowsBindingTarget = new DatabaseSetupOptions
    {
        UseWindowsAuthentication = false,
        Username = username,
        Password = password
    };
    var windowsBinding = DatabaseAuthenticationBinding.Apply(DatabaseAuthenticationBinding.WindowsMode, windowsBindingTarget);
    Expect(windowsBinding.Success && windowsBindingTarget.UseWindowsAuthentication,
        "Explicit Windows Authentication mode did not bind to Windows Authentication.", failures);
    Expect(string.IsNullOrEmpty(windowsBindingTarget.Username) && string.IsNullOrEmpty(windowsBindingTarget.Password),
        "Windows Authentication binding retained SQL credentials.", failures);
}
finally
{
    SqlConnection.ClearAllPools();
    try { Directory.Delete(temporaryRoot, recursive: true); } catch (IOException) { }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("P02 SQL Authentication checks FAILED:");
    failures.ForEach(failure => Console.Error.WriteLine($" - {failure}"));
    return 1;
}

Console.WriteLine("P02 SQL Authentication checks passed: explicit mode binding, transient SqlConnectionStringBuilder credentials, master target, Encrypt/TrustServerCertificate preservation, successful SQL login, wrong-credential classification and protected setup-state persistence are valid.");
return 0;

static string RequireEnvironment(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Required runtime test variable {name} is missing.");
    }
    return value;
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

file sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
