# CURRENT_PHASE.md

## Canonical current phase

**P17 — Final convergence and release closure**

Status: **CLOSED**

P17 is formally **CLOSED** from repository/cloud implementation and closure evidence proposed by this exact reconciliation line. Product **IMPLEMENTATION LOCKED** remains in force: no product/runtime implementation is duplicated here. The P17 closure-transition PR #93 exact head `7e4fb7264c346f4f5310ad4b44c07fe51a07c151` completed **41/41 governed pull-request workflows SUCCESS**, merged normally with expected-head protection, and produced exact main `72d1ea92e14978b08a1b0ed1727716001ebf1390`, which completed **37/37 governed push workflows SUCCESS**. This formal-closure reconciliation becomes canonical only after its own exact head passes the complete governed pull-request matrix, merges normally with expected-head protection, and the resulting exact-new-main passes the complete governed push matrix.

P00-P16 are formally **CLOSED** from preserved exact integrated evidence, and P16 is formally **CLOSED**. P17 being CLOSED does **not** terminate project-wide convergence: any later cloud-actionable gap anywhere in P00-P17 or shared infrastructure must still be repaired, and final cloud convergence requires two consecutive full LIVE-state zero-gap sweeps on an unchanged terminal-green exact `main`. No owner-only or external evidence is converted to PASS.

## Preserved P13 closed baseline provenance

P13 remains **CLOSED** from its exact accepted implementation provenance: final P13 implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8` integrated as exact implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`. This provenance remains explicit so the closed P13 bilingual UI/accessibility baseline stays independently verifiable during and after P17.

## Preserved P14 and P15 closed baseline provenance

P14 remains **CLOSED** from its accepted implementation and closure provenance: final P14 implementation head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` integrated as implementation main `1f1def164d639c75d9cc26710a905cc491355a2b`, followed by closure-transition main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`. The resulting closure main completed **33/33 governed push workflows SUCCESS**.

P15 remains **CLOSED** from its accepted Windows/IIS installer and packaging provenance: final P15 implementation head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` integrated as implementation main `d60318da5b19d06df20864083f5a4a78b9792e88`, followed by closure-transition main `48267786e9f0d21934dee339eed32688b6e2d488`. The resulting closure main completed **34/34 governed push workflows SUCCESS**. This explicit provenance is retained so the closed P15 installer/package acceptance remains independently verifiable during and after P17.

## P16 formal closure evidence

Canonical P16 implementation unit: `P16::full-acceptance-release-candidate`.  
Canonical implementation branch: `worker/p16-full-acceptance-continuation`.  
Canonical implementation PR: #80.  
Final exact implementation head: `e655dc4d0effb4f962d3e97e4a0230471238ba55` — **38/38 governed pull-request workflows SUCCESS**.  
Original exact integrated implementation main: `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85` — **35/35 governed push workflows SUCCESS**.

The superseding API129 UAT regression recovery used PR #85. Final corrected head `0e767d4f355e403b70602a7645cd7ffba25b3b43` completed **40/40 governed pull-request workflows SUCCESS** and was normally merged as exact accepted product source `8863b4b8151ebb08e98fdb225ea5418c5297eb5c`. That exact main completed **36/36 governed push workflows SUCCESS**. Exact-main P16 run `34456867989` completed `static-contract`, `release-candidate-package`, `runtime-ui-recovery`, and aggregate `p16-gate` SUCCESS.

The accepted API129 UAT contract remains exact-scope `x-api-key + username + password` -> `POST /genToken` -> transient Bearer token from response `data` -> target request carrying the same `x-api-key + Authorization: Bearer`. Optional empty-credential placeholders and broad cross-service authentication convergence remain removed. P04 Default Deny and per-service authorization remain authoritative. Production remains independent and fail-closed; Production -> UAT fallback is forbidden.

Canonical P16 closure unit: `P16::closure-reconciliation`.  
Canonical closure branch: `worker/p16-closure-reconciliation`.  
Canonical closure PR: #86.  
Final exact closure head: `eeb03879d4b31b04bf333c8b5d021983ace8afe9` — **39/39 governed pull-request workflows SUCCESS**.  
Exact closure-transition main: `18efd6c71a35505105d44df673d1d75fde293a2e` — **36/36 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at terminal verification.  
Issue #1 closure evidence comment: `5616357892`.

All four closure conditions previously encoded for P16 are therefore proven: exact reconciliation head green, normal merge complete, resulting exact-new-main terminal green, and tracker reconciliation recorded. P16 is formally **CLOSED**.

## Exact accepted P16 release-candidate identity

Accepted product source SHA: `8863b4b8151ebb08e98fdb225ea5418c5297eb5c`  
Version: `0.1.2`  
Target runtime: `win-x64`  
Framework: `net10.0` / pinned SDK `10.0.400`

- `GSIP-0.1.2-win-x64.zip` — SHA-256 `b4971d31b11f3c1c6a6afba9026afd6865b7c006063eb59ffbdc9e2182efd426`;
- `GSIP-0.1.2-Setup-x64.exe` — SHA-256 `7a5de1c18bf9b84bd052d855d089f51ffdae175d40c872b574a68892fba99ff0`;
- Actions artifact `10143946003` — `P16-Release-Candidate-8863b4b8151ebb08e98fdb225ea5418c5297eb5c`, size 62,185,768 bytes, archive digest `sha256:b7c8f175b1669323917bbfa6b557076ce8b2d2c611af7009b13f83e4282429d6`;
- Actions artifact `10144204144` — `P16-Runtime-UI-Recovery-8863b4b8151ebb08e98fdb225ea5418c5297eb5c`, size 3,482,259 bytes, archive digest `sha256:4b5564603f8c00c5d07357dd206647f0f4dabfa5b04cd8fcc931bc1ab5a4965d`.

Earlier P16 0.1.0 and 0.1.1 release identities remain historical provenance only and are superseded by the exact 0.1.2 accepted candidate above.

## P17 implementation and closure evidence

Canonical P17 implementation/convergence PR: #92.  
Final exact implementation head: `18bedd6891bfa7812c1da7715f504e7cc233e752` — **43/43 governed pull-request workflows SUCCESS**.  
Exact implementation main after normal expected-head-protected merge: `2723e85e9d63182b1384615400a20ecc8babfe83` — **38/38 governed push workflows SUCCESS** with no failure, queued, in-progress, or null-conclusion run at terminal verification.

The P17 implementation line integrated the owner-supplied MOH/CSC/MOE UAT contracts and recovered all cloud-actionable project-wide regressions found during convergence, including exact-candidate workflow binding, unsupported MOH Certificate API-key inference removal, MOE Basic-auth scope hardening, cross-environment AuthProfile sharing rejection, phase-monotonic P04/P05/P13 closed-baseline acceptance, P10 CSP-safe printing, P04 last-effective `Roles.Manage` protection, P15 exact-Git-head package provenance, P16 final-candidate compatibility, and deterministic P17 SQL idempotency acceptance.

Canonical P17 closure-transition PR: #93.  
Final exact closure-transition head: `7e4fb7264c346f4f5310ad4b44c07fe51a07c151` — **41/41 governed pull-request workflows SUCCESS**.  
Normal expected-head-protected merge produced exact main `72d1ea92e14978b08a1b0ed1727716001ebf1390` — **37/37 governed push workflows SUCCESS** with failure=0 at terminal verification.

Exact `72d1ea92e14978b08a1b0ed1727716001ebf1390` pre-formal-closure evidence:

- P15 run `34516162860`: `GSIP-0.1.2-win-x64.zip` SHA-256 `472e8eed6e1118bf9da14e54bc26fbfcbc4d457570c99d800fc2465479446ecf`; `GSIP-0.1.2-Setup-x64.exe` SHA-256 `c90b835d8f16d82bf71e992c065c9309180222af2b232141943b07c98549cfbc`; artifact `10167797374`, size 62,247,061 bytes, archive digest `sha256:457a428ef40379f0dce36be9cd48d14b6e70dff208af2175f767d218af5b8b24`.
- P16 run `34516163009`: release ZIP SHA-256 `1761ac167bc40c36a08051ddf07e53f73512b1293531ffb8cc10ca029d637bb0`; Setup EXE SHA-256 `ce39d65319b1c977cf3797789686d6c6b45fb338ec5fb6b4272fcf68641349b1`; release artifact `10167858384`, size 62,247,913 bytes, archive digest `sha256:746c7805303c8692b9b31d45fab943c8a869d34ea30e4771a6d240850051335d`; runtime/UI artifact `10168083331`, size 3,482,563 bytes, archive digest `sha256:b549a04e6aaa4465355af0eeb32595d6aab0156f088edc0b2b9cb3bf03d22d4c`.
- P17 run `34516163083`: artifact `10167841257`, size 342 bytes, archive digest `sha256:552b5935cdcca7de3198291529807f3227cf4f41367942243108a992850fe3d4`.

The earlier implementation-candidate artifacts remain historical provenance. Because this formal-closure source changes governance and an acceptance verifier, `72d1ea92...` artifacts are also provenance only for the closure-transition integration; this exact formal-closure head and its resulting exact final main must independently emit fresh same-SHA P15/P16/P17 evidence before any final claim.

## Formal P17 closure reconciliation

This line changes canonical governance/evidence and the P12 later-phase acceptance predicate only. The P12 change is phase-monotonic and fail-closed: formal P17 CLOSED authority is accepted only when P17 identity/status, P16 formal closure, exact preserved P12 evidence/SHAs, P12/P17 CLOSED ledger rows, and formal P17 closure project-control authority all agree. All P12 runtime, authorization, CSRF, service/environment isolation, audit, bilingual UI, secret-safety and operational-state checks remain unchanged.

The mandatory Design Parity Evidence remains the four repository-native references under `docs/ui-baseline/`: Dashboard, Service Execution, Permissions, and Audit. Formal closure may not merge if exact-candidate bilingual high-fidelity UI evidence fails.

Final evidence must satisfy `docs/FINAL_ACCEPTANCE_CRITERIA.md` on one exact final source identity. Source, tests, CI, package/installer, SHA-256, UI/security evidence, database/schema evidence, rollback/handoff information and reconciled governance must agree on the final candidate. `UNPUSHED_WORK=NONE` is mandatory before any stop or final handoff.

## Formal-closure integration gate and post-closure convergence

This formal P17 CLOSED reconciliation may merge only when every governed workflow is terminal green on its exact PR head, fresh P15/P16/P17 candidate-bound artifacts and hashes are available, exact `main` and claims/reviews remain lawful, and expected-head protection is used for normal merge. The resulting exact-new-main must then complete the governed push matrix terminal green with fresh same-SHA artifact/hash evidence.

P17 being **CLOSED** does not authorize execution to stop. After formal closure integration, run a complete LIVE project-wide sweep. If ANY cloud-actionable gap exists anywhere in P00-P17 or shared infrastructure, repair the highest-priority gap immediately, merge lawfully, verify exact-main CI, and restart the clean-sweep count. Final cloud convergence requires **two consecutive full LIVE-state sweeps** with zero cloud-actionable gaps while exact-main CI remains terminal green.

`VERIFIED_FINAL_COMPLETE` remains **FORBIDDEN** until actual same-commit/same-artifact final evidence satisfies `docs/FINAL_ACCEPTANCE_CRITERIA.md`, all recoverable work is pushed, `UNPUSHED_WORK=NONE`, and the required two zero-gap sweeps pass.

## Deferred / owner-last boundaries — NOT PASS

Authorized real MOJ UAT smoke requiring owner-controlled credentials/SecretRefs and approved non-destructive test data remains `DEFERRED_EXTERNAL_NOT_PASS` where unavailable. Owner action: enter secrets only through protected AuthProfile/Secret Vault administration; verify exact UAT ServiceEnvironmentConfig/AuthProfile/SecretRef bindings with no Production fallback; execute one authorized non-destructive smoke for each five canonical services; retain only sanitized RequestId/CorrelationId, timestamp, service/environment and governed outcome evidence; never record credentials, tokens or personal identifiers.

Unproven Production operation details remain `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Owner action: obtain official Production endpoint/auth/operation evidence and authorized Production reachability/credentials/test authorization; configure Production independently through protected administration and run the minimal authorized non-destructive acceptance. Production details must never be copied or inferred from UAT.

Actual target Windows Server/IIS deployment, owner-approved Production HTTPS certificate selection/binding/SNI, Production DNS/network/proxy/firewall behavior and Production performance/capacity remain `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS`. Owner action: install the exact accepted artifact on the authorized target, bind the approved certificate, verify IIS site/app-pool/bindings/SNI/health/lifecycle and network behavior, and retain sanitized evidence.

Owner-controlled release/code signing remains `OWNER_LAST_SIGNING_NOT_PASS` where deployment policy requires it. Owner action: sign with the approved owner-controlled key/certificate outside Git, verify the produced signature, and retain sanitized signature evidence without exposing private key material.

Main branch protection remains `OWNER_LAST / NOT PASS` and is also tracked explicitly as `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS`; live read-back reports `main` with `protected=false` and required status-check enforcement off. Owner action: an authorized repository administrator must apply the documented main protection policy with provider-bound governed required checks, strict up-to-date enforcement, admin enforcement, conversation resolution, force-push disabled and deletion disabled, then independently read back the applied policy.

None of these owner/external boundaries is PASS, and none authorizes fabrication of evidence. Independent cloud-actionable project convergence continues around them even after P17 closure.