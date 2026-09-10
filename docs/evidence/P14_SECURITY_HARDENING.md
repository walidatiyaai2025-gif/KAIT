# P14 Security Hardening and Resilience Evidence

Status: **OPEN-PHASE CANDIDATE — NOT P14 CLOSURE**

Canonical unit: `P14::security-hardening-resilience-convergence`  
Canonical branch: `worker/p14-security-hardening-resilience`  
Opening base: exact `main` `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`, after P13 closure PR #72 and **32/32 exact-main push workflows SUCCESS**.

## High-priority finding and repair

The P14 audit found that restricted MFA / forced-password sessions are represented by an authenticated ASP.NET Core Identity principal. Before P14, the normal shell redirected restricted sessions, but the boundary was not enforced centrally for every controller and the RBAC evaluator did not independently reject the restriction claim. A restricted principal therefore had an unsafe path to authorization evaluation outside the shell flow.

The candidate repairs that boundary with defense in depth:

- `RestrictedSessionBoundaryMiddleware` runs after authentication and before authorization;
- a restricted principal can reach only its exact `/mfa/enroll`, `/mfa/verify`, or `/password/change` challenge plus `/logout`;
- cross-boundary state-changing requests fail closed with HTTP 403; safe GET/HEAD requests are redirected only to the exact required challenge;
- unknown restriction claims fail closed;
- both global and service-scoped RBAC evaluation reject any restricted principal independently of the web middleware;
- MFA enrollment, MFA verification, and forced-password POSTs are globally rate-limited by authenticated user identity, with IP fallback and HTTP 429 rejection;
- the existing anonymous login IP limiter and account lockout remain intact.

## Executable acceptance

`tests/GSIP.P14SecurityChecks` covers:

1. canonical restriction claims and unknown-claim rejection;
2. normal-route bypass denial for restricted principals;
3. cross-boundary POST denial and exact-challenge reachability;
4. middleware ordering: authentication -> rate limiting -> restricted-session boundary -> authorization;
5. independent RBAC fail-closed checks;
6. antiforgery enforcement on every MVC POST/PUT/PATCH/DELETE action by reflection;
7. Razor output-encoding bypass scan (`Html.Raw` forbidden in application views);
8. bounded execution retries, timeout/cancellation distinction, and bounded response handling;
9. token-cache single-flight refresh and caller-cancellation isolation;
10. CSP/script/framing hardening preservation.

`.github/workflows/p14-security-hardening.yml` runs the exact-candidate acceptance and also captures an exact NuGet dependency graph. `scripts/verify_p14_dependency_audit.py` fails the gate if `dotnet list ... --vulnerable --include-transitive --format json` reports any known vulnerable package. A deprecated-package report is retained for review; it is evidence, not an automatic claim that all deprecated packages are exploitable.

## Threat model (current P14 scope)

### Protected assets

- authenticated administrator/user sessions and MFA state;
- service-level authorization boundaries;
- UAT/Production service/environment isolation;
- AuthProfile and SecretRef identities and protected secret material;
- MOJ tokens and token-cache identities;
- request/audit history and tamper-evidence chain;
- database schema/data and Data Protection keys;
- outbound service execution and diagnostic network boundaries.

### Principal threats

- authentication-stage privilege escalation through a restricted MFA/password session;
- IDOR/forged service, environment, AuthProfile or SecretRef identifiers;
- CSRF on state-changing MVC endpoints;
- reflected/stored XSS or deliberate Razor output-encoding bypass;
- brute force against login/MFA/current-password challenges;
- session fixation or stale session authorization;
- secret/token leakage into logs, errors, evidence, headers or persisted history;
- retry storms, unbounded Retry-After, unbounded response bodies, cancellation loss or external outage amplification;
- cross-service/environment token reuse and concurrent refresh races;
- dangerous dynamic input/regex behavior;
- vulnerable/deprecated dependencies and unsafe package drift;
- unsafe installer/deployment behavior (future P15/P16 ownership; not claimed by P14).

### Trust boundaries

Browser -> ASP.NET Core pipeline -> authorization/RBAC -> application services -> SQL Server / Data Protection / Secret Vault -> outbound MOJ endpoints. Restricted authentication sessions are a separate trust state and are not normal authorized sessions.

## Security checklist

- [x] Restricted-session authorization bypass is centrally blocked and RBAC is fail closed.
- [x] Authentication challenge brute-force throttling is added; login lockout/IP limiter preserved.
- [x] Mutating MVC antiforgery is executable acceptance, not a manual checklist assertion.
- [x] Razor output-encoding bypass is scanned in executable acceptance.
- [x] Existing secure headers/CSP baseline is preserved and checked.
- [x] Existing execution retries are capped at three and Retry-After is capped at two seconds; cancellation/timeout and response-size controls are checked.
- [x] Existing token cache single-flight/caller-cancellation safety is checked.
- [ ] Exact-candidate P14 workflow must become terminal green.
- [ ] NuGet vulnerable/deprecated reports must be reviewed from exact-candidate artifacts; known Critical/High vulnerabilities must be zero before closure.
- [ ] Full governed regression matrix must be terminal green on one exact final P14 head.
- [ ] Exact-new-main regression matrix must be terminal green after normal integration.
- [ ] Final P14 governance/ledger reconciliation must be integrated before P15 may open.

## Preserved deferred boundaries

P09 authorized live UAT remains `DEFERRED_EXTERNAL_NOT_PASS`. Production MOJ operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Main branch protection/rulesets remain `OWNER_LAST / NOT PASS` until an authorized repository administrator applies and independently reads back the required policy. None of these are converted to PASS by this P14 candidate.

P15–P17 remain locked.
