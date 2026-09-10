# CURRENT_PHASE.md

## Canonical current phase

**P15 — Professional Windows/IIS installer and packaging**

Status: **OPEN / ACTIVE — closure transition; P16 STAGED / IMPLEMENTATION LOCKED**

P14 remains **CLOSED**. Its closure transition PR #74 integrated to exact `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633`, whose resulting exact-main gate completed **33/33 push workflows SUCCESS**. The later P14 outbound-security repair PR #76 integrated as exact `main` `78cfe0433bdb02367ddd1365df57d118fea9257a` and was recovered into P15 without reverting that closed-baseline repair.

P15 implementation was normally integrated by PR #75 from exact implementation head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` to exact implementation `main` `d60318da5b19d06df20864083f5a4a78b9792e88`. The PR head completed **37/37 governed workflows SUCCESS** before merge. The resulting exact implementation main completed **34/34 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at closure review.

All P15 cloud-actionable implementation work is terminal. P15 nevertheless remains the sole canonical OPEN / ACTIVE phase until this governance/evidence closure transition is normally integrated and its resulting exact-new-main CI is terminal green. This closure work reuses the existing canonical line `worker/p15-installer-packaging`; no replacement branch and no P16 production implementation are authorized by this transition candidate.

## P15 closed implementation boundary

The exact integrated implementation provides:

- deterministic `win-x64` application package for version `0.1.0`;
- self-contained single-file GUI `GSIP-0.1.0-Setup-x64.exe` with Welcome / Prerequisites / Installation / Binding / Review / Finish and Next / Back / Finish navigation;
- Windows Server/IIS prerequisite checks, site/application-pool provisioning and least-privilege `App_Data` ACL;
- fail-closed install-root and IIS ownership isolation, including rejection of non-empty unowned roots, ownerless purge, first-install IIS collisions and forged site/app-pool identities;
- HTTP/HTTPS binding with explicit Local Computer certificate selection and host-specific IIS SNI (`sslFlags=1`);
- install, explicit previous-version upgrade, repair, default uninstall, reinstall and explicit owner-scoped purge;
- atomic ownership-manifest replacement and rollback consistency while preserving `App_Data/keys` and `App_Data/setup/completed.protected`;
- sanitized installer logging and package exclusion gates for secrets, mutable state, private keys and unsafe deployment material;
- versioned ZIP/EXE SHA-256 sidecars and exact-source artifact identity.

Detailed evidence: `docs/evidence/P15_INSTALLER_PACKAGING.md`.

## Exact integrated P15 evidence

Implementation source: exact `main` `d60318da5b19d06df20864083f5a4a78b9792e88`.

- final PR #75 head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7`: **37/37 governed pull-request workflows SUCCESS**, failure=0;
- normal PR #75 merge: `d60318da5b19d06df20864083f5a4a78b9792e88`;
- resulting exact-main gate: **34/34 push workflows SUCCESS**, failure=0, queued=0, in-progress=0;
- exact-main P15 Installer and Packaging run `34440072557`: **SUCCESS**;
- `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`;
- solution/package build: **PASS**, 0 warnings / 0 errors;
- ownership-negative and forged-identity lifecycle acceptance: **PASS**;
- clean install -> synthetic `0.0.9` upgrade -> repair -> default uninstall -> reinstall -> explicit owner-scoped purge: **PASS**;
- state preservation, sanitized logging, execution-plan integrity and SHA-256 verification: **PASS**;
- `GSIP-0.1.0-win-x64.zip` SHA-256: `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`;
- `GSIP-0.1.0-Setup-x64.exe` SHA-256: `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`;
- exact-main Actions artifact `10137632143`, digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`, size 62,159,500 bytes.

## Legal work now

Canonical closure line: `worker/p15-installer-packaging`.

Legal work is limited to P15 closure materialization: reconcile `CURRENT_PHASE.md`, `PROJECT_CONTROL.md`, `docs/TASK_LEDGER.md`, `docs/evidence/P15_INSTALLER_PACKAGING.md`, tracker Issue #1 and exact closure CI evidence. Preserve all integrated runtime/security behavior and all historical deferred classifications.

## P15 exit condition

The implementation side of the P15 exit gate is satisfied on exact `main` `d60318da5b19d06df20864083f5a4a78b9792e88`: final PR head green, normal integration complete, exact-main 34/34 terminal green, exact integrated artifact/hash/source identity recorded, and all cloud-actionable installer/packaging work terminal.

The remaining transition gate is procedural but mandatory:

1. this closure-transition head must complete its governed PR workflow matrix successfully;
2. the closure transition must be normally integrated while based on current exact `main`;
3. every governed workflow on the resulting exact-new-main SHA must be terminal SUCCESS.

Only after all three are true is P15 formally **CLOSED** and P16 legally authorized as the sole canonical implementation phase.

## Staged next phase

**P16 — Full automated acceptance on the exact release candidate** is **STAGED / IMPLEMENTATION LOCKED**.

No P16 implementation may merge before the P15 closure-transition exact-new-main gate above is green.

## Locked future work

**P17 — Final convergence and release closure** remains **LOCKED** until P16 closes normally.

## Deferred boundaries

Historical authorized live-UAT evidence that remains unavailable is still `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production -> UAT fallback is allowed.

Actual owner Windows Server/IIS installation, approved production certificate selection/binding, production DNS/network/proxy/firewall behavior and owner-controlled release signing remain external/owner evidence and are not fabricated as PASS.

Main branch protection remains `OWNER_LAST / NOT PASS` while the last live read-back shows `main` unprotected and no repository ruleset configured. This repository-administration gap is not converted to PASS by P15 cloud evidence.
