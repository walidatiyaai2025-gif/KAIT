using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace GSIP.Setup;

internal static class IisBindingConfigurator
{
    private const string SslAppId = "{B122B67B-40F3-4D4D-9856-3E6E4AD9E1E4}";

    public static IReadOnlyList<string> GetPrerequisiteIssues()
    {
        var issues = new List<string>();
        if (!File.Exists(AppCmdPath))
            issues.Add("IIS Web Server role / appcmd.exe is missing.");

        var moduleCandidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS", "Asp.Net Core Module", "V2", "aspnetcorev2.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS", "Asp.Net Core Module", "V2", "aspnetcorev2_inprocess.dll")
        };
        if (!moduleCandidates.Any(File.Exists))
            issues.Add(".NET 10 Hosting Bundle / AspNetCoreModuleV2 is missing.");

        return issues;
    }

    public static IReadOnlyList<CertificateChoice> GetEligibleCertificates()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var now = DateTimeOffset.UtcNow;

        return store.Certificates
            .OfType<X509Certificate2>()
            .Where(certificate =>
                certificate.HasPrivateKey &&
                certificate.NotBefore.ToUniversalTime() <= now.UtcDateTime &&
                certificate.NotAfter.ToUniversalTime() > now.UtcDateTime &&
                !string.IsNullOrWhiteSpace(certificate.Thumbprint))
            .Select(certificate => new CertificateChoice(
                NormalizeThumbprint(certificate.Thumbprint),
                BuildCertificateDisplayName(certificate),
                certificate.NotAfter))
            .OrderBy(choice => choice.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(choice => choice.ExpiresAt)
            .ToArray();
    }

    public static void Configure(
        string siteName,
        string protocol,
        int port,
        string hostName,
        string? certificateThumbprint,
        SanitizedInstallLog log)
    {
        var normalizedProtocol = protocol.Trim().ToLowerInvariant();
        if (normalizedProtocol is not ("http" or "https"))
            throw new ArgumentException("IIS binding protocol must be HTTP or HTTPS.");
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "IIS binding port must be 1-65535.");

        var normalizedHost = NormalizeHostName(hostName);
        var desiredBinding = $"*:{port}:{normalizedHost}";
        var automaticHttpBinding = $"*:{port}:";

        if (normalizedProtocol == "http")
        {
            if (!string.IsNullOrWhiteSpace(normalizedHost))
                RemoveBindingIfPresent(siteName, "http", automaticHttpBinding);
            EnsureBinding(siteName, "http", desiredBinding);
            log.Write("BINDING", $"protocol=http port={port} hostname={(string.IsNullOrWhiteSpace(normalizedHost) ? "(all)" : normalizedHost)}");
            return;
        }

        if (string.IsNullOrWhiteSpace(certificateThumbprint))
            throw new InvalidOperationException("An HTTPS certificate must be selected.");
        var thumbprint = NormalizeThumbprint(certificateThumbprint);
        ValidateCertificate(thumbprint);

        RemoveBindingIfPresent(siteName, "http", automaticHttpBinding);
        EnsureBinding(siteName, "https", desiredBinding);
        SetHttpsSslFlags(siteName, desiredBinding, string.IsNullOrWhiteSpace(normalizedHost) ? 0 : 1);
        ConfigureSslCertificate(port, normalizedHost, thumbprint);
        log.Write("BINDING", $"protocol=https port={port} hostname={(string.IsNullOrWhiteSpace(normalizedHost) ? "(all)" : normalizedHost)} certificate=[SELECTED] sni={(string.IsNullOrWhiteSpace(normalizedHost) ? "off" : "on")}");
    }

    public static bool VerifyBinding(string siteName, string protocol, int port, string hostName)
    {
        var normalizedProtocol = protocol.Trim().ToLowerInvariant();
        var normalizedHost = NormalizeHostName(hostName);
        var expected = $"{normalizedProtocol}/*:{port}:{normalizedHost}";
        var output = RunProcessCapture(AppCmdPath, "list", "site", $"/name:{siteName}", "/text:bindings");
        return output.ExitCode == 0 &&
               output.Output.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                   .Any(binding => string.Equals(binding, expected, StringComparison.OrdinalIgnoreCase));
    }

    public static Uri BuildFirstRunSetupUri(string protocol, string hostName, int port)
    {
        var normalizedProtocol = protocol.Trim().ToLowerInvariant();
        var normalizedHost = NormalizeHostName(hostName);
        var host = string.IsNullOrWhiteSpace(normalizedHost) ? "localhost" : normalizedHost;
        var builder = new UriBuilder(normalizedProtocol, host, port, "/setup");
        return builder.Uri;
    }

    private static void EnsureBinding(string siteName, string protocol, string bindingInformation)
    {
        var output = RunProcessCapture(AppCmdPath, "list", "site", $"/name:{siteName}", "/text:bindings");
        if (output.ExitCode != 0)
            throw new InvalidOperationException("Could not read the configured IIS site bindings.");

        var expected = $"{protocol}/{bindingInformation}";
        if (output.Output.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(binding => string.Equals(binding, expected, StringComparison.OrdinalIgnoreCase)))
            return;

        RunRequired(
            AppCmdPath,
            "set",
            "site",
            $"/site.name:{siteName}",
            $"/+bindings.[protocol='{protocol}',bindingInformation='{bindingInformation}']");
    }

    private static void RemoveBindingIfPresent(string siteName, string protocol, string bindingInformation)
    {
        var output = RunProcessCapture(AppCmdPath, "list", "site", $"/name:{siteName}", "/text:bindings");
        if (output.ExitCode != 0)
            return;

        var expected = $"{protocol}/{bindingInformation}";
        if (!output.Output.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(binding => string.Equals(binding, expected, StringComparison.OrdinalIgnoreCase)))
            return;

        RunRequired(
            AppCmdPath,
            "set",
            "site",
            $"/site.name:{siteName}",
            $"/-bindings.[protocol='{protocol}',bindingInformation='{bindingInformation}']");
    }

    private static void SetHttpsSslFlags(string siteName, string bindingInformation, int sslFlags)
    {
        if (sslFlags is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(sslFlags));

        RunRequired(
            AppCmdPath,
            "set",
            "site",
            $"/site.name:{siteName}",
            $"/bindings.[protocol='https',bindingInformation='{bindingInformation}'].sslFlags:{sslFlags}");
    }

    private static void ConfigureSslCertificate(int port, string hostName, string thumbprint)
    {
        var locator = string.IsNullOrWhiteSpace(hostName)
            ? $"ipport=0.0.0.0:{port}"
            : $"hostnameport={hostName}:{port}";

        _ = RunProcessCapture("netsh.exe", "http", "delete", "sslcert", locator);
        RunRequired(
            "netsh.exe",
            "http",
            "add",
            "sslcert",
            locator,
            $"certhash={thumbprint}",
            $"appid={SslAppId}",
            "certstorename=MY");
    }

    private static void ValidateCertificate(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        var certificate = matches.OfType<X509Certificate2>().FirstOrDefault()
            ?? throw new InvalidOperationException("The selected HTTPS certificate is no longer available.");

        var now = DateTimeOffset.UtcNow;
        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException("The selected HTTPS certificate does not have an accessible private key.");
        if (certificate.NotBefore.ToUniversalTime() > now.UtcDateTime || certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime)
            throw new InvalidOperationException("The selected HTTPS certificate is not currently valid.");
    }

    private static string NormalizeHostName(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > 253 ||
            normalized.Any(character =>
                char.IsControl(character) ||
                char.IsWhiteSpace(character) ||
                character is '/' or '\\' or '"' or '\'' or ';' or ','))
            throw new ArgumentException("Invalid IIS binding host name.");

        if (normalized.Length > 0 && Uri.CheckHostName(normalized) == UriHostNameType.Unknown)
            throw new ArgumentException("Invalid IIS binding host name.");
        return normalized;
    }

    private static string NormalizeThumbprint(string value)
    {
        var normalized = new string((value ?? string.Empty)
            .Where(Uri.IsHexDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (normalized.Length is < 40 or > 128)
            throw new ArgumentException("Invalid certificate thumbprint.");
        return normalized;
    }

    private static string BuildCertificateDisplayName(X509Certificate2 certificate)
    {
        var name = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (string.IsNullOrWhiteSpace(name))
            name = certificate.Subject;
        var suffix = certificate.Thumbprint is { Length: >= 8 }
            ? certificate.Thumbprint[^8..]
            : certificate.Thumbprint ?? string.Empty;
        return $"{name} — expires {certificate.NotAfter:yyyy-MM-dd} — …{suffix}";
    }

    private static void RunRequired(string fileName, params string[] args)
    {
        var result = RunProcessCapture(fileName, args);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(fileName)} failed with exit code {result.ExitCode}.");
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
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {Path.GetFileName(fileName)}.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, string.IsNullOrWhiteSpace(output) ? error : output);
    }

    private static string AppCmdPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "inetsrv", "appcmd.exe");

    internal sealed record CertificateChoice(string Thumbprint, string DisplayName, DateTime ExpiresAt)
    {
        public override string ToString() => DisplayName;
    }
}
