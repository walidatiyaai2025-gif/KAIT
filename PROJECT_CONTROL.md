# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P06 CLOSED from corrected final evidence; P07 Generic service execution engine is the canonical current phase and remains OPEN / READY
- Initial executable version: `0.1.0`
- Pinned SDK / target framework: .NET SDK `10.0.400` / `net10.0`
- Initial entity: Ministry of Justice (MOJ), Kuwait
- UI languages: Arabic (RTL) and English (LTR)
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server

## Last closed phase evidence

P06 is closed from corrected exact integrated implementation/security `main` SHA `fef5882abf8a6f12990c3e7c0e9f849d08cd7947` after:

- rotation/redaction/cache was normally integrated through PR #24 and its focused regression follow-up PR #27;
- canonical Secret Vault / SecretRef / AuthProfile persistence and lifecycle was normally integrated through PR #25;
- Windows/IIS persisted ASP.NET Core Data Protection keys were protected with DPAPI through PR #28;
- server-authorized bilingual AuthProfile administration and browser evidence was normally integrated through PR #29;
- the recovered independent security/evidence gate was normally integrated through PR #26 without duplicating production implementation;
- the earlier closure reconciliation was normally integrated through PR #30;
- a later live audit of the higher-authority execution plan identified one unintegrated canonical P06 requirement: a runtime token cache with expiry safety window and single-flight refresh;
- the already-existing legitimate `P06::token-cache-runtime` branch was recovered, reconciled to current main and normally integrated through PR #31 instead of creating duplicate work;
- runtime token caching now uses the existing exact Service + Environment + AuthProfile + version/generation `TokenCacheIdentity`, a configurable expiry safety window, single-flight refresh, caller-cancellation isolation, fail-safe non-caching of failed/canceled/near-expiry refresh results, and secret-safe token serialization/diagnostics;
- all 13 exact-main workflow runs succeeded on the corrected implementation/security SHA;
- P06 Token Cache Runtime run `34281504875`: SUCCESS;
- P06 Security Acceptance and Evidence run `34281505005`: SUCCESS;
- exact-main security artifact `P06-Security-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947` was produced at 5,975 bytes with digest `sha256:2a3ccafdd523e513caca803514b1dde6b3da349460d5715bca17e6841fe5a573`;
- P06 AuthProfile Administration run `34281505040`: SUCCESS;
- exact-main AuthProfile administration/browser artifact `P06-AuthProfile-Admin-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947` was produced at 557,361 bytes with digest `sha256:aa2adb57ffb3cd7ecece6c696ed4df3bec0bb3c41ad99180b0fb530ac10bce91`;
- opaque SecretRefs are scoped to the owning AuthProfile/Service/Environment and cannot be silently reused across boundaries;
- Shared AuthProfile use is explicit and auditable;
- secret rotation uses durable expected-current-reference/generation compare-and-swap semantics with rollback and cache-validity version advancement;
- centralized redaction/masking, write-only secret handling and no-leak evidence gates are executable;
- AuthProfile metadata/binding mutations are protected by server-side authorization, IDOR/CSRF checks and safe owner/shared-binding lifecycle rules;
- Arabic RTL / English LTR desktop and narrow browser evidence passed;
- closed P00–P05 contracts remained green;
- PR #31 introduced no P07 execution-engine implementation and no P08+ scope;
- no owner-only or external P06 evidence was deferred.

The P06 evidence baseline recorded by PR #30 is superseded by this corrected closure record because it predated integration of the canonical runtime token-cache requirement. The final corrected closure reconciliation was normally integrated through PR #32 at exact `main` SHA `90b3068ea39a342392222ae581e568b94f7f9004`; all 15 applicable exact-main push workflows on that SHA completed successfully. P07 is therefore the sole legal current implementation phase. This status does not mark any open P07 implementation, evidence, or closure item complete.

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
- P00, P01, P02, P03, P04, P05 and P06 are closed; P07 is the canonical current legal implementation phase and remains OPEN / READY.
- P08–P17 remain locked until the current phase is formally CLOSED.
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

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim.
