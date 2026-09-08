# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Default branch: `main`
- Delivery model: phase-gated autonomous implementation
- Current planned product state: P03 CLOSED; P04 RBAC and service-level permissions is the next legal phase after transition exact-main verification
- Initial executable version: `0.1.0`
- Pinned SDK / target framework: .NET SDK `10.0.400` / `net10.0`
- Initial entity: Ministry of Justice (MOJ), Kuwait
- UI languages: Arabic (RTL) and English (LTR)
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server

## Last closed phase evidence

P03 was closed from exact integrated `main` SHA `20d2542427a388ac81e5249cc59f60dcbfa2ea91` after:

- the P03 security-header component was normally integrated through PR #12 and the recovered Identity/MFA/account-security implementation was normally integrated through PR #13;
- Planning Integrity run `34239792230`: SUCCESS;
- P00 Build Baseline regression run `34239792270`: SUCCESS;
- P01 Architecture and UI Shell regression run `34239792297`: SUCCESS;
- P02 First-run Setup Wizard regression run `34239792244`: SUCCESS;
- P03 Web Security Headers run `34239792235`: SUCCESS;
- P03 Identity and Account Security run `34239792740`: SUCCESS;
- Identity persistence, login/logout, password/session/account controls, forced password change, MFA enrollment/verification, privileged-account MFA policy, lockout/rate limiting, CSRF, authenticated dashboard gating, production cookie/HSTS/security-header posture and sanitized authentication audit events were exercised by the P03 gate;
- Arabic RTL / English LTR authentication browser evidence was produced at desktop and mobile viewports;
- exact-main artifact `P03-Identity-Evidence-20d2542427a388ac81e5249cc59f60dcbfa2ea91` was produced at 330,634 bytes;
- workflow artifact digest: `sha256:d12317bb99d8a3835bd0a4a34db0688af5c164e1e3865e13f76828b1bbac9245`;
- no owner-only or external evidence was deferred for P03.

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
- P00, P01, P02 and P03 are closed; P04 is the next phase after this transition is integrated and exact-main verified.
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

P03 established the authentication boundary required before detailed RBAC. Runtime access uses ASP.NET Core Identity persistence with protected login/logout, configurable password/session/remember-me and account controls, forced password change, MFA enrollment/verification, privileged-account MFA policy, lockout/rate limiting, server-side authentication challenge, CSRF protection, security headers/HSTS/cookie controls and sanitized authentication audit events. P04 authorization must preserve and build on this boundary rather than bypassing it.

## Service environment / Go-Live control

Every service owns independent UAT/Production bindings by default. Follow `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`: `Entity -> Service -> ServiceEnvironmentConfig -> AuthProfile -> SecretRef`. Endpoint, HTTP behavior, header names, timeout, TLS/proxy settings and credentials may differ per service and environment. No secret or token may cross service boundaries accidentally. Shared AuthProfile use must be explicit and auditable.

## Security control

No live secrets or real personal data are allowed in Git. All sensitive runtime values must be stored through the approved configuration/secret-vault design and redacted from logs/audit/evidence.

## UI control

`docs/ui-baseline/` contains the mandatory v1 visual baselines. High-fidelity parity is a closure gate, not optional inspiration.

## Release control

A release candidate is valid only when source, tests, evidence, installer/package, hashes and CI all refer to the same exact commit. P17 is the only phase allowed to make a final-completion claim.
