# P14 Security Hardening and Resilience Evidence

Status: **CLOSED FROM IMPLEMENTATION EVIDENCE / CLOSURE TRANSITION PENDING**

Canonical implementation unit: `P14::security-hardening-resilience-convergence`  
Canonical implementation branch: `worker/p14-security-hardening-resilience`  
Closure reconciliation branch: `worker/p14-closure-reconciliation`  
Opening base: exact `main` `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`, after P13 closure PR #72 and **32/32 exact-main push workflows SUCCESS**.  
Final P14 implementation head: `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de`.  
Integrated P14 implementation main: `1f1def164d639c75d9cc26710a905cc491355a2b`.

## Closure decision

P14 cloud-actionable implementation is terminal. PR #73 normally integrated the final P14 implementation candidate after the exact head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` completed **37/37 governed pull-request workflows SUCCESS**. The resulting exact implementation `main` SHA `1f1def164d639c75d9cc26710a905cc491355a2b` completed **34/34 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at closure review.

No known cloud-actionable Critical or High vulnerability remains in the P14 finding register. The closure review therefore permits a governance/evidence-only P14 closure transition. P15 is only **STAGED** by that transition; P15 implementation is forbidden until the closure transition is normally integrated and every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS.

## High-priority finding and repair

The P14 audit found that restricted MFA / forced-password sessions are represented by an authenticated ASP.NET Core Identity principal. Before P14, the normal shell redirected restricted sessions, but the boundary was not enforced centrally for every controller and the RBAC evaluator did not independently reject the restriction claim. A restricted principal therefore had an unsafe path to authorization evaluation outside the shell flow.

The integrated implementation repairs that boundary with defense in depth:

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

`.github/workflows/p14-security-hardening.yml` binds acceptance to the exact candidate and captures an exact NuGet dependency graph. `scripts/verify_p14_dependency_audit.py` fails the gate if `dotnet list ... --vulnerable --include-transitive --format json` reports a known vulnerable package. Deprecated-package reporting is retained for review and is not treated as proof of exploitability.

## Threat model (closed P14 scope)

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
- unsafe installer/deployment behavior (P15/P16 ownership; not claimed by P14).

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
- [x] Exact P14 implementation head completed 37/37 governed workflows SUCCESS.
- [x] NuGet vulnerable/deprecated reports were reviewed from exact-candidate evidence; no known cloud-actionable Critical/High vulnerability remains.
- [x] Full governed regression matrix was terminal green on the exact final P14 implementation head.
- [x] Exact implementation main `1f1def164d639c75d9cc26710a905cc491355a2b` completed 34/34 push workflows SUCCESS.
- [x] All P14 cloud-actionable implementation tasks are terminal.
- [ ] P14 closure-transition head must complete its governed PR matrix SUCCESS.
- [ ] The closure transition must be normally integrated.
- [ ] Every governed workflow on the resulting exact-new-main SHA must be terminal SUCCESS before P15 implementation is authorized.

## Preserved deferred boundaries

P09 authorized live UAT remains `DEFERRED_EXTERNAL_NOT_PASS`. Production MOJ operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Main branch protection/rulesets remain `OWNER_LAST / NOT PASS` until an authorized repository administrator applies and independently reads back the required policy. None of these are converted to PASS by P14 closure.

## P14 -> P15 transition rule

This artifact records terminal P14 implementation evidence and authorizes only the closure transition. P15 remains staged/implementation-locked until this closure transition is normally merged and exact-new-main CI is terminal green. After that verification, P14 is formally CLOSED and P15 may become the sole canonical implementation phase.
