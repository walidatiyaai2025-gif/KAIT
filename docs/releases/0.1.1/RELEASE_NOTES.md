# GSIP 0.1.1 Hotfix Release Notes

GSIP 0.1.1 is a focused hotfix over the accepted 0.1.0 baseline. It adds an administrator-only MOJ UAT configuration workflow for API 129 so an owner-held `x-api-key` can be stored through the protected Secret Vault and the exact UAT service can be executed without SQL or browser-console workarounds.

The hotfix preserves the existing RBAC, secret-isolation, audit, request-history, installer, backup/restore, and Production fail-closed boundaries. It does not contain credentials, tokens, Civil IDs, or Production endpoint/authentication assumptions.

The live external MOJ UAT call remains owner-executed evidence and is not claimed as PASS by repository CI. `VERIFIED_FINAL_COMPLETE` remains forbidden until the governed final phase has exact final evidence.
