# Government Services Integration Portal (GSIP)

Repository for the bilingual Arabic/English Government Services Integration Portal.

## Worker start

Autonomous implementation must start from the **live repository state**, not from an old prompt. Read these files in order:

1. `WORKER_START_HERE.md`
2. `AGENTS.md`
3. `PROJECT_CONTROL.md`
4. `CURRENT_PHASE.md`
5. `docs/TASK_LEDGER.md`
6. `docs/UI_DESIGN_PARITY_GATE.md`
7. `docs/OWNER_LAST_EXECUTION_POLICY.md`
8. `docs/SECURITY_SECRETS_POLICY.md`
9. `docs/FINAL_ACCEPTANCE_CRITERIA.md`
10. `docs/plans/GSIP_Complete_Implementation_Plan_AR.md`
11. `execution/GSIP_Full_Execution.json`

The current phase is controlled only by `CURRENT_PHASE.md`. The prepared bootstrap starts at **P00**; future phases must not be implemented until the current phase is formally closed from live evidence.

## Initial entity and services

The first integrated entity is the Kuwait Ministry of Justice (MOJ), with five initial services:

- Marriage Cases Service
- Is Single Basic Service
- Marriage Couple Last Case Service
- Family Judgment Text Service
- Procuration Status Service

Official API-reference indexing is under `docs/moj-api-reference/`. Workers must re-read the live CAIT/MOJ Swagger/OpenAPI contracts during P08/P09 and must not invent request/response fields.

## Setup-first product contract

The product must provide a complete **first-run Setup Wizard before normal Login**, including language, prerequisite checks, SQL Server configuration, `Test Connection`, create/use database, migrations, initial System Administrator, organization/branding, security baseline/MFA, integration environment/authentication configuration, final health review and Finish.

The Windows/IIS deployment package must also provide a professional installer/upgrade/repair/uninstall experience.

## UI baseline

The four repository-native reference designs under `docs/ui-baseline/` are mandatory high-fidelity baselines for v1:

- `bilingual_kuwait_government_services_dashboard.svg` — Dashboard/Home
- `bilingual_kuwait_government_service_portal.svg` — Service Execution
- `bilingual_kuwait_government_permissions_dashboard.svg` — Permissions & Role Management
- `kuwait_government_audit_dashboard.svg` — Audit & Monitoring

See `docs/UI_DESIGN_PARITY_GATE.md`. Generic admin-template substitution or material simplification is not acceptable closure.

## Security

No real API key, token, password, certificate private key, database credential, or real personal test record may be committed. Use approved runtime secret storage and sanitized fixtures/evidence only.

## Autonomous execution

`execution/GSIP_Full_Execution.json` is the canonical worker sequence. It contains exactly 18 enabled phases P00–P17. Its `DefaultDelaySeconds` and every individual message delay are fixed at **40 seconds**.

The repository includes `Planning Integrity` CI to protect the execution-plan cadence and required governance/UI/reference files.

## Tracker

GitHub issue **#1 — GSIP Autonomous Implementation Tracker — P00 to P17** is the phase-level progress thread. Phase closure must also reconcile `CURRENT_PHASE.md` and `docs/TASK_LEDGER.md` on the exact integrated commit.
