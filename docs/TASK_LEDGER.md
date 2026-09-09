# TASK_LEDGER.md — GSIP Canonical Phase Ledger

This ledger is evidence-driven. Never mark a phase CLOSED because a prompt says it should be closed. Update status only from live implementation/tests/CI/evidence on the exact integrated commit. `DEFERRED_EXTERNAL` is never equivalent to PASS.

| Phase | Status | Scope / closure focus |
|---|---|---|
| P00 | CLOSED | Closed from exact-main SHA `8156c46ce8ce366424d955a6194d677c2da5055f`; Planning Integrity `34210899288` SUCCESS; P00 Build Baseline `34210899267` SUCCESS; exact-main baseline artifact produced and hashed |
| P01 | CLOSED | Closed from exact-main SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3`; Planning `34213985141`, P00 regression `34213985591`, P01 runtime/browser `34213985167` SUCCESS; exact-main bilingual UI evidence produced and hashed |
| P02 | CLOSED | Closed from exact-main SHA `efa56808db13fa45f803131f7bcf2d65c485a66d`; later SQL Authentication closed-baseline regression repair PR #61 integrated at exact main `42b3b7af073efe6bd933f473b707333df8924346`; protected first-run Setup and SQL auth diagnostics/acceptance remain green |
| P03 | CLOSED | Closed from exact-main SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91`; Planning/P00–P03 identity and web-security evidence verified |
| P04 | CLOSED | Closed from exact-main SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`; RBAC, Default Deny, server-side/service authorization, admin/IDOR and bilingual Permissions UI evidence verified |
| P05 | CLOSED | Closed from exact-main implementation SHA `4d706fab57071236e9847670342f5a399b3527e6`; metadata catalog, versioning, isolation/security, import/export and protected administration verified |
| P06 | CLOSED | Corrected closure implementation/security baseline `fef5882abf8a6f12990c3e7c0e9f849d08cd7947`; final reconciliation PR #32 at `90b3068ea39a342392222ae581e568b94f7f9004`; SecretRefs/AuthProfiles/rotation/redaction/token-cache/admin acceptance verified |
| P07 | CLOSED | Closed from exact integrated implementation baseline `9535fa158441160ab7c7d204863776e38e560a33`; runtime/UI/upgrade/security evidence verified and hashed |
| P08 | CLOSED | Historical closure baseline `67968a9230dae453b36f48549eda138d7b5401c7`; canonical x-api-key + `/genToken` + Bearer runtime, exact scope isolation, protected Test Authentication and independent security acceptance; later authenticated Marriage UAT evidence integrated through closed-baseline repair without reopening P08 |
| P09 | CLOSED | Closed from exact integrated main `42b3b7af073efe6bd933f473b707333df8924346`; PRs #58/#59/#60 integrated official contracts for APIs 129/132/130/196/134, five canonical services, token variants, independent security acceptance and execution UI/UAT readiness; exact-main **29/29 SUCCESS**; unavailable owner live UAT remains `DEFERRED_EXTERNAL_NOT_PASS` |
| P10 | OPEN / READY | Canonical current phase: Request/result history, masked/protected persistence, Own/Department/All + service isolation, filters, retention/concurrency and permissioned CSV/XLSX/PDF/Print exports with export Audit Event |
| P11 | LOCKED | Tamper-evident audit trail, monitoring, high-fidelity Audit dashboard and evidence |
| P12 | LOCKED | Admin operations for Entity -> Service -> Environments -> UAT/Production, Edit/Test Connection/Test Authentication/Activate/Disable/Rotate Secret, health/diagnostics and operational alerts |
| P13 | LOCKED | Full Arabic/English RTL/LTR UX convergence + UI DESIGN PARITY GATE for all four reference screens |
| P14 | LOCKED | Security hardening, resilience, authorization/IDOR/CSRF/XSS/rate-limit/fuzz/dependency checks including cross-service endpoint/secret/token isolation |
| P15 | LOCKED | Professional Windows/IIS installer, upgrade/repair/uninstall path, versioned artifact + SHA-256 |
| P16 | LOCKED | Full automated acceptance on exact release candidate including setup→login→RBAC→MOJ→history→audit→installer + UI parity + service/environment isolation |
| P17 | LOCKED | Final convergence, exact-main regression repair, stale integration recovery, ledger/evidence reconciliation and final release closure |

## P00 closure record

P00 implementation was integrated by PR #2. Exact integrated `main` SHA `8156c46ce8ce366424d955a6194d677c2da5055f` passed the required workflows and produced the hashed P00 baseline artifact. No owner-only evidence was deferred.

## P01 closure record

P01 implementation was integrated by PR #4 at exact `main` SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3`. Planning, P00 regression and P01 runtime/browser workflows all succeeded and exact-main bilingual UI evidence was produced and hashed. No owner-only evidence was deferred.

## P02 closure and regression record

P02 was originally integrated through PRs #6, #7, #8 and #9 at exact `main` SHA `efa56808db13fa45f803131f7bcf2d65c485a66d`. Planning/P00/P01/P02 workflows succeeded and protected first-run Setup, SQL negative/retry, critical Review/Health gating and bilingual browser evidence were verified.

A later owner-reproduced SQL Authentication Setup Wizard regression was repaired through PR #61. The repair preserves explicit SQL-vs-Windows auth binding, transient SQL credentials, safe `master` connection testing, sanitized SQL diagnostics and caller-selected TLS semantics. PR #61 normally merged at exact main `42b3b7af073efe6bd933f473b707333df8924346`; the exact-main set completed 29/29 SUCCESS. Owner same-host operational smoke remains owner-last and is not fabricated as PASS.

## P03 closure record

P03 was integrated through PRs #12 and #13 at exact `main` SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91`. Identity persistence, MFA, privileged-account policy, lockout/rate limiting, CSRF/security headers/cookie posture, sanitized audit and bilingual browser evidence were verified. Detailed evidence: `docs/evidence/P03_IDENTITY_ACCOUNT_SECURITY.md`.

## P04 closure record

P04 culminated through PR #20 at exact `main` SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`. Editable roles/permissions, per-service Default Deny, server-side authorization, last-System-Administrator protection, forged-identifier denial and bilingual responsive Permissions UI evidence were verified. Detailed evidence: `docs/evidence/P04_RBAC_PERMISSIONS_UI.md`.

## P05 closure record

P05 was normally integrated through PR #22 at exact implementation `main` SHA `4d706fab57071236e9847670342f5a399b3527e6`. Metadata Entity/Environment/Service/ServiceField/ResultMapping persistence, independent UAT/Production bindings, secure validation/versioning, JSON import/export, protected bilingual administration and generic service proof were verified. Detailed evidence: `docs/evidence/P05_METADATA_CATALOG.md`.

## P06 closure record

P06 converged through PRs #24, #25, #26, #27, #28 and #29. A higher-authority plan audit recovered the legitimate runtime token-cache requirement and PR #31 integrated it. Corrected implementation/security baseline is `fef5882abf8a6f12990c3e7c0e9f849d08cd7947`; final reconciliation PR #32 integrated at `90b3068ea39a342392222ae581e568b94f7f9004`. Scoped SecretRefs, isolated/explicitly shared AuthProfiles, atomic rotation, centralized redaction, token-cache safety, protected administration and independent no-leak acceptance are verified. Detailed evidence: `docs/evidence/P06_SECRET_VAULT_AUTH_PROFILES.md`.

## P07 closure record

P07 is **CLOSED** from exact integrated implementation baseline `9535fa158441160ab7c7d204863776e38e560a33` after normal integration of PRs #33, #34, #36, #37 and #38 and convergence/reconciliation work. All **16/16 exact-main workflows** completed successfully.

Acceptance covers server-side `Services.Execute` authorization; exact Service + Environment + AuthProfile resolution; forged/cross-scope rejection and no fallback; metadata-generated fields and validation; RequestId/CorrelationId; `IHttpClientFactory`; bounded timeout/retry; HTTP/TLS/network classification; bounded response reads; ResultMappings; secret-safe diagnostics; sensitive raw-response masking; bilingual RTL/LTR responsive Service Execution UI; fake-endpoint acceptance; and upgrade/data-preservation safety. Detailed evidence: `docs/evidence/P07_GENERIC_EXECUTION_ENGINE.md`.

## P08 closure record

P08 is **CLOSED** from exact integrated implementation baseline `67968a9230dae453b36f48549eda138d7b5401c7`.

PR #47 normally merged the authoritative P08 convergence: metadata-driven token endpoint control, documented token acquisition and Bearer attachment, exact-scope secret/token behavior, protected administration Test Authentication, independent security acceptance and CI wiring. PR #47 exact head passed 22/22 workflows and exact main completed 17/17 SUCCESS.

Original P08 closure correctly left exact `/genToken` `ApiKeyAuth` applicability as `DEFERRED_EXTERNAL — NOT PASS` because it was not yet proven. Later owner-supplied authenticated official CAIT evidence proved the observed Marriage UAT composite contract and the closed-baseline repair extended only the canonical runtime. Production remains a separate evidence boundary; unproven Production full paths/auth/payload details remain **PRODUCTION_DEFERRED_EXTERNAL — NOT PASS**.

Detailed evidence:

- `docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md`
- `docs/evidence/P08_CONTRACT_RECONCILIATION.md`
- `docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md`
- `docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md`

Historical deferred classifications are preserved and are not rewritten as PASS.

## P09 closure record

P09 is **CLOSED** from exact integrated `main` SHA `42b3b7af073efe6bd933f473b707333df8924346`.

Normal convergence chain:

- PR #58 — official MOJ contract convergence for APIs 129, 132, 130, 196 and 134, metadata/runtime reconciliation and token-contract variants.
- PR #59 — independent P09 contract/security acceptance.
- PR #60 — P09 execution UI and UAT-readiness convergence.
- PR #61 — unrelated closed-baseline P02 SQL Authentication repair; post-repair exact-main verification preserved all P09 gates green.

The five canonical P09 services are Marriage Cases, Is Single Basic, Marriage Couple Last Case, Family Judgment Text and Procuration Status. Their request/response/auth metadata is grounded in repository-approved official evidence. Unproven Production details are not inferred.

Exact-main `42b3b7af073efe6bd933f473b707333df8924346` completed **29/29 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0. Representative P09 runs:

- Official Contract Snapshots `34390738947` — SUCCESS.
- Marriage Cases Service `34390739120` — SUCCESS.
- MOJ Token Contract Variants `34390739231` — SUCCESS.
- Independent Contract Security Acceptance `34390739078` — SUCCESS.
- Execution UI and UAT Readiness `34390739312` — SUCCESS.

Authorized live UAT requiring owner-controlled credentials or personal test records remains **DEFERRED_EXTERNAL_NOT_PASS** where unavailable; this is not called PASS. Detailed closure evidence: `docs/evidence/P09_CLOSURE.md`.

## P10 opening record

P10 is **OPEN / READY** after P09 closure and exact-main regression verification.

Canonical P10 scope is the authoritative execution-plan unit: persist RequestId, user/department, Entity/Service/version, masked input snapshot, status code, duration, CorrelationId and timestamps; protect configurable structured/raw result persistence at rest with raw storage disableable; enforce Own/Department/All permissions plus current `Services.View`; support search/filter/date/status/entity/service; provide permissioned Print/PDF/CSV/Excel exports with export Audit Event; prevent unmasked Civil ID/sensitive-payload leakage; and prove migration, retention, concurrency, user/department/service isolation and IDOR-negative behavior.

## Cross-phase gates

The following apply to every phase:

- **LIVE STATE FIRST:** inspect main/PRs/branches/CI/claims before new work.
- **NO DUPLICATION:** recover legitimate existing work before creating overlapping work.
- **NO FUTURE PHASE WORK:** current-phase closure controls progression.
- **TEST/REPAIR/RETEST:** failed required tests or exact-main regressions have priority.
- **NO SECRET LEAKAGE:** secrets and personal data must not enter Git/logs/evidence.
- **SERVICE ISOLATION:** endpoint/auth/secret/token configuration defaults to independent Service + Environment bindings; sharing requires an explicit Shared AuthProfile.
