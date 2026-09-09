# P08 MOJ Authentication Contract Matrix

Status: **POST-CLOSURE RECONCILED — UAT COMPOSITE CONTRACT PROVEN**

Historical P08 closure implementation baseline: exact `main` SHA `67968a9230dae453b36f48549eda138d7b5401c7`.

Post-closure repair record: `docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md`.

This matrix preserves the original closure boundary and adds the later owner-supplied official CAIT evidence. A fact proven for observed UAT is not promoted to Production.

| ID | Requirement | Authority | Integrated implementation / evidence | Current status |
|---|---|---|---|---|
| AUTH-CONTRACT-01 | UAT and Production use independent environment base URLs/configuration. | Official repository references + GSIP environment contract | P05/P06/P07 environment binding and P08 exact-scope authentication resolution. | PASS |
| AUTH-CONTRACT-02 | MOJ gateway authentication supports the documented `x-api-key` header where applicable. | Official repository references | Generic header/API-key AuthProfile support; secret comes from Secret Vault and is scoped to exact Service + Environment + AuthProfile. | PASS |
| AUTH-CONTRACT-03 | Token generation uses `POST /genToken` with `application/x-www-form-urlencoded` username/password and consumes the documented `data` token response. | Official repository references | P08 metadata-driven token acquisition; token endpoint path is governed metadata, not a production hard-code. | PASS |
| AUTH-CONTRACT-04 | Bearer authentication is attached to protected MOJ requests where the documented operation requires it. | Official repository references | Token is transiently attached as `Authorization: Bearer ...`; plaintext is not persisted/logged/evidenced. | PASS |
| AUTH-OBSERVED-UAT-01 | Marriage Cases UAT `/genToken` requires `x-api-key` together with form `username`/`password`. | Owner-supplied authenticated official CAIT Swagger + owner-executed UAT token call | PR #50 extends the canonical TokenEndpoint path to resolve the API key from the same exact Service + Environment + AuthProfile scope and attach it transiently to `/genToken`. | UAT_PROVEN |
| AUTH-OBSERVED-UAT-02 | Marriage Cases UAT target `POST /marriageCasesAPIGEE` requires `x-api-key` plus the acquired Bearer token; request is form-urlencoded and `civilId` is a proven required field. | Owner-supplied authenticated official CAIT Swagger | Synthetic `synthetic.invalid` contract acceptance verifies the same two-stage request topology without using real credentials or personal data. | UAT_PROVEN |
| AUTH-PLATFORM-01 | No plaintext API key, username, password or Bearer token in metadata, Git, logs or evidence. | GSIP security policy | SecretRef/AuthProfile vault boundary plus independent P08 leakage scan. | PASS |
| AUTH-PLATFORM-02 | Authentication lookup is exact Service + Environment + AuthProfile, with no Production→UAT or cross-service fallback. | GSIP security policy | P08 Auth Scope Isolation acceptance covers forged/stale profiles/refs, cross-service, cross-environment and fallback attempts. | PASS |
| AUTH-PLATFORM-03 | Cached tokens are isolated by governed scope and credential generation/version and cannot reuse stale/wrong-scope material. | GSIP security policy | Canonical P06 token cache reused by P08; composite identity includes API-key generation as a validity dimension in addition to profile/scope identity. | PASS |
| AUTH-PLATFORM-04 | Missing, malformed, empty or invalid authentication configuration fails closed. | GSIP security policy | Runtime and independent P08 negative tests cover missing/invalid auth, token path and token response. | PASS |
| AUTH-PLATFORM-05 | Token acquisition and target execution do not create unbounded retries/auth storms. | GSIP security policy | Independent security acceptance covers bounded retry, target 401 behavior, cancellation and failure atomicity. | PASS |
| AUTH-PLATFORM-06 | Diagnostics/evidence must be secret-safe. | GSIP security policy | Dedicated leakage scan and sanitized evidence artifact. | PASS |
| AUTH-ADMIN-01 | Administration provides a token-generation authentication Test action without exposing token plaintext. | Canonical P08 execution plan | Existing server-authorized Test Authentication path is extended to use the same proven composite `/genToken` request when the profile contains `x-api-key`, `username`, `password`. | PASS |
| AUTH-NOT-ASSUME-01 | Do not assume credentials/AuthProfiles are shared between MOJ services or environments. | Canonical P08 rules | Default isolated bindings; sharing only through explicit governed relationship. | PASS |
| AUTH-NOT-ASSUME-02 | Do not invent OAuth client-id/client-secret/scope/grant conventions absent official evidence. | Evidence discipline | Runtime implements only repository-supported form username/password token contract plus the now-proven UAT API-key requirement. | PASS |
| AUTH-NOT-ASSUME-03 | Do not invent token lifetime when the response does not document one. | Evidence discipline | Cache re-use derives expiry from JWT `exp` when present; otherwise token is not given an invented reusable lifetime. | PASS |
| AUTH-NOT-ASSUME-04 | Do not infer the observed UAT Marriage path/security contract for Production. | Evidence discipline / owner-last | Production evidence proves only gateway prefix `https://moj.api.cait.gov.kw`; no Marriage suffix, `/genToken` security applicability, target suffix or payload contract is invented. | PRODUCTION_DEFERRED_EXTERNAL |

## Historical exact-main closure baseline

The original P08 closure evidence remains unchanged as history: on exact `main` SHA `67968a9230dae453b36f48549eda138d7b5401c7`, all **17/17** push workflows completed successfully. At that time operation-level `ApiKeyAuth` applicability to `/genToken` was legitimately `DEFERRED_EXTERNAL` because the later authenticated UAT evidence did not yet exist.

## Post-closure repair acceptance

On PR #50 code/security head `a26686221673a3ea3062cb6f6b00380d38e753b0`, Release build and the relevant P06/P07/P08 runtime, isolation, security, convergence, closure and governance gates completed successfully before evidence reconciliation. P08 Security Acceptance run `34323699339` completed SUCCESS and produced sanitized artifact `P08-Security-Acceptance-a26686221673a3ea3062cb6f6b00380d38e753b0` with digest `sha256:3ca0fb7d3d31eef623c51dd3b237e4a594ac1c97ff47d96fa425b8fc58f1623b`.

The final evidence commit, normal merge, and exact-new-main verification remain required before the regression repair can be marked integrated.
