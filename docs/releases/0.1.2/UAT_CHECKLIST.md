# GSIP 0.1.2 UAT Checklist

Version: **0.1.2**

- [ ] Exact deployed version is 0.1.2.
- [ ] `/moj-uat` opens for an authorized administrator.
- [ ] Only the UAT `x-api-key` is required from the administrator for API 129 setup.
- [ ] The stored API key is never redisplayed.
- [ ] Authentication type is `TokenEndpoint`.
- [ ] Token endpoint is `/genToken`.
- [ ] UAT username/password may be empty and are not exposed in the UI.
- [ ] `/genToken` receives `x-api-key` and the environment credential fields.
- [ ] Bearer token is obtained from response field `data` and is not shown in the UI/logs.
- [ ] `/marriageCasesAPIGEE` receives the same `x-api-key` and `Authorization: Bearer`.
- [ ] System Administrator can see Marriage Cases in `/execute` without SQL repair.
- [ ] Canonical MOJ UAT services use TokenEndpoint metadata with `x-api-key` required.
- [ ] Production stays disabled and no Production URL/credential is inferred.
- [ ] Production, when officially configured, uses real username + password + API key to obtain a token before service execution.
- [ ] No real Civil ID, API key, password, or Bearer token appears in committed evidence.
