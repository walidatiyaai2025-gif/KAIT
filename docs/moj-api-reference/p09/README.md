# P09 Official MOJ Contract Capture

Status: **FIVE AUTHORITATIVE UAT CONTRACTS CAPTURED — OWNER-LAST ITEMS REMAIN DEFERRED**

Unit: `P09::official-contract-convergence`

This directory is the machine-readable P09 contract evidence set for exactly the five canonical MOJ services. It remains fail-closed: a field, path, authentication rule, response schema, error schema, result mapping, or non-destructive classification is treated as proven only when the owner-supplied authenticated official CAIT evidence supports it. A proven UAT contract is not equivalent to a completed live UAT smoke test.

## Canonical service status

| API | Service | Capture status | Authoritative UAT contract |
|---|---|---|---|
| 129 | Marriage Cases Service | `PROVEN_UAT_CONTRACT` | Marriage UAT base; `POST /genToken` form contract; `POST /marriageCasesAPIGEE` form contract; documented response/error schemas; observed UAT composite authentication where explicitly proven. |
| 132 | Is Single Basic Service | `PROVEN_UAT_CONTRACT` | Marriage UAT base; common Marriage token contract; `POST /isSingleBasicAPIGEE`; exact request field and documented response/error schemas. |
| 130 | Marriage Couple Last Case Service | `PROVEN_UAT_CONTRACT` | Marriage UAT base; common Marriage token contract; `POST /marriageCoupleLastAPIGEE`; exact request fields and documented response/error schemas. |
| 196 | Family Judgment Text Service | `PROVEN_UAT_CONTRACT` | Verdict UAT base; JSON token contract; `POST /familyJudgmentText`; documented response/error schemas. Official UAT try-out is identified as a mock target. |
| 134 | Procuration Status Service | `PROVEN_UAT_CONTRACT` | Procuration UAT base; JSON authentication-token contract; `POST /Procuration/ProcurationStatus`; documented response/error schemas and six-hour token lifetime. Official UAT try-out is identified as a mock target. |

The exact machine-readable authority is `manifest.json` plus the five `api-*.contract.json` snapshots. The executable validator is `scripts/validate_p09_contract_snapshots.py`.

## Authentication evidence boundary

API 129 Marriage Cases has observed UAT operation-level evidence for the sequence:

- `POST /genToken` with the documented gateway API-key header and form credentials;
- token returned in the documented response field;
- `POST /marriageCasesAPIGEE` with the documented gateway API-key header plus Bearer authorization.

For APIs 132, 130, 196 and 134, Bearer participation is captured where the supplied official operation text proves it. The existence of an API-key security scheme at service level is **not** promoted into operation-level API-key applicability unless that exact operation is independently proven. Those unresolved operation-level API-key facts remain `DEFERRED_EXTERNAL`.

No credential, API key, token, Civil ID or personal result is stored in this reference set.

## Environment boundary

The supplied official evidence establishes the UAT contracts represented in the snapshots. It does not establish service-specific Production suffixes, operation authentication applicability, or Production payload contracts beyond the separately proven Production gateway prefix.

Therefore:

- UAT contract metadata may be seeded only as represented by the authoritative snapshots;
- Production service-specific operation details remain `DEFERRED_EXTERNAL` and fail closed;
- there is no Production-to-UAT fallback;
- UAT and Production credentials/tokens remain isolated by the canonical Service + Environment + AuthProfile scope.

## OWNER_LAST / DEFERRED_EXTERNAL

The following items are not autonomous PASS conditions:

1. service-specific Production operation URLs/authentication/payload contracts beyond the proven gateway prefix;
2. exact operation-level API-key applicability for API132/API130/API196/API134 where the supplied operation evidence proves Bearer but not the API-key header on that exact operation;
3. live backend-data validation for API196/API134 because official CAIT identifies their UAT try-out targets as mocks;
4. any live UAT call that requires owner-held credentials and an approved synthetic/test record.

When credentials or approved test records are unavailable, live UAT remains `OWNER_LAST / DEFERRED_EXTERNAL`, never PASS. Autonomous contract, metadata, runtime, isolation, masking and synthetic acceptance must still complete independently.

## Sanitization and drift discipline

All committed contract evidence and executable fixtures must remain sanitized. Do not add real API keys, consumer credentials, usernames/passwords, Bearer/JWT tokens, Civil IDs or personal MOJ payloads. If new official evidence conflicts with a snapshot, update the snapshot, validator, seed/runtime metadata and affected acceptance together; never relax an assertion merely to make speculative behavior pass.

CI workflow: `.github/workflows/p09-contract-snapshots.yml`

This contract-capture/convergence unit by itself does not close P09.
