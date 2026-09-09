# CURRENT_PHASE.md

## Canonical current phase

**P10 — Requests, result history and exports**

Status: **OPEN / READY**

P09 — Seed and implement the five MOJ services is **CLOSED** from exact integrated `main` SHA `42b3b7af073efe6bd933f473b707333df8924346` after normal convergence of PRs #58, #59 and #60 and preservation of the closed P00–P08 baselines through the later P02 Setup Wizard regression repair PR #61.

On exact `main` SHA `42b3b7af073efe6bd933f473b707333df8924346`, all **29/29 push workflow runs succeeded**, with failure=0, queued=0 and in-progress=0 after completion. P09 exact-main evidence includes official-contract snapshots, all five service gates, token-contract variants, independent contract/security acceptance and execution UI/UAT-readiness acceptance.

Detailed P09 closure evidence is recorded in `docs/evidence/P09_CLOSURE.md`.

## P09 closure truth

The five canonical MOJ services are represented from repository-approved official CAIT/MOJ contract evidence:

1. Marriage Cases Service — API 129
2. Is Single Basic Service — API 132
3. Marriage Couple Last Case Service — API 130
4. Family Judgment Text Service — API 196
5. Procuration Status Service — API 134

P09 preserves exact Service + Environment + AuthProfile isolation, secret/token redaction, no Production→UAT fallback, metadata-driven request/result mapping and the closed P00–P08 security/runtime controls.

Authorized live UAT execution that requires owner-controlled credentials or personal test records remains **DEFERRED_EXTERNAL_NOT_PASS** where unavailable. It is not called PASS. Unproven Production full paths, operation details and credentials remain **PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS** and fail closed.

Representative exact-main P09 runs on `42b3b7af073efe6bd933f473b707333df8924346`:

- P09 Official Contract Snapshots `34390738947` — SUCCESS.
- P09 Marriage Cases Service `34390739120` — SUCCESS.
- P09 MOJ Token Contract Variants `34390739231` — SUCCESS.
- P09 Independent Contract Security Acceptance `34390739078` — SUCCESS.
- P09 Execution UI and UAT Readiness `34390739312` — SUCCESS.

## Legal work now

P10 only, plus any repair required to preserve closed P00–P09 baselines and repository controls.

Canonical P10 scope from `execution/GSIP_Full_Execution.json`:

- persist execution RequestId, actor/user, department, Entity/Service/version, masked input snapshot, status code, duration, CorrelationId and timestamps;
- configurable per-service structured-result/raw-response storage; sensitive stored result data protected at rest and raw storage disableable;
- Own history by default, Department/All only by permission and current `Services.View` scope;
- search/filter/date range/status/entity/service;
- permissioned export/print/PDF/CSV/Excel with an Audit Event for every export;
- no unmasked Civil IDs or sensitive payload leakage;
- executable user/department/service isolation and IDOR-negative acceptance;
- retention, bounds, concurrency and migration safety required by the canonical implementation.

## Locked future work

P11–P17 remain locked. Tamper-evident audit/monitoring, broader operations/diagnostics, final UI convergence, security hardening, installer, full automated acceptance and final release convergence must not begin before their canonical phase opens.

## P08 historical closure boundary

P08 remains CLOSED. Its historical closure baseline is `67968a9230dae453b36f48549eda138d7b5401c7`; later authenticated owner evidence proved the observed Marriage UAT composite authentication contract and the closed-baseline repair was integrated without reopening P08. Historical evidence remains in:

- `docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md`
- `docs/evidence/P08_CONTRACT_RECONCILIATION.md`
- `docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md`
- `docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md`

Historical deferred classifications are not rewritten retroactively.

## P10 exit condition

P10 may be marked CLOSED only when the canonical request/history persistence and lifecycle, masked/protected result handling, Own/Department/All authorization, service visibility isolation, filtering, retention, migration/concurrency safety, permissioned exports with injection defense and export audit events, bilingual UI and required regression tests are integrated; exact-main evidence must be green. Any genuinely owner-only/external evidence must remain explicitly deferred rather than being called PASS.
