# GSIP 0.1.2 UAT Checklist

Version: **0.1.2**

- [ ] Exact deployed version is 0.1.2.
- [ ] `/moj-uat` opens only for an authorized administrator.
- [ ] API 129 UAT setup requires write-only `x-api-key`, `username`, and `password` inputs.
- [ ] None of the three stored credentials is redisplayed in UI, logs, errors, or evidence.
- [ ] Authentication type is `TokenEndpoint`.
- [ ] Token endpoint is `/genToken` using `application/x-www-form-urlencoded`.
- [ ] `/genToken` receives the exact required username/password fields and `x-api-key`.
- [ ] Missing username, password, or API key fails closed before outbound token transport.
- [ ] Bearer token is obtained from response field `data` and is not shown in the UI/logs.
- [ ] `/marriageCasesAPIGEE` receives the same `x-api-key` and `Authorization: Bearer`.
- [ ] API 129 remains bound to exact UAT Service + Environment + AuthProfile scope with no implicit sharing.
- [ ] Other canonical MOJ services retain their documented service-specific token metadata and are not forced to require x-api-key unless their evidence says so.
- [ ] Production stays disabled and no Production URL, credentials, or auth semantics are inferred from UAT.
- [ ] No real Civil ID, API key, username, password, or Bearer token appears in committed evidence.
- [ ] All workflows on the exact PR head are terminal SUCCESS before merge.
- [ ] After merge, every workflow on the exact new `main` SHA is terminal SUCCESS before P16 closure reconciliation resumes.
