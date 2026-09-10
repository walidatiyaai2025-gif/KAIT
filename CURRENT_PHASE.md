# CURRENT_PHASE.md

## Canonical current phase

**P16 — Full automated acceptance on the exact release candidate**

Status: **OPEN / ACTIVE** — closure reconciliation; P17 **STAGED / IMPLEMENTATION LOCKED**

P15 and all earlier phases P00-P15 are formally **CLOSED** from preserved exact integrated evidence. P16 remains the sole canonical OPEN/ACTIVE phase until this closure transition is normally integrated and the resulting exact-new-main governed CI is terminal green.

P13 remains **CLOSED** from its preserved exact implementation provenance: final implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8`, integrated implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`. This provenance remains explicit so the closed P13 UI/accessibility baseline stays independently verifiable during later phases.

## P15 formal closure / P16 legal entry evidence

Canonical P15 implementation PR #75 final exact head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` completed **37/37 governed pull-request workflows SUCCESS** and integrated as exact implementation main `d60318da5b19d06df20864083f5a4a78b9792e88`, which completed **34/34 governed push workflows SUCCESS**.

P15 closure PR #77 final exact head `7ad7f07b760f3102bd2776cdfe4e35d2fb427765` completed **37/37 governed pull-request workflows SUCCESS** and integrated as exact closure main `48267786e9f0d21934dee339eed32688b6e2d488`, which completed **34/34 governed push workflows SUCCESS**.

P15 phase-entry materialization PR #78 final exact head `462a32f6a72e913b1c7ac2abe83aef98f0d97415` completed **37/37 governed pull-request workflows SUCCESS** and integrated as exact `main` `3c995a3667d2f82637a6076ecb62c80326fd15d1`. That exact-new-main phase-entry gate completed **34/34 governed push workflows SUCCESS**, failure=0, queued=0 and in-progress=0. P16 implementation was therefore legally authorized from that exact green main.

## P16 implementation evidence

Canonical implementation unit: `P16::full-acceptance-release-candidate`.
Canonical recovered implementation branch: `worker/p16-full-acceptance-continuation`.
Canonical implementation PR: #80.
Final exact implementation head: `e655dc4d0effb4f962d3e97e4a0230471238ba55` — **38/38 governed pull-request workflows SUCCESS**.
Exact integrated implementation main: `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85` — **35/35 governed push workflows SUCCESS**, with no failure, queued, in-progress, cancelled or skipped workflows in the terminal snapshot.

Exact-main P16 workflow run `34446771730` completed all four jobs SUCCESS: `static-contract`, `release-candidate-package`, `runtime-ui-recovery`, and aggregate `p16-gate`. Acceptance includes exact-candidate restore/build and contract validation; preserved P14 security and P15 installer contracts; Permissions, Service Execution, Audit and Dashboard browser/runtime acceptance; Arabic RTL and English LTR desktop+narrow UI parity consolidation; SQL Server LocalDB plus protected App_Data backup/restore rehearsal; bounded localhost performance smoke; execution-plan integrity; and exact-candidate release packaging.

Exact integrated P16 release-candidate identities bound to source `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85`:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `bc6be07796d13c0d4158a229342649ac53de565b80dbd41f9cfb049f05db0ed5`;
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `80e6a6658a81dde78590aa99caf561ecf94f2e0f67c5be56cfe912a9faa7d3f7`;
- Actions artifact `10140021151` — `P16-Release-Candidate-75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85`, archive digest `sha256:93ac5baf64d873ca890dd39ebc43ee5b4667ab67ac99f7d8749f2c5ae6739315`, size 62,160,245 bytes;
- Actions artifact `10140125405` — `P16-Runtime-UI-Recovery-75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85`, archive digest `sha256:0d896d6b0de2ca46a3ae983d97d497eb1a9b0702cf6dbda7e408b87f851df55d`, size 3,468,847 bytes.

The earlier `worker/p16-full-acceptance` line is a stale competing governance line. Its unique work is governance-only and it contains no acceptance implementation missing from the canonical continuation. It must not be merged over the accepted P16 implementation.

## P16 closure transition

Canonical closure unit: `P16::closure-reconciliation`.
Canonical closure branch: `worker/p16-closure-reconciliation`.

All cloud-actionable P16 implementation/acceptance work is terminal from exact implementation evidence above. This branch is limited to governance/evidence reconciliation and must not implement P17 production/final-convergence work.

P16 remains OPEN/ACTIVE until all of the following are true:

1. this closure PR final exact head completes every governed pull-request workflow with SUCCESS;
2. the closure PR is normally integrated from the then-current exact `main` without bypass or stale-base merge;
3. every governed push workflow on the resulting exact-new-main is terminal SUCCESS;
4. closure evidence and tracker state are reconciled to that resulting exact main.

Only after all four conditions are evidenced may P16 become formally CLOSED and P17 become the sole canonical phase.

## Locked future work

**P17 — Final convergence and release closure** is **STAGED / IMPLEMENTATION LOCKED**. P17 may not begin implementation merely because P16 implementation passed; it requires the P16 closure transition itself to integrate and the resulting exact-new-main gate to be terminal green.

P17 is the only phase allowed to make a final-completion claim. `VERIFIED_FINAL_COMPLETE` remains forbidden before actual P17 same-commit/same-artifact final evidence exists.

## Deferred boundaries

Historical authorized live-UAT evidence that remains unavailable stays `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production -> UAT fallback is allowed.

Target-server Windows/IIS deployment, selection/binding of the owner-approved Production HTTPS certificate, Production DNS/network/proxy/firewall behavior, Production performance/capacity evidence and owner-controlled code/release signing remain external/owner evidence and are NOT PASS where unavailable.

Main branch protection remains `OWNER_LAST / NOT PASS`; the latest live read-back at P16 closure review reports `main` with `protected=false` and required status-check enforcement off. Repository administration is a separate acceptance boundary and is not converted to PASS by automated product evidence.
