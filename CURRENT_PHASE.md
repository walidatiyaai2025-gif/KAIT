# CURRENT_PHASE.md

## Canonical current phase

**P03 — Identity, MFA, sessions and account security**

Status: **OPEN / READY**

P02 is CLOSED from integrated exact-main evidence. The final P02 implementation gap was normally merged by PR #9 at `efa56808db13fa45f803131f7bcf2d65c485a66d`. On that exact SHA, Planning Integrity run `34228695674`, P00 Build Baseline run `34228695950`, P01 Architecture and UI Shell run `34228695746`, and P02 First-run Setup Wizard run `34228695995` all succeeded. The P02 run performed Release build, preserved P00/P01 contracts, exercised SQL/setup/security checks including wrong SQL credentials and migration failure/retry, executed bilingual browser evidence, verified the Review/Health Check critical-failure gate, and uploaded exact-main evidence.

The exact-main P02 artifact is `P02-Setup-Evidence-efa56808db13fa45f803131f7bcf2d65c485a66d` (390,868 bytes) with workflow digest `sha256:f03ad21356d2ba92308b2cff015d934d0ef23b9e78b02a4f0eb26c9cd06f8961`.

This P03 transition becomes authoritative only after this closure change is normally integrated to `main` and the resulting exact-main Planning/P00/P01/P02 gates remain green. Do not begin P03 implementation from an unmerged transition branch.

## Legal work now

After this transition is integrated and exact-main verification is green, P03 only, plus any repair needed to preserve closed P00/P01/P02 baselines and repository controls.

P03 must implement identity and account security before detailed RBAC:

- ASP.NET Core Identity or a mature equivalent with login/logout and secure account lifecycle;
- configurable password policy, lockout/rate limiting, session timeout and remember-me policy;
- account enable/disable, last-login tracking and forced password change;
- MFA enrollment and verification, with MFA required for privileged accounts according to configuration;
- production HTTPS/HSTS settings and Secure/HttpOnly/SameSite cookies;
- CSRF protection, CSP/security headers and server-side controls against authentication bypass;
- audit events for login success/failure, logout, MFA and lockout without storing passwords, tokens or secrets;
- positive and negative tests, including lockout/rate-limit and bypass paths;
- build/test/repair/retest, normal PR/merge, evidence and exact-main verification before closure.

Detailed RBAC/service-level permission implementation remains P04 work and must not begin before P03 is CLOSED.

## Locked future work

P04–P17 remain locked. Do not implement RBAC, metadata administration, Secret Vault/auth profiles, generic execution, MOJ authentication/services, history/audit, operational administration, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding. Every future Service + Environment binding and AuthProfile remains isolated by default and no secret or token may cross service boundaries automatically.

## P02 closure evidence

- Final integrated implementation SHA: `efa56808db13fa45f803131f7bcf2d65c485a66d`
- Final implementation PR: #9 — normally merged
- P02 implementation lineage: PRs #6, #7, #8 and #9
- Planning Integrity: run `34228695674` — SUCCESS
- P00 Build Baseline regression: run `34228695950` — SUCCESS
- P01 Architecture and UI Shell regression: run `34228695746` — SUCCESS
- P02 First-run Setup Wizard: run `34228695995` — SUCCESS
- Exact-main artifact: `P02-Setup-Evidence-efa56808db13fa45f803131f7bcf2d65c485a66d`
- Artifact size: 390,868 bytes
- Artifact workflow digest: `sha256:f03ad21356d2ba92308b2cff015d934d0ef23b9e78b02a4f0eb26c9cd06f8961`
- Browser evidence: bilingual first-run Setup flow with protected pre-Login gate and Review/Health Check acceptance
- Security evidence: protected setup state, no live credentials, password redaction/hashing, SQL negative paths, post-Finish setup lock
- Owner/external dependency: none deferred for P02

Detailed evidence is recorded in `docs/evidence/P02_SETUP_WIZARD.md`.

## P03 exit condition

P03 may be marked CLOSED only when identity/login/logout, password/session/account controls, privileged-account MFA, lockout/rate limiting, production cookie/transport/security-header controls, authentication audit events, negative/bypass tests, CI/evidence and exact-main verification are complete and pushed without weakening security or exposing secrets.
