# P08 MOJ Authentication Contract Matrix

Status: **CLOSURE-RECONCILED**

Evidence baseline: exact `main` implementation SHA `67968a9230dae453b36f48549eda138d7b5401c7`.

This matrix separates facts preserved from official CAIT/MOJ material in this repository from GSIP security controls. Unknown operation-level details are never promoted to facts.

| ID | Requirement | Authority | Integrated implementation / evidence | Closure status |
|---|---|---|---|---|
| AUTH-CONTRACT-01 | UAT and Production use independent environment base URLs/configuration. | Official repository references + GSIP environment contract | P05/P06/P07 environment binding and P08 exact-scope authentication resolution. | PASS |
| AUTH-CONTRACT-02 | MOJ gateway authentication supports the documented `x-api-key` header where applicable. | Official repository references | Generic header/API-key AuthProfile support; secret comes from Secret Vault and is scoped to exact Service + Environment + AuthProfile. | PASS |
| AUTH-CONTRACT-03 | Token generation uses `POST /genToken` with `application/x-www-form-urlencoded` username/password and consumes the documented `data` token response. | Official repository references | P08 metadata-driven token acquisition; token endpoint path is governed metadata, not a production hard-code. Synthetic non-default path proof is included in runtime acceptance. | PASS |
| AUTH-CONTRACT-04 | Bearer authentication is attached to protected MOJ requests where the documented operation requires it. | Official repository references | Token is transiently attached as `Authorization: Bearer ...`; plaintext is not persisted/logged/evidenced. | PASS |
| AUTH-CONTRACT-05 | MOJ service paths referenced by the project include `/business`, `/court`, `/powerOfAttorney`, `/auction`, and `/genToken`. | Official repository references | P08 does not seed P09 service payload contracts. The generic runtime composes governed BaseUrl + RelativePath and authentication metadata. | PASS / P09 payload details remain out of scope |
| AUTH-PLATFORM-01 | No plaintext API key, username, password or Bearer token in metadata, Git, logs or evidence. | GSIP security policy | SecretRef/AuthProfile vault boundary plus independent P08 leakage scan. | PASS |
| AUTH-PLATFORM-02 | Authentication lookup is exact Service + Environment + AuthProfile, with no Production→UAT or cross-service fallback. | GSIP security policy | P08 Auth Scope Isolation acceptance covers forged/stale profiles/refs, cross-service, cross-environment and fallback attempts. | PASS |
| AUTH-PLATFORM-03 | Cached tokens are isolated by governed scope and credential generation/version and cannot reuse stale/wrong-scope material. | GSIP security policy | Canonical P06 token cache reused by P08; P08 negative acceptance covers stale/wrong-scope and refresh/failure safety. | PASS |
| AUTH-PLATFORM-04 | Missing, malformed, empty or invalid authentication configuration fails closed. | GSIP security policy | Runtime and independent P08 negative tests cover missing/invalid auth, token path and token response. | PASS |
| AUTH-PLATFORM-05 | Token acquisition and target execution do not create unbounded retries/auth storms. | GSIP security policy | Independent security acceptance covers bounded retry, target 401 behavior, cancellation and failure atomicity. | PASS |
| AUTH-PLATFORM-06 | Diagnostics/evidence must be secret-safe. | GSIP security policy | Dedicated leakage scan and sanitized evidence artifact on exact main. | PASS |
| AUTH-ADMIN-01 | Administration provides a token-generation authentication Test action without exposing token plaintext. | Canonical P08 execution plan | `AuthProfileAuthenticationController.TestTokenGeneration` is server-authorized, anti-forgery protected and returns only pass/fail presentation state. | PASS |
| AUTH-NOT-ASSUME-01 | Do not assume credentials/AuthProfiles are shared between MOJ services or environments. | Canonical P08 rules | Default isolated bindings; sharing only through explicit governed relationship. | PASS |
| AUTH-NOT-ASSUME-02 | Do not invent OAuth client-id/client-secret/scope/grant conventions absent official evidence. | Evidence discipline | Runtime implements only repository-supported form username/password token contract. | PASS |
| AUTH-NOT-ASSUME-03 | Do not invent token lifetime when the response does not document one. | Evidence discipline | Cache re-use derives expiry from JWT `exp` when present; otherwise token is not given an invented reusable lifetime. | PASS |
| AUTH-NOT-ASSUME-04 | Exact operation-level applicability of `ApiKeyAuth` to `/genToken` must not be guessed when official operation detail is unavailable. | Evidence discipline / owner-last | Runtime can compose the supported mechanisms from governed metadata, but the exact external operation rule remains unasserted. | DEFERRED_EXTERNAL |

## Exact-main acceptance baseline

On exact `main` SHA `67968a9230dae453b36f48549eda138d7b5401c7`, all **17/17** push workflows completed successfully with zero failed, queued or in-progress runs after completion. P08 Security Acceptance run `34318032200` completed SUCCESS and produced artifact `P08-Security-Acceptance-67968a9230dae453b36f48549eda138d7b5401c7`, size 2,314 bytes, digest `sha256:b39ea805fec25b5a45a35c2eb445074e64622f48a44dc7c7e3ccd6a0ea8b3d98`.

The independent composed acceptance proves 13 security-boundary cases, 9 MOJ runtime cases, P07 execution authorization/binding regression coverage, P06 token-cache regression coverage, P08 Auth Scope Isolation, governance validation and non-secret evidence scanning.

## Deferred external evidence

`AUTH-NOT-ASSUME-04` remains `DEFERRED_EXTERNAL`; it is not PASS. Missing dependency: authenticated operation-level CAIT/MOJ documentation (or authorized live UAT configuration) proving whether `x-api-key` is required on `/genToken` for the target environment.

This does not require a second runtime: the integrated runtime already supports governed composition of the documented mechanisms. When authorized evidence is available, the owner/operator must configure the exact service/environment binding through the secure administration path and run **Test Authentication**. Expected result: the configured contract succeeds without cross-environment fallback or secret/token disclosure. Any mismatch remains a configuration/contract finding and must fail closed.
