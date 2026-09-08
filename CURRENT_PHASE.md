# CURRENT_PHASE.md

## Canonical current phase

**P04 — RBAC and service-level permissions**

Status: **OPEN / READY**

P03 is CLOSED from integrated exact-main evidence. The final P03 Identity/MFA/account-security implementation was normally merged by PR #13 at `20d2542427a388ac81e5249cc59f60dcbfa2ea91`, building on the previously integrated P03 security-header component from PR #12. On that exact implementation SHA, Planning Integrity run `34239792230`, P00 Build Baseline run `34239792270`, P01 Architecture and UI Shell run `34239792297`, P02 First-run Setup Wizard run `34239792244`, P03 Web Security Headers run `34239792235`, and P03 Identity and Account Security run `34239792740` all succeeded.

The exact-main P03 artifact is `P03-Identity-Evidence-20d2542427a388ac81e5249cc59f60dcbfa2ea91` (330,634 bytes) with workflow digest `sha256:d12317bb99d8a3835bd0a4a34db0688af5c164e1e3865e13f76828b1bbac9245`.

This P04 transition becomes authoritative only after this closure reconciliation is normally integrated to `main` and the resulting exact-main closed-phase regressions remain green. Do not begin P04 implementation from an unmerged transition branch.

## Legal work now

After this transition is integrated and exact-main verification is green, P04 only, plus any repair needed to preserve closed P00/P01/P02/P03 baselines and repository controls.

P04 must implement detailed RBAC and service-level permissions before metadata/service implementation:

- seed editable roles: System Administrator, Integration Manager, Service Operator, Auditor and Read Only;
- permission catalog covering entity/service visibility and management, service execution/secrets, users/roles, request scopes/exports, audit access, settings, diagnostics and backup/restore according to the canonical execution plan;
- RolePermissions, UserRoles and a service permission matrix capable of allowing/denying individual services per role;
- Default Deny for new permissions;
- server-side authorization on controllers/handlers/APIs, not UI-only controls;
- high-fidelity Permissions & Role Management UI against `docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg`, including Arabic RTL and English LTR evidence;
- negative authorization and IDOR tests;
- build/test/repair/retest, normal PR/merge, evidence and exact-main verification before closure.

## Locked future work

P05–P17 remain locked. Do not implement metadata administration, Secret Vault/auth profiles, generic execution, MOJ authentication/services, history/audit, operational administration, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding. Every future Service + Environment binding and AuthProfile remains isolated by default and no secret or token may cross service boundaries automatically.

## P03 closure evidence

- Final integrated implementation SHA: `20d2542427a388ac81e5249cc59f60dcbfa2ea91`
- Final implementation PR: #13 — normally merged
- Supporting web-security PR: #12 — normally merged
- Planning Integrity: run `34239792230` — SUCCESS
- P00 Build Baseline regression: run `34239792270` — SUCCESS
- P01 Architecture and UI Shell regression: run `34239792297` — SUCCESS
- P02 First-run Setup Wizard regression: run `34239792244` — SUCCESS
- P03 Web Security Headers: run `34239792235` — SUCCESS
- P03 Identity and Account Security: run `34239792740` — SUCCESS
- Exact-main artifact: `P03-Identity-Evidence-20d2542427a388ac81e5249cc59f60dcbfa2ea91`
- Artifact size: 330,634 bytes
- Artifact workflow digest: `sha256:d12317bb99d8a3835bd0a4a34db0688af5c164e1e3865e13f76828b1bbac9245`
- Identity evidence: login/logout, Identity persistence, account controls, forced password change, MFA enrollment/verification, privileged-account MFA policy, lockout/rate limiting and session/remember-me controls
- Security evidence: authentication challenge, CSRF rejection, CSP/security headers, production HSTS/cookie posture and sanitized authentication audit events
- Browser evidence: bilingual Arabic RTL / English LTR authentication screens at desktop and mobile viewports
- Regression evidence: P00, P01 and P02 contracts remain green on the exact P03 implementation SHA
- Owner/external dependency: none deferred for P03

Detailed evidence is recorded in `docs/evidence/P03_IDENTITY_ACCOUNT_SECURITY.md`.

## P04 exit condition

P04 may be marked CLOSED only when the role/permission model, Default Deny behavior, per-service permission matrix, server-side authorization, required permissions UI parity, negative authorization/IDOR tests, CI/evidence and exact-main verification are complete and pushed without weakening the closed P03 identity/security boundary or introducing future-phase scope.
