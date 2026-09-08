# P02 — First-run Setup Wizard Closure Evidence

## Exact integrated implementation

- Repository: `walidatiyaai2025-gif/KAIT`
- Integrated implementation `main` SHA: `efa56808db13fa45f803131f7bcf2d65c485a66d`
- Implementation lineage: PR #6 (initial P02 Setup Wizard), PR #7 (negative/retry/restart acceptance), PR #8 (wrong SQL credentials and migration failure/retry), PR #9 (Review/Health Check critical-failure gate)
- Final implementation PR: #9 — normally merged

## Exact-main verification

All required regressions and P02 acceptance completed successfully on the same exact integrated implementation SHA:

- Planning Integrity run `34228695674` — SUCCESS
- P00 Build Baseline run `34228695950` — SUCCESS
- P01 Architecture and UI Shell run `34228695746` — SUCCESS
- P02 First-run Setup Wizard run `34228695995` — SUCCESS

The P02 job performed restore, Release build, preserved P00/P01 contracts, started SQL Server LocalDB, ran SQL/setup/security acceptance, ran first-run browser evidence and uploaded the evidence artifact.

## Artifact

- Name: `P02-Setup-Evidence-efa56808db13fa45f803131f7bcf2d65c485a66d`
- Artifact ID: `10056873438`
- Size: 390,868 bytes
- Workflow digest: `sha256:f03ad21356d2ba92308b2cff015d934d0ef23b9e78b02a4f0eb26c9cd06f8961`
- Workflow run: `34228695995`
- Source branch: `main`
- Source SHA: `efa56808db13fa45f803131f7bcf2d65c485a66d`

## Acceptance covered

P02 evidence verifies the current-phase contract, including:

- protected first-run gate redirects normal application access to `/setup` until setup is completed;
- Welcome/language and bilingual browser behavior;
- preflight and protected restart/resume state using persisted Data Protection keys;
- SQL Server configuration with Windows or SQL authentication, encryption/certificate/timeout controls and explicit Test Connection;
- missing and incorrect SQL-credential negative paths without logging or rendering the password;
- create-new/use-existing database behavior and EF Core migrations;
- a real migration-blocking schema conflict followed by successful retry after the conflict is removed;
- initial System Administrator bootstrap with password policy, one-way hashing and forced-change handoff;
- organization/branding, security baseline, integration environment and optional notification configuration;
- ten inactive MOJ Service+Environment placeholders: five services × UAT/Production, without real endpoints or credentials and without cross-service secret sharing;
- Review/Health Check with structured PASS/WARNING/FAIL behavior and a server-side critical-failure gate that blocks Finish;
- successful Finish persists protected completion state and prevents public setup rerun;
- exact-main bilingual browser evidence and regression preservation for P00/P01.

## Security and isolation

- No live CAIT/MOJ credentials, API keys, consumer secrets, tokens, database passwords, private keys, certificates or personal test data are recorded in source, fixtures, logs or this evidence.
- Setup state and completion metadata that contain sensitive configuration are protected at rest; passwords are not rendered back to HTML.
- Bootstrap administrator password storage is one-way hashed.
- P02 creates configuration placeholders only. UAT and Production remain distinct per Service and no credential/token sharing is introduced.
- TLS, authorization or test protections were not weakened for closure.

## Owner-last / external inputs

No owner-only or external input is required to close P02. Real government UAT/Production credentials, network allowlisting and production certificates remain future deployment/integration inputs and were neither required nor faked for this Setup phase.

## Transition rule

This evidence records P02 implementation closure eligibility on `efa56808db13fa45f803131f7bcf2d65c485a66d`. P03 becomes the canonical current phase only after the closure/reconciliation PR containing this document, `CURRENT_PHASE.md`, `PROJECT_CONTROL.md` and `docs/TASK_LEDGER.md` is normally merged to `main` and the resulting exact-main required gates remain green.
