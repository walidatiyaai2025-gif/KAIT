# CURRENT_PHASE.md

## Canonical current phase

**P01 — Solution architecture and bilingual shell**

Status: **OPEN / READY**

P00 is CLOSED from integrated exact-main evidence. The P00 implementation was normally merged at `8156c46ce8ce366424d955a6194d677c2da5055f`; on that exact SHA, Planning Integrity run `34210899288` and P00 Build Baseline run `34210899267` succeeded, including pinned SDK verification, restore, Release build, executable baseline checks, publish, package/hash generation and artifact upload.

This P01 transition becomes authoritative only after this closure change is integrated to `main` and the resulting exact-main CI is green. Do not begin P01 implementation from an unmerged transition branch.

## Legal work now

After the transition is integrated and exact-main verification is green, P01 only, plus any repair needed to keep the P00 baseline and repository controls valid.

P01 must:

- expand the solution into the canonical layered architecture (`GSIP.Web`, `GSIP.Application`, `GSIP.Domain`, `GSIP.Infrastructure`, `GSIP.Integrations`, `GSIP.Contracts`) plus appropriate test projects;
- establish dependency direction, DI/Options patterns and infrastructure abstractions without implementing future-phase business features;
- implement the bilingual Arabic/English application shell with real RTL/LTR behavior;
- implement the v1 design system from the mandatory repository-native SVG baselines under `docs/ui-baseline/` (navy/blue/gold, header, sidebar, cards, forms/tables/states as applicable to the shell);
- add only the landing/login shell required by P01; Setup, identity, RBAC and service execution remain future-phase work;
- create `docs/DESIGN_DECISIONS.md` and record any justified deviation from the baselines;
- add unit/smoke/browser checks and Arabic/English screenshot/design-parity evidence appropriate to P01;
- build/test/repair/retest, push/PR/merge, then verify the exact resulting `main` before closing P01.

## Locked future work

P02–P17 remain locked. In particular, do not implement Setup Wizard, Identity, RBAC, metadata catalog, Secret Vault, integration execution, MOJ services, history/audit, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding architecture input even when its runtime implementation belongs to later phases.

## P00 closure evidence

- Integrated implementation SHA: `8156c46ce8ce366424d955a6194d677c2da5055f`
- Planning Integrity: run `34210899288` — SUCCESS
- P00 Build Baseline: run `34210899267` — SUCCESS
- Exact-main artifact: `GSIP-P00-Baseline-0.1.0-8156c46ce8ce.zip`
- Artifact workflow digest: `sha256:455be98b93727c20646193da3682f92a20bbc9d373881ea6b3b4f443d967a731`
- Owner/external dependency: none

## P01 exit condition

P01 may be marked CLOSED only when the layered solution, bilingual shell, design system, P01 tests/browser evidence, design decisions, CI and exact-main verification are complete and pushed on the same reconciled implementation state.
