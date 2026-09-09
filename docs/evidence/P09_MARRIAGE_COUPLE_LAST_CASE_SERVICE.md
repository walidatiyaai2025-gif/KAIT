# P09 Marriage Couple Last Case Service — strict official-snapshot boundary

Unit: `P09::marriage-couple-last-case-service`

Authoritative contract snapshot: `docs/moj-api-reference/p09/api-130-marriage-couple-last-case.contract.json`.

## Current official evidence

The integrated CAIT snapshot for API 130 is `DEFERRED_EXTERNAL`. The authenticated CAIT Developer Portal operation specification/OpenAPI for this exact API is not present in the repository and was not retrievable by the official contract-capture worker without sign-in.

Accordingly, the repository currently has **no authoritative API130 evidence** for:

- UAT or Production operation base URL;
- HTTP method or relative path;
- request content type;
- request field names, including any male/female Civil ID key spelling;
- requiredness, technical type, length/range/pattern or other validation rules;
- operation authentication applicability/composition;
- success status/schema;
- documented error statuses/schemas;
- response fields, sensitivity or business result mappings;
- read-only/non-destructive classification.

Historical screenshots or owner notes that visually suggest male/female Civil ID inputs are not promoted into metadata, code, tests or evidence. They do not establish exact key spelling, casing, type, validation, ordering or wire format.

## Autonomous acceptance implemented

`tests/GSIP.P09MarriageCoupleLastCaseChecks` verifies only the lawful fail-closed boundary:

- the exact API130 snapshot remains `DEFERRED_EXTERNAL` for every operation-level contract item;
- the canonical `MARRIAGECOUPLELASTCASE` service exists without invented request fields or result mappings;
- UAT remains disabled with no base URL/path/method/content type/AuthProfile;
- Production remains disabled and contains only the already-proven generic CAIT Production gateway prefix, with no operation suffix/method/content type/AuthProfile;
- no AuthProfile, binding or secret reference is owned by or shared to API130;
- the existing Marriage Cases credential profile is not implicitly reused;
- execution of the seeded UAT and Production configurations fails closed before secret resolution or HTTP transport;
- a synthetic wrong-service AuthProfile is rejected before secret resolution or transport;
- a synthetic wrong-environment request cannot fall back to UAT;
- a structurally plausible but rejected synthetic SecretRef produces `AuthenticationUnavailable` before outbound transport;
- logs and generated evidence do not contain the synthetic private-input sentinel or SecretRef material;
- CI makes no live CAIT/MOJ request and contains no real secret or personal data.

Synthetic endpoint/profile values used by the negative runtime harness are intentionally non-authoritative test-only values and are never seeded or represented as API130 contract facts.

## Required items that remain blocked — NOT PASS

The following requested acceptance cases cannot lawfully be implemented until the authenticated official API130 operation schema is captured:

- exact method/path/content type serialization;
- exact male/female request keys;
- required/type/validation rules;
- swapped male/female field semantics;
- missing official-field validation;
- operation auth composition;
- documented success mapping;
- malformed documented response handling;
- documented error handling and error-body mapping;
- response-field sensitivity/masking rules.

These remain `DEFERRED_EXTERNAL — NOT PASS`. A future authorized official snapshot must update the contract JSON, canonical metadata, executable acceptance and result mappings together. No screenshot-derived field name or Marriage Cases credential may be substituted for that missing evidence.

P09 remains open. P10-P17 remain locked until formal P09 closure.
