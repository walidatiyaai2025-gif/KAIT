# P08 Contract / Evidence Reconciliation

Unit: `P08::contract-evidence-reconciliation`  
Role: independent contract auditor / evidence worker  
Tracker: issue #1  
Audit branch: `worker/p08-contract-evidence-reconciliation`  
Integrated audit base: `main=e63e4ca7d5719404452791784709af8d509ea498`

> Evidence/reconciliation only. This document does **not** mark P08 CLOSED and does not authorize P09.

## 1. Live state inspected

- P08 is the sole OPEN / READY phase; P09–P17 remain locked.
- PR #43 is integrated on current audit base and adds the P08 stale-binding Secret Vault fail-closed production guard.
- PR #44 is OPEN and extends Worker 2 isolation acceptance only.
- PR #45 is OPEN for Worker 1 MOJ auth runtime. Latest inspected PR head: `337fc2ab157a21ec79593070d9718307cdd7d9f6`.
- Worker 3 independent security acceptance exists on `worker/p08-security-acceptance-ci`, latest inspected head `2b32ca6af8222a831106471f6423d7fce25734c1`, composed on the Worker 1 runtime candidate; it was not yet integrated when inspected.
- Captain convergence claim exists on tracker #1. This worker does not close the phase.

## 2. Official CAIT/MOJ authentication evidence

`docs/moj-api-reference/INDEX.md` preserves owner-supplied official evidence for:

- `ApiKeyAuth` header `x-api-key`;
- `POST /genToken`;
- `application/x-www-form-urlencoded`;
- `username` + `password` form fields;
- successful `{ "data": "string" }` token shape;
- Bearer use for Marriage APIs where applicable.

The exact referenced CAIT operation pages were not retrievable by this audit as a complete machine-readable contract. General public CAIT guidance corroborates separate UAT/live environments, JWT/Bearer use, and token expiry as concepts, but does not establish the exact MOJ token TTL, scope/audience, per-operation auth applicability, base URL, or full status/error schema.

Therefore exact unavailable details are `DEFERRED_EXTERNAL`; this audit does not invent `expires_in`, scope/audience, `grant_type`, client credentials, refresh-token behavior, undocumented fields, or undocumented status schemas.

## 3. Worker 1 runtime audit — PR #45

### Candidate behavior supported by the preserved contract

The inspected candidate:

- preserves exact `x-api-key` header capability through `ApiKeyHeader`;
- posts form-urlencoded `username` + `password` for token acquisition;
- parses token only from response `data` string;
- attaches the acquired token transiently as Bearer to the P07 service request;
- resolves token credentials under exact execution Service + Environment + AuthProfile scope;
- reuses canonical `ITokenCache`/`TokenCacheIdentity` with version/generation validity inputs;
- derives reusable expiry only from JWT `exp`; if absent/unusable it returns current time, so no MOJ TTL is invented;
- keeps the flow inside `GenericServiceExecutionEngine` and runs P07 runtime/security regressions in its dedicated workflow.

PR #45 candidate Action query showed 21 workflow runs at audit time with no observed failed, queued, or in-progress run. Candidate-green evidence is not exact-main closure evidence.

### Blocking contract defect found

PR #45 introduces:

`private const string MojTokenPath = "/genToken";`

and constructs the token endpoint using that runtime constant. Canonical P08 explicitly requires base URL, header name, and **paths** to come from environment/metadata, not hard-coded production behavior.

This audit posted a blocking handoff:

- PR #45 comment id `5596048376`;
- tracker #1 HANDOFF comment id `5596056631`.

Required repair is the smallest lawful metadata/configuration change that resolves the token endpoint path from exact Service + Environment + AuthProfile governed configuration, fails closed when absent/invalid, includes the resolved validity-affecting path in token-cache identity, and proves the behavior with a non-default synthetic token-path test. No P09 service fields should be introduced.

Exact applicability of `x-api-key` to `/genToken` itself remains `DEFERRED_EXTERNAL`; the preserved evidence proves the header scheme exists but does not prove operation-level scheme assignment. The implementation must not guess that combination.

## 4. Worker 2 isolation audit

PR #43 integrated the fail-closed production guard on `main=e63e4ca7d5719404452791784709af8d509ea498`. It requires canonical active ServiceEnvironmentConfig selection to agree with the requested AuthProfile before secret material can be resolved. PR #43 reported P08 Auth Scope Isolation run `34312024564` SUCCESS on its reconciled candidate.

PR #44 is a lawful follow-up limited to the owned isolation test/workflow paths. Its stated matrix covers forged/stale AuthProfile/SecretRef, cross-service/environment misuse, Production→UAT fallback, disabled/missing binding, cache isolation, near-expiry, single-flight and failed-refresh safety. It remains pending integration in this audit snapshot.

## 5. Worker 3 independent acceptance audit

The branch contains `tests/GSIP.P08SecurityAcceptanceChecks` and exercises synthetic-only cases including:

- official-contract fixture integrity;
- valid x-api-key and Bearer;
- missing/invalid auth;
- forged AuthProfile and SecretRef;
- HTTP/retry/network/TLS/timeout behavior;
- stale/wrong-scope token cache identity;
- failed refresh and single-flight safety;
- secret/token diagnostics leakage.

This is useful independent evidence, but it was not yet integrated/exact-main evidence when inspected. Its synthetic `scope` values test GSIP cache isolation only and must not be represented as proof that MOJ defines OAuth scope.

## 6. Remaining contract gap — administration Test action

`execution/GSIP_Full_Execution.json` P08 requires a token-generation **Test** action/button in administration. Current specialist boundaries do not own it: Worker 1 and Worker 3 explicitly exclude UI; Worker 2 owns isolation; this worker is contract/evidence only.

Captain handoff requirement: assign or implement the smallest P08-only server-authorized, environment-scoped, redacted Test Authentication/token-generation administration flow over the canonical P04/P06/P08 boundaries, using synthetic tests and no plaintext/token persistence. Do not enter P09.

This remains `AUTH-ADMIN-01 = CONTRACT_GAP / HANDOFF_REQUIRED` until resolved or lawfully reconciled by higher-authority project evidence.

## 7. Regression / ledger assessment

- Before P08 feature work, `main=8609ae4b5eb963208b366e59e8bd6b3f9311daa3` had 16 observed workflow runs with no failure/queued/in-progress/cancelled result and served as the closed P00–P07 baseline after PR #42.
- Audit base `e63e4ca7...` includes PR #43. Final closure still requires the complete applicable exact-main P00–P08 matrix after all P08 lines are integrated.
- `CURRENT_PHASE.md` correctly keeps P08 OPEN / READY and P09+ locked.
- `docs/TASK_LEDGER.md` correctly does not claim P08 CLOSED.
- The P07 `9535fa...` reference is an implementation-baseline reference, while PR #41 is later governance reconciliation; this audit does not treat that distinction as stale evidence.

## 8. Validation artifact

`scripts/verify-p08-auth-contract.ps1` is the unit-owned static validator. In normal open-phase mode it verifies preserved official-contract anchors, matrix/governance discipline, and reports pending runtime. In `-ClosureGate` mode it requires composed Worker 1–3 evidence paths and rejects pending/gap markers. When the PR #45 runtime is composed, its production-source scan rejects a literal hard-coded `/genToken` path and undocumented OAuth request conventions.

No separate CI workflow is added by this unit because Worker 1–3 already own dedicated P08 CI paths; the captain should run this validator on the composed convergence head.

## 9. Closure-readiness verdict

**NOT CLOSURE-READY.**

Blocking / pending items:

1. PR #45 hard-coded `/genToken` production path violates metadata-driven P08 configuration and requires repair.
2. `AUTH-ADMIN-01` token-generation Test administration action is unowned/unimplemented.
3. PR #44 full isolation acceptance is not yet integrated.
4. Worker 3 independent security acceptance is not yet integrated/exact-main verified.
5. Final exact-main P00–P08 regression/evidence gate is necessarily pending.
6. Exact unavailable CAIT operation details remain `DEFERRED_EXTERNAL` and must not be guessed.

`UNPUSHED_WORK=NONE` after GitHub-backed branch writes.
