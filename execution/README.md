# Execution Plan Usage

Canonical file: `GSIP_Full_Execution.json`

The file follows the worker-message sequence schema:

- `SchemaVersion: 1`
- `Loop: false`
- `DefaultDelaySeconds: 40`
- 18 enabled messages P00 through P17
- every individual `DelaySeconds: 40`
- initial `Sent: false`

## How to use it

Import the JSON into the worker/orchestrator that supports this message-sequence format. The worker must still read the live repository before every phase; the JSON is an execution sequence, not a substitute for `CURRENT_PHASE.md` or live state.

Do not manually jump to a later message because the previous phase "looks done". A phase advances only after its exit gate is evidenced and `CURRENT_PHASE.md` / `docs/TASK_LEDGER.md` are reconciled on pushed integrated work.

If the orchestration tool updates `Sent` state locally, do not commit transient sent-state changes unless the repository explicitly adopts that as execution evidence. The canonical committed plan should normally remain reusable and auditable.

The 40-second cadence is owner-requested. Do not lower it unless the owner explicitly changes the requirement.
