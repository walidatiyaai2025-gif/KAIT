# Repository Bootstrap Manifest

This repository was prepared as the execution source of truth for the GSIP build.

## Governance and worker control

- `AGENTS.md`
- `WORKER_START_HERE.md`
- `PROJECT_CONTROL.md`
- `CURRENT_PHASE.md`
- `docs/TASK_LEDGER.md`
- `docs/OWNER_LAST_EXECUTION_POLICY.md`
- `docs/SECURITY_SECRETS_POLICY.md`
- `docs/FINAL_ACCEPTANCE_CRITERIA.md`

## Plans

- `docs/plans/GSIP_Complete_Implementation_Plan_AR.md`
- `execution/GSIP_Full_Execution.json`
- `execution/README.md`

## Mandatory UI baselines

- `docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg`
- `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg`
- `docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg`
- `docs/ui-baseline/kuwait_government_audit_dashboard.svg`
- `docs/UI_DESIGN_PARITY_GATE.md`

The vector baselines are repository-native versions of the approved visual concepts and are intentionally easy for workers, browsers and version control to inspect.

## MOJ references

- `docs/moj-api-reference/INDEX.md`

It records the five owner-selected official CAIT service pages and the confirmed high-level auth flow visible in the supplied official Swagger material. P08/P09 must fetch and capture the live exact OpenAPI contracts; unknown fields are never guessed.

## Automation integrity

- `.github/workflows/planning-integrity.yml`
- `scripts/validate_execution_plan.py`

The validator enforces the 18-message P00–P17 sequence, `DefaultDelaySeconds = 40`, each message `DelaySeconds = 40`, and presence of the required governance/UI/reference files.

## Security bootstrap

- `.gitignore` excludes common local secrets, certificates/private keys, local DB/runtime state and generated installer output.
- Real CAIT/MOJ credentials previously used for exploratory testing are intentionally not stored in this repository.

## Tracker

GitHub issue **#1 — GSIP Autonomous Implementation Tracker — P00 to P17** is the live phase-level progress thread. Workers should append exact-SHA closure evidence there while keeping `CURRENT_PHASE.md` and `docs/TASK_LEDGER.md` canonical.
