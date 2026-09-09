# P09 Official MOJ Contract Capture

Status: **PARTIAL AUTHORITATIVE CAPTURE — MATERIAL EXTERNAL CONTRACT ITEMS REMAIN DEFERRED**

Unit: `P09::official-contract-capture`

This directory is the machine-readable P09 contract evidence set for exactly the five canonical MOJ services. It is intentionally fail-closed: a field, path, authentication rule, response schema, error schema, result mapping, or non-destructive classification is present as proven only when official CAIT evidence supports it.

## Canonical service status

| API | Service | Capture status | What is authoritative now |
|---|---|---|---|
| 129 | Marriage Cases Service | `PARTIAL_PROVEN` | Observed UAT base; `/genToken` request/auth and HTTP 200 token shape; `/marriageCasesAPIGEE` method/path/content type, composite auth and required `civilId`. |
| 132 | Is Single Basic Service | `DEFERRED_EXTERNAL` | Official service reference only. Operation-level specification was not obtainable from the unauthenticated portal context. |
| 130 | Marriage Couple Last Case Service | `DEFERRED_EXTERNAL` | Official service reference only. Operation-level specification was not obtainable from the unauthenticated portal context. |
| 196 | Family Judgment Text Service | `DEFERRED_EXTERNAL` | Official service reference only. Operation-level specification was not obtainable from the unauthenticated portal context. |
| 134 | Procuration Status Service | `DEFERRED_EXTERNAL` | Official service reference only. Operation-level specification was not obtainable from the unauthenticated portal context. |

`DEFERRED_EXTERNAL` is not PASS and does not authorize seed metadata for the missing operation contract.

## API 129 evidence boundary

Owner-supplied authenticated official CAIT Swagger evidence establishes the observed UAT Marriage contract only:

- UAT base: `https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage`
- token operation: `POST /genToken`
- token header: `x-api-key`
- token content type: `application/x-www-form-urlencoded`
- token required fields: `username`, `password`
- owner-executed token call: HTTP 200
- successful token shape contains string field `data`
- target operation: `POST /marriageCasesAPIGEE`
- target headers/auth: `x-api-key` plus Bearer authorization
- target content type: `application/x-www-form-urlencoded`
- target required field proven: `civilId`

The available evidence does **not** prove the target success schema/status, documented error statuses/schemas, business response fields/result mappings, `civilId` type/validation, read-only/non-destructive classification, or the service-specific Production path/auth/payload contract. Those items remain explicit `DEFERRED_EXTERNAL`.

No Production suffix is inferred from UAT.

## Lawful retrieval record

The five official CAIT references were rechecked during this unit:

- `https://developer.api.cait.gov.kw/api/129`
- `https://developer.api.cait.gov.kw/api/132`
- `https://developer.api.cait.gov.kw/api/130`
- `https://developer.api.cait.gov.kw/api/196`
- `https://developer.api.cait.gov.kw/api/134`

The public CAIT Getting Started documentation states that users must sign in to see API specifications and additional documents. In the worker's unauthenticated retrieval context, direct page access and public search did not expose a usable operation-level OpenAPI/Swagger document for APIs 132, 130, 196, or 134. No alternate unofficial schema was substituted.

Public process reference: `https://developer.api.cait.gov.kw/index.php/get-started?language_content_entity=en`

## Exact deferred dependency and follow-up

For each service still deferred, an authorized operator must sign in to the CAIT Developer Portal, open the exact API page, and obtain the operation-level OpenAPI/Swagger specification or equivalent official CAIT contract evidence. The sanitized capture must establish:

1. environment server/base URL where explicitly provided;
2. HTTP method and exact relative path;
3. request content type;
4. request fields, types, required flags and documented validation;
5. operation-level authentication requirements;
6. success statuses and schemas;
7. documented error statuses and schemas;
8. business response fields and result-mapping candidates;
9. read-only/non-destructive classification where documented.

If newly obtained evidence conflicts with an existing snapshot, the snapshot and its executable validator must be changed together and reviewed; runtime/seed metadata must not be relaxed to make a speculative contract pass.

## Sanitization

These snapshots contain no live API keys, credentials, bearer/JWT tokens, Civil IDs, or personal MOJ results. Examples are intentionally omitted.

Executable drift gate: `scripts/validate_p09_contract_snapshots.py`
CI workflow: `.github/workflows/p09-contract-snapshots.yml`

This unit does not close P09. Material contract details for four services, plus several API 129 response/Production details, remain external.
