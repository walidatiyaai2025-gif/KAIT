# CURRENT_PHASE.md

## Canonical phase authority

**P17 — Final convergence and release closure**

Status on this closure candidate: **CLOSED**

This file is the proposed P17 closure transition. It becomes canonical only after this exact branch head passes the complete governed pull-request workflow matrix, is normally merged with expected-head protection, and the resulting exact `main` passes the complete governed push workflow matrix. Until those gates pass, canonical `main` remains the authority and no final-completion claim is permitted.

## P17 implementation convergence

Canonical implementation/convergence PR: **#92**.  
Exact accepted implementation head: `18bedd6891bfa7812c1da7715f504e7cc233e752` — **43/43 governed pull-request workflows SUCCESS**.  
Normal implementation merge main: `2723e85e9d63182b1384615400a20ecc8babfe83` — **38/38 governed push workflows SUCCESS**, with no failure, queued, in-progress, or null-conclusion run at terminal verification.

The implementation line recovered and integrated the owner-supplied MOH/CSC/MOE UAT contracts and all project-wide cloud-actionable regressions found during P17 convergence, including exact-candidate workflow binding, MOH Certificate API-key evidence correction, MOE Basic-auth scope hardening, cross-environment AuthProfile sharing rejection, P04/P05/P13 phase-monotonic regression acceptance, P10 CSP-safe print behavior, P04 last-effective-`Roles.Manage` protection, P15 package provenance binding, P16 final-candidate compatibility, and deterministic P17 SQL idempotency acceptance.

## Exact accepted implementation-candidate release evidence

Version: `0.1.2`  
Target runtime: `win-x64`  
Framework/toolchain: `net10.0` / .NET SDK `10.0.400`

On exact candidate `18bedd6891bfa7812c1da7715f504e7cc233e752`:

- `GSIP-0.1.2-win-x64.zip` — SHA-256 `8245aa3e64c22b15b1f230f556c15366b3a9dfc5b4040f590fa20ad29b9a0200`;
- `GSIP-0.1.2-Setup-x64.exe` — SHA-256 `a8101b5c8ce02e8992979727977ff704a326aaead1adff37eac7216a475c1bc8`;
- P15 Actions artifact `10165281409` — `P15-Installer-18bedd6891bfa7812c1da7715f504e7cc233e752`, archive digest `sha256:0757f55a4d9db46fca1750a4f0bacef0be45a7c06f82a716b891626361b0e40c`, size 62,248,347 bytes;
- P16 Actions artifact `10165407817` — `P16-Release-Candidate-18bedd6891bfa7812c1da7715f504e7cc233e752`, archive digest `sha256:404c7101d464a0dcb5b0229b557f1e7bafa13e282c432031299a7bbb785695f3`, size 62,249,003 bytes;
- P17 Actions artifact `10165274081` — `P17-Official-Agency-Contracts-18bedd6891bfa7812c1da7715f504e7cc233e752`, archive digest `sha256:c29085f6d1310b6dd4350a1fbc3c7a4457b987b17bcb52e829ee6b4577ae8e58`, size 343 bytes.

The closure candidate and resulting final `main` must generate fresh same-SHA P15/P16/P17 evidence. Historical hashes above establish the accepted implementation source but may not be substituted for exact closure/final-main evidence.

## Preserved closed baselines

P00 through P16 remain formally **CLOSED** from their previously recorded exact integrated evidence in `docs/TASK_LEDGER.md` and phase evidence documents. A closed phase is not immune from regression: any newly discovered project-wide cloud-actionable defect has priority and must be repaired even after P17 is closed.

## Project-wide post-closure rule

P17 closure is **not** permission to stop execution. After any P17 closure merge, workers must perform a full LIVE-state project sweep and immediately repair the highest-priority cloud-actionable gap found anywhere in the project. After every fix or merge, repeat the sweep.

Final cloud convergence requires **two consecutive full LIVE-state sweeps** that both find zero cloud-actionable gaps while exact `main` governed CI remains terminal SUCCESS. Each sweep must verify at minimum:

- exact repository and exact `main`;
- no exact-main regression;
- no lawful merge-ready PR left open;
- no legitimate stale READY/IN_PROGRESS work unattended;
- no unreviewed branch with legitimate unique commits;
- no merged implementation with stale canonical evidence;
- no actionable production TODO/FIXME/stub;
- no unexplained skipped required test;
- `CURRENT_PHASE.md`, `PROJECT_CONTROL.md`, `docs/TASK_LEDGER.md`, and issue #1 reconciled;
- release/version/artifact/SHA-256 evidence reconciled where applicable;
- all required exact-main workflows terminal SUCCESS;
- `UNPUSHED_WORK=NONE`.

Only after both sweeps remain clean may `REMAINING_CLOUD_ACTIONABLE_WORK=0` be recorded. `VERIFIED_FINAL_COMPLETE` must still respect `docs/FINAL_ACCEPTANCE_CRITERIA.md` and may not convert external/owner-only NOT-PASS evidence into PASS.

## OWNER_LAST / deferred external boundaries — NOT PASS

The following remain explicit **NOT PASS** and are not fabricated as completed cloud evidence:

- `DEFERRED_EXTERNAL_NOT_PASS` — authorized credential-bearing UAT smoke where owner-controlled credentials/SecretRefs and approved non-destructive test data are unavailable. Owner action: provision secrets only through protected administration, verify exact Service + UAT Environment + AuthProfile/SecretRef scope with no Production fallback, execute the approved minimal smoke, and retain only sanitized correlation/outcome evidence.
- `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` — authoritative Production operation/authentication evidence and authorized Production reachability/test authorization remain unavailable. Owner action: obtain official Production evidence, configure Production independently, and execute only authorized minimal non-destructive acceptance; never infer/copy UAT settings.
- `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS` — target Windows Server/IIS/TLS/DNS/network acceptance requires the authorized real host and approved certificate. Owner action: install the exact accepted artifact and retain sanitized deployment/health/lifecycle evidence.
- `OWNER_LAST_SIGNING_NOT_PASS` — owner-controlled code-signing material remains outside Git. Owner action: sign with the approved owner-controlled key/certificate and retain sanitized signature evidence.
- `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS` — live `main` read-back remains `protected=false` with required-check enforcement off. Owner/admin action: apply the documented PR-only/provider-bound protection policy and independently read it back.

These boundaries do not reopen cloud implementation by themselves, but none is PASS.