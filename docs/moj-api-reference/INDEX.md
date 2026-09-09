# MOJ / CAIT API Reference Index

This folder records the owner-supplied official CAIT Developer Portal references for the first Ministry of Justice integration. Workers must re-open the official documentation live during P08/P09 and preserve exact schemas/contracts. Do **not** invent fields from this index.

## Services

| Service | CAIT Developer Portal reference | Owner-supplied visible notes |
|---|---|---|
| Marriage Cases Service | `https://developer.api.cait.gov.kw/api/129` | Marriage service family; page exposes `/genToken` and a marriage-cases operation. |
| Is Single Basic Service | `https://developer.api.cait.gov.kw/api/132` | Checks whether a person is single/unmarried for a specified Civil ID. |
| Marriage Couple Last Case Service | `https://developer.api.cait.gov.kw/api/130` | Retrieves the last marriage case for a couple; owner screenshot shows male/female Civil ID request fields. |
| Family Judgment Text Service | `https://developer.api.cait.gov.kw/api/196` | Retrieves family judgment text; owner screenshot indicates case number/type inputs. |
| Procuration Status Service | `https://developer.api.cait.gov.kw/api/134` | Procuration status endpoint; official schema must be read live before seeding fields. |

## Authentication behavior visible in the original owner-supplied screenshots

The Marriage service documentation states that a consumer must first call a Token API to obtain an access token and include that token in the `Authorization` header as a Bearer token when calling other Marriage APIs.

The Token operation shown is:

- `POST /genToken`
- request content type: `application/x-www-form-urlencoded`
- request fields shown: `username`, `password`
- successful example shape shows `{ "data": "string" }`

The Swagger authorization dialog shown by the owner contains both:

- `ApiKeyAuth` in a header, with the exact header name displayed as `x-api-key`/the Swagger-defined casing;
- `BearerAuth` for the JWT/access token obtained from the authentication endpoint.

At original P08 closure, the screenshots above did not establish operation-level `ApiKeyAuth` applicability to `/genToken`, so that exact fact was correctly classified `DEFERRED_EXTERNAL` at that time.

## Post-closure authenticated UAT evidence — 2026-09-09

Owner-supplied authenticated official CAIT Swagger evidence plus an owner-executed successful UAT token call now resolves the former operation-level question **for the observed UAT Marriage Cases contract only**:

- UAT Swagger base: `https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage`
- `POST /genToken` requires header `x-api-key` plus form-urlencoded `username` and `password`;
- successful token shape is `{ "data": "<token>" }`;
- `POST /marriageCasesAPIGEE` requires `x-api-key` plus `Authorization: Bearer <token>`;
- the target request is form-urlencoded and the supplied operation evidence proves required field `civilId`.

Current classification for this observed UAT fact: `UAT_PROVEN`.

Production remains deliberately separate. Owner evidence proves only the Production gateway prefix `https://moj.api.cait.gov.kw`; it does not prove a Marriage suffix or the same operation-level security/payload contract. Current classification for those unproven Production details: `PRODUCTION_DEFERRED_EXTERNAL`.

See `docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md` for the additive repair record. Do not reinterpret the historical P08 closure evidence as if this external fact had been known earlier.

## Contract-capture requirement

During P08/P09 the worker must:

1. open each official CAIT page live;
2. record the selected server/base URL per environment;
3. capture operation method/path/content type;
4. capture required request fields and validation;
5. capture authentication requirements;
6. capture successful and documented error schemas/statuses;
7. store a sanitized machine-readable contract snapshot (OpenAPI JSON/YAML where obtainable) under this folder;
8. build contract fixtures/tests from those snapshots;
9. ensure no real API key, username/password, Bearer token, Civil ID or personal result is committed.

If the portal is unavailable or a schema is not visible, mark only that exact contract item `DEFERRED_EXTERNAL`; finish the generic engine, tests, metadata schema and sanitized fixtures that can be completed independently.

## P09 authoritative snapshot set

The P09 machine-readable capture is under `docs/moj-api-reference/p09/`. It is the implementation authority for P09 contract facts; the historical descriptive notes above must **not** be promoted into request/response metadata unless the corresponding P09 snapshot marks the item proven.

Current capture status:

- API 129 Marriage Cases: `PARTIAL_PROVEN` for the owner-supplied authenticated UAT facts recorded above; unproven response/error/Production/read-only details remain `DEFERRED_EXTERNAL`.
- API 132 Is Single Basic: `DEFERRED_EXTERNAL`.
- API 130 Marriage Couple Last Case: `DEFERRED_EXTERNAL`.
- API 196 Family Judgment Text: `DEFERRED_EXTERNAL`.
- API 134 Procuration Status: `DEFERRED_EXTERNAL`.

CAIT public guidance requires sign-in to view API specifications/additional documents. The four fully deferred services therefore remain materially blocked for seed/runtime contract work until authorized operation-level evidence is captured. P09 is not eligible for closure from this partial capture.
