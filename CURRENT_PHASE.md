# CURRENT_PHASE.md

## Canonical current phase

**P16 — Full automated acceptance on the exact release candidate**

Status: **OPEN / READY**

P15 is formally **CLOSED** from exact repository evidence. Its closure-transition PR #77 used exact head `7ad7f07b760f3102bd2776cdfe4e35d2fb427765`, completed **37/37 governed pull-request workflows SUCCESS**, and was normally integrated as exact `main` `48267786e9f0d21934dee339eed32688b6e2d488`. The resulting exact-new-main gate completed **34/34 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0.

P14 and all earlier phases P00-P14 remain **CLOSED** from their preserved integrated evidence. P17 remains **LOCKED** until P16 closes normally.

P13 remains **CLOSED** from its preserved exact implementation provenance: final implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8`, integrated implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`. This provenance is retained explicitly so the closed P13 UI/accessibility baseline remains independently verifiable during later phases.

## P15 formal closure evidence

Canonical implementation unit: `P15::installer-packaging-convergence`.
Canonical implementation branch: `worker/p15-installer-packaging`.
Canonical implementation PR: #75.
Final exact implementation head: `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` — **37/37 governed pull-request workflows SUCCESS**.
Exact integrated implementation main: `d60318da5b19d06df20864083f5a4a78b9792e88` — **34/34 governed push workflows SUCCESS**.
Canonical closure unit: `P15::closure-reconciliation`.
Canonical closure branch: `worker/p15-closure-reconciliation`.
Canonical closure PR: #77.
Final exact closure head: `7ad7f07b760f3102bd2776cdfe4e35d2fb427765` — **37/37 governed pull-request workflows SUCCESS**.
Exact closure main: `48267786e9f0d21934dee339eed32688b6e2d488` — **34/34 governed push workflows SUCCESS**.

The exact integrated P15 package workflow run `34440072557` completed SUCCESS on Windows Server 2025 and recorded `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`, solution/package build with 0 warnings / 0 errors, ownership/destructive-operation negatives, install/upgrade/repair/default-uninstall/reinstall/explicit-purge lifecycle, protected-state preservation, sanitized logging, execution-plan integrity and SHA-256 verification.

Exact integrated P15 artifacts remain bound to source `d60318da5b19d06df20864083f5a4a78b9792e88`:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`;
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`;
- Actions artifact `10137632143` — `P15-Installer-d60318da5b19d06df20864083f5a4a78b9792e88`, archive digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`, size 62,159,500 bytes.

## P16 legal work

P16 is now the sole canonical implementation/acceptance phase. Before any P16 write, workers must perform LIVE STATE FIRST, inspect Issue #1 and all active branches/PRs/claims, and recover any legitimate existing P16 work before creating a new claim or branch.

P16 scope is full automated acceptance on the exact release candidate, preserving every closed P00-P15 contract. Acceptance must exercise the strongest available integrated path across first-run Setup, authentication/login/MFA/account state, RBAC and Default Deny, metadata/service-environment isolation, SecretRef/AuthProfile/token isolation, MOJ contract/auth execution, request history/exports, audit/tamper evidence, administration/health, Arabic RTL and English LTR UI/accessibility parity, security hardening/resilience, and P15 installer/package/lifecycle evidence.

P16 must bind source, automated acceptance and any release-candidate artifact identity to the same exact candidate commit. No historical green run may substitute for a failing or incomplete current exact-head gate. Exact-main regressions and stale integration take priority over new acceptance work.

## P16 exit condition

P16 may close only after its canonical exact candidate has complete automated acceptance, required governed CI is terminal green on the exact head, any required integration is performed normally from current `main`, resulting exact-new-main governed CI is terminal green, and P16 evidence/ledger/project control are reconciled. Only then may P17 become the sole canonical phase.

## Locked future work

**P17 — Final convergence and release closure** is **LOCKED** until P16 is formally closed. P17 is the only phase allowed to make a final-completion claim, and `VERIFIED_FINAL_COMPLETE` is forbidden before actual exact final evidence exists.

## Deferred boundaries

Historical authorized live-UAT evidence that remains unavailable stays `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production -> UAT fallback is allowed.

P15 target-server Windows/IIS deployment, selection/binding of the owner-approved Production HTTPS certificate, Production DNS/network/proxy/firewall behavior and owner-controlled code/release signing remain external/owner evidence and are NOT PASS where unavailable.

Main branch protection remains `OWNER_LAST / NOT PASS`; the live read-back at P16 phase entry shows `main` with `protected=false` and required status-check enforcement off. Repository administration is a separate acceptance boundary and is not converted to PASS by automated product evidence.
