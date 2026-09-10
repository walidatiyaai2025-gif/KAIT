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

P00-P17 are formally CLOSED from repository/cloud implementation. P17 is formally **CLOSED**. This does not terminate project-wide convergence: any later cloud-actionable regression, stale integration/evidence, unique legitimate branch work, required-test gap, release mismatch, or governance drift must be repaired immediately.

P17 implementation/convergence was integrated through PR #92:

- exact implementation head `18bedd6891bfa7812c1da7715f504e7cc233e752` — **43/43 governed PR workflows SUCCESS**;
- exact implementation main `2723e85e9d63182b1384615400a20ecc8babfe83` — **38/38 governed push workflows SUCCESS**.

P17 closure transition was integrated through PR #93:

- exact closure-transition head `7e4fb7264c346f4f5310ad4b44c07fe51a07c151` — **41/41 governed PR workflows SUCCESS**;
- normal expected-head-protected merge produced exact main `72d1ea92e14978b08a1b0ed1727716001ebf1390` — **37/37 governed push workflows SUCCESS**;
- default-main static sweep found no actionable TODO, FIXME, NotImplementedException, Assert.Inconclusive, explicit `Skip =`, or Assert.Ignore finding in the inspected repository scope;
- release/tag inventory contained no GitHub releases and no Git tag refs; repository final acceptance requires exact artifact identity/hash evidence, not a fabricated release/tag.

P17 formal closure was integrated through PR #94:

- exact formal-closure head `6d3aa3008f08b4d8a534bff2a1bd5e81cde8ad1f` — **41/41 governed PR workflows SUCCESS** with failure=0, queued=0 and in-progress=0 at terminal verification;
- fresh same-head P15/P16/P17 artifact identities and SHA-256 archive digests were read back before merge;
- no review, unresolved review thread, competing claim or open competing PR displaced the canonical line;
- PR #94 merged normally using `expected_head_sha=6d3aa3008f08b4d8a534bff2a1bd5e81cde8ad1f`;
- the merge produced exact formal-closure integration main `4d7e5371a28d37237b32cfc382e748582af01b46`;
- that exact main completed **38/38 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0;
- exact-main P16 run `34520888811` completed static contract, release candidate packaging, runtime/UI recovery and aggregate `p16-gate` SUCCESS;
- branch reconciliation found no lawful unique P17 work outside main: formal/closure/idempotency/MOH/green branches were `ahead_by=0`; `hotfix/p17-remove-stray-moh-placeholder` is PR #91 and is explicitly **SUPERSEDED — DO NOT MERGE** because replay would delete legitimate newer MOH evidence.

Post-formal-closure evidence reconciliation was integrated through PR #95:

- exact reconciliation head `75c85e21f9c06943cd2bb31724f8fa1924c27896` — **40/40 governed PR workflows SUCCESS**;
- PR #95 merged normally with expected-head protection and produced exact main `cbbcd4eaadb98d61b65e040c048e57b2c0f50e47`;
- exact `cbbcd4eaadb98d61b65e040c048e57b2c0f50e47` completed **38/38 governed push workflows SUCCESS**, failure=0;
- fresh P15/P16/P17 exact-main artifacts and internal release SHA-256 values were generated and verified from that exact source;
- P17 remains CLOSED; no owner-only or deferred-external item is promoted to PASS.

`cbbcd4eaadb98d61b65e040c048e57b2c0f50e47` is the latest fully verified exact-main predecessor recorded by this evidence-only reconciliation. The merge SHA eventually produced by this reconciliation line is intentionally not self-encoded here: live repository state and issue #1 post-merge evidence are authoritative for any resulting newer main, which must receive its own exact-main CI and artifact validation.

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

This `72d1ea92...` evidence is retained only as closure-transition provenance after the formal-closure source changed governance and a regression verifier.

## Exact formal-closure integration artifact identity

Version `0.1.2`, exact source `4d7e5371a28d37237b32cfc382e748582af01b46`:

- P15 run `34520888888`; `GSIP-0.1.2-win-x64.zip` SHA-256 `f5bd784d97bae7fef7caa81fa417cc9558ceb667fb735cc7f02a19d8b59755a9`; `GSIP-0.1.2-Setup-x64.exe` SHA-256 `995436e25567447a27d4dd079186959b0c337b9e3c5e675d23537a4760873fe4`; artifact `10169748209`, 62,248,637 bytes, archive digest `sha256:85829cf800762407d7cfb8f5247ea1f5cdb92e0d5a411ecb09d9c15eafea696d`;
- P16 run `34520888811`; release `GSIP-0.1.2-win-x64.zip` SHA-256 `163ca0280d63198b974906a001670e09dd47872229d6debac61deb5d634b8f75`; release `GSIP-0.1.2-Setup-x64.exe` SHA-256 `2cbd9c38af344fee1ec61579a2cf08273da8aa7ffba2aed6e45f8a493f201c5f`; release artifact `10169728697`, 62,248,706 bytes, archive digest `sha256:dc71cde929e91a51eecd7e2a242ab370198063714450c057bb4ee47bae7cc73d`; runtime/UI artifact `10169865209`, 3,482,090 bytes, archive digest `sha256:6a5bd3f2073881c806c1a7ee298b98783f3aa1788b7582a115eca96c6337cf1f`;
- P17 run `34520888987`; artifact `10169702431`, 342 bytes, archive digest `sha256:7f1f91ec8d741f34c8bb3b46805b718205a89a1711300b1344df188eb3aae100`.

The P15 and P16 manifests inside those exact-main artifacts both bind `SourceSha`/`CandidateSha` to `4d7e5371a28d37237b32cfc382e748582af01b46`. These identities are historical evidence for the formal-closure integration main. Any later governance/evidence reconciliation merge creates a new exact main and must be validated independently; green CI or artifacts from `4d7e5371...` cannot be reused as evidence for a newer SHA.

## Exact post-formal-closure reconciliation-main artifact identity

Version `0.1.2`, exact source `cbbcd4eaadb98d61b65e040c048e57b2c0f50e47`:

- P15 run `34525041438`: `GSIP-0.1.2-win-x64.zip` SHA-256 `1620aa6bad7358da5ade77392d995c2315f4bb57bc5f018e022e02860253b2f2`; `GSIP-0.1.2-Setup-x64.exe` SHA-256 `b072319217532a3944d952e8938b7f8a991d97dd3092802538e0fe77f577ddd1`; artifact `10171329005`, 62,248,493 bytes, archive digest `sha256:f09c187422a8fdadeec34ca41d529439dc92a62d1c9389966552942c70743aa0`;
- P16 run `34525040954`: release `GSIP-0.1.2-win-x64.zip` SHA-256 `d19f7adbc3d3388daebca81df40eddbef8d032b901f8383e2a6976bd3f99350b`; release `GSIP-0.1.2-Setup-x64.exe` SHA-256 `32aea16cd60d6b006fdcb873bc6bd748fbec3c9e4c4af59fdd88fa7d5419e2d3`; release artifact `10171336671`, 62,249,137 bytes, archive digest `sha256:dc3a994c9ea26af0de28bf67e9e7214fad6e05ada37fba9cdb80597061736d28`; runtime/UI artifact `10171433048`, 3,482,444 bytes, archive digest `sha256:7816db30ee723129a4792a0b5ea2f7683683be317f8892a5fb1e7060ad15cfdc`;
- P17 run `34525041117`: artifact `10171310053`, 341 bytes, archive digest `sha256:0f6809c018fb4083e20f3b949d65bf61d2c062f39d5ca1ddf0289dccd717c0a2`.

P15 and P16 independently rebuild from the same exact source for different acceptance purposes, so their generated byte hashes are not treated as interchangeable artifacts. The P16 release-candidate artifact is the release-candidate identity for final handoff; P15 is independent installer lifecycle/hash evidence. Both are exact-source-bound and their own SHA-256 verification steps passed.

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

P17 added official MOH/CSC/MOE UAT materialization and recovered project-wide regressions discovered during final convergence without weakening prior boundaries. Product implementation remains locked after formal P17 closure.

## Post-P17 zero-gap requirement

P17 CLOSED is not final execution termination. After every formal-closure or evidence reconciliation merge, perform full LIVE project sweeps until **two consecutive sweeps** find zero cloud-actionable gaps while exact-main governed CI remains terminal green. Each sweep must cover open PRs/issues, claims/leases, all branches with potential unique legitimate work, recent merges, CI, production TODO/FIXME/stubs, required-test skips, canonical documents, release/version/artifact/hash evidence, owner/deferred classifications, and `UNPUSHED_WORK`.

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