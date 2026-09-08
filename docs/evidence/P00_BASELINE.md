# P00 Baseline Evidence

Status: **IMPLEMENTATION CANDIDATE — NOT YET CLOSED**

P00 is intentionally kept open until this implementation is integrated, required CI is green, and the exact resulting `main` SHA is rechecked.

## Implemented candidate controls

- .NET 10 LTS target with SDK `10.0.400` pinned in `global.json`.
- `net10.0`, C# 14, nullable enabled, deterministic build and warnings-as-errors baseline.
- Initial semantic version `0.1.0` and auditable artifact naming contract.
- Buildable ASP.NET Core web baseline with a liveness health endpoint only; no future-phase business feature is claimed.
- Executable repository contract checks requiring the pinned SDK/version/solution/security baseline.
- Windows-hosted CI workflow for restore, Release build, P00 checks, publish, ZIP packaging and SHA-256 evidence.
- Per-service/per-environment Go-Live configuration isolation contract recorded for later implementation phases.

## Closure evidence still required

- PR head CI success for Planning Integrity and P00 Build Baseline.
- Normal merge into `main` under repository policy.
- Exact-main Planning Integrity and P00 Build Baseline success on the integrated SHA.
- Reconcile `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`, `PROJECT_CONTROL.md` and issue #1 from that evidence before transition to P01.

No owner-only or external dependency is required to close P00.
