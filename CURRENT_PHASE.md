# CURRENT_PHASE.md

## Canonical current phase

**P06 — Secret vault and isolated authentication profiles**

Status: **OPEN / READY**

P05 is CLOSED from integrated exact-main evidence. The metadata-driven Entity and Service catalog implementation was normally merged through PR #22 at exact `main` SHA `4d706fab57071236e9847670342f5a399b3527e6`.

On that exact implementation SHA, all ten applicable exact-main workflows succeeded. The P05 Metadata Catalog workflow run `34262416559` succeeded and produced `P05-Metadata-Evidence-4d706fab57071236e9847670342f5a399b3527e6` (507,662 bytes) with workflow digest `sha256:4f903fb240b6f0195fa7ad260afac9b45ece0d443e57f0752457d567abf41796`.

P05 acceptance verifies metadata-driven Entity/Environment/Service/ServiceField/ResultMapping persistence, secure administration, used-definition versioning, strict independent UAT/Production bindings, Production HTTPS and certificate-validation enforcement, rejection of secret-bearing headers, atomic rejection of invalid definitions, JSON-schema import/export round trip, Arabic RTL / English LTR protected administration UI, and a complete synthetic service definition without custom per-service Controller/View code. No owner-only or external P05 evidence is deferred.

This P06 transition becomes authoritative only after this closure reconciliation is normally integrated to `main` and the resulting exact-main closed-phase regressions remain green. Do not begin P06 implementation from an unmerged transition branch.

## Legal work now

After this transition is integrated and exact-main verification is green, P06 only, plus any repair needed to preserve closed P00/P01/P02/P03/P04/P05 baselines and repository controls.

P06 scope is the canonical ledger scope: Secret vault, isolated authentication profiles/SecretRefs, explicit Shared AuthProfile support, rotation/redaction, and cross-service-safe token cache keys.

## Locked future work

P07–P17 remain locked. Do not implement generic execution, MOJ authentication/services, history/audit, operational administration, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding. Every Service + Environment binding and AuthProfile remains isolated by default; sharing must be explicit and auditable, and no secret or token may cross service boundaries automatically.

## P05 closure evidence

- Final integrated implementation SHA: `4d706fab57071236e9847670342f5a399b3527e6`
- Final implementation PR: #22 — normally merged
- Exact-main applicable workflows: 10/10 SUCCESS
- P05 Metadata Catalog: run `34262416559` — SUCCESS
- Exact-main artifact: `P05-Metadata-Evidence-4d706fab57071236e9847670342f5a399b3527e6`
- Artifact size: 507,662 bytes
- Artifact workflow digest: `sha256:4f903fb240b6f0195fa7ad260afac9b45ece0d443e57f0752457d567abf41796`
- Metadata evidence: Entity/Environment/Service/ServiceField/ResultMapping catalog and generic sample proof
- Versioning evidence: previously used definitions create a new version while history remains immutable
- Isolation/security evidence: exactly one UAT and one Production binding per service, HTTPS/certificate enforcement for Production, secret-bearing header rejection, rejected-definition atomicity
- Import/export evidence: schema-governed JSON round trip
- UI evidence: protected bilingual Arabic RTL / English LTR metadata administration
- Regression evidence: P00–P04 closed contracts remain green on the exact P05 implementation SHA
- Owner/external dependency: none deferred for P05

Detailed evidence is recorded in `docs/evidence/P05_METADATA_CATALOG.md`.

## P06 exit condition

P06 may be marked CLOSED only when the Secret Vault and authentication-profile model is implemented with secure SecretRefs, isolated per-Service + Environment bindings, explicit auditable sharing where allowed, rotation/redaction and cross-service-safe token cache semantics, with tests, CI/evidence and exact-main verification complete and pushed without weakening P00–P05 or introducing P07+ scope.
