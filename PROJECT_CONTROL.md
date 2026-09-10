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

## Canonical status proposed by this closure transition

P00 through P16 are **CLOSED** from repository/cloud evidence. P17 is a **CLOSURE TRANSITION CANDIDATE** until this exact closure head is terminal green, normally merged with expected-head protection, and the resulting exact-new-main is terminal green. Closing P17 does not terminate project-wide convergence: any later cloud-actionable regression, stale integration/evidence, unique legitimate branch work, required-test gap, release mismatch, or governance drift must be repaired immediately.

P17 implementation/convergence was integrated through PR #92:

- exact implementation head `18bedd6891bfa7812c1da7715f504e7cc233e752` — **43/43 governed PR workflows SUCCESS**;
- exact implementation main `2723e85e9d63182b1384615400a20ecc8babfe83` — **38/38 governed push workflows SUCCESS**;
- open PRs immediately after implementation merge: none;
- default-main static sweep: no actionable TODO, FIXME, NotImplementedException, Assert.Inconclusive, explicit `Skip =`, or Assert.Ignore finding in the inspected repository scope;
- release/tag inventory at implementation-main sweep: no GitHub releases and no Git tag refs; repository final acceptance requires exact artifact identity/hash evidence, not a fabricated release/tag.

Detailed P17 closure evidence: `docs/evidence/P17_FINAL_CONVERGENCE.md`.

## Preserved P12 exact closure provenance

P12 remains **CLOSED** from exact accepted evidence and that provenance is retained explicitly for later-phase regression acceptance. Final P12 implementation head `32ea4d59d3e25b58eaf19879c5cd752eb4ca3142` was integrated as exact P12 main `951901869dece4771a3ad99f4854ab5f0c7a5be4`. The canonical P12 evidence file preserves the same identities. No P17 closure transition may erase or reinterpret this proven P12 admin operations/health/diagnostics baseline.

## Exact implementation-candidate artifact identity

Version `0.1.2`, exact source `18bedd6891bfa7812c1da7715f504e7cc233e752`:

- `GSIP-0.1.2-win-x64.zip` — SHA-256 `8245aa3e64c22b15b1f230f556c15366b3a9dfc5b4040f590fa20ad29b9a0200`;
- `GSIP-0.1.2-Setup-x64.exe` — SHA-256 `a8101b5c8ce02e8992979727977ff704a326aaead1adff37eac7216a475c1bc8`;
- P15 artifact `10165281409`, archive digest `sha256:0757f55a4d9db46fca1750a4f0bacef0be45a7c06f82a716b891626361b0e40c`;
- P16 artifact `10165407817`, archive digest `sha256:404c7101d464a0dcb5b0229b557f1e7bafa13e282c432031299a7bbb785695f3`;
- P17 artifact `10165274081`, archive digest `sha256:c29085f6d1310b6dd4350a1fbc3c7a4457b987b17bcb52e829ee6b4577ae8e58`.

This implementation-candidate evidence is provenance only after the closure source changes. The closure PR head and resulting final `main` must each pass their governed matrices; same-SHA P15/P16/P17 artifacts/hashes from those final candidates must be read back before final handoff.

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
- Exactly one canonical current phase exists while phase governance is active.
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
- Cross-environment AuthProfile sharing is forbidden; the P17 exact candidate includes executable UAT→Production rejection acceptance while preserving valid same-environment sharing.
- MOE Basic auth is constrained to the exact proven HTTPS UAT Student API operations and protected carrier secrets are stripped before network transport.
- MOH Certificate Information does not infer an operation-level API-key requirement unsupported by provider evidence.

## Product boundary summary

The closed P00-P16 evidence remains authoritative for setup/database safety; identity/MFA/session controls; RBAC and service-level Default Deny; metadata versioning and independent environment configuration; Secret Vault/AuthProfiles/token cache; generic execution; five canonical MOJ services; request history/export; tamper-evident audit; admin health/diagnostics; bilingual accessibility/design parity; security/resilience; Windows/IIS package lifecycle; backup/restore rehearsal; and full automated release-candidate acceptance.

P17 added official MOH/CSC/MOE UAT materialization and recovered project-wide regressions discovered during final convergence without weakening prior boundaries.

## Post-P17 zero-gap requirement

P17 CLOSED is not final execution termination. After closure integration, perform full LIVE project sweeps until **two consecutive sweeps** find zero cloud-actionable gaps while exact-main governed CI remains terminal green. Each sweep must cover open PRs/issues, claims/leases, all branches with potential unique legitimate work, recent merges, CI, production TODO/FIXME/stubs, required-test skips, canonical documents, release/version/artifact/hash evidence, and `UNPUSHED_WORK`.

Only after both sweeps are clean may `REMAINING_CLOUD_ACTIONABLE_WORK=0` be reported.

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