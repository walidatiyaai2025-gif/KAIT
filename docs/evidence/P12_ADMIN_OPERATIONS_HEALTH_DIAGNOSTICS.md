# P12 — Admin Operations, Health and Diagnostics

Status: **CLOSED — exact integrated evidence**

P12 is closed from the canonical recovered implementation line `worker/p12-admin-health-diagnostics`, normal integration through PR #69, and terminal exact-main verification. This document records only evidence actually produced by repository implementation and CI; historical owner/external boundaries remain NOT PASS where evidence is unavailable.

## Recovery and integration authority

P11 closure reconciliation opened P12 on exact main `a81f5d196aa3e35e125735cde2a62e90c7d75c16` after its transition matrix completed green.

The pre-existing P12 branch was recovered rather than replaced. Historical P12 work was reconciled non-force with current main, preserving legitimate Admin Operations contracts/service work and preventing a duplicate implementation line.

PR #69 became the single canonical P12 integration line.

### Superseded candidate and required semantic repair

Historical candidate `08680d5388cf1dfb3a5ddeabc6ac247f2e65ef5e` completed a green workflow wave but is **superseded and is not the final closure candidate**. Post-gate semantic review found that Activate/Disable reused `IMetadataCatalogService.UpdateServiceAsync`. For an already-used service, the canonical metadata service intentionally creates a new definition revision; therefore an operational toggle could change the current ServiceId and destabilize exact AuthProfile/SecretRef scope even though the then-current tests were green.

The defect was repaired on the same canonical P12 line without weakening acceptance. Operational state mutation is separated from metadata-definition revisioning through `IAdminOperationalStateService` / `AdminOperationalStateService`.

The final state command:

- targets the exact current ServiceId + EnvironmentId binding;
- enforces exact service-level `Services.Manage` authorization in addition to the MVC policy;
- rejects empty, unknown, mismatched and inactive-parent activation targets;
- updates only `ServiceEnvironmentConfig.Active` on the exact existing row;
- does not call metadata revision APIs;
- preserves ServiceId, definition version, AuthProfileId, SecretRef ownership, endpoint metadata and sibling UAT/Production configuration;
- commits the state change and sanitized P11 audit record atomically and rolls back the mutation if auditing fails.

A dedicated Windows SQL LocalDB executable acceptance uses an already-used synthetic service (`FirstUsedAtUtc` populated) specifically to exercise this invariant. It proves disable/activate retains exact ServiceId, metadata version and configuration-row identity; UAT changes leave Production state/endpoint/timeout/diagnostic status unchanged; and unauthorized, forged-environment and inactive-parent paths fail closed.

## Operational health and diagnostics boundary

P12 extends the existing metadata/authentication/secret/audit architecture rather than creating competing engines.

`IAdminOperationsService` provides protected health and diagnostic operations for:

- SQL Server connectivity and pending-migration state;
- application runtime/uptime;
- Data Protection round-trip health;
- disk free-space thresholds;
- configuration readiness for visible Service + Environment bindings;
- external integration endpoint reachability using configured health paths;
- bounded health-probe timeout;
- HTTP, timeout, TLS/network and clock-skew classification;
- exact-scope authentication diagnostics through the canonical P08 authentication probe;
- repeated operational-failure and missing-health-probe alerts.

Integration reachability probes never resolve or attach secret material. Authentication testing remains a separate protected operation through the exact AuthProfile/SecretRef boundary. Production-to-UAT fallback is not introduced.

## Reuse of canonical administration boundaries

P12 deliberately reuses closed-phase administration instead of duplicating it:

- `/metadata` remains the canonical edit surface for Service + Environment endpoint, timeout, TLS, certificate validation, proxy, health and AuthProfile metadata;
- `/auth-profiles` remains the canonical write-only secret administration and masked display surface;
- authentication testing remains server-authorized and anti-forgery protected;
- secret rotation remains the P06 atomic stage -> activate -> discard-on-failure boundary, preserving the current valid secret on failed rotation and never redisplaying plaintext.

The Operations dashboard links those canonical edit/authentication/rotation surfaces while adding health, diagnostics and exact environment activation/disable operations.

## Authorization, CSRF and evidence safety

The `/operations` surface is authenticated and operational health requires `Diagnostics.Run`.

Mutation routes are anti-forgery protected, including database diagnostic, Service + Environment Test Connection, exact-scope Test Authentication and Activate/Disable. Activate/Disable additionally requires `Services.Manage` and exact service-level authorization. Forged or missing targets fail without cross-scope fallback.

Audit/diagnostic evidence is limited to bounded operational facts such as result code, state, duration, HTTP status, clock skew, environment code and active state. Passwords, API keys, bearer tokens, request/response bodies and personal MOJ payloads are excluded from committed evidence.

## Bilingual operations UI

`/operations` provides responsive Arabic RTL / English LTR administration with overall health, database/runtime/Data Protection/disk/configuration components, Entity -> Service -> Environment inventory, UAT/Production state, Test Connection, Activate/Disable, links to canonical Edit/Test Authentication/Rotate Secret administration, last sanitized test status, alerts and correlation identifier. No secret input or recovered plaintext is rendered by the Operations view.

## Final exact-head acceptance

Final implementation head: `f367ca78fca217e9d5c7da0a1328dca047940390`.

PR #69 completed **35/35 exact-head workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 before merge.

Dedicated P12 acceptance included:

- full solution Release build with no acceptance waiver;
- `P12_OPERATIONS_STATIC_ACCEPTANCE=PASS checks=38`;
- `P12_OPERATIONAL_STATE_ISOLATION=PASS checks=4` against SQL LocalDB;
- already-used-service ServiceId/version/configuration identity preservation;
- UAT/Production sibling isolation;
- unauthorized, forged-environment and inactive-parent fail-closed paths;
- protected Arabic RTL / English LTR runtime/browser acceptance;
- unauthenticated challenge and Read Only denial;
- authorized database diagnostic;
- anti-forgery rejection without a token;
- forged Service + Environment target rejection;
- browser screenshots/evidence;
- bearer/API-key/password/personal-data leakage scanning;
- exact-candidate evidence artifact production.

## Normal integration and exact-main verification

PR #69 merged normally into exact main:

`1ed40707552f62058980b44d6cb1e7251dbeb2d4`

The resulting exact main completed **31/31 push workflows SUCCESS**, failure=0, queued=0 and in-progress=0.

Exact-main P12 workflow run `34413925793` completed SUCCESS on the same integrated SHA.

Exact-main artifacts:

- Runtime: `P12-Admin-Operations-Runtime-1ed40707552f62058980b44d6cb1e7251dbeb2d4`
  - artifact id: `10128361921`
  - size: `727846` bytes
  - digest: `sha256:8161d7c95e2ee64cf3c0fdac6b3d252bc0ee0052fe662c1a2973e1b9cc4f194b`
- Static: `P12-Admin-Operations-Static-1ed40707552f62058980b44d6cb1e7251dbeb2d4`
  - artifact id: `10128310606`
  - size: `1640` bytes
  - digest: `sha256:3029f8ddf62255952478fb0197a4399a5724b028a83919eb7de332a9552654d9`

These artifacts are bound by GitHub Actions metadata to exact main `1ed40707552f62058980b44d6cb1e7251dbeb2d4`.

## External / owner-last truth

P12 cloud acceptance uses synthetic/local infrastructure and requires no live MOJ credential or personal record. Historical P09 authorized live-UAT evidence that remains unavailable stays `DEFERRED_EXTERNAL_NOT_PASS`. Unproven Production operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Neither classification is rewritten as PASS by P12 automated diagnostics.

Any validation that genuinely requires owner deployment remains OWNER_LAST / DEFERRED_EXTERNAL with an exact later acceptance action; it is not fabricated from local evidence.

## Closure and transition

All cloud-actionable P12 implementation, dedicated acceptance and exact-main verification required by the P12 exit condition are integrated and green. P12 may therefore be recorded CLOSED by the governance reconciliation.

P13 is the next phase, but this evidence document does not itself authorize P13 implementation. P13 becomes canonical only after the P12 closure reconciliation is normally integrated and the resulting exact-new-main regression matrix is terminal green.
