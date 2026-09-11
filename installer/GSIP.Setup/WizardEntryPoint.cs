using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace GSIP.Setup;

internal static class WizardEntryPoint
{
    [STAThread]
    public static int Main(string[] args)
    {
        var envelope = SetupCommandEnvelope.Parse(args);

        if (envelope.UseWizard)
        {
            HideConsoleWindow();
            if (!IsAdministrator())
            {
                try
                {
                    RelaunchElevated(envelope.LogPath);
                    return 0;
                }
                catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
                {
                    MessageBox.Show(
                        InstallerLocalization.T(
                            "Administrator elevation was cancelled. GSIP Setup requires elevation to configure IIS.",
                            "تم إلغاء منح صلاحيات المسؤول. يتطلب إعداد GSIP صلاحيات المسؤول لضبط IIS."),
                        InstallerLocalization.T("GSIP Setup", "إعداد GSIP"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 1;
                }
            }

            using var log = SanitizedInstallLog.Create(envelope.LogPath);
            log.Write("START", "interactive wizard");
            ApplicationConfiguration.Initialize();
            using var wizard = new InstallerWizard(log);
            InstallerLocalization.Attach(wizard);
            Application.Run(wizard);
            log.Write(wizard.ExitCode == 0 ? "SUCCESS" : "CANCELLED", "interactive wizard");
            return wizard.ExitCode;
        }

        using (var log = SanitizedInstallLog.Create(envelope.LogPath))
        {
            var action = envelope.RemainingArgs.Length == 0 ? "install" : envelope.RemainingArgs[0];
            log.Write("START", $"command action={action}");
            var exitCode = Program.Main(envelope.RemainingArgs);
            log.Write(exitCode == 0 ? "SUCCESS" : "FAILURE", $"command action={action}");
            return exitCode;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RelaunchElevated(string? logPath)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(InstallerLocalization.T(
                "Could not resolve the GSIP Setup executable path.",
                "تعذر تحديد مسار ملف تشغيل إعداد GSIP."));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        start.ArgumentList.Add("--wizard");
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            start.ArgumentList.Add("--log-path");
            start.ArgumentList.Add(logPath);
        }

        Process.Start(start);
    }

    private static void HideConsoleWindow()
    {
        var window = GetConsoleWindow();
        if (window != IntPtr.Zero)
            ShowWindow(window, 0);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private sealed record SetupCommandEnvelope(bool UseWizard, string? LogPath, string[] RemainingArgs)
    {
        public static SetupCommandEnvelope Parse(string[] args)
        {
            var useWizard = args.Length == 0;
            string? logPath = null;
            var remaining = new List<string>();

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (string.Equals(arg, "--wizard", StringComparison.OrdinalIgnoreCase))
                {
                    useWizard = true;
                    continue;
                }

                if (string.Equals(arg, "--log-path", StringComparison.OrdinalIgnoreCase))
                {
                    if (++index >= args.Length)
                        throw new ArgumentException(InstallerLocalization.T(
                            "Missing value for --log-path.",
                            "القيمة المطلوبة للوسيط --log-path غير موجودة."));
                    logPath = args[index];
                    continue;
                }

                remaining.Add(arg);
            }

            if (useWizard && remaining.Count != 0)
                throw new ArgumentException(InstallerLocalization.T(
                    "--wizard cannot be combined with command-line maintenance arguments.",
                    "لا يمكن استخدام --wizard مع معاملات صيانة سطر الأوامر."));

            return new SetupCommandEnvelope(useWizard, logPath, remaining.ToArray());
        }
    }
}
