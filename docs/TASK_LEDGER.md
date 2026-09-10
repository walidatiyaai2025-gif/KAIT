# TASK_LEDGER.md — GSIP Canonical Phase Ledger

This ledger is evidence-driven. `DEFERRED_EXTERNAL` and `OWNER_LAST` are never equivalent to PASS. A phase may be marked CLOSED only from implementation/tests/CI/evidence, and later project-wide regressions must still be repaired even if the originating phase is closed.

| Phase | Status | Canonical closure / current evidence |
|---|---|---|
| P00 | CLOSED | Baseline/build/package and Planning Integrity established and preserved by later full matrices. |
| P01 | CLOSED | Architecture, bilingual shell and runtime/browser acceptance established and preserved. |
| P02 | CLOSED | First-run Setup plus later SQL Authentication regression repair integrated and preserved. |
| P03 | CLOSED | Identity, MFA, session/account security, lockout/rate-limit and security-header acceptance preserved. |
| P04 | CLOSED | RBAC, service-level Default Deny, admin/IDOR protection and last-effective `Roles.Manage` protection preserved. |
| P05 | CLOSED | Metadata catalog/versioning/import-export and independent UAT/Production configuration preserved. |
| P06 | CLOSED | Secret Vault/AuthProfiles/rotation/redaction/token-cache/admin acceptance preserved; cross-environment sharing is now additionally fail-closed by P17 recovery. |
| P07 | CLOSED | Generic metadata-driven execution, auth binding, response bounds, UI and upgrade persistence acceptance preserved. |
| P08 | CLOSED | MOJ x-api-key/token/Bearer runtime and exact scope isolation preserved; evidence is operation/environment specific. |
| P09 | CLOSED | Five canonical MOJ services and official contract/security/UI readiness integrated; credential-bearing live UAT remains external NOT PASS where unavailable. |
| P10 | CLOSED | Request/result history, masking, Own/Department/All scope, pagination filters and export/audit behavior preserved; print is CSP-safe. |
| P11 | CLOSED | Append-only tamper-evident audit, retention/concurrency/monitoring/security/browser evidence preserved. |
| P12 | CLOSED | Protected admin operations, health/diagnostics, independent environment controls and no-revision operational toggles preserved. |
| P13 | CLOSED | Arabic RTL/English LTR, accessibility and responsive browser parity preserved; Home verifier is phase-monotonic for later candidates. |
| P14 | CLOSED | Security/resilience hardening, IDOR/CSRF/XSS/rate-limit/timeout/retry/concurrency/dependency review preserved. |
| P15 | CLOSED | Windows/IIS installer/package lifecycle, upgrade/repair/uninstall, state preservation, redaction and package hashing preserved. |
| P16 | CLOSED | Full automated acceptance, backup/restore rehearsal, multi-screen bilingual UI, security/runtime and same-candidate release packaging preserved. |
| P17 | CLOSED — CLOSURE CANDIDATE | PR #92 exact head `18bedd6891bfa7812c1da7715f504e7cc233e752` passed **43/43** governed PR workflows; normal merge produced exact implementation main `2723e85e9d63182b1384615400a20ecc8babfe83`, which passed **38/38** governed push workflows. This closure transition must itself pass exact-head CI, merge normally with expected-head protection, and produce terminal-green exact-new-main CI before P17 closure is canonical. Project-wide zero-gap sweeps continue after P17 CLOSED. |

## P17 implementation/convergence record

Canonical implementation/convergence PR #92 integrated owner-supplied official MOH/CSC/MOE UAT contracts and recovered project-wide cloud-actionable regressions found during final convergence. The exact accepted implementation head is `18bedd6891bfa7812c1da7715f504e7cc233e752`; its complete governed pull-request matrix finished **43/43 SUCCESS**. It was merged normally using expected-head protection, producing exact main `2723e85e9d63182b1384615400a20ecc8babfe83`; that main completed **38/38 governed push workflows SUCCESS** with no failure/queued/in-progress/null-conclusion run at terminal verification.

Recovered and integrated work includes:

- MOH Certificate contract no longer invents unsupported operation-level API-key transport;
- MOE Basic authentication is constrained to exact proven HTTPS UAT operations and protected carriers fail closed outside authorized scope;
- AuthProfile sharing rejects UAT→Production cross-environment targets while same-environment explicit sharing remains supported;
- legacy governed workflows bind exact candidate SHA/artifact identity rather than synthetic PR merge SHAs;
- P04/P05/P13 closed-phase verifiers are phase-monotonic instead of requiring obsolete phase labels or runtime monkey-patching;
- P10 print export uses CSP-compatible same-origin script behavior;
- P04 prevents removal of the final effective `Roles.Manage` grant/assignment;
- P15 package provenance binds to actual Git HEAD and candidate identity;
- P16 release evidence is neutral/final-candidate compatible and full acceptance runs on later P17 candidates;
- P17 OfficialAgency/MOE persistence acceptance uses deterministic in-memory Guid ordering while preserving exact row-count/ID equality;
- P17 acceptance is unfiltered for PR and main-push candidates so a governance-only closure cannot bypass the contract gate.

## P17 exact implementation-candidate artifacts

Version `0.1.2`, source `18bedd6891bfa7812c1da7715f504e7cc233e752`:

- `GSIP-0.1.2-win-x64.zip` SHA-256 `8245aa3e64c22b15b1f230f556c15366b3a9dfc5b4040f590fa20ad29b9a0200`;
- `GSIP-0.1.2-Setup-x64.exe` SHA-256 `a8101b5c8ce02e8992979727977ff704a326aaead1adff37eac7216a475c1bc8`;
- P15 artifact id `10165281409`, archive digest `sha256:0757f55a4d9db46fca1750a4f0bacef0be45a7c06f82a716b891626361b0e40c`, 62,248,347 bytes;
- P16 artifact id `10165407817`, archive digest `sha256:404c7101d464a0dcb5b0229b557f1e7bafa13e282c432031299a7bbb785695f3`, 62,249,003 bytes;
- P17 artifact id `10165274081`, archive digest `sha256:c29085f6d1310b6dd4350a1fbc3c7a4457b987b17bcb52e829ee6b4577ae8e58`, 343 bytes.

These hashes are accepted implementation provenance. The closure candidate and resulting closure main must generate/read back fresh same-SHA evidence; old candidate artifacts cannot substitute for the exact final integrated commit required by `docs/FINAL_ACCEPTANCE_CRITERIA.md`.

## First post-implementation project-wide sweep

The sweep performed from exact implementation main `2723e85e9d63182b1384615400a20ecc8babfe83` found:

- exact-main governed push CI: **38/38 SUCCESS**;
- open PRs: none after #92 merge;
- open issues: issue #1 tracker only;
- repository/default-main code search: no actionable `TODO`, `FIXME`, `NotImplementedException`, `Assert.Inconclusive`, explicit `Skip =`, or `Assert.Ignore` finding in the inspected scope;
- branch review: current P17 implementation line merged; P17 ordering staging is older than canonical implementation; prior P17 placeholder-removal branch is intentionally superseded because applying it would delete legitimate owner-supplied MOH evidence; earlier worker/hotfix branches are historical integrated/superseded closed-phase lines with no current open PR/claim requiring replay;
- releases: none; Git tag refs: none. Neither is required by final criteria when exact Actions artifact location/size/hash evidence is provided;
- canonical governance gap identified: P17 closure documents were still OPEN and therefore this closure transition was required.

This is **not** one of the two final zero-gap sweeps because the closure-governance gap existed and was repaired by this transition.

## Required post-closure convergence

After this closure transition is merged and exact-new-main CI is terminal green, repeat a complete LIVE project sweep. If any cloud-actionable gap exists anywhere in P00-P17 or cross-phase infrastructure, immediately repair the highest-priority gap, merge lawfully, verify exact-main CI, and restart the sweep count.

Final cloud convergence requires **two consecutive full LIVE-state sweeps with zero cloud-actionable gaps** while exact-main governed CI remains terminal green. Required census includes open PRs/issues, active/stale claims, branches with unique legitimate commits, recent merges, workflow outcomes, required-test skips, production TODO/FIXME/stubs, canonical evidence, release/version/artifact SHA-256 identity, and `UNPUSHED_WORK`.

## OWNER_LAST / deferred external — NOT PASS

The following remain NOT PASS and are never converted to PASS by repository closure:

- `DEFERRED_EXTERNAL_NOT_PASS` — authorized credential-bearing UAT smoke requiring owner-controlled credentials/approved test data.
- `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` — authoritative Production operation/auth/reachability/authorized test evidence unavailable; Production stays independently fail-closed.
- `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS` — actual target Windows Server/IIS/TLS/DNS/network verification needs the owner-controlled target/certificate.
- `OWNER_LAST_SIGNING_NOT_PASS` — code signing requires owner-controlled key/certificate material outside Git.
- `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS` — latest live main read-back remains `protected=false` with required-check enforcement off; authorized admin configuration and independent read-back remain required.

## Non-negotiable ledger rules

- **LIVE STATE FIRST** before any execution or claim.
- **RECOVER BEFORE CREATE** to prevent duplicate work.
- **TEST / REPAIR / RETEST**: exact-main regressions and failed required tests outrank new work.
- **NO SECRET LEAKAGE**: no credentials, tokens, private keys, Civil IDs, or real personal government data in Git/logs/evidence.
- **SERVICE / ENVIRONMENT ISOLATION**: no implicit cross-service or cross-environment credential/configuration sharing and no Production→UAT fallback.
- **NO FALSE FINALITY**: P17 CLOSED does not end project-wide convergence; `VERIFIED_FINAL_COMPLETE` is forbidden until exact final source/CI/artifacts/hashes/governance agree and two consecutive zero-gap LIVE sweeps succeed.
- **UNPUSHED_WORK=NONE** before any stop.