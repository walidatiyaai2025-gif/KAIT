# P08 Contract / Evidence Reconciliation

Unit: `P08::contract-evidence-reconciliation`  
Role: independent contract auditor / evidence worker  
Tracker: issue #1  
Audit branch: `worker/p08-contract-evidence-reconciliation`  
Live integrated base at reconciliation start: `e63e4ca7d5719404452791784709af8d509ea498`

> This document is evidence/reconciliation only. It does **not** mark P08 CLOSED and does not authorize P09 work.

## 1. Live-state findings

### Current phase and governance

- P08 — MOJ authentication integration — is the sole OPEN / READY implementation phase.
- P09–P17 remain locked.
- P08 exit requires authoritative MOJ authentication behavior, exact Service + Environment + AuthProfile isolation, executable negative/security acceptance, preserved P00–P07 regressions and exact-main evidence.
- The canonical P07 execution runtime remains the integration boundary; P08 must extend authentication behavior rather than introduce a competing execution runtime.

### P08 worker ownership observed

| Unit | Branch / PR | Owned scope | Audit disposition |
|---|---|---|---|
| `P08::moj-auth-runtime` | `worker/p08-moj-auth-runtime` | P08 x-api-key, `/genToken`, Bearer runtime and P07 wiring | Branch exists but remained at baseline `8609ae4b5eb963208b366e59e8bd6b3f9311daa3` when inspected; **no implementation evidence yet**. |
| `P08::moj-auth-scope-isolation` | `worker/p08-moj-auth-scope-isolation`; PR #43 merged; PR #44 open | fail-closed isolation and dedicated acceptance | PR #43 production guard integrated on current base. PR #44 full acceptance extension pending integration. |
| `P08::security-acceptance-ci` | `worker/p08-security-acceptance-ci` | independent P08 security/negative/no-leak acceptance | Branch exists; integration evidence pending. |
| `P08::contract-evidence-reconciliation` | this branch | contract matrix, traceability, evidence and non-overlapping validation | Active. |
| P08 captain convergence | `worker/p08-moj-authentication-convergence` was claimed in issue #1 | integration/closure convergence | Captain claim exists; no closure conclusion is made here. |

### Integrated P08 change observed

PR #43 normally merged the minimum stale-binding Secret Vault guard into `main=e63e4ca7d5719404452791784709af8d509ea498`. The change requires canonical active `ServiceEnvironmentConfig.AuthProfileId` to agree with the exact requested AuthProfile before secret material can be resolved. The PR reported dedicated P08 Auth Scope Isolation run `34312024564` SUCCESS on its reconciled candidate.

PR #44 is a lawful continuation of the same isolation lease and, at this snapshot, remains OPEN. Its stated delta is limited to the unit-owned isolation test/workflow paths and extends synthetic-only coverage across forged/stale SecretRefs/AuthProfiles, cross-service/environment misuse, Production→UAT fallback, disabled/missing binding, cache-scope/version/generation isolation, near-expiry refresh and single-flight/failure safety. Because it is not yet integrated, those claims are not promoted to exact-main PASS by this audit.

## 2. Official CAIT/MOJ contract capture

The repository-preserved owner-supplied official evidence in `docs/moj-api-reference/INDEX.md` establishes:

- `ApiKeyAuth` request header named `x-api-key` / Swagger-defined casing;
- `POST /genToken`;
- `application/x-www-form-urlencoded` request content type;
- form fields `username` and `password`;
- successful example token response shape `{ "data": "string" }`;
- Bearer use of the token for other Marriage APIs where that authentication applies.

The live public CAIT portal is reachable, but exact referenced API detail pages were not available to the auditing retrieval path as a complete machine-readable operation contract. General public CAIT guidance corroborates separate UAT/live environments, JWT Bearer usage and token expiry as concepts; it does not establish the exact MOJ `/genToken` TTL, scope, audience, per-operation auth scheme, base URL or full error schema.

Therefore the following are deliberately **not invented**:

- token TTL / `expires_in`;
- OAuth `scope` or audience;
- `grant_type`, `client_id`, `client_secret`, refresh-token semantics;
- undocumented `/genToken` fields;
- exact UAT/Production token base URLs;
- undocumented error status/response shapes.

Exact inaccessible items are recorded as `DEFERRED_EXTERNAL` in `docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md`.

## 3. Implementation-to-contract traceability status

The authoritative matrix is `docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md`.

At this snapshot:

- x-api-key runtime: **PENDING_IMPLEMENTATION**;
- `/genToken` exact request/response runtime: **PENDING_IMPLEMENTATION**;
- Bearer injection: **PENDING_IMPLEMENTATION**;
- P07 authenticated-send integration: **PENDING_IMPLEMENTATION**;
- P08 runtime-specific redaction/no-leak evidence: **PENDING**;
- exact secret-binding stale/forged fail-closed production guard: **INTEGRATED via PR #43**;
- extended isolation acceptance: **PENDING_INTEGRATION via PR #44**;
- token-cache isolation foundation: **PRESENT from P06**, with P08-specific acceptance still pending;
- official exact token TTL/scope: **NOT CAPTURED; MUST NOT BE INVENTED**.

## 4. Contract gap requiring captain handoff

### `AUTH-ADMIN-01` — P08 token-generation Test administration action

The canonical P08 execution instruction requires a token-generation **Test** button/action in administration. Current specialist leases do not own that implementation:

- Worker 1 excludes UI;
- Worker 2 owns isolation paths only;
- Worker 3 excludes UI;
- this worker is evidence/contract only.

**Required captain action:** assign or implement the smallest P08-only administration Test Authentication/token-generation flow using the canonical P04 authorization + P06 AuthProfile/Secret Vault + P08 authentication runtime. It must be server-side authorized, redacted, environment-scoped, non-persistent for plaintext/token material, and tested without real credentials. It must not seed/implement P09 service-specific request/response behavior.

Until this requirement is resolved or lawfully reconciled by higher-authority project evidence, P08 should not be declared closure-ready.

## 5. P00–P07 regression evidence

Before P08 feature changes, exact `main=8609ae4b5eb963208b366e59e8bd6b3f9311daa3` had 16 workflow runs with no observed failed, queued, in-progress or cancelled run in the live query. That served as the closed P00–P07 baseline after PR #42 repaired the P06 browser-evidence timing flake.

Current audit base advanced to `main=e63e4ca7d5719404452791784709af8d509ea498` through PR #43. Final P08 convergence must re-run and record the complete applicable exact-main P00–P07 regression matrix on the **final integrated P08 SHA**. Candidate/PR-head green evidence is necessary but not sufficient for phase closure.

## 6. Ledger/evidence reconciliation assessment

- `CURRENT_PHASE.md` correctly states P08 OPEN / READY and keeps P09+ locked.
- `docs/TASK_LEDGER.md` correctly keeps P08 OPEN / READY; it does not falsely claim the partially landed P08 work is closed.
- P07 governance references the exact integrated P07 **implementation baseline** `9535fa...`; later PR #41 is a reconciliation/phase-transition commit. This distinction is not treated as a defect by this audit.
- No phase-state document should be changed by this independent worker merely because PR #43 landed; P08 remains OPEN and the captain owns final convergence/closure reconciliation.

## 7. Validation policy

`scripts/verify-p08-auth-contract.ps1` is the non-overlapping static contract validator owned by this unit. It:

- verifies the preserved official minimum contract is still present;
- verifies this matrix carries required contract IDs and `DEFERRED_EXTERNAL` discipline;
- rejects a false `P08 CLOSED` claim in this audit document/matrix;
- checks current governance still identifies P08 and keeps P09 locked;
- scans P08 authentication production locations, when present, for hard-coded external URLs/`/genToken` path and common undocumented OAuth request assumptions;
- supports `-ClosureGate` to turn pending runtime/security evidence into a hard failure during captain convergence.

This worker does not add a competing CI workflow because dedicated P08 runtime, isolation and security CI paths are already owned by Workers 1–3. The captain should execute this script after composing those lines and before closure.

## 8. Current closure-readiness verdict

**NOT CLOSURE-READY at this audit snapshot.**

Reasons:

1. Worker 1 MOJ auth runtime had no implementation commit when inspected.
2. Worker 3 independent security acceptance had no integrated evidence when inspected.
3. PR #44 full isolation acceptance remained open.
4. `AUTH-ADMIN-01` had no specialist owner/implementation.
5. Final exact-main P00–P07 + P08 evidence cannot exist until the above lines are integrated.
6. Exact live CAIT contract details beyond the preserved owner-supplied minimum remain `DEFERRED_EXTERNAL` and must not be guessed.

The integrated PR #43 isolation repair is legitimate progress and should be preserved.

`UNPUSHED_WORK=NONE` after this branch is pushed by GitHub-backed writes.
