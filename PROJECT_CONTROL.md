# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P10 CLOSED from exact integrated evidence; P11 tamper-evident audit/monitoring is the canonical current phase and is OPEN / READY
- Initial executable version: `0.1.0`
- Pinned SDK / target framework: .NET SDK `10.0.400` / `net10.0`
- Initial entity: Ministry of Justice (MOJ), Kuwait
- UI languages: Arabic (RTL) and English (LTR)
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server

## Last closed phase evidence

P10 is CLOSED from exact integrated `main` SHA `f4b0142175207af1f8cb3c32cfb935e7a66ff856` after normal integration of PR #62. The exact integrated baseline completed **32/32** push workflow runs with failure=0, queued=0, in-progress=0 and cancelled=0 after completion.

P10 closure includes canonical request/result lifecycle persistence, masked/protected bounded result storage, Own/Department/All authorization plus current service visibility, IDOR rejection, filters/search, retention/migration/concurrency acceptance, permissioned CSV/XLSX/PDF/Print exports with injection defense and export audit events, and bilingual reachable History UI. Detailed evidence: `docs/evidence/P10_CLOSURE.md`.

Historical P08/P09 owner/external classifications remain unchanged. Evidence proven for UAT is not promoted to Production. `DEFERRED_EXTERNAL` remains NOT PASS and Production→UAT fallback is forbidden.

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
- P00 through P10 are CLOSED.
- P11 is the canonical current legal implementation phase and is OPEN / READY.
- P12–P17 remain locked until P11 is formally CLOSED.
- Phase exit requires implementation + tests + evidence + documentation reconciliation + pushed commit + required CI + exact-main recheck.
- Integration recovery and exact-main regressions take priority over new feature work.
- A phase-transition branch does not authorize new-phase implementation until that transition is integrated and the resulting exact-main gate is green.
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

P10 established canonical request/result history with bounded protected persistence, scoped Own/Department/All access plus current service visibility, filtering, retention/migration/concurrency safety and permissioned export/print behavior. Export operations are auditable and sensitive data remains governed by the existing masking/protection policies.

## P11 audit/monitoring contract boundary

P11 is the current phase. It must converge the canonical tamper-evident audit trail and monitoring boundary: append-only database enforcement, chained integrity evidence, mutation/tail-deletion detection, safe concurrent append, verifiable retention checkpointing, sanitized monitoring, protected high-fidelity Audit administration, bilingual UI and executable security/LocalDB/UI evidence. P11 must preserve all closed P00-P10 controls.

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim.
