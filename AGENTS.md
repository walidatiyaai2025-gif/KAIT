# AGENTS.md — GSIP Autonomous Worker Contract

This repository is intended for autonomous, phase-gated implementation. These rules are mandatory for every worker, agent, or automation.

## 1. Live state first

Before any write in every cycle:

- fetch `main` and inspect the exact head SHA;
- inspect open PRs, active branches, recent commits, issues/claims, and CI;
- read `PROJECT_CONTROL.md`, `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`, and the current phase contract;
- recover legitimate stale/integration-pending work before creating duplicate work;
- never assume a phase/task state from an old prompt when live repository evidence differs.

## 2. One legal phase at a time

`CURRENT_PHASE.md` is the phase gate. Work only inside the current phase unless a dependency must be repaired to make that phase pass. Do not start future-phase feature work early. A phase closes only when its implementation, tests, evidence, docs/ledger reconciliation, CI, and exact-main recheck are complete.

## 3. Canonical execution sequence

`execution/GSIP_Full_Execution.json` is the canonical autonomous sequence P00–P17. `DefaultDelaySeconds` and every message `DelaySeconds` must remain **40 seconds** unless the owner explicitly changes the cadence.

## 4. UI design parity is a release gate

The four files under `docs/ui-baseline/` are mandatory high-fidelity baselines for v1. Do not replace them with a generic admin template or materially simplify the information architecture. See `docs/UI_DESIGN_PARITY_GATE.md`.

## 5. Setup-first product contract

The finished product must have:

- a professional Windows/IIS deployment installer with Next/Back/Finish;
- a first-run application Setup Wizard before normal Login;
- SQL Server configuration and `Test Connection`;
- create/use database + migrations;
- initial System Administrator creation;
- security baseline/MFA configuration;
- environment/integration configuration;
- review/health check/finish;
- a locked setup state after successful completion, with controlled recovery only.

## 6. Security and secrets

Never commit live API keys, access tokens, passwords, private keys, certificates containing private keys, connection-string passwords, or real personal data. Do not echo secrets in logs, screenshots, tests, exceptions, audit payloads, PR comments, or fixtures. Follow `docs/SECURITY_SECRETS_POLICY.md`.

## 7. External API correctness

Official CAIT/MOJ API documentation and the reference material in `docs/moj-api-reference/` are the authority for service contracts. Do not invent request fields, response fields, authentication behavior, or endpoints. When documentation is incomplete, implement all non-blocked framework work and record the precise missing evidence instead of fabricating data.

Real UAT/Production calls are allowed only with credentials and data that are explicitly authorized for that environment. Prefer contract fixtures/mocks for automated tests.

## 8. Quality loop

For each legitimate unit:

inspect contracts/tests/implementation → implement/recover → test → repair → retest → reconcile docs/evidence → commit → push → PR/update when repository policy requires → inspect CI → repair CI → merge when authorized → exact-main recheck.

No completion claim is valid from a dirty/unpushed working state.

## 9. Owner-last rule

Do not stop merely because a genuine external credential, UAT availability, Windows target, certificate, or owner-only check is unavailable. First finish every cloud/local part possible: implementation, mocks/fixtures, negative tests, setup logic, packaging scripts, CI, docs, evidence templates, and diagnostics. Defer only the irreducibly external check and state it precisely.

## 10. Closure discipline

Never mark a deferred owner-only item PASS without real evidence. Never claim final completion until P17 closure is proven on the same exact commit and release artifacts. The final handoff must identify exact repository/branch/SHA, version, installer/artifacts, hashes, CI status, automated acceptance, security status, UI parity evidence, and any truly external remaining acceptance.
