# P16 Full Automated Acceptance Evidence

Status: **OPEN / ACTIVE — candidate acceptance in progress; not phase closure**

Canonical unit: `P16::full-acceptance-release-candidate`  
Canonical continuation branch: `worker/p16-full-acceptance-continuation`

## Legal phase entry

P15 is formally CLOSED. P16 entry was materialized by PR #78 after its final exact head `462a32f6a72e913b1c7ac2abe83aef98f0d97415` completed **37/37 governed pull-request workflows SUCCESS**. PR #78 was normally integrated as exact `main` `3c995a3667d2f82637a6076ecb62c80326fd15d1`, and that exact-new-main completed **34/34 governed push workflows SUCCESS**, failure=0, queued=0 and in-progress=0.

The pre-entry branch `worker/p16-full-acceptance@38bca520590e89d26e85d4f8e9fd27f9920b9d4e` contained only redundant phase-authority material from pre-#78 main and is `SUPERSEDED_BY_PHASE_ENTRY_INTEGRATION`. It is not merged or force-rewritten. This continuation branch recovers the same P16 unit from exact green main and is not a duplicate claim.

## Same-candidate rule

P16 acceptance is valid only when source, runtime/browser evidence, backup/restore rehearsal, performance baseline and release-candidate package/hash identity are produced from the same exact checked-out candidate. Historical green evidence may establish a preserved closed baseline, but it cannot substitute for a failing or missing current exact-candidate gate.

The dedicated P16 workflow must assert `CANDIDATE_SHA` before execution. The release-candidate packaging wrapper overrides legacy PR event SHA ambiguity and verifies that the package manifest `SourceSha` equals the actual checked-out candidate.

## Cloud-actionable acceptance scope

The P16 candidate must complete all of the following before implementation closure can be considered:

- solution restore/build and P16 source-contract validation;
- preserved security/resilience and installer-source checks;
- exact-candidate browser/runtime acceptance for Dashboard, Service Execution, Permissions, and Audit using synthetic data and SQL Server LocalDB;
- English LTR and Arabic RTL desktop plus narrow screenshot evidence for all four canonical reference screens;
- semantic UI parity against the four repository-native SVG baselines, with screenshot and baseline SHA-256 identities bound to the candidate;
- SQL Server LocalDB `BACKUP DATABASE` / `RESTORE VERIFYONLY` / destructive-loss / `RESTORE DATABASE` rehearsal with restored application-state verification;
- App_Data Data Protection-key and protected setup-state backup/delete/restore hash-preservation rehearsal using synthetic state only;
- bounded local health/login performance smoke baseline with recorded averages, p95, maxima and zero HTTP failures;
- exact-candidate versioned Windows package and Setup EXE with verified SHA-256 and byte sizes;
- all repository-governed pull-request workflows terminal SUCCESS on the final exact P16 implementation head;
- normal integration from current main followed by every governed exact-new-main workflow terminal SUCCESS;
- governance, ledger and closure evidence reconciliation before any legal P17 transition.

## UI parity evidence model

Canonical references remain:

1. `docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg` — Dashboard;
2. `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg` — Service Execution;
3. `docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg` — Permissions & Role Management;
4. `docs/ui-baseline/kuwait_government_audit_dashboard.svg` — Audit & Monitoring.

P16 executes the existing runtime/browser verifiers on the exact candidate instead of duplicating their implementation. The P16 parity consolidator runs only after those scripts succeed, then copies and hashes 16 browser captures (English/Arabic × desktop/narrow × four screens), hashes the four canonical SVG baselines and records the semantic-verifier source for each screen. It does not claim mathematical pixel equivalence; the executed phase verifiers provide semantic component/hierarchy, bilingual direction, authorization and responsive acceptance as required by `docs/UI_DESIGN_PARITY_GATE.md`.

## Recovery boundary

The cloud recovery rehearsal uses synthetic SQL Server LocalDB and synthetic App_Data state. It validates the supported procedure mechanically without manufacturing Production credentials, certificates, personal data or server evidence. Production backup location, retention, encryption, service account, DPAPI machine context and restore-window approval remain deployment-owner concerns and are not inferred from CI.

## Performance boundary

The P16 performance job is a bounded localhost smoke baseline, not a capacity/SLA certification. It verifies that repeated `/health/live` and bilingual Login requests complete with zero HTTP failures under generous hosted-runner p95 thresholds and records raw summary metrics in the P16 artifact. Production load, network latency, proxy behavior and concurrency sizing remain environment-specific acceptance.

## Deferred / owner-last boundaries — NOT PASS

The following remain explicitly outside cloud evidence where unavailable and MUST NOT be promoted to PASS by P16 automation:

- authorized real MOJ UAT smoke: `DEFERRED_EXTERNAL_NOT_PASS` until owner-controlled credentials/data are available and approved for non-destructive testing;
- unproven MOJ Production operation details: `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`; no Production -> UAT fallback is permitted;
- actual target Windows Server/IIS installation, approved Production HTTPS certificate selection/binding, Production DNS/network/proxy/firewall behavior and owner-controlled code/release signing: `OWNER_LAST / NOT PASS` until executed on the owner environment;
- repository main branch protection / required-check enforcement: `OWNER_LAST / NOT PASS` while live repository read-back reports protection disabled.

These external states do not stop independent cloud-actionable P16 acceptance, but they cannot be represented as passed evidence.

## P16 exit gate

P16 is **not CLOSED** by this document or by creation of the workflow. Closure requires the final exact P16 implementation head to pass the complete governed PR matrix including the dedicated P16 gate; normal merge from current main; resulting exact-main governed CI terminal green; exact release-candidate artifact identity reconciled to that accepted source; and canonical governance/evidence reconciliation. Only after that gate may P17 become the sole canonical phase. `VERIFIED_FINAL_COMPLETE` remains forbidden before P17 final evidence exists.
