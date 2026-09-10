# P15 Windows / IIS Installer Runbook

## Deployment contract

P15 packages the GSIP version declared by `Directory.Build.props` (`VersionPrefix`) for `win-x64` and targets Windows Server with IIS. The setup executable is a self-contained Windows installer; the ASP.NET Core application payload is framework-dependent and requires the supported .NET 10 Hosting Bundle / `AspNetCoreModuleV2` on the target server. Microsoft SQL Server remains external and is configured through the existing protected First-Run Setup flow. The deployment installer never requests or packages database passwords, MOJ/API credentials, bearer tokens, private keys, Civil IDs or real MOJ personal data.

Expected build outputs for the exact candidate version are:

- `GSIP-{Version}-win-x64.zip`
- `GSIP-{Version}-win-x64.zip.sha256`
- `GSIP-{Version}-Setup-x64.exe`
- `GSIP-{Version}-Setup-x64.exe.sha256`

`Directory.Build.props` remains the version source. `{Version}` in this runbook always means the exact `VersionPrefix` read from the same source SHA that produced the package; never substitute a historical phase version or rename an older artifact.

## Professional wizard

Launching `GSIP-{Version}-Setup-x64.exe` without command-line maintenance arguments opens the elevated Windows setup wizard. The wizard uses **Next/Back/Finish** navigation and presents:

1. Welcome and the security boundary.
2. Prerequisites for IIS and the .NET Hosting Bundle.
3. Install path, IIS site, application pool and supported `ApplicationPoolIdentity`.
4. IIS binding protocol, port, optional host name and HTTPS certificate selection.
5. Review before deployment.
6. Verification and Finish, with **Launch First-Run Setup** enabled by default.

HTTPS certificate choices come only from `Local Computer\Personal` certificates that are currently valid and have an accessible private key. The selected certificate is bound through HTTP.sys/IIS; its private key is never copied into the package or log. A host-specific HTTPS binding explicitly enables IIS SNI (`sslFlags=1`) and uses the matching HTTP.sys `hostnameport` certificate binding; a hostname-free HTTPS binding uses the normal IP/port binding with `sslFlags=0`. Cloud CI can prove the selection/binding implementation and compile it, but the owner's actual production certificate and target-server binding remain external deployment evidence.

After successful deployment the wizard verifies the application payload and requested IIS binding. Finish can launch `/setup`, where the protected application Setup Wizard configures SQL Server, initial administrator, security baseline and integration settings.

## Installation ownership and destructive-operation safety

`install-manifest.json` is the installer ownership marker. A valid marker must identify the GSIP product, `win-x64` architecture, version, IIS site and application-pool identity. Repair, uninstall and destructive purge fail closed without that marker, and the supplied site/application-pool names must match it.

A first installation accepts only a missing or empty target directory. A non-empty directory without a valid GSIP ownership manifest is never overwritten and can never be removed by `--purge-state`. Likewise, the first IIS deployment refuses to reuse or retarget a pre-existing site or application pool with the requested name; an existing IIS resource is eligible for repair only after the GSIP ownership marker proves the installation identity. This prevents GSIP Setup from taking over or deleting unrelated IIS applications.

The manifest is written through a temporary file followed by atomic replacement. During upgrade or repair the installer captures the exact previous manifest before replacing binaries; if a later deployment/IIS step fails, rollback restores both the previous application files and the previous manifest. A failed upgrade therefore cannot leave old binaries paired with a falsely advanced installed version. For a first installation that reaches IIS mutation, the new ownership marker remains intentionally so a partial installation can be repaired or explicitly removed instead of becoming an unattributed IIS footprint.

The default uninstall intentionally preserves `install-manifest.json` together with protected mutable state. This retained marker authorizes a later reinstall or an explicit state purge while continuing to bind destructive operations to the original GSIP site/application-pool identity. Only a correctly owned installation can be recursively purged.

## State preservation

GSIP has two deployment-critical protected state families under the application content root:

- `App_Data/keys`: ASP.NET Core Data Protection keys. On Windows they are DPAPI-protected and are required to decrypt persisted protected state and Secret Vault material.
- `App_Data/setup/completed.protected`: protected first-run completion state containing the configured runtime database connection.

Install, upgrade, repair and the default uninstall preserve the complete `App_Data` directory. A state purge occurs only when an administrator explicitly invokes uninstall with `--purge-state` against a valid, matching GSIP ownership marker. Losing the Data Protection key set can make protected setup/secret material undecryptable, so ordinary binary replacement must never delete or regenerate that state as an installation side effect.

The published payload removes `App_Data` and rejects private-key file extensions, protected setup state and environment-specific deployment files. This prevents a development database or runtime-owned state from being silently copied into a release package.

## Maintenance commands

Normal users should use the graphical wizard for initial deployment. Explicit command-line actions are retained for deterministic maintenance and automated acceptance. Replace `{Version}` below with the exact `Directory.Build.props` `VersionPrefix` from the source SHA whose SHA-256 evidence you are installing.

Elevated HTTP installation:

```powershell
.\GSIP-{Version}-Setup-x64.exe install --site-name GSIP --app-pool GSIP --port 8080
```

Repair uses the same exact setup executable, requires the matching ownership manifest, and preserves `App_Data`:

```powershell
.\GSIP-{Version}-Setup-x64.exe repair --site-name GSIP --app-pool GSIP --port 8080
```

Default uninstall removes IIS registration and replaceable application binaries while retaining protected mutable state and the ownership marker:

```powershell
.\GSIP-{Version}-Setup-x64.exe uninstall --site-name GSIP --app-pool GSIP
```

Destructive state removal is explicit and still requires matching GSIP ownership:

```powershell
.\GSIP-{Version}-Setup-x64.exe uninstall --site-name GSIP --app-pool GSIP --purge-state
```

`--install-root` may override the default `%ProgramFiles%\GSIP`. `--skip-iis` is an isolated lifecycle-acceptance mode used by CI; it bypasses only IIS mutation, not ownership checks, and is not target-server deployment evidence. `--log-path <path>` selects the sanitized setup log location for automation; otherwise the installer writes under `%ProgramData%\GSIP\InstallerLogs`.

## Upgrade and repair acceptance

P15 CI performs an explicit previous-version upgrade rehearsal rather than treating repair as a synonym for upgrade. It first proves that an unowned non-empty directory cannot be installed over or purged, then performs a clean GSIP installation, writes only synthetic protected-state markers, verifies forged site/application-pool identities fail closed, changes the synthetic installed manifest to a deliberately older synthetic version, adds a replaceable legacy file, and runs the current exact-candidate installer again. Acceptance requires:

- foreign content in an unowned directory to survive rejected install and purge attempts;
- forged ownership identity to be rejected without damaging the owned installation;
- the replaceable legacy file to disappear;
- the installed manifest to return to the exact candidate `{Version}`;
- `App_Data/keys` to remain unchanged;
- `App_Data/setup/completed.protected` to remain unchanged;
- default uninstall to retain the GSIP ownership marker.

The same exact setup executable then runs repair, default uninstall, reinstall and explicit owner-scoped purge acceptance. Static acceptance additionally requires atomic manifest replacement and restoration of the prior ownership manifest in the maintenance rollback path.

## Sanitized installer log

The setup entry point writes only bounded high-level lifecycle records. The logger redacts password/secret/token/authorization/API-key/Bearer assignments before persistence. No command line containing runtime credentials is required by P15, and the installer deliberately does not collect such credentials.

CI requires the log to exist, contain successful lifecycle records and contain no unredacted sensitive assignment pattern. Target Windows event logs and IIS operational diagnostics remain supplemental; they do not replace the setup log.

## IIS behavior

The deployment engine requires administrative elevation, IIS `appcmd.exe` and the ASP.NET Core IIS module. It creates an application pool only when no unowned pool with that name exists, uses no CLR managed runtime, `AlwaysRunning`, and `ApplicationPoolIdentity`; creates a site only when no unowned site with that name exists; assigns the exact application pool; grants that app-pool identity Modify access to the mutable `App_Data` subtree; and starts the site/pool after successful application replacement. A valid prior GSIP ownership marker permits repair/recreation of the exact recorded resources. If application replacement fails, replaceable binaries and any prior ownership manifest are rolled back while `App_Data` remains untouched.

The deterministic CLI installation creates the initial HTTP binding. The graphical wizard then applies and verifies the administrator-selected HTTP or HTTPS binding. For HTTPS, the wizard selects a valid Local Computer certificate, applies deterministic IIS SNI flags for host-specific bindings, and configures the HTTP.sys certificate binding without embedding certificate material.

## Owner/external boundary

Repository/cloud acceptance proves source compilation, versioned package construction, installer lifecycle, ownership/destructive-operation isolation, explicit previous-version upgrade preservation, sanitized installer logging, SHA-256 integrity and the HTTPS/SNI selection/binding implementation. It must not fabricate evidence for:

- installation on the owner's actual Windows Server/IIS target;
- the owner's approved production TLS certificate/private-key availability and production DNS binding;
- production DNS/network/proxy/firewall reachability;
- code-signing/release-signing operations requiring owner-controlled signing material;
- live MOJ UAT/Production acceptance already classified in earlier phases.

These remain `DEFERRED_EXTERNAL`/owner-last where applicable until performed with authorized infrastructure. They do not authorize weakening or skipping any cloud-actionable installer gate.
