# CURRENT_PHASE.md

## Canonical current phase

**P02 — Complete first-run Setup Wizard**

Status: **OPEN / READY**

P01 is CLOSED from integrated exact-main evidence. The P01 implementation was normally merged by PR #4 at `34c665b12e910b6dfcb735f4978be6649e03d5c3`. On that exact SHA, Planning Integrity run `34213985141`, P00 Build Baseline run `34213985591`, and P01 Architecture and UI Shell run `34213985167` all succeeded. The P01 run performed Release build, preserved P00 contracts, passed P01 architecture/design checks, executed bilingual runtime/browser verification, and uploaded exact-main UI evidence.

The exact-main P01 artifact is `P01-UI-Evidence-34c665b12e910b6dfcb735f4978be6649e03d5c3` with workflow digest `sha256:11492dc4afa4d2257f410540cf757a40be91926e89a61611896bc3f30b06d09c`.

This P02 transition becomes authoritative only after this closure change is normally integrated to `main` and the resulting exact-main P00/P01/Planning gates remain green. Do not begin P02 implementation from an unmerged transition branch.

## Legal work now

After this transition is integrated and exact-main verification is green, P02 only, plus any repair needed to preserve closed P00/P01 baselines and repository controls.

P02 must implement the complete first-run Setup Wizard before normal Login:

- redirect to `/setup` whenever protected setup-completed state is absent; normal Login must not be usable before Finish;
- Welcome + language, preflight, database configuration, Test Connection, create/use existing database, EF Core migrations, initial System Administrator, organization/branding, security baseline, integration environment, secret placeholders, optional notifications, review/health check and Finish;
- SQL Server inputs for Server/Instance, Database Name, Windows or SQL authentication, username/password when required, Encrypt, TrustServerCertificate and timeout;
- secure handling of connection credentials without source, JSON, log, audit or evidence leakage;
- restart/resume behavior, critical-failure blocking, migration failure/retry, server-side protection against unauthorized setup rerun after completion;
- service/environment placeholders only where required by setup; no real Go-Live credentials and no future-phase service execution/auth implementation;
- automated unit/integration/browser tests covering successful setup and negative/retry paths;
- build/test/repair/retest, normal PR/merge and exact-main verification before closure.

## Locked future work

P03–P17 remain locked. Do not implement Identity/MFA, detailed RBAC, metadata administration, Secret Vault/auth profiles, generic execution, MOJ authentication/services, history/audit, operational administration, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding. Setup may create placeholders, but every future Service + Environment binding and AuthProfile remains isolated by default and no secret may be copied between services automatically.

## P01 closure evidence

- Integrated implementation SHA: `34c665b12e910b6dfcb735f4978be6649e03d5c3`
- PR: #4 — normally merged
- Planning Integrity: run `34213985141` — SUCCESS
- P00 Build Baseline regression: run `34213985591` — SUCCESS
- P01 Architecture and UI Shell: run `34213985167` — SUCCESS
- Exact-main artifact: `P01-UI-Evidence-34c665b12e910b6dfcb735f4978be6649e03d5c3`
- Artifact workflow digest: `sha256:11492dc4afa4d2257f410540cf757a40be91926e89a61611896bc3f30b06d09c`
- Browser evidence: English/Arabic Dashboard and Login, true LTR/RTL, visible `v0.1.0`, shell-only Login boundary
- Owner/external dependency: none

## P02 exit condition

P02 may be marked CLOSED only when the complete first-run setup flow, SQL Server/database creation-or-use and migrations, protected setup state, initial administrator bootstrap, security/configuration review, restart/retry behavior, automated tests/browser evidence, CI and exact-main verification are complete and pushed without exposing live secrets.
