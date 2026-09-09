# P09 Marriage Cases Service — autonomous acceptance and owner-last boundary

Unit: `P09::marriage-cases-service`

This unit uses the existing metadata-driven P05/P07 execution architecture and the P08 MOJ composite-authentication runtime. It does not add a service-specific controller, HTTP client path, credential store, or authentication implementation.

## Authoritative API 129 facts used

The integrated sanitized contract at `docs/moj-api-reference/p09/api-129-marriage-cases.contract.json` is authoritative for this unit. It proves the following UAT facts for the Marriage Cases target operation:

- base family: `https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage`;
- method: `POST`;
- relative path: `/marriageCasesAPIGEE`;
- request content type: `application/x-www-form-urlencoded`;
- exact required request key: `civilId`;
- target authentication: `x-api-key` plus Bearer obtained through the proven `/genToken` flow.

Production remains a separate evidence boundary. The integrated Production evidence proves the gateway prefix only. No target suffix, request contract, authentication applicability, response contract, or fallback from UAT is inferred.

## Executable autonomous acceptance

`tests/GSIP.P09MarriageCasesChecks` verifies with synthetic-only material:

- exact `/genToken` and `/marriageCasesAPIGEE` URI composition preserves the complete `/WSWEB/WS/v1/marriage` base path;
- exact `civilId` form-key casing and form serialization;
- generic browser `required` validation is driven from metadata and sensitive submitted input is not redisplayed;
- server-side required-field validation stops before secret material or outbound transport;
- the target uses the existing composite x-api-key + Bearer runtime;
- secret resolution remains scoped to the exact Service + Environment + AuthProfile;
- cross-service and cross-environment AuthProfile misuse fail closed before secret resolution/transport;
- Production cannot fall back to UAT configuration/authentication;
- a synthetic target `401` remains one target attempt and does not reacquire the already-valid cached token or cause a token storm;
- sensitive request values are absent from execution logs, diagnostics and generated evidence;
- CI never calls the live UAT endpoint.

The synthetic target `200` used by the transport test is intentionally an empty synthetic transport body. It is **not** represented as an official MOJ success fixture and is not used to create business result mappings.

## Mandatory deferred items — NOT PASS

The current official API 129 snapshot explicitly leaves these target-operation facts `DEFERRED_EXTERNAL`:

- target success HTTP status/schema;
- target documented error statuses/schemas (including any 400/401/404/500 contract details);
- target response fields;
- business result mappings;
- response-field sensitivity/masking rules that depend on the unavailable response schema;
- `civilId` technical type and validation rules beyond requiredness;
- read-only/non-destructive classification.

Accordingly, this unit does **not** invent a Marriage Cases 200 response fixture, error bodies/status contract, result mapping, field validation rule, or response masking rule. Those items remain external blockers until stronger authorized official CAIT/MOJ evidence is integrated. A future official snapshot update must update the executable acceptance before those items can become PASS.

## Owner-operated UAT smoke — gated instructions

No credential or personal data should be placed in Git, issue comments, chat, CI variables used as evidence, screenshots, or test fixtures.

1. Confirm through an authorized owner channel that a permitted UAT test record is available and that executing the Marriage Cases operation is approved and non-destructive. Do not proceed while the snapshot's read-only/non-destructive classification remains unresolved for the intended test.
2. In the protected GSIP AuthProfile administration flow, configure the UAT Marriage Cases profile using write-only vault inputs for the exact required secret names. Do not expose or export their plaintext values.
3. Keep the binding scoped to the Marriage Cases Service + UAT environment. Do not share the profile and do not bind it to Production.
4. Enable the UAT AuthProfile and UAT service environment only after the exact binding and secret generations have been verified. Leave Production disabled.
5. Through the generic Service Execution UI, select MOJ → Marriage Cases → UAT and enter only the authorized UAT test record through the sensitive request field. Never copy the value into logs/evidence.
6. Execute once. Capture only non-sensitive operational evidence such as the GSIP request/correlation identifier, HTTP status classification and pass/fail outcome. Do not capture raw request data, auth headers, tokens, or personal response content.
7. If target response/error schema evidence is obtained from the authorized official source, sanitize it before repository capture and update `api-129-marriage-cases.contract.json` plus this unit's acceptance. Until then, response/error/result-mapping items remain `DEFERRED_EXTERNAL — NOT PASS`.

P09 remains open. P10-P17 remain locked until formal P09 closure.
