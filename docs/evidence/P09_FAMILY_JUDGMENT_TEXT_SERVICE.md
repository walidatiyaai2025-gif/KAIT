# P09 API196 Family Judgment Text Service — Lawful Acceptance Evidence

## Unit

- Phase: `P09`
- Unit: `P09::family-judgment-text-service`
- Service code: `FAMILYJUDGMENTTEXT`
- Official CAIT reference: `/api/196`
- Implementation mode: fail-closed/readiness and generic security acceptance only

## Authoritative contract status

The integrated official snapshot `docs/moj-api-reference/p09/api-196-family-judgment-text.contract.json` is `DEFERRED_EXTERNAL`.

The authenticated operation specification/OpenAPI for API196 is not available in the repository. Therefore this unit does **not** infer or seed any of the following from the service name, historical notes, screenshots, API129, or another MOJ service:

- UAT server URL;
- Production operation URL beyond the existing canonical gateway prefix;
- HTTP method;
- relative path;
- request content type;
- case-number/type request keys, formats, required flags, or validation;
- operation authentication composition;
- documented success or error statuses/schemas;
- response-field names;
- result mappings;
- judgment-text response-field sensitivity/masking metadata;
- read-only/non-destructive classification.

Those items remain `DEFERRED_EXTERNAL — NOT PASS`.

## Seeded production boundary verified

The executable acceptance verifies the existing canonical API196 seed remains non-executable:

- zero request fields;
- zero result mappings;
- exactly separate UAT and Production environment rows;
- UAT disabled with no server/path/method/content type/AuthProfile;
- Production disabled, gateway-prefix-only, with no path/method/content type/AuthProfile;
- no API196-owned AuthProfile, binding, secret reference, or implicit Marriage Cases credential reuse.

No production metadata or runtime implementation is changed by this unit.

## Automated security/readiness acceptance

The dedicated executable checks verify:

- seeded UAT and Production execution fail closed before transport or secret resolution;
- an explicit authorization denial fails before transport or secret resolution;
- an undocumented synthetic input is rejected by the generic validation boundary rather than becoming an inferred API196 field;
- a cross-service AuthProfile is rejected;
- a Production request cannot fall back to a synthetic UAT binding;
- a forged synthetic SecretRef becomes `AuthenticationUnavailable` before outbound transport;
- an exact synthetic Service+Environment AuthProfile binding is required before the bounded-response transport test can run;
- generic response-size enforcement returns `ResponseTooLarge` with no raw-response retention;
- synthetic judgment text, synthetic input, synthetic header material, and secret-reference text are absent from runtime logs and generated evidence.

The synthetic transport uses only a `.invalid` endpoint fixture and an in-memory handler. CI makes no live CAIT/MOJ request and contains no credential or personal-result material.

## Contract-specific requested cases that remain blocked

The following cannot lawfully be marked PASS until sanitized authenticated official API196 operation evidence is integrated:

- exact method/path/content type;
- exact case request schema and field validation;
- exact operation authentication;
- documented success-schema parsing;
- documented error-schema handling;
- malformed-response handling against the official response schema;
- exact response-field/result mappings;
- exact judgment-text or identifier sensitivity/masking designation.

When the official operation snapshot becomes available, these deferred cases must be implemented against that exact evidence rather than inferred from this readiness harness.
