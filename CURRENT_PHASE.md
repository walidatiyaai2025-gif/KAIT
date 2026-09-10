# CURRENT_PHASE.md

## Canonical current phase

**P17 — Final convergence and release closure**

Status: **OPEN / READY**

P00-P16 are formally **CLOSED** from preserved exact integrated evidence. P17 is now the sole canonical phase. This materialization opens P17 for cloud-actionable final convergence only; it does **not** itself establish final completion and does not convert any owner-only or external evidence to PASS.

## Preserved P13 closed baseline provenance

P13 remains **CLOSED** from its exact accepted implementation provenance: final P13 implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8` integrated as exact implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`. This provenance remains explicit so the closed P13 bilingual UI/accessibility baseline stays independently verifiable during P17.

## Preserved P14 and P15 closed baseline provenance

P14 remains **CLOSED** from its accepted implementation and closure provenance: final P14 implementation head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` integrated as implementation main `1f1def164d639c75d9cc26710a905cc491355a2b`, followed by closure-transition main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`. The resulting closure main completed **33/33 governed push workflows SUCCESS**.

P15 remains **CLOSED** from its accepted Windows/IIS installer and packaging provenance: final P15 implementation head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` integrated as implementation main `d60318da5b19d06df20864083f5a4a78b9792e88`, followed by closure-transition main `48267786e9f0d21934dee339eed32688b6e2d488`. The resulting closure main completed **34/34 governed push workflows SUCCESS**. This explicit provenance is retained so the closed P15 installer/package acceptance remains independently verifiable during P17.

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

## P17 legal work

P17 scope is **final convergence and release closure** from live repository state. Before every P17 write, workers must re-read exact `main`, open PRs/issues, active branches/claims, recent merges and governed CI; recover legitimate existing work before creating new scope; and prioritize any exact-main regression or stale integration.

P17 must converge the exact final candidate across the complete integrated product: first-run Setup and database safety; identity/MFA/account controls; RBAC and service-level Default Deny; metadata/service-environment isolation; SecretRef/AuthProfile/token safety; five MOJ service contracts/auth execution; request history/exports; audit/tamper evidence; administration/health; Arabic RTL and English LTR UX/accessibility; security hardening/resilience; Windows/IIS installer lifecycle; backup/restore; automated acceptance; and release artifacts.

The mandatory Design Parity Evidence must cover all four repository-native references under `docs/ui-baseline/`: Dashboard, Service Execution, Permissions, and Audit. P17 may not close if exact-candidate bilingual high-fidelity UI evidence fails.

Final evidence must satisfy `docs/FINAL_ACCEPTANCE_CRITERIA.md` on one exact final source identity. Source, tests, CI, package/installer, SHA-256, UI/security evidence, database/schema evidence, rollback/handoff information and reconciled governance must agree on the final candidate. `UNPUSHED_WORK=NONE` is mandatory before any stop or final handoff.

## P17 exit condition

P17 may close only when every cloud-actionable final-convergence requirement is complete on one exact final candidate, every governed workflow required by repository policy is terminal green on the exact PR head and resulting exact integrated main, final package/installer hashes and artifacts are reconciled to that exact final candidate, governance/evidence is reconciled, and all remaining owner/external items are recorded precisely as NOT PASS with exact owner acceptance actions.

`VERIFIED_FINAL_COMPLETE` remains **FORBIDDEN** until actual P17 same-commit/same-artifact final evidence satisfies `docs/FINAL_ACCEPTANCE_CRITERIA.md`. This P17 phase-entry materialization is not a final-completion claim.

## Deferred / owner-last boundaries — NOT PASS

Authorized real MOJ UAT smoke requiring owner-controlled credentials/SecretRefs and approved non-destructive test data remains `DEFERRED_EXTERNAL_NOT_PASS` where unavailable. Owner action: enter secrets only through protected AuthProfile/Secret Vault administration; verify exact UAT ServiceEnvironmentConfig/AuthProfile/SecretRef bindings with no Production fallback; execute one authorized non-destructive smoke for each five canonical services; retain only sanitized RequestId/CorrelationId, timestamp, service/environment and governed outcome evidence; never record credentials, tokens or personal identifiers.

Unproven Production operation details remain `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Owner action: obtain official Production endpoint/auth/operation evidence and authorized Production reachability/credentials/test authorization; configure Production independently through protected administration and run the minimal authorized non-destructive acceptance. Production details must never be copied or inferred from UAT.

Actual target Windows Server/IIS deployment, owner-approved Production HTTPS certificate selection/binding/SNI, Production DNS/network/proxy/firewall behavior and Production performance/capacity remain `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS`. Owner action: install the exact accepted artifact on the authorized target, bind the approved certificate, verify IIS site/app-pool/bindings/SNI/health/lifecycle and network behavior, and retain sanitized evidence.

Owner-controlled release/code signing remains `OWNER_LAST_SIGNING_NOT_PASS` where deployment policy requires it. Owner action: sign with the approved owner-controlled key/certificate outside Git, verify the produced signature, and retain sanitized signature evidence without exposing private key material.

Main branch protection remains `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS`; live read-back at P17 entry reports `main` with `protected=false` and required status-check enforcement off. Owner action: an authorized repository administrator must apply the documented main protection policy with provider-bound governed required checks, strict up-to-date enforcement, admin enforcement, conversation resolution, force-push disabled and deletion disabled, then independently read back the applied policy.

None of these owner/external boundaries is PASS, and none authorizes fabrication of evidence. Independent cloud-actionable P17 convergence continues around them.