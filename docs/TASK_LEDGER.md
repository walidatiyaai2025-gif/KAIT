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
| P06 | CLOSED | Secret Vault/AuthProfiles/rotation/redaction/token-cache/admin acceptance preserved; cross-environment sharing is additionally fail-closed by P17 recovery. |
| P07 | CLOSED | Generic metadata-driven execution, auth binding, response bounds, UI and upgrade persistence acceptance preserved. |
| P08 | CLOSED | MOJ x-api-key/token/Bearer runtime and exact scope isolation preserved; evidence is operation/environment specific. |
| P09 | CLOSED | Five canonical MOJ services and official contract/security/UI readiness integrated; credential-bearing live UAT remains external NOT PASS where unavailable. |
| P10 | CLOSED | Request/result history, masking, Own/Department/All scope, pagination filters and export/audit behavior preserved; print is CSP-safe. |
| P11 | CLOSED | Append-only tamper-evident audit, retention/concurrency/monitoring/security/browser evidence preserved. |
| P12 | CLOSED | Protected admin operations, health/diagnostics, independent environment controls and no-revision operational toggles preserved; formal P17 CLOSED authority is phase-monotonic only with exact preserved P12 evidence. |
| P13 | CLOSED | Arabic RTL/English LTR, accessibility and responsive browser parity preserved; Home verifier is phase-monotonic for later candidates. |
| P14 | CLOSED | Security/resilience hardening, IDOR/CSRF/XSS/rate-limit/timeout/retry/concurrency/dependency review preserved. |
| P15 | CLOSED | Windows/IIS installer/package lifecycle, upgrade/repair/uninstall, state preservation, redaction and package hashing preserved. |
| P16 | CLOSED | Full automated acceptance, backup/restore rehearsal, multi-screen bilingual UI, security/runtime and same-candidate release packaging preserved. |
| P17 | CLOSED | Implementation PR #92 head `18bedd6891bfa7812c1da7715f504e7cc233e752` passed **43/43** and integrated as `2723e85e9d63182b1384615400a20ecc8babfe83` with **38/38** push SUCCESS. Closure-transition PR #93 head `7e4fb7264c346f4f5310ad4b44c07fe51a07c151` passed **41/41** and integrated as `72d1ea92e14978b08a1b0ed1727716001ebf1390` with **37/37** push SUCCESS. Formal-closure PR #94 head `6d3aa3008f08b4d8a534bff2a1bd5e81cde8ad1f` passed **41/41**, merged normally with expected-head protection, and produced `4d7e5371a28d37237b32cfc382e748582af01b46`, which passed **37/37** governed push workflows with fresh same-SHA P15/P16/P17 artifacts. |

## P17 implementation/convergence record

Canonical implementation/convergence PR #92 integrated owner-supplied official MOH/CSC/MOE UAT contracts and recovered project-wide cloud-actionable regressions found during convergence. The exact accepted implementation head is `18bedd6891bfa7812c1da7715f504e7cc233e752`; its complete governed pull-request matrix finished **43/43 SUCCESS**. It was merged normally using expected-head protection, producing exact main `2723e85e9d63182b1384615400a20ecc8babfe83`; that main completed **38/38 governed push workflows SUCCESS**.

Recovered and integrated work includes MOH Certificate no-unsupported-API-key transport, exact MOE Basic-auth scope, cross-environment AuthProfile rejection, exact-candidate workflow binding, phase-monotonic closed-phase acceptance, CSP-safe printing, last-effective `Roles.Manage` protection, exact-Git-head package provenance, P16 final-candidate compatibility, deterministic P17 SQL idempotency acceptance and unfiltered P17 PR/main contract acceptance.

## P17 closure-transition record

Canonical closure-transition PR #93 exact head `7e4fb7264c346f4f5310ad4b44c07fe51a07c151` completed **41/41 governed PR workflows SUCCESS**. It merged normally with expected-head protection to exact main `72d1ea92e14978b08a1b0ed1727716001ebf1390`, which completed **37/37 governed push workflows SUCCESS** with no failure at terminal verification.

Exact `72d1ea92...` evidence includes:

- P15 ZIP SHA-256 `472e8eed6e1118bf9da14e54bc26fbfcbc4d457570c99d800fc2465479446ecf`; Setup SHA-256 `c90b835d8f16d82bf71e992c065c9309180222af2b232141943b07c98549cfbc`; artifact `10167797374`, digest `sha256:457a428ef40379f0dce36be9cd48d14b6e70dff208af2175f767d218af5b8b24`, 62,247,061 bytes;
- P16 release ZIP SHA-256 `1761ac167bc40c36a08051ddf07e53f73512b1293531ffb8cc10ca029d637bb0`; Setup SHA-256 `ce39d65319b1c977cf3797789686d6c6b45fb338ec5fb6b4272fcf68641349b1`; release artifact `10167858384`, digest `sha256:746c7805303c8692b9b31d45fab943c8a869d34ea30e4771a6d240850051335d`, 62,247,913 bytes; runtime/UI artifact `10168083331`, digest `sha256:b549a04e6aaa4465355af0eeb32595d6aab0156f088edc0b2b9cb3bf03d22d4c`, 3,482,563 bytes;
- P17 artifact `10167841257`, digest `sha256:552b5935cdcca7de3198291529807f3227cf4f41367942243108a992850fe3d4`, 342 bytes.

These remain closure-transition provenance only after formal-closure source changes.

## P17 formal-closure record

Canonical formal-closure PR #94 exact head `6d3aa3008f08b4d8a534bff2a1bd5e81cde8ad1f` completed **41/41 governed PR workflows SUCCESS**, with failure=0, queued=0 and in-progress=0. Same-head P15/P16/P17 artifacts and archive SHA-256 digests were read back before merge. Reviews and review threads were empty, no conflicting tracker claim displaced the line, and exact base main remained `72d1ea92e14978b08a1b0ed1727716001ebf1390` through the immediate pre-merge gate.

PR #94 merged normally using expected-head protection, producing exact formal-closure integration main `4d7e5371a28d37237b32cfc382e748582af01b46`. That exact main completed **37/37 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0. Exact-main P16 run `34520888811` completed all four jobs including aggregate `p16-gate` SUCCESS.

Fresh exact `4d7e5371...` evidence includes:

- P15 run `34520888888`: package `GSIP-0.1.2-win-x64.zip` SHA-256 `f5bd784d97bae7fef7caa81fa417cc9558ceb667fb735cc7f02a19d8b59755a9`; installer `GSIP-0.1.2-Setup-x64.exe` SHA-256 `995436e25567447a27d4dd079186959b0c337b9e3c5e675d23537a4760873fe4`; artifact `10169748209`, digest `sha256:85829cf800762407d7cfb8f5247ea1f5cdb92e0d5a411ecb09d9c15eafea696d`, 62,248,637 bytes;
- P16 run `34520888811`: release package SHA-256 `163ca0280d63198b974906a001670e09dd47872229d6debac61deb5d634b8f75`; installer SHA-256 `2cbd9c38af344fee1ec61579a2cf08273da8aa7ffba2aed6e45f8a493f201c5f`; release artifact `10169728697`, digest `sha256:dc71cde929e91a51eecd7e2a242ab370198063714450c057bb4ee47bae7cc73d`, 62,248,706 bytes; runtime/UI artifact `10169865209`, digest `sha256:6a5bd3f2073881c806c1a7ee298b98783f3aa1788b7582a115eca96c6337cf1f`, 3,482,090 bytes;
- P17 run `34520888987`: artifact `10169702431`, digest `sha256:7f1f91ec8d741f34c8bb3b46805b718205a89a1711300b1344df188eb3aae100`, 342 bytes.

Both downloaded P15/P16 manifests bind source identity to exact `4d7e5371a28d37237b32cfc382e748582af01b46`. This evidence may not be reused after any later main movement; any governance/evidence reconciliation merge must be validated on its own resulting exact main.

## Branch reconciliation after formal closure

The full branch census covered all returned branch pages. Against exact `4d7e5371...`, the P17 formal-closure, closure-reconciliation, idempotency staging, MOH contract and green-catalog branches all had `ahead_by=0`. The only divergent P17 hotfix was `hotfix/p17-remove-stray-moh-placeholder`; its PR #91 is explicitly **SUPERSEDED — DO NOT MERGE** because replay would remove legitimate newer MOH contract evidence. No lawful unique P17 implementation commit remained outside main.

## Required post-closure convergence

After every formal closure or evidence reconciliation merge, repeat a complete LIVE project sweep. If any cloud-actionable gap exists anywhere in P00-P17 or cross-phase infrastructure, repair it immediately and restart the sweep count.

Final cloud convergence requires **two consecutive full LIVE-state sweeps with zero cloud-actionable gaps** while exact-main governed CI remains terminal green. Required census includes open PRs/issues, active/stale claims, branches with unique legitimate commits, recent merges, workflow outcomes, required-test skips, production TODO/FIXME/stubs, canonical evidence, release/version/artifact SHA-256 identity, owner/deferred classifications and `UNPUSHED_WORK`.

The first terminal-green sweep after PR #94 found stale tracker/canonical governance text, so it did **not** count as a clean sweep. That gap is the reason for this evidence reconciliation line. Any main produced by this reconciliation must receive new same-SHA validation before the clean-sweep count can restart.

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