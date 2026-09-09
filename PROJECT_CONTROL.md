# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P07 CLOSED from exact integrated evidence; P08 MOJ authentication integration is the canonical current phase and is OPEN / READY
- Initial executable version: `0.1.0`
- Pinned SDK / target framework: .NET SDK `10.0.400` / `net10.0`
- Initial entity: Ministry of Justice (MOJ), Kuwait
- UI languages: Arabic (RTL) and English (LTR)
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server

## Last closed phase evidence

P07 is CLOSED from exact integrated implementation baseline `9535fa158441160ab7c7d204863776e38e560a33` after normal integration of PRs #33, #34, #36, #37 and #38, with PR #39 providing intermediate progress reconciliation. The exact-main baseline passed **16/16** push workflow runs with no failure, queued or in-progress run after completion.

Closure evidence on that exact SHA includes:

- P07 Generic Execution Runtime run `34305403834`: SUCCESS; artifact `P07-Generic-Execution-Runtime-9535fa158441160ab7c7d204863776e38e560a33`, 809 bytes, digest `sha256:322395583f95a1f8fd1cacab0975ca0f4b3c0c971de1a86fa44185ed3f57a8dc`;
- P07 Service Execution UI run `34305403861`: SUCCESS; artifact `P07-Execution-UI-Evidence-9535fa158441160ab7c7d204863776e38e560a33`, 372,853 bytes, digest `sha256:5d9b9fd470d6f953fb120391c42130a01412a9b4e8a6288399170cf8399b8f78`;
- P07 Upgrade Persistence Acceptance run `34305403851`: SUCCESS; artifact `p07-upgrade-persistence-evidence-9535fa158441160ab7c7d204863776e38e560a33`, 647 bytes, digest `sha256:5ec9fd3f01925d62cf47d375be1bf591b27dd2eebf9c3848ea448e01c00482ef`.

Acceptance covers exact authorized Service + Environment + AuthProfile resolution, no cross-service/environment fallback, metadata-generated execution forms, client/server validation, RequestId/CorrelationId, `IHttpClientFactory` execution, bounded timeout/retry with POST retry only when explicitly `SafeToRetry`, ResultMappings, bounded response reads, required HTTP/TLS/network error classification, secret-safe status/duration/endpoint-alias telemetry, sensitive raw-response masking, bilingual responsive Service Execution UI, accessibility/visual evidence, fake endpoints, persistence/upgrade safety and preservation of P00–P06 security/isolation contracts. No owner-only or external P07 evidence is deferred.

The P07 final closure reconciliation is governance/evidence-only and introduces no P08 implementation.

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

No old prompt, screenshot caption, branch description, or stale ledger overrides live evidence.

## Phase policy

- Exactly one canonical current phase exists at a time.
- P00, P01, P02, P03, P04, P05, P06 and P07 are CLOSED.
- P08 is the canonical current legal implementation phase and is OPEN / READY.
- P09–P17 remain locked until P08 is formally CLOSED.
- Phase exit requires implementation + tests + evidence + documentation reconciliation + pushed commit + required CI + exact-main recheck.
- Integration recovery and exact-main regressions take priority over new feature work.
- A phase-transition branch does not authorize new-phase implementation until that transition is integrated and the resulting exact-main gate is green.

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

P07 established the metadata-driven execution boundary. Execution must resolve the exact authorized Service + Environment + AuthProfile binding, never fall back across service/environment boundaries, validate metadata-driven fields on both client and server, propagate RequestId/CorrelationId, use `IHttpClientFactory` with bounded timeout/resilience, retry POST only when explicitly marked `SafeToRetry`, map structured results through canonical ResultMappings, bound response reads, classify required HTTP/TLS/network failures, and keep telemetry/diagnostics secret-free. The Service Execution UI is bilingual Arabic RTL / English LTR, responsive, accessible and high-fidelity, with Result / Raw Response / History presentation and sensitive-response masking. P08+ service-specific authentication semantics must layer onto this generic boundary rather than bypass it.

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim.
