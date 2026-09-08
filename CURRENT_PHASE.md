# CURRENT_PHASE.md

## Canonical current phase

**P00 — Baseline and live-state discovery**

Status: **OPEN / READY**

This repository has been bootstrapped with the owner-approved plans, execution sequence, UI references, MOJ documentation references, governance contracts, and a planning-integrity gate. Product implementation has not yet been declared complete for P00.

## Legal work now

P00 only, plus any repair needed to keep the repository bootstrap valid.

The worker must:

- inspect live `main`, PRs, branches, tracker issue(s), and CI before writing;
- verify the plan/reference files are present and internally consistent;
- pin the supported .NET target/runtime and solution/build toolchain;
- establish the initial solution architecture and build/test/CI baseline required by P00;
- set initial semantic version/artifact naming conventions;
- reconcile `PROJECT_CONTROL.md` and `docs/TASK_LEDGER.md` from actual implementation evidence;
- run the planning-integrity workflow and any new P00 CI;
- push/merge legitimate P00 work and recheck exact `main`;
- close P00 only from evidence, then change this file to P01.

## Locked future work

P01–P17 are not current work until P00 is CLOSED. Do not implement future-phase features merely because their requirements are already known.

## Exit condition

P00 may be marked CLOSED only when its live-state discovery, platform/toolchain decisions, repository structure, build/test baseline, CI baseline, control documents, and exact-main verification are complete and pushed.
