# P15 — Windows / IIS Installer and Packaging Evidence

Status: **OPEN / ACTIVE implementation candidate — not closure evidence**

## Authority and phase entry

P14 was formally closed only after closure PR #74 was normally integrated and exact resulting `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633` completed **33/33 push workflows SUCCESS**. P15 implementation started from that exact main under tracker Issue #1 lease `P15::installer-packaging-convergence` on `worker/p15-installer-packaging`, with canonical implementation PR #75.

Historical `DEFERRED_EXTERNAL_NOT_PASS`, `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`, and repository-administration `OWNER_LAST / NOT PASS` classifications remain unchanged.

## Implemented cloud boundary

P15 adds a deterministic Windows Server/IIS deployment layer over the existing GSIP runtime:

- `installer/GSIP.Setup/GSIP.Setup.csproj` builds a .NET 10 Windows x64 self-contained single-file GUI setup executable;
- `installer/GSIP.Setup/WizardEntryPoint.cs` and `InstallerWizard.cs` provide the production wizard flow with Welcome, Prerequisites, Installation, Binding, Review and Finish pages plus Next/Back/Finish navigation;
- `installer/GSIP.Setup/IisBindingConfigurator.cs` provides explicit HTTP/HTTPS binding configuration and eligible Local Computer certificate discovery/selection without packaging private keys;
- `installer/GSIP.Setup/Program.cs` remains the shared maintenance engine for install, explicit previous-version upgrade, repair, uninstall and explicit `--purge-state` behavior;
- `installer/GSIP.Setup/SanitizedInstallLog.cs` provides a sanitized installer log without exposing credential/token/API-key assignments;
- successful wizard deployment can launch the protected First-Run Setup endpoint at `/setup`;
- `scripts/build-p15-package.ps1` builds the application, publishes the IIS payload, excludes mutable state and dangerous deployment material, and emits versioned ZIP/EXE artifacts with SHA-256 sidecars;
- `scripts/verify_p15_installer.py` provides executable static contract acceptance including the wizard, HTTPS, logging and lifecycle contract;
- `.github/workflows/p15-installer-packaging.yml` binds CI to the exact candidate SHA and executes package/lifecycle acceptance on `windows-latest`, explicitly waiting for the GUI-subsystem Setup EXE and validating its real exit code;
- `docs/P15_INSTALLER_RUNBOOK.md` documents prerequisites, lifecycle, state preservation, HTTPS selection and owner/external boundaries.

## State preservation contract

The application uses ASP.NET Core Data Protection persisted under `App_Data/keys` and DPAPI-protected on Windows. The protected first-run database binding is stored at `App_Data/setup/completed.protected`. Secret Vault payloads also depend on Data Protection.

Therefore install/upgrade/repair/default uninstall preserve the complete runtime-owned `App_Data` subtree. Replaceable application binaries are staged and backed up separately. An explicit uninstall `--purge-state` is the only installer path that intentionally removes preserved state.

The package itself rejects embedded `App_Data`, private-key file extensions, protected setup state and environment-specific appsettings that could smuggle credentials or target assumptions into the installer.

## IIS / HTTPS contract

Normal target installation requires administrative elevation, IIS `appcmd.exe`, and the supported ASP.NET Core Hosting Bundle / `AspNetCoreModuleV2`. The setup creates or repairs an application pool using `ApplicationPoolIdentity`, no CLR managed runtime and `AlwaysRunning`; creates or retargets the IIS site; assigns the application pool; and grants that exact app-pool identity Modify access to `App_Data`.

The wizard allows the operator to choose HTTP or HTTPS, port and optional host name. For HTTPS it enumerates currently valid eligible certificates from Local Computer / Personal, requires explicit certificate selection and configures the requested binding. The installer does not embed, export or invent production TLS certificate/private-key material.

Cloud CI proves the implementation contract and isolated Setup EXE lifecycle. Installation against the owner’s actual Windows Server/IIS target, approved production certificate selection, DNS/network/proxy/firewall behavior and any code/release signing remain authorized external/owner evidence and are not fabricated as PASS.

## Exact validated implementation candidate evidence

Validated implementation candidate before governance reconciliation: `4e811f17d707f2e1539c5dab9b40c64b6f498cc1`.

- P15 Installer and Packaging PR run `34436171235`: **SUCCESS**.
- Exact candidate checkout/assertion: **PASS**.
- Static installer acceptance: `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=36`.
- Solution/package build: `P15_PACKAGE_BUILD=PASS`, 0 warnings / 0 errors.
- Real Setup EXE clean install: **PASS**.
- Explicit previous-version upgrade from synthetic installed version `0.0.9` to `0.1.0`: **PASS**.
- Replaceable legacy binary removal during upgrade: **PASS**.
- Data Protection key state preservation across upgrade/repair/default uninstall/reinstall: **PASS**.
- Protected first-run setup/database-binding state preservation across upgrade/repair/default uninstall/reinstall: **PASS**.
- Repair: **PASS**.
- Default uninstall with protected mutable-state preservation: **PASS**.
- Reinstall/rediscovery of protected state: **PASS**.
- Explicit destructive purge: **PASS**.
- Sanitized installer log acceptance: **PASS**.
- Execution plan integrity: **PASS**.
- Artifact SHA-256 verification: **PASS**.
- `GSIP-0.1.0-win-x64.zip` SHA-256: `def9054f7a7746c04a5790f127836c17cadc35cabeb3922487fe9c8d65f69fa9`.
- `GSIP-0.1.0-Setup-x64.exe` SHA-256: `c9465545ade663a31fe7d83002e8a88001eb60dedcdc376f81bb60ac1db94d03`.
- Exact-candidate Actions artifact ID `10136339172`; artifact archive digest `sha256:e7ddce03120177943bbfe4949fedac13462949c14858125068f38a1d6ff47867`; size 62,160,131 bytes.

The implementation history contains two superseded real CI failures that were repaired rather than waived. Candidate `18023650aeadbc23e3c476246316f5e1442ccd5f` failed Setup publish only because nullable warnings-as-errors flagged system-font dereferences; the implementation was fixed without weakening any gate. Candidate `1bd81efc7270ed056c058fe4f550b2aff19f6ac2` then built successfully, but PowerShell did not synchronously wait for the GUI-subsystem `WinExe` before reading `$LASTEXITCODE`; the acceptance harness was corrected to start the same Setup EXE through `System.Diagnostics.Process`, wait for exit and validate the actual exit code. The final validated implementation candidate `4e811f17...` passed the complete P15 workflow.

## Not proven by cloud CI

The following remain outside this candidate’s cloud proof and must not be called PASS without authorized target evidence:

- installation on the owner’s actual Windows Server/IIS target;
- selection/binding of the owner-approved production HTTPS certificate on that target;
- production DNS/network/proxy/firewall behavior;
- code signing / release signing when owner-controlled certificate material is required;
- live MOJ UAT/Production evidence already classified in earlier phases.

`--skip-iis` is intentionally an isolated CI mode used to execute the exact Setup EXE lifecycle without claiming target IIS deployment evidence. The production wizard/IIS/HTTPS code path remains fail-closed on missing prerequisites or certificate selection.

## Remaining P15 gate

This document is not P15 closure. The governance-reconciliation commit containing this evidence supersedes `4e811f17...` as the PR merge candidate. P15 remains OPEN / ACTIVE until the final exact PR head passes every governed workflow, PR #75 is normally integrated from current exact `main`, every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS, package/installer/hash identity is reconciled to exact post-merge evidence, and canonical closure governance is recorded. P16 remains locked until that gate is satisfied.
