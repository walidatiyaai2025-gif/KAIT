# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P14 is formally CLOSED after closure PR #74 and exact-main `f5dd87de7aa3f8e709d04cf8804f53c10c938633` completed 33/33 push workflows SUCCESS. P15 is the sole canonical OPEN / ACTIVE phase under `P15::installer-packaging-convergence`, branch `worker/p15-installer-packaging`, PR #75. P16/P17 remain locked until P15 closes normally.
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

P15 is **OPEN / ACTIVE**. Its canonical implementation PR #75 is based on exact closed-P14 main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`. Validated pre-reconciliation implementation candidate `4e811f17d707f2e1539c5dab9b40c64b6f498cc1` passed P15 Installer and Packaging run `34436171235`, including 36 static checks, 0-warning/0-error package build, explicit previous-version upgrade, repair/uninstall/reinstall/purge lifecycle, protected-state preservation, sanitized log acceptance, execution-plan integrity and SHA-256 verification. This is implementation-candidate evidence only; the reconciliation commit supersedes that SHA as the PR merge candidate and P15 remains OPEN until final exact-head CI, normal merge, exact-new-main CI and closure reconciliation are complete.

Historical P08/P09 owner/external classifications remain unchanged. Evidence proven for UAT is not promoted to Production. `DEFERRED_EXTERNAL_NOT_PASS` and `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` remain NOT PASS and Production→UAT fallback is forbidden. Main branch protection remains `OWNER_LAST / NOT PASS` while live read-back reports `protected=false` and repository rulesets are absent.

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
- P00 through P14 are CLOSED from integrated evidence.
- P15 is the sole canonical OPEN / ACTIVE phase under its existing tracker lease, branch and PR.
- P16–P17 remain locked until their preceding phases close normally.
- Phase exit requires implementation + tests + evidence + documentation reconciliation + pushed commit + required CI + exact-main recheck.
- Integration recovery and exact-main regressions take priority over new feature work.
- A phase-transition branch does not authorize new-phase implementation until that transition is integrated and the resulting exact-new-main gate is green.
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

## P15 installer / packaging active boundary

P15 is **OPEN / ACTIVE** under canonical tracker unit `P15::installer-packaging-convergence`, branch `worker/p15-installer-packaging`, PR #75, based on exact closed-P14 main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`.

The cloud implementation provides a self-contained single-file Windows GUI Setup EXE with a real Next/Back/Finish wizard, prerequisite checks, application-path/site/app-pool choices, HTTP/HTTPS binding and explicit Local Computer certificate selection, protected First-Run Setup launch, install/upgrade/repair/uninstall/reinstall/purge lifecycle, rollback and `App_Data` preservation, sanitized install logging, versioned ZIP/EXE packaging and SHA-256 evidence. Validated implementation candidate `4e811f17d707f2e1539c5dab9b40c64b6f498cc1` passed P15 run `34436171235` with 36/36 static contract checks and complete package/lifecycle acceptance. Detailed current evidence: `docs/evidence/P15_INSTALLER_PACKAGING.md`.

The reconciliation commit supersedes `4e811f17...` as the PR merge candidate. P15 is not CLOSED until the final exact PR head is fully green, PR #75 is normally integrated from current exact main, the resulting exact-new-main governed matrix is terminal green, package/installer/hash identity is reconciled to the exact integrated source, and formal closure governance is recorded. P16/P17 remain locked.

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

Repository administration is a separate acceptance boundary: live read-back currently shows `main` unprotected and no repository rulesets. This remains `OWNER_LAST / NOT PASS` until an authorized administrator applies the required policy and independent read-back proves it.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim.
