# P15 Windows / IIS Installer Runbook

## Deployment contract

P15 packages GSIP `0.1.0` for `win-x64` and targets Windows Server with IIS. The setup executable is a self-contained Windows installer; the ASP.NET Core application payload is framework-dependent and requires the supported .NET 10 Hosting Bundle / `AspNetCoreModuleV2` on the target server. Microsoft SQL Server remains external and is configured through the existing protected First-Run Setup flow. The deployment installer never requests or packages database passwords, MOJ/API credentials, bearer tokens, private keys, Civil IDs or real MOJ personal data.

Expected build outputs are:

- `GSIP-0.1.0-win-x64.zip`
- `GSIP-0.1.0-win-x64.zip.sha256`
- `GSIP-0.1.0-Setup-x64.exe`
- `GSIP-0.1.0-Setup-x64.exe.sha256`

`Directory.Build.props` remains the version source.

## Professional wizard

Launching `GSIP-0.1.0-Setup-x64.exe` without command-line maintenance arguments opens the elevated Windows setup wizard. The wizard uses **Next/Back/Finish** navigation and presents:

1. Welcome and the security boundary.
2. Prerequisites for IIS and the .NET Hosting Bundle.
3. Install path, IIS site, application pool and supported `ApplicationPoolIdentity`.
4. IIS binding protocol, port, optional host name and HTTPS certificate selection.
5. Review before deployment.
6. Verification and Finish, with **Launch First-Run Setup** enabled by default.

HTTPS certificate choices come only from `Local Computer\Personal` certificates that are currently valid and have an accessible private key. The selected certificate is bound through HTTP.sys/IIS; its private key is never copied into the package or log. Cloud CI can prove the selection/binding implementation and compile it, but the owner's actual production certificate and target-server binding remain external deployment evidence.

After successful deployment the wizard verifies the application payload and requested IIS binding. Finish can launch `/setup`, where the protected application Setup Wizard configures SQL Server, initial administrator, security baseline and integration settings.

## State preservation

GSIP has two deployment-critical protected state families under the application content root:

- `App_Data/keys`: ASP.NET Core Data Protection keys. On Windows they are DPAPI-protected and are required to decrypt persisted protected state and Secret Vault material.
- `App_Data/setup/completed.protected`: protected first-run completion state containing the configured runtime database connection.

Install, upgrade, repair and the default uninstall preserve the complete `App_Data` directory. A state purge occurs only when an administrator explicitly invokes uninstall with `--purge-state`. Losing the Data Protection key set can make protected setup/secret material undecryptable, so ordinary binary replacement must never delete or regenerate that state as an installation side effect.

The published payload removes `App_Data` and rejects private-key file extensions, protected setup state and environment-specific deployment files. This prevents a development database or runtime-owned state from being silently copied into a release package.

## Maintenance commands

Normal users should use the graphical wizard for initial deployment. Explicit command-line actions are retained for deterministic maintenance and automated acceptance.

Elevated HTTP installation:

```powershell
.\GSIP-0.1.0-Setup-x64.exe install --site-name GSIP --app-pool GSIP --port 8080
```

Repair uses the same setup executable and preserves `App_Data`:

```powershell
.\GSIP-0.1.0-Setup-x64.exe repair --site-name GSIP --app-pool GSIP --port 8080
```

Default uninstall removes IIS registration and replaceable application binaries while retaining protected mutable state:

```powershell
.\GSIP-0.1.0-Setup-x64.exe uninstall --site-name GSIP --app-pool GSIP
```

Destructive state removal is explicit:

```powershell
.\GSIP-0.1.0-Setup-x64.exe uninstall --site-name GSIP --app-pool GSIP --purge-state
```

`--install-root` may override the default `%ProgramFiles%\GSIP`. `--skip-iis` is an isolated lifecycle-acceptance mode used by CI; it is not target-server deployment evidence. `--log-path <path>` selects the sanitized setup log location for automation; otherwise the installer writes under `%ProgramData%\GSIP\InstallerLogs`.

## Upgrade and repair acceptance

P15 CI performs an explicit previous-version upgrade rehearsal rather than treating repair as a synonym for upgrade. It first performs a clean installation, writes only synthetic protected-state markers, changes the synthetic installed manifest to version `0.0.9`, adds a replaceable legacy file, then runs the current `0.1.0` installer again. Acceptance requires:

- the replaceable legacy file to disappear;
- the installed manifest to return to `0.1.0`;
- `App_Data/keys` to remain unchanged;
- `App_Data/setup/completed.protected` to remain unchanged.

The same exact setup executable then runs repair, default uninstall, reinstall and explicit purge acceptance.

## Sanitized installer log

The setup entry point writes only bounded high-level lifecycle records. The logger redacts password/secret/token/authorization/API-key/Bearer assignments before persistence. No command line containing runtime credentials is required by P15, and the installer deliberately does not collect such credentials.

CI requires the log to exist, contain successful lifecycle records and contain no unredacted sensitive assignment pattern. Target Windows event logs and IIS operational diagnostics remain supplemental; they do not replace the setup log.

## IIS behavior

The deployment engine requires administrative elevation, IIS `appcmd.exe` and the ASP.NET Core IIS module. It creates or repairs an application pool with no CLR managed runtime, `AlwaysRunning`, and `ApplicationPoolIdentity`; creates or retargets the GSIP site; assigns the exact application pool; grants that app-pool identity Modify access to the mutable `App_Data` subtree; and starts the site/pool after successful application replacement. If application replacement fails, replaceable binaries are rolled back while `App_Data` remains untouched.

The existing deterministic CLI installation creates the initial HTTP binding. The graphical wizard then applies and verifies the administrator-selected HTTP or HTTPS binding. For HTTPS, the wizard selects a valid Local Computer certificate and configures the HTTP.sys certificate binding without embedding certificate material.

## Owner/external boundary

Repository/cloud acceptance proves source compilation, versioned package construction, installer lifecycle, explicit previous-version upgrade preservation, sanitized installer logging, SHA-256 integrity and the HTTPS-selection/binding implementation. It must not fabricate evidence for:

- installation on the owner's actual Windows Server/IIS target;
- the owner's approved production TLS certificate/private-key availability and production DNS binding;
- production DNS/network/proxy/firewall reachability;
- code-signing/release-signing operations requiring owner-controlled signing material;
- live MOJ UAT/Production acceptance already classified in earlier phases.

These remain `DEFERRED_EXTERNAL`/owner-last where applicable until performed with authorized infrastructure. They do not authorize weakening or skipping any cloud-actionable installer gate.
