# WORKER START HERE — GSIP

Repository: `walidatiyaai2025-gif/KAIT`

Your objective is to deliver the complete production-grade GSIP product, not a prototype or static plan.

## Mandatory startup sequence

1. Read `AGENTS.md`.
2. Fetch exact `main`, open PRs, branches, recent commits, CI and tracker issues.
3. Read `PROJECT_CONTROL.md`, `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`.
4. Read `docs/UI_DESIGN_PARITY_GATE.md`, `docs/OWNER_LAST_EXECUTION_POLICY.md`, `docs/SECURITY_SECRETS_POLICY.md`, `docs/FINAL_ACCEPTANCE_CRITERIA.md`.
5. Read `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`; service endpoint/auth/secret/token isolation is mandatory and defaults to per-service/per-environment isolation.
6. Read the human plan in `docs/plans/GSIP_Complete_Implementation_Plan_AR.md`.
7. Execute the canonical sequence from `execution/GSIP_Full_Execution.json` in order P00→P17. Do not bypass phase closure.

## Product contract

Build a bilingual Arabic/English ASP.NET Core Government Services Integration Portal with:

- professional Windows/IIS installation package;
- first-run Setup Wizard before Login;
- SQL Server setup/test/create-or-use/migrations;
- Identity, MFA, sessions and account security;
- RBAC and per-service permissions;
- metadata-driven entities/services/forms/results;
- per-service/per-environment `ServiceEnvironmentConfig` bindings with isolated endpoints, auth profiles and SecretRefs by default;
- encrypted secret vault and authentication profiles, with sharing only through an explicit Shared AuthProfile;
- token caches keyed by ServiceId + EnvironmentId + AuthProfileId plus effective audience/scope dimensions;
- generic external service execution engine;
- request history, exports and masking;
- tamper-evident audit trail and monitoring;
- health/diagnostics/rotation/backup-recovery operational support;
- high-fidelity bilingual UI based on the four mandatory references;
- MOJ as the first entity with the five listed services;
- automated tests, CI, installer artifacts and final release evidence.

## Initial MOJ services

- Marriage Cases Service
- Is Single Basic Service
- Marriage Couple Last Case Service
- Family Judgment Text Service
- Procuration Status Service

Use the official CAIT/MOJ service documentation and `docs/moj-api-reference/`. Never invent service schemas.

## 40-second execution cadence

The execution JSON intentionally uses a 40-second delay for every message. Do not reduce it, normalize it to another value, or remove it unless the owner explicitly requests a change.

## Definition of done

The work is not done because source code exists. P17 requires convergence on exact `main`, green CI, reconciled phase/task documents, versioned Windows installer, hashes, automated acceptance, UI parity evidence, security checks, no committed secrets, and a concise exact-SHA final handoff.
