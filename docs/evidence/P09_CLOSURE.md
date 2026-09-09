# P09 Closure Evidence — Five MOJ Services

## Closure state

P09 is CLOSED from the exact integrated baseline `42b3b7af073efe6bd933f473b707333df8924346` after the P09 contract, runtime/UI and independent-acceptance lines were normally integrated and the later P02 Setup Wizard regression repair was also integrated without regressing P09.

Exact-main push verification on `42b3b7af073efe6bd933f473b707333df8924346` completed **29/29 SUCCESS** with failure=0, queued=0 and in-progress=0.

## Integrated P09 chain

- PR #58 — official MOJ contract convergence for APIs 129, 132, 130, 196 and 134, metadata/runtime reconciliation and token-contract variants.
- PR #59 — independent P09 contract/security acceptance.
- PR #60 — P09 execution UI and UAT-readiness convergence.
- PR #61 — closed-baseline P02 SQL Authentication repair; exact-main verification after this repair preserved all P09 gates green.

## Five canonical services

The canonical MOJ Entity contains exactly the P09 services required by the execution plan:

1. Marriage Cases Service (API 129)
2. Is Single Basic Service (API 132)
3. Marriage Couple Last Case Service (API 130)
4. Family Judgment Text Service (API 196)
5. Procuration Status Service (API 134)

Request fields, response mappings, endpoint aliases and authentication metadata are grounded in repository-approved official CAIT/MOJ evidence. No unproven Production suffix or payload contract is inferred. Production and UAT remain isolated and no Production-to-UAT fallback is permitted.

## Exact-main evidence

Representative exact-main runs on `42b3b7af073efe6bd933f473b707333df8924346`:

- P09 Official Contract Snapshots — run `34390738947` — SUCCESS.
- P09 Marriage Cases Service — run `34390739120` — SUCCESS.
- P09 MOJ Token Contract Variants — run `34390739231` — SUCCESS.
- P09 Independent Contract Security Acceptance — run `34390739078` — SUCCESS.
- P09 Execution UI and UAT Readiness — run `34390739312` — SUCCESS.
- The remaining P09 service/metadata workflows and all closed-baseline regression workflows are included in the 29/29 exact-main SUCCESS set.

## Owner-last / external truth

Authorized live UAT execution that requires owner-controlled credentials or personal test records remains **DEFERRED_EXTERNAL_NOT_PASS** where such evidence is unavailable. It is not represented as PASS and it is not required to invent or expose credentials/personal data.

Unproven Production full paths, per-operation details or credentials remain **PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS** and fail closed.

## Exit decision

The P09 exit condition is satisfied for cloud-actionable implementation, security acceptance, contract evidence and exact-main regression verification. P10 may open. This closure does not claim that deferred owner/external UAT evidence passed.
