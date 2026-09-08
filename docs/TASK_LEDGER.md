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
| P07 | OPEN | Canonical current phase: Generic service execution engine resolving the exact Service + Environment + AuthProfile binding; high-fidelity dynamic Service Execution UI; no open P07 implementation/evidence unit is treated as integrated or PASS until normally merged and exact-main verified |
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

P02 was integrated through PRs #6, #7, #8 and #9, culminating at exact `main` SHA `efa56808db13fa45f803131f7bcf2d65c485a66d`. On that same SHA, Planning Integrity run `34228695674`, P00 Build Baseline regression run `34228695950`, P01 Architecture and UI Shell regression run `34228695746`, and P02 First-run Setup Wizard regression run `34228695995` all succeeded. The exact-main artifact `P02-Setup-Evidence-efa56808db13fa45f803131f7bcf2d65c485a66d` is 390,868 bytes with workflow digest `sha256:f03ad21356d2ba92308b2cff015d934d0ef23b9e78b02a4f0eb26c9cd06f8961`. Acceptance includes pre-Login setup gating, protected restart-safe state, SQL Windows/SQL-auth paths, wrong credentials, create/use DB, migration failure/retry, bootstrap administrator, isolated inactive MOJ UAT/Production placeholders, critical Review/Health blocking, post-Finish lock and bilingual browser evidence. No owner-only evidence was deferred for P02.

## P03 closure record

P03 was integrated through the hardened web-security PR #12 and the recovered Identity/MFA/account-security PR #13, culminating at exact integrated `main` SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91`. On that same SHA, Planning Integrity run `34239792230`, P00 Build Baseline regression run `34239792270`, P01 Architecture and UI Shell regression run `34239792297`, P02 First-run Setup Wizard regression run `34239792244`, P03 Web Security Headers run `34239792235`, and P03 Identity and Account Security run `34239792740` all succeeded.

The exact-main artifact `P03-Identity-Evidence-20d2542427a388ac81e5249cc59f60dcbfa2ea91` is 330,634 bytes with workflow digest `sha256:d12317bb99d8a3835bd0a4a34db0688af5c164e1e3865e13f76828b1bbac9245`. Acceptance covers Identity persistence, login/logout, configurable password/session/remember-me and account controls, forced password change, MFA enrollment/verification and privileged-account MFA policy, lockout/rate limiting, authenticated dashboard challenge, CSRF rejection, CSP/HSTS/security headers, secure cookie posture, sanitized authentication audit events and Arabic RTL / English LTR browser evidence at desktop and mobile viewports. No owner-only or external evidence was deferred for P03. Detailed evidence is recorded in `docs/evidence/P03_IDENTITY_ACCOUNT_SECURITY.md`.

## P04 closure record

P04 culminated in the normally merged permissions administration/UI PR #20 at exact integrated `main` SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`. On that same SHA, Planning Integrity `34253075877`, P00 Build Baseline `34253075003`, P01 Architecture and UI Shell `34253075362`, P02 First-run Setup Wizard `34253075183`, P03 Web Security Headers `34253075119`, P03 Identity and Account Security `34253074987`, P04 RBAC Core `34253075074`, and P04 Permissions UI `34253075409` all succeeded.

The exact-main artifact `P04-Permissions-UI-Evidence-c3aa76a8d456c9b951b602001bcbe1023ae26dd9` is 476,233 bytes with workflow digest `sha256:b5b6fa42c7dd114b302a2db3572c25130f01e2c045586a90bcaf90b97d290826`. Acceptance covers editable seeded roles, permission catalog, RolePermissions/UserRoles, per-service permission matrix, Default Deny, server-side authorization, role create/rename, user-role assignment/removal, last-enabled-System-Administrator protection, forged identifier denial, truthful pending-approvals presentation and Arabic RTL / English LTR responsive browser evidence. No owner-only or external evidence was deferred for P04. Detailed evidence is recorded in `docs/evidence/P04_RBAC_PERMISSIONS_UI.md`.

## P05 closure record

P05 was implemented on `worker/p05-metadata-catalog` and normally integrated through PR #22, yielding exact implementation `main` SHA `4d706fab57071236e9847670342f5a399b3527e6`. All ten applicable exact-main workflows succeeded on that SHA, including P05 Metadata Catalog run `34262416559`.

The exact-main artifact `P05-Metadata-Evidence-4d706fab57071236e9847670342f5a399b3527e6` is 507,662 bytes with workflow digest `sha256:4f903fb240b6f0195fa7ad260afac9b45ece0d443e57f0752457d567abf41796`. Acceptance covers metadata-driven Entity/Environment/Service/ServiceField/ResultMapping persistence; independent UAT/Production service bindings; Production HTTPS and server-certificate validation; secret-bearing header rejection; atomic rejection of invalid definitions; used-definition versioning and historical preservation; schema-governed JSON export/import round trip; protected Arabic RTL / English LTR administration; and a complete synthetic service without custom per-service Controller/View code. No owner-only or external evidence was deferred for P05. Detailed evidence is recorded in `docs/evidence/P05_METADATA_CATALOG.md`.

## P06 closure record

P06 originally converged through normally merged PRs #24 (rotation/redaction/cache), #27 (focused fail-closed redaction regression), #28 (Windows/IIS DPAPI Data Protection key protection), #25 (canonical Secret Vault/SecretRef/AuthProfile foundation), #29 (server-authorized bilingual AuthProfile administration/browser evidence), and #26 (independent security acceptance/evidence). PR #30 then recorded an initial closure baseline.

A final LIVE STATE / execution-plan audit found that the higher-authority canonical P06 plan also required an actual runtime token cache with an expiry safety window and single-flight refresh. The already-existing legitimate `P06::token-cache-runtime` branch was recovered rather than duplicated, reconciled onto current main, validated across the repository and normally integrated through PR #31. The corrected final exact implementation/security `main` SHA is `fef5882abf8a6f12990c3e7c0e9f849d08cd7947`; this corrected record supersedes the earlier PR #30 P06 evidence baseline without introducing P07 implementation.

All **13/13 exact-main workflow runs** succeeded on the corrected implementation SHA. P06 Token Cache Runtime run `34281504875` succeeded. P06 Security Acceptance and Evidence run `34281505005` succeeded after executing the pinned SDK/Release build, LocalDB, P00–P05 regression contracts, P05 bilingual browser acceptance, canonical P06 core/migrations, P06 rotation/redaction/cache, P06 AuthProfile authorization/browser acceptance, independent negative security checks, governance validation and fail-closed no-leak evidence packaging. P06 AuthProfile Administration run `34281505040` also succeeded with exact-main bilingual/390px browser evidence.

The corrected exact-main security artifact `P06-Security-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947` is 5,975 bytes with digest `sha256:2a3ccafdd523e513caca803514b1dde6b3da349460d5715bca17e6841fe5a573`. The corrected exact-main administration/browser artifact `P06-AuthProfile-Admin-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947` is 557,361 bytes with digest `sha256:aa2adb57ffb3cd7ecece6c696ed4df3bec0bb3c41ad99180b0fb530ac10bce91`.

Acceptance covers opaque SecretRefs; strict Service + Environment + AuthProfile ownership; explicit audited sharing; rejection of forged/cross-scope identifiers; durable staged secret rotation with expected-current-reference/generation CAS, rollback and cache-validity version advancement; centralized masking/redaction with fail-closed leak sentinels; exact Service + Environment + AuthProfile + version/generation token-cache identity; runtime-only in-memory token reuse with configurable expiry safety window; single-flight refresh per exact cache identity; caller-cancellation isolation; fail-safe non-caching of failed, canceled or near-expiry refresh results; secret-safe token serialization/diagnostics; protected AuthProfile metadata/update/shared-unbind lifecycle; owner binding protection; write-only plaintext secret administration and masked-only display; server-side authorization, IDOR and CSRF enforcement; Arabic RTL / English LTR responsive browser parity evidence; DPAPI protection for filesystem-persisted ASP.NET Core Data Protection keys on Windows/IIS; and preservation of P00–P05 baselines. No owner-only or external evidence was deferred for P06.

The final corrected closure reconciliation was normally integrated through PR #32 at exact `main` SHA `90b3068ea39a342392222ae581e568b94f7f9004`. All 15 applicable exact-main push workflows on that SHA completed successfully with no failure, queued, or in-progress run. P07 is therefore the sole legal current implementation phase and remains OPEN; P08–P17 remain locked. No open P07 implementation, evidence, deferred, or unknown item is converted to PASS by this reconciliation.

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
