# P08 Post-Closure MOJ Composite Authentication Contract Repair

Status: **POST-CLOSURE REGRESSION REPAIR — UAT CONTRACT PROVEN / PRODUCTION DETAIL DEFERRED**

This record is additive. It does not rewrite the historical P08 closure decision or its exact-main evidence. P08 remains **CLOSED** and P09 remains the current **OPEN / READY** phase while this closed-baseline regression is repaired before new P09 payload implementation.

## New authoritative external evidence

After P08 closed, the owner supplied authenticated official CAIT Developer Portal Swagger evidence for **Marriage Cases Service / API 129** and reported an owner-executed Swagger token call returning HTTP 200 in UAT.

For the observed **UAT** contract only, that evidence establishes:

- Swagger base: `https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage`
- token operation: `POST /genToken`
- token request header: `x-api-key`
- token content type: `application/x-www-form-urlencoded`
- token required form fields: `username`, `password`
- token successful response shape: `{ "data": "<token>" }`
- Marriage target operation: `POST /marriageCasesAPIGEE`
- target headers: `x-api-key` and `Authorization: Bearer <token>`
- target content type: `application/x-www-form-urlencoded`
- target required field proven by the supplied operation evidence: `civilId`

Classification: `UAT_PROVEN`.

No real API key, username, password, token, Civil ID, or personal MOJ response is recorded here.

## Production evidence boundary

The owner-supplied Production email proves only the Production gateway prefix `https://moj.api.cait.gov.kw`. It does **not** prove the Marriage path suffix, `/genToken` operation security requirement, target operation suffix, or payload details for Production.

Classification: `PRODUCTION_DEFERRED_EXTERNAL`.

There is no Production→UAT fallback and no inferred Production URL/auth contract.

## Canonical runtime repair

PR #50 extends the existing P08 canonical authentication path rather than creating a second runtime:

- `TokenEndpoint` profiles may use the proven three-secret composite material: `x-api-key`, `username`, `password`;
- `/genToken` receives the transient `x-api-key` header plus form username/password;
- the response `data` value is validated and used as the transient Bearer token;
- the target request receives the transient `x-api-key` plus `Authorization: Bearer ...`;
- exact ServiceId + EnvironmentId + AuthProfileId secret resolution remains mandatory;
- token-cache validity includes auth-profile version and per-secret generation information, including API-key generation;
- configured base-path prefixes are preserved when composing token and target URIs;
- legacy two-secret TokenEndpoint behavior and existing None/API-key/static-Bearer/custom-header paths remain intact;
- token acquisition failure is fail-closed and does not create an authentication retry storm;
- no credential/token plaintext is persisted or emitted in diagnostics/evidence.

The protected administration **Test Authentication** flow is extended through the same canonical secret resolution and token request semantics.

## Synthetic executable topology

Executable acceptance uses only synthetic material and the reserved `synthetic.invalid` domain. The contract fixture models:

`https://synthetic.invalid/WSWEB/WS/v1/marriage` + `/genToken` + `/marriageCasesAPIGEE`

It verifies the proven UAT request sequence without contacting MOJ or storing any owner credential or personal identifier.

Coverage includes composite success, missing/invalid API key, invalid token metadata, malformed/empty token responses, HTTP/network/TLS/timeout/cancellation failure, failed-acquisition atomicity, target-401 no-auth-storm behavior, exact-scope secret use, API-key generation participation in cache validity, and credential/token/input leakage rejection.

## Exact-head acceptance before evidence reconciliation

Code/security candidate: `a26686221673a3ea3062cb6f6b00380d38e753b0`.

On that exact PR #50 head, the following required gates completed SUCCESS before this evidence update:

- P00 Build Baseline (Release build)
- P06 Token Cache Runtime
- P06 Rotation Redaction Cache
- P07 Generic Execution Runtime
- P07 Execution Security Boundary
- P08 MOJ Auth Runtime
- P08 Auth Scope Isolation
- P08 Authentication Convergence
- P08 Security Acceptance
- P08 Phase Closure Gate
- Planning Integrity

P08 Security Acceptance run `34323699339` completed SUCCESS and produced artifact `P08-Security-Acceptance-a26686221673a3ea3062cb6f6b00380d38e753b0`, size 2,345 bytes, digest `sha256:3ca0fb7d3d31eef623c51dd3b237e4a594ac1c97ff47d96fa425b8fc58f1623b`.

The final PR head and the exact new `main` must still be reverified after this evidence commit and normal merge. No final-complete claim is made here.
