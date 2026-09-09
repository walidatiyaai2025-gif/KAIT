# MOJ / CAIT API Reference Index

This folder records the owner-supplied official CAIT Developer Portal references for the first Ministry of Justice integration. The machine-readable P09 snapshots under `docs/moj-api-reference/p09/` are the implementation authority for contract facts. Do **not** invent fields or promote historical descriptive notes beyond what those snapshots prove.

## Canonical services

| API | Service | CAIT Developer Portal reference | Current P09 UAT contract status |
|---|---|---|---|
| 129 | Marriage Cases Service | `https://developer.api.cait.gov.kw/api/129` | `PROVEN_UAT_CONTRACT` |
| 132 | Is Single Basic Service | `https://developer.api.cait.gov.kw/api/132` | `PROVEN_UAT_CONTRACT` |
| 130 | Marriage Couple Last Case Service | `https://developer.api.cait.gov.kw/api/130` | `PROVEN_UAT_CONTRACT` |
| 196 | Family Judgment Text Service | `https://developer.api.cait.gov.kw/api/196` | `PROVEN_UAT_CONTRACT` |
| 134 | Procuration Status Service | `https://developer.api.cait.gov.kw/api/134` | `PROVEN_UAT_CONTRACT` |

Exactly these five services belong to the first MOJ entity in P09.

## Historical P08 authentication evidence

The original owner-supplied Marriage documentation established the common token flow:

- `POST /genToken`;
- `application/x-www-form-urlencoded` token request;
- canonical secret names `username` and `password`;
- successful documented token response shape `{ "data": "string" }`;
- token value returned from the documented `data` response field;
- Swagger scheme `BearerAuth`, used as a Bearer token on protected Marriage operations.

The original screenshots also exposed the `x-api-key` API-key scheme and the Bearer security scheme, but at initial P08 closure they did not prove operation-level API-key applicability. That uncertainty was correctly kept fail-closed at that time.

Subsequent authenticated owner evidence on 2026-09-09 proved the observed UAT Marriage Cases sequence:

- gateway API-key header plus the documented form credentials for `/genToken`;
- token returned from `data`;
- gateway API-key header plus Bearer authorization for `/marriageCasesAPIGEE`.

This historical progression is retained so later evidence does not rewrite what was known at P08 closure.

## P09 authoritative contract set

`docs/moj-api-reference/p09/manifest.json` plus the five `api-*.contract.json` files now capture authoritative UAT contracts for all five services from owner-supplied authenticated official CAIT evidence. `scripts/validate_p09_contract_snapshots.py` is the executable drift gate.

The captured facts include, where explicitly documented:

1. UAT base/environment boundary;
2. exact HTTP method and relative path;
3. request content type;
4. request field names, types, requiredness and documented validation status;
5. authentication requirements and unresolved authentication applicability;
6. success and documented error statuses/schemas;
7. response fields and result-mapping candidates;
8. documented token response paths/lifetimes;
9. read-only/non-destructive classification only where official evidence supports it.

A `PROVEN_UAT_CONTRACT` status means the sanitized contract is authoritative for autonomous implementation and acceptance. It does **not** mean a live owner-operated UAT smoke test has passed.

## Fail-closed / OWNER_LAST boundary

The current authoritative manifest keeps these items `DEFERRED_EXTERNAL`, never PASS:

- service-specific Production operation URLs/authentication/payload contracts beyond the separately proven Production gateway prefix;
- exact operation-level API-key applicability for API132, API130, API196 and API134 where the supplied operation evidence proves Bearer but does not prove the API-key header on that exact operation;
- live backend-data validation for API196 and API134 because official CAIT identifies their UAT try-out targets as mocks;
- any live UAT call that requires owner-held credentials and an approved test record when those inputs are unavailable to the worker.

There is no Production-to-UAT fallback and no implicit credential/token sharing between Service + Environment + AuthProfile scopes.

## Sanitization

Repository references, fixtures, tests, CI artifacts and screenshots must contain no real API key, consumer credential, username/password, Bearer/JWT token, Civil ID or personal MOJ result. Synthetic evidence must remain clearly synthetic and non-personal.

If new official evidence conflicts with an existing snapshot, update the snapshot, validator, metadata/runtime representation and affected acceptance together. Never weaken the gate to make speculative behavior pass.

P09 is not closed merely because the five UAT contracts are captured; executable independent acceptance, UI/runtime readiness, owner-last classification and formal phase-exit evidence remain governed by the live ledger and acceptance criteria.
