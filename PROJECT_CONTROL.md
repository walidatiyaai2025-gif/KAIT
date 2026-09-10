# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P00-P15 are formally CLOSED from exact integrated evidence. P16 cloud-actionable implementation and its superseding 0.1.2 exact-main regression recovery are terminal from exact repository evidence. P16 is now in a governance/evidence-only **CLOSURE TRANSITION CANDIDATE** on `worker/p16-closure-reconciliation`. P17 remains **STAGED / IMPLEMENTATION LOCKED** until that reconciliation PR itself passes every governed exact-head check, integrates normally, and the resulting exact-new-main governed CI is terminal green.
- Initial executable version: `0.1.0`
- Pinned SDK / target framework: .NET SDK `10.0.400` / `net10.0`
- Initial entity: Ministry of Justice (MOJ), Kuwait
- UI languages: Arabic (RTL) and English (LTR)
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server

## Last closed phase evidence

P13 is **CLOSED** after its closure transition PR #72 was normally integrated at exact `main` `9f76f6593123a86c1ef999ba5ec5b7b9338a53de` and the resulting exact-main matrix completed **32/32 push workflows SUCCESS**.

P14 is **CLOSED**. PR #73 normally integrated exact implementation head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` after **37/37 governed pull-request workflows SUCCESS**. The resulting exact implementation `main` SHA `1f1def164d639c75d9cc26710a905cc491355a2b` completed **34/34 push workflows SUCCESS**. Governance/evidence closure PR #74 then normally integrated exact closure head `58022cc53c75a986cca3cb119671c213ca1fdf4d` into exact `main` `f5dd87de7aa3f8e709d04cf8804f53c10c938633`, whose resulting exact-main gate completed **33/33 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0 at formal closure verification.

P14 closed evidence covers restricted-session authorization repair, independent RBAC fail-closed enforcement, authenticated challenge throttling, anti-forgery and Razor output-encoding checks, CSP/security-header preservation, bounded retry/timeout/cancellation/response behavior, token-refresh concurrency safety, dependency vulnerability/deprecation review and preservation of all closed P00-P13 security/isolation contracts. The P14 register has no known cloud-actionable Critical or High vulnerability. Detailed evidence: `docs/evidence/P14_SECURITY_HARDENING.md`.

P15 is formally **CLOSED**. Final implementation PR #75 head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` completed **37/37 governed pull-request workflows SUCCESS** and was normally integrated as exact implementation `main` `d60318da5b19d06df20864083f5a4a78b9792e88`. That exact integrated main completed **34/34 governed push workflows SUCCESS**. Exact-main P15 run `34440072557` completed 45 static installer checks, package/lifecycle acceptance, ownership/destructive-operation negatives, maintenance-state preservation, sanitized logging, execution-plan validation and SHA-256 verification. Closure-transition PR #77 exact head `7ad7f07b760f3102bd2776cdfe4e35d2fb427765` completed **37/37 governed pull-request workflows SUCCESS**, normally integrated as exact closure main `48267786e9f0d21934dee339eed32688b6e2d488`, and that exact-new-main completed **34/34 governed push workflows SUCCESS**, failure=0, queued=0 and in-progress=0.

Exact integrated P15 ZIP SHA-256 is `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`; Setup EXE SHA-256 is `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`; artifact `10137632143` has archive digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`.

Historical P08/P09 owner/external classifications remain unchanged. Evidence proven for UAT is not promoted to Production. `DEFERRED_EXTERNAL_NOT_PASS` and `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` remain NOT PASS and Production→UAT fallback is forbidden. Target-server/certificate/signing evidence remains owner/external NOT PASS where unavailable. Main branch protection remains `OWNER_LAST / NOT PASS` while live read-back reports `protected=false` and required status-check enforcement off.

## Authoritative documents

Priority order when instructions conflict:

1. Live repository state and exact `main` evidence
2. `AGENTS.md`
3. `CURRENT_PHASE.md`
4. `docs/TASK_LEDGER.md`
5. `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`
6. phase-specific requirements in `execution/GSIP_Full_Execution.json`
7. `docs/FINAL_ACCEPTANCE_CRITERIA.md`
8. `docs/UI_DESIGN_PARITY_GATE.md`
9. `docs/OWNER_LAST_EXECUTION_POLICY.md`
10. `docs/plans/GSIP_Complete_Implementation_Plan_AR.md`

No old prompt, screenshot caption, branch description, stale ledger or superseded evidence overrides live evidence.

## Phase policy

- Exactly one canonical current phase exists at a time.
- P00 through P15 are CLOSED from integrated evidence.
- P16 is the sole canonical phase in **CLOSURE TRANSITION CANDIDATE** state through `P16::closure-reconciliation`, branch `worker/p16-closure-reconciliation`.
- P17 remains **STAGED / IMPLEMENTATION LOCKED** until the P16 reconciliation PR final exact head passes every governed workflow, the PR normally integrates from exact current main, and every governed workflow on the resulting exact-new-main is terminal SUCCESS.
- Phase exit requires implementation/acceptance + tests + evidence + documentation reconciliation + pushed commit + required CI + exact-main recheck.
- Integration recovery and exact-main regressions take priority over new feature or acceptance work.
- A phase-transition branch does not authorize future-phase implementation until that transition is integrated and the resulting exact-new-main gate is green.
- Legitimate future-phase recovery work may repair a known real defect only under the explicit owner non-stop exception; it remains DO NOT MERGE and does not change canonical phase authority.
- Deferred external evidence remains explicitly deferred and must never be converted to PASS without real evidence.
- Evidence proven for one environment or operation must not be promoted to another environment or operation without official support.

## Branch and PR policy

- `main` is the canonical integrated branch.
- Workers must inspect active branches/PRs before creating new work.
- Use short-lived feature/recovery branches when parallel or reviewable work requires them.
- Never duplicate an active worker's legitimate scope.
- Merge only when repository permissions/policy authorize it and required checks pass.
- After merge, re-run the relevant acceptance on the exact new `main` SHA.

## Versioning

P00 pinned .NET 10 LTS / `net10.0`, SDK `10.0.400`, initial version `0.1.0` and artifact naming in `docs/BUILD_AND_VERSIONING.md`. Do not silently change pinned platform versions later.

## Setup contract

P02 established the protected first-run Setup boundary. Normal Login is illegal before first-run setup completes. Setup covers database connectivity, migrations, initial administrator, security baseline, organization/branding, integration environment, secrets placeholders, health review and Finish. Successful setup persists a protected completed state and prevents accidental public rerun.

## Identity and account-security contract

P03 established the authentication boundary required before detailed RBAC. Runtime access uses ASP.NET Core Identity persistence with protected login/logout, configurable password/session/remember-me and account controls, forced password change, MFA enrollment/verification, privileged-account MFA policy, lockout/rate limiting, server-side authentication challenge, CSRF protection, security headers/HSTS/cookie controls and sanitized authentication audit events.

## RBAC and permissions contract

P04 established the authorization boundary. Runtime authorization uses editable seeded roles, a canonical permission catalog, RolePermissions, UserRoles, per-service permission controls and Default Deny with server-side enforcement. Administration must protect privileged access, reject forged/unknown identifiers, prevent removal of the last enabled System Administrator and preserve the bilingual Permissions management experience.

## Metadata catalog contract

P05 established the metadata-driven Entity/Environment/Service/ServiceField/ResultMapping boundary. Service definitions use independent UAT/Production bindings, secure metadata validation, historical versioning after first use, schema-governed JSON import/export, protected bilingual administration and generic execution metadata without custom per-service Controller/View requirements. Invalid or secret-bearing definitions must fail atomically and must not be persisted by later valid operations.

## Secret and authentication-profile contract

P06 established the Secret Vault/AuthProfile and runtime token-cache safety boundary. Plaintext secrets never belong in metadata, Git, logs or evidence. Runtime references use opaque SecretRefs scoped to the exact AuthProfile, Service and Environment. Shared AuthProfile relationships must be explicit and auditable. Rotation must stage safely, activate atomically against the expected current reference/generation, preserve the current valid secret on failure and advance cache-validity identity on success. Administration is write-only for plaintext secret input and masked-only for display, protected server-side by authorization/IDOR/CSRF controls. Persisted ASP.NET Core Data Protection keys on Windows/IIS use DPAPI protection. Runtime token reuse is in-memory only and keyed by the exact governed token-cache identity; reuse must respect an expiry safety window, coalesce concurrent refresh through single-flight semantics, isolate caller cancellation, reject failed/canceled/unsafe refresh results from caching, and avoid token leakage through normal serialization or diagnostics.

## Generic service execution contract

P07 established the metadata-driven execution boundary. Execution must resolve the exact authorized Service + Environment + AuthProfile binding, never fall back across service/environment boundaries, validate metadata-driven fields on both client and server, propagate RequestId/CorrelationId, use `IHttpClientFactory` with bounded timeout/resilience, retry POST only when explicitly marked `SafeToRetry`, map structured results through canonical ResultMappings, bound response reads, classify required HTTP/TLS/network failures, and keep telemetry/diagnostics secret-free. The Service Execution UI is bilingual Arabic RTL / English LTR, responsive, accessible and high-fidelity, with Result / Raw Response / History presentation and sensitive-response masking. Service-specific authentication semantics layer onto this generic boundary rather than bypass it.

## MOJ authentication contract

P08 established the MOJ authentication layer using repository-preserved official evidence and the existing P06/P07 boundaries. Runtime supports the documented `x-api-key` mechanism where governed metadata requires it, documented `POST /genToken` form-urlencoded username/password token acquisition with the token consumed from response `data`, and transient `Authorization: Bearer ...` attachment where the governed operation requires Bearer authentication. Base URL, header name and token path are configuration/metadata driven. Secrets remain in the Secret Vault; token/cache identity remains exact Service + Environment + AuthProfile + configuration/generation scope; cross-service/environment and Production→UAT fallback fail closed. Administration exposes a server-authorized, anti-forgery protected Test Authentication action that never redisplays token plaintext.

The post-closure UAT Marriage evidence adds a proven composite specialization without creating a second engine: exact-scope `x-api-key`, `username`, and `password` material is used to call `/genToken`; the token response `data` becomes a transient Bearer; the target request carries the same transient API key plus Bearer; API-key generation participates in token-cache validity. Existing None/API-key/static-Bearer/custom-header/legacy token behavior remains supported. This proven specialization applies only to the observed UAT Marriage contract. Unproven Production operation details remain owner-last external and must fail closed rather than inherit UAT configuration.

## P09 MOJ five-service contract boundary

P09 seeded/implemented only these five services from repository-approved official evidence:

1. Marriage Cases Service
2. Is Single Basic Service
3. Marriage Couple Last Case Service
4. Family Judgment Text Service
5. Procuration Status Service

Unconfirmed Production details remain deferred and fail closed rather than being inferred.

## P10 request/history contract boundary

P10 established canonical request/result history with bounded protected persistence, scoped Own/Department/All access plus current service visibility, filtering with active filter preservation across pagination, retention/migration/concurrency safety and permissioned export/print behavior. Export operations are auditable and sensitive data remains governed by the existing masking/protection policies. Closed-baseline regression repair PR #65 added executable coverage for pagination filter preservation.

## P11 audit/monitoring contract boundary

P11 is CLOSED from exact integrated `main` SHA `3d0a77f3f76fac37691cdd19a030932c876bd1e5`. It established the canonical tamper-evident audit and monitoring boundary: append-only database enforcement, monotonic SHA-256 chained integrity, mutation/tail-deletion/gap detection, safe concurrent append, verifiable retention checkpointing, sanitized monitoring, protected high-fidelity Audit administration, bilingual UI and executable security/LocalDB/runtime/browser evidence. P11 preserves all closed P00-P10 controls.

## P12 admin operations / health / diagnostics contract boundary

P12 is CLOSED from exact integrated `main` SHA `1ed40707552f62058980b44d6cb1e7251dbeb2d4`. It converged production-grade administration and operational diagnostics over the existing P05-P11 boundaries without creating competing persistence, secret, authentication, execution or audit engines.

The closed P12 boundary includes exact Entity -> Service -> Environment operational state; independent UAT/Production handling; protected Test Connection/Test Authentication; activation/disable through a dedicated no-revision state service; safe secret rotation through the canonical P06 boundary; bounded timeout/TLS/proxy/endpoint diagnostics; database/Data Protection/runtime/disk/integration health; repeated-failure and readiness alerts; server-side authorization/IDOR/CSRF controls; sanitized audit/evidence; bilingual Arabic RTL / English LTR administration; and executable SQL LocalDB/runtime/browser acceptance. The final repair proves operational toggles preserve exact ServiceId, metadata version, AuthProfile/SecretRef scope and sibling-environment configuration.

## P13 UX / accessibility / parity contract boundary

P13 is CLOSED after normal integration of implementation PR #71 and closure transition PR #72. Its final closure-transition main is `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`, which completed **32/32 push workflows SUCCESS**.

The closed boundary includes Arabic RTL / English LTR rendering, reachable mobile primary navigation, skip-to-content, visible keyboard focus, semantic labels/statuses, explicit LTR technical identifiers, protected P10 History routing from Service Execution, reference-card hierarchy on Home, and least-privilege catalogue-backed metrics without widening Request/Audit visibility solely for visual parity. Closed P00-P12 security and isolation boundaries remain authoritative.

## P14 security hardening / resilience closure boundary

P14 is formally **CLOSED**. PR #73 final implementation head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` passed **37/37** governed pull-request workflows and integrated as exact implementation main `1f1def164d639c75d9cc26710a905cc491355a2b`, which completed **34/34** push workflows SUCCESS. Closure PR #74 then integrated exact closure head `58022cc53c75a986cca3cb119671c213ca1fdf4d` into exact main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`, which completed **33/33** push workflows SUCCESS.

The closed boundary includes authorization-bypass/IDOR hardening for restricted authentication sessions, RBAC defense in depth, CSRF/XSS/output-encoding acceptance, authenticated challenge rate limiting, secure-header preservation, secret/log leakage protection, retry/timeout/cancellation/response bounds, token-refresh concurrency safety, dependency vulnerability/deprecation review, threat-model evidence and preservation of all earlier service/environment/authentication/audit boundaries. No known cloud-actionable Critical/High vulnerability remains in the P14 register.

## P15 installer / packaging closed boundary

P15 is formally **CLOSED**. Canonical implementation unit `P15::installer-packaging-convergence` used branch `worker/p15-installer-packaging`, PR #75. The final exact implementation head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` passed **37/37 governed pull-request workflows SUCCESS** before normal merge. PR #75 integrated as exact implementation `main` `d60318da5b19d06df20864083f5a4a78b9792e88`; that exact integrated main passed **34/34 governed push workflows SUCCESS**, failure=0, queued=0 and in-progress=0.

The closed cloud implementation boundary provides a self-contained single-file Windows GUI Setup EXE with Next/Back/Finish wizard; Windows/IIS prerequisite validation; application path/site/app-pool controls; explicit HTTP/HTTPS binding with Local Computer certificate selection; host-specific SNI correctness; protected First-Run Setup launch; install/upgrade/repair/default-uninstall/reinstall/explicit-purge lifecycle; fail-closed install-root and IIS ownership isolation; atomic ownership-manifest rollback; `App_Data`/Data Protection/setup-state preservation; sanitized install logging; deterministic versioned ZIP/EXE packaging; and SHA-256 verification.

Exact integrated P15 run `34440072557` completed `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`, 0-warning/0-error package build and full Setup EXE lifecycle acceptance. Exact integrated package identities are:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`;
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`;
- Actions artifact `10137632143`, digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`, size 62,159,500 bytes.

Formal closure unit `P15::closure-reconciliation` used `worker/p15-closure-reconciliation`, PR #77. Exact closure head `7ad7f07b760f3102bd2776cdfe4e35d2fb427765` passed **37/37 governed pull-request workflows SUCCESS**. PR #77 normally integrated as exact closure main `48267786e9f0d21934dee339eed32688b6e2d488`; that exact-new-main completed **34/34 governed push workflows SUCCESS**, failure=0, queued=0 and in-progress=0. Detailed evidence: `docs/evidence/P15_INSTALLER_PACKAGING.md`.

Target-server Windows/IIS deployment proof, owner-approved Production certificate selection, Production DNS/network/proxy/firewall behavior, owner-controlled signing and previously deferred live MOJ evidence remain external/owner-only and are NOT PASS where unavailable. They are not converted to cloud PASS by P15 closure evidence.

## P16 full automated acceptance boundary

P16 cloud-actionable implementation and the superseding 0.1.2 regression recovery are terminal from exact integrated evidence. P16 is in **CLOSURE TRANSITION CANDIDATE** state on the canonical `worker/p16-closure-reconciliation` line; P17 remains locked until this governance/evidence-only transition itself is integrated and its resulting exact-new-main gate is green.

Canonical P16 implementation unit `P16::full-acceptance-release-candidate` used branch `worker/p16-full-acceptance-continuation`, PR #80. Final implementation head `e655dc4d0effb4f962d3e97e4a0230471238ba55` completed **38/38 governed pull-request workflows SUCCESS** and integrated as exact implementation main `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85`, which completed **35/35 governed push workflows SUCCESS**.

The later API129 UAT authentication regression was finally recovered by PR #85. Its exact corrected head `0e767d4f355e403b70602a7645cd7ffba25b3b43` completed **40/40 governed pull-request workflows SUCCESS** and was normally integrated as exact main `8863b4b8151ebb08e98fdb225ea5418c5297eb5c`. That exact main completed **36/36 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at terminal verification. Exact-main P16 run `34456867989` completed `static-contract`, `release-candidate-package`, `runtime-ui-recovery`, and aggregate `p16-gate` SUCCESS on that same SHA.

The accepted 0.1.2 API129 UAT contract is exact-scope `x-api-key + username + password` -> `/genToken` -> transient Bearer from response `data` -> target request carrying the same API key plus Bearer. Optional empty-credential placeholders and broad cross-service MOJ authentication convergence are removed. P04 Default Deny remains authoritative, including per-service permissions. Production remains independent and fail-closed; no UAT credential/endpoint fallback is permitted.

Current same-SHA P16 closure candidate is version `0.1.2`:

- `GSIP-0.1.2-win-x64.zip` — SHA-256 `b4971d31b11f3c1c6a6afba9026afd6865b7c006063eb59ffbdc9e2182efd426`;
- `GSIP-0.1.2-Setup-x64.exe` — SHA-256 `7a5de1c18bf9b84bd052d855d089f51ffdae175d40c872b574a68892fba99ff0`;
- release-candidate artifact `10143946003`, digest `sha256:b7c8f175b1669323917bbfa6b557076ce8b2d2c611af7009b13f83e4282429d6`, size 62,185,768 bytes;
- runtime/UI artifact `10144204144`, digest `sha256:4b5564603f8c00c5d07357dd206647f0f4dabfa5b04cd8fcc931bc1ab5a4965d`, size 3,482,259 bytes.

Earlier 0.1.0/0.1.1 P16 release identities remain historical provenance only. They must not substitute for the exact 0.1.2 closure candidate above.

Canonical closure unit is `P16::closure-reconciliation`, branch `worker/p16-closure-reconciliation`. Before P16 may be recorded formally CLOSED and P17 opened, the reconciliation PR final exact head must pass every governed pull-request workflow, normally integrate from exact current main, and every governed workflow on the resulting exact-new-main must be terminal SUCCESS. Historical/external UAT, unproven Production, target-server/certificate/signing and branch-protection evidence remains NOT PASS where unavailable.

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

Repository administration is a separate acceptance boundary: live P16 reconciliation read-back shows `main` with `protected=false` and required status-check enforcement off. This remains `OWNER_LAST / NOT PASS` until an authorized administrator applies the required policy and independent read-back proves it.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim. `VERIFIED_FINAL_COMPLETE` is forbidden before actual P17 exact final evidence exists.