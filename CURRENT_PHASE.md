# CURRENT_PHASE.md

## Canonical current phase

**P07 — Generic service execution engine**

Status: **OPEN / READY**

P06 is CLOSED from integrated exact-main evidence. The final P06 implementation/security candidate was normally integrated through PR #26 at exact `main` SHA `4c6c8460415a71c4eedb184066f6ba928faa8416`, after the canonical foundation, rotation/redaction/cache, Windows/IIS Data Protection hardening, AuthProfile administration and independent security/evidence work had converged through PRs #24, #25, #27, #28 and #29.

On that exact P06 implementation SHA, all **13/13** applicable exact-main checks succeeded. P06 Security Acceptance and Evidence run `34278983765` succeeded and produced `P06-Security-Evidence-4c6c8460415a71c4eedb184066f6ba928faa8416` (5,975 bytes), digest `sha256:052190f8b090ca8981e01bef73bc5e2176d6282b5c448faa03b05fe5adb6af1a`. P06 AuthProfile Administration run `34278983611` also produced `P06-AuthProfile-Admin-Evidence-4c6c8460415a71c4eedb184066f6ba928faa8416` (552,487 bytes), digest `sha256:b3d4f0d62418e7c69cc03ad52599e88bc54c6c3f14910594cd8432d617d241a7`.

P06 acceptance verifies opaque scoped SecretRefs, default Service + Environment isolation, explicit auditable Shared AuthProfile bindings, durable atomic expected-current-reference/generation rotation with rollback, profile/cache generation advancement, centralized redaction/masking, cross-service-safe token-cache identity, protected metadata/binding lifecycle, server-side authorization/IDOR/CSRF controls, write-only secret administration, masked-only display, Windows/IIS DPAPI protection for persisted Data Protection keys, bilingual Arabic RTL / English LTR responsive browser evidence, independent negative/no-leak security acceptance, and preservation of P00–P05 contracts. No owner-only or external P06 evidence is deferred.

This P07 transition becomes authoritative only after this closure reconciliation is normally integrated to `main` and the resulting exact-main closed-phase regressions remain green. Do not begin P07 implementation from an unmerged transition branch.

## Legal work now

After this transition is integrated and exact-main verification is green, P07 only, plus any repair needed to preserve closed P00/P01/P02/P03/P04/P05/P06 baselines and repository controls.

P07 scope is the canonical ledger scope: the generic service execution engine must resolve the exact `Service + Environment + AuthProfile` binding and drive the high-fidelity dynamic Service Execution UI without introducing MOJ-specific P08/P09 behavior early.

## Locked future work

P08–P17 remain locked. Do not implement MOJ authentication/services, history/audit, operational administration, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding. Execution must never fall back across Service or Environment boundaries, and credential/token resolution must remain scoped to the exact authorized binding.

## P06 closure evidence

- Final integrated implementation/security SHA: `4c6c8460415a71c4eedb184066f6ba928faa8416`
- Final independent security PR: #26 — normally merged
- AuthProfile administration PR: #29 — normally merged
- Canonical foundation PR: #25 — normally merged
- Rotation/redaction/cache PRs: #24 and #27 — normally merged
- Windows/IIS Data Protection hardening PR: #28 — normally merged
- Exact-main applicable checks: **13/13 SUCCESS**
- P06 Security Acceptance and Evidence: run `34278983765` — **SUCCESS**
- Exact-main security artifact: `P06-Security-Evidence-4c6c8460415a71c4eedb184066f6ba928faa8416`
- Security artifact size: 5,975 bytes
- Security artifact digest: `sha256:052190f8b090ca8981e01bef73bc5e2176d6282b5c448faa03b05fe5adb6af1a`
- Exact-main admin/browser artifact: `P06-AuthProfile-Admin-Evidence-4c6c8460415a71c4eedb184066f6ba928faa8416`
- Admin/browser artifact size: 552,487 bytes
- Admin/browser artifact digest: `sha256:b3d4f0d62418e7c69cc03ad52599e88bc54c6c3f14910594cd8432d617d241a7`
- Owner/external dependency: none deferred for P06

Detailed evidence is recorded in `docs/evidence/P06_SECRET_VAULT_AUTH_PROFILES.md`.

## P07 exit condition

P07 may be marked CLOSED only when generic execution resolves the exact authorized Service + Environment + AuthProfile configuration, performs metadata-driven request execution without cross-service/environment fallback, exposes the required dynamic high-fidelity execution UI, has executable positive/negative tests and evidence, preserves P00–P06 security/isolation contracts, and passes exact-main verification without introducing P08+ scope.
