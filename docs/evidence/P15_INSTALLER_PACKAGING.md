# P15 — Windows / IIS Installer and Packaging Evidence

Status: **CLOSED**

## Authority and phase entry

P14 was formally closed only after closure PR #74 was normally integrated and exact resulting `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633` completed **33/33 push workflows SUCCESS**. P15 implementation started from that exact main under tracker Issue #1 lease `P15::installer-packaging-convergence` on `worker/p15-installer-packaging`, with canonical implementation PR #75. PR #75 later recovered post-closure P14 security repair PR #76 and reconciled against exact main `78cfe0433bdb02367ddd1365df57d118fea9257a` without duplicating or reverting that work.

Historical `DEFERRED_EXTERNAL_NOT_PASS`, `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`, target-server/certificate/signing owner evidence, and repository-administration `OWNER_LAST / NOT PASS` classifications remain unchanged.

## Implemented cloud boundary

P15 adds a deterministic Windows Server/IIS deployment layer over the existing GSIP runtime:

- `installer/GSIP.Setup/GSIP.Setup.csproj` builds a .NET 10 Windows x64 self-contained single-file GUI setup executable;
- `installer/GSIP.Setup/WizardEntryPoint.cs` and `InstallerWizard.cs` provide the production wizard flow with Welcome, Prerequisites, Installation, Binding, Review and Finish pages plus Next/Back/Finish navigation;
- `installer/GSIP.Setup/IisBindingConfigurator.cs` provides explicit HTTP/HTTPS binding configuration, eligible Local Computer certificate discovery/selection, deterministic SNI (`sslFlags=1`) for host-specific HTTPS, and hostname-free HTTPS behavior without packaging private keys;
- `installer/GSIP.Setup/Program.cs` is the shared maintenance engine for install, explicit previous-version upgrade, repair, uninstall and explicit `--purge-state`, with a fail-closed GSIP ownership manifest around filesystem/IIS takeover and destructive removal;
- ownership-manifest writes use temporary-file atomic replacement, and failed upgrade/repair restores the exact previous manifest together with rolled-back binaries;
- `installer/GSIP.Setup/SanitizedInstallLog.cs` provides sanitized installer logging without exposing credential/token/API-key assignments;
- successful wizard deployment can launch the protected First-Run Setup endpoint at `/setup`;
- `scripts/build-p15-package.ps1` builds the application, publishes the IIS payload, excludes mutable state and dangerous deployment material, and emits versioned ZIP/EXE artifacts with SHA-256 sidecars;
- `scripts/verify_p15_installer.py` provides executable static contract acceptance including ownership/destructive isolation, atomic manifest rollback, wizard, HTTPS/SNI, logging and lifecycle contracts;
- `.github/workflows/p15-installer-packaging.yml` binds CI to the exact candidate SHA and executes package/lifecycle acceptance on Windows, including negative unowned-root and forged-ownership tests;
- `docs/P15_INSTALLER_RUNBOOK.md` documents prerequisites, lifecycle, ownership, rollback, state preservation, HTTPS/SNI selection and owner/external boundaries.

## Convergence repairs retained in the final implementation

A post-reconciliation audit found a real P15 blocker before merge: the earlier candidate could retarget a pre-existing IIS site/application pool not proven to belong to GSIP, uninstall could delete named IIS resources without installation ownership proof, and `--purge-state` could recursively delete an arbitrary non-drive-root supplied by the caller. The canonical PR #75 line repaired this rather than classifying it as external.

The repaired boundary requires a valid `install-manifest.json` product/architecture/site/application-pool ownership marker for repair, uninstall and purge; rejects a non-empty unowned installation directory; rejects first-install collision with an existing IIS site or application pool; rejects forged site/application-pool identities against an owned installation; preserves the ownership marker on default uninstall; and allows recursive purge only after marker and identity validation. Exact Setup EXE CI carries negative cases proving foreign sentinel content survives rejected install/purge attempts.

A subsequent rollback audit found that updating the manifest before IIS mutation could leave an existing upgrade with a new manifest version after a later IIS failure. The maintenance path now snapshots the exact previous manifest, writes replacement manifests through temporary-file atomic replacement, and restores the prior manifest whenever an existing install/repair rolls back. A brand-new install that has already reached IIS mutation intentionally retains its GSIP marker so the partial footprint remains attributable and recoverable.

The same audit identified host-specific HTTPS SNI drift. The canonical line explicitly sets `sslFlags=1` for hostname HTTPS and `sslFlags=0` for hostname-free HTTPS before the corresponding HTTP.sys certificate binding.

## Final exact-head acceptance

Final implementation head: `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7`.

That exact head completed **37/37 governed pull-request workflows SUCCESS** before normal integration. The strongest P15-specific exact-head push acceptance also completed SUCCESS and proved the repaired ownership, rollback and SNI contract. No failing required check was waived, skipped or weakened to obtain the final candidate.

## Exact integrated main acceptance

PR #75 was normally integrated as exact `main`:

`d60318da5b19d06df20864083f5a4a78b9792e88`

The resulting exact-main gate completed **34/34 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at closure review.

P15 Installer and Packaging exact-main run `34440072557` completed SUCCESS on Windows Server 2025. Its exact checkout/assertion bound the run to `d60318da5b19d06df20864083f5a4a78b9792e88` and recorded:

- `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`;
- solution/package build: **PASS**, 0 warnings / 0 errors;
- `P15_PACKAGE_BUILD=PASS`;
- unowned non-empty install-root rejection: **PASS**;
- unowned purge rejection with foreign sentinel preservation: **PASS**;
- forged site/application-pool ownership rejection: **PASS**;
- clean install: **PASS**;
- explicit previous-version upgrade from synthetic installed version `0.0.9` to `0.1.0`: **PASS**;
- replaceable legacy binary removal during upgrade: **PASS**;
- repair: **PASS**;
- default uninstall with Data Protection/setup-state/ownership-marker preservation: **PASS**;
- reinstall/rediscovery of preserved protected state: **PASS**;
- explicit owner-scoped destructive purge: **PASS**;
- sanitized installer log acceptance: **PASS**;
- execution plan integrity: **PASS**;
- SHA-256 verification: **PASS**.

## Exact integrated artifacts

The package/installer identity below belongs to exact integrated source `d60318da5b19d06df20864083f5a4a78b9792e88`, not to a pre-merge candidate:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`;
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`;
- Actions artifact `10137632143` — `P15-Installer-d60318da5b19d06df20864083f5a4a78b9792e88`;
- artifact archive digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`;
- artifact archive size: 62,159,500 bytes.

## State preservation contract

The application uses ASP.NET Core Data Protection persisted under `App_Data/keys` and DPAPI-protected on Windows. The protected first-run database binding is stored at `App_Data/setup/completed.protected`. Secret Vault payloads also depend on Data Protection.

Install/upgrade/repair/default uninstall preserve the complete runtime-owned `App_Data` subtree. Replaceable application binaries are staged and backed up separately. Default uninstall also preserves the validated ownership manifest so reinstall or later explicit purge remains owner-scoped. An explicit uninstall `--purge-state` is the only installer path that intentionally removes preserved state, and it is rejected without matching GSIP ownership.

The package rejects embedded `App_Data`, private-key file extensions, protected setup state and environment-specific appsettings that could smuggle credentials or target assumptions into the installer.

## IIS / HTTPS contract

Normal target installation requires administrative elevation, IIS `appcmd.exe`, and the supported ASP.NET Core Hosting Bundle / `AspNetCoreModuleV2`. The setup refuses unowned IIS site/application-pool collisions, creates or repairs only owner-scoped resources using `ApplicationPoolIdentity`, no CLR managed runtime and `AlwaysRunning`, assigns the application pool, and grants that exact app-pool identity Modify access to `App_Data`.

The wizard allows the operator to choose HTTP or HTTPS, port and optional host name. For HTTPS it enumerates currently valid eligible certificates from Local Computer / Personal, requires explicit certificate selection, configures `sslFlags=1` for host-specific SNI and `sslFlags=0` for hostname-free HTTPS, then configures the requested certificate binding. The installer does not embed, export or invent production TLS certificate/private-key material.

## Historical implementation provenance

Historical validated candidate `4e811f17d707f2e1539c5dab9b40c64b6f498cc1` passed P15 Installer and Packaging run `34436171235` with 36 static checks, package/lifecycle acceptance and its own candidate artifacts. Those historical hashes and artifact remain provenance only and do not substitute for the exact integrated identities above:

- historical ZIP SHA-256 `def9054f7a7746c04a5790f127836c17cadc35cabeb3922487fe9c8d65f69fa9`;
- historical Setup EXE SHA-256 `c9465545ade663a31fe7d83002e8a88001eb60dedcdc376f81bb60ac1db94d03`;
- historical artifact `10136339172`, digest `sha256:e7ddce03120177943bbfe4949fedac13462949c14858125068f38a1d6ff47867`, size 62,160,131 bytes.

Implementation history also contains real failures that were repaired rather than waived: nullable warnings-as-errors in Setup publishing, GUI-subsystem process-exit handling in the PowerShell lifecycle harness, stale-main reconciliation after PR #76, ownership/destructive-operation isolation, atomic maintenance rollback and HTTPS SNI correctness.

## Not proven by cloud CI

The following remain outside P15 cloud proof and must not be called PASS without authorized target evidence:

- installation on the owner’s actual Windows Server/IIS target;
- selection/binding of the owner-approved production HTTPS certificate on that target;
- production DNS/network/proxy/firewall behavior;
- code signing / release signing when owner-controlled certificate material is required;
- live MOJ UAT/Production evidence already classified in earlier phases.

`--skip-iis` is intentionally an isolated CI mode used to execute the exact Setup EXE lifecycle without claiming target IIS deployment evidence. It bypasses only IIS mutation; ownership/destructive-operation checks remain active. The production wizard/IIS/HTTPS code path remains fail-closed on missing prerequisites, unowned collisions or certificate selection.

## Formal closure-transition evidence

Canonical closure unit `P15::closure-reconciliation` used `worker/p15-closure-reconciliation` and PR #77. Exact closure head `7ad7f07b760f3102bd2776cdfe4e35d2fb427765` completed **37/37 governed pull-request workflows SUCCESS**. PR #77 was then normally integrated from exact implementation main `d60318da5b19d06df20864083f5a4a78b9792e88` as exact closure main:

`48267786e9f0d21934dee339eed32688b6e2d488`

The resulting exact-new-main gate completed **34/34 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0. No required gate was waived, weakened or represented as PASS while incomplete.

All eight closure conditions recorded by `CURRENT_PHASE.md` before the transition are therefore satisfied from exact repository evidence: final implementation head green, normal implementation merge, implementation exact-main green, integrated package/hash identity reconciled, external items preserved as NOT PASS, closure-transition head green, normal closure merge, and exact closure main green.

P15 is formally **CLOSED**. P16 is legally **OPEN / READY** as the sole canonical current phase. P17 remains locked until P16 closes normally.

No target-server, certificate, signing, live-UAT or Production evidence is promoted to PASS by P15 closure. Repository branch protection also remains `OWNER_LAST / NOT PASS` until authorized administration and independent read-back prove otherwise.
