# P08 Contract / Evidence Reconciliation

## Authority

This document supersedes the stale pre-integration findings from PR #46 and reconciles them against exact integrated implementation `main` SHA `67968a9230dae453b36f48549eda138d7b5401c7`.

## Reconciled findings at original P08 closure

The historical contract audit correctly identified two gaps while P08 work was still in flight:

1. token endpoint path had to be governed metadata rather than a production hard-code or an emitted pseudo-header;
2. the canonical phase required a protected token-generation Test action in administration.

Both gaps were resolved on the integrated baseline:

- the runtime consumes validated non-secret token endpoint metadata, proves a non-default synthetic token path, keeps that path in token-cache identity and does not forward control metadata as a downstream business header;
- `AuthProfileAuthenticationController.TestTokenGeneration` is protected by `ServiceSecrets.Manage` and anti-forgery, probes the exact Service + Environment + AuthProfile binding, and presents only pass/fail state.

PR #44 Auth Scope Isolation is integrated. PR #47 is merged and exact-main verified. The independent acceptance delta from PR #48 was recovered into PR #47 and PR #48 is superseded rather than merged as a duplicate. PR #45 runtime content is likewise represented in the converged main.

## Historical evidence status

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

## External/unknown fact discipline at original closure

Exact operation-level applicability of `ApiKeyAuth` to `/genToken` was `DEFERRED_EXTERNAL`, not PASS, because authenticated operation evidence or authorized UAT evidence had not yet been supplied. That classification is preserved as historical truth.

## Post-closure reconciliation — 2026-09-09

New owner-supplied authenticated official CAIT Swagger evidence and an owner-executed successful UAT token call now prove the Marriage Cases UAT composite contract: `x-api-key` is required on `/genToken` together with form username/password, and the target requires the same API key plus the acquired Bearer token.

This is a **closed-baseline regression repair**, not a retroactive rewrite of P08 closure and not a new P09 payload implementation. PR #50 extends only the canonical runtime/Test Authentication path and adds synthetic executable acceptance.

Observed UAT status: **UAT_PROVEN**.

Production status remains **PRODUCTION_DEFERRED_EXTERNAL — NOT PASS** beyond the proven gateway prefix. No Production path suffix or operation-level auth/payload applicability is inferred.

The authoritative additive evidence is `docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md`.
