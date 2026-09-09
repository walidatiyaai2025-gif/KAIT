# P11 — Audit Trail, Tamper Evidence and Monitoring

Status: **OPEN-PHASE IMPLEMENTATION CANDIDATE — NOT P11 CLOSURE**

This evidence describes the cloud-actionable P11 implementation on the canonical existing branch `worker/p11-audit-tamper-monitoring`. Canonical phase authority is `CURRENT_PHASE.md` / `PROJECT_CONTROL.md` / `docs/TASK_LEDGER.md`.

P10 is CLOSED after PR #66 normally merged at exact `main` SHA `951901869dece4771a3ad99f4854ab5f0c7a5be4`; that exact main completed **29/29 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 after completion. P11 is therefore the canonical **OPEN / READY** phase. Nothing in this document marks P11 CLOSED; normal integration plus exact-new-main closure evidence is still required.

## Canonical audit architecture

P11 extends the existing `AuthenticationAuditEvents` store rather than creating a competing log. Authentication events, security-sensitive administration, service execution, request-history access/export and audit self-access converge on the same audit writer and projection.

Canonical records carry a monotonic sequence and SHA-256 chain over actor/action/target/timestamp/outcome/correlation/request/entity/service/source/device/sanitized metadata/retention/previous-hash fields. `AuditChainState` keeps the persisted tail and retention checkpoint so verification can detect record mutation, gaps/reordering and tail deletion.

Database append-only enforcement rejects UPDATE/DELETE of canonical records. Retention is a bounded prefix-only operation that first validates chain continuity and uses a transaction-scoped maintenance exception; it advances a checkpoint rather than silently resetting the chain.

Concurrent writers are serialized by a SQL transaction application lock. Audit storage and integrity operations are bounded and transaction-scoped.

## Sensitive-data controls

Audit facts are sanitized through the existing central secret-redaction contract. The audit layer does not intentionally persist request/response bodies, authorization headers, passwords, API keys, bearer tokens, secret values or personal service payloads.

The MVC audit boundary records controller/action/route facts only. It deliberately does not inspect action arguments, form fields, request bodies, query payloads or credential headers.

Independent negative acceptance uses synthetic markers only and verifies that secret/token/password/personal-payload markers are absent from audit-safe projections and CI evidence. No production credentials, JWTs, Civil IDs or personal MOJ data are included in P11 evidence.

## Server-side authorization

- Audit query/detail/monitoring: `Audit.View`.
- Export: `Audit.View` + `Audit.Export`.
- Sensitive audit projection: `Audit.ViewSensitive`, with sensitive-view access itself audited.
- Integrity verification: `Diagnostics.Run`.
- MVC controller is authenticated and treats `AuditTrailAccessDeniedException` as `Forbid`.
- Integrity verification is POST-only and anti-forgery protected.
- Protected runtime acceptance uses a separate authenticated synthetic Read Only account and requires HTTP 403 for Audit dashboard access and Audit export, proving that authentication alone cannot bypass server-side Audit permissions.

UI visibility is not treated as an authorization boundary.

## Monitoring and UI

The bilingual `/audit` dashboard is derived from the canonical audit service and includes:

- current audit counts and failed events in the last 24 hours;
- integrity status, last sequence and retention checkpoint;
- retained canonical/legacy record indicators;
- latest audit and verification timestamps;
- bounded filters and paging;
- audit activity volume visualization;
- safe event detail projection with correlation/request IDs, sanitized metadata and record hash;
- permission-gated CSV export and integrity verification;
- responsive Arabic RTL / English LTR presentation.

The global navigation links the canonical request-history and audit surfaces; authorization remains server-side.

P11 visual/runtime acceptance is bound to `docs/ui-baseline/kuwait_government_audit_dashboard.svg`. The Windows acceptance job now starts the real GSIP web application against an isolated synthetic SQL LocalDB database in the dedicated `RegressionTesting` environment, verifies the unauthenticated challenge, authenticates both a synthetic Read Only user and a synthetic System Administrator, proves authenticated unauthorized denial, exercises the protected `/audit` route in English LTR and Arabic RTL, verifies filtering and event detail, downloads the permissioned CSV export, performs authorized integrity verification, proves CSRF rejection without an anti-forgery token, and captures desktop and narrow Chrome/Edge screenshots. Runtime evidence is scanned for bearer/API-key patterns, 12-digit personal identifiers and the synthetic login password before upload.

This runtime/browser path is a required candidate gate. Its presence in the workflow is not itself a PASS; only a terminal-success result bound to the exact final PR head is acceptance evidence.

## Recovered integrity regression

The recovered historical P11 branch initially failed fresh-chain LocalDB integrity verification. Root cause was schema-level fixed-width padding: the migration declared `PreviousHash` as `char(64)`, while the first record stores the sentinel `GENESIS`. SQL Server returned that value padded to 64 characters, so the unchanged verifier correctly rejected the record.

The canonical branch repair changed only `PreviousHash` storage to variable-length `nvarchar(64)`; `RecordHash` remains the fixed-width SHA-256 representation. No integrity, tamper, append-only, concurrency or retention assertion was weakened. On repaired head `0593d97c176cb76e855a3209c3413201f76d9a39`, both the P00 baseline workflow and the dedicated P11 workflow completed SUCCESS, including append-only, integrity, concurrency, retention, monitoring and tamper-detection acceptance.

The branch was then reconciled with exact P10-closed main without force-push and stale closed-phase content was replaced by exact-main versions. The prior reconciled head passed the full existing matrix; the strengthened runtime/browser gate must now pass again on the new exact final candidate head before integration.

## Executable acceptance

`.github/workflows/p11-audit-monitoring.yml` binds evidence to the exact candidate SHA and contains two independent jobs:

1. Linux security/UI contract job:
   - exact-SHA checkout;
   - pinned SDK restore/build of the solution;
   - executable leakage/hash negative acceptance;
   - bilingual Audit-dashboard source contract verification;
   - forbidden evidence-pattern scan;
   - planning-integrity validation;
   - evidence artifact upload.

2. Windows SQL LocalDB + protected runtime/browser job:
   - exact-SHA checkout;
   - pinned SDK restore/build of the solution and P11 integration executable;
   - real migrations on isolated LocalDB;
   - append-only enforcement;
   - integrity/tamper/tail-deletion detection;
   - concurrent writer serialization;
   - retention checkpoint behavior;
   - monitoring snapshot behavior;
   - real protected GSIP web runtime using synthetic-only LocalDB state;
   - unauthenticated `/audit` challenge verification;
   - authenticated Read Only `/audit` and export denial;
   - authenticated System Administrator English LTR / Arabic RTL `/audit` rendering;
   - canonical LoginSuccess audit visibility, filter and detail verification;
   - permissioned CSV export verification;
   - authorized integrity POST plus missing-CSRF negative acceptance;
   - desktop and narrow Chrome/Edge screenshots against the Audit dashboard baseline;
   - forbidden secret/personal-data evidence scan;
   - exact-candidate evidence manifest and artifact upload.

P00 build/package/hash and all closed-phase workflows remain independent regression baselines.

## External / owner-last truth

P11 implementation and its automated acceptance do not require owner credentials or personal MOJ data. The historical P09 authorized live-UAT smoke and unproven Production operation proof remain `DEFERRED_EXTERNAL_NOT_PASS` / `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` under the tracker and owner-last policy; P11 does not rewrite them as PASS.

Any later operational validation that genuinely requires a deployed owner environment must be recorded as `OWNER_LAST` or `DEFERRED_EXTERNAL` with an exact acceptance action. It is never substituted by automated evidence or called PASS without the real owner-side result.
