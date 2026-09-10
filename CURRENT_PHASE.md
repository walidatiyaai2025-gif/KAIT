# CURRENT_PHASE.md

## Canonical current phase

**P14 — Security hardening and resilience**

Status: **OPEN / READY only after this P13 closure transition is normally integrated and the resulting exact-new-main CI is terminal green**

P13 — Arabic/English UX and accessibility convergence is **CLOSED from implementation evidence** on exact integrated `main` SHA `203cc28db714fae5c2c70e85adc9cc2306bb2107` after normal integration of PR #71 from exact implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8`.

PR #71 completed **35/35 exact-head pull-request workflows SUCCESS** before merge. Its P08 Authentication Convergence run had one transient LocalDB create timeout on the same exact head; the failed job was rerun without source, test, or acceptance changes and the rerun completed SUCCESS. The resulting exact integrated main `203cc28db714fae5c2c70e85adc9cc2306bb2107` completed **32/32 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0.

## P13 closure truth

The integrated P13 baseline provides:

- high-fidelity Home convergence to the repository-native entity-summary-card and service-card hierarchy instead of a generic administration table;
- least-privilege Home metrics using only authenticated non-secret catalogue facts, without widening Request/Audit visibility merely to mimic decorative reference values;
- exact-candidate and exact-main bilingual Arabic RTL / English LTR Home browser evidence across desktop and narrow/mobile viewports;
- reachable mobile primary navigation, skip-to-content, keyboard focus treatment and preserved LTR presentation for technical identifiers;
- preserved Permissions, Service Execution and Audit reference hierarchies through their independent exact-candidate browser gates;
- Service Execution History connected to the protected P10 `/requests` history surface rather than leaving the stale P07 deferred control in place;
- phase-monotonic P12/P13 acceptance so closed security/UI baselines remain executable after legal phase progression;
- preservation of all closed P00-P12 functional, authorization, secret-isolation, audit, diagnostics and evidence controls.

Exact integrated evidence:

- PR #71 implementation head: `1576764a16ea6ddfed735cb0824cb38de26c83d8` — **35/35 governed PR workflows SUCCESS** after same-SHA transient LocalDB retry;
- implementation merge main: `203cc28db714fae5c2c70e85adc9cc2306bb2107` — **32/32 push workflows SUCCESS**;
- exact-main P13 workflow: `34421612012` — SUCCESS;
- exact-main Home artifact: `P13-Home-UI-203cc28db714fae5c2c70e85adc9cc2306bb2107`, artifact `10131131126`, digest `sha256:19b8a382a59cc049374578d1fc8f865439bc16a523268f74ef98175aeb68fe2e`;
- exact-main static artifact: `P13-UI-Accessibility-Static-203cc28db714fae5c2c70e85adc9cc2306bb2107`, artifact `10131148598`, digest `sha256:062421c5545952d26d42b1a91b8865ffbd4626ef7f3f9d8041504362a3d4e329`.

Detailed P13 closure evidence is recorded in `docs/evidence/P13_UI_ACCESSIBILITY_CONVERGENCE.md`.

## Legal work now

This reconciliation branch is **governance/evidence-only**. It does **not** authorize P14 implementation from its own head.

After this P13 closure transition is normally merged and every governed workflow on the resulting exact new `main` SHA is terminal SUCCESS, P14 becomes the only canonical implementation phase, plus any repair required to preserve closed P00-P13 baselines and repository controls.

Canonical P14 scope is security hardening and resilience: authorization-bypass/IDOR, CSRF/XSS/output encoding, rate limiting/session fixation/brute force, secure headers, secret/log leakage, migration safety, retry-storm prevention, timeout/cancellation and external-outage behavior, token-refresh concurrency, dynamic-input fuzzing, applicable upload/logo restrictions, dependency/license/vulnerability review, threat model and security checklist. P14 cannot close with a known Critical/High vulnerability.

## Locked future work

P15–P17 remain **LOCKED**. Installer/package, full release-candidate automated acceptance and final release convergence must not begin before their canonical phase opens.

Legitimate already-started future-phase recovery work may repair a known real defect only under the documented owner non-stop exception; it remains **DO NOT MERGE** and does not change canonical phase authority until preceding phases close normally.

## Deferred boundaries

Historical owner/external classifications remain unchanged. The P09 authorized live-UAT evidence that remains unavailable is still `DEFERRED_EXTERNAL_NOT_PASS`; unproven Production operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production→UAT fallback is allowed.

Main branch protection remains `OWNER_LAST / NOT PASS` while `main` is unprotected and no repository ruleset is configured. This repository-administration gap is not converted to PASS by P13 cloud evidence.

## P14 opening condition

P14 implementation may begin only after this P13 closure reconciliation is normally integrated and the resulting exact-new-main regression matrix is terminal green. Until then, P14 is staged by this transition but not implementation-authorized.
