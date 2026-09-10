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
| P10 | CLOSED | Closed from exact integrated and repaired main `b7e81cece985b566c9c3222a2c495b83e800e080`; PR #62 integrated request/result history and exports; closed-baseline repair PR #65 preserved `fromUtc`/`toUtc` and all active filters across pagination and added executable regression coverage; PR #65 exact-head **32/32 SUCCESS** and resulting exact-main **29/29 SUCCESS** |
| P11 | CLOSED | Closed from exact integrated main `3d0a77f3f76fac37691cdd19a030932c876bd1e5`; PR #67 exact head `ae61ff7cedaf119bc6948a5b18eda811ab249b82` passed **36/36** workflows; exact integrated main passed **31/31** push workflows after P01 run `34408109285` same-SHA attempt 2 succeeded with no source/acceptance change; append-only/tamper/retention/concurrency/monitoring/security/browser evidence verified |
| P12 | CLOSED | Closed from exact integrated main `1ed40707552f62058980b44d6cb1e7251dbeb2d4`; PR #69 exact head `f367ca78fca217e9d5c7da0a1328dca047940390` passed **35/35** workflows; exact integrated main passed **31/31** push workflows; P12 exact-main run `34413925793` SUCCESS with static/runtime artifacts; protected admin operations, health/diagnostics, no-revision ServiceId/version preservation, UAT/Production isolation, authorization/CSRF/IDOR and bilingual runtime/browser evidence verified |
| P13 | CLOSED | Implementation PR #71 and closure transition PR #72 normally integrated; final closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de` completed **32/32** push workflows SUCCESS |
| P14 | CLOSED | PR #73 exact implementation head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` passed **37/37** governed workflows; implementation main `1f1def164d639c75d9cc26710a905cc491355a2b` passed **34/34** push workflows; closure PR #74 exact head `58022cc53c75a986cca3cb119671c213ca1fdf4d` integrated as exact main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`, which passed **33/33** push workflows; no known cloud-actionable Critical/High vulnerability remains |
| P15 | CLOSED FROM IMPLEMENTATION EVIDENCE / TRANSITION PENDING | PR #75 exact head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` passed **37/37** governed workflows; normal integration main `d60318da5b19d06df20864083f5a4a78b9792e88` passed **34/34** push workflows with exact-main installer/package/hash evidence; closure transition + resulting exact-new-main green verification remain mandatory |
| P16 | STAGED / IMPLEMENTATION LOCKED | Full automated acceptance on exact release candidate including setup→login→RBAC→MOJ→history→audit→installer + UI parity + service/environment isolation; becomes canonical only after P15 closure transition merges and resulting exact-new-main CI is terminal green |
| P17 | LOCKED | Final convergence, exact-main regression repair, stale integration recovery, ledger/evidence reconciliation and final release closure |

## P00 closure record

P00 implementation was integrated by PR #2. Exact integrated `main` SHA `8156c46ce8ce366424d955a6194d677c2da5055f` passed the required workflows and produced the hashed P00 baseline artifact. No owner-only evidence was deferred.

## P01 closure record

P01 was integrated through PR #4 at exact `main` SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3`. Planning, P00 regression and P01 runtime/browser workflows all succeeded and exact-main bilingual UI evidence was produced and hashed. No owner-only evidence was deferred.

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
- P09 Independent Contract Security Acceptance `34390739078` — SUCCESS.
- P09 Execution UI and UAT Readiness `34390739312` — SUCCESS.

Authorized live UAT requiring owner-controlled credentials or personal test records remains **DEFERRED_EXTERNAL_NOT_PASS** where unavailable; this is not called PASS. Detailed closure evidence: `docs/evidence/P09_CLOSURE.md`.

## P10 closure record

P10 is **CLOSED** from exact integrated and repaired `main` SHA `b7e81cece985b566c9c3222a2c495b83e800e080` after normal integration of PR #62 and closed-baseline regression repair PR #65.

The canonical baseline persists the required request lifecycle and masked input facts; protects bounded configurable structured/raw result storage; enforces Own/Department/All scope plus current service visibility and IDOR rejection; supports filters/search; preserves `fromUtc`, `toUtc`, scope, search, status, entity, service and page-size filters across pagination; enforces retention/migration/concurrency safety; and provides permissioned Print/PDF/CSV/XLSX exports with injection defense and export audit events. Arabic/English history list/detail surfaces are integrated and Requests is reachable from canonical navigation.

PR #65 repaired the post-PR-#62 pagination defect and added executable UI/static regression coverage for filter preservation. PR #65 completed **32/32 exact-head workflows SUCCESS** before normal merge. Resulting exact-main `b7e81cece985b566c9c3222a2c495b83e800e080` completed **29/29 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 after completion. The exact-main P10 workflow included successful build/security checks and Windows LocalDB migration, scope/IDOR, export/audit/injection-defense, concurrency and retention acceptance. No P10 owner-only requirement was fabricated as PASS. Detailed evidence: `docs/evidence/P10_CLOSURE.md`.

## P11 closure record

P11 is **CLOSED** from exact integrated `main` SHA `3d0a77f3f76fac37691cdd19a030932c876bd1e5` after normal integration of PR #67.

The exact P11 implementation head `ae61ff7cedaf119bc6948a5b18eda811ab249b82` completed **36/36 exact-head workflows SUCCESS** before merge. Acceptance includes canonical append-only audit persistence, SHA-256 hash-chain integrity, mutation/gap/reordering/tail-deletion detection, bounded checkpointed retention, concurrent append serialization, monitoring, sanitized projections, protected permissions/export/integrity actions, bilingual Arabic RTL / English LTR Audit UI, LocalDB integration, CSRF-negative behavior and browser evidence using synthetic-only data.

The resulting exact integrated main `3d0a77f3f76fac37691cdd19a030932c876bd1e5` completed **31/31 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0 after completion. P01 Architecture and UI Shell run `34408109285` attempt 1 failed only because the web runtime did not become healthy while its uploaded stdout/stderr logs were both empty. The same job was rerun against the exact same SHA with no source or acceptance modification; attempt 2 completed SUCCESS including runtime and bilingual browser evidence. No check was waived or weakened.

P11 automated closure requires no owner credential or personal MOJ data. Historical P09 live-UAT and unproven Production evidence remain `DEFERRED_EXTERNAL_NOT_PASS` / `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Detailed evidence: `docs/evidence/P11_AUDIT_TRAIL_MONITORING.md`.

## P12 closure record

P12 is **CLOSED** from exact integrated `main` SHA `1ed40707552f62058980b44d6cb1e7251dbeb2d4` after normal integration of PR #69.

The pre-existing `worker/p12-admin-health-diagnostics` branch was recovered non-force and reused rather than replaced. The final implementation head `f367ca78fca217e9d5c7da0a1328dca047940390` completed **35/35 exact-head workflows SUCCESS** before merge.

A prior green candidate was deliberately superseded after semantic review found that Activate/Disable could pass through metadata definition revisioning for an already-used service. The final repair introduced the dedicated operational-state boundary and executable LocalDB acceptance proving ServiceId, metadata version, AuthProfile/SecretRef scope and the sibling environment remain stable. This was a strengthened gate, not a waiver.

Final P12 acceptance covers protected database/runtime/Data Protection/disk/integration health; bounded endpoint/timeout/TLS/proxy diagnostics; exact Service + Environment Test Connection/Test Authentication; activation/disable with service-level authorization; canonical P06 secret rotation reuse; operational alerts; authorization/IDOR/CSRF negatives; sanitized audit/evidence; Arabic RTL / English LTR responsive Operations runtime; and executable already-used-service UAT/Production isolation.

The resulting exact integrated main `1ed40707552f62058980b44d6cb1e7251dbeb2d4` completed **31/31 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0. Exact-main P12 workflow run `34413925793` completed SUCCESS.

Exact-main P12 artifacts:

- `P12-Admin-Operations-Runtime-1ed40707552f62058980b44d6cb1e7251dbeb2d4` — artifact `10128361921`, digest `sha256:8161d7c95e2ee64cf3c0fdac6b3d252bc0ee0052fe662c1a2973e1b9cc4f194b`.
- `P12-Admin-Operations-Static-1ed40707552f62058980b44d6cb1e7251dbeb2d4` — artifact `10128310606`, digest `sha256:3029f8ddf62255952478fb0197a4399a5724b028a83919eb7de332a9552654d9`.

P12 cloud acceptance requires no live MOJ credential or personal MOJ data. Historical P09 authorized live-UAT and unproven Production evidence remain `DEFERRED_EXTERNAL_NOT_PASS` / `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` and are not rewritten as PASS. Detailed evidence: `docs/evidence/P12_ADMIN_OPERATIONS_HEALTH_DIAGNOSTICS.md`.

## P13 closure record

P13 implementation was normally integrated through PR #71 from exact head `1576764a16ea6ddfed735cb0824cb38de26c83d8` into exact implementation `main` SHA `203cc28db714fae5c2c70e85adc9cc2306bb2107`.

The final implementation candidate completed **35/35 governed PR workflows SUCCESS**. P08 Authentication Convergence had one first-attempt LocalDB creation timeout; the same exact SHA was rerun without any source, test or acceptance change and then completed SUCCESS. No gate was waived or weakened.

The implementation main completed **32/32 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0. Exact-main P13 workflow run `34421612012` completed SUCCESS and produced:

- `P13-Home-UI-203cc28db714fae5c2c70e85adc9cc2306bb2107` — artifact `10131131126`, digest `sha256:19b8a382a59cc049374578d1fc8f865439bc16a523268f74ef98175aeb68fe2e`.
- `P13-UI-Accessibility-Static-203cc28db714fae5c2c70e85adc9cc2306bb2107` — artifact `10131148598`, digest `sha256:062421c5545952d26d42b1a91b8865ffbd4626ef7f3f9d8041504362a3d4e329`.

P13 converged the Home/dashboard to the repository-native card hierarchy, preserved least-privilege data exposure, connected Service Execution History to protected P10 request history, restored all primary routes on narrow screens, added skip/focus/accessibility semantics, preserved Arabic RTL / English LTR and explicit LTR technical identifiers, and retained independent candidate-bound Permissions, Service Execution and Audit browser acceptance. P00-P12 functional/security contracts remain authoritative.

P13 closure reconciliation PR #72 was normally integrated at exact `main` `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`; its resulting exact-main matrix completed **32/32 push workflows SUCCESS**. P13 is therefore fully CLOSED.

Historical owner/external evidence remains `DEFERRED_EXTERNAL_NOT_PASS` / `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Main branch protection remains `OWNER_LAST / NOT PASS`.

## P14 closure record

P14 implementation was normally integrated through PR #73 from exact final implementation head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` into exact implementation `main` SHA `1f1def164d639c75d9cc26710a905cc491355a2b`.

The final P14 implementation candidate completed **37/37 governed pull-request workflows SUCCESS**. The resulting exact implementation main completed **34/34 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0 at implementation closure review.

P14 closes the cloud-actionable hardening scope covering restricted-session authorization bypass prevention, independent RBAC fail-closed enforcement, challenge rate limiting, anti-forgery/XSS/output-encoding checks, secure-header preservation, bounded retry/timeout/cancellation/response behavior, token-refresh concurrency safety, dependency vulnerability/deprecation review and threat-model/security-checklist evidence. The P14 register contains no known cloud-actionable Critical or High vulnerability.

Governance/evidence closure PR #74 then normally integrated exact closure head `58022cc53c75a986cca3cb119671c213ca1fdf4d` into exact main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`. The resulting exact-new-main gate completed **33/33 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0. P14 is therefore formally CLOSED. Detailed evidence: `docs/evidence/P14_SECURITY_HARDENING.md`.

Historical authorized live-UAT and unproven Production evidence remain `DEFERRED_EXTERNAL_NOT_PASS` / `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`; repository protection remains `OWNER_LAST / NOT PASS`.

## P15 closure transition record

P15 became the sole canonical OPEN / ACTIVE phase only after the P14 closure transition was integrated and exact main `f5dd87de7aa3f8e709d04cf8804f53c10c938633` completed **33/33 push workflows SUCCESS**. The canonical implementation line later recovered exact main `78cfe0433bdb02367ddd1365df57d118fea9257a` after legitimate P14 outbound-security repair PR #76, preserving that repair rather than duplicating or reverting it.

P15 implementation converged on the existing canonical tracker unit `P15::installer-packaging-convergence`, branch `worker/p15-installer-packaging`, and PR #75. Before merge, a post-reconciliation audit repaired real ownership/destructive-operation, maintenance rollback and HTTPS/SNI defects on that same line. The final exact implementation head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` completed **37/37 governed pull-request workflows SUCCESS**, failure=0.

PR #75 was then normally integrated as exact implementation `main` `d60318da5b19d06df20864083f5a4a78b9792e88`. The resulting exact-main matrix completed **34/34 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0. Exact-main P15 Installer and Packaging run `34440072557` completed SUCCESS with `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`, 0-warning/0-error package build, ownership-negative and forged-identity cases, clean install, synthetic `0.0.9` upgrade, repair, default uninstall, reinstall, explicit owner-scoped purge, protected-state preservation, sanitized logging, execution-plan integrity and SHA-256 verification all PASS.

Exact integrated P15 artifacts from `d60318da5b19d06df20864083f5a4a78b9792e88`:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`.
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`.
- Actions artifact `10137632143` — digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`, size 62,159,500 bytes.

P15 cloud-actionable implementation is therefore closed from exact implementation evidence, but formal phase closure remains transition-gated. This governance/evidence-only transition must pass its full governed PR matrix, be normally integrated from current exact main, and the resulting exact-new-main matrix must be terminal green before P15 is formally CLOSED and P16 implementation becomes legal.

Actual owner Windows Server/IIS installation, owner-approved production certificate binding, production DNS/network/proxy/firewall behavior and owner-controlled release signing remain external/owner evidence and are **NOT PASS** where unavailable. Historical `DEFERRED_EXTERNAL_NOT_PASS`, `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`, and repository-protection `OWNER_LAST / NOT PASS` classifications remain unchanged.

## Cross-phase gates

The following apply to every phase:

- **LIVE STATE FIRST:** inspect main/PRs/branches/CI/claims before new work.
- **NO DUPLICATION:** recover legitimate existing work before creating overlapping work.
- **NO FUTURE PHASE WORK:** current-phase closure controls progression.
- **TEST/REPAIR/RETEST:** failed required tests or exact-main regressions have priority.
- **NO SECRET LEAKAGE:** secrets and personal data must not enter Git/logs/evidence.
- **SERVICE ISOLATION:** endpoint/auth/secret/token configuration defaults to independent Service + Environment bindings; sharing requires an explicit Shared AuthProfile.
