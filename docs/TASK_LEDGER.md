# TASK_LEDGER.md — GSIP Canonical Phase Ledger

This ledger is evidence-driven. Never mark a phase CLOSED because a prompt says it should be closed. Update status only from live implementation/tests/CI/evidence on the exact integrated commit.

| Phase | Status | Scope / closure focus |
|---|---|---|
| P00 | CLOSED | Closed from exact-main SHA `8156c46ce8ce366424d955a6194d677c2da5055f`; Planning Integrity `34210899288` SUCCESS; P00 Build Baseline `34210899267` SUCCESS; exact-main baseline artifact produced and hashed |
| P01 | CLOSED | Closed from exact-main SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3`; Planning `34213985141`, P00 regression `34213985591`, P01 runtime/browser `34213985167` SUCCESS; exact-main bilingual UI evidence produced and hashed |
| P02 | CLOSED | Closed from exact-main SHA `efa56808db13fa45f803131f7bcf2d65c485a66d`; Planning `34228695674`, P00 `34228695950`, P01 `34228695746`, P02 `34228695995` SUCCESS; protected first-run Setup, SQL negative/retry, Review/Health critical gate and bilingual browser evidence verified; exact-main artifact produced and hashed |
| P03 | CLOSED | Closed from exact-main SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91`; Planning `34239792230`, P00 `34239792270`, P01 `34239792297`, P02 `34239792244`, P03 web security `34239792235`, P03 identity/account security `34239792740` SUCCESS; exact-main identity/browser/security artifact produced and hashed |
| P04 | CLOSED | Closed from exact-main SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`; 8/8 applicable exact-main workflows SUCCESS; RBAC, Default Deny, server-side/service authorization, admin/IDOR and bilingual Permissions UI evidence verified and artifact produced/hashed |
| P05 | CLOSED | Closed from exact-main implementation SHA `4d706fab57071236e9847670342f5a399b3527e6`; 10/10 applicable exact-main workflows SUCCESS; metadata catalog, versioning, isolation/security, JSON-schema import/export, generic sample proof and bilingual protected administration verified; artifact produced/hashed |
| P06 | CLOSED | Corrected closure baseline exact-main implementation/security SHA `fef5882abf8a6f12990c3e7c0e9f849d08cd7947`; 13/13 exact-main workflow runs SUCCESS; scoped SecretRefs, isolated/explicitly shared AuthProfiles, atomic rotation CAS, centralized redaction, runtime token-cache identity/expiry/single-flight safety, protected bilingual administration and independent negative/no-leak evidence verified and artifacts produced/hashed; final reconciliation PR #32 integrated at `90b3068ea39a342392222ae581e568b94f7f9004` with 15/15 post-reconciliation exact-main push workflows SUCCESS |
| P07 | CLOSED | Closed from exact integrated implementation baseline `9535fa158441160ab7c7d204863776e38e560a33`; PRs #33, #34, #36, #37 and #38 integrated; PR #39 was intermediate progress reconciliation; 16/16 exact-main workflows SUCCESS; dedicated runtime, Service Execution UI and upgrade-persistence artifacts produced and SHA-256 hashed; exact Service + Environment + AuthProfile isolation, generic metadata-driven execution, resilience, response mapping/masking and bilingual responsive UI evidence verified; no owner/external evidence deferred |
| P08 | OPEN / READY | Canonical current phase: MOJ x-api-key + `/genToken` + Bearer flow based on official docs/fixtures without assuming credentials are shared between MOJ services |
| P09 | LOCKED | MOJ entity + five services, exact official request/response contracts, independent endpoint/auth bindings, tests and authorized UAT smoke if available |
| P10 | LOCKED | Request/result history, masking, scoped access and permissioned exports |
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

## P02 closure record

P02 was integrated through PRs #6, #7, #8 and #9 at exact `main` SHA `efa56808db13fa45f803131f7bcf2d65c485a66d`. Planning/P00/P01/P02 workflows succeeded and protected first-run Setup, SQL negative/retry, critical Review/Health gating and bilingual browser evidence were verified. No owner-only evidence was deferred.

## P03 closure record

P03 was integrated through PRs #12 and #13 at exact `main` SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91`. All relevant Planning/P00–P03 workflows succeeded. Identity persistence, MFA, privileged-account policy, lockout/rate limiting, CSRF/security headers/cookie posture, sanitized audit and bilingual browser evidence were verified. Detailed evidence: `docs/evidence/P03_IDENTITY_ACCOUNT_SECURITY.md`.

## P04 closure record

P04 culminated through PR #20 at exact `main` SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`; 8/8 applicable exact-main workflows succeeded. Editable roles/permissions, per-service Default Deny, server-side authorization, last-System-Administrator protection, forged-identifier denial and bilingual responsive Permissions UI evidence were verified. Detailed evidence: `docs/evidence/P04_RBAC_PERMISSIONS_UI.md`.

## P05 closure record

P05 was normally integrated through PR #22 at exact implementation `main` SHA `4d706fab57071236e9847670342f5a399b3527e6`; 10/10 applicable exact-main workflows succeeded. Metadata Entity/Environment/Service/ServiceField/ResultMapping persistence, independent UAT/Production bindings, secure validation/versioning, JSON import/export, protected bilingual administration and generic service proof were verified. Detailed evidence: `docs/evidence/P05_METADATA_CATALOG.md`.

## P06 closure record

P06 converged through PRs #24, #25, #26, #27, #28 and #29. A live higher-authority plan audit after initial reconciliation PR #30 recovered the legitimate runtime token-cache requirement instead of duplicating it, and PR #31 normally integrated that work. Corrected implementation/security baseline is `fef5882abf8a6f12990c3e7c0e9f849d08cd7947`; 13/13 exact-main workflow runs succeeded, including token-cache, independent security/no-leak and AuthProfile administration/browser acceptance. Final corrected reconciliation PR #32 integrated at `90b3068ea39a342392222ae581e568b94f7f9004` with 15/15 exact-main push workflows successful. Detailed evidence: `docs/evidence/P06_SECRET_VAULT_AUTH_PROFILES.md`.

## P07 closure record

P07 is **CLOSED** from exact integrated implementation baseline `9535fa158441160ab7c7d204863776e38e560a33`.

Normal integration/reconciliation chain:

- PR #33 — `P07::execution-auth-binding-security`, merged at `de4cffc6cf98672ab1ce41dd2fd0d54df765a0fc`.
- PR #34 — `P07::upgrade-persistence-acceptance`, merged at `77bcecc7c090ce28a607fe527caebb96170d78b4`.
- PR #36 — `P07::service-execution-ui-parity`, merged at `b002ce8e9a79fc03c2c9f96fae8c4bc6956b4c49`.
- PR #37 — `P07::generic-execution-runtime`, normally merged.
- PR #39 — intermediate integrated-progress reconciliation.
- PR #38 — final Service Execution UI/runtime convergence normally merged, yielding exact implementation `main` SHA `9535fa158441160ab7c7d204863776e38e560a33`.

All **16/16 exact-main workflow runs** on `9535fa158441160ab7c7d204863776e38e560a33` completed successfully with no failure, queued or in-progress run after completion.

Exact-main P07 evidence:

- P07 Generic Execution Runtime run `34305403834` — SUCCESS; `P07-Generic-Execution-Runtime-9535fa158441160ab7c7d204863776e38e560a33`, 809 bytes, digest `sha256:322395583f95a1f8fd1cacab0975ca0f4b3c0c971de1a86fa44185ed3f57a8dc`.
- P07 Service Execution UI run `34305403861` — SUCCESS; `P07-Execution-UI-Evidence-9535fa158441160ab7c7d204863776e38e560a33`, 372,853 bytes, digest `sha256:5d9b9fd470d6f953fb120391c42130a01412a9b4e8a6288399170cf8399b8f78`.
- P07 Upgrade Persistence Acceptance run `34305403851` — SUCCESS; `p07-upgrade-persistence-evidence-9535fa158441160ab7c7d204863776e38e560a33`, 647 bytes, digest `sha256:5ec9fd3f01925d62cf47d375be1bf591b27dd2eebf9c3848ea448e01c00482ef`.

Acceptance covers server-side `Services.Execute` authorization; exact Service + Environment + AuthProfile resolution; forged/cross-scope rejection and no fallback; metadata-generated fields and client/server validation; RequestId/CorrelationId; `IHttpClientFactory` outbound execution; bounded timeout/retry with POST retry only when explicitly `SafeToRetry`; HTTP/TLS/network classification; bounded response reads; ResultMappings; endpoint-alias/status/duration secret-safe diagnostics; sensitive raw-response masking; required success/error UX; bilingual Arabic RTL / English LTR responsive and accessible Service Execution UI; visual/browser evidence; fake-endpoint acceptance; upgrade/data-preservation safety; and preservation of P00–P06 baselines.

At closure-reconciliation start there were no open P07 PRs. No owner-only or external P07 evidence is deferred. The closure reconciliation itself introduces no P08 implementation. Detailed immutable evidence is recorded in `docs/evidence/P07_GENERIC_EXECUTION_ENGINE.md`; the former `docs/evidence/P07_INTEGRATED_PROGRESS.md` is retained only as a superseded historical progress record.

## Cross-phase gates

The following apply to every phase:

- **LIVE STATE FIRST:** inspect main/PRs/branches/CI/claims before new work.
- **NO DUPLICATION:** recover legitimate existing work before creating overlapping work.
- **NO FUTURE PHASE WORK:** current-phase closure controls progression.
- **TEST/REPAIR/RETEST:** failed required tests or exact-main regressions have priority.
- **NO SECRET LEAKAGE:** secrets and personal data must not enter Git/logs/evidence.
- **SERVICE ISOLATION:** endpoint/auth/secret/token configuration defaults to independent Service + Environment bindings; sharing requires an explicit Shared AuthProfile.
- **UI PARITY:** any phase touching one of the four reference screens must preserve the mandatory baseline and capture evidence.
- **OWNER-LAST:** finish all autonomous portions before deferring an external/owner-only check.
- **PUSHED EVIDENCE:** no phase closure from unpushed local work.
- **EXACT-MAIN RECHECK:** after integration, verify the exact new main SHA.

## Initial MOJ scope

1. Marriage Cases Service
2. Is Single Basic Service
3. Marriage Couple Last Case Service
4. Family Judgment Text Service
5. Procuration Status Service

Request/response/auth details must come from official CAIT/MOJ documentation and the references under `docs/moj-api-reference/`; unknown fields must not be invented.
