using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GSIP.Setup;

internal sealed class InstallerWizard : Form
{
    private const int WelcomePage = 0;
    private const int PrerequisitesPage = 1;
    private const int InstallationPage = 2;
    private const int BindingPage = 3;
    private const int ReviewPage = 4;
    private const int FinishPage = 5;

    private readonly SanitizedInstallLog _log;
    private readonly Panel _content = new() { Dock = DockStyle.Fill, Padding = new Padding(28) };
    private readonly Label _heading = new()
    {
        Dock = DockStyle.Top,
        Height = 62,
        Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 18, FontStyle.Bold),
        Padding = new Padding(28, 18, 28, 4)
    };
    private readonly Label _step = new()
    {
        Dock = DockStyle.Top,
        Height = 28,
        Padding = new Padding(30, 0, 28, 4)
    };
    private readonly Button _back = new() { Text = "Back", Width = 100, Height = 34 };
    private readonly Button _next = new() { Text = "Next", Width = 100, Height = 34 };
    private readonly Button _cancel = new() { Text = "Cancel", Width = 100, Height = 34 };

    private readonly TextBox _installRoot = new() { Dock = DockStyle.Fill };
    private readonly TextBox _siteName = new() { Dock = DockStyle.Fill, Text = "GSIP" };
    private readonly ComboBox _appPoolIdentity = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _appPoolName = new() { Dock = DockStyle.Fill, Text = "GSIP" };
    private readonly ComboBox _protocol = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Value = 8080, Width = 160 };
    private readonly TextBox _hostName = new() { Width = 360 };
    private readonly ComboBox _certificate = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    private readonly Button _refreshCertificates = new() { Text = "Refresh certificates", AutoSize = true };
    private readonly CheckBox _launchSetup = new()
    {
        Text = "Launch First-Run Setup when I click Finish",
        AutoSize = true,
        Checked = true
    };

    private int _page;
    private bool _installSucceeded;

    public InstallerWizard(SanitizedInstallLog log)
    {
        _log = log;
        ExitCode = 1;

        Text = "Government Services Integration Portal Setup";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 560);
        Size = new Size(880, 650);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        _installRoot.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "GSIP");
        _appPoolIdentity.Items.Add("ApplicationPoolIdentity");
        _appPoolIdentity.SelectedIndex = 0;
        _protocol.Items.AddRange(["HTTP", "HTTPS"]);
        _protocol.SelectedIndex = 0;

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(18, 14, 18, 8),
            WrapContents = false
        };
        footer.Controls.AddRange([_cancel, _next, _back]);

        Controls.Add(_content);
        Controls.Add(_step);
        Controls.Add(_heading);
        Controls.Add(footer);

        _back.Click += (_, _) => MoveBack();
        _next.Click += async (_, _) => await MoveNextAsync();
        _cancel.Click += (_, _) => Close();
        _protocol.SelectedIndexChanged += (_, _) => UpdateCertificateState();
        _refreshCertificates.Click += (_, _) => LoadCertificates();

        RenderPage();
    }

    public int ExitCode { get; private set; }

    private void RenderPage()
    {
        _content.Controls.Clear();
        _step.Text = $"Step {_page + 1} of 6";
        _back.Enabled = _page > WelcomePage && _page < FinishPage;
        _cancel.Visible = _page < FinishPage;
        _next.Enabled = true;
        _next.Text = _page switch
        {
            ReviewPage => "Install",
            FinishPage => "Finish",
            _ => "Next"
        };

        switch (_page)
        {
            case WelcomePage:
                _heading.Text = "Welcome to GSIP Setup";
                RenderWelcome();
                break;
            case PrerequisitesPage:
                _heading.Text = "Prerequisites";
                RenderPrerequisites();
                break;
            case InstallationPage:
                _heading.Text = "Installation and IIS application pool";
                RenderInstallation();
                break;
            case BindingPage:
                _heading.Text = "IIS binding and HTTPS certificate";
                RenderBinding();
                break;
            case ReviewPage:
                _heading.Text = "Ready to install";
                RenderReview();
                break;
            case FinishPage:
                _heading.Text = "Installation complete";
                RenderFinish();
                break;
        }
    }

    private void RenderWelcome()
    {
        _content.Controls.Add(CreateTextBlock(
            "This wizard installs Government Services Integration Portal 0.1.0 on Windows Server / IIS.\r\n\r\n" +
            "Database and government-service credentials are not collected by this installer. " +
            "After deployment, GSIP opens its protected First-Run Setup at /setup for SQL Server, administrator, security and integration configuration.\r\n\r\n" +
            $"A sanitized installer log is written to:\r\n{_log.Path}"));
    }

    private void RenderPrerequisites()
    {
        var issues = IisBindingConfigurator.GetPrerequisiteIssues();
        var status = issues.Count == 0
            ? "Prerequisite check passed.\r\n\r\n• IIS Web Server / appcmd.exe detected\r\n• ASP.NET Core Module V2 detected\r\n• Administrator elevation active"
            : "Prerequisite check found blocking items:\r\n\r\n• " + string.Join("\r\n• ", issues);

        var label = CreateTextBlock(status);
        label.Tag = issues.Count == 0;
        _content.Controls.Add(label);
    }

    private void RenderInstallation()
    {
        var table = CreateFormTable();
        AddRow(table, 0, "Install path", _installRoot);
        AddRow(table, 1, "IIS site name", _siteName);
        AddRow(table, 2, "Application pool", _appPoolName);
        AddRow(table, 3, "App Pool identity", _appPoolIdentity);
        table.Controls.Add(CreateHint(
            "ApplicationPoolIdentity is the supported least-privilege identity. " +
            "The installer grants this pool Modify access only to app/App_Data."), 1, 4);
        _content.Controls.Add(table);
    }

    private void RenderBinding()
    {
        LoadCertificatesIfNeeded();
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(panel, 0, "Protocol", _protocol);
        AddRow(panel, 1, "Port", _port);
        AddRow(panel, 2, "Host name (optional)", _hostName);

        var certificatePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        certificatePanel.Controls.Add(_certificate);
        certificatePanel.Controls.Add(_refreshCertificates);
        AddRow(panel, 3, "HTTPS certificate", certificatePanel);

        panel.Controls.Add(CreateHint(
            "HTTPS certificates are read from Local Computer / Personal and must be currently valid with an accessible private key. " +
            "No private key or certificate file is copied into the installer or log."), 1, 4);

        _content.Controls.Add(panel);
        UpdateCertificateState();
    }

    private void RenderReview()
    {
        var protocol = SelectedProtocol;
        var binding = $"{protocol.ToUpperInvariant()} :{(int)_port.Value}" +
                      (string.IsNullOrWhiteSpace(_hostName.Text) ? string.Empty : $" host={_hostName.Text.Trim()}");
        var certificateText = protocol == "https"
            ? (_certificate.SelectedItem is IisBindingConfigurator.CertificateChoice choice ? choice.DisplayName : "No certificate selected")
            : "Not required for HTTP";

        _content.Controls.Add(CreateTextBlock(
            $"Install path: {_installRoot.Text.Trim()}\r\n" +
            $"IIS site: {_siteName.Text.Trim()}\r\n" +
            $"Application pool: {_appPoolName.Text.Trim()} ({_appPoolIdentity.SelectedItem})\r\n" +
            $"Binding: {binding}\r\n" +
            $"Certificate: {certificateText}\r\n\r\n" +
            "Protected runtime state under App_Data is preserved across install/upgrade/repair/default uninstall. " +
            "The application package does not contain a development database or runtime secrets.\r\n\r\n" +
            "Click Install to deploy files, configure the application pool, ACL and IIS binding, then verify the installation."));
    }

    private void RenderFinish()
    {
        var setupUri = IisBindingConfigurator.BuildFirstRunSetupUri(
            SelectedProtocol,
            _hostName.Text,
            (int)_port.Value);

        var container = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        container.Controls.Add(CreateTextBlock(
            "GSIP application files, IIS application pool, binding and mutable-state ACL were configured and verified.\r\n\r\n" +
            $"First-Run Setup: {setupUri}\r\n" +
            $"Sanitized installer log: {_log.Path}\r\n\r\n" +
            "First-Run Setup configures SQL Server, the initial administrator, security baseline and service integration settings."));

        container.Controls.Add(_launchSetup);
        _content.Controls.Add(container);
    }

    private void MoveBack()
    {
        if (_page <= WelcomePage || _page >= FinishPage)
            return;
        _page--;
        RenderPage();
    }

    private async Task MoveNextAsync()
    {
        try
        {
            if (_page == FinishPage)
            {
                if (_launchSetup.Checked)
                    LaunchFirstRunSetup();
                Close();
                return;
            }

            ValidateCurrentPage();
            if (_page == ReviewPage)
            {
                await InstallAsync();
                return;
            }

            _page++;
            RenderPage();
        }
        catch (Exception exception)
        {
            _log.Write("ERROR", exception.Message);
            MessageBox.Show(
                exception.Message,
                "GSIP Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ValidateCurrentPage()
    {
        if (_page == PrerequisitesPage)
        {
            var issues = IisBindingConfigurator.GetPrerequisiteIssues();
            if (issues.Count != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, issues));
        }

        if (_page == InstallationPage)
        {
            if (string.IsNullOrWhiteSpace(_installRoot.Text))
                throw new InvalidOperationException("Install path is required.");
            if (string.IsNullOrWhiteSpace(_siteName.Text))
                throw new InvalidOperationException("IIS site name is required.");
            if (string.IsNullOrWhiteSpace(_appPoolName.Text))
                throw new InvalidOperationException("Application pool name is required.");
        }

        if (_page == BindingPage)
        {
            _ = IisBindingConfigurator.BuildFirstRunSetupUri(
                SelectedProtocol,
                _hostName.Text,
                (int)_port.Value);
            if (SelectedProtocol == "https" && _certificate.SelectedItem is not IisBindingConfigurator.CertificateChoice)
                throw new InvalidOperationException("Select a valid Local Computer HTTPS certificate before continuing.");
        }
    }

    private async Task InstallAsync()
    {
        SetBusy(true);
        try
        {
            var installRoot = _installRoot.Text.Trim();
            var siteName = _siteName.Text.Trim();
            var appPoolName = _appPoolName.Text.Trim();
            var port = (int)_port.Value;

            _log.Write("DEPLOY", $"install-root={installRoot} site={siteName} app-pool={appPoolName}");
            var exitCode = await Task.Run(() => Program.Main(
            [
                "install",
                "--install-root", installRoot,
                "--site-name", siteName,
                "--app-pool", appPoolName,
                "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ]));
            if (exitCode != 0)
                throw new InvalidOperationException("Application deployment failed. Review the sanitized installer log and Windows event logs.");

            var thumbprint = (_certificate.SelectedItem as IisBindingConfigurator.CertificateChoice)?.Thumbprint;
            IisBindingConfigurator.Configure(
                siteName,
                SelectedProtocol,
                port,
                _hostName.Text,
                thumbprint,
                _log);

            var deployedDll = Path.Combine(installRoot, "app", "GSIP.Web.dll");
            if (!File.Exists(deployedDll))
                throw new InvalidOperationException("Installation verification failed: GSIP.Web.dll is missing.");
            if (!IisBindingConfigurator.VerifyBinding(siteName, SelectedProtocol, port, _hostName.Text))
                throw new InvalidOperationException("Installation verification failed: requested IIS binding was not found.");

            _log.Write("VERIFY", "application payload, IIS binding and state directory verified");
            _installSucceeded = true;
            ExitCode = 0;
            _page = FinishPage;
            RenderPage();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void LaunchFirstRunSetup()
    {
        if (!_installSucceeded)
            return;

        var setupUri = IisBindingConfigurator.BuildFirstRunSetupUri(
            SelectedProtocol,
            _hostName.Text,
            (int)_port.Value);
        _log.Write("LAUNCH", $"first-run setup {setupUri.Scheme}://{setupUri.Host}:{setupUri.Port}/setup");
        Process.Start(new ProcessStartInfo(setupUri.AbsoluteUri) { UseShellExecute = true });
    }

    private void LoadCertificatesIfNeeded()
    {
        if (_certificate.Items.Count == 0)
            LoadCertificates();
    }

    private void LoadCertificates()
    {
        var selectedThumbprint = (_certificate.SelectedItem as IisBindingConfigurator.CertificateChoice)?.Thumbprint;
        _certificate.BeginUpdate();
        try
        {
            _certificate.Items.Clear();
            foreach (var choice in IisBindingConfigurator.GetEligibleCertificates())
                _certificate.Items.Add(choice);

            if (!string.IsNullOrWhiteSpace(selectedThumbprint))
            {
                var match = _certificate.Items
                    .OfType<IisBindingConfigurator.CertificateChoice>()
                    .FirstOrDefault(choice => string.Equals(choice.Thumbprint, selectedThumbprint, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    _certificate.SelectedItem = match;
            }

            if (_certificate.SelectedIndex < 0 && _certificate.Items.Count > 0)
                _certificate.SelectedIndex = 0;
        }
        finally
        {
            _certificate.EndUpdate();
        }

        UpdateCertificateState();
    }

    private void UpdateCertificateState()
    {
        var enabled = SelectedProtocol == "https";
        _certificate.Enabled = enabled;
        _refreshCertificates.Enabled = enabled;
        if (enabled && _port.Value == 8080)
            _port.Value = 443;
        if (!enabled && _port.Value == 443)
            _port.Value = 8080;
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _back.Enabled = !busy && _page > WelcomePage && _page < FinishPage;
        _next.Enabled = !busy;
        _cancel.Enabled = !busy;
    }

    private string SelectedProtocol =>
        string.Equals(_protocol.SelectedItem?.ToString(), "HTTPS", StringComparison.OrdinalIgnoreCase)
            ? "https"
            : "http";

    private static Label CreateTextBlock(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        AutoSize = true,
        MaximumSize = new Size(760, 0),
        Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10),
        Padding = new Padding(0, 8, 0, 8)
    };

    private static Label CreateHint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(560, 0),
        Padding = new Padding(0, 8, 0, 8)
    };

    private static TableLayoutPanel CreateFormTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 8, 10, 8)
        }, 0, row);
        control.Margin = new Padding(3, 6, 3, 6);
        table.Controls.Add(control, 1, row);
    }
}
