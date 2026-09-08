# TASK_LEDGER.md — GSIP Canonical Phase Ledger

This ledger is evidence-driven. Never mark a phase CLOSED because a prompt says it should be closed. Update status only from live implementation/tests/CI/evidence on the exact integrated commit.

| Phase | Status | Scope / closure focus |
|---|---|---|
| P00 | CLOSED | Closed from exact-main SHA `8156c46ce8ce366424d955a6194d677c2da5055f`; Planning Integrity `34210899288` SUCCESS; P00 Build Baseline `34210899267` SUCCESS; exact-main baseline artifact produced and hashed |
| P01 | CLOSED | Closed from exact-main SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3`; Planning `34213985141`, P00 regression `34213985591`, P01 runtime/browser `34213985167` SUCCESS; exact-main bilingual UI evidence produced and hashed |
| P02 | OPEN | Complete first-run Setup Wizard before Login, SQL Server test/create/use/migrations/admin/security/review/finish; create service/environment placeholders only, never source-coded live credentials |
| P03 | LOCKED | Identity, MFA, sessions, lockout/rate limiting, account security and auth audit |
| P04 | LOCKED | RBAC, server-side authorization, per-service permissions, high-fidelity permissions UI |
| P05 | LOCKED | Metadata-driven entities/services/fields/result mappings plus independent `ServiceEnvironmentConfig` per Service + UAT/Production environment; admin/versioning/import-export |
| P06 | LOCKED | Secret vault, isolated authentication profiles/SecretRefs, explicit Shared AuthProfile support, rotation/redaction, cross-service-safe token cache keys |
| P07 | LOCKED | Generic service execution engine resolving the exact Service + Environment + AuthProfile binding; high-fidelity dynamic Service Execution UI |
| P08 | LOCKED | MOJ x-api-key + `/genToken` + Bearer flow based on official docs/fixtures without assuming credentials are shared between MOJ services |
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

P00 implementation was integrated by PR #2. The exact integrated `main` SHA `8156c46ce8ce366424d955a6194d677c2da5055f` passed both required workflows and produced `GSIP-P00-Baseline-0.1.0-8156c46ce8ce.zip` with workflow artifact digest `sha256:455be98b93727c20646193da3682f92a20bbc9d373881ea6b3b4f443d967a731`. No owner-only evidence was deferred for P00.

## P01 closure record

P01 implementation was integrated by PR #4 at exact `main` SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3`. Planning Integrity run `34213985141`, P00 Build Baseline regression run `34213985591`, and P01 Architecture and UI Shell run `34213985167` all succeeded on that SHA. The exact-main UI artifact `P01-UI-Evidence-34c665b12e910b6dfcb735f4978be6649e03d5c3` has workflow digest `sha256:11492dc4afa4d2257f410540cf757a40be91926e89a61611896bc3f30b06d09c`. It contains English/Arabic Dashboard and Login browser captures plus manifest hashes. No owner-only evidence was deferred for P01.

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
