# P09 API134 Procuration Status Service — Lawful Acceptance Evidence

## Unit

- Phase: `P09`
- Unit: `P09::procuration-status-service`
- Service code: `PROCURATIONSTATUS`
- Official CAIT reference: `/api/134`
- Implementation mode: fail-closed/readiness and generic security acceptance only

## Authoritative contract status

The integrated official snapshot `docs/moj-api-reference/p09/api-134-procuration-status.contract.json` is `DEFERRED_EXTERNAL`.

The authenticated operation specification/OpenAPI for API134 is not available in the repository. Therefore this unit does **not** infer or seed any of the following from the service name, historical notes, screenshots, API129, or any Marriage service:

- UAT server URL;
- Production operation URL beyond the existing canonical gateway prefix;
- HTTP method;
- relative path;
- request content type;
- request field names, types, required flags, formats, or validation;
- operation authentication composition;
- documented success or error statuses/schemas;
- response-field names;
- result mappings;
- sensitive response-field designations/masking metadata;
- read-only/non-destructive classification.

Those contract items remain `DEFERRED_EXTERNAL — NOT PASS` until sanitized authenticated official API134 operation evidence is integrated.

## Seeded production boundary verified

The executable acceptance verifies the existing canonical API134 seed remains non-executable:

- zero request fields;
- zero result mappings;
- exactly separate UAT and Production environment rows;
- UAT disabled with no server/path/method/content type/AuthProfile;
- Production disabled, gateway-prefix-only, with no path/method/content type/AuthProfile;
- no API134-owned AuthProfile, binding, or secret reference;
- no implicit Marriage Cases credential reuse.

No production metadata, execution runtime, or authentication implementation is changed by this unit.

## Automated security/readiness acceptance

The dedicated executable checks verify:

- seeded UAT and Production execution fail closed before transport or secret resolution;
- an explicit authorization denial fails before transport or secret resolution;
- an undocumented synthetic input is rejected instead of being treated as an inferred API134 request field;
- a cross-service AuthProfile is rejected;
- a Production request cannot fall back to a synthetic UAT AuthProfile binding;
- a forged synthetic SecretRef becomes `AuthenticationUnavailable` before outbound transport;
- an exact synthetic Service+Environment AuthProfile binding is required before the generic bounded-response transport test can run;
- generic response-size enforcement returns `ResponseTooLarge` with no raw-response retention;
- synthetic procuration response content, input material, header material, and secret-reference text are absent from runtime logs and generated evidence.

The synthetic transport uses only a `.invalid` endpoint fixture and an in-memory handler. CI makes no live CAIT/MOJ request and contains no credential, Civil ID, or personal-result material.

## Contract-specific requested cases that remain blocked

The following cannot lawfully be marked PASS from the current official snapshot:

- exact method/path/content type;
- exact request field names/types/required validation;
- exact operation-level authentication;
- documented success-schema parsing;
- documented error-schema handling;
- malformed-response handling against the official response schema;
- exact structured result mappings;
- exact sensitive-data designation and masking rules.

When the authenticated official operation specification becomes available, these deferred cases must be implemented against that exact evidence rather than inferred from this readiness harness.
