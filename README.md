# Government Services Integration Portal (GSIP)

Repository for the bilingual Arabic/English Government Services Integration Portal.

## Start here

Autonomous implementation must start from the live repository state and follow these files in order:

1. `AGENTS.md`
2. `PROJECT_CONTROL.md`
3. `CURRENT_PHASE.md`
4. `docs/TASK_LEDGER.md`
5. `docs/UI_DESIGN_PARITY_GATE.md`
6. `docs/OWNER_LAST_EXECUTION_POLICY.md`
7. `execution/GSIP_Full_Execution.json`
8. `docs/plans/GSIP_Complete_Implementation_Plan_AR.docx`

The first integrated entity is the Kuwait Ministry of Justice (MOJ), with five initial services:

- Marriage Cases Service
- Is Single Basic Service
- Marriage Couple Last Case Service
- Family Judgment Text Service
- Procuration Status Service

The application must provide a complete first-run Setup Wizard before normal login, including SQL Server configuration and connection testing, initial administrator creation, security baseline, integration environment configuration, and final health review.

## UI baseline

The four reference designs under `docs/ui-baseline/` are mandatory high-fidelity baselines for v1:

- Dashboard
- Service Execution
- Permissions & Role Management
- Audit & Monitoring

See `docs/UI_DESIGN_PARITY_GATE.md` for the closure gate.

## Security

No real API key, token, password, certificate private key, or database credential may be committed. Use secret storage and local/environment configuration only. The repository intentionally contains documentation and sanitized references, not live credentials.

## Autonomous execution

`execution/GSIP_Full_Execution.json` is the canonical worker sequence. Its default delay and every message delay are fixed at **40 seconds**. Phases are sequential P00 through P17; future-phase work must not be claimed before the current phase is closed from live evidence.
