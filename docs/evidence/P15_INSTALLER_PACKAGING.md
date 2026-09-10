# P15 — Windows / IIS Installer and Packaging Evidence

Status: **CLOSED FROM EXACT IMPLEMENTATION EVIDENCE / CLOSURE TRANSITION PENDING**

## Authority and phase entry

P14 was formally closed after closure PR #74 integrated and exact resulting `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633` completed **33/33 push workflows SUCCESS**. P15 then became canonical under tracker Issue #1 unit `P15::installer-packaging-convergence` on `worker/p15-installer-packaging`, PR #75. The line later recovered legitimate P14 outbound-security repair PR #76 from exact `main` `78cfe0433bdb02367ddd1365df57d118fea9257a` without duplicating or reverting it.

Historical `DEFERRED_EXTERNAL_NOT_PASS`, `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`, and repository-administration `OWNER_LAST / NOT PASS` classifications remain unchanged.

## Implemented cloud boundary

P15 adds a deterministic Windows Server/IIS deployment layer over the existing GSIP runtime:

- `installer/GSIP.Setup/GSIP.Setup.csproj` builds a .NET 10 Windows x64 self-contained single-file GUI setup executable;
- `installer/GSIP.Setup/WizardEntryPoint.cs` and `InstallerWizard.cs` provide Welcome, Prerequisites, Installation, Binding, Review and Finish pages with Next/Back/Finish navigation;
- `installer/GSIP.Setup/IisBindingConfigurator.cs` provides explicit HTTP/HTTPS binding, eligible Local Computer certificate discovery/selection, and deterministic SNI (`sslFlags=1`) for host-specific HTTPS without packaging private keys;
- `installer/GSIP.Setup/Program.cs` provides install, explicit previous-version upgrade, repair, uninstall and owner-scoped `--purge-state` behind a fail-closed filesystem/IIS ownership manifest;
- ownership-manifest writes use temporary-file atomic replacement and failed existing-install upgrade/repair restores the exact previous manifest with rolled-back binaries;
- `installer/GSIP.Setup/SanitizedInstallLog.cs` prevents credential/token/API-key assignments from being exposed by normal installer logging;
- successful wizard deployment can launch protected First-Run Setup at `/setup`;
- `scripts/build-p15-package.ps1` excludes mutable state and dangerous deployment material and emits versioned ZIP/EXE artifacts with SHA-256 sidecars;
- `scripts/verify_p15_installer.py` statically gates ownership/destructive isolation, atomic rollback, wizard, HTTPS/SNI, logging and lifecycle contracts;
- `.github/workflows/p15-installer-packaging.yml` executes exact-candidate package/lifecycle acceptance on Windows, including unowned-root and forged-ownership negative tests;
- `docs/P15_INSTALLER_RUNBOOK.md` documents prerequisites, lifecycle, ownership, rollback, state preservation, HTTPS/SNI and external evidence boundaries.

## Convergence repairs retained in the final implementation

A post-reconciliation audit found a real destructive-operation blocker: the earlier candidate could retarget a pre-existing IIS site/application pool without GSIP ownership proof, uninstall could delete named IIS resources without installation ownership proof, and `--purge-state` could recursively delete an arbitrary non-drive-root path. The canonical PR #75 line repaired this instead of waiving it. Repair/uninstall/purge require a valid `install-manifest.json`; non-empty unowned roots and first-install IIS collisions fail closed; forged site/application-pool identities are rejected; default uninstall preserves the ownership marker; and recursive purge requires matching ownership.

A subsequent rollback audit found that an existing upgrade could leave a newly advanced manifest if a later IIS step failed after binaries were rolled back. The final maintenance path snapshots the exact previous manifest, writes through atomic temporary-file replacement, and restores the old manifest whenever an existing install/repair rolls back.

The same audit identified hostname HTTPS SNI drift. The final code explicitly sets IIS `sslFlags=1` for hostname HTTPS and `sslFlags=0` for hostname-free HTTPS before the matching HTTP.sys certificate binding.

## State preservation contract

ASP.NET Core Data Protection state is persisted under `App_Data/keys`; the protected first-run database binding is under `App_Data/setup/completed.protected`; Secret Vault payloads depend on Data Protection. Install/upgrade/repair/default uninstall therefore preserve the runtime-owned `App_Data` subtree. Default uninstall also preserves the validated ownership manifest. Explicit `--purge-state` is the only installer path that intentionally removes preserved state and is rejected without matching ownership.

The package rejects embedded `App_Data`, private-key file extensions, protected setup state and environment-specific deployment material that could smuggle credentials or target assumptions.

## IIS / HTTPS contract

Normal target installation requires administrator elevation, IIS `appcmd.exe`, and the supported ASP.NET Core Hosting Bundle / `AspNetCoreModuleV2`. The setup refuses unowned IIS site/application-pool collisions, uses `ApplicationPoolIdentity`, no CLR managed runtime and `AlwaysRunning`, and grants the selected app-pool identity Modify access to `App_Data`.

The wizard allows HTTP/HTTPS, port and optional host name. HTTPS enumerates currently valid eligible certificates from Local Computer / Personal and requires explicit certificate selection. Host-specific HTTPS uses SNI. No TLS private key or production certificate is embedded or invented.

## Final exact-head acceptance before merge

Final implementation head: `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7`.

- full governed PR matrix: **37/37 terminal SUCCESS**, failure=0;
- P15 Installer and Packaging PR run `34438884799`: **SUCCESS**;
- P15 Installer and Packaging exact-head push run `34438881903`: **SUCCESS**;
- static acceptance: `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`;
- solution/package build: **PASS**, 0 warnings / 0 errors;
- unowned-root install and ownerless purge rejection: **PASS**;
- forged site/application-pool identity rejection: **PASS**;
- clean install -> synthetic `0.0.9` upgrade -> repair -> default uninstall -> reinstall -> explicit owner-scoped purge: **PASS**;
- Data Protection/setup-state/ownership-marker preservation: **PASS**;
- sanitized log acceptance, execution-plan integrity and SHA-256 verification: **PASS**.

These exact-head results were used only as the pre-merge gate; they were not promoted to exact-main closure evidence.

## Exact integrated-main acceptance

PR #75 was normally merged with expected head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7`. Exact integrated implementation `main` is:

`d60318da5b19d06df20864083f5a4a78b9792e88`

The resulting exact-main push matrix completed **34/34 terminal SUCCESS**, failure=0, queued=0 and in-progress=0.

Exact-main P15 Installer and Packaging run `34440072557` completed **SUCCESS** on that same source SHA and produced:

- `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`;
- solution/package build **PASS**, 0 warnings / 0 errors;
- unowned-root and ownerless-purge negative protection **PASS**;
- forged site/application-pool ownership isolation **PASS**;
- install -> synthetic `0.0.9` upgrade -> repair -> default uninstall -> reinstall -> explicit owner-scoped purge **PASS**;
- `App_Data/keys`, `App_Data/setup/completed.protected` and default-uninstall ownership marker preservation **PASS**;
- sanitized installer logging **PASS**;
- execution-plan integrity **PASS**;
- artifact SHA-256 verification **PASS**.

Exact integrated artifacts:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`;
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`;
- Actions artifact ID `10137632143` — archive digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`, size 62,159,500 bytes.

This is the authoritative P15 implementation closure evidence. Historical pre-repair artifacts remain provenance only.

## Historical implementation provenance

Earlier candidate `4e811f17d707f2e1539c5dab9b40c64b6f498cc1` passed P15 run `34436171235` with 36 static checks, a 0-warning/0-error build, lifecycle/state-preservation/log/hash acceptance and artifacts:

- ZIP SHA-256 `def9054f7a7746c04a5790f127836c17cadc35cabeb3922487fe9c8d65f69fa9`;
- Setup EXE SHA-256 `c9465545ade663a31fe7d83002e8a88001eb60dedcdc376f81bb60ac1db94d03`;
- artifact `10136339172`, digest `sha256:e7ddce03120177943bbfe4949fedac13462949c14858125068f38a1d6ff47867`, size 62,160,131 bytes.

That evidence does not supersede the exact integrated-main values above.

## Superseded CI failures and reconciliation history

Candidate `18023650aeadbc23e3c476246316f5e1442ccd5f` failed Setup publish because nullable warnings-as-errors flagged system-font dereferences; the implementation was fixed without weakening a gate. Candidate `1bd81efc7270ed056c058fe4f550b2aff19f6ac2` built successfully, but the PowerShell harness did not synchronously wait for the GUI-subsystem `WinExe`; the harness was corrected to execute the same Setup EXE through `System.Diagnostics.Process`, wait for exit and validate the actual exit code. A later stale P15 candidate recovered exact main after P14 outbound-security repair PR #76 instead of deleting or bypassing the stronger P14 verifier.

## Not proven by cloud CI

The following remain outside cloud proof and are **NOT PASS** where authorized target evidence is unavailable:

- installation on the owner’s actual Windows Server/IIS target;
- selection/binding of the owner-approved production HTTPS certificate on that target;
- production DNS/network/proxy/firewall behavior;
- code/release signing when owner-controlled certificate material is required;
- live MOJ UAT/Production evidence already classified in earlier phases.

`--skip-iis` is an isolated CI mode for exercising the exact Setup EXE lifecycle without pretending to prove the owner’s IIS server. It bypasses only IIS mutation; ownership/destructive checks remain active. The production IIS/HTTPS path remains fail-closed on missing prerequisites, unowned collisions or missing certificate selection.

## Closure-transition gate

P15 cloud-actionable implementation is terminal and exact integrated-main evidence is green. Formal phase closure still requires the governance/evidence-only transition carrying this reconciliation to:

1. pass every governed workflow on its exact transition head;
2. be normally integrated while based on current exact `main`;
3. pass every governed workflow on the resulting exact-new-main SHA.

Only after those three transition conditions are true is P15 formally **CLOSED** and P16 implementation legally authorized. No owner/external NOT PASS item is converted to PASS by this transition.
