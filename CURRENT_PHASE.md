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
Canonical base: exact closed-P14 main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`.

The current implementation candidate adds the production packaging boundary without altering the P00-P14 application/security architecture:

- deterministic `win-x64` application package for version `0.1.0`;
- self-contained single-file `GSIP-0.1.0-Setup-x64.exe` carrying the application payload;
- Windows Server/IIS prerequisite validation and IIS site/application-pool provisioning;
- install, repair/upgrade, default uninstall and explicit destructive purge lifecycle;
- atomic replaceable-binary backup/rollback while preserving runtime-owned `App_Data`;
- preservation of `App_Data/keys` Data Protection material and `App_Data/setup/completed.protected` first-run database binding across repair/default uninstall/reinstall;
- exact app-pool write access limited to mutable `App_Data`;
- package exclusion gates for mutable state, private-key files and unsafe deployment material;
- versioned ZIP/EXE SHA-256 sidecars and exact-candidate artifact manifest;
- executable Windows CI covering build, package, real setup executable lifecycle and hash verification.

Current pre-PR evidence on exact branch head `f390d53c88c7d1cfc4ebeb3e5ed893c7f056501e`:

- P00 Build Baseline run `34434136972`: **SUCCESS**;
- P15 Installer and Packaging run `34434136995`: **SUCCESS**;
- static P15 installer acceptance: **22 checks PASS**;
- package/lifecycle build: **PASS**;
- install -> repair -> default uninstall -> reinstall -> explicit purge acceptance: **PASS**;
- `GSIP-0.1.0-win-x64.zip` SHA-256: `edcef580ac19097100e474fa03cbf274003bb29b8f900fec479070d13417b7d9`;
- `GSIP-0.1.0-Setup-x64.exe` SHA-256: `0d803908f90217b8ddea8dfece8a69b67d7c5eaf43bb7aff93c9597fac45f3be`;
- Actions artifact `10135572984`, digest `sha256:ecef6b1c5b1925cbc22c1a546519eceea6f5203cda5792be27c075036fc3caf7`.

This is implementation-candidate evidence only. P15 is not CLOSED until normal integration and the resulting exact-new-main gate are terminal green, followed by governance/evidence closure reconciliation if required.

## P15 safety boundary

The installer must not embed or manufacture SQL credentials, MOJ credentials, API keys, bearer tokens, Civil IDs, personal MOJ data, private keys, production certificates or Production endpoint assumptions.

Microsoft SQL Server remains external and is configured through the existing protected first-run Setup flow. ASP.NET Core Data Protection keys remain persisted under `App_Data/keys` and protected with DPAPI on Windows/IIS. The protected runtime database connection remains under `App_Data/setup/completed.protected`; ordinary package replacement must not destroy either state family.

Production HTTPS certificate selection/binding, target-server installation proof, and any code/release signing requiring owner-controlled infrastructure or credentials are external/owner evidence and are not fabricated as cloud PASS.

## P15 exit condition

P15 may leave OPEN / ACTIVE only after all lawful cloud-actionable installer/packaging work is terminal and the following are true:

1. the final P15 exact PR head passes every governed workflow required for that candidate;
2. the P15 implementation PR is normally integrated while based on current `main`;
3. every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS;
4. package identity, installer identity, SHA-256 evidence and source SHA are reconciled to exact evidence;
5. any remaining owner/external target evidence is explicitly classified NOT PASS rather than promoted to PASS;
6. closure evidence/ledger/project control are reconciled before legal P16 authorization.

## Locked future work

**P16 — Full automated acceptance on the exact release candidate** is **LOCKED** until P15 is formally closed.

**P17 — Final convergence and release closure** is **LOCKED** until P16 is formally closed.

## Deferred boundaries

Historical authorized live-UAT evidence that remains unavailable is still `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production -> UAT fallback is allowed.

Main branch protection remains `OWNER_LAST / NOT PASS` while the last live read-back shows `main` unprotected and no repository ruleset configured. This repository-administration gap is not converted to PASS by P15 cloud evidence.
