# P17 Final Convergence Evidence

## Scope

This document records repository/cloud evidence for P17 final convergence. It does not convert owner-controlled or external acceptance into PASS. It also does not authorize execution to stop merely because P17 becomes CLOSED: project-wide convergence continues until two consecutive full LIVE sweeps find zero cloud-actionable gaps while exact-main CI remains green.

## Implementation integration

- Repository: `walidatiyaai2025-gif/KAIT`
- Implementation/convergence PR: #92
- Exact accepted implementation head: `18bedd6891bfa7812c1da7715f504e7cc233e752`
- Exact-head governed pull-request workflows: **43/43 SUCCESS**
- Normal merge result: exact implementation main `2723e85e9d63182b1384615400a20ecc8babfe83`
- Exact implementation-main governed push workflows: **38/38 SUCCESS**
- Open PRs immediately after implementation merge: none
- Open issues immediately after implementation merge: issue #1 tracker only
- Main protection: `protected=false`; preserved as `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS`

## Integrated P17 / project-wide repairs

The exact implementation candidate includes all recovered lawful cloud-actionable work discovered during convergence:

1. Owner-supplied official MOH, CSC and MOE UAT contract materialization with independent disabled/fail-closed Production configuration.
2. MOH Certificate Information does not infer operation-level `x-api-key` without provider evidence; persistence acceptance proves the unsupported metadata is absent.
3. MOE Basic credentials are transformed only inside authorized scope, protected carriers are stripped before transport, and fallback is limited to the three exact proven HTTPS UAT Student API operations.
4. Explicit AuthProfile sharing is environment-isolated. UAT→Production sharing is rejected; valid UAT→UAT sharing remains supported and is covered by P17 LocalDB acceptance.
5. Governed workflows use exact `CANDIDATE_SHA` checkout/artifact identity instead of relying on synthetic PR merge SHA behavior.
6. Closed-phase P04/P05/P13 verifiers are phase-monotonic; no runtime monkey-patching or stale literal phase label is required.
7. P10 print export uses CSP-compatible same-origin static script behavior.
8. P04 administration rejects removal of the final effective `Roles.Manage` grant/assignment.
9. P15 package provenance is derived from actual checked-out Git HEAD and checked against candidate identity.
10. P16 full acceptance and release evidence are compatible with lawful P17 OPEN/CLOSURE-TRANSITION-CANDIDATE/CLOSED authority and execute on later final candidates.
11. P17 OfficialAgency/MOE idempotency checks compare one in-memory .NET Guid ordering while preserving row-count and exact-ID equality.
12. P17 Official Agency Contracts runs on every PR and main push, preventing a governance-only closure candidate from bypassing P17 acceptance.

## Closure-transition regression recovery

The P17 closure line deliberately runs every governed prior-phase gate so governance edits cannot erase accepted phase evidence.

- Initial closure head `6914ac2a7e3bb2dea526c56dfaa54512a7d707fa` exposed lossy P13/P15 provenance consolidation. No runtime defect was hidden; the affected static gates failed.
- Recovery head `36a825d902b6c2eeb69c4f65168043cba173ef9b` restored exact P13/P14/P15 provenance. P15 package/lifecycle acceptance became fully green, while P12 static acceptance then exposed missing verified-later-phase authority semantics; the P12 protected runtime/browser acceptance remained healthy.
- Recovery head `40005148c10e75d1ec010438490b68a34d7bae92` restored `CLOSURE TRANSITION CANDIDATE`, `IMPLEMENTATION LOCKED`, normal-integration and exact-new-main semantics, but a stale P12 SHA pair in `PROJECT_CONTROL.md` still prevented exact P12 closure verification.
- The authoritative P12 verifier/evidence identities are final implementation head `f367ca78fca217e9d5c7da0a1328dca047940390` and integrated main `1ed40707552f62058980b44d6cb1e7251dbeb2d4`, with **35/35 exact-head workflows SUCCESS** and **31/31 push workflows SUCCESS**. Those exact values are now preserved in project control.
- P16 acceptance previously recognized P17 only as OPEN or CLOSED, creating an impossible pre-merge combination with P12's strict closure-transition semantics. The verifier now has an explicit, fail-closed P17 closure-transition authority path. It requires P17 phase identity, exact `CLOSURE TRANSITION CANDIDATE` status, P16 formally CLOSED, `IMPLEMENTATION LOCKED`, normal-integration and exact-new-main semantics, exact P16/P17 ledger states, and matching project-control state. OPEN and CLOSED paths remain unchanged; no runtime/security/release assertion was removed.

Any superseded closure-head green evidence is provenance only. The current exact closure head must independently pass the complete governed matrix before merge.

## Security / quality sweep on implementation main

From exact main `2723e85e9d63182b1384615400a20ecc8babfe83`:

- full governed push matrix: 38/38 SUCCESS;
- no open product PR remained;
- issue #1 was reconciled to the merged implementation state;
- code searches found no actionable production `TODO`, `FIXME`, `NotImplementedException`, `Assert.Inconclusive`, explicit `Skip =`, or `Assert.Ignore` result in the inspected default-main scope;
- historical P17 placeholder-removal branch remains intentionally superseded because merging it would delete legitimate later owner-supplied MOH evidence;
- P17 idempotency staging branch is an ancestor of the canonical integrated line and contains no newer unique work;
- the canonical P17 implementation branch is an ancestor of exact implementation main;
- sampled recent recovery/feature branches were either ancestors of implementation main or documented superseded divergence, with no current open PR/claim requiring replay;
- GitHub releases: none;
- Git tag refs: none;
- no final-criteria rule requires inventing a release/tag when exact Actions artifact location, size and SHA-256 evidence exists.

This sweep found the cloud-actionable P17 closure-governance gap, so it does not count toward the required two final zero-gap sweeps.

## Exact implementation-candidate release evidence

Candidate source: `18bedd6891bfa7812c1da7715f504e7cc233e752`  
Version: `0.1.2`  
Runtime: `win-x64`  
Toolchain: .NET SDK `10.0.400`, `net10.0`

Package evidence:

- `GSIP-0.1.2-win-x64.zip`
  - SHA-256: `8245aa3e64c22b15b1f230f556c15366b3a9dfc5b4040f590fa20ad29b9a0200`
- `GSIP-0.1.2-Setup-x64.exe`
  - SHA-256: `a8101b5c8ce02e8992979727977ff704a326aaead1adff37eac7216a475c1bc8`
- P15 Actions artifact
  - ID: `10165281409`
  - name: `P15-Installer-18bedd6891bfa7812c1da7715f504e7cc233e752`
  - size: `62,248,347` bytes
  - archive digest: `sha256:0757f55a4d9db46fca1750a4f0bacef0be45a7c06f82a716b891626361b0e40c`
- P16 Actions artifact
  - ID: `10165407817`
  - name: `P16-Release-Candidate-18bedd6891bfa7812c1da7715f504e7cc233e752`
  - size: `62,249,003` bytes
  - archive digest: `sha256:404c7101d464a0dcb5b0229b557f1e7bafa13e282c432031299a7bbb785695f3`
- P17 Actions artifact
  - ID: `10165274081`
  - name: `P17-Official-Agency-Contracts-18bedd6891bfa7812c1da7715f504e7cc233e752`
  - size: `343` bytes
  - archive digest: `sha256:c29085f6d1310b6dd4350a1fbc3c7a4457b987b17bcb52e829ee6b4577ae8e58`

The P16 package workflow on that exact SHA reported build success with 0 warnings/0 errors and emitted both package hashes above. It also preserved P15 installer static acceptance, version baseline `0.1.2`, exact source identity, and installer log redaction acceptance.

These implementation artifacts are provenance only once closure-source changes exist. Fresh P15/P16/P17 artifacts and internal package/installer SHA-256 values must be read back from the exact closure candidate and resulting exact final main.

## Closure candidate gates

This document is part of `worker/p17-closure-reconciliation`, based exactly on implementation main `2723e85e9d63182b1384615400a20ecc8babfe83` after its 38/38 terminal-green push matrix.

The closure branch is not final merely because its ledger marks P17 as a closure candidate. Before merge it must satisfy all of the following:

- exact closure head known and unchanged;
- complete governed PR workflow matrix terminal SUCCESS;
- P12 admin operations static/runtime acceptance reruns successfully;
- P13 bilingual UI/accessibility acceptance reruns successfully;
- P15 package lifecycle acceptance reruns on that exact head;
- P16 full automated acceptance/release candidate reruns on that exact head;
- P17 Official Agency Contracts reruns on that exact head;
- fresh exact-head P15/P16/P17 artifact identities/hashes read back;
- `main` remains at the expected base or the branch is lawfully reconciled;
- no unresolved review thread/change request/current conflicting claim;
- expected-head protected normal merge only.

After merge, resulting exact-new-main must run the complete governed push matrix to terminal SUCCESS. Same-SHA final-main package/artifact/hash evidence must be read back before final handoff.

## Post-closure two-sweep gate

After exact closure main is terminal green and canonical P17 status is CLOSED, perform a complete LIVE-state project sweep covering at least:

- exact repo/main identity and main CI;
- open PRs/issues;
- latest claims/leases/READY/IN_PROGRESS work;
- all branch lines that may contain legitimate unique commits;
- recent merges/integration state;
- production TODO/FIXME/stubs;
- unexplained required-test skips;
- canonical governance/evidence consistency;
- release/version/artifact filenames, sizes and SHA-256 identities;
- owner/external classifications;
- `UNPUSHED_WORK`.

If a gap is found, repair it immediately and restart the consecutive clean-sweep count after the resulting merge/main verification. Only two consecutive zero-gap sweeps on an unchanged terminal-green exact main satisfy cloud convergence.

## OWNER_LAST / deferred external — NOT PASS

These remain explicitly NOT PASS:

1. `DEFERRED_EXTERNAL_NOT_PASS`: credential-bearing authorized UAT smoke requires owner-controlled credentials/SecretRefs and approved non-destructive test data. Owner action: provision through protected administration, verify exact service/environment/profile scope with no Production fallback, execute the approved minimal UAT request, retain only sanitized evidence.
2. `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`: official Production operation/authentication/reachability/authorized test evidence is unavailable. Owner action: obtain authoritative Production evidence, configure independently, and execute minimum authorized non-destructive acceptance; never infer UAT behavior.
3. `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS`: real target Windows Server/IIS/TLS/DNS/network lifecycle acceptance requires owner-controlled target/certificate. Owner action: install the exact accepted artifact and retain sanitized host/site/binding/health evidence.
4. `OWNER_LAST_SIGNING_NOT_PASS`: private signing material must remain outside Git. Owner action: sign with the approved owner-controlled key/certificate and retain sanitized signature evidence.
5. `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS`: live main remains unprotected. Owner/admin action: apply the documented PR-only/provider-bound required-check policy with strict/up-to-date checks and safe force-push/deletion settings, then independently read back the applied policy.

None of these is promoted to PASS by P17 repository/cloud closure.