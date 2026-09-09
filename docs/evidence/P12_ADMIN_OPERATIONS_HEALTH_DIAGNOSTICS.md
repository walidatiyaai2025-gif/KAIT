# P12 — Admin Operations, Health and Diagnostics

Status: **OPEN-PHASE IMPLEMENTATION CANDIDATE — NOT P12 CLOSURE**

This evidence describes the cloud-actionable P12 implementation on the canonical existing branch `worker/p12-admin-health-diagnostics`. Canonical phase authority remains `CURRENT_PHASE.md`, `PROJECT_CONTROL.md` and `docs/TASK_LEDGER.md` until normal integration and exact-new-main verification complete.

## Recovery and integration authority

P11 closure reconciliation merged normally through PR #68 at exact `main` SHA `a81f5d196aa3e35e125735cde2a62e90c7d75c16`. That transition main completed **30/30 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 after completion.

The pre-existing P12 branch was not replaced. Historical branch head `5444317a3bf882bd7393c4f1baf163f0064d56af` was recovered by a non-force merge that preserved its four legitimate P12 commits while taking the exact P11-closed main as ancestry and integrated baseline. Recovered head `199c9044d21451ccfe4f880c15b2dfe175371c4f` was behind main by zero and retained only the legitimate P12 Operations contracts/service plus additive dependency-injection registrations before convergence.

PR #69 is the single canonical P12 integration line. P13-P17 remain locked.

Historical candidate `08680d5388cf1dfb3a5ddeabc6ac247f2e65ef5e` completed **35/35 pull-request workflows SUCCESS**, including both P12 jobs and candidate-bound static/runtime artifacts. That head is **superseded, not closure evidence**, because post-gate semantic review found that its Activate/Disable controller reused `IMetadataCatalogService.UpdateServiceAsync`. For an already-used service, the canonical metadata service deliberately creates a new definition revision; therefore an operational toggle could change the current `ServiceId` and destabilize exact AuthProfile/SecretRef scope even though the then-current tests were green. The acceptance gap is being closed rather than waived.

## Operational health architecture

P12 extends the existing metadata/authentication/secret/audit architecture rather than introducing a competing administration engine.

`IAdminOperationsService` provides protected health and diagnostic operations for:

- SQL Server connectivity and pending-migration state;
- application runtime/uptime;
- Data Protection round-trip health;
- disk free-space thresholds;
- configuration readiness for visible Service + Environment bindings;
- external integration endpoint reachability using configured health paths;
- bounded health probe timeout;
- HTTP, timeout, TLS/network and clock-skew classification;
- exact-scope authentication diagnostic through the canonical P08 authentication probe;
- repeated operational-failure and missing-health-probe alerts.

Health diagnostics never resolve or attach secret material to endpoint reachability probes. Integration health uses only configured `GET`/`HEAD`, preserves the configured BaseUrl scheme/host/port boundary and adds only a correlation identifier. Authentication testing remains a separate protected action through the canonical AuthProfile/SecretRef boundary.

## Exact Service + Environment operational-state isolation

Operational state mutation is intentionally separated from metadata definition versioning through `IAdminOperationalStateService` / `AdminOperationalStateService`.

The state command:

- accepts only one exact current ServiceId + EnvironmentId binding;
- enforces exact service-level `Services.Manage` authorization in addition to the MVC global policy;
- rejects empty, unknown or mismatched target identifiers;
- rejects activation when the parent Service or global Environment is inactive;
- updates only `ServiceEnvironmentConfig.Active` on the exact existing row;
- does not call metadata revision APIs;
- does not change ServiceId, definition version, AuthProfileId, SecretRef ownership, endpoint metadata or sibling UAT/Production configuration;
- writes the state change and its sanitized P11 audit event within the same database transaction;
- rolls back the state mutation if auditing fails.

A dedicated Windows LocalDB executable acceptance uses an already-used synthetic service (`FirstUsedAtUtc` populated) specifically to exercise the metadata-revision boundary. It asserts that disable and activate retain the exact ServiceId, metadata version and configuration row identity; UAT toggles leave Production state/endpoint/timeout/diagnostic status unchanged; and unauthorized, forged-environment and inactive-parent paths fail closed.

No Production-to-UAT fallback is introduced.

## Reuse of canonical administration boundaries

P12 deliberately reuses closed-phase administration instead of duplicating it:

- `/metadata` remains the canonical Edit surface for Service + Environment endpoint, timeout, TLS, certificate validation, proxy, health and AuthProfile metadata;
- operational Activate/Disable no longer uses metadata-definition revisioning and is handled by the dedicated P12 state command;
- `/auth-profiles` remains the canonical write-only secret administration and masked display surface;
- canonical authentication testing remains anti-forgery protected and permission protected;
- canonical secret rotation remains the P06 atomic `stage -> activate -> discard-on-failure` implementation, preserving current valid material on failed rotation and preventing plaintext redisplay.

The P12 Operations dashboard links those canonical edit/auth/rotation surfaces while adding health, diagnostics and exact environment activation/disable operations.

## Server-side authorization and CSRF

The `/operations` surface is authenticated and P12 health service access requires `Diagnostics.Run`.

Mutation routes use anti-forgery validation:

- database diagnostic;
- Service + Environment Test Connection;
- exact-scope Test Authentication;
- Service + Environment Activate/Disable.

Activate/Disable additionally requires global `Services.Manage` policy and exact service-level `Services.Manage` evaluation. Forged/missing targets fail with no cross-scope lookup fallback.

All diagnostic/state-change auditing contains bounded operational facts such as result code, state, duration, HTTP status, clock skew, environment code and active state. Passwords, API keys, bearer tokens, request/response bodies and personal service payloads are not intentionally emitted.

## Bilingual operations UI

`/operations` provides a responsive Arabic RTL / English LTR dashboard with:

- overall health state;
- database/runtime/data-protection/disk/configuration component cards;
- Entity -> Service -> Environment operational inventory;
- exact UAT/Production state indicators;
- Test Connection action;
- Activate/Disable action;
- links to canonical Edit and Test Authentication/Rotate Secret administration;
- last sanitized test status;
- operational alerts and correlation identifier.

The canonical navigation exposes the P12 operations surface as Settings/Operations and identifies P12 as the current phase. No secret input or secret plaintext is rendered by the Operations view.

## Executable acceptance candidate

`.github/workflows/p12-admin-operations.yml` binds acceptance to the exact candidate SHA and defines two independent jobs.

### Linux static/security contract

The job:

- checks out/asserts the exact candidate SHA;
- restores and builds the full solution under the pinned SDK;
- executes `scripts/verify_p12_operations_ui.py`;
- verifies server-side authorization, exact-target/IDOR rejection contracts, anti-forgery coverage, the dedicated no-revision operational-state boundary, canonical authentication/rotation reuse, bilingual RTL/LTR/responsive UI and no secret-bearing view fields;
- verifies source-level presence of executable already-used-service identity and UAT/Production isolation acceptance;
- preserves execution-plan governance;
- rejects forbidden credential/personal-data evidence patterns;
- emits a candidate-bound manifest and evidence artifact.

### Windows LocalDB protected runtime/browser

The job:

- checks out/asserts the exact candidate SHA;
- builds the full solution and the dedicated `GSIP.P12OperationsStateChecks` executable with warnings-as-errors;
- starts SQL Server LocalDB;
- executes the already-used-service operational-state identity/isolation acceptance in a unique synthetic database;
- emits candidate-bound operational-state evidence only after the executable PASS marker is observed;
- creates only synthetic setup/users for the browser database;
- starts the real GSIP web runtime in the dedicated regression environment;
- verifies unauthenticated challenge;
- verifies authenticated Read Only denial without `Diagnostics.Run`;
- verifies System Administrator English LTR and Arabic RTL Operations rendering;
- runs an authorized database diagnostic;
- proves anti-forgery rejection without a token;
- proves forged Service + Environment diagnostic/state targets return rejection;
- captures desktop/narrow Chrome or Edge screenshots;
- rejects bearer/API-key/synthetic-password/12-digit-personal-data evidence patterns;
- uploads the state-isolation and browser/runtime evidence together.

The workflow presence is not itself PASS. Only terminal SUCCESS results on the exact final PR head can be used as P12 acceptance evidence. The prior `08680d5…` successful wave is retained as historical evidence only and cannot satisfy the final gate after this repair.

## External / owner-last truth

P12 cloud acceptance uses synthetic/local infrastructure and requires no live MOJ credentials or personal records. Historical P09 authorized live-UAT evidence that remains unavailable stays `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Neither classification is rewritten as PASS by P12 automated diagnostics.

Any operational validation that genuinely requires the owner deployment remains OWNER_LAST / DEFERRED_EXTERNAL with an exact acceptance action; it is not fabricated from local evidence.

## Exit condition

This document does **not** close P12. Closure requires:

1. exact final PR-head full workflow matrix terminal green, including the strengthened dedicated P12 workflow;
2. normal integration of the canonical PR;
3. exact-new-main regression verification terminal green;
4. reconciliation of `CURRENT_PHASE.md`, `PROJECT_CONTROL.md` and `docs/TASK_LEDGER.md` from the exact integrated evidence;
5. no unresolved cloud-actionable P12 defect or stale integration.
