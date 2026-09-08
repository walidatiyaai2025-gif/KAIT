# TASK_LEDGER.md — GSIP Canonical Phase Ledger

This ledger is evidence-driven. Never mark a phase CLOSED because a prompt says it should be closed. Update status only from live implementation/tests/CI/evidence on the exact integrated commit.

| Phase | Status | Scope / closure focus |
|---|---|---|
| P00 | CLOSED | Closed from exact-main SHA `8156c46ce8ce366424d955a6194d677c2da5055f`; Planning Integrity `34210899288` SUCCESS; P00 Build Baseline `34210899267` SUCCESS; exact-main baseline artifact produced and hashed |
| P01 | CLOSED | Closed from exact-main SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3`; Planning `34213985141`, P00 regression `34213985591`, P01 runtime/browser `34213985167` SUCCESS; exact-main bilingual UI evidence produced and hashed |
| P02 | CLOSED | Closed from exact-main SHA `efa56808db13fa45f803131f7bcf2d65c485a66d`; Planning `34228695674`, P00 `34228695950`, P01 `34228695746`, P02 `34228695995` SUCCESS; protected first-run Setup, SQL negative/retry, Review/Health critical gate and bilingual browser evidence verified; exact-main artifact produced and hashed |
| P03 | CLOSED | Closed from exact-main SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91`; Planning `34239792230`, P00 `34239792270`, P01 `34239792297`, P02 `34239792244`, P03 web security `34239792235`, P03 identity/account security `34239792740` SUCCESS; exact-main identity/browser/security artifact produced and hashed |
| P04 | OPEN | RBAC, server-side authorization, Default Deny, per-service permissions, negative authorization/IDOR tests and high-fidelity bilingual Permissions UI |
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

## P02 closure record

P02 implementation was integrated through PRs #6, #7, #8 and #9, culminating at exact `main` SHA `efa56808db13fa45f803131f7bcf2d65c485a66d`. Planning Integrity run `34228695674`, P00 Build Baseline regression run `34228695950`, P01 Architecture and UI Shell regression run `34228695746`, and P02 First-run Setup Wizard run `34228695995` all succeeded on that SHA. The exact-main artifact `P02-Setup-Evidence-efa56808db13fa45f803131f7bcf2d65c485a66d` is 390,868 bytes with workflow digest `sha256:f03ad21356d2ba92308b2cff015d934d0ef23b9e78b02a4f0eb26c9cd06f8961`. Acceptance includes pre-Login setup gating, protected restart-safe state, SQL Windows/SQL-auth paths, wrong credentials, create/use DB, migration failure/retry, bootstrap administrator, isolated inactive MOJ UAT/Production placeholders, critical Review/Health blocking, post-Finish lock and bilingual browser evidence. No owner-only evidence was deferred for P02.

## P03 closure record

P03 was integrated through the hardened web-security PR #12 and the recovered Identity/MFA/account-security PR #13, culminating at exact integrated `main` SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91`. On that same SHA, Planning Integrity run `34239792230`, P00 Build Baseline regression run `34239792270`, P01 Architecture and UI Shell regression run `34239792297`, P02 First-run Setup Wizard regression run `34239792244`, P03 Web Security Headers run `34239792235`, and P03 Identity and Account Security run `34239792740` all succeeded.

The exact-main artifact `P03-Identity-Evidence-20d2542427a388ac81e5249cc59f60dcbfa2ea91` is 330,634 bytes with workflow digest `sha256:d12317bb99d8a3835bd0a4a34db0688af5c164e1e3865e13f76828b1bbac9245`. Acceptance covers Identity persistence, login/logout, configurable password/session/remember-me and account controls, forced password change, MFA enrollment/verification and privileged-account MFA policy, lockout/rate limiting, authenticated dashboard challenge, CSRF rejection, CSP/HSTS/security headers, secure cookie posture, sanitized authentication audit events and Arabic RTL / English LTR browser evidence at desktop and mobile viewports. No owner-only or external evidence was deferred for P03. Detailed evidence is recorded in `docs/evidence/P03_IDENTITY_ACCOUNT_SECURITY.md`.

P04 is the next legal phase only after this P03 closure-reconciliation change is normally integrated to `main` and the resulting exact-main closed-phase regressions remain green.

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
