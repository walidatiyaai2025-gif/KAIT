# CURRENT_PHASE.md

## Canonical current phase

**P16 — Full automated acceptance on the exact release candidate**

Status: **OPEN / ACTIVE — closure reconciliation**. P17 is **STAGED / IMPLEMENTATION LOCKED**.

P00-P15 remain formally CLOSED from preserved integrated evidence. P16 implementation plus the post-implementation 0.1.1 regression repair are terminal from exact repository evidence, but P16 itself remains OPEN until this closure transition is normally integrated and the resulting exact-new-main governed CI is terminal green.

## Preserved P13 closed baseline provenance

P13 remains **CLOSED** from its exact accepted implementation provenance: final P13 implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8` integrated as exact implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by its normally integrated closure transition. This exact provenance remains explicit so the closed P13 bilingual UI/accessibility baseline can continue to be independently verified by post-P13 governed CI.

## P16 accepted implementation and regression-recovery evidence

Canonical implementation unit: `P16::full-acceptance-release-candidate`.
Canonical implementation branch: `worker/p16-full-acceptance-continuation`.
Canonical implementation PR: #80.
Final exact implementation head: `e655dc4d0effb4f962d3e97e4a0230471238ba55` — **38/38 governed pull-request workflows SUCCESS**.
Original exact integrated implementation main: `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85` — **35/35 governed push workflows SUCCESS**.

A later exact-main regression/operability repair was required for API 129 UAT configuration. Stale PR #82 was closed without merge and replayed from the then-current exact main as canonical hotfix PR #83. PR #83 final exact head `fd2ca1491db0a614ac93292a2dcfe8c656018d09` completed **39/39 governed pull-request workflows SUCCESS** and was normally merged with an expected-head guard.

The current accepted exact integrated P16 source is therefore:

`252e3422dd31bc9eb58b64c1019800f5b50b6e51` — Merge PR #83: secure API129 UAT 0.1.1 hotfix.

That exact main completed **36/36 governed push workflows SUCCESS**, with queued=0, in-progress=0 and failure=0 at terminal verification.

Exact-main P16 workflow run `34450118608` completed all four jobs SUCCESS:

- `static-contract`;
- `release-candidate-package`;
- `runtime-ui-recovery`;
- aggregate `p16-gate`.

Acceptance includes exact-candidate restore/build and contracts, P14/P15 preservation, Permissions, Service Execution, Audit and Dashboard browser/runtime acceptance, Arabic RTL and English LTR desktop+narrow UI parity consolidation, SQL Server LocalDB plus protected App_Data backup/restore rehearsal, bounded localhost performance smoke, execution-plan integrity and exact-candidate release packaging.

## Exact current release-candidate identity

Source SHA: `252e3422dd31bc9eb58b64c1019800f5b50b6e51`  
Version: `0.1.1`  
Target runtime: `win-x64`  
Framework: `net10.0` / pinned SDK `10.0.400`

- `GSIP-0.1.1-win-x64.zip` — SHA-256 `a9f99baad0ef4acd9ce27e85745628ed0f9167c9f4df8208a2c2494d30fcac01`;
- `GSIP-0.1.1-Setup-x64.exe` — SHA-256 `4bfac9c2a9a7603057a5ef76eab442eb0ae0b8676d5b68a10d6604d9d8a632b7`;
- Actions artifact `10141308643` — `P16-Release-Candidate-252e3422dd31bc9eb58b64c1019800f5b50b6e51`, size 62,175,280 bytes, archive digest `sha256:8729cd521783fda8f084833cb221566abaeea7e7f153035e889f1a9a40ae96a3`;
- Actions artifact `10141410049` — `P16-Runtime-UI-Recovery-252e3422dd31bc9eb58b64c1019800f5b50b6e51`, size 3,481,261 bytes, archive digest `sha256:5ba6df9ea5d4ce59570250d96a9c45647d4f8550b61429d89d55c6e9145d3317`.

The earlier P16 0.1.0 package/artifact identities remain historical evidence for pre-hotfix main `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85`; they are superseded for P16 closure by the 0.1.1 same-SHA evidence above.

## P16 closure transition

Canonical closure unit: `P16::closure-reconciliation`.
Canonical closure branch: `worker/p16-closure-reconciliation`.
Canonical closure PR: #84.

This closure line is governance/evidence-only. It must not implement P17 functionality or weaken any existing gate.

P16 remains OPEN/ACTIVE until all of the following are proven:

1. PR #84 final exact head completes every governed pull-request workflow SUCCESS;
2. PR #84 is normally integrated from the then-current exact `main` without bypass or stale-base merge;
3. every governed push workflow on the resulting exact-new-main is terminal SUCCESS;
4. Issue #1 and canonical governance/ledger state are reconciled to that resulting exact main.

Only after those conditions are proven may P16 become formally CLOSED and P17 become the sole canonical phase.

## Locked future work

**P17 — Final convergence and release closure** remains **STAGED / IMPLEMENTATION LOCKED**. `VERIFIED_FINAL_COMPLETE` remains forbidden until actual P17 same-commit/same-artifact final evidence satisfies `docs/FINAL_ACCEPTANCE_CRITERIA.md`.

## Deferred boundaries

Authorized live MOJ UAT evidence requiring owner-controlled credentials or approved personal test data remains `DEFERRED_EXTERNAL_NOT_PASS` where unavailable. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`; Production details are never inferred from UAT and Production -> UAT fallback is forbidden.

Target-server Windows/IIS deployment, owner-approved Production HTTPS certificate selection/binding, Production DNS/network/proxy/firewall behavior, Production performance/capacity evidence and owner-controlled code/release signing remain external/owner evidence and are NOT PASS where unavailable.

Main branch protection remains `OWNER_LAST / NOT PASS`; live read-back at this reconciliation reports `main` with `protected=false` and required status-check enforcement off. Repository administration is a separate acceptance boundary and is not converted to PASS by automated product evidence.
