# CURRENT_PHASE.md

## Canonical current phase

**P14 — Security hardening and resilience**

Status: **OPEN / ACTIVE**

P13 — Arabic/English UX and accessibility convergence is **CLOSED**. Its implementation was normally integrated by PR #71 from exact implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8` to `main` `203cc28db714fae5c2c70e85adc9cc2306bb2107`. PR #71 completed **35/35 exact-head pull-request workflows SUCCESS** before merge, including a same-SHA retry of one transient LocalDB timeout without source/test/acceptance changes, and implementation main completed **32/32 push workflows SUCCESS**.

P13 closure reconciliation was then normally integrated by PR #72 from exact transition head `7bec3bac79d9fb31f2d9b2fcf15778290f833f68`. That exact transition head completed **35/35 pull-request workflows SUCCESS**. The resulting exact `main` SHA `9f76f6593123a86c1ef999ba5ec5b7b9338a53de` completed **32/32 push workflows SUCCESS**, failure=0, queued=0, in-progress=0. The P13 phase-exit condition is therefore satisfied and P14 is the sole canonical implementation phase.

## P13 closure truth

The integrated P13 baseline provides:

- high-fidelity Home convergence to the repository-native entity-summary-card and service-card hierarchy instead of a generic administration table;
- least-privilege Home metrics using only authenticated non-secret catalogue facts, without widening Request/Audit visibility merely to mimic decorative reference values;
- exact-candidate and exact-main bilingual Arabic RTL / English LTR Home browser evidence across desktop and narrow/mobile viewports;
- reachable mobile primary navigation, skip-to-content, keyboard focus treatment and preserved LTR presentation for technical identifiers;
- preserved Permissions, Service Execution and Audit reference hierarchies through their independent exact-candidate browser gates;
- Service Execution History connected to the protected P10 `/requests` history surface;
- phase-monotonic P12/P13 acceptance so closed security/UI baselines remain executable after legal phase progression;
- preservation of all closed P00-P12 functional, authorization, secret-isolation, audit, diagnostics and evidence controls.

Exact P13 evidence includes implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, implementation exact-main P13 run `34421612012`, and final closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`. Detailed evidence is recorded in `docs/evidence/P13_UI_ACCESSIBILITY_CONVERGENCE.md`.

## Legal work now

Canonical P14 branch/lease: `worker/p14-security-hardening-resilience`, unit `P14::security-hardening-resilience-convergence`, based only on exact green P13-closed main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`.

P14 scope is security hardening and resilience: authorization-bypass/IDOR, CSRF/XSS/output encoding, rate limiting/session fixation/brute force, secure headers, secret/log leakage, migration safety, retry-storm prevention, timeout/cancellation and external-outage behavior, token-refresh concurrency, dynamic-input fuzzing, applicable upload/logo restrictions, dependency/license/vulnerability review, threat model and security checklist. P14 cannot close with a known Critical/High vulnerability.

The active P14 audit found a restricted-session authorization boundary gap: MFA/forced-password challenge principals were authenticated and shell-local redirects were not a sufficient global authorization boundary. The canonical P14 line is repairing this with central restricted-session middleware, RBAC fail-closed enforcement, authenticated challenge rate limiting, executable CSRF/XSS/resilience checks and an exact-candidate dependency-vulnerability gate. This is **OPEN-PHASE work**, not closure evidence yet.

## Locked future work

P15–P17 remain **LOCKED**. Installer/package, release-candidate automated acceptance and final release convergence must not begin before their canonical phase opens.

Legitimate already-started future-phase recovery work may repair a known real defect only under the documented owner non-stop exception; it remains **DO NOT MERGE** and does not change canonical phase authority until preceding phases close normally.

## Deferred boundaries

Historical owner/external classifications remain unchanged. P09 authorized live-UAT evidence that remains unavailable is still `DEFERRED_EXTERNAL_NOT_PASS`; unproven Production operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production→UAT fallback is allowed.

Main branch protection remains `OWNER_LAST / NOT PASS` while live read-back reports `main` unprotected and no repository ruleset configured. This repository-administration gap is not converted to PASS by P14 cloud evidence.

## P14 exit condition

P14 closes only after the full required hardening scope is implemented or explicitly evidenced as already safe; executable P14 acceptance and dependency review are terminal green on one exact candidate; no known Critical/High vulnerability remains; all governed closed-baseline workflows are green on the same candidate; governance/evidence is reconciled; normal integration completes; and exact-new-main CI is terminal green. Only then may P15 open.
