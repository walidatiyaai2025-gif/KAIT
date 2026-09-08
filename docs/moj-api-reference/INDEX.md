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

## Authentication behavior visible in the official owner-supplied screenshots

The Marriage service documentation states that a consumer must first call a Token API to obtain an access token and include that token in the `Authorization` header as a Bearer token when calling other Marriage APIs.

The Token operation shown is:

- `POST /genToken`
- request content type: `application/x-www-form-urlencoded`
- request fields shown: `username`, `password`
- successful example shape shows `{ "data": "string" }`

The Swagger authorization dialog shown by the owner contains both:

- `ApiKeyAuth` in a header, with the exact header name displayed as `x-api-key`/the Swagger-defined casing;
- `BearerAuth` for the JWT/access token obtained from the authentication endpoint.

Treat exact base URLs, path casing, header casing/semantics, token lifetime, request/response field names and status schemas as live-documentation facts that must be verified during implementation.

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
