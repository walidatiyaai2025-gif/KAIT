# P09 Is Single Basic Service — independent readiness evidence

## Authority

- Unit: `P09::is-single-basic-service`
- Canonical service: `ISSINGLEBASIC` / CAIT API 132.
- Authoritative snapshot: `docs/moj-api-reference/p09/api-132-is-single-basic.contract.json`.
- Current official operation status: `DEFERRED_EXTERNAL`.

The authenticated CAIT operation specification is not present in repository evidence. Therefore method, relative path, content type, request fields/types/validation, operation authentication, success/error schemas, response fields, result mappings, sensitive-response designations, and read-only classification are **not accepted as PASS** and are not inferred from the service title, API129, historical chat, screenshots, or any other MOJ service.

## Automated lawful acceptance

The dedicated executable check proves that:

- the database contains one canonical MOJ entity and exactly five canonical MOJ services;
- API132 exists as `ISSINGLEBASIC` with stable seeded identity;
- every official API132 contract item remains `DEFERRED_EXTERNAL` and empty where operation details are unknown;
- no request fields or result mappings are invented;
- UAT and Production are separate rows;
- UAT remains disabled with no server/path/method/content type/AuthProfile;
- Production remains disabled, gateway-prefix-only, with no inferred suffix/path/method/content type/AuthProfile;
- no Production-to-UAT fallback is represented;
- API132 owns no AuthProfile, binding, AuthProfileSecret, or implicit shared credential;
- reseeding is idempotent and preserves stable service/environment identities;
- CI uses synthetic metadata only and makes no CAIT/MOJ request;
- the generated evidence manifest contains no credential, token, SecretRef, Civil ID, or personal response payload.

## Explicit NOT PASS items

Until sanitized authenticated official API132 operation evidence is integrated, the following remain `DEFERRED_EXTERNAL — NOT PASS`:

- exact HTTP method/path/content type;
- exact request fields, types, required flags, formats and validation;
- exact operation-level authentication;
- documented success statuses/schema;
- documented 4xx/5xx statuses/schema;
- malformed-response validation against an official schema;
- exact structured result mappings;
- exact sensitive response-field masking rules.

This is a fail-closed readiness/security acceptance boundary, not a competing production runtime and not a P09 closure claim.
