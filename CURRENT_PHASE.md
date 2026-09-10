# CURRENT_PHASE.md

## Canonical current phase

**P15 — Professional Windows/IIS installer and packaging**

Status: **OPEN / ACTIVE** — closure transition; P16 **STAGED / IMPLEMENTATION LOCKED**

P14 remains **CLOSED**. Its governance/evidence closure transition PR #74 was normally integrated from exact closure head `58022cc53c75a986cca3cb119671c213ca1fdf4d` into exact `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633`. The resulting exact-new-main gate completed **33/33 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at formal closure verification.

P13 remains **CLOSED** from its preserved exact implementation provenance: final implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8`, integrated implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`.

P15 is the sole canonical OPEN / ACTIVE phase while its closure reconciliation is being integrated. P16 and P17 are not authorized for production implementation by this transition branch.

## P15 integrated implementation evidence

Canonical implementation unit: `P15::installer-packaging-convergence`.
Canonical implementation branch: `worker/p15-installer-packaging`.
Canonical implementation PR: #75.
Final exact PR head: `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7`.
Exact integrated implementation main: `d60318da5b19d06df20864083f5a4a78b9792e88`.
Canonical closure unit: `P15::closure-reconciliation`.
Canonical closure branch: `worker/p15-closure-reconciliation`.

PR #75 recovered legitimate post-P14 security repair main `78cfe0433bdb02367ddd1365df57d118fea9257a` before final validation. Its final exact head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` completed **37/37 governed pull-request workflows SUCCESS** and was then normally integrated. The resulting exact `main` `d60318da5b19d06df20864083f5a4a78b9792e88` completed **34/34 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at closure review.

The exact integrated P15 package workflow run `34440072557` completed SUCCESS on Windows Server 2025 and proves:

- `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`;
- solution/package build SUCCESS with 0 warnings / 0 errors;
- fail-closed unowned-root install and purge rejection;
- forged site/application-pool ownership rejection;
- clean install -> explicit synthetic `0.0.9` upgrade -> repair -> default uninstall -> reinstall -> explicit owner-scoped purge;
- preservation of Data Protection key state, protected first-run setup state and the ownership marker across non-destructive maintenance;
- sanitized installer logging;
- execution-plan integrity;
- SHA-256 verification.

Exact integrated artifacts for source `d60318da5b19d06df20864083f5a4a78b9792e88`:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`;
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`;
- Actions artifact `10137632143` — `P15-Installer-d60318da5b19d06df20864083f5a4a78b9792e88`, archive digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`, size 62,159,500 bytes.

All lawful cloud-actionable P15 implementation work is therefore terminal from exact-head and exact-integrated-main evidence. This does **not** by itself authorize P16: the P15 closure transition must still pass its own exact-head governed matrix, merge normally from current `main`, and the resulting exact-new-main governed matrix must be terminal SUCCESS.

## P15 safety boundary

The installer does not embed or manufacture SQL credentials, MOJ credentials, API keys, bearer tokens, Civil IDs, personal MOJ data, private keys, production certificates or Production endpoint assumptions.

Microsoft SQL Server remains external and is configured through the existing protected first-run Setup flow. ASP.NET Core Data Protection keys remain persisted under `App_Data/keys` and protected with DPAPI on Windows/IIS. The protected runtime database connection remains under `App_Data/setup/completed.protected`; ordinary package replacement must not destroy either state family.

The cloud implementation supports explicit HTTPS certificate discovery/selection/binding, but installation against the owner’s actual Windows Server/IIS target, selection of the approved production certificate, DNS/network/proxy/firewall behavior, and any code/release signing requiring owner-controlled certificate material remain external/owner evidence and are not fabricated as PASS.

## P15 closure-transition exit condition

P15 may become formally CLOSED only after all of the following are true:

1. final implementation exact head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` remains supported by **37/37** governed PR workflows SUCCESS;
2. implementation PR #75 remains normally integrated as exact implementation main `d60318da5b19d06df20864083f5a4a78b9792e88`;
3. that exact integrated implementation main remains supported by **34/34** governed push workflows SUCCESS;
4. exact integrated package/installer/hash identity remains reconciled to source `d60318da5b19d06df20864083f5a4a78b9792e88`;
5. remaining owner/external target evidence remains explicitly NOT PASS rather than being promoted to PASS;
6. this governance/evidence closure transition passes every governed workflow on its exact head;
7. the closure transition is normally integrated from current `main`;
8. every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS.

Only after items 6–8 are satisfied may P16 become the sole canonical phase.

## Locked future work

**P16 — Full automated acceptance on the exact release candidate** is **STAGED / IMPLEMENTATION LOCKED** until P15 is formally closed by the closure-transition merge plus exact-new-main verification.

**P17 — Final convergence and release closure** is **LOCKED** until P16 is formally closed.

## Deferred boundaries

Historical authorized live-UAT evidence that remains unavailable is still `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production -> UAT fallback is allowed.

Main branch protection remains `OWNER_LAST / NOT PASS`; the latest live read-back before this closure write shows `main` with `protected=false` and required status-check enforcement off. Repository administration is a separate acceptance boundary and is not converted to PASS by P15 cloud evidence.
