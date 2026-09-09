# P08 MOJ Authentication Contract Matrix

Status: **INDEPENDENT AUDIT — P08 OPEN; NOT A PHASE-CLOSURE CLAIM**  
Audit unit: `P08::contract-evidence-reconciliation`  
Audit base: `main=e63e4ca7d5719404452791784709af8d509ea498`  
Tracker: issue #1

## Authority and evidence discipline

This matrix separates **official CAIT/MOJ contract facts** from **GSIP safety/phase requirements**. A repository plan, worker lease, branch name, or test intention is not implementation evidence. A row is satisfied only when the implementation, executable test, and CI evidence can be tied to an actual commit/run.

Official CAIT/MOJ facts are limited to what is preserved in `docs/moj-api-reference/INDEX.md` or what can be recaptured from the official CAIT Developer Portal. The currently preserved owner-supplied official evidence establishes the minimum authentication facts below. It does **not** establish an exact token lifetime, OAuth-style scope, audience, `grant_type`, refresh-token flow, per-service base URL, or a complete status/error schema.

If the live portal does not expose an exact item to the worker, that item is `DEFERRED_EXTERNAL`; it must not be guessed.

## Officially evidenced minimum authentication contract

| ID | Official requirement / fact | Authority | Implementation | Executable test | CI evidence | Audit status |
|---|---|---|---|---|---|---|
| `AUTH-APIKEY-01` | Swagger authorization evidence shows `ApiKeyAuth` in a request header with the header name displayed as `x-api-key` / Swagger-defined casing. Secret value must never be committed. | `docs/moj-api-reference/INDEX.md` | Worker 1 `P08::moj-auth-runtime` branch exists but, at this audit snapshot, is still at its baseline and has no P08 runtime commit. | Worker 1 runtime checks pending; Worker 3 independent security acceptance pending. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-TOKEN-01` | Token operation is `POST /genToken`. | `docs/moj-api-reference/INDEX.md` | No landed P08 token-acquisition implementation at this audit snapshot. Token endpoint/path must come from the exact environment/auth metadata rather than a hard-coded production URL/path. | Worker 1 runtime checks pending. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-TOKEN-02` | `/genToken` request content type is `application/x-www-form-urlencoded`. | `docs/moj-api-reference/INDEX.md` | Pending Worker 1 runtime. | Must assert exact media type. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-TOKEN-03` | `/genToken` form fields shown by official evidence are exactly `username` and `password`. No additional field is authorized by the preserved evidence. | `docs/moj-api-reference/INDEX.md` | Pending Worker 1 runtime. Username/password must resolve transiently through the canonical Secret Vault/`SecretRef` boundary. | Must reject missing/empty credentials and must not introduce undocumented `grant_type`, `client_id`, `client_secret`, `scope`, audience, or refresh-token fields. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-TOKEN-04` | Successful example response shape contains the token in `{ "data": "string" }`. | `docs/moj-api-reference/INDEX.md` | Pending Worker 1 runtime. | Must cover valid `data`, missing `data`, empty/whitespace `data`, malformed JSON, and non-success response without token leakage. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-BEARER-01` | Marriage API documentation states that the access token obtained from the Token API is sent in `Authorization` as a Bearer token for other Marriage APIs where that authentication applies. | `docs/moj-api-reference/INDEX.md` | Pending Worker 1 P07 integration. | Must verify `Authorization: Bearer <token>` is attached only to the intended request and is removed/not retained across unrelated requests. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-EXPIRY-01` | Tokens may expire, but the preserved official MOJ authentication evidence does **not** define an exact TTL or `expires_in` field. | Official repository evidence; live general CAIT guidance corroborates expiry exists but does not establish the MOJ operation TTL. | P06 provides an in-memory cache with an expiry safety window and caller-supplied expiry. P08 must not manufacture an MOJ lifetime. | Expired-token acceptance may use a synthetic documented/intrinsic expiry signal or deterministic cache fixture; it must not encode an invented MOJ TTL. | P06 cache regression exists; P08-specific evidence pending. | **PARTIAL — NO OFFICIAL TTL CAPTURED** |
| `AUTH-SCOPE-01` | No OAuth-style `scope` or `audience` requirement is present in the preserved official MOJ authentication evidence. | `docs/moj-api-reference/INDEX.md` | Canonical cache identity can isolate audience/scope when a contract actually defines them, but P08 must leave such dimensions empty unless official evidence supplies them. | Contract-negative/static checks must reject undocumented auth request fields or assumptions. | Pending Worker 1/3. | **NO OFFICIAL SCOPE CAPTURED — DO NOT INVENT** |

## GSIP safety and phase requirements

These rows are mandatory GSIP requirements even where they are not external CAIT protocol fields.

| ID | GSIP requirement | Authority | Implementation | Executable test | CI evidence | Audit status |
|---|---|---|---|---|---|---|
| `AUTH-ISOLATION-01` | Secret resolution must bind exact `ServiceId + EnvironmentId + AuthProfileId`; stale/forged/cross-scope bindings fail closed before secret material is exposed. | `AGENTS.md`; `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`; `CURRENT_PHASE.md` | PR #43 integrated the stale-binding fail-closed production guard on `main=e63e4ca7d5719404452791784709af8d509ea498`. | PR #43 stale-binding negative check passed on its candidate. PR #44 extends the unit-owned full isolation acceptance matrix and is not yet integrated at this snapshot. | PR #43 reported dedicated P08 Auth Scope Isolation run `34312024564` SUCCESS before merge. Exact-new-main/PR #44 convergence still required. | **PARTIAL_PASS — PRODUCTION GUARD INTEGRATED; FULL MATRIX PENDING** |
| `AUTH-ISOLATION-02` | Token cache identity includes exact Service, Environment and AuthProfile plus profile version, secret generation, and any officially valid audience/scope dimensions. No Production→UAT or cross-service reuse. | `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`; P06 token-cache contract | Existing `TokenCacheIdentity` includes those isolation dimensions. | PR #44 states coverage for cross-service/environment/profile/version/generation/scope reuse and near-expiry refresh; audit must inspect the merged code/run before promoting to PASS. | PR #44 pending. | **PARTIAL — BASE INFRASTRUCTURE PRESENT, P08 ACCEPTANCE PENDING** |
| `AUTH-REDACT-01` | API keys, username/password, access tokens and `Authorization`/`x-api-key` values must not appear in logs, exceptions, audit payloads, screenshots or evidence. | `docs/SECURITY_SECRETS_POLICY.md`; `AGENTS.md` | P06 redaction/vault and token-value serialization protections are present. P08 runtime-specific paths are not yet landed. | Worker 3 owns independent P08 leakage scanning; Worker 1 must also exercise secret-safe runtime failure paths. | Pending. | **PENDING_P08_RUNTIME_EVIDENCE** |
| `AUTH-P07-01` | P08 authentication must layer into the canonical P07 `IServiceExecutionEngine` / authenticated-send boundary; it must not create a second service execution runtime or bypass P07 authorization, retry, correlation, masking, or error classification. | `PROJECT_CONTROL.md`; `CURRENT_PHASE.md` | Existing P07 engine currently returns `AuthenticationUnavailable` for `TokenEndpoint`; Worker 1 owns the P08 integration replacement/extension. No P08 integration commit exists at this snapshot. | Worker 1 runtime checks and Worker 3 acceptance pending. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-METADATA-01` | Base URL, token endpoint/path, header names and environment bindings are metadata/configuration concerns; UAT and Production stay independent. | `execution/GSIP_Full_Execution.json` P08; `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` | Pending Worker 1 runtime/configuration use. | Contract validation must detect hard-coded external URLs or `/genToken` production path in P08 production source. | Pending. | **PENDING_IMPLEMENTATION** |
| `AUTH-FIXTURE-01` | P08 requires synthetic contract fixtures/checks for success, unauthorized, and expired-token behavior; no live credentials. | `execution/GSIP_Full_Execution.json` P08 | Worker 1 and Worker 3 own runtime/security test paths; neither is integrated at this snapshot. | Required: valid x-api-key, valid `/genToken`, valid Bearer, missing/invalid auth, malformed/empty token, stale token, wrong-scope/cross-scope cases as applicable without inventing a protocol scope. | Pending. | **PENDING_ACCEPTANCE** |
| `AUTH-ADMIN-01` | P08 canonical execution plan requires a token-generation **Test** action/button in administration. | `execution/GSIP_Full_Execution.json` P08 | **No active specialist lease currently owns P08 administration/UI implementation.** Worker 1 explicitly excludes UI; Worker 2 and Worker 3 exclude UI. Captain convergence must assign or implement the smallest lawful P08 admin Test flow without entering P09. | Server-side authorization, failure-safe/redacted result, and bilingual UI behavior must be tested if this phase requirement is retained. | None at this snapshot. | **CONTRACT_GAP / HANDOFF_REQUIRED** |

## Exact external items not captured live

The following remain `DEFERRED_EXTERNAL` unless an authorized worker can access and sanitize the exact official CAIT page/OpenAPI material:

1. Exact UAT and Production server/base URLs for the relevant P08 token/auth operation.
2. Complete `/genToken` response schema beyond the preserved successful `{ "data": "string" }` example.
3. Documented `/genToken` error status/schema matrix.
4. Exact token lifetime/TTL or expiry response field, if any.
5. Any official audience/scope semantics, if any.
6. Exact per-service requirement for `ApiKeyAuth`, `BearerAuth`, or both across the five later P09 services.
7. Sanitized machine-readable OpenAPI snapshot where obtainable.

These deferred items must **not** be replaced by guessed constants or common OAuth conventions.

## Closure audit rule

P08 is not closure-ready from this snapshot. The captain may consider closure only after:

- Worker 1 runtime is committed, reviewed against `AUTH-APIKEY-*`, `AUTH-TOKEN-*`, `AUTH-BEARER-*`, `AUTH-METADATA-*` and `AUTH-P07-*`;
- Worker 2 full isolation acceptance is integrated and exact-main green;
- Worker 3 independent security/no-leak acceptance is integrated and exact-main green;
- `AUTH-ADMIN-01` is resolved or the canonical phase requirement is lawfully reconciled by higher-authority evidence;
- all P00-P07 regressions remain green on the exact final P08 main;
- no undocumented token TTL/scope/auth field has entered production code;
- any irreducibly inaccessible official contract detail is recorded precisely as `DEFERRED_EXTERNAL`, never as PASS.
