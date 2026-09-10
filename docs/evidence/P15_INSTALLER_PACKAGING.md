# P15 — Windows / IIS Installer and Packaging Evidence

Status: **OPEN / ACTIVE implementation candidate — not closure evidence**

## Authority and phase entry

P14 was formally closed only after closure PR #74 was normally integrated and exact resulting `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633` completed **33/33 push workflows SUCCESS**. P15 implementation started from that exact main under tracker Issue #1 lease `P15::installer-packaging-convergence` on `worker/p15-installer-packaging`.

Historical `DEFERRED_EXTERNAL_NOT_PASS`, `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`, and repository-administration `OWNER_LAST / NOT PASS` classifications remain unchanged.

## Implemented cloud boundary

P15 adds a deterministic Windows Server/IIS deployment layer over the existing GSIP runtime:

- `installer/GSIP.Setup/GSIP.Setup.csproj` builds a .NET 10 Windows x64 self-contained single-file setup executable;
- `installer/GSIP.Setup/Program.cs` provides install, repair/upgrade, uninstall and explicit `--purge-state` behavior;
- `scripts/build-p15-package.ps1` builds the application, publishes the framework-dependent IIS payload, excludes mutable state and dangerous deployment material, emits versioned ZIP/EXE artifacts and SHA-256 sidecars;
- `scripts/verify_p15_installer.py` provides executable static contract acceptance;
- `.github/workflows/p15-installer-packaging.yml` binds CI to the exact candidate SHA and executes packaging plus lifecycle acceptance on `windows-latest`;
- `docs/P15_INSTALLER_RUNBOOK.md` documents prerequisites, lifecycle, state preservation and owner/external boundaries.

## State preservation contract

The application uses ASP.NET Core Data Protection persisted under `App_Data/keys` and DPAPI-protected on Windows. The protected first-run database binding is stored at `App_Data/setup/completed.protected`. Secret Vault payloads also depend on Data Protection.

Therefore install/repair/upgrade/default uninstall preserve the complete runtime-owned `App_Data` subtree. Replaceable application binaries are staged and backed up separately. An explicit uninstall `--purge-state` is the only installer path that intentionally removes preserved state.

The package itself rejects embedded `App_Data`, private-key file extensions, protected setup state and environment-specific appsettings that could smuggle credentials or target assumptions into the installer.

## IIS contract

Normal target installation requires administrative elevation, IIS `appcmd.exe`, and the supported ASP.NET Core Hosting Bundle / `AspNetCoreModuleV2`. The setup creates or repairs an application pool using `ApplicationPoolIdentity`, no CLR managed runtime, and `AlwaysRunning`; creates or retargets the IIS site; assigns the application pool; and grants that exact app-pool identity Modify access to `App_Data`.

The installer does not invent production TLS certificate material. Production certificate selection/binding and target-server operational proof remain authorized deployment evidence, not cloud CI proof.

## Exact pre-PR candidate evidence

Exact candidate: `f390d53c88c7d1cfc4ebeb3e5ed893c7f056501e`.

- P00 Build Baseline run `34434136972`: **SUCCESS**.
- P15 Installer and Packaging run `34434136995`: **SUCCESS**.
- Static installer acceptance: `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=22`.
- Build: `P15_PACKAGE_BUILD=PASS`, solution build 0 warnings / 0 errors.
- Real setup executable lifecycle in isolated CI: install -> repair -> default uninstall -> reinstall -> explicit purge: **PASS**.
- Execution plan integrity: **PASS**.
- Artifact SHA-256 verification: **PASS**.
- `GSIP-0.1.0-win-x64.zip` SHA-256: `edcef580ac19097100e474fa03cbf274003bb29b8f900fec479070d13417b7d9`.
- `GSIP-0.1.0-Setup-x64.exe` SHA-256: `0d803908f90217b8ddea8dfece8a69b67d7c5eaf43bb7aff93c9597fac45f3be`.
- Exact-candidate Actions artifact ID `10135572984`; artifact archive digest `sha256:ecef6b1c5b1925cbc22c1a546519eceea6f5203cda5792be27c075036fc3caf7`; size 47,835,068 bytes.

The first candidate `0abe874b6d56ab4f56fabf3988bee5148f87f4ca` failed only a static path-literal predicate before package build. Commit `f390d53c88c7d1cfc4ebeb3e5ed893c7f056501e` corrected that verifier to the actual forward-slash workflow path; no installer assertion or lifecycle requirement was weakened.

## Not proven by cloud CI

The following remain outside this candidate’s cloud proof and must not be called PASS without authorized target evidence:

- installation on the owner’s actual Windows Server/IIS target;
- approved production HTTPS certificate selection and binding;
- production DNS/network/proxy/firewall behavior;
- code signing / release signing when owner-controlled certificate material is required;
- live MOJ UAT/Production evidence already classified in earlier phases.

`--skip-iis` is intentionally an isolated CI mode used to execute the exact Setup EXE lifecycle without claiming target IIS deployment evidence.

## Remaining P15 gate

This document is not P15 closure. P15 remains OPEN / ACTIVE until the final exact PR head is fully green, the implementation is normally integrated, every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS, and the canonical ledger/project-control/closure evidence are reconciled. P16 remains locked until that gate is satisfied.
