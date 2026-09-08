# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P06 CLOSED; P07 Generic service execution engine is the next legal phase after this transition is integrated and exact-main verified
- Initial executable version: `0.1.0`
- Pinned SDK / target framework: .NET SDK `10.0.400` / `net10.0`
- Initial entity: Ministry of Justice (MOJ), Kuwait
- UI languages: Arabic (RTL) and English (LTR)
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server

## Last closed phase evidence

P06 was closed from exact integrated implementation/security `main` SHA `4c6c8460415a71c4eedb184066f6ba928faa8416` after:

- rotation/redaction/cache was normally integrated through PR #24 and its focused regression follow-up PR #27;
- canonical Secret Vault / SecretRef / AuthProfile persistence and lifecycle was normally integrated through PR #25;
- Windows/IIS persisted ASP.NET Core Data Protection keys were protected with DPAPI through PR #28;
- server-authorized bilingual AuthProfile administration and browser evidence was normally integrated through PR #29;
- the recovered independent security/evidence gate was normally integrated through PR #26 without duplicating production implementation;
- all 13 applicable exact-main checks succeeded on the exact implementation/security SHA;
- P06 Security Acceptance and Evidence run `34278983765`: SUCCESS;
- exact-main security artifact `P06-Security-Evidence-4c6c8460415a71c4eedb184066f6ba928faa8416` was produced at 5,975 bytes with digest `sha256:052190f8b090ca8981e01bef73bc5e2176d6282b5c448faa03b05fe5adb6af1a`;
- exact-main AuthProfile administration/browser artifact `P06-AuthProfile-Admin-Evidence-4c6c8460415a71c4eedb184066f6ba928faa8416` was produced at 552,487 bytes with digest `sha256:b3d4f0d62418e7c69cc03ad52599e88bc54c6c3f14910594cd8432d617d241a7`;
- opaque SecretRefs are scoped to the owning AuthProfile/Service/Environment and cannot be silently reused across boundaries;
- Shared AuthProfile use is explicit and auditable;
- secret rotation uses durable expected-current-reference/generation compare-and-swap semantics with rollback and cache-validity version advancement;
- centralized redaction/masking, write-only secret handling and no-leak evidence gates are executable;
- AuthProfile metadata/binding mutations are protected by server-side authorization, IDOR/CSRF checks and safe owner/shared-binding lifecycle rules;
- Arabic RTL / English LTR desktop and narrow browser evidence passed;
- closed P00–P05 contracts remained green;
- no owner-only or external P06 evidence was deferred.

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
- P00, P01, P02, P03, P04, P05 and P06 are closed; P07 is the next phase after this transition is integrated and exact-main verified.
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

P06 established the Secret Vault/AuthProfile boundary. Plaintext secrets never belong in metadata, Git, logs or evidence. Runtime references use opaque SecretRefs scoped to the exact AuthProfile, Service and Environment. Shared AuthProfile relationships must be explicit and auditable. Rotation must stage safely, activate atomically against the expected current reference/generation, preserve the current valid secret on failure and advance cache-validity identity on success. Administration is write-only for plaintext secret input and masked-only for display, protected server-side by authorization/IDOR/CSRF controls. Persisted ASP.NET Core Data Protection keys on Windows/IIS use DPAPI protection.

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim.
