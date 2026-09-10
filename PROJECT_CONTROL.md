# PROJECT_CONTROL.md

## Project identity

- Project: Government Services Integration Portal (GSIP)
- Repository: `walidatiyaai2025-gif/KAIT`
- Canonical branch: `main`
- Delivery model: evidence-driven, phase-gated autonomous implementation with project-wide regression recovery
- Platform: .NET SDK `10.0.400` / `net10.0`
- Runtime/package target: `win-x64`
- Product version: `0.1.2`
- Deployment target: Windows Server / IIS
- Database target: Microsoft SQL Server
- UI languages: Arabic RTL and English LTR

## Formal phase status

P00-P17 are formally CLOSED from repository/cloud implementation and closure evidence proposed by this exact reconciliation line. P17 is formally **CLOSED**. This does not terminate project-wide convergence: any later cloud-actionable regression, stale integration/evidence, unique legitimate branch work, required-test gap, release mismatch, or governance drift must be repaired immediately. This formal-closure line is canonical only after its exact head is terminal green, normally integrated with expected-head protection, and the resulting exact-new-main is terminal green.

P17 implementation/convergence was integrated through PR #92:

- exact implementation head `18bedd6891bfa7812c1da7715f504e7cc233e752` — **43/43 governed PR workflows SUCCESS**;
- exact implementation main `2723e85e9d63182b1384615400a20ecc8babfe83` — **38/38 governed push workflows SUCCESS**.

P17 closure transition was integrated through PR #93:

- exact closure-transition head `7e4fb7264c346f4f5310ad4b44c07fe51a07c151` — **41/41 governed PR workflows SUCCESS**;
- normal expected-head-protected merge produced exact main `72d1ea92e14978b08a1b0ed1727716001ebf1390` — **37/37 governed push workflows SUCCESS**;
- open PRs immediately after #93 merge: none;
- default-main static sweep: no actionable TODO, FIXME, NotImplementedException, Assert.Inconclusive, explicit `Skip =`, or Assert.Ignore finding in the inspected repository scope;
- release/tag inventory: no GitHub releases and no Git tag refs; repository final acceptance requires exact artifact identity/hash evidence, not a fabricated release/tag.

Detailed P17 closure evidence: `docs/evidence/P17_FINAL_CONVERGENCE.md`.

## Preserved P12 exact closure provenance

P12 remains **CLOSED** from exact accepted evidence and that provenance is retained explicitly for later-phase regression acceptance. Final P12 implementation head `f367ca78fca217e9d5c7da0a1328dca047940390` was normally integrated as exact P12 implementation main `1ed40707552f62058980b44d6cb1e7251dbeb2d4`. The canonical P12 evidence file preserves the same identities together with **35/35 exact-head workflows SUCCESS** and **31/31 push workflows SUCCESS**. Formal P17 CLOSED authority is accepted by the P12 static gate only when these exact identities, preserved P12 evidence, P12/P17 CLOSED ledger rows, P16 formal closure and this project-control authority all agree.

## Exact closure-transition-main artifact identity

Version `0.1.2`, exact source `72d1ea92e14978b08a1b0ed1727716001ebf1390`:

- P15 `GSIP-0.1.2-win-x64.zip` SHA-256 `472e8eed6e1118bf9da14e54bc26fbfcbc4d457570c99d800fc2465479446ecf`;
- P15 `GSIP-0.1.2-Setup-x64.exe` SHA-256 `c90b835d8f16d82bf71e992c065c9309180222af2b232141943b07c98549cfbc`;
- P15 artifact `10167797374`, 62,247,061 bytes, archive digest `sha256:457a428ef40379f0dce36be9cd48d14b6e70dff208af2175f767d218af5b8b24`;
- P16 release ZIP SHA-256 `1761ac167bc40c36a08051ddf07e53f73512b1293531ffb8cc10ca029d637bb0`;
- P16 Setup EXE SHA-256 `ce39d65319b1c977cf3797789686d6c6b45fb338ec5fb6b4272fcf68641349b1`;
- P16 release artifact `10167858384`, 62,247,913 bytes, archive digest `sha256:746c7805303c8692b9b31d45fab943c8a869d34ea30e4771a6d240850051335d`;
- P16 runtime/UI artifact `10168083331`, 3,482,563 bytes, archive digest `sha256:b549a04e6aaa4465355af0eeb32595d6aab0156f088edc0b2b9cb3bf03d22d4c`;
- P17 artifact `10167841257`, 342 bytes, archive digest `sha256:552b5935cdcca7de3198291529807f3227cf4f41367942243108a992850fe3d4`.

Because the formal-closure source changes governance and a regression verifier, this `72d1ea92...` evidence is closure-transition provenance only. The exact formal-closure PR head and its resulting exact final main must independently pass their governed matrices and emit fresh same-SHA P15/P16/P17 evidence before final handoff.

## Authoritative source order

When information conflicts, use this order:

1. live repository state and exact `main` evidence;
2. `AGENTS.md`;
3. `CURRENT_PHASE.md`;
4. `docs/TASK_LEDGER.md`;
5. `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`;
6. phase-specific requirements in `execution/GSIP_Full_Execution.json`;
7. `docs/FINAL_ACCEPTANCE_CRITERIA.md`;
8. `docs/UI_DESIGN_PARITY_GATE.md`;
9. `docs/OWNER_LAST_EXECUTION_POLICY.md`;
10. historical plans/evidence.

No stale prompt, old issue comment, branch description, superseded evidence, or historical CI overrides current live evidence.

## Execution and integration policy

- LIVE STATE FIRST on every iteration.
- Recover legitimate active/stale READY work before creating new work.
- Exact-main regressions and stale integration/evidence have priority over new work.
- Do not duplicate implementation already present on main or an active legitimate branch/PR.
- Preserve unrelated legitimate work.
- Merge only after exact-head required CI is terminal SUCCESS and after immediate main/head/review/claim re-read; use expected-head protection.
- After every merge, verify exact-new-main required CI.
- A phase marked CLOSED can still contain a later regression; CLOSED is not a reason to ignore a cloud-actionable gap.
- Before any stop require `UNPUSHED_WORK=NONE`.

## Security and environment invariants

- Never commit credentials, API keys, bearer tokens, private signing keys, Civil IDs, or real personal government data.
- Secret material belongs only in protected Secret Vault/AuthProfile flows and runtime memory as designed.
- Service + Environment + AuthProfile/SecretRef/token scope is fail-closed and isolated by default.
- Production must never fall back to UAT or inherit UAT endpoint/auth configuration by inference.
- Cross-environment AuthProfile sharing is forbidden; P17 executable acceptance rejects UAT→Production sharing while preserving valid same-environment sharing.
- MOE Basic auth is constrained to the exact proven HTTPS UAT Student API operations and protected carrier secrets are stripped before network transport.
- MOH Certificate Information does not infer an operation-level API-key requirement unsupported by provider evidence.

## Product boundary summary

The closed P00-P16 evidence remains authoritative for setup/database safety; identity/MFA/session controls; RBAC and service-level Default Deny; metadata versioning and independent environment configuration; Secret Vault/AuthProfiles/token cache; generic execution; five canonical MOJ services; request history/export; tamper-evident audit; admin health/diagnostics; bilingual accessibility/design parity; security/resilience; Windows/IIS package lifecycle; backup/restore rehearsal; and full automated release-candidate acceptance.

P17 added official MOH/CSC/MOE UAT materialization and recovered project-wide regressions discovered during final convergence without weakening prior boundaries. Product implementation remains locked during formal closure reconciliation.

## Post-P17 zero-gap requirement

P17 CLOSED is not final execution termination. After formal closure integration, perform full LIVE project sweeps until **two consecutive sweeps** find zero cloud-actionable gaps while exact-main governed CI remains terminal green. Each sweep must cover open PRs/issues, claims/leases, all branches with potential unique legitimate work, recent merges, CI, production TODO/FIXME/stubs, required-test skips, canonical documents, release/version/artifact/hash evidence, owner/deferred classifications, and `UNPUSHED_WORK`.

Only after both sweeps are clean may `REMAINING_CLOUD_ACTIONABLE_WORK=0` be reported. `VERIFIED_FINAL_COMPLETE` remains forbidden unless the user's stricter exact-final-main/same-SHA CI/artifact/governance conditions are also proven.

## OWNER_LAST / deferred external — NOT PASS

These remain explicitly NOT PASS and are not converted to PASS by repository/cloud closure:

- `DEFERRED_EXTERNAL_NOT_PASS`: authorized credential-bearing UAT smoke where owner-held credentials/approved test data are unavailable.
- `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`: authoritative Production operation/auth/reachability/test evidence unavailable; Production remains independently fail-closed.
- `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS`: actual target Windows Server/IIS/TLS/DNS/network acceptance requires owner-controlled infrastructure and certificate.
- `OWNER_LAST_SIGNING_NOT_PASS`: signing requires owner-controlled key/certificate material outside Git.
- `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS`: live `main` is still `protected=false` with required-check enforcement off; authorized repository administration plus independent read-back is required.

Precise owner actions are retained in `CURRENT_PHASE.md`, issue #1, and `docs/OWNER_LAST_EXECUTION_POLICY.md`.

## Release control

Final acceptance is a same-commit/same-artifact claim. Source, tests, CI, installer/package, SHA-256, UI/security/database evidence, rollback/handoff evidence, and canonical governance must refer to the exact final candidate. A stale CI result, dirty/unpushed work, mismatched artifact identity, or unreconciled canonical document blocks final completion.