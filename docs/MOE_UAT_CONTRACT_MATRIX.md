# MOE UAT contract matrix

Owner-supplied CAIT Swagger/OAS 3.0 evidence captured on 2026-09-10.

These contracts are UAT-only. No Production endpoint is inferred. The Swagger publishes test Basic-auth credentials, but GSIP deliberately does **not** commit or seed any username/password material. Credentials must be provisioned through the protected secret vault.

| GSIP service | UAT base URL | Operation | Input | Authentication |
| --- | --- | --- | --- | --- |
| MOE Last Active Record | `https://moe-uat.api-non-prod.cait.gov.kw/Student-API/v1/studentdata` | `GET /lastactive` | required integer query `cid` | HTTP Basic |
| MOE Last Student Record | `https://moe-uat.api-non-prod.cait.gov.kw/Student-API/v1/studentdata` | `GET /last` | required integer query `cid` | HTTP Basic |
| MOE Last Success Record | `https://moe-uat.api-non-prod.cait.gov.kw/Student-API/v1/studentdata` | `GET /lastsuccess` | required integer query `cid` | HTTP Basic |

## Response contracts

### Last Active Record

Successful response fields captured from Swagger: `cid`, `name`, `school`, `level`, `division`, `status`, `year`, `currentstatus`.

### Last Student Record

Successful response fields captured from Swagger: `cid`, `name`, `school`, `level`, `division`, `status`, `year`, `currentstatus`, `quitdate`.

### Last Success Record

Successful response fields captured from Swagger: `cid`, `name`, `school`, `level`, `division`, `status`, `year`, `currentstatus`.

The documented error model for all three operations is JSON `{ "message": "string" }`, with operation-specific documented `401`, `404`, and `500` responses.

## GSIP authentication behavior

Each service has an independent UAT AuthProfile and exact Service + Environment binding. The profile is intentionally seeded disabled and empty.

To provision a profile, create exactly these two protected secret slots:

- `X-GSIP-Basic-Username`
- `X-GSIP-Basic-Password`

The generic execution engine resolves those values from the existing protected vault. `BasicAuthenticationTransformHandler` then removes both internal carrier headers before network transport and creates the standard `Authorization: Basic <credentials>` header in memory for the outbound request. It clears the Authorization header after the send completes. A partial/malformed Basic credential pair fails closed locally and is not sent to the provider.

Automatic recognition of the owner-proven MOE operations is restricted to the exact HTTPS host/path on the default HTTPS port. A same-host request on a non-default port does not inherit the MOE Basic-auth scope and protected credential carriers are rejected before transport.

No username, password, Civil ID, Authorization value, or other personal identifier is committed to source control.

Production remains disabled with an empty endpoint until an authoritative Production contract is supplied.
