# P11 — Audit Trail, Tamper Evidence and Monitoring

Status: **CANDIDATE / STACKED GOVERNANCE EXCEPTION — NOT CANONICAL PHASE CLOSURE**

This evidence describes the cloud-actionable P11 candidate on `worker/p11-audit-tamper-monitoring`. Canonical phase authority remains governed by `CURRENT_PHASE.md` / `PROJECT_CONTROL.md` / `docs/TASK_LEDGER.md`. P11 must not be represented as canonical CLOSED or merged ahead of unresolved predecessor reconciliation.

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

## Executable acceptance

`.github/workflows/p11-audit-monitoring.yml` binds evidence to the exact candidate SHA and contains two independent jobs:

1. Linux security/UI contract job:
   - exact-SHA checkout;
   - pinned SDK restore/build of the solution;
   - executable leakage/hash negative acceptance;
   - bilingual Audit-dashboard contract verification;
   - forbidden evidence-pattern scan;
   - planning-integrity validation;
   - evidence artifact upload.

2. Windows SQL LocalDB job:
   - exact-SHA checkout;
   - build of the P11 integration executable;
   - real migrations on LocalDB;
   - append-only enforcement;
   - integrity/tamper/tail-deletion detection;
   - concurrent writer serialization;
   - retention checkpoint behavior;
   - monitoring snapshot behavior;
   - exact-candidate evidence manifest and artifact upload.

P00 build/package/hash remains an independent regression baseline.

## Predecessor and external truth

P11 is stacked on the legitimate P10 candidate and retains P10 ancestry. It is not eligible for a normal merge until predecessor phase/governance reconciliation permits it. This document does not convert queued, running, failed or unexecuted CI into PASS.

Operational validation requiring a deployed owner environment may be recorded later as `OWNER_LAST` / `DEFERRED_EXTERNAL`; it is not treated as automated evidence.
