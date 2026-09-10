using System.Text.RegularExpressions;

namespace GSIP.Setup;

internal sealed class SanitizedInstallLog : IDisposable
{
    private static readonly Regex SensitiveAssignment = new(
        @"(?i)\b(password|pwd|secret|token|authorization|x-api-key|api[-_]?key|bearer)\b\s*[:=]\s*[^\s;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BearerValue = new(
        @"(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BasicValue = new(
        @"(?i)\bbasic\s+[A-Za-z0-9+/]+=*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _path;
    private readonly object _sync = new();

    private SanitizedInstallLog(string path)
    {
        _path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
    }

    public string Path => _path;

    public static SanitizedInstallLog Create(string? requestedPath)
    {
        var path = string.IsNullOrWhiteSpace(requestedPath)
            ? DefaultPath()
            : System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(requestedPath));
        return new SanitizedInstallLog(path);
    }

    public void Write(string stage, string message)
    {
        var safeStage = Redact(stage).Replace('\r', ' ').Replace('\n', ' ');
        var safeMessage = Redact(message).Replace('\r', ' ').Replace('\n', ' ');
        var line = $"{DateTimeOffset.UtcNow:O} [{safeStage}] {safeMessage}{Environment.NewLine}";
        lock (_sync)
            File.AppendAllText(_path, line);
    }

    internal static string Redact(string value)
    {
        // Redact complete credential schemes before generic assignment masking.
        // Otherwise `Authorization: Basic <credential>` would first become
        // `Authorization=[REDACTED] <credential>` and leave the credential tail exposed.
        var redacted = BearerValue.Replace(value ?? string.Empty, "Bearer [REDACTED]");
        redacted = BasicValue.Replace(redacted, "Basic [REDACTED]");
        return SensitiveAssignment.Replace(redacted, "$1=[REDACTED]");
    }

    private static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = System.IO.Path.GetTempPath();

        return System.IO.Path.Combine(
            root,
            "GSIP",
            "InstallerLogs",
            $"GSIP-Setup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
    }

    public void Dispose()
    {
    }
}
