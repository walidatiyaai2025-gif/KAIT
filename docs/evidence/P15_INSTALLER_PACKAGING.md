# P15 — Windows / IIS Installer and Packaging Evidence

Status: **OPEN / ACTIVE implementation candidate — not closure evidence**

## Authority and phase entry

P14 was formally closed only after closure PR #74 was normally integrated and exact resulting `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633` completed **33/33 push workflows SUCCESS**. P15 implementation started from that exact main under tracker Issue #1 lease `P15::installer-packaging-convergence` on `worker/p15-installer-packaging`, with canonical implementation PR #75. PR #75 later recovered post-closure P14 security repair PR #76 and is based on exact current main `78cfe0433bdb02367ddd1365df57d118fea9257a` without duplicating or reverting that work.

Historical `DEFERRED_EXTERNAL_NOT_PASS`, `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`, and repository-administration `OWNER_LAST / NOT PASS` classifications remain unchanged.

## Implemented cloud boundary

P15 adds a deterministic Windows Server/IIS deployment layer over the existing GSIP runtime:

- `installer/GSIP.Setup/GSIP.Setup.csproj` builds a .NET 10 Windows x64 self-contained single-file GUI setup executable;
- `installer/GSIP.Setup/WizardEntryPoint.cs` and `InstallerWizard.cs` provide the production wizard flow with Welcome, Prerequisites, Installation, Binding, Review and Finish pages plus Next/Back/Finish navigation;
- `installer/GSIP.Setup/IisBindingConfigurator.cs` provides explicit HTTP/HTTPS binding configuration, eligible Local Computer certificate discovery/selection, and deterministic SNI (`sslFlags=1`) for host-specific HTTPS without packaging private keys;
- `installer/GSIP.Setup/Program.cs` is the shared maintenance engine for install, explicit previous-version upgrade, repair, uninstall and explicit `--purge-state`, with a fail-closed GSIP ownership manifest boundary around filesystem/IIS takeover and destructive removal;
- ownership-manifest writes use temporary-file replacement, and failed upgrade/repair restores the exact previous manifest together with rolled-back binaries so installed-version evidence cannot advance when deployment fails;
- `installer/GSIP.Setup/SanitizedInstallLog.cs` provides a sanitized installer log without exposing credential/token/API-key assignments;
- successful wizard deployment can launch the protected First-Run Setup endpoint at `/setup`;
- `scripts/build-p15-package.ps1` builds the application, publishes the IIS payload, excludes mutable state and dangerous deployment material, and emits versioned ZIP/EXE artifacts with SHA-256 sidecars;
- `scripts/verify_p15_installer.py` provides executable static contract acceptance including ownership/destructive isolation, atomic manifest rollback, wizard, HTTPS/SNI, logging and lifecycle contracts;
- `.github/workflows/p15-installer-packaging.yml` binds CI to the exact candidate SHA and executes package/lifecycle acceptance on `windows-latest`, including negative unowned-root and forged-ownership tests;
- `docs/P15_INSTALLER_RUNBOOK.md` documents prerequisites, lifecycle, ownership, rollback, state preservation, HTTPS/SNI selection and owner/external boundaries.

## Ownership/destructive safety repair discovered during convergence

A post-reconciliation code audit found a real P15 blocker before merge: the earlier candidate could retarget a pre-existing IIS site/application pool that was not proven to belong to GSIP, uninstall could delete named IIS resources without installation ownership proof, and `--purge-state` could recursively delete an arbitrary non-drive-root supplied by the caller. This was not accepted as an external limitation and was repaired in the canonical PR #75 line.

The repaired boundary requires a valid `install-manifest.json` product/architecture/site/application-pool ownership marker for repair, uninstall and purge; rejects a non-empty unowned installation directory; rejects first-install collision with an existing IIS site or application pool; rejects forged site/application-pool identities against an owned installation; preserves the ownership marker on default uninstall; and allows recursive purge only after marker and identity validation. CI now carries executable negative cases proving foreign sentinel content survives rejected install/purge attempts.

A subsequent rollback audit found that updating the manifest before IIS mutation was necessary to attribute a partially created first installation, but could leave an existing upgrade with a new manifest version if a later IIS step failed after binaries had been rolled back. The maintenance path now snapshots the exact previous manifest, writes replacement manifests through temporary-file atomic replacement, and restores the prior manifest whenever an existing install/repair rolls back. A brand-new install that has already reached IIS mutation intentionally retains its GSIP marker so the partial footprint remains attributable and recoverable. This repair is gated statically and requires fresh exact-head CI; it is not asserted as PASS from documentation alone.

The same audit identified host-specific HTTPS SNI drift: the earlier code created a hostname HTTPS binding but did not explicitly set the IIS SNI flag. The canonical line now sets `sslFlags=1` for hostname HTTPS and `sslFlags=0` for hostname-free HTTPS before binding the certificate through HTTP.sys. These repairs require fresh exact-head CI and are **not** promoted to PASS merely by this document.

## State preservation contract

The application uses ASP.NET Core Data Protection persisted under `App_Data/keys` and DPAPI-protected on Windows. The protected first-run database binding is stored at `App_Data/setup/completed.protected`. Secret Vault payloads also depend on Data Protection.

Therefore install/upgrade/repair/default uninstall preserve the complete runtime-owned `App_Data` subtree. Replaceable application binaries are staged and backed up separately. Default uninstall also preserves the validated ownership manifest so reinstall or later explicit purge remains owner-scoped. An explicit uninstall `--purge-state` is the only installer path that intentionally removes preserved state, and it is rejected without matching GSIP ownership.

The package itself rejects embedded `App_Data`, private-key file extensions, protected setup state and environment-specific appsettings that could smuggle credentials or target assumptions into the installer.

## IIS / HTTPS contract

Normal target installation requires administrative elevation, IIS `appcmd.exe`, and the supported ASP.NET Core Hosting Bundle / `AspNetCoreModuleV2`. The setup refuses unowned IIS site/application-pool collisions, creates or repairs only owner-scoped resources using `ApplicationPoolIdentity`, no CLR managed runtime and `AlwaysRunning`, assigns the application pool, and grants that exact app-pool identity Modify access to `App_Data`.

The wizard allows the operator to choose HTTP or HTTPS, port and optional host name. For HTTPS it enumerates currently valid eligible certificates from Local Computer / Personal, requires explicit certificate selection, configures `sslFlags=1` for host-specific SNI, and configures the requested certificate binding. The installer does not embed, export or invent production TLS certificate/private-key material.

Cloud CI proves the implementation contract and isolated Setup EXE lifecycle. Installation against the owner’s actual Windows Server/IIS target, approved production certificate selection, DNS/network/proxy/firewall behavior and any code/release signing remain authorized external/owner evidence and are not fabricated as PASS.

## Historical validated implementation candidate evidence

Validated implementation candidate before the ownership/SNI convergence repair: `4e811f17d707f2e1539c5dab9b40c64b6f498cc1`.

- P15 Installer and Packaging PR run `34436171235`: **SUCCESS**.
- Exact candidate checkout/assertion: **PASS**.
- Static P15 installer acceptance: `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=36`.
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

This historical evidence demonstrates the pre-repair packaging/lifecycle baseline only. It cannot close the newer ownership/SNI/rollback candidate.

## Superseded CI failures and reconciliation history

The implementation history contains real CI failures that were repaired rather than waived. Candidate `18023650aeadbc23e3c476246316f5e1442ccd5f` failed Setup publish because nullable warnings-as-errors flagged system-font dereferences; the implementation was fixed without weakening any gate. Candidate `1bd81efc7270ed056c058fe4f550b2aff19f6ac2` then built successfully, but PowerShell did not synchronously wait for the GUI-subsystem `WinExe` before reading `$LASTEXITCODE`; the acceptance harness was corrected to start the same Setup EXE through `System.Diagnostics.Process`, wait for exit and validate the actual exit code. A later P15 head was stale after PR #76 added the P14 outbound-security verifier; the canonical branch recovered exact current main instead of deleting or bypassing the new P14 gate.

## Not proven by cloud CI

The following remain outside this candidate’s cloud proof and must not be called PASS without authorized target evidence:

- installation on the owner’s actual Windows Server/IIS target;
- selection/binding of the owner-approved production HTTPS certificate on that target;
- production DNS/network/proxy/firewall behavior;
- code signing / release signing when owner-controlled certificate material is required;
- live MOJ UAT/Production evidence already classified in earlier phases.

`--skip-iis` is intentionally an isolated CI mode used to execute the exact Setup EXE lifecycle without claiming target IIS deployment evidence. It bypasses only IIS mutation; ownership/destructive-operation checks remain active. The production wizard/IIS/HTTPS code path remains fail-closed on missing prerequisites, unowned collisions or certificate selection.

## Remaining P15 gate

This document is not P15 closure. The ownership/SNI/rollback repair head supersedes all prior candidates as the PR merge candidate. P15 remains OPEN / ACTIVE until that exact final PR head passes every governed workflow, PR #75 is normally integrated from current exact `main`, every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS, package/installer/hash identity is reconciled to exact post-merge evidence, and canonical closure governance is recorded. P16 remains locked until that gate is satisfied.
