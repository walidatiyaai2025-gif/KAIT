# GSIP 0.1.2 Hotfix Release Notes

Version: **0.1.2**

This hotfix restores the authoritative MOJ API 129 token-exchange flow after the 0.1.1 UAT helper incorrectly converted API 129 to direct API-key authentication.

## Fixed

- MOJ API 129 UAT calls `/genToken` first with the required `x-api-key`, `username`, and `password`, reads the Bearer token from `data`, then calls `/marriageCasesAPIGEE` with the same `x-api-key` plus `Authorization: Bearer <token>`.
- The API 129 UAT administration flow requires all three credential values and stores them only through the protected Secret Vault. Values are write-only and are never redisplayed.
- Missing username, password, or API key keeps the authentication profile fail-closed; no empty credential placeholders are synthesized.
- The correction is scoped to API 129 UAT. Other MOJ services retain their own evidence-bound token paths, request formats, fields, and API-key requirements; no global authentication shape is inferred.
- Production remains isolated and disabled until its own official URL and authentication contract are supplied. UAT credentials or endpoints are never copied to Production.
- The existing generic token runtime continues to obtain and cache Bearer tokens by exact Service + Environment + AuthProfile + secret generation scope.

No credential, API key, Civil ID, Bearer token, or Production secret is committed in this release. External UAT success remains owner/external evidence and is not promoted to PASS by repository-only tests.
