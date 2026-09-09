# P08 MOJ Authentication Contract Matrix

Status: **INDEPENDENT AUDIT — P08 OPEN; NOT A PHASE-CLOSURE CLAIM**  
Audit unit: `P08::contract-evidence-reconciliation`  
Integrated audit base: `main=e63e4ca7d5719404452791784709af8d509ea498`  
Latest runtime candidate inspected: PR #45 head `337fc2ab157a21ec79593070d9718307cdd7d9f6`

## Evidence discipline

This matrix separates official CAIT/MOJ protocol facts from GSIP phase/security requirements. A branch, lease, PR description, or candidate-green run is not exact-main closure evidence. Unknown official details are `DEFERRED_EXTERNAL`; they must not be replaced with common OAuth/JWT assumptions.

## OFFICIAL REQUIREMENT → IMPLEMENTATION → TEST → CI EVIDENCE

| ID | Requirement / authority | Implementation inspected | Test / CI evidence | Audit status |
|---|---|---|---|---|
| `AUTH-APIKEY-01` | Preserved official Swagger evidence shows `ApiKeyAuth` request header `x-api-key`. | PR #45 preserves generic `ApiKeyHeader`; its focused test uses secret-slot metadata name `x-api-key` and exact execution Service+Environment+AuthProfile secret scope. Exact applicability of ApiKeyAuth to `/genToken` vs downstream operations is not captured. | PR #45 `ApiKeyUsesExactHeaderAndExecutionScopeAsync`; candidate workflows observed with no failure at audit time. Worker 3 adds independent `valid-x-api-key`. | **CANDIDATE_PASS; OPERATION APPLICABILITY DEFERRED_EXTERNAL** |
| `AUTH-TOKEN-01` | Official token operation is `POST /genToken`. | PR #45 implements POST token acquisition, **but hard-codes** `private const string MojTokenPath = "/genToken";`. Canonical P08 requires paths from environment/metadata. | Focused PR #45 test asserts `/genToken`, but does not prove metadata-driven token-path selection. Handoff posted on PR #45 and tracker #1. | **BLOCKING_CONTRACT_GAP** |
| `AUTH-TOKEN-02` | Token request content type is `application/x-www-form-urlencoded`. | PR #45 uses `FormUrlEncodedContent`. | PR #45 focused test asserts media type. | **CANDIDATE_PASS** |
| `AUTH-TOKEN-03` | Preserved official form fields are `username` and `password`; no additional request field is evidenced. | PR #45 requires exactly `username` + `password` secret slots and resolves them through exact Service+Environment+AuthProfile `SecretRef` scope. No `grant_type`, `client_id`, `client_secret`, scope, audience, or refresh-token request field was found in the inspected runtime patch. | Focused tests cover valid fields and missing credential; Worker 3 independent negative suite is pending integration. | **CANDIDATE_PASS / EXACT-MAIN PENDING** |
| `AUTH-TOKEN-04` | Preserved successful response example is `{ "data": "string" }`. | PR #45 parses only object `data` string and fails closed on unusable token material. | Focused test covers valid `data` and empty `data`; independent acceptance remains pending for the wider malformed/non-success matrix. | **CANDIDATE_PASS / ACCEPTANCE PENDING** |
| `AUTH-BEARER-01` | Marriage documentation says token from Token API is used as `Authorization: Bearer <token>` where that auth applies. | PR #45 attaches acquired token transiently to the target P07 request and clears `Authorization` in `finally`. | PR #45 focused test verifies acquired Bearer on service request and no Bearer on token acquisition. | **CANDIDATE_PASS / EXACT-MAIN PENDING** |
| `AUTH-EXPIRY-01` | Preserved MOJ evidence does not state an exact token TTL or `expires_in`. | PR #45 derives cache expiry only from JWT `exp` when parseable; absent/invalid `exp` returns current time, making the token immediately non-reusable. No fallback lifetime is invented. | Candidate runtime test uses a synthetic JWT `exp`; Worker 3 covers stale/near-expiry cache behavior. | **CONTRACT-SAFE CANDIDATE; NO OFFICIAL TTL CAPTURED** |
| `AUTH-SCOPE-01` | No OAuth-style `scope` or audience requirement is present in preserved MOJ auth evidence. | Inspected PR #45 token request does not send scope/audience. Canonical P06 cache identity can carry scope only when a real contract requires it. | Worker 2/3 acceptance tests cache-scope isolation synthetically; this is a GSIP isolation dimension, not a claim that MOJ defines OAuth scope. | **NO OFFICIAL SCOPE CAPTURED — DO NOT INVENT** |

## GSIP safety / phase requirements

| ID | Requirement | Implementation inspected | Test / CI evidence | Audit status |
|---|---|---|---|---|
| `AUTH-ISOLATION-01` | Exact Service+Environment+AuthProfile secret resolution; forged/stale/cross-scope bindings fail closed. | PR #43 integrated stale-binding fail-closed guard on `main=e63e4ca7...`. PR #45 uses `binding.ServiceId`, `binding.EnvironmentId`, `profile.Id` for secret resolution. | PR #43 reported P08 Auth Scope Isolation run `34312024564` SUCCESS. PR #44 full matrix remains open. | **PARTIAL_PASS — FULL MATRIX PENDING_INTEGRATION** |
| `AUTH-ISOLATION-02` | Cached token cannot cross Service, Environment, AuthProfile/version, secret generation, or officially relevant validity dimensions. | P06 `TokenCacheIdentity` already carries these boundaries. PR #45 uses exact execution identity and individual credential generations in validity parameters. | PR #44 and Worker 3 cover cross-scope/stale/near-expiry/single-flight cases; not yet exact-main closure evidence. | **CANDIDATE_PASS / EXACT-MAIN PENDING** |
| `AUTH-REDACT-01` | API keys, passwords, access tokens, `Authorization`, `x-api-key` must not leak to logs/errors/evidence. | P06 redaction/token serialization protections exist; PR #45 maps secret resolver failures to safe authentication-unavailable behavior and clears transient Authorization. | PR #45 evidence grep uses synthetic sentinels; Worker 3 `no-secret-token-diagnostics-leak` suite exists on branch head `2b32ca6a...`, not yet integrated. | **CANDIDATE_PASS / INDEPENDENT ACCEPTANCE PENDING** |
| `AUTH-P07-01` | P08 must layer into P07 generic execution, not create a second runtime. | PR #45 modifies `GenericServiceExecutionEngine.SendAuthenticatedAsync`/TokenEndpoint flow and reuses canonical P07 binding, HttpClient, result/error path. | PR #45 workflow explicitly runs P07 execution runtime/security regressions. | **CANDIDATE_PASS / EXACT-MAIN PENDING** |
| `AUTH-METADATA-01` | Base URL, header name and paths must come from Service+Environment/AuthProfile metadata; no hard-coded production auth path. | Base URL is binding-driven and `x-api-key` header name comes from secret/profile metadata, but PR #45 hard-codes `MojTokenPath = "/genToken"`. | Existing focused test only asserts hard-coded `/genToken`. Required repair test: configure a non-default synthetic token path and prove it is honored. | **BLOCKING_CONTRACT_GAP / HANDOFF_REQUIRED** |
| `AUTH-FIXTURE-01` | Synthetic P08 contract acceptance: valid x-api-key, valid `/genToken`, valid Bearer, missing/invalid auth, malformed/empty token, stale token and cross-scope cases; no real credentials. | Worker 1 focused runtime checks exist. Worker 3 independent suite exists on `worker/p08-security-acceptance-ci` head `2b32ca6a...`, composed on Worker 1 candidate. | Worker 3 cases include official contract fixture, valid x-api-key/Bearer, missing-invalid auth, forged AuthProfile/SecretRef, stale/wrong-scope token, failed refresh, single-flight and no-leak; integration/CI evidence pending. | **PENDING_INTEGRATION** |
| `AUTH-ADMIN-01` | Canonical P08 execution plan requires a token-generation **Test** administration action/button. | No current Worker 1/2/3 specialist lease owns P08 UI/admin implementation; Worker 1 and Worker 3 explicitly exclude UI. | No P08 admin Test-action evidence found. Captain handoff posted. | **CONTRACT_GAP / HANDOFF_REQUIRED** |

## Exact external items not captured live

The following remain `DEFERRED_EXTERNAL` unless sanitized exact official CAIT operation evidence is obtained:

1. Exact UAT/Production token server/base URLs.
2. Full `/genToken` success/error schemas and documented status matrix beyond the preserved successful `data` example.
3. Exact token TTL / expiry response field, if any.
4. Any official audience/scope semantics, if any.
5. Exact per-operation applicability of `ApiKeyAuth`, `BearerAuth`, or both, including whether `/genToken` itself requires `x-api-key`.
6. Sanitized machine-readable OpenAPI snapshot where obtainable.

## Current closure verdict

**P08 is NOT closure-ready from this audit snapshot.** Blocking items are the hard-coded token path in PR #45 and the unowned `AUTH-ADMIN-01` Test action. PR #44 and Worker 3 acceptance also require lawful integration and final exact-main P00–P08 verification. P09 remains locked.
