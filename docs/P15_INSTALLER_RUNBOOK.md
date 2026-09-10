# P15 Windows / IIS Installer Runbook

## Deployment contract

P15 packages GSIP `0.1.0` for `win-x64` and targets Windows Server with IIS. The ASP.NET Core application is framework-dependent and requires the supported .NET 10 Hosting Bundle / `AspNetCoreModuleV2` to be installed before normal IIS installation. Microsoft SQL Server remains external and is configured through the existing protected first-run Setup flow; the installer never packages a database credential, API credential, private key, Civil ID, token or real MOJ personal data.

Expected build outputs are:

- `GSIP-0.1.0-win-x64.zip`
- `GSIP-0.1.0-win-x64.zip.sha256`
- `GSIP-0.1.0-Setup-x64.exe`
- `GSIP-0.1.0-Setup-x64.exe.sha256`

`Directory.Build.props` remains the version source.

## State preservation

GSIP has two deployment-critical protected state families under the application content root:

- `App_Data/keys`: ASP.NET Core Data Protection keys. On Windows they are DPAPI-protected and are required to decrypt persisted protected state and Secret Vault material.
- `App_Data/setup/completed.protected`: protected first-run completion state containing the configured runtime database connection.

Install, repair, upgrade and the default uninstall preserve the complete `App_Data` directory. A state purge occurs only when an administrator explicitly invokes uninstall with `--purge-state`. Losing the Data Protection key set can make protected setup/secret material undecryptable, so ordinary binary replacement must never delete or regenerate that state as an installation side effect.

## Commands

Normal elevated IIS installation:

```powershell
.\GSIP-0.1.0-Setup-x64.exe install --site-name GSIP --app-pool GSIP --port 8080
```

Repair/upgrade uses the same setup executable and preserves `App_Data`:

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

`--install-root` may override the default `%ProgramFiles%\GSIP`. `--skip-iis` is an isolated package-lifecycle acceptance mode used by CI; it is not target-server deployment evidence.

## IIS behavior

The installer requires administrative elevation, IIS `appcmd.exe`, and the ASP.NET Core IIS module. It creates or repairs an application pool with no CLR managed runtime, `AlwaysRunning`, and `ApplicationPoolIdentity`; creates or retargets the GSIP site; assigns the exact application pool; grants only that app-pool identity Modify access to the mutable `App_Data` subtree; and starts the site/pool after a successful application replacement. If application replacement fails, replaceable binaries are rolled back while `App_Data` remains untouched.

The default bootstrap binding is HTTP on the configured port. Production TLS certificate selection/binding is deployment-environment material and is deliberately not invented or embedded in source. Production go-live requires the authorized target administrator to bind the approved certificate/HTTPS policy and provide target-UAT/Production evidence in the applicable later acceptance phase.

## Owner/external boundary

Cloud CI proves exact-candidate package construction, SHA-256 integrity and isolated install/repair/uninstall/reinstall state preservation. It does not convert target-server installation, production certificate binding, code signing, or release signing into PASS. Those remain owner/external evidence until performed with authorized infrastructure and credentials.
