# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P04 CLOSED; P05 Entity and Service metadata catalog is the next legal phase after this transition is integrated and exact-main verified
- Initial executable version: `0.1.0`
- Pinned SDK / target framework: .NET SDK `10.0.400` / `net10.0`
- Initial entity: Ministry of Justice (MOJ), Kuwait
- UI languages: Arabic (RTL) and English (LTR)
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server

## Last closed phase evidence

P04 was closed from exact integrated `main` SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9` after:

- the recovered permissions administration/UI closure implementation was normally integrated through PR #20 on top of the already integrated P04 role/permission and negative-authorization foundation;
- Planning Integrity run `34253075877`: SUCCESS;
- P00 Build Baseline regression run `34253075003`: SUCCESS;
- P01 Architecture and UI Shell regression run `34253075362`: SUCCESS;
- P02 First-run Setup Wizard regression run `34253075183`: SUCCESS;
- P03 Web Security Headers regression run `34253075119`: SUCCESS;
- P03 Identity and Account Security regression run `34253074987`: SUCCESS;
- P04 RBAC Core run `34253075074`: SUCCESS;
- P04 Permissions UI run `34253075409`: SUCCESS;
- seeded editable roles, permission catalog, RolePermissions, UserRoles, per-service permissions, Default Deny and server-side authorization were exercised;
- role creation/rename, user-role assignment/removal, forged identifier denial and protection against removing the last enabled System Administrator were exercised by executable administration/IDOR gates;
- Arabic RTL / English LTR Permissions browser evidence was produced at the required responsive viewports with the governed role/user/service matrix and pending-approvals presentation;
- exact-main artifact `P04-Permissions-UI-Evidence-c3aa76a8d456c9b951b602001bcbe1023ae26dd9` was produced at 476,233 bytes;
- workflow artifact digest: `sha256:b5b6fa42c7dd114b302a2db3572c25130f01e2c045586a90bcaf90b97d290826`;
- no owner-only or external evidence was deferred for P04.

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
- P00, P01, P02, P03 and P04 are closed; P05 is the next phase after this transition is integrated and exact-main verified.
- Future phases remain locked until the current phase is formally CLOSED.
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

P04 established the authorization boundary required before metadata administration. Runtime authorization uses editable seeded roles, a canonical permission catalog, RolePermissions, UserRoles, per-service permission controls and Default Deny with server-side enforcement. Administration must continue to protect privileged access, reject forged/unknown identifiers, prevent removal of the last enabled System Administrator and preserve the bilingual Permissions management experience. P05 metadata administration must use this authorization boundary rather than bypass it.

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim.
