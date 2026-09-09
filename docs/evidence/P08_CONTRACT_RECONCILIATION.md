# P08 Contract / Evidence Reconciliation

## Authority

This document supersedes the stale pre-integration findings from PR #46 and reconciles them against exact integrated implementation `main` SHA `67968a9230dae453b36f48549eda138d7b5401c7`.

## Reconciled findings

The historical contract audit correctly identified two gaps while P08 work was still in flight:

1. token endpoint path had to be governed metadata rather than a production hard-code or an emitted pseudo-header;
2. the canonical phase required a protected token-generation Test action in administration.

Both gaps are resolved on the integrated baseline:

- the runtime consumes validated non-secret token endpoint metadata, proves a non-default synthetic token path, keeps that path in token-cache identity and does not forward control metadata as a downstream business header;
- `AuthProfileAuthenticationController.TestTokenGeneration` is protected by `ServiceSecrets.Manage` and anti-forgery, probes the exact Service + Environment + AuthProfile binding, and presents only pass/fail state.

PR #44 Auth Scope Isolation is integrated. PR #47 is merged and exact-main verified. The independent acceptance delta from PR #48 was recovered into PR #47 and PR #48 is superseded rather than merged as a duplicate. PR #45 runtime content is likewise represented in the converged main.

## Evidence status

- Exact integrated implementation main: `67968a9230dae453b36f48549eda138d7b5401c7`.
- Exact-head PR #47 workflow matrix: 22/22 SUCCESS.
- Exact-main push workflow matrix: 17/17 SUCCESS.
- P08 Security Acceptance run `34318032200`: SUCCESS.
- Exact-main security artifact digest: `sha256:b39ea805fec25b5a45a35c2eb445074e64622f48a44dc7c7e3ccd6a0ea8b3d98`.
- MOJ runtime acceptance: PASS.
- Auth Scope Isolation: PASS.
- Administration Test Authentication: PASS.
- Secret/token leakage scan: PASS.
- P09 implementation introduced: NONE.

## External/unknown fact discipline

Exact operation-level applicability of `ApiKeyAuth` to `/genToken` remains `DEFERRED_EXTERNAL`, not PASS, unless authenticated official operation evidence or authorized UAT evidence establishes it. The generic runtime already supports governed composition of the documented authentication mechanisms; therefore no speculative code or second runtime is required.

The exact owner/operator validation procedure is recorded in `docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md`.

## Closure handoff

The phase-closure branch owns only governance, ledger, evidence and closure CI reconciliation. It does not modify MOJ production authentication behavior and does not implement P09 payloads. If the closure gate is green, this evidence authorizes the governance transition P08 CLOSED → P09 OPEN / READY, followed by exact-new-main verification.
