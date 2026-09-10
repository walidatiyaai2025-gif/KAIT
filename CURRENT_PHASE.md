# CURRENT_PHASE.md

## Canonical current phase

**P15 — Professional Windows/IIS installer and packaging**

Status: **OPEN / ACTIVE**

P14 is **CLOSED**. Its governance/evidence closure transition PR #74 was normally integrated from exact closure head `58022cc53c75a986cca3cb119671c213ca1fdf4d` into exact `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633`. The resulting exact-new-main gate completed **33/33 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at formal closure verification.

P13 remains **CLOSED** from its preserved exact implementation provenance: final implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8`, integrated implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`.

P15 is therefore the sole canonical implementation phase. P16 and P17 remain locked.

## Active P15 implementation

Canonical unit: `P15::installer-packaging-convergence`.
Canonical branch: `worker/p15-installer-packaging`.
Canonical PR: #75.
Canonical base: exact closed-P14 main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`.

The current implementation candidate adds the production packaging boundary without altering the P00-P14 application/security architecture:

- deterministic `win-x64` application package for version `0.1.0`;
- self-contained single-file GUI `GSIP-0.1.0-Setup-x64.exe` carrying the application payload;
- professional Windows wizard with Welcome / Prerequisites / Installation / Binding / Review / Finish flow and Next / Back / Finish controls;
- Windows Server/IIS prerequisite validation, IIS site/application-pool provisioning and least-privilege mutable-state ACL;
- HTTP/HTTPS binding configuration with explicit Local Computer certificate selection for HTTPS and no embedded private key material;
- install, explicit previous-version upgrade, repair, default uninstall, reinstall and explicit destructive purge lifecycle;
- atomic replaceable-binary backup/rollback while preserving runtime-owned `App_Data`;
- preservation of `App_Data/keys` Data Protection material and `App_Data/setup/completed.protected` first-run database binding across upgrade/repair/default uninstall/reinstall;
- sanitized installer logging with secret/token/API-key assignment rejection in CI;
- optional Launch First-Run Setup to the installed `/setup` endpoint after successful deployment;
- package exclusion gates for mutable state, private-key files and unsafe deployment material;
- versioned ZIP/EXE SHA-256 sidecars and exact-candidate artifact manifest;
- executable Windows CI covering build, package, real Setup EXE lifecycle, execution-plan integrity and hash verification.

Validated implementation evidence before this governance reconciliation is exact head `4e811f17d707f2e1539c5dab9b40c64b6f498cc1`:

- P15 Installer and Packaging PR run `34436171235`: **SUCCESS**;
- static P15 installer acceptance: `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=36`;
- solution/package build: **PASS**, 0 warnings / 0 errors;
- clean install -> explicit `0.0.9` upgrade -> repair -> default uninstall -> reinstall -> explicit purge: **PASS**;
- Data Protection key state and protected first-run setup state preservation across upgrade/repair/default uninstall/reinstall: **PASS**;
- sanitized installer log acceptance: **PASS**;
- execution-plan integrity: **PASS**;
- artifact SHA-256 verification: **PASS**;
- `GSIP-0.1.0-win-x64.zip` SHA-256: `def9054f7a7746c04a5790f127836c17cadc35cabeb3922487fe9c8d65f69fa9`;
- `GSIP-0.1.0-Setup-x64.exe` SHA-256: `c9465545ade663a31fe7d83002e8a88001eb60dedcdc376f81bb60ac1db94d03`;
- Actions artifact `10136339172`, digest `sha256:e7ddce03120177943bbfe4949fedac13462949c14858125068f38a1d6ff47867`, size 62,160,131 bytes.

The reconciliation commit that contains this document supersedes `4e811f17...` as the PR merge candidate. P15 remains OPEN / ACTIVE until that final exact PR head passes every governed workflow, PR #75 is normally integrated from current `main`, and the resulting exact-new-main gate is terminal green. No pre-merge evidence is promoted to exact-main closure evidence.

## P15 safety boundary

The installer must not embed or manufacture SQL credentials, MOJ credentials, API keys, bearer tokens, Civil IDs, personal MOJ data, private keys, production certificates or Production endpoint assumptions.

Microsoft SQL Server remains external and is configured through the existing protected first-run Setup flow. ASP.NET Core Data Protection keys remain persisted under `App_Data/keys` and protected with DPAPI on Windows/IIS. The protected runtime database connection remains under `App_Data/setup/completed.protected`; ordinary package replacement must not destroy either state family.

The cloud implementation supports explicit HTTPS certificate discovery/selection/binding, but installation against the owner’s actual Windows Server/IIS target, selection of the approved production certificate, DNS/network/proxy/firewall behavior, and any code/release signing requiring owner-controlled certificate material remain external/owner evidence and are not fabricated as cloud PASS.

## P15 exit condition

P15 may leave OPEN / ACTIVE only after all lawful cloud-actionable installer/packaging work is terminal and the following are true:

1. the final P15 exact PR head passes every governed workflow required for that candidate;
2. PR #75 is normally integrated while based on current `main`;
3. every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS;
4. package identity, installer identity, SHA-256 evidence and source SHA are reconciled to exact post-merge evidence;
5. any remaining owner/external target evidence is explicitly classified NOT PASS rather than promoted to PASS;
6. closure evidence/ledger/project control are reconciled before legal P16 authorization.

## Locked future work

**P16 — Full automated acceptance on the exact release candidate** is **LOCKED** until P15 is formally closed.

**P17 — Final convergence and release closure** is **LOCKED** until P16 is formally closed.

## Deferred boundaries

Historical authorized live-UAT evidence that remains unavailable is still `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production -> UAT fallback is allowed.

Main branch protection remains `OWNER_LAST / NOT PASS` while the last live read-back shows `main` unprotected and no repository ruleset configured. This repository-administration gap is not converted to PASS by P15 cloud evidence.
