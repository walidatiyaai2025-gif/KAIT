# P13 UI / Accessibility Convergence Evidence

**State: OPEN-PHASE CANDIDATE — NOT P13 CLOSURE**

This document records the cloud-actionable P13 implementation candidate only. It MUST NOT be interpreted as P13 closure, exact-main acceptance, owner acceptance, Production acceptance, or `VERIFIED_FINAL_COMPLETE`.

## Authority

- Canonical base before P13 implementation: `42136cf59202c90f20f204e520c644acc69a88ca`.
- P12 is closed from actual merged/exact-main evidence.
- P13 is the only implementation phase currently `OPEN / READY`.
- P14-P17 remain locked.
- Canonical UI parity contract: `docs/UI_DESIGN_PARITY_GATE.md`.
- Repository-native reference surfaces:
  - `docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg`
  - `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg`
  - `docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg`
  - `docs/ui-baseline/kuwait_government_audit_dashboard.svg`

## Candidate scope

P13 converges the stale P01 Home preview into a catalogue-backed bilingual government-services dashboard, keeps the established shell and feature implementations, restores all primary navigation routes on narrow screens, adds shared keyboard focus treatment, preserves RTL/LTR and explicit LTR rendering for technical identifiers, and retains exact-candidate browser artifacts for all four reference screens.

Home reads only the canonical non-secret metadata catalogue. It does not import Request or Audit totals into the dashboard because those surfaces have narrower authorization boundaries. The least-privilege deviation from the decorative reference KPI labels is documented in `docs/DESIGN_DECISIONS.md`; no fake or production-looking data is introduced.

## Independent exact-candidate evidence model

The candidate intentionally reuses independent pre-existing acceptance workers rather than copying them:

- Home / Dashboard: `scripts/verify-p13-home-ui.ps1` via `P13 UI Accessibility Convergence`.
- Service Execution: `scripts/verify-p07-execution-ui.ps1` via `P07 Service Execution UI`.
- Permissions: `scripts/verify-p04-permissions-ui.ps1` via `P04 Permissions UI`; P13 strengthens this workflow to checkout the exact PR head and retain its screenshot artifact on pull requests.
- Audit / Monitoring: `scripts/verify-p11-audit-runtime-ui.ps1` via `P11 Audit Tamper Evidence and Monitoring`.
- Cross-screen semantic/static contract: `scripts/verify_p13_ui_accessibility.py`.

All four visual lines must execute on the same final P13 candidate SHA. P13 closure is forbidden if any governed workflow on that final SHA is failed, queued, in progress, missing when required, or superseded by a newer candidate.

## Security and privacy

- Home exposes only active non-secret catalogue metadata already available to the authenticated application shell.
- Service execution authorization remains enforced independently by the canonical execution screen/engine; a Home link does not grant execution rights.
- No password, API key, bearer token, secret value, real Civil ID, or personal MOJ payload is intentionally placed in P13 browser evidence.
- Runtime P13 evidence uses synthetic local data only and redacts antiforgery token values before saved HTML is retained.
- Historical P09 authorized live-UAT evidence remains `DEFERRED_EXTERNAL_NOT_PASS`.
- Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`.
- Main-branch protection remains `OWNER_LAST / NOT PASS` until an authorized administrator configures it and read-back evidence proves the exact policy.

## Closure gate

This file is not closure evidence. P13 may become CLOSED only after all of the following are true on the final candidate and integrated main:

1. the exact final P13 pull-request head is fixed and identified;
2. all governed exact-head PR workflows are terminal SUCCESS with no hidden/superseded failure treated as PASS;
3. Home, Service Execution, Permissions and Audit bilingual browser artifacts correspond to that exact candidate SHA;
4. semantic/accessibility/parity contract is PASS without weakening prior P00-P12 acceptance;
5. the PR is normally integrated under repository policy with an expected-head guard;
6. the resulting exact-new-main SHA is read back independently;
7. all governed exact-new-main push workflows are terminal SUCCESS;
8. TASK_LEDGER / CURRENT_PHASE / PROJECT_CONTROL and final P13 evidence are reconciled to that actual integrated SHA;
9. P14 remains unopened until the legal P13 closure transition itself is integrated and its exact-main gate is green.

No owner-only or external dependency is converted to PASS by this cloud evidence.
