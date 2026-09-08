# FINAL ACCEPTANCE CRITERIA — GSIP

Final completion is a same-commit/same-artifact claim. P17 may declare the product complete only when all applicable criteria below are backed by evidence from the exact final integrated commit.

## Product and setup

- Professional Windows/IIS installer with versioned artifact, clean install, upgrade, repair and uninstall behavior tested.
- First-run application Setup Wizard appears before normal Login on a clean deployment.
- Setup supports language, prerequisites, SQL Server settings, Test Connection, create/use database, migrations, initial administrator, organization/branding, security baseline, integration environment, secret placeholders, review/health and Finish.
- Setup-completed state is persisted/protected and controlled recovery is tested.

## Identity and authorization

- Login/logout, password policy, lockout/rate limit, session controls and MFA pass positive/negative tests.
- RBAC is enforced server-side.
- Service-level permissions and request/audit/export scopes are verified against unauthorized access/IDOR paths.

## Integration platform

- Metadata-driven entities/services/fields/result mappings work without per-service controller/view code.
- Secret Vault prevents plaintext secret persistence and supports rotation/redaction.
- Generic execution engine validates inputs, resolves environment/auth, uses safe HttpClient patterns, correlates requests and maps results.

## MOJ initial scope

The following five services are configured from official CAIT/MOJ contracts and have contract fixtures/tests:

1. Marriage Cases Service
2. Is Single Basic Service
3. Marriage Couple Last Case Service
4. Family Judgment Text Service
5. Procuration Status Service

Where authorized UAT credentials/data are available, perform limited non-destructive UAT smoke tests. If not, the external check may remain explicitly deferred under the Owner-Last policy; no service schema may be invented to hide a documentation gap.

## Requests, audit and operations

- Request history, masking, own/department/all scoping and exports are permission-tested.
- Audit events are append-only from the UI and tamper evidence can be verified.
- Sensitive payload viewing/export is permissioned and audited.
- Health/diagnostics/rotation paths are protected and sanitize outputs.
- Backup/restore rehearsal or documented supported procedure is validated for the selected deployment design.

## Bilingual UX and design parity

- Arabic RTL and English LTR pass browser acceptance across Setup, Login, Dashboard, Entities, Services, Service Execution, Requests, Permissions, Audit and Settings.
- The four canonical reference screens pass `docs/UI_DESIGN_PARITY_GATE.md` with screenshot evidence tied to the exact release candidate.
- No critical visual drift, generic-template substitution, broken RTL/LTR or missing major reference component remains.

## Security/resilience

- Required authorization/IDOR, CSRF, XSS/output encoding, rate limiting, session fixation, brute force, secret leakage, timeout/cancellation, retry-storm and dynamic-input tests pass.
- Known Critical/High security defects that affect the release are resolved or the candidate is blocked.
- Dependency/license/security review is documented.
- No live credential or real personal test record is committed.

## Release evidence

Final handoff must state:

- exact repository, branch and commit SHA;
- product version and pinned runtime/toolchain;
- exact Windows installer/package filename(s);
- artifact location and byte size;
- SHA-256 for release artifacts;
- exact CI summary;
- automated test/acceptance summary;
- UI parity evidence summary;
- security summary;
- installer/setup validation summary;
- any truly external deferred acceptance, with precise reason and next action.

A dirty/unpushed workspace, stale CI, mismatched artifact SHA, or unreconciled `CURRENT_PHASE.md` / `docs/TASK_LEDGER.md` blocks final completion.
