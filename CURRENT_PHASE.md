# CURRENT_PHASE.md

## Canonical current phase

**P09 — Seed and implement the five MOJ services**

Status: **OPEN / READY**

P08 — MOJ authentication integration is **CLOSED** from exact integrated implementation baseline `67968a9230dae453b36f48549eda138d7b5401c7` after convergence of the P08 authentication runtime, scope isolation, independent security acceptance and protected administration Test Authentication flow through PR #47, with the earlier legitimate worker lines recovered rather than duplicated.

On exact implementation `main` SHA `67968a9230dae453b36f48549eda138d7b5401c7`, all **17/17 exact-main push workflow runs succeeded**, with no failed, queued or in-progress run after completion. PR #47 exact-head verification was **22/22 SUCCESS** before normal merge.

P08 exact-main closure evidence includes:

- P08 Security Acceptance run `34318032200` — **SUCCESS**.
- Artifact `P08-Security-Acceptance-67968a9230dae453b36f48549eda138d7b5401c7`, 2,314 bytes, digest `sha256:b39ea805fec25b5a45a35c2eb445074e64622f48a44dc7c7e3ccd6a0ea8b3d98`.
- MOJ runtime acceptance — **PASS**, including valid `/genToken` → Bearer composition and malformed/empty/failure cases supported by the evidence then available.
- P08 Auth Scope Isolation — **PASS**, including forged/stale AuthProfile and SecretRef, cross-service, cross-environment, Production→UAT fallback and stale/wrong-scope token rejection.
- Protected administration Test Authentication — **PASS**, server-authorized and anti-forgery protected without token plaintext disclosure.
- Independent secret/token leakage scan — **PASS**.

At the time P08 closed, exact operation-level `ApiKeyAuth` applicability to `/genToken` was correctly **DEFERRED_EXTERNAL — NOT PASS** because authenticated operation-level evidence had not yet been supplied. That historical closure classification is preserved and is not rewritten retroactively.

After P08 closure, owner-supplied authenticated official CAIT Swagger evidence for Marriage Cases API 129, together with an owner-executed successful UAT token call, proved the observed **UAT Marriage composite authentication contract**: `/genToken` requires transient `x-api-key` plus form-urlencoded `username`/`password`, and the Marriage target requires the same `x-api-key` plus the acquired Bearer token. The closed-baseline regression repair is tracked by PR #50 / `P08-REGRESSION::moj-composite-auth-real-contract` and extends only the canonical P06/P07/P08 runtime/Test Authentication path. Status for this observed UAT contract: **UAT_PROVEN**.

Production remains deliberately separate. Owner evidence proves only the Production gateway prefix `https://moj.api.cait.gov.kw`; no Marriage path suffix, `/genToken` operation-level security applicability, target suffix, or payload contract is inferred. Status for those unproven details: **PRODUCTION_DEFERRED_EXTERNAL — NOT PASS**. No Production→UAT fallback is permitted.

This post-closure repair introduces **no unrelated P09 service payload implementation**. P09 remains the canonical current phase, but new P09 payload work must not outrun integration and exact-main verification of this closed-baseline regression repair.

## Legal work now

P09 only, plus any repair required to preserve closed P00–P08 baselines and repository controls. The active P08 closed-baseline regression repair has priority over new P09 payload work until normally integrated and verified on exact new `main`.

P09 scope is exactly the canonical execution-plan unit: add MOJ as the first Entity and seed/implement only the five services — Marriage Cases Service, Is Single Basic Service, Marriage Couple Last Case Service, Family Judgment Text Service, and Procuration Status Service. Request fields, response fields, validation, endpoints, authentication applicability and mappings must come from official OpenAPI/Swagger snapshots or other repository-approved official evidence. Unknown fields remain blocked/deferred; do not invent them. Authorized UAT smoke, if available, must be limited and non-destructive.

## Locked future work

P10–P17 remain locked. Request history, audit/monitoring, broader administration, final UI convergence, security hardening, installer, full acceptance and final release convergence must not begin before their canonical phase opens.

## P08 closure and post-closure evidence

Detailed evidence is recorded in:

- `docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md`
- `docs/evidence/P08_CONTRACT_RECONCILIATION.md`
- `docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md`
- `docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md`

Historical closure facts:

- Implementation baseline: `67968a9230dae453b36f48549eda138d7b5401c7`
- Exact-main push workflows: **17/17 SUCCESS**
- PR #47 exact-head workflows: **22/22 SUCCESS**
- Exact-main P08 Security Acceptance: run `34318032200` — **SUCCESS**
- Owner/external evidence at original closure: exact `/genToken` operation-level `ApiKeyAuth` applicability was **DEFERRED_EXTERNAL — NOT PASS**
- P09 implementation introduced by P08 closure reconciliation: **NONE**

Post-closure repair facts:

- Observed Marriage UAT `/genToken` API-key applicability and downstream API-key + Bearer composition: **UAT_PROVEN** from later authenticated official CAIT evidence.
- Production Marriage path/auth/payload details beyond the proven Production gateway prefix: **PRODUCTION_DEFERRED_EXTERNAL — NOT PASS**.
- PR #50 carries the canonical regression repair and synthetic-only acceptance; normal merge plus exact-new-main green verification remain required before the repair is called integrated.

## P09 exit condition

P09 may be marked CLOSED only when the five canonical MOJ services are represented from official request/response contract evidence without invented fields, their independent endpoint/auth bindings and contract tests are complete, closed P00–P08 regressions remain green, any unavailable authorized UAT dependency is classified under owner-last without being called PASS, and exact-main evidence supports closure without introducing P10+ scope.
