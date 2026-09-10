# CURRENT_PHASE.md

## Canonical current phase

**P16 — Full automated acceptance on the exact release candidate**

Status: **CLOSURE TRANSITION CANDIDATE**. P17 is **STAGED / IMPLEMENTATION LOCKED** until this reconciliation is normally integrated and every governed workflow on the resulting exact-new-main is terminal SUCCESS.

P00-P15 remain formally CLOSED from preserved integrated evidence. P16 cloud-actionable implementation, regression recovery, exact release-candidate packaging, runtime/browser acceptance, backup/restore rehearsal, bounded performance smoke and UI parity evidence are terminal from exact repository evidence on the current accepted integrated source. This branch performs governance/evidence reconciliation only and does not authorize P17 implementation before its merge and exact-new-main gate complete.

## Preserved P13 closed baseline provenance

P13 remains **CLOSED** from its exact accepted implementation provenance: final P13 implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8` integrated as exact implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by its normally integrated closure transition. This provenance remains explicit so the closed P13 bilingual UI/accessibility baseline can continue to be independently verified by post-P13 governed CI.

## P16 implementation and regression-recovery provenance

Canonical implementation unit: `P16::full-acceptance-release-candidate`.  
Canonical implementation branch: `worker/p16-full-acceptance-continuation`.  
Canonical implementation PR: #80.  
Final exact implementation head: `e655dc4d0effb4f962d3e97e4a0230471238ba55` — **38/38 governed pull-request workflows SUCCESS**.  
Original exact integrated implementation main: `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85` — **35/35 governed push workflows SUCCESS**.

The subsequent 0.1.1 API129 UAT shortcut was later found to contradict the repository-authoritative token-exchange contract. That exact-main regression was recovered on the existing hotfix line rather than bypassed or duplicated. PR #85 final corrected head `0e767d4f355e403b70602a7645cd7ffba25b3b43` completed **40/40 governed pull-request workflows SUCCESS** and was normally merged with an expected-head guard.

The current accepted exact integrated P16 source is:

`8863b4b8151ebb08e98fdb225ea5418c5297eb5c` — Merge PR #85: restore evidence-bound MOJ token exchange.

That exact main completed **36/36 governed push workflows SUCCESS**, with no queued, in-progress, failed, cancelled, timed-out or action-required workflow in the terminal snapshot.

The corrected API129 UAT contract is exact-scope `x-api-key + username + password` -> `POST /genToken` -> transient Bearer token from `data` -> target request with the same `x-api-key + Authorization: Bearer`. Optional empty-credential placeholders and broad cross-service MOJ authentication convergence were removed. Production remains independently configured and fail-closed; Production -> UAT fallback is forbidden.

P04 Default Deny remains authoritative: global `Services.Execute` is not sufficient to bypass the per-service permission matrix. No automatic System Administrator per-service privilege expansion is introduced by this recovery.

## Exact current P16 acceptance

Exact-main P16 workflow run: `34456867989`  
Exact source SHA: `8863b4b8151ebb08e98fdb225ea5418c5297eb5c`  
Version: `0.1.2`  
Target runtime: `win-x64`  
Framework: `net10.0` / pinned SDK `10.0.400`

All required P16 jobs completed SUCCESS:

- `static-contract`;
- `release-candidate-package`;
- `runtime-ui-recovery`;
- aggregate `p16-gate`.

Acceptance includes exact-candidate restore/build and contracts, preservation of P14/P15 controls, Permissions, Service Execution, Audit and Dashboard browser/runtime acceptance, Arabic RTL and English LTR desktop+narrow UI parity consolidation, SQL Server LocalDB plus protected App_Data backup/restore rehearsal, bounded localhost performance smoke, execution-plan integrity and exact-candidate release packaging.

## Exact current release-candidate identity

- `GSIP-0.1.2-win-x64.zip` — SHA-256 `b4971d31b11f3c1c6a6afba9026afd6865b7c006063eb59ffbdc9e2182efd426`;
- `GSIP-0.1.2-Setup-x64.exe` — SHA-256 `7a5de1c18bf9b84bd052d855d089f51ffdae175d40c872b574a68892fba99ff0`;
- Actions artifact `10143946003` — `P16-Release-Candidate-8863b4b8151ebb08e98fdb225ea5418c5297eb5c`, size 62,185,768 bytes, archive digest `sha256:b7c8f175b1669323917bbfa6b557076ce8b2d2c611af7009b13f83e4282429d6`;
- Actions artifact `10144204144` — `P16-Runtime-UI-Recovery-8863b4b8151ebb08e98fdb225ea5418c5297eb5c`, size 3,482,259 bytes, archive digest `sha256:4b5564603f8c00c5d07357dd206647f0f4dabfa5b04cd8fcc931bc1ab5a4965d`.

Earlier P16 0.1.0 and 0.1.1 artifacts remain historical provenance only and are superseded for closure by the exact 0.1.2 same-SHA evidence above.

## P16 closure transition

Canonical closure unit: `P16::closure-reconciliation`.  
Canonical closure branch: `worker/p16-closure-reconciliation`.

This closure line is governance/evidence-only. It must not implement P17 functionality or weaken any existing gate.

P16 may be recorded as formally CLOSED and P17 may become the sole canonical phase only after all of the following are proven:

1. this reconciliation PR final exact head completes every governed pull-request workflow SUCCESS;
2. the reconciliation PR is normally integrated from exact current `main` without bypass or stale-base merge;
3. every governed push workflow on the resulting exact-new-main is terminal SUCCESS;
4. Issue #1 is updated with the exact closure-transition head, merge SHA and exact-new-main CI evidence.

Until those conditions are proven, P17 implementation remains forbidden.

## Locked future work

**P17 — Final convergence and release closure** remains **STAGED / IMPLEMENTATION LOCKED**. `VERIFIED_FINAL_COMPLETE` remains forbidden until actual P17 same-commit/same-artifact final evidence satisfies `docs/FINAL_ACCEPTANCE_CRITERIA.md`.

## Deferred boundaries

Authorized live MOJ UAT evidence requiring owner-controlled credentials or approved personal test data remains `DEFERRED_EXTERNAL_NOT_PASS` where unavailable. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`; Production details are never inferred from UAT and Production -> UAT fallback is forbidden.

Target-server Windows/IIS deployment, owner-approved Production HTTPS certificate selection/binding, Production DNS/network/proxy/firewall behavior, Production performance/capacity evidence and owner-controlled code/release signing remain external/owner evidence and are NOT PASS where unavailable.

Main branch protection remains `OWNER_LAST / NOT PASS`; live read-back at this reconciliation reports `main` with `protected=false` and required status-check enforcement off. Repository administration is a separate acceptance boundary and is not converted to PASS by automated product evidence.
