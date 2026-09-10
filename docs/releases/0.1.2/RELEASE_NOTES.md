# GSIP 0.1.2 Hotfix Release Notes

Version: **0.1.2**

This hotfix restores the authoritative MOJ token-exchange flow after the 0.1.1 UAT shortcut incorrectly converted API 129 to direct API-key authentication.

## Fixed

- MOJ API 129 UAT now calls `/genToken` first with `x-api-key` and environment credential fields, reads the Bearer token from `data`, then calls `/marriageCasesAPIGEE` with the same `x-api-key` plus `Authorization: Bearer <token>`.
- UAT username/password may be represented internally as protected empty placeholders; the wire request sends empty values when the UAT metadata marks those fields optional.
- Production remains isolated and disabled until its official URL and credentials are provided. Production token profiles continue to require real username/password plus API key unless explicitly documented otherwise.
- All current MOJ UAT token metadata is converged to the common token-endpoint + Bearer model with `x-api-key` required, while preserving service-specific token paths and extras.
- System Administrator execution entitlements are converged for the canonical MOJ services so the service selector no longer disappears for the bootstrap administrator.
- API keys remain write-only and are stored only through the protected Secret Vault. Bearer tokens are never persisted or redisplayed by the UAT configuration UI.

No credential, API key, Civil ID, Bearer token, or Production secret is committed in this release.
