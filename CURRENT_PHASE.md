# CURRENT_PHASE.md

## Canonical current phase

**P07 — Generic service execution engine**

Status: **OPEN / READY**

P06 is CLOSED from corrected integrated exact-main evidence. The final P06 implementation/security baseline is exact `main` SHA `fef5882abf8a6f12990c3e7c0e9f849d08cd7947`. P06 originally converged through PRs #24, #25, #26, #27, #28 and #29; a final live-plan audit after the earlier PR #30 closure reconciliation found one canonical P06 requirement that had not yet been integrated: the runtime token cache required by `execution/GSIP_Full_Execution.json`, including an expiry safety window and single-flight refresh. The already-existing legitimate `P06::token-cache-runtime` work was recovered instead of duplicated, reconciled to current main and normally merged through PR #31.

On that corrected exact P06 implementation SHA, all **13/13 exact-main workflow runs succeeded**. P06 Token Cache Runtime run `34281504875` succeeded. P06 Security Acceptance and Evidence run `34281505005` succeeded and produced `P06-Security-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947` (5,975 bytes), digest `sha256:2a3ccafdd523e513caca803514b1dde6b3da349460d5715bca17e6841fe5a573`. P06 AuthProfile Administration run `34281505040` succeeded and produced `P06-AuthProfile-Admin-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947` (557,361 bytes), digest `sha256:aa2adb57ffb3cd7ecece6c696ed4df3bec0bb3c41ad99180b0fb530ac10bce91`.

P06 acceptance verifies opaque scoped SecretRefs, default Service + Environment isolation, explicit auditable Shared AuthProfile bindings, durable atomic expected-current-reference/generation rotation with rollback, profile/cache generation advancement, centralized redaction/masking, protected metadata/binding lifecycle, server-side authorization/IDOR/CSRF controls, write-only secret administration, masked-only display, Windows/IIS DPAPI protection for persisted Data Protection keys, bilingual Arabic RTL / English LTR responsive browser evidence, independent negative/no-leak security acceptance, and the corrected runtime token-cache contract: exact Service + Environment + AuthProfile + version/generation identity, configurable expiry safety window, single-flight refresh, caller-cancellation isolation, rejection of failed/canceled/near-expiry refresh results, and secret-safe token serialization/diagnostics. P00–P05 contracts remain preserved. No owner-only or external P06 evidence is deferred.

The earlier PR #30 closure record is superseded only as to its P06 evidence baseline; its P07 transition remains the intended next phase. The final corrected closure reconciliation was normally integrated through PR #32 at exact `main` SHA `90b3068ea39a342392222ae581e568b94f7f9004`, and all 15 applicable exact-main push workflows on that SHA completed successfully. P07 is therefore the canonical current phase now; this reconciliation does not claim any open P07 implementation unit complete.

## Legal work now

P07 only, plus any repair needed to preserve closed P00/P01/P02/P03/P04/P05/P06 baselines and repository controls.

P07 scope is the canonical ledger scope: the generic service execution engine must resolve the exact `Service + Environment + AuthProfile` binding and drive the high-fidelity dynamic Service Execution UI without introducing MOJ-specific P08/P09 behavior early.

## Locked future work

P08–P17 remain locked. Do not implement MOJ authentication/services, history/audit, operational administration, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding. Execution must never fall back across Service or Environment boundaries, and credential/token resolution must remain scoped to the exact authorized binding.

## P06 closure evidence

- Corrected final integrated implementation/security SHA: `fef5882abf8a6f12990c3e7c0e9f849d08cd7947`
- Runtime token-cache recovery PR: #31 — normally merged
- Final independent security PR: #26 — normally merged
- AuthProfile administration PR: #29 — normally merged
- Canonical foundation PR: #25 — normally merged
- Rotation/redaction/cache PRs: #24 and #27 — normally merged
- Windows/IIS Data Protection hardening PR: #28 — normally merged
- Earlier closure reconciliation: PR #30 — superseded only by this corrected P06 evidence baseline
- Final corrected closure reconciliation: PR #32 — normally merged at exact `main` SHA `90b3068ea39a342392222ae581e568b94f7f9004`
- Exact-main post-reconciliation workflows on `90b3068ea39a342392222ae581e568b94f7f9004`: **15/15 SUCCESS**
- Exact-main workflow runs on corrected implementation SHA: **13/13 SUCCESS**
- P06 Token Cache Runtime: run `34281504875` — **SUCCESS**
- P06 Security Acceptance and Evidence: run `34281505005` — **SUCCESS**
- Exact-main security artifact: `P06-Security-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947`
- Security artifact size: 5,975 bytes
- Security artifact digest: `sha256:2a3ccafdd523e513caca803514b1dde6b3da349460d5715bca17e6841fe5a573`
- P06 AuthProfile Administration: run `34281505040` — **SUCCESS**
- Exact-main admin/browser artifact: `P06-AuthProfile-Admin-Evidence-fef5882abf8a6f12990c3e7c0e9f849d08cd7947`
- Admin/browser artifact size: 557,361 bytes
- Admin/browser artifact digest: `sha256:aa2adb57ffb3cd7ecece6c696ed4df3bec0bb3c41ad99180b0fb530ac10bce91`
- Runtime cache evidence: exact identity isolation, expiry safety window, single-flight refresh, cancellation/failure safety and secret-safe token diagnostics
- P07 implementation introduced by the P06 repair: **NONE**
- Owner/external dependency deferred for P06: **NONE**

Detailed evidence is recorded in `docs/evidence/P06_SECRET_VAULT_AUTH_PROFILES.md`.

## P07 exit condition

P07 may be marked CLOSED only when generic execution resolves the exact authorized Service + Environment + AuthProfile configuration, performs metadata-driven request execution without cross-service/environment fallback, exposes the required dynamic high-fidelity execution UI, has executable positive/negative tests and evidence, preserves P00–P06 security/isolation contracts, and passes exact-main verification without introducing P08+ scope.
